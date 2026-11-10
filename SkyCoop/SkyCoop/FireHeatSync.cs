using System;
using System.Collections.Generic;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

public static class FireHeatSync
{
	private const float MaxFuelSeconds = 24 * 3600;
	private const float MaxHeat = 1000;
	private const long ExtinguishOperation = -2;
	private const string ExtinguishedSnapshot = "off";
	private readonly record struct FireKey(int Level, string Scene, string Guid, Vector3 Position);
	private sealed class Entry
	{
		internal DataStr.FireSourcesSync State;
		internal double Deadline;
		internal int Starter;
		internal bool ExpiredPublished;
		internal bool Extinguished;
		internal float EmberSeconds;
		internal int Simulator = -1;
	}

	private sealed class LocalFireClock
	{
		internal string Generation;
		internal int Minute;
		internal bool ReportedOff;
		internal bool WasOutOfFuel;
	}

	private static readonly Dictionary<FireKey, Entry> Authority = new();
	private static readonly Dictionary<string, string> AcceptedOperations = new();
	private static readonly Dictionary<FireKey, DataStr.FireSourcesSync> Snapshots = new();
	private static readonly Dictionary<int, DataStr.FireSourcesSync> Bound = new();
	private static readonly Dictionary<int, DataStr.FireSourcesSync> PendingStarts = new();
	private static readonly HashSet<int> NewIgnitions = new();
	private static readonly Dictionary<int, LocalFireClock> FireClocks = new();
	private static readonly Queue<DataStr.FireSourcesSync> Pending = new();
	private static Client serverSession;
	private static long revision;
	private static string localScene;
	private static double? nativeDelta;
	private static bool advancingCooking;
	private static bool suppressWind;

	private static bool Online => MyMod.ServerConfig.m_FireSync != 0 && (MyMod.iAmHost || MyMod.sendMyPosition);
	private static int latestMinute;
	private static int Minute => latestMinute = Math.Max(latestMinute, MyMod.MinutesFromStartServer);
	private static float Positive(float value) => float.IsFinite(value) ? Math.Max(0, value) : 0;
	private static FireKey Key(DataStr.FireSourcesSync data) => new(
		data.m_LevelId, data.m_LevelGUID, data.m_Guid,
		string.IsNullOrEmpty(data.m_Guid) ? data.m_Position : Vector3.zero);

	private static bool Local(DataStr.FireSourcesSync data)
		=> data.m_LevelId == MyMod.levelid && data.m_LevelGUID == MyMod.level_guid;

	internal static bool AllowCooking(Fire fire)
		=> fire == null || !Online || MyMod.ServerConfig.m_FireSync == 2 || fire.m_StartedByPlayer || !fire.IsBurning();

	private static DataStr.FireSourcesSync Capture(Fire fire)
	{
		ObjectGuid guid = fire.GetComponent<ObjectGuid>();
		HeatSource heat = fire.m_HeatSource;
		return new DataStr.FireSourcesSync
		{
			m_LevelId = MyMod.levelid,
			m_LevelGUID = MyMod.level_guid,
			m_Position = fire.transform.position,
			m_Rotation = fire.transform.rotation,
			m_IsCampfire = fire.m_Campfire != null,
			m_Guid = fire.m_Campfire == null && guid != null ? guid.Get() ?? "" : "",
			m_Fuel = Positive(fire.GetRemainingLifeTimeSeconds()),
			m_MaxHeat = Positive(fire.m_FuelHeatIncrease),
			m_HeatInnerRadius = heat != null ? Positive(heat.m_MaxTempIncreaseInnerRadius) : 0,
			m_HeatOuterRadius = heat != null ? Positive(heat.m_MaxTempIncreaseOuterRadius) : 0,
			m_HeatMasked = heat != null && heat.m_MaskTempIncrease,
			m_WorldMinute = Minute
		};
	}

