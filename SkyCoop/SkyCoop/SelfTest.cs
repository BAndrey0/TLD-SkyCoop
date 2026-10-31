using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Developer self-test, off unless SKYCOOP_SELFTEST_SLOT is set (slot name like sandbox16 or its display name):
// loads that save from the main menu and starts a host (plain UDP server from server.json defaults), so host-side
// start-up can be reproduced without a second player or clicking through the menus.
public static class SelfTest
{
	private static string slot;

	private static bool started;

	private static float menuSince = -1f;

	public static void Tick()
	{
		if (started)
		{
			return;
		}
		if (slot == null)
		{
			slot = Environment.GetEnvironmentVariable("SKYCOOP_SELFTEST_SLOT") ?? "";
			if (slot.Length > 0)
			{
				MelonLogger.Msg("[SelfTest] Will host slot " + slot);
			}
		}
		if (slot.Length == 0 || !Shared.IsMainMenuScene(MyMod.level_name))
		{
			return;
		}
		if (menuSince < 0f)
		{
			menuSince = Time.realtimeSinceStartup;
		}
		// The menu scene exists while startup movies are still showing; loading a save
		// during them leaves their overlay/camera above the world. Give the UI test time
		// to finish the startup screens before automatically hosting.
		if (Time.realtimeSinceStartup - menuSince < 60f)
		{
			return;
		}
		started = true;
		Il2CppSystem.Collections.Generic.List<SaveSlotInfo> slots = SaveGameSystem.GetSortedSaveSlots(Episode.One, SaveSlotType.SANDBOX);
		for (int i = 0; i < slots.Count; i++)
		{
			SaveSlotInfo info = slots[i];
			if (info.m_SaveSlotName == slot || info.m_UserDefinedName == slot)
			{
				MelonLogger.Msg("[SelfTest] Loading " + info.m_SaveSlotName + " and hosting after load");
				MyMod.AutoHostWhenLoaded = true;
				SaveGameSlots.SetBaseNameForSave(info.m_SaveSlotName, info.m_SaveSlotName);
				SaveGameSystem.SetCurrentSaveInfo(info.m_Episode, info.m_GameMode, info.m_GameId, info.m_SaveSlotName);
				GameManager.LoadSaveGameSlot(info.m_SaveSlotName, info.m_SaveChangelistVersion);
				return;
			}
		}
		MelonLogger.Warning("[SelfTest] Slot " + slot + " not found");
	}
}
