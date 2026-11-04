using System;
using GameServer;
using HarmonyLib;
using Il2Cpp;

namespace SkyCoop;

internal static class WorldSaveIdentity
{
	private const string SaveKey = "skycoop_worldid";
	internal static string Active { get; private set; } = "";

	internal static string Read(string slotName)
	{
		return SaveSlotCompat.LoadDataFromSlot(slotName, SaveKey) ?? "";
	}

	[HarmonyPatch(typeof(SaveGameSystem), "SaveGlobalData")]
	private static class SandboxSaved
	{
		private static void Postfix(SlotData slot)
		{
			string worldId = Active;
			if (slot == null || worldId.Length == 0 || slot.m_InternalName != SaveGameSystem.GetCurrentSaveName())
			{
				return;
			}
			using (SaveSlotCompat.UseWriteTarget(slot))
			{
				SaveSlotCompat.SaveDataToSlot(slot.m_GameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, slot.m_InternalName, SaveKey, worldId);
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "LaunchSandbox")]
	private static class SandboxStarted
	{
		private static void Prefix()
		{
			Server.Stop();
			Active = MyMod.sendMyPosition && !MyMod.ShouldCreateSaveForHost && MyMod.PendingSave != null
				? MyMod.PendingSave.m_WorldId
				: Guid.NewGuid().ToString("N");
		}
	}

	[HarmonyPatch(typeof(SaveGameSystem), "RestoreGlobalData")]
	private static class SandboxRestored
	{
		private static void Postfix(string name)
		{
			string saved = Read(name);
			Active = Guid.TryParseExact(saved, "N", out Guid worldId) ? worldId.ToString("N") : "";
		}
	}
}