	private static void ObserveServer()
	{
		Server.clients.TryGetValue(1, out var session);
		if (ReferenceEquals(serverSession, session))
			return;
		serverSession = session;
		Authority.Clear();
		AcceptedOperations.Clear();
	}

	internal static void ReceiveOperation(int sender, DataStr.FireSourcesSync request)
	{
		ObserveServer();
		if (!Online || !Guid.TryParseExact(request.m_Operation, "N", out _))
			return;

		if (AcceptedOperations.ContainsKey(request.m_Operation))
		{
			Acknowledge(sender, request);
			return;
		}

		if (sender != 0 && (sender >= MyMod.playersData.Count || MyMod.playersData[sender] == null
			|| MyMod.playersData[sender].m_LevelGuid != request.m_LevelGUID))
		{
			// A scene change may overtake an already queued interaction on UDP.
			// Remember the rejection so a retransmission cannot later apply it.
			AcceptedOperations.Add(request.m_Operation, "");
			Acknowledge(sender, request);
			return;
		}

		FireKey key = Key(request);
		Authority.TryGetValue(key, out var entry);
		bool live = entry != null && !entry.Extinguished
			&& entry.Deadline + entry.EmberSeconds > FireClockPolicy.SecondsAt(Minute);
		string generation = "";
		if (request.m_Revision == ExtinguishOperation)
		{
			if (entry != null && !entry.Extinguished && sender == entry.Simulator
				&& request.m_Generation == entry.State.m_Generation)
			{
				entry.Extinguished = true;
				entry.Deadline = Math.Min(entry.Deadline, FireClockPolicy.SecondsAt(Minute));
				generation = request.m_Generation;
				Publish(entry);
			}
		}
		else if (string.IsNullOrEmpty(request.m_Generation))
		{
			if (live)
			{
				generation = entry.State.m_Generation;
			}
			else if ((entry == null || request.m_Revision == -1)
				&& Positive(request.m_Fuel) > 0 && request.m_WorldMinute <= Minute + 1)
			{
				// Stamp the deadline at ignition, so a delayed retry cannot refill it.
				double deadline = FireClockPolicy.SecondsAt(Math.Min(request.m_WorldMinute, Minute))
					+ Math.Min(Positive(request.m_Fuel), MaxFuelSeconds);
				if (deadline > FireClockPolicy.SecondsAt(Minute))
				{
					generation = request.m_Operation;
					entry = new Entry
					{
						State = request,
						Deadline = deadline,
						Starter = sender,
						EmberSeconds = Math.Min(Positive(request.m_Capacity), 3600)
					};
					request.m_Generation = generation;
					request.m_MaxHeat = Math.Clamp(Positive(request.m_MaxHeat), 0, MaxHeat);
					request.m_HeatInnerRadius = Positive(request.m_HeatInnerRadius);
					request.m_HeatOuterRadius = Positive(request.m_HeatOuterRadius);
					Authority[key] = entry;
					Publish(entry);
				}
			}
			else if (entry != null)
			{
				// The requester may have loaded an old local copy from a native save.
				Publish(entry);
			}
		}
		else if (live && (MyMod.ServerConfig.m_FireSync == 2 || entry.Starter == sender))
		{
			string target = AcceptedOperations.TryGetValue(request.m_Generation, out var alias)
				? alias : request.m_Generation;
			if (target == entry.State.m_Generation && float.IsFinite(request.m_Fuel))
			{
				float capacity = Math.Clamp(Positive(request.m_Capacity), 1, MaxFuelSeconds);
				entry.Deadline = FireClockPolicy.ChangeDeadline(entry.Deadline, Minute, request.m_Fuel, capacity);
				entry.State.m_MaxHeat = Math.Clamp(entry.State.m_MaxHeat + Positive(request.m_MaxHeat), 0, MaxHeat);
				entry.State.m_HeatInnerRadius = Math.Max(entry.State.m_HeatInnerRadius, Positive(request.m_HeatInnerRadius));
				entry.State.m_HeatOuterRadius = Math.Max(entry.State.m_HeatOuterRadius, Positive(request.m_HeatOuterRadius));
				generation = target;
				Publish(entry);
			}
		}

		AcceptedOperations.Add(request.m_Operation, generation);
		Acknowledge(sender, request);
	}

