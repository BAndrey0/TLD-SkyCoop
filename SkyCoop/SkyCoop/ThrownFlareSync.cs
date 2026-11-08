using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class ThrownFlareSync
{
	private sealed class Visual
	{
		public GameObject Owner;

		public GameObject Effects;

		public Light Light;

		public int Deadline;
		public Loop Sound;
	}

	[HarmonyPatch(typeof(MyMod), "OnUpdate")]
	private static class Update
	{
		private static void Postfix()
		{
			Tick();
		}
	}

	private sealed class Loop
	{
		internal GameObject Emitter;
		internal string Event;
		internal uint Playing;
	}

	private static readonly Dictionary<int, Loop> Held = new();
	private static float nextSoundUpdate;
	private static AkAudioListener[] listeners = Array.Empty<AkAudioListener>();
	private static float nextListenerScan;
	private static readonly List<Visual> visuals = new List<Visual>();


	public static int VisualCount => visuals.Count;

	public static bool IsFlare(GearItem gear)
	{
		if (gear != null)
		{
			return gear.m_FlareItem != null;
		}
		return false;
	}

	private static void Tick()
	{
		float now = Time.realtimeSinceStartup;
		bool updateAudio = now >= nextSoundUpdate;
		if (updateAudio)
		{
			nextSoundUpdate = now + .2f;
			foreach (var loop in Held.Values) UpdateLoop(loop);
		}
		for (int num = visuals.Count - 1; num >= 0; num--)
		{
			Visual visual = visuals[num];
			if (visual.Owner == null)
			{
				StopLoop(visual.Sound);
				visuals.RemoveAt(num);
			}
			else
			{
				bool flag = MyMod.MinutesFromStartServer < visual.Deadline;
				if (visual.Effects != null && visual.Effects.activeSelf != flag)
				{
					visual.Effects.SetActive(flag);
				}
				if (visual.Light != null)
				{
					visual.Light.enabled = flag;
				}
				if (flag && updateAudio) UpdateLoop(visual.Sound);
				if (!flag)
				{
					StopLoop(visual.Sound);
				visuals.RemoveAt(num);
				}
			}
		}
	}

	public static void FillDropExtra(GearItem gear, DataStr.ExtraDataForDroppedGear extra)
	{
		if (IsFlare(gear) && extra != null)
		{
			FlareItem flareItem = gear.m_FlareItem;
			bool flag = flareItem.IsBurning();
			extra.m_Variant = flag ? 1 : flareItem.IsBurnedOut() ? 2 : 0;
			float num = Math.Max(0f, flareItem.GetModifiedBurnLifetimeMinutes() - flareItem.m_ElapsedBurnMinutes);
			extra.m_FlareRemainingMinutes = flag ? num : -1f;
			extra.m_GoalTime = (flag ? (MyMod.MinutesFromStartServer + (int)Math.Ceiling(num)) : 0);
		}
	}

	public static void PrepareReplica(GameObject obj, DataStr.ExtraDataForDroppedGear extra)
	{
		if (obj == null || extra == null || extra.m_Variant != 1 || extra.m_GoalTime <= MyMod.MinutesFromStartServer)
		{
			return;
		}
		FlareItem component = obj.GetComponent<FlareItem>();
		if (!(component == null))
		{
			component.OnIgniteComplete();
			Visual visual = new Visual
			{
				Owner = obj,
				Deadline = extra.m_GoalTime,
				Sound = NewLoop(obj, component.m_LoopWithoutIgniteAudio)
			};
			if (component.m_FXGameObject != null && component.m_FXGameObject != obj)
			{
				visual.Effects = UnityEngine.Object.Instantiate(component.m_FXGameObject, obj.transform, worldPositionStays: false);
				visual.Effects.name = "SkyCoopFlareEffects";
				visual.Effects.SetActive(value: true);
				foreach (var effect in visual.Effects.GetComponentsInChildren<ParticleSystem>(true))
					if (effect.gameObject.activeInHierarchy) effect.Play();
			}
			Light light = (GameManager.GetWeatherComponent()?.UseOutdoorLightingForLightSources() == true ? component.m_LightOutdoor : component.m_LightIndoor);
			if (light != null)
			{
				GameObject gameObject = new GameObject("SkyCoopFlareLight");
				gameObject.transform.SetParent(obj.transform, worldPositionStays: false);
				gameObject.transform.position = light.transform.position;
				visual.Light = gameObject.AddComponent<Light>();
				visual.Light.type = light.type;
				visual.Light.color = light.color;
				visual.Light.range = light.range;
				visual.Light.intensity = light.intensity;
				visual.Light.shadows = light.shadows;
				visual.Light.enabled = true;
			}
			component.StopLoopingAudio();
			component.HideEffects(hide: true);
			if (component.m_FlareCapMeshObject != null)
			{
				component.m_FlareCapMeshObject.SetActive(value: false);
			}
			visuals.Add(visual);
			UpdateLoop(visual.Sound);
		}
	}

	internal static GameObject CreateFlightReplica(GameObject prefab, Vector3 position, Quaternion rotation)
	{
		var obj = UnityEngine.Object.Instantiate(prefab, position, rotation);
		foreach (var script in obj.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
		foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
		foreach (var body in obj.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
		PrepareReplica(obj, new DataStr.ExtraDataForDroppedGear { m_Variant = 1, m_GoalTime = int.MaxValue });
		return obj;
	}

	internal static void UpdateHeld(Comps.MultiplayerPlayer player)
	{
		int id = player.m_ID;
		string name = player.m_HoldingItem;
		bool burning = player.isActiveAndEnabled && player.m_LightSourceOn && (name == "GEAR_FlareA" || name == "GEAR_BlueFlare");
		if (Held.TryGetValue(id, out var previous))
		{
			if (burning && previous.Emitter != null && previous.Emitter.transform.parent == player.transform) return;
			StopLoop(previous);
			if (previous.Emitter != null) UnityEngine.Object.Destroy(previous.Emitter);
			Held.Remove(id);
		}
		if (!burning) return;
		var flare = MyMod.GetGearItemPrefab(name)?.m_FlareItem;
		var loop = NewLoop(player.gameObject, flare?.m_LoopWithoutIgniteAudio);
		if (loop != null) Held[id] = loop;
	}

	private static Loop NewLoop(GameObject parent, string sound)
	{
		if (string.IsNullOrEmpty(sound)) return null;
		var emitter = new GameObject("TLD Coop flare audio");
		emitter.transform.SetParent(parent.transform, false);
		return new Loop { Emitter = emitter, Event = sound };
	}

	private static void StopLoop(Loop loop)
	{
		if (loop == null || loop.Playing == 0) return;
		AkSoundEngine.StopPlayingID(loop.Playing, 150);
		loop.Playing = 0;
	}

	private static void UpdateLoop(Loop loop)
	{
		var listener = GameManager.GetPlayerTransform();
		if (loop == null) return;
		if (!MyMod.InOnline() || MyMod.LoadingScreenIsOn || loop.Emitter == null || !loop.Emitter.activeInHierarchy || listener == null)
		{ StopLoop(loop); return; }
		float distance = Vector3.Distance(listener.position, loop.Emitter.transform.position);
		float volume = distance < 3f ? 1f : distance < 7f ? .55f : distance < 12f ? .24f : distance < 18f ? .08f : 0f;
		if (volume == 0f) { StopLoop(loop); return; }
		if (Time.realtimeSinceStartup >= nextListenerScan)
		{
			nextListenerScan = Time.realtimeSinceStartup + 2f;
			var active = UnityEngine.Object.FindObjectsOfType<AkAudioListener>();
			listeners = new AkAudioListener[active.Length];
			for (int i = 0; i < active.Length; i++) listeners[i] = active[i];
		}
		if (loop.Playing == 0) AkSoundEngine.RegisterGameObj(loop.Emitter);
		foreach (var audioListener in listeners)
			if (audioListener != null) AkSoundEngine.SetGameObjectOutputBusVolume(loop.Emitter, audioListener.gameObject, volume);
		if (loop.Playing == 0) loop.Playing = AkSoundEngine.PostEvent(loop.Event, loop.Emitter);
	}

	internal static float BurningElapsed(GearItem gear)
	{
		var flare = gear?.m_FlareItem;
		return flare != null && flare.IsBurning() && float.IsFinite(flare.m_ElapsedBurnMinutes) ? Math.Max(0f, flare.m_ElapsedBurnMinutes) : -1f;
	}

	internal static void PreserveThrowBurn(GearItem gear, float elapsed)
	{
		var flare = gear?.m_FlareItem;
		if (flare == null || elapsed < 0f) return;
		if (elapsed >= flare.GetModifiedBurnLifetimeMinutes()) { flare.ForceBurnout(); return; }
		if (flare.IsBurning()) elapsed = Math.Max(elapsed, flare.m_ElapsedBurnMinutes);
		else flare.OnIgniteComplete();
		flare.m_State = FlareState.Burning;
		flare.m_ElapsedBurnMinutes = elapsed;
	}

	public static void ApplyPickupBurn(GearItem gear, DataStr.ExtraDataForDroppedGear extra)
	{
		if (!IsFlare(gear) || extra == null) return;
		var flare = gear.m_FlareItem;
		if (extra.m_Variant == 2) { flare.ForceBurnout(); return; }
		if (extra.m_Variant != 1) return;
		float remaining = float.IsFinite(extra.m_FlareRemainingMinutes) && extra.m_FlareRemainingMinutes > 0f
			? extra.m_FlareRemainingMinutes - Math.Max(0, MyMod.MinutesFromStartServer - extra.m_DroppedTime)
			: extra.m_GoalTime - MyMod.MinutesFromStartServer;
		if (remaining <= 0f) { flare.ForceBurnout(); return; }
		if (!flare.IsBurning()) flare.OnIgniteComplete();
		flare.m_State = FlareState.Burning;
		flare.m_ElapsedBurnMinutes = Math.Max(0f, flare.GetModifiedBurnLifetimeMinutes() - remaining);
	}
}
