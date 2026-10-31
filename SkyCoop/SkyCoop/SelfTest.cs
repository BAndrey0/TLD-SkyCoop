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
	private static bool dumpedAvatar;
	private static bool previewCreated;

	public static void Tick()
	{
		if (!previewCreated && Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_PREVIEW") == "1" && MyMod.WildlifeSceneReady() && !Shared.IsMainMenuScene(MyMod.level_name))
		{
			var prefab = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
			var camera = GameManager.GetMainCamera();
			if (prefab != null && camera != null)
			{
				var direction = camera.transform.forward; direction.y = 0; direction.Normalize();
				var position = GameManager.GetPlayerTransform().position + direction * 2.5f;
				var avatar = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(0,camera.transform.eulerAngles.y+180,0));
				avatar.name = "QA_NEW_SURVIVOR";
				foreach(var collider in avatar.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
				SurvivorModels.Apply(avatar,"GEAR_BasicWoolHat","GEAR_DownSkiJacket","GEAR_CombatPants","GEAR_WorkBoots","GEAR_Scarf",0,true);
				previewCreated = true;
			}
		}
		var dump = Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_DUMP");
		if (!dumpedAvatar && !string.IsNullOrEmpty(dump))
		{
			var prefab = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
			if (prefab != null)
			{
				var output = new System.Text.StringBuilder();
				foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					output.AppendLine("MESH " + renderer.name + " root=" + renderer.rootBone?.name);
					foreach (var bone in renderer.bones) if (bone != null) output.AppendLine(bone.name + ";" + bone.parent?.name + ";" + bone.position.ToString("F4"));
				}
				System.IO.File.WriteAllText(dump, output.ToString());
				dumpedAvatar = true;
			}
		}
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