	private static void Acknowledge(int sender, DataStr.FireSourcesSync request)
	{
		if (sender == 0)
			Acknowledge(request.m_Operation);
		else
			ServerSend.FIREFUEL(sender, request, toEveryOne: false, OnlyFor: sender);
	}

	internal static void Acknowledge(string operation)
	{
		if (Pending.TryPeek(out var first) && first.m_Operation == operation)
			Pending.Dequeue();
	}

	private static void Publish(Entry entry)
	{
		var snapshot = entry.State.Copy();
		entry.Simulator = FindSimulator(snapshot);
		snapshot.m_WorldMinute = Minute;
		snapshot.m_Fuel = (float)(entry.Deadline - FireClockPolicy.SecondsAt(Minute));
		// Capacity is request-only. On snapshots this field carries the peer
		// whose native wind/extinguish result is accepted for this scene.
		snapshot.m_Capacity = entry.Simulator;
		snapshot.m_Operation = entry.Extinguished ? ExtinguishedSnapshot : "";
		entry.ExpiredPublished = entry.Extinguished
			|| entry.Deadline + entry.EmberSeconds + 60 <= FireClockPolicy.SecondsAt(Minute);
		snapshot.m_Revision = ++revision;
		ReceiveSnapshot(snapshot);
		ServerSend.FIRE(0, snapshot, toEveryOne: true);
	}

	private static int FindSimulator(DataStr.FireSourcesSync data)
	{
		if (!MyMod.DedicatedServerAppMode && MyMod.ApplyOtherCampfires && Local(data))
			return 0;
		for (int id = 1; id < MyMod.playersData.Count; id++)
		{
			if (MyMod.playersData[id]?.m_LevelGuid == data.m_LevelGUID
				&& Server.clients.TryGetValue(id, out var client) && client.Ready)
				return id;
		}
		return -1;
	}

	internal static void ReceiveSnapshot(DataStr.FireSourcesSync data)
	{
		if (!Online || !Local(data) || data.m_Revision <= 0 || !float.IsFinite(data.m_Fuel))
			return;
		if (!MyMod.iAmHost) SyncCookingClock(data.m_WorldMinute);
		latestMinute = Math.Max(latestMinute, data.m_WorldMinute);
		FireKey key = Key(data);
		if (!Snapshots.TryGetValue(key, out var old) || data.m_Revision > old.m_Revision)
			Snapshots[key] = data;
	}

	private static float Remaining(DataStr.FireSourcesSync data)
		=> Remaining(data, Minute);

	private static float Remaining(DataStr.FireSourcesSync data, int minute)
		=> FireClockPolicy.Remaining(FireClockPolicy.SecondsAt(data.m_WorldMinute) + data.m_Fuel, minute);

	internal static void SyncCookingClock(int minute) => AdvanceCooking(Minute, minute);

