using System;
using System.Collections.Generic;
using System.Text;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.AI;
using Il2CppTLD.BigCarry;
using Il2CppTLD.Trader;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class FarTerritory
{
	public static class CougarSync
	{
		private static CougarManager s_Manager;

		private static float s_NextPush;

		private static string s_LastSnapshot = "";

		private const float PushIntervalSeconds = 5f;

		public static CougarManager GetManager()
		{
			if (s_Manager == null)
			{
				s_Manager = UnityEngine.Object.FindObjectOfType<CougarManager>();
			}
			return s_Manager;
		}

		public static void OnSceneChanged()
		{
			s_Manager = null;
			s_LastSnapshot = "";
		}

		public static DataStr.CougarStateSync Capture()
		{
			DataStr.CougarStateSync cougarStateSync = new DataStr.CougarStateSync();
			CougarManager manager = GetManager();
			if (manager == null)
			{
				return cougarStateSync;
			}
			try
			{
				cougarStateSync.m_Enabled = manager.IsEnabled;
				cougarStateSync.m_ActiveRegionName = manager.m_ActiveRegionName ?? "";
				Il2CppSystem.Collections.Generic.Dictionary<string, int> currentThreatLevelByRegion = manager.m_CurrentThreatLevelByRegion;
				Il2CppSystem.Collections.Generic.Dictionary<string, float> currentThreatCooldownByRegion = manager.m_CurrentThreatCooldownByRegion;
				if (currentThreatLevelByRegion != null)
				{
					Il2CppSystem.Collections.Generic.Dictionary<string, int>.Enumerator enumerator = currentThreatLevelByRegion.GetEnumerator();
					while (enumerator.MoveNext())
					{
						Il2CppSystem.Collections.Generic.KeyValuePair<string, int> current = enumerator.Current;
						float value = 0f;
						currentThreatCooldownByRegion?.TryGetValue(current.Key, out value);
						cougarStateSync.m_RegionNames.Add(current.Key);
						cougarStateSync.m_ThreatLevels.Add(current.Value);
						cougarStateSync.m_ThreatCooldowns.Add(value);
					}
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[SkyCoop] Could not read the cougar state: " + ex.Message);
			}
			return cougarStateSync;
		}

		public static void Apply(DataStr.CougarStateSync state)
		{
			CougarManager manager = GetManager();
			if (manager == null || state == null)
			{
				return;
			}
			try
			{
				if (state.m_Enabled)
				{
					manager.IsEnabled = true;
				}
				Il2CppSystem.Collections.Generic.Dictionary<string, int> dictionary = manager.m_CurrentThreatLevelByRegion;
				Il2CppSystem.Collections.Generic.Dictionary<string, float> dictionary2 = manager.m_CurrentThreatCooldownByRegion;
				if (dictionary == null)
				{
					dictionary = (manager.m_CurrentThreatLevelByRegion = new Il2CppSystem.Collections.Generic.Dictionary<string, int>());
				}
				if (dictionary2 == null)
				{
					dictionary2 = (manager.m_CurrentThreatCooldownByRegion = new Il2CppSystem.Collections.Generic.Dictionary<string, float>());
				}
				for (int i = 0; i < state.m_RegionNames.Count; i++)
				{
					string key = state.m_RegionNames[i];
					if (!dictionary.TryGetValue(key, out var value) || state.m_ThreatLevels[i] > value)
					{
						dictionary[key] = state.m_ThreatLevels[i];
					}
					if (!dictionary2.TryGetValue(key, out var value2) || state.m_ThreatCooldowns[i] > value2)
					{
						dictionary2[key] = state.m_ThreatCooldowns[i];
					}
				}
				s_LastSnapshot = Fingerprint(Capture());
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[SkyCoop] Could not apply the cougar state: " + ex.Message);
			}
		}

		private static string Fingerprint(DataStr.CougarStateSync state)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(state.m_Enabled).Append('|');
			for (int i = 0; i < state.m_RegionNames.Count; i++)
			{
				stringBuilder.Append(state.m_RegionNames[i]).Append(':').Append(state.m_ThreatLevels[i])
					.Append(';');
			}
			return stringBuilder.ToString();
		}

		public static void MaybePush()
		{
			if (Time.time < s_NextPush)
			{
				return;
			}
			s_NextPush = Time.time + 5f;
			if (GetManager() == null)
			{
				return;
			}
			DataStr.CougarStateSync cougarStateSync = Capture();
			string text = Fingerprint(cougarStateSync);
			if (text == s_LastSnapshot)
			{
				return;
			}
			s_LastSnapshot = text;
			if (MyMod.iAmHost)
			{
				ServerSend.COUGARSTATE(cougarStateSync);
			}
			else if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(190))
				{
					packet.Write(cougarStateSync);
					MyMod.SendUDPData(packet);
				}
			}
		}
	}

	public static class TraderSync
	{
		private static TraderManager s_Manager;

		private static float s_NextPush;

		private static string s_LastPushed = "";

		private static bool s_Applying;

		private const float PushIntervalSeconds = 5f;

		public static TraderManager GetManager()
		{
			if (s_Manager == null)
			{
				s_Manager = UnityEngine.Object.FindObjectOfType<TraderManager>();
			}
			return s_Manager;
		}

		public static void OnSceneChanged()
		{
			s_Manager = null;
			s_LastPushed = "";
		}

		public static string Capture()
		{
			TraderManager manager = GetManager();
			if (manager == null || manager.m_CurrentState == null)
			{
				return "";
			}
			try
			{
				return Utils.SerializeObject(manager.m_CurrentState);
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[SkyCoop] Could not read the trader state: " + ex.Message);
				return "";
			}
		}

		public static void Apply(string serialized)
		{
			TraderManager manager = GetManager();
			if (manager == null || string.IsNullOrEmpty(serialized))
			{
				return;
			}
			try
			{
				s_Applying = true;
				TraderState traderState = Utils.DeserializeObject<TraderState>(serialized);
				if (traderState != null)
				{
					manager.m_CurrentState = traderState;
					s_LastPushed = serialized;
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[SkyCoop] Could not apply the trader state: " + ex.Message);
			}
			finally
			{
				s_Applying = false;
			}
		}

		public static void MaybePush()
		{
			if (MyMod.iAmHost && !s_Applying && !(Time.time < s_NextPush))
			{
				s_NextPush = Time.time + 5f;
				string text = Capture();
				if (!string.IsNullOrEmpty(text) && !(text == s_LastPushed))
				{
					s_LastPushed = text;
					ServerSend.TRADERSTATE(text);
				}
			}
		}

		public static void PushNow()
		{
			string text = Capture();
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			s_LastPushed = text;
			if (MyMod.iAmHost)
			{
				ServerSend.TRADERSTATE(text);
			}
			else if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(191))
				{
					DataStr.TraderStateSync traderStateSync = new DataStr.TraderStateSync();
					traderStateSync.m_SerializedState = text;
					packet.Write(traderStateSync);
					MyMod.SendUDPData(packet);
				}
			}
		}
	}

	public static class TravoisSyncing
	{
		private static readonly System.Collections.Generic.Dictionary<string, DataStr.TravoisSync> s_Remote = new System.Collections.Generic.Dictionary<string, DataStr.TravoisSync>();

		private static float s_NextPush;

		private const float PushIntervalSeconds = 0.2f;

		public static void Clear()
		{
			s_Remote.Clear();
		}

		public static string GetGuid(BigCarryItem item)
		{
			if (item == null)
			{
				return "";
			}
			ObjectGuid component = item.gameObject.GetComponent<ObjectGuid>();
			if (component == null)
			{
				ObjectGuid.MaybeAttachObjectGuidAndRegister(item.gameObject, ObjectGuidManager.GenerateNewGuidString());
				component = item.gameObject.GetComponent<ObjectGuid>();
			}
			if (!(component != null))
			{
				return "";
			}
			return component.Get();
		}

		private static DataStr.TravoisSync Describe(BigCarryItem item, int carriedBy)
		{
			return new DataStr.TravoisSync
			{
				m_GUID = GetGuid(item),
				m_LevelGUID = MyMod.level_guid,
				m_LevelID = MyMod.levelid,
				m_Position = item.gameObject.transform.position,
				m_Rotation = item.gameObject.transform.rotation,
				m_CarriedBy = carriedBy
			};
		}

		public static void Send(BigCarryItem item, int carriedBy)
		{
			if (item == null || !MyMod.InOnline())
			{
				return;
			}
			DataStr.TravoisSync travoisSync = Describe(item, carriedBy);
			if (string.IsNullOrEmpty(travoisSync.m_GUID))
			{
				return;
			}
			if (MyMod.iAmHost)
			{
				ServerSend.TRAVOISSYNC(0, travoisSync);
			}
			else if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(192))
				{
					packet.Write(travoisSync);
					MyMod.SendUDPData(packet);
				}
			}
		}

		public static void MaybePushCarried()
		{
			if (MyMod.InOnline() && !(Time.time < s_NextPush))
			{
				s_NextPush = Time.time + 0.2f;
				BigCarrySystem instance = BigCarrySystem.Instance;
				if (!(instance == null) && !(instance.CarriedItem == null))
				{
					Send(instance.CarriedItem, ClientUser.myId);
				}
			}
		}

		public static void Receive(DataStr.TravoisSync data)
		{
			if (data == null || string.IsNullOrEmpty(data.m_GUID))
			{
				return;
			}
			s_Remote[data.m_GUID] = data;
			if (data.m_LevelID != MyMod.levelid || data.m_LevelGUID != MyMod.level_guid || data.m_CarriedBy == ClientUser.myId)
			{
				return;
			}
			GameObject gameObject = ObjectGuidManager.Lookup(data.m_GUID);
			if (!(gameObject == null))
			{
				BigCarrySystem instance = BigCarrySystem.Instance;
				if (instance != null && instance.CarriedItem != null && instance.CarriedItem.gameObject == gameObject)
				{
					instance.MaybeDropImmediate();
				}
				gameObject.transform.position = data.m_Position;
				gameObject.transform.rotation = data.m_Rotation;
			}
		}
	}

	public static class NoiseMakerSync
	{
		private static bool s_Igniting;

		public static void Send(NoiseMakerItem item)
		{
			if (item == null || !MyMod.InOnline())
			{
				return;
			}
			ObjectGuid component = item.gameObject.GetComponent<ObjectGuid>();
			if (component == null)
			{
				ObjectGuid.MaybeAttachObjectGuidAndRegister(item.gameObject, ObjectGuidManager.GenerateNewGuidString());
				component = item.gameObject.GetComponent<ObjectGuid>();
			}
			if (component == null)
			{
				return;
			}
			if (MyMod.iAmHost)
			{
				ServerSend.NOISEMAKERIGNITE(0, component.Get(), MyMod.level_guid, MyMod.levelid);
			}
			else if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(193))
				{
					packet.Write(component.Get());
					packet.Write(MyMod.level_guid);
					packet.Write(MyMod.levelid);
					MyMod.SendUDPData(packet);
				}
			}
		}

		public static void Receive(string guid, string levelGuid, int levelId)
		{
			if (levelId != MyMod.levelid || levelGuid != MyMod.level_guid)
			{
				return;
			}
			GameObject gameObject = ObjectGuidManager.Lookup(guid);
			if (gameObject == null)
			{
				return;
			}
			NoiseMakerItem component = gameObject.GetComponent<NoiseMakerItem>();
			if (!(component != null) || s_Igniting)
			{
				return;
			}
			try
			{
				s_Igniting = true;
				component.Ignite();
			}
			finally
			{
				s_Igniting = false;
			}
		}

		public static bool IsApplyingRemoteIgnite()
		{
			return s_Igniting;
		}
	}

	[HarmonyPatch(typeof(BigCarrySystem), "BeginCarry")]
	internal static class BigCarrySystem_BeginCarry
	{
		private static void Postfix(BigCarrySystem __instance)
		{
			TravoisSyncing.Send(__instance.CarriedItem, ClientUser.myId);
		}
	}

	[HarmonyPatch(typeof(BigCarrySystem), "MaybeDrop")]
	internal static class BigCarrySystem_MaybeDrop
	{
		private static void Prefix(BigCarrySystem __instance, out BigCarryItem __state)
		{
			__state = __instance.CarriedItem;
		}

		private static void Postfix(BigCarrySystem __instance, BigCarryItem __state, bool __result)
		{
			if (__result && __state != null)
			{
				TravoisSyncing.Send(__state, -1);
			}
		}
	}

	[HarmonyPatch(typeof(BigCarrySystem), "MaybeDropImmediate")]
	internal static class BigCarrySystem_MaybeDropImmediate
	{
		private static void Prefix(BigCarrySystem __instance, out BigCarryItem __state)
		{
			__state = __instance.CarriedItem;
		}

		private static void Postfix(BigCarrySystem __instance, BigCarryItem __state, bool __result)
		{
			if (__result && __state != null)
			{
				TravoisSyncing.Send(__state, -1);
			}
		}
	}

	[HarmonyPatch(typeof(TraderManager), "CompleteTrade")]
	internal static class TraderManager_CompleteTrade
	{
		private static void Postfix()
		{
			TraderSync.PushNow();
		}
	}

	[HarmonyPatch(typeof(TraderManager), "InitiateTrade")]
	internal static class TraderManager_InitiateTrade
	{
		private static void Postfix()
		{
			TraderSync.PushNow();
		}
	}

	[HarmonyPatch(typeof(TraderManager), "CancelTrade")]
	internal static class TraderManager_CancelTrade
	{
		private static void Postfix()
		{
			TraderSync.PushNow();
		}
	}

	[HarmonyPatch(typeof(TraderManager), "ContactTrader")]
	internal static class TraderManager_ContactTrader
	{
		private static void Postfix()
		{
			TraderSync.PushNow();
		}
	}

	[HarmonyPatch(typeof(TraderManager), "NotifyConversationHad")]
	internal static class TraderManager_NotifyConversationHad
	{
		private static void Postfix()
		{
			TraderSync.PushNow();
		}
	}

	[HarmonyPatch(typeof(NoiseMakerItem), "Ignite", new Type[] { })]
	internal static class NoiseMakerItem_Ignite
	{
		private static void Postfix(NoiseMakerItem __instance)
		{
			if (!NoiseMakerSync.IsApplyingRemoteIgnite())
			{
				NoiseMakerSync.Send(__instance);
			}
		}
	}

	public const string CougarPrefab = "WILDLIFE_Cougar";

	public const string PtarmiganPrefab = "WILDLIFE_Ptarmigan";

	public static string GetAnimalPrefabName(string instanceName)
	{
		if (string.IsNullOrEmpty(instanceName))
		{
			return null;
		}
		if (instanceName.IndexOf("Cougar", StringComparison.OrdinalIgnoreCase) < 0 && instanceName.IndexOf("Ptarmigan", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return null;
		}
		string text = GameCompat.StripCloneSuffix(instanceName);
		int num = text.LastIndexOf('_');
		if (num > 0 && num < text.Length - 1)
		{
			bool flag = true;
			for (int i = num + 1; i < text.Length; i++)
			{
				if (!char.IsDigit(text[i]))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				text = text.Substring(0, num);
			}
		}
		return text;
	}

	public static bool IsFarTerritoryAnimal(string instanceName)
	{
		return GetAnimalPrefabName(instanceName) != null;
	}

	public static void StripAi(GameObject animal)
	{
		if (!(animal == null))
		{
			AiCougar component = animal.GetComponent<AiCougar>();
			if (component != null)
			{
				UnityEngine.Object.Destroy(component);
			}
			AiPtarmigan component2 = animal.GetComponent<AiPtarmigan>();
			if (component2 != null)
			{
				UnityEngine.Object.Destroy(component2);
			}
		}
	}

	public static void SetAiEnabled(GameObject animal, bool enabled)
	{
		if (!(animal == null))
		{
			AiCougar component = animal.GetComponent<AiCougar>();
			if (component != null)
			{
				component.enabled = enabled;
			}
			AiPtarmigan component2 = animal.GetComponent<AiPtarmigan>();
			if (component2 != null)
			{
				component2.enabled = enabled;
			}
		}
	}

	public static void Update()
	{
		if (MyMod.InOnline())
		{
			CougarSync.MaybePush();
			TraderSync.MaybePush();
			TravoisSyncing.MaybePushCarried();
		}
	}

	public static void OnSceneChanged()
	{
		CougarSync.OnSceneChanged();
		TraderSync.OnSceneChanged();
		TravoisSyncing.Clear();
	}
}
