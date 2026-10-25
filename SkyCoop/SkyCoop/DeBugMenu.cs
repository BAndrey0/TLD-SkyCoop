using System;
using System.Collections.Generic;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

public class DeBugMenu
{
	public class DebugFunction
	{
		public float X;

		public float Y;

		public Action Fn;

		public DebugFunction(float _X, float _Y, Action _Fn)
		{
			X = _X;
			Y = _Y;
			Fn = _Fn;
		}
	}

	public static int DebugVal = 0;

	public static uint DebugSound = 0u;

	public static string ContainerOverride = "";

	public static DecalProjectorInstance Replica;

	public static bool Inited = false;

	public static Dictionary<string, List<DebugFunction>> Categories = new Dictionary<string, List<DebugFunction>>();

	public void AddOption(string Tab, Action Fn)
	{
		if (!Categories.TryGetValue(Tab, out var value))
		{
			Categories.Add(Tab, new List<DebugFunction>());
			Categories.TryGetValue(Tab, out value);
		}
		float x = 20f;
		if (Tab == "Main")
		{
			x = 160f;
		}
		int num = 20;
		float y = 10 + num * value.Count;
		value.Add(new DebugFunction(x, y, Fn));
	}

	public static void InitUI()
	{
		Inited = true;
	}

	public static void Render()
	{
		if (!MyMod.DebugGUI)
		{
			return;
		}
		if (!Inited)
		{
			InitUI();
		}
		if (MyMod.AdvancedDebugMode != "")
		{
			int count = BaseAiManager.m_BaseAis.Count;
			int count2 = MyMod.ActorsList.Count;
			if (MyMod.AdvancedDebugMode == "AnimalsGUID" || MyMod.AdvancedDebugMode == "AnimalsAllStats")
			{
				int num = 20;
				GUI.Label(new Rect(700f, 10f, 500f, 100f), "Animals: " + count + " Actors: " + count2 + " Controller " + MyMod.AnimalsController);
				GUI.Label(new Rect(700f, 10 + num, 500f, 100f), "GUID: " + MyMod.DebugAnimalGUID);
				GUI.Label(new Rect(700f, 10 + num * 2, 500f, 100f), "Last GUID: " + MyMod.DebugAnimalGUIDLast);
				bool flag = true;
				flag = !(MyMod.DebugLastAnimal == null);
				GUI.Label(new Rect(850f, 10 + num * 2, 500f, 100f), "Decoy:");
				for (int i = 0; i < GearManager.m_DroppedDecoys.Count; i++)
				{
					GearItem gearItem = GearManager.m_DroppedDecoys[i];
					if ((bool)gearItem)
					{
						string text = gearItem.GetGearName();
						Comps.FakeDecoy component = gearItem.gameObject.GetComponent<Comps.FakeDecoy>();
						if ((bool)component && (bool)component.m_DropGearDummy)
						{
							text = text + " (" + component.m_DropGearDummy.m_Extra.m_GearName + ")";
						}
						GUI.Label(new Rect(850f, 10 + num * (3 + i), 500f, 100f), i + ". " + text);
					}
				}
				GUI.Label(new Rect(700f, 10 + num * 3, 500f, 100f), "Still exists: " + flag);
				if (flag && MyMod.DebugLastAnimal != null && MyMod.AdvancedDebugMode == "AnimalsAllStats")
				{
					Comps.AnimalUpdates component2 = MyMod.DebugLastAnimal.GetComponent<Comps.AnimalUpdates>();
					BaseAi component3 = MyMod.DebugLastAnimal.GetComponent<BaseAi>();
					string text2 = "";
					if (component3.m_SpawnRegionParent != null)
					{
						string text3 = "NONE";
						if (component3.m_SpawnRegionParent.GetComponent<ObjectGuid>() != null)
						{
							text3 = component3.m_SpawnRegionParent.GetComponent<ObjectGuid>().Get();
						}
						bool activeSelf = component3.m_SpawnRegionParent.gameObject.activeSelf;
						text2 = activeSelf + " (" + text3 + ") " + component3.m_SpawnRegionParent.gameObject.name;
					}
					else
					{
						text2 = "Null";
					}
					if (component2 != null)
					{
						GUI.Label(new Rect(700f, 10 + num * 5, 500f, 100f), "m_Banned " + component2.m_Banned);
						GUI.Label(new Rect(700f, 10 + num * 6, 500f, 100f), "m_DampingIgnore " + component2.m_DampingIgnore);
						GUI.Label(new Rect(700f, 10 + num * 7, 500f, 100f), "NoResponce " + component2.NoResponce);
						GUI.Label(new Rect(700f, 10 + num * 8, 500f, 100f), "Object active " + MyMod.DebugLastAnimal.activeSelf + " Rendering " + component3.m_RenderersEnabled);
						GUI.Label(new Rect(700f, 10 + num * 9, 500f, 100f), "RetakeCooldown " + component2.ReTakeCoolDown);
						GUI.Label(new Rect(700f, 10 + num * 10, 500f, 100f), "LastFoundPlayer " + component2.LastFoundPlayer);
						GUI.Label(new Rect(700f, 10 + num * 11, 500f, 100f), "Ai Mode " + component3.GetAiMode());
						GUI.Label(new Rect(700f, 10 + num * 12, 500f, 100f), "HP " + component3.m_CurrentHP);
						GUI.Label(new Rect(700f, 10 + num * 13, 500f, 100f), "m_HasEnteredStruggleOnLastAttack " + component3.m_HasEnteredStruggleOnLastAttack);
						GUI.Label(new Rect(700f, 10 + num * 14, 500f, 100f), "SpawnRegion " + text2);
						if (GameManager.m_PlayerObject != null)
						{
							GUI.Label(new Rect(700f, 10 + num * 18, 500f, 100f), "My Distance " + Vector3.Distance(MyMod.DebugLastAnimal.transform.position, GameManager.GetPlayerObject().transform.position));
							for (int j = 0; j < MyMod.playersData.Count; j++)
							{
								if (MyMod.playersData[j] != null)
								{
									int num2 = 19 + j;
									GUI.Label(new Rect(700f, 10 + num * num2, 500f, 100f), "Player[" + j + "] Distance " + Vector3.Distance(MyMod.DebugLastAnimal.transform.position, MyMod.playersData[j].m_Position));
								}
							}
						}
					}
					else
					{
						GUI.Label(new Rect(700f, 10 + num * 39, 500f, 100f), "No AnimalUpdates!");
					}
				}
				if (MyMod.iAmHost)
				{
					for (int k = 0; k < MyMod.MaxPlayers; k++)
					{
						int num3 = 40 + k;
						if (MyMod.playersData[k].m_Levelid != 0)
						{
							GUI.Label(new Rect(700f, 10 + num * num3, 500f, 100f), MyMod.playersData[k].m_Name + " Ticks: " + MyMod.playersData[k].m_TicksOnScene + " Scene " + MyMod.playersData[k].m_Levelid);
						}
					}
				}
			}
		}
		GUI.Box(new Rect(10f, 10f, 100f, 160f), "Debug menu");
		if (GUI.Button(new Rect(20f, 40f, 80f, 20f), "Cube"))
		{
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			Transform playerTransform = GameManager.GetPlayerTransform();
			gameObject.transform.position = new Vector3(playerTransform.position.x, playerTransform.position.y, playerTransform.position.z);
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(4);
				packet.Write(gameObject.transform.position);
				MyMod.SendUDPData(packet);
			}
			if (MyMod.iAmHost)
			{
				using (new Packet(4))
				{
					ServerSend.BLOCK(0, gameObject.transform.position, toEveryOne: true);
				}
			}
		}
		if (GUI.Button(new Rect(20f, 70f, 80f, 20f), "Hour skip"))
		{
			Shared.SkipRTTime(1);
		}
		if (GUI.Button(new Rect(20f, 100f, 80f, 20f), "More"))
		{
			if (MyMod.UIDebugType != "")
			{
				MyMod.UIDebugType = "";
			}
			else
			{
				MyMod.UIDebugType = "Open";
			}
		}
		if (GUI.Button(new Rect(20f, 130f, 80f, 20f), "Advanced"))
		{
			if (MyMod.UIDebugType != "")
			{
				MyMod.UIDebugType = "";
			}
			else
			{
				MyMod.UIDebugType = "Advanced";
			}
		}
		if (MyMod.UIDebugType == "Advanced")
		{
			GUI.Box(new Rect(150f, 10f, 140f, 200f), "Crazy Debug Tools");
			if (GUI.Button(new Rect(160f, 40f, 120f, 20f), "UpdateBan=" + MyMod.KillOnUpdate))
			{
				MyMod.KillOnUpdate = !MyMod.KillOnUpdate;
			}
			if (GUI.Button(new Rect(160f, 70f, 120f, 20f), "Trace=" + MyMod.CrazyPatchesLogger))
			{
				MyMod.CrazyPatchesLogger = !MyMod.CrazyPatchesLogger;
			}
			if (GUI.Button(new Rect(160f, 100f, 120f, 20f), "Binds=" + MyMod.DebugBind))
			{
				MyMod.DebugBind = !MyMod.DebugBind;
			}
			if (GUI.Button(new Rect(160f, 130f, 120f, 20f), "Nuke patches"))
			{
				HarmonyLib.Harmony.UnpatchAll();
			}
			if (GUI.Button(new Rect(160f, 160f, 120f, 20f), "SecondBan=" + MyMod.KillEverySecond))
			{
				MyMod.KillEverySecond = !MyMod.KillEverySecond;
			}
			if (GUI.Button(new Rect(160f, 190f, 120f, 20f), "MinuteBan=" + MyMod.KillEveryInGameMinute))
			{
				MyMod.KillEveryInGameMinute = !MyMod.KillEveryInGameMinute;
			}
			if (GUI.Button(new Rect(160f, 220f, 120f, 20f), "Flag1=" + MyMod.Flag1))
			{
				MyMod.Flag1 = !MyMod.Flag1;
			}
			if (GUI.Button(new Rect(160f, 250f, 120f, 20f), "Flag2=" + MyMod.Flag2))
			{
				MyMod.Flag2 = !MyMod.Flag2;
			}
			if (GUI.Button(new Rect(160f, 280f, 120f, 20f), "Flag3=" + MyMod.Flag3))
			{
				MyMod.Flag3 = !MyMod.Flag3;
			}
			if (GUI.Button(new Rect(160f, 310f, 120f, 20f), "Flag4=" + MyMod.Flag4))
			{
				MyMod.Flag4 = !MyMod.Flag4;
			}
			if (GUI.Button(new Rect(160f, 340f, 120f, 20f), "Flag5=" + MyMod.Flag5))
			{
				MyMod.Flag5 = !MyMod.Flag5;
			}
			if (GUI.Button(new Rect(160f, 370f, 120f, 20f), "Flag6=" + MyMod.Flag6))
			{
				MyMod.Flag6 = !MyMod.Flag6;
			}
			if (GUI.Button(new Rect(160f, 400f, 120f, 20f), "Flag7=" + MyMod.Flag7))
			{
				MyMod.Flag7 = !MyMod.Flag7;
			}
			if (GUI.Button(new Rect(160f, 430f, 120f, 20f), "Flag8=" + MyMod.Flag8))
			{
				MyMod.Flag8 = !MyMod.Flag8;
			}
			if (GUI.Button(new Rect(160f, 460f, 120f, 20f), "Flag9=" + MyMod.Flag9))
			{
				MyMod.Flag9 = !MyMod.Flag9;
			}
			if (GUI.Button(new Rect(160f, 490f, 120f, 20f), "Flag10=" + MyMod.Flag10))
			{
				MyMod.Flag10 = !MyMod.Flag10;
			}
			if (GUI.Button(new Rect(160f, 520f, 120f, 20f), "Flag11=" + MyMod.Flag11))
			{
				MyMod.Flag11 = !MyMod.Flag11;
			}
			if (GUI.Button(new Rect(160f, 550f, 120f, 20f), "Flag12=" + MyMod.Flag12))
			{
				MyMod.Flag12 = !MyMod.Flag12;
			}
			if (GUI.Button(new Rect(160f, 580f, 120f, 20f), "Flag13=" + MyMod.Flag13))
			{
				MyMod.Flag13 = !MyMod.Flag13;
			}
		}
		if (MyMod.UIDebugType == "Open")
		{
			GUI.Box(new Rect(150f, 10f, 100f, 200f), "Debug Tools");
			if (GUI.Button(new Rect(160f, 40f, 80f, 20f), "Player"))
			{
				MyMod.UIDebugType = "PlayerDebug";
			}
			if (GUI.Button(new Rect(160f, 70f, 80f, 20f), "Teleport"))
			{
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "INPUT ID OF PLAYER TELEPORT TO", "", Panel_Confirmation.ButtonLayout.Button_2, "TELEPORT", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
			}
			if (GUI.Button(new Rect(160f, 100f, 80f, 20f), "Animals"))
			{
				MyMod.UIDebugType = "AnimalsDebug";
			}
			if (GUI.Button(new Rect(160f, 130f, 80f, 20f), "Photo"))
			{
				ResourceIndependent.SpawnSomeGearsOnScene(MyMod.level_name, "", "GEAR_SCPresent");
				ResourceIndependent.SpawnSomeGearsOnScene(MyMod.level_name, "SANDBOX", "GEAR_SCPresent");
			}
			if (GUI.Button(new Rect(160f, 160f, 80f, 20f), "CrashSite") && MyMod.iAmHost)
			{
				ExpeditionManager.StartCrashSite();
			}
			if (GUI.Button(new Rect(160f, 190f, 80f, 20f), "Show"))
			{
				SafeZoneManager.DebugRenderZones();
			}
		}
		if (MyMod.UIDebugType == "PlayerDebug")
		{
			GUI.Box(new Rect(150f, 10f, 100f, 200f), "Player Debug");
			if (GUI.Button(new Rect(160f, 40f, 80f, 20f), "Items"))
			{
				MyMod.UIDebugType = "PlayerDebug_Items";
			}
			if (GUI.Button(new Rect(160f, 70f, 80f, 20f), "Anims"))
			{
				MyMod.UIDebugType = "PlayerDebug_Anims";
			}
			if (GUI.Button(new Rect(160f, 100f, 80f, 20f), "Set"))
			{
				MyMod.playersData[0].m_Levelid = MyMod.levelid;
				MyMod.playersData[0].m_LevelGuid = MyMod.level_guid;
				MyMod.playersData[0].m_Position = GameManager.GetPlayerTransform().position;
				MyMod.playersData[0].m_Rotation = GameManager.GetPlayerTransform().rotation;
			}
			if (GUI.Button(new Rect(160f, 130f, 80f, 20f), "Mimic"))
			{
				if (!MyMod.playersData[0].m_Mimic)
				{
					MyMod.playersData[0].m_Mimic = true;
				}
				else
				{
					MyMod.playersData[0].m_Mimic = false;
				}
			}
			if (GUI.Button(new Rect(160f, 160f, 80f, 20f), "Clothes"))
			{
				MyMod.UIDebugType = "Clothes";
				MyMod.playersData[0].m_PlayerClothingData.m_Boots = "GEAR_CombatBoots";
			}
			if (GUI.Button(new Rect(160f, 190f, 80f, 20f), "Fake") && MyMod.iAmHost)
			{
				MyMod.playersData[1].m_Levelid = MyMod.levelid;
				MyMod.playersData[1].m_Position = GameManager.GetPlayerTransform().position;
				MyMod.playersData[1].m_Rotation = GameManager.GetPlayerTransform().rotation;
				MyMod.playersData[1].m_Position = GameManager.GetPlayerTransform().position;
				MyMod.playersData[1].m_Rotation = GameManager.GetPlayerTransform().rotation;
				Server.clients[1].udp.sid = "11111111111111111";
				Server.clients[1].TimeOutTime = 0;
			}
			if (GUI.Button(new Rect(160f, 220f, 80f, 20f), "Back"))
			{
				MyMod.UIDebugType = "Open";
			}
		}
		if (MyMod.UIDebugType == "PlayerDebug_Items")
		{
			GUI.Box(new Rect(150f, 10f, 100f, 250f), "Debug Items");
			if (MyMod.ItemsForDebug.Count == 0)
			{
				MyMod.ItemsForDebug.Add("GEAR_WoodMatches");
				MyMod.ItemsForDebug.Add("GEAR_FlareA");
				MyMod.ItemsForDebug.Add("GEAR_BlueFlare");
				MyMod.ItemsForDebug.Add("GEAR_Torch");
				MyMod.ItemsForDebug.Add("GEAR_Rifle");
				MyMod.ItemsForDebug.Add("GEAR_Revolver");
				MyMod.ItemsForDebug.Add("GEAR_SprayPaintCanGlyphA");
				MyMod.ItemsForDebug.Add("GEAR_Stone");
				MyMod.ItemsForDebug.Add("GEAR_KeroseneLamp");
				MyMod.ItemsForDebug.Add("GEAR_FlareGun");
				MyMod.ItemsForDebug.Add("Book");
				MyMod.ItemsForDebug.Add("");
			}
			if (GUI.Button(new Rect(160f, 40f, 80f, 20f), "Next Item"))
			{
				MyMod.ItemForDebug++;
				if (MyMod.ItemForDebug > MyMod.ItemsForDebug.Count)
				{
					MyMod.ItemForDebug = 0;
				}
				MyMod.playersData[0].m_PlayerEquipmentData.m_HoldingItem = MyMod.ItemsForDebug[MyMod.ItemForDebug];
			}
			if (GUI.Button(new Rect(160f, 70f, 80f, 20f), "Light"))
			{
				if (!MyMod.playersData[0].m_PlayerEquipmentData.m_LightSourceOn)
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_LightSourceOn = true;
				}
				else
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_LightSourceOn = false;
				}
			}
			if (GUI.Button(new Rect(160f, 100f, 80f, 20f), "Has Rifle"))
			{
				if (!MyMod.playersData[0].m_PlayerEquipmentData.m_HasRifle)
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasRifle = true;
				}
				else
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasRifle = false;
				}
			}
			if (GUI.Button(new Rect(160f, 130f, 80f, 20f), "Has Rev."))
			{
				if (!MyMod.playersData[0].m_PlayerEquipmentData.m_HasRevolver)
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasRevolver = true;
				}
				else
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasRevolver = false;
				}
			}
			if (GUI.Button(new Rect(160f, 160f, 80f, 20f), "Has Axe"))
			{
				if (!MyMod.playersData[0].m_PlayerEquipmentData.m_HasAxe)
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasAxe = true;
				}
				else
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasAxe = false;
				}
			}
			if (GUI.Button(new Rect(160f, 190f, 80f, 20f), "Has Medkit"))
			{
				if (!MyMod.playersData[0].m_PlayerEquipmentData.m_HasMedkit)
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasMedkit = true;
				}
				else
				{
					MyMod.playersData[0].m_PlayerEquipmentData.m_HasMedkit = false;
				}
			}
			if (GUI.Button(new Rect(160f, 220f, 80f, 20f), "Back"))
			{
				MyMod.UIDebugType = "PlayerDebug";
			}
		}
		if (MyMod.UIDebugType == "PlayerDebug_Anims")
		{
			GUI.Box(new Rect(150f, 10f, 100f, 230f), "Debug Anims");
			if (GUI.Button(new Rect(160f, 40f, 80f, 20f), "Idle"))
			{
				MyMod.playersData[0].m_AnimState = "Idle";
			}
			if (GUI.Button(new Rect(160f, 70f, 80f, 20f), "Walk"))
			{
				MyMod.playersData[0].m_AnimState = "Walk";
			}
			if (GUI.Button(new Rect(160f, 100f, 80f, 20f), "Run"))
			{
				MyMod.playersData[0].m_AnimState = "Run";
			}
			if (GUI.Button(new Rect(160f, 130f, 80f, 20f), "Sit"))
			{
				if (MyMod.playersData[0].m_AnimState != "Ctrl")
				{
					MyMod.playersData[0].m_AnimState = "Ctrl";
				}
				else
				{
					MyMod.playersData[0].m_AnimState = "Idle";
				}
			}
			if (GUI.Button(new Rect(160f, 160f, 80f, 20f), "Flex"))
			{
				MyMod.playersData[0].m_AnimState = "Cringe2";
			}
			if (GUI.Button(new Rect(160f, 190f, 80f, 20f), "Aim") && MyMod.playersData[0].m_PlayerEquipmentData.m_HoldingItem == "GEAR_Revolver")
			{
				if (MyMod.playersData[0].m_AnimState != "Ctrl")
				{
					MyMod.playersData[0].m_AnimState = "HoldGun";
				}
				else
				{
					MyMod.playersData[0].m_AnimState = "HoldGun_Sit";
				}
			}
			if (GUI.Button(new Rect(160f, 220f, 80f, 20f), "Back"))
			{
				MyMod.UIDebugType = "PlayerDebug";
			}
		}
		if (MyMod.UIDebugType == "AnimalsDebug")
		{
			GUI.Box(new Rect(150f, 10f, 100f, 270f), "Debug Animals");
			GUI.Button(new Rect(160f, 40f, 80f, 20f), ThreadManager.executeOnMainThread.Count + "-" + ThreadManager.executeCopiedOnMainThread.Count);
			if (GUI.Button(new Rect(160f, 70f, 80f, 20f), "GUID Stats"))
			{
				if (MyMod.AdvancedDebugMode == "AnimalsGUID")
				{
					MyMod.AdvancedDebugMode = "";
				}
				else
				{
					MyMod.AdvancedDebugMode = "AnimalsGUID";
				}
			}
			if (GUI.Button(new Rect(160f, 100f, 80f, 20f), "All Stats"))
			{
				if (MyMod.AdvancedDebugMode == "AnimalsAllStats")
				{
					MyMod.AdvancedDebugMode = "";
				}
				else
				{
					MyMod.AdvancedDebugMode = "AnimalsAllStats";
				}
			}
			if (GUI.Button(new Rect(160f, 130f, 80f, 20f), "TP to GUID"))
			{
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "INPUT GUID TO TELEPORT TO", "", Panel_Confirmation.ButtonLayout.Button_2, "TELEPORT", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
			}
			if (GUI.Button(new Rect(160f, 160f, 80f, 20f), "Track GUID"))
			{
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "INPUT GUID TO TRACK", "", Panel_Confirmation.ButtonLayout.Button_2, "TELEPORT", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
			}
			if (GUI.Button(new Rect(160f, 220f, 80f, 20f), "Remove all"))
			{
				for (int l = 0; l < BaseAiManager.m_BaseAis.Count; l++)
				{
					GameObject gameObject2 = BaseAiManager.m_BaseAis[l].gameObject;
					if (gameObject2 != null)
					{
						ObjectGuidManager.UnRegisterGuid(gameObject2.GetComponent<ObjectGuid>().Get());
						UnityEngine.Object.Destroy(gameObject2);
					}
				}
			}
			if (GUI.Button(new Rect(160f, 250f, 80f, 20f), "Back"))
			{
				MyMod.UIDebugType = "Open";
			}
		}
		if (!(MyMod.UIDebugType == "Clothes"))
		{
			return;
		}
		GUI.Box(new Rect(150f, 10f, 100f, 270f), "Debug Clotches");
		if (GUI.Button(new Rect(160f, 40f, 80f, 20f), "Next Top"))
		{
			int childCount = MyMod.players[0].transform.GetChild(0).GetChild(1).childCount;
			Comps.MultiplayerPlayerClothingManager component4 = MyMod.players[0].GetComponent<Comps.MultiplayerPlayerClothingManager>();
			component4.m_Debug = true;
			component4.m_DebugT++;
			if (component4.m_DebugT >= childCount)
			{
				component4.m_DebugT = 0;
			}
			component4.UpdateClothing();
		}
		if (GUI.Button(new Rect(160f, 70f, 80f, 20f), "Next Bottom"))
		{
			int childCount2 = MyMod.players[0].transform.GetChild(0).GetChild(2).childCount;
			Comps.MultiplayerPlayerClothingManager component5 = MyMod.players[0].GetComponent<Comps.MultiplayerPlayerClothingManager>();
			component5.m_Debug = true;
			component5.m_DebugB++;
			if (component5.m_DebugB >= childCount2)
			{
				component5.m_DebugB = 0;
			}
			component5.UpdateClothing();
		}
		if (GUI.Button(new Rect(160f, 100f, 80f, 20f), "Back"))
		{
			MyMod.UIDebugType = "PlayerDebug";
		}
	}
}