	internal static void AdvanceCooking(int fromMinute, int toMinute)
	{
		if (!Online || !MyMod.ApplyOtherCampfires || MyMod.DedicatedServerAppMode || !GearCatalogIndex.InWorld
			|| MyMod.LoadingScreenIsOn || fromMinute < 0 || (long)toMinute - fromMinute <= 1)
			return;

		var previousDelta = nativeDelta;
		advancingCooking = true;
		try
		{
			foreach (var pot in UnityEngine.Object.FindObjectsOfType<CookingPotItem>())
			{
				if (pot == null || !pot.isActiveAndEnabled || !HasCookingContents(pot)) continue;
				var fire = pot.m_FireBeingUsed;
				if (fire == null || !fire.IsBurning() || fire.IsEmbers()) continue;
				int id = pot.GetInstanceID();
				int start = CookingUpdate.Clocks.TryGetValue(id, out int last) ? Math.Max(fromMinute, last) : fromMinute;
				double seconds = FireClockPolicy.SecondsAt((int)Math.Clamp((long)toMinute - start, 0, MaxFuelSeconds / 60));
				if (!fire.m_IsPerpetual)
					seconds = Math.Min(seconds, Bound.TryGetValue(fire.GetInstanceID(), out var state)
						? Remaining(state, start) : Positive(fire.GetRemainingLifeTimeSeconds()));
				// Native cooking changes one phase per update (e.g. melting, then boiling).
				while (seconds > 0 && pot != null && HasCookingContents(pot) && pot.m_CookingState != CookingPotItem.CookingState.Ruined)
				{
					double step = Math.Min(60, seconds);
					// The native update can inline its time conversion. Advance the active phase counter directly.
					float hours = (float)(step / 3600);
					if (pot.m_CookingState == CookingPotItem.CookingState.Ready) pot.m_GracePeriodElapsedHours += hours;
					else pot.m_CookingElapsedHours += hours;
					nativeDelta = 0;
					pot.Update();
					seconds -= step;
				}
				if (pot != null) CookingUpdate.Clocks[id] = toMinute;
			}
		}
		finally
		{
			advancingCooking = false;
			nativeDelta = previousDelta;
		}
	}

	private static bool HasCookingContents(CookingPotItem pot) => pot.m_GearItemBeingCooked != null
		|| pot.m_LitersSnowBeingMelted.ToLiters() > 0f || pot.m_LitersWaterBeingBoiled.ToLiters() > 0f;

	private static string EnsureStarted(Fire fire)
	{
		int id = fire.GetInstanceID();
		if (Bound.TryGetValue(id, out var snapshot))
			return snapshot.m_Generation;
		if (PendingStarts.TryGetValue(id, out var pending))
			return pending.m_Operation;
		if (!fire.m_StartedByPlayer || fire.m_FireState != FireState.FullBurn)
			return "";
		var request = Capture(fire);
		if (request.m_Fuel <= 0)
			return "";
		request.m_Operation = Guid.NewGuid().ToString("N");
		request.m_Revision = NewIgnitions.Remove(id) ? -1 : 0;
		request.m_Capacity = fire.m_UseEmbers ? Positive(fire.m_EmberDurationSecondsTOD) : 0;
		PendingStarts.Add(id, request);
		Pending.Enqueue(request);
		return request.m_Operation;
	}

	internal static DataStr.FireSourcesSync BeforeFuelChange(Fire fire)
	{
		if (!Online || !MyMod.ApplyOtherCampfires || nativeDelta.HasValue || fire.m_IsPerpetual
			|| (MyMod.ServerConfig.m_FireSync != 2 && !fire.m_StartedByPlayer))
			return null;
		string generation = EnsureStarted(fire);
		if (generation == "")
			return null;
		var before = Capture(fire);
		before.m_Generation = generation;
		return before;
	}

	internal static void AfterFuelChange(Fire fire, DataStr.FireSourcesSync before, bool add)
	{
		if (before == null)
			return;
		var after = Capture(fire);
		float delta = after.m_Fuel - before.m_Fuel;
		float heatDelta = Math.Max(0, after.m_MaxHeat - before.m_MaxHeat);
		if (add ? delta <= 0 && heatDelta <= 0 : delta >= 0)
			return;
		after.m_Fuel = delta;
		after.m_MaxHeat = heatDelta;
		after.m_Generation = before.m_Generation;
		after.m_Capacity = GameManager.GetFireManagerComponent().m_MaxDurationHoursOfFire * 3600;
		after.m_Operation = Guid.NewGuid().ToString("N");
		Pending.Enqueue(after);
	}

	internal static void Tick()
	{
		if (!Online)
		{
			latestMinute = 0;
			localScene = null;
			Snapshots.Clear();
			Bound.Clear();
			PendingStarts.Clear();
			NewIgnitions.Clear();
			FireClocks.Clear();
			Pending.Clear();
			CookingUpdate.Clocks.Clear();
			return;
		}

		if (MyMod.iAmHost)
		{
			ObserveServer();
			foreach (var entry in Authority.Values)
			{
				if (!entry.ExpiredPublished)
					Publish(entry);
			}
		}

		if (localScene != MyMod.level_guid)
		{
			localScene = MyMod.level_guid;
			foreach (var key in new List<FireKey>(Snapshots.Keys))
				if (key.Scene != localScene)
					Snapshots.Remove(key);
			Bound.Clear();
			PendingStarts.Clear();
			NewIgnitions.Clear();
			FireClocks.Clear();
			CookingUpdate.Clocks.Clear();
		}

		if (!MyMod.DedicatedServerAppMode && MyMod.ApplyOtherCampfires
			&& WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_name) && FireManager.m_Fires != null)
			UpdateScene();

		if (!Pending.TryPeek(out var request))
			return;
		if (MyMod.iAmHost)
			ReceiveOperation(0, request);
		else if (MyMod.sendMyPosition)
		{
			using var packet = new Packet(84);
			packet.Write(request);
			MyMod.SendUDPData(packet);
		}
	}

	private static void UpdateScene()
	{
		var seen = new HashSet<FireKey>();
		Bound.Clear();
		for (int i = 0; i < FireManager.m_Fires.Count; i++)
		{
			Fire fire = FireManager.m_Fires[i];
			if (fire == null || fire.m_IsPerpetual)
				continue;
			int id = fire.GetInstanceID();
			FireKey key = Key(Capture(fire));
			if (Snapshots.TryGetValue(key, out var settled) && Remaining(settled) <= 0
				&& PendingStarts.TryGetValue(id, out var pending) && pending.m_Revision == 0)
				PendingStarts.Remove(id);
			if (Snapshots.TryGetValue(key, out var data) && data.m_Operation != ExtinguishedSnapshot
				&& (Remaining(data) > 0 || FireClocks.TryGetValue(id, out var clock) && clock.Generation == data.m_Generation))
			{
				seen.Add(key);
				PendingStarts.Remove(id);
				Bind(fire, data);
			}
			else if (Snapshots.ContainsKey(key) && !NewIgnitions.Contains(id) && !PendingStarts.ContainsKey(id))
			{
				if (fire.m_FireState != FireState.Off)
					Expire(fire);
			}
			else
			{
				if (fire.m_FireState == FireState.Off)
					PendingStarts.Remove(id);
				EnsureStarted(fire);
			}
		}
		foreach (var pair in new List<KeyValuePair<FireKey, DataStr.FireSourcesSync>>(Snapshots))
		{
			var data = pair.Value;
			if (Remaining(data) <= 0)
			{
				continue;
			}
			if (seen.Contains(pair.Key) || !data.m_IsCampfire)
				continue;
			Fire fire = GameManager.GetFireManagerComponent().InstantiateCampFire();
			if (fire == null)
				continue;
			fire.gameObject.name = "CAMPFIRE";
			fire.transform.SetPositionAndRotation(data.m_Position, data.m_Rotation);
			fire.m_SkipSerialization = true;
			Bind(fire, data);
		}
	}

	private static void Bind(Fire fire, DataStr.FireSourcesSync data)
	{
		int id = fire.GetInstanceID();
		Bound[id] = data;
		bool firstBinding = !FireClocks.TryGetValue(id, out var clock) || clock.Generation != data.m_Generation;
		if (firstBinding)
		{
			clock = new LocalFireClock { Generation = data.m_Generation, Minute = Minute };
			FireClocks[id] = clock;
		}
		if (clock.ReportedOff)
			return;
		bool fueled = Remaining(data) > 0;
		bool relitFromEmbers = clock.WasOutOfFuel && fueled;
		if (firstBinding || relitFromEmbers)
		{
			if (fire.m_FireState != FireState.FullBurn || fire.IsEmbers())
			{
				fire.m_EmberTimer = 0;
				fire.FireStateSet(FireState.FullBurn);
				if (fire.m_FX != null)
					fire.m_FX.TriggerStage(FireState.FullBurn, shouldPlay: true, stopOtherStages: true);
				fire.PlayFireLoop(100f);
				if (fire.m_Campfire != null && fire.m_Campfire.m_State != CampfireState.Lit)
					fire.m_Campfire.SetState(CampfireState.Lit);
			}
		}
		clock.WasOutOfFuel = !fueled;
		if (MyMod.ServerConfig.m_FireSync == 2)
			fire.m_StartedByPlayer = true; // Native interaction flag, never network authority.
		if (fueled && (firstBinding || relitFromEmbers))
			SetTimer(fire, data, Minute);
		if (fueled && fire.m_FireState == FireState.FullBurn && !fire.IsEmbers() && fire.m_HeatSource != null)
		{
			HeatSource heat = fire.m_HeatSource;
			heat.enabled = true;
			heat.TurnOn();
			heat.m_MaskTempIncrease = data.m_HeatMasked;
			heat.m_MaxTempIncrease = Positive(data.m_MaxHeat);
			heat.m_MaxTempIncreaseInnerRadius = Positive(data.m_HeatInnerRadius);
			heat.m_MaxTempIncreaseOuterRadius = Math.Max(heat.m_MaxTempIncreaseInnerRadius, Positive(data.m_HeatOuterRadius));
			heat.m_TempIncrease = heat.m_MaxTempIncrease;
		}
		fire.m_FuelHeatIncrease = Positive(data.m_MaxHeat);
	}

	private static void SetTimer(Fire fire, DataStr.FireSourcesSync data, int minute)
	{
		fire.m_MaxOnTODSeconds = fire.m_ElapsedOnTODSeconds + Remaining(data, minute);
	}

	private static void Expire(Fire fire)
	{
		fire.TurnOffImmediate();
		if (fire.m_HeatSource != null)
			fire.m_HeatSource.TurnOffImmediate();
	}

	[HarmonyPatch(typeof(Fire), nameof(Fire.FireStateSet))]
	private static class FireIgnition
	{
		private static void Postfix(Fire __instance, FireState __0)
		{
			if (Online && __0 == FireState.FullBurn && !Bound.ContainsKey(__instance.GetInstanceID()))
				NewIgnitions.Add(__instance.GetInstanceID());
		}
	}

	[HarmonyPatch(typeof(Fire), "Update")]
	private static class FireUpdate
	{
		private static void Prefix(Fire __instance, out (double? Delta, bool SuppressWind, DataStr.FireSourcesSync Data) __state)
		{
			__state = (nativeDelta, suppressWind, null);
			if (!Online || !MyMod.ApplyOtherCampfires)
				return;
			int id = __instance.GetInstanceID();
			if (!Bound.TryGetValue(id, out var data) || !FireClocks.TryGetValue(id, out var clock))
			{
				if (PendingStarts.TryGetValue(id, out var start))
				{
					if (Remaining(start) > 0)
						SetTimer(__instance, start, Minute);
					else if (__instance.m_FireState != FireState.Off)
						Expire(__instance);
					nativeDelta = 0;
					suppressWind = true;
				}
				return;
			}

			if (clock.ReportedOff)
				return;
			int minute = Minute;
			int previous = clock.Minute;
			clock.Minute = minute;
			suppressWind = (int)data.m_Capacity != (MyMod.iAmHost ? 0 : ClientUser.myId);
			__state.Data = suppressWind ? null : data;
			double deadline = FireClockPolicy.SecondsAt(data.m_WorldMinute) + data.m_Fuel;
			float emberSeconds = __instance.m_UseEmbers ? Positive(__instance.m_EmberDurationSecondsTOD) : 0;
			if (__instance.m_FireState != FireState.Off
				&& FireClockPolicy.SecondsAt(minute) >= deadline + Math.Min(emberSeconds, 3600) + 60)
			{
				Expire(__instance);
				nativeDelta = 0;
				return;
			}
			if (!__instance.IsEmbers() && Remaining(data, previous) > 0)
				SetTimer(__instance, data, previous);
			nativeDelta = FireClockPolicy.SecondsAt(Math.Max(0, minute - previous));
		}

		private static void Postfix(Fire __instance, (double? Delta, bool SuppressWind, DataStr.FireSourcesSync Data) __state)
		{
			var data = __state.Data;
			if (data == null || __instance.IsEmbers()
				|| __instance.m_FireState != FireState.Blownout && __instance.m_FireState != FireState.Off
				|| !FireClocks.TryGetValue(__instance.GetInstanceID(), out var clock))
				return;
			clock.ReportedOff = true;
			var request = data.Copy();
			request.m_Operation = Guid.NewGuid().ToString("N");
			request.m_Revision = ExtinguishOperation;
			Pending.Enqueue(request);
		}

		private static void Finalizer((double? Delta, bool SuppressWind, DataStr.FireSourcesSync Data) __state)
			=> (nativeDelta, suppressWind) = (__state.Delta, __state.SuppressWind);
	}

	[HarmonyPatch(typeof(Fire), nameof(Fire.MaybeBlowOutFromWind))]
	private static class FireWind
	{
		private static bool Prefix() => !suppressWind;
	}

	[HarmonyPatch(typeof(CookingPotItem), "Update")]
	private static class CookingUpdate
	{
		internal static readonly Dictionary<int, int> Clocks = new();

		private static void Prefix(CookingPotItem __instance, out double? __state)
		{
			__state = nativeDelta;
			if (advancingCooking) return;
			int id = __instance.GetInstanceID();
			if (!Online || !MyMod.ApplyOtherCampfires || __instance.m_FireBeingUsed == null
				|| !Bound.ContainsKey(__instance.m_FireBeingUsed.GetInstanceID()))
			{
				Clocks.Remove(id);
				return;
			}
			int minute = Minute;
			nativeDelta = Clocks.TryGetValue(id, out int previous)
				? FireClockPolicy.SecondsAt(Math.Max(0, minute - previous)) : 0;
			Clocks[id] = minute;
		}

		private static void Finalizer(double? __state) => nativeDelta = __state;
	}

	[HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.OnDestroy))]
	private static class CookingDestroyed
	{
		private static void Prefix(CookingPotItem __instance) => CookingUpdate.Clocks.Remove(__instance.GetInstanceID());
	}

	[HarmonyPatch]
	private static class NativeTimeConversion
	{
		private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
		{
			foreach (string method in new[] { "GetTODSeconds", "GetTODSecondsUnscaled", "GetTODMinutes",
				"GetTODMinutesUnscaled", "GetTODHours", "GetTODHoursUnscaled" })
				yield return AccessTools.Method(typeof(TimeOfDay), method, new[] { typeof(float) });
		}

		private static bool Prefix(System.Reflection.MethodBase __originalMethod, float __0, ref float __result)
		{
			if (!nativeDelta.HasValue)
				return true;
			double units = __originalMethod.Name.StartsWith("GetTODHours", StringComparison.Ordinal) ? 3600
				: __originalMethod.Name.StartsWith("GetTODMinutes", StringComparison.Ordinal) ? 60 : 1;
			__result = __0 > 0 ? (float)(nativeDelta.Value / units) : 0;
			return false;
		}
	}
}
