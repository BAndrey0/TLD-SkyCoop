using System;
using System.Collections.Generic;
using GameServer;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public class ClientHandle
{
	public static void Welcome(Packet _packet)
	{
		MyMod.RemovePleaseWait();
		MyMod.DiscardRepeatPacket();
		int myId = _packet.ReadInt();
		int maxPlayers = _packet.ReadInt();
		System.Collections.Generic.List<string> modsWhiteList = _packet.ReadStringList();
		MyMod.MaxPlayers = maxPlayers;
		Shared.InitAllPlayers();
		MelonLogger.Msg("Welcome to server!");
		ClientUser.myId = myId;
		MelonLogger.Msg("Host registered me as client " + myId);
		MyMod.sendMyPosition = true;
		ClientUser.LastConnectedIp = ClientUser.PendingConnectionIp;
		MyMod.NoHostResponceSeconds = 0;
		MyMod.NeedTryReconnect = false;
		MyMod.TryingReconnect = false;
		WelcomeReceived(modsWhiteList);
	}

	public static void WelcomeReceived(System.Collections.Generic.List<string> ModsWhiteList)
	{
		MyMod.RemovePleaseWait();
		MyMod.DiscardRepeatPacket();
		using (Packet packet = new Packet(1))
		{
			packet.Write(ClientUser.myId);
			packet.Write(MyMod.MyChatName);
			packet.Write("0.16.11");
			packet.Write(Supporters.MyID);
			packet.Write(Supporters.ConfiguratedBenefits);
			packet.Write(ModsValidation.GetModsHash(Force: true, ModsWhiteList).m_Hash);
			packet.Write(WorldSaveMatch.Protocol);
			MyMod.SendUDPData(packet);
		}
		if (MyMod.level_name != "Empty" && MyMod.level_name != "Boot" && !Shared.IsMainMenuScene(MyMod.level_name))
		{
			MyMod.SendSpawnData();
		}
	}

	public static void XYZ(Packet _packet)
	{
		Vector3 position = _packet.ReadVector3();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_Position = position;
			if (MyMod.players[index] != null && MyMod.players[index].GetComponent<Comps.MultiplayerPlayer>() != null)
			{
				MyMod.LongActionCancleCauseMoved(MyMod.players[index].GetComponent<Comps.MultiplayerPlayer>());
			}
		}
	}

	public static void XYZDW(Packet _packet)
	{
	}

	public static void XYZW(Packet _packet)
	{
		Quaternion rotation = _packet.ReadQuaternion();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_Rotation = rotation;
		}
	}

	public static void BLOCK(Packet _packet)
	{
		Vector3 position = _packet.ReadVector3();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null && MyMod.playersData[index].m_Levelid == MyMod.levelid)
		{
			GameObject.CreatePrimitive(PrimitiveType.Cube).transform.position = position;
		}
	}

	public static void LEVELID(Packet _packet)
	{
		int num = _packet.ReadInt();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null && MyMod.playersData[index].m_Levelid != num)
		{
			MyMod.playersData[index].m_Levelid = num;
			MelonLogger.Msg("Player " + index + " transition to level " + num);
		}
	}

	public static void LEVELGUID(Packet _packet)
	{
		string text = _packet.ReadString();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null && MyMod.playersData[index].m_LevelGuid != text)
		{
			MyMod.playersData[index].m_LevelGuid = text;
		}
	}

	public static void GOTITEM(Packet _packet)
	{
		DataStr.GearItemDataPacket gearItemDataPacket = _packet.ReadGearData();
		_ = gearItemDataPacket.m_SendedTo;
		_ = ClientUser.myId;
		MyMod.GiveRecivedItem(gearItemDataPacket);
	}

	public static void GAMETIME(Packet _packet)
	{
		MyMod.OveridedTime = _packet.ReadString();
		MyMod.MinutesFromStartServer = _packet.ReadInt();
		MyMod.NeedSyncTime = true;
	}

	public static void LIGHTSOURCENAME(Packet _packet)
	{
		string holdingItem = _packet.ReadString();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_PlayerEquipmentData.m_HoldingItem = holdingItem;
		}
	}

	public static void LIGHTSOURCE(Packet _packet)
	{
		bool lightSourceOn = _packet.ReadBool();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_PlayerEquipmentData.m_LightSourceOn = lightSourceOn;
		}
	}

	public static void MAKEFIRE(Packet _packet)
	{
	}

	public static void ANIMSTATE(Packet _packet)
	{
		string animState = _packet.ReadString();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_AnimState = animState;
		}
	}

	public static void EQUIPMENT(Packet _packet)
	{
		DataStr.PlayerEquipmentData playerEquipmentData = _packet.ReadEQ();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_PlayerEquipmentData.m_Arrows = playerEquipmentData.m_Arrows;
			MyMod.playersData[index].m_PlayerEquipmentData.m_HasAxe = playerEquipmentData.m_HasAxe;
			MyMod.playersData[index].m_PlayerEquipmentData.m_HasMedkit = playerEquipmentData.m_HasMedkit;
			MyMod.playersData[index].m_PlayerEquipmentData.m_HasRevolver = playerEquipmentData.m_HasRevolver;
			MyMod.playersData[index].m_PlayerEquipmentData.m_HasRifle = playerEquipmentData.m_HasRifle;
			MyMod.playersData[index].m_PlayerEquipmentData.m_Flares = playerEquipmentData.m_Flares;
			MyMod.playersData[index].m_PlayerEquipmentData.m_BlueFlares = playerEquipmentData.m_BlueFlares;
		}
	}

	public static void SLEEPHOURS(Packet _packet)
	{
		int sleepHours = _packet.ReadInt();
		int index = _packet.ReadInt();
		if (MyMod.playersData.Count > 0 && MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_SleepHours = sleepHours;
		}
	}

	public static void SYNCWEATHER(Packet _packet)
	{
	}

	public static void REVIVE(Packet _packet)
	{
		MyMod.SimRevive();
		if (MyMod.sendMyPosition)
		{
			using (Packet packet = new Packet(15))
			{
				packet.Write(_value: true);
				MyMod.SendUDPData(packet);
			}
		}
	}

	public static void ANIMALROLE(Packet _packet)
	{
		bool flag = _packet.ReadBool();
		if (_packet.UnreadLength() < 8)
		{
			return;
		}
		int num = _packet.ReadInt();
		string text;
		try
		{
			text = _packet.ReadString();
		}
		catch (Exception)
		{
			return;
		}
		if (num != ClientUser.myId || !WildlifeSpawnAuthorityPolicy.AcceptGrant(!flag || MyMod.WildlifeSceneReady(), MyMod.level_guid, text))
		{
			return;
		}
		MyMod.WildlifeAuthorityScene = (flag ? text : "");
		if (MyMod.AnimalsController != flag)
		{
			MyMod.AnimalsController = flag;
			if (MyMod.AnimalsController)
			{
				WildlifeSpawnerAudit.HandoffQuietUntil = Time.realtimeSinceStartup + 4;
			}
			MelonLogger.Msg("[Wildlife] Local spawn controller=" + flag + " scene=" + text);
		}
	}

	public static void ALIGNANIMAL(Packet _packet)
	{
	}

	public static void ASKFORANIMALPROXY(Packet _packet)
	{
	}

	public static void ANIMALDELETE(Packet _packet)
	{
		MyMod.DeleteAnimal(_packet.ReadString());
	}

	public static void KEEPITALIVE(Packet _packet)
	{
		ConnectionWatchdog.ReceivedHeartbeat();
		MyMod.LastResponceTime = Time.time;
		MyMod.NoHostResponceSeconds = 0;
	}

	public static void RQRECONNECT(Packet _packet)
	{
		string reconnectAddress = ClientUser.LastConnectedIp;
		bool reconnectSteam = MyMod.ConnectedSteamWorks && !string.IsNullOrEmpty(MyMod.SteamServerWorks);
		string reconnectHost = MyMod.SteamServerWorks;
		MyMod.RequestSaveBeforeDisconnect(delegate
		{
			if (reconnectSteam)
			{
				SteamConnect.Main.ConnectToHost(reconnectHost);
			}
			else
			{
				ClientUser.DoConnectToIp(reconnectAddress);
			}
		});
	}

	public static void CHAT(Packet _packet)
	{
		DataStr.MultiplayerChatMessage message = _packet.ReadChat();
		_packet.ReadInt();
		Shared.SendMessageToChat(message, needSync: false);
	}

	public static void PLAYERSSTATUS(Packet _packet)
	{
		int num = _packet.ReadInt();
		if (num > MyMod.MaxPlayers)
		{
			return;
		}
		for (int i = 0; i < MyMod.MaxPlayers; i++)
		{
			if (MyMod.playersData[i] != null)
			{
				MyMod.playersData[i].m_Used = false;
			}
		}
		System.Collections.Generic.List<DataStr.MultiPlayerClientStatus> list = new System.Collections.Generic.List<DataStr.MultiPlayerClientStatus>();
		int num2 = 0;
		int num3 = 0;
		for (int j = 1; j <= num; j++)
		{
			DataStr.MultiPlayerClientStatus multiPlayerClientStatus = _packet.ReadClientStatus();
			list.Add(multiPlayerClientStatus);
			if (MyMod.playersData[multiPlayerClientStatus.m_ID] != null)
			{
				MyMod.playersData[multiPlayerClientStatus.m_ID].m_Used = true;
				MyMod.playersData[multiPlayerClientStatus.m_ID].m_Name = multiPlayerClientStatus.m_Name;
				MyMod.playersData[multiPlayerClientStatus.m_ID].m_IsLoading = multiPlayerClientStatus.m_IsLoading;
				if (multiPlayerClientStatus.m_Dead || multiPlayerClientStatus.m_Sleep)
				{
					num2++;
				}
				if (multiPlayerClientStatus.m_Dead)
				{
					num3++;
				}
				if (multiPlayerClientStatus.m_Sleep)
				{
					MyMod.playersData[multiPlayerClientStatus.m_ID].m_SleepHours = 1;
				}
				else
				{
					MyMod.playersData[multiPlayerClientStatus.m_ID].m_SleepHours = 0;
				}
				MyMod.playersData[multiPlayerClientStatus.m_ID].m_Dead = multiPlayerClientStatus.m_Dead;
			}
		}
		int finalHours = _packet.ReadInt();
		Shared.ProcessSleep(num2, num, num3, finalHours);
		MyMod.UpdatePlayerStatusMenu(list);
	}

	public static void ANIMALTEST(Packet _packet)
	{
		DataStr.AnimalCompactData dat = _packet.ReadAnimalCompactData();
		DataStr.AnimalAnimsSync anim = _packet.ReadAnimalAnim();
		int sender = _packet.ReadInt();
		if (sender < 0 || sender >= MyMod.playersData.Count || sender == ClientUser.myId)
			return;
		string scene = _packet.UnreadLength() >= 4 ? _packet.ReadString() : MyMod.playersData[sender]?.m_LevelGuid;
		if (WildlifeSpawnAuthorityPolicy.SameScene(scene, MyMod.level_guid))
			MyMod.DoAnimalSync(dat, anim, sender);
	}

	public static void ANIMALSYNCTRIGG(Packet _packet)
	{
		if (!(MyMod.level_name == "Boot") && !(MyMod.level_name == "Empty"))
		{
			MyMod.SetAnimalTriggers(_packet.ReadAnimalTrigger());
		}
	}

	public static void REVIVEDONE(Packet _packet)
	{
		GameManager.GetInventoryComponent().RemoveGearFromInventory("GEAR_MedicalSupplies_hangar", 1);
	}

	public static void DARKWALKERREADY(Packet _packet)
	{
	}

	public static void HOSTISDARKWALKER(Packet _packet)
	{
	}

	public static void WARDISACTIVE(Packet _packet)
	{
	}

	public static void DWCOUNTDOWN(Packet _packet)
	{
	}

	public static void SHOOTSYNC(Packet _packet)
	{
		DataStr.ShootSync shoot = _packet.ReadShoot();
		int num = _packet.ReadInt();
		MyMod.DoShootSync(shoot, num);
	}

	public static void PIMPSKILL(Packet _packet)
	{
		switch (_packet.ReadInt())
		{
		case 1:
			GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Rifle, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
			MelonLogger.Msg("Got remote skill upgrade Rifle");
			break;
		case 2:
			GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Revolver, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
			MelonLogger.Msg("Got remote skill upgrade Revolver");
			break;
		}
	}

	public static void HARVESTINGANIMAL(Packet _packet)
	{
		string harvestingAnimal = _packet.ReadString();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_HarvestingAnimal = harvestingAnimal;
		}
	}

	public static void BULLETDAMAGE(Packet _packet)
	{
		float damage = _packet.ReadFloat();
		int bodypart = _packet.ReadInt();
		int num = _packet.ReadInt();
		bool flag = _packet.ReadBool();
		string meleeWeapon = "";
		if (flag)
		{
			meleeWeapon = _packet.ReadString();
		}
		MyMod.DamageByBullet(damage, num, bodypart, flag, meleeWeapon);
	}

	public static void MULTISOUND(Packet _packet)
	{
		string sound = _packet.ReadString();
		int num = _packet.ReadInt();
		MyMod.PlayMultiplayer3dAduio(sound, num);
	}

	public static void CONTAINEROPEN(Packet _packet)
	{
		MyMod.DoSyncContainer(_packet.ReadContainer());
	}

	public static void LUREPLACEMENT(Packet _packet)
	{
	}

	public static void LUREISACTIVE(Packet _packet)
	{
	}

	private static bool receivedSaveData;
	private static bool awaitingSaveVerification;

	internal static void ResetSaveJoin()
	{
		receivedSaveData = false;
		awaitingSaveVerification = false;
		MyMod.PendingSave = null;
		MyMod.PendingSaveSlotInfo = null;
		MyMod.ShouldCreateSaveForHost = false;
	}

	public static void SAVEDATA(Packet _packet)
	{
		// The host answers every repeated Welcome ack with SAVEDATA; handle one per join or character creation opens several times.
		if (receivedSaveData || !Shared.IsMainMenuScene(MyMod.level_name))
		{
			MelonLogger.Msg("Ignored duplicate SAVEDATA");
			return;
		}
		DataStr.SaveSlotSync saveSlotSync = _packet.ReadSaveSlot();
		if (saveSlotSync.m_WorldId != "" && !System.Guid.TryParseExact(saveSlotSync.m_WorldId, "N", out _))
		{
			MyMod.DoKickMessage("The host sent an invalid world ID.");
			return;
		}
		receivedSaveData = true;
		awaitingSaveVerification = true;
		MyMod.RemoveWaitForConnect();
		MyMod.ApplyOtherCampfires = true;
		MyMod.PendingSave = saveSlotSync;
		MyMod.CheckHaveSaveFileToJoin(saveSlotSync);
	}

	public static void VERIFYSAVE(Packet _packet)
	{
		if (!awaitingSaveVerification || !Shared.IsMainMenuScene(MyMod.level_name))
		{
			MelonLogger.Msg("Ignored duplicate VERIFYSAVE");
			return;
		}
		awaitingSaveVerification = false;
		MyMod.RemoveWaitForConnect();
		MyMod.RemovePleaseWait();
		MyMod.MyUGUID = _packet.ReadString();
		bool valid = _packet.ReadBool();
		MyMod.VerifiedSave(valid);
	}

	public static void CARRYBODY(Packet _packet)
	{
	}

	public static void BODYWARP(Packet _packet)
	{
	}

	public static void CHANGENAME(Packet _packet)
	{
		string name = _packet.ReadString();
		int index = _packet.ReadInt();
		MyMod.playersData[index].m_Name = name;
	}

	public static void CLOTH(Packet _packet)
	{
		DataStr.PlayerClothingData playerClothingData = _packet.ReadClothingData();
		int index = _packet.ReadInt();
		MyMod.playersData[index].m_PlayerClothingData = playerClothingData;
	}

	public static void ASKSPAWNDATA(Packet _packet)
	{
		int num = _packet.ReadInt();
		_packet.ReadInt();
		if (num == MyMod.levelid)
		{
			MyMod.SendSpawnData();
		}
	}

	public static void FURNBROKEN(Packet _packet)
	{
		DataStr.BrokenFurnitureSync brokenFurnitureSync = _packet.ReadFurn();
		if (!(brokenFurnitureSync.m_LevelGUID == MyMod.level_guid))
		{
			return;
		}
		if (brokenFurnitureSync.m_Broken)
		{
			if (MyMod.DelayedGearsPickup)
			{
				MyMod.BrokenFurnsBackup.Add(brokenFurnitureSync);
			}
			else
			{
				MyMod.RemoveBrokenFurniture(brokenFurnitureSync.m_Guid, brokenFurnitureSync.m_ParentGuid);
			}
		}
		else
		{
			MyMod.RepairBrokenFurniture(brokenFurnitureSync.m_Guid, brokenFurnitureSync.m_ParentGuid);
		}
	}

	public static void FURNBROKENLIST(Packet _packet)
	{
	}

	public static void FURNBREAKINGGUID(Packet _packet)
	{
		DataStr.BrokenFurnitureSync brokenFurnitureSync = _packet.ReadFurn();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_BrakingObject = brokenFurnitureSync;
			if (MyMod.playersData[index].m_LevelGuid == MyMod.level_guid)
			{
				MyMod.playersData[index].m_BrakingSounds = MyMod.GetBreakDownSound(brokenFurnitureSync);
			}
		}
	}

	public static void FURNBREAKINSTOP(Packet _packet)
	{
		_packet.ReadBool();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_BrakingObject = new DataStr.BrokenFurnitureSync();
			MyMod.playersData[index].m_BrakingSounds = "";
		}
	}

	public static void ProcessDeleyedGearsPickcup()
	{
		MyMod.DelayedGearsPickup = false;
		foreach (DataStr.PickedGearSync item in MyMod.PickedGearsBackup)
		{
			Shared.AddPickedGear(item.m_Spawn, item.m_LevelID, item.m_LevelGUID, -1, item.m_MyInstanceID, item.m_GearName, needSync: false);
		}
		MyMod.FoundSomethingToBreak = false;
		foreach (DataStr.BrokenFurnitureSync item2 in MyMod.BrokenFurnsBackup)
		{
			MyMod.RemoveBrokenFurniture(item2.m_Guid, item2.m_ParentGuid, ReBake: false);
		}
		MyMod.BakePreSpawnedGearsList();
		if (MyMod.FoundSomethingToBreak)
		{
			MyMod.RemoveAttachedGears = 2;
		}
		else
		{
			MyMod.PickedGearsBackup.Clear();
		}
		MyMod.BrokenFurnsBackup.Clear();
	}

	public static void GEARPICKUP(Packet _packet)
	{
		DataStr.PickedGearSync pickedGearSync = _packet.ReadPickedGear();
		int num = _packet.ReadInt();
		if (num != -1 && MyMod.playersData[num] != null && MyMod.playersData[num].m_LevelGuid == MyMod.level_guid && MyMod.players[num] != null && MyMod.players[num].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
		{
			MyMod.players[num].GetComponent<Comps.MultiplayerPlayerAnimator>().Pickup();
		}
		if (MyMod.DelayedGearsPickup)
		{
			MyMod.PickedGearsBackup.Add(pickedGearSync);
		}
		else
		{
			Shared.AddPickedGear(pickedGearSync.m_Spawn, pickedGearSync.m_LevelID, pickedGearSync.m_LevelGUID, num, pickedGearSync.m_MyInstanceID, pickedGearSync.m_GearName, needSync: false);
		}
	}

	public static void GEARPICKUPLIST(Packet _packet)
	{
	}

	public static void ROPE(Packet _packet)
	{
		DataStr.ClimbingRopeSync climbingRopeSync = _packet.ReadRope();
		Shared.AddDeployedRopes(climbingRopeSync.m_Position, climbingRopeSync.m_Deployed, climbingRopeSync.m_Snapped, climbingRopeSync.m_LevelID, climbingRopeSync.m_LevelGUID, needSync: false);
	}

	public static void ROPELIST(Packet _packet)
	{
		int num = _packet.ReadInt();
		for (int i = 1; i <= num; i++)
		{
			DataStr.ClimbingRopeSync climbingRopeSync = _packet.ReadRope();
			if (!MyMod.DeployedRopes.Contains(climbingRopeSync))
			{
				Shared.AddDeployedRopes(climbingRopeSync.m_Position, climbingRopeSync.m_Deployed, climbingRopeSync.m_Snapped, climbingRopeSync.m_LevelID, climbingRopeSync.m_LevelGUID, needSync: false);
			}
		}
	}

	public static void CONSUME(Packet _packet)
	{
		bool isDrink = _packet.ReadBool();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null && MyMod.playersData[index].m_Levelid == MyMod.levelid && MyMod.playersData[index].m_LevelGuid == MyMod.level_guid && MyMod.players[index] != null && MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
		{
			MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>().m_IsDrink = isDrink;
			MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>().Consumption();
		}
	}

	public static void SERVERCFG(Packet _packet)
	{
		MyMod.ServerConfig = _packet.ReadServerCFG();
		MyMod.ShowCFGData();
	}

	public static void STOPCONSUME(Packet _packet)
	{
		string animState = _packet.ReadString();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			if (MyMod.playersData[index].m_Levelid == MyMod.levelid && MyMod.playersData[index].m_LevelGuid == MyMod.level_guid && MyMod.players[index] != null && MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
			{
				MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>().StopConsumption();
			}
			MyMod.playersData[index].m_AnimState = animState;
		}
	}

	public static void HEAVYBREATH(Packet _packet)
	{
		bool heavyBreath = _packet.ReadBool();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_HeavyBreath = heavyBreath;
		}
	}

	public static void BLOODLOSTS(Packet _packet)
	{
		int bloodLosts = _packet.ReadInt();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_BloodLosts = bloodLosts;
		}
	}

	public static void APPLYACTIONONPLAYER(Packet _packet)
	{
		string actionType = _packet.ReadString();
		int fromId = _packet.ReadInt();
		MyMod.OtherPlayerApplyActionOnMe(actionType, fromId);
	}

	public static void DONTMOVEWARNING(Packet _packet)
	{
		_packet.ReadBool();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			HUDMessage.AddMessage("PLEASE DON'T MOVE, " + MyMod.playersData[index].m_Name + " IS TENDING YOU");
			GameManager.GetVpFPSPlayer().Controller.Stop();
		}
	}

	public static void INFECTIONSRISK(Packet _packet)
	{
		bool needAntiseptic = _packet.ReadBool();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_NeedAntiseptic = needAntiseptic;
		}
	}

	public static void CANCLEPICKUP(Packet _packet)
	{
		DataStr.PickedGearSync pickedGearSync = _packet.ReadPickedGear();
		_packet.ReadInt();
		MelonLogger.Msg(ConsoleColor.Blue, "Other shokal has pickup item before me! I need to delete my picked gear Item InstanceID " + pickedGearSync.m_MyInstanceID);
		int myInstanceID = pickedGearSync.m_MyInstanceID;
		Il2CppSystem.Collections.Generic.List<GearItemObject> items = GameManager.GetInventoryComponent().m_Items;
		for (int i = 0; i < items.Count; i++)
		{
			GearItemObject gearItemObject = items[i];
			if (gearItemObject != null && gearItemObject.m_GearItem.m_InstanceID == myInstanceID)
			{
				HUDMessage.AddMessage("THIS ALREADY PICKED!");
				GameAudioManager.PlayGUIError();
				GameManager.GetInventoryComponent().DestroyGear(gearItemObject.m_GearItem.gameObject);
				return;
			}
		}
		for (int j = 0; j < GearManager.m_Gear.Count; j++)
		{
			GearItem gearItem = GearManager.m_Gear[j];
			if (gearItem.m_InstanceID == myInstanceID)
			{
				gearItem.gameObject.SetActive(value: false);
				UnityEngine.Object.Destroy(gearItem.gameObject);
				HUDMessage.AddMessage("THIS ALREADY PICKED!");
				GameAudioManager.PlayGUIError();
				break;
			}
		}
	}

	public static void CONTAINERINTERACT(Packet _packet)
	{
		DataStr.ContainerOpenSync containerOpenSync = _packet.ReadContainer();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			if (containerOpenSync.m_Guid == "NULL")
			{
				MyMod.playersData[index].m_Container = null;
			}
			else
			{
				MyMod.playersData[index].m_Container = containerOpenSync;
			}
		}
	}

	public static void LOOTEDCONTAINER(Packet _packet)
	{
		DataStr.ContainerOpenSync box = _packet.ReadContainer();
		int looter = _packet.ReadInt();
		int state = _packet.ReadInt();
		Shared.AddLootedContainer(box, needSync: false, looter, state);
	}

	public static void LOOTEDCONTAINERLIST(Packet _packet)
	{
	}

	public static void HARVESTPLANT(Packet _packet)
	{
		DataStr.HarvestableSyncData harvestableSyncData = _packet.ReadHarvestablePlant();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			if (harvestableSyncData.m_State == "Start")
			{
				MyMod.playersData[index].m_Plant = harvestableSyncData.m_Guid;
			}
			else
			{
				MyMod.playersData[index].m_Plant = "";
			}
		}
	}

	public static void LOOTEDHARVESTABLE(Packet _packet)
	{
		string harvGUID = _packet.ReadString();
		string text = _packet.ReadString();
		int num = _packet.ReadInt();
		if (text == MyMod.level_guid)
		{
			if (num == -1)
			{
				MyMod.AddHarvastedPlant(harvGUID);
			}
			else
			{
				MyMod.RemoveHarvastedPlant(harvGUID);
			}
		}
	}

	public static void LOOTEDHARVESTABLEALL(Packet _packet)
	{
	}

	public static void SELECTEDCHARACTER(Packet _packet)
	{
		int num = _packet.ReadInt();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_Character = num;
			if (num == 0)
			{
				MyMod.playersData[index].m_Female = false;
			}
			if (num == 1)
			{
				MyMod.playersData[index].m_Female = true;
			}
		}
	}

	public static void ADDSHELTER(Packet _packet)
	{
		MelonLogger.Msg("Someone created shelter!");
		DataStr.ShowShelterByOther showShelterByOther = _packet.ReadShelter();
		_packet.ReadInt();
		Shared.ShelterCreated(showShelterByOther.m_Position, showShelterByOther.m_Rotation, showShelterByOther.m_LevelID, showShelterByOther.m_LevelGUID, needSync: false);
	}

	public static void REMOVESHELTER(Packet _packet)
	{
		MelonLogger.Msg("Someone removed shelter!");
		DataStr.ShowShelterByOther showShelterByOther = _packet.ReadShelter();
		_packet.ReadInt();
		Shared.ShelterRemoved(showShelterByOther.m_Position, showShelterByOther.m_LevelID, showShelterByOther.m_LevelGUID, needSync: false);
	}

	public static void ALLSHELTERS(Packet _packet)
	{
		int num = _packet.ReadInt();
		for (int i = 1; i <= num; i++)
		{
			DataStr.ShowShelterByOther showShelterByOther = _packet.ReadShelter();
			if (!MyMod.ShowSheltersBuilded.Contains(showShelterByOther))
			{
				Shared.ShelterCreated(showShelterByOther.m_Position, showShelterByOther.m_Rotation, showShelterByOther.m_LevelID, showShelterByOther.m_LevelGUID, needSync: false);
			}
		}
	}

	public static void USESHELTER(Packet _packet)
	{
		DataStr.ShowShelterByOther showShelterByOther = _packet.ReadShelter();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			if (showShelterByOther.m_Position == new Vector3(0f, 0f, 0f))
			{
				MyMod.playersData[index].m_Shelter = null;
			}
			else
			{
				MyMod.playersData[index].m_Shelter = showShelterByOther;
			}
		}
	}

	public static void FIRE(Packet _packet)
	{
		FireHeatSync.ReceiveSnapshot(_packet.ReadFire());
	}

	public static void FIREFUEL(Packet _packet)
	{
		FireHeatSync.Acknowledge(_packet.ReadFire().m_Operation);
	}

	public static void CUSTOM(Packet _packet)
	{
		API.CustomEventCallback(_packet, -1);
	}

	public static void KICKMESSAGE(Packet _packet)
	{
		MyMod.DoKickMessage(_packet.ReadString());
	}

	public static void PrintModsList(string RawString)
	{
		MelonLogger.Msg(ConsoleColor.Red, "SERVER MODS:\n" + RawString.Replace("M\\", "Mods\\").Replace("A\\", "Mods\\").Replace("P\\", "Plugins\\"));
	}

	public static void MODSLIST(Packet _packet)
	{
		PrintModsList(Shared.DecompressString(_packet.ReadString()));
	}

	public static void GOTITEMSLICE(Packet _packet)
	{
		MyMod.AddSlicedJsonData(_packet.ReadSlicedGear());
	}

	public static void VOICECHAT(Packet _packet)
	{
		int arraySize = _packet.ReadInt();
		int length = _packet.ReadInt();
		byte[] array = _packet.ReadBytes(length);
		float recordTime = _packet.ReadFloat();
		float num = _packet.ReadFloat();
		num = Mathf.Round(num * 10f) * 0.1f;
		int num2 = _packet.ReadInt();
		int num3 = _packet.ReadInt();
		if (MyMod.playersData[num2] != null && MyMod.playersData[num2].m_LevelGuid == MyMod.level_guid)
		{
			bool radio = false;
			if (num2 != num3)
			{
				radio = true;
			}
			if (!MyMod.DedicatedServerAppMode)
			{
				MyMod.ProcessVoiceChatData(num2, array, uint.Parse(arraySize.ToString()), recordTime, radio);
			}
		}
		if (MyMod.RadioFrequency == num && MyMod.playersData[num2].m_PlayerEquipmentData.m_HoldingItem == "GEAR_HandheldShortwave" && !MyMod.DoingRecord)
		{
			MyMod.ProcessRadioChatData(array, uint.Parse(arraySize.ToString()), recordTime);
			MyMod.SendMyRadioAudio(array, arraySize, recordTime, num3);
		}
	}

	public static void SLICEDBYTES(Packet _packet)
	{
	}

	public static void ANIMALDAMAGE(Packet _packet)
	{
		string guid = _packet.ReadString();
		float damage = _packet.ReadFloat();
		MyMod.DoAnimalDamage(guid, damage);
	}

	public static void DROPITEM(Packet _packet)
	{
		Shared.FakeDropItem(_packet.ReadDroppedGearData());
	}

	public static void PICKDROPPEDGEAR(Packet _packet)
	{
		int hash = _packet.ReadInt();
		int picker = _packet.ReadInt();
		MyMod.PickDroppedItem(hash, picker);
	}

	public static void GETREQUESTEDITEMSLICE(Packet _packet)
	{
		MyMod.AddSlicedJsonDataForPicker(_packet.ReadSlicedGear(), place: false);
	}

	public static void GETREQUESTEDFORPLACESLICE(Packet _packet)
	{
		MyMod.AddSlicedJsonDataForPicker(_packet.ReadSlicedGear(), place: true);
	}

	public static void GOTCONTAINERSLICE(Packet _packet)
	{
	}

	public static void OPENEMPTYCONTAINER(Packet _packet)
	{
		bool flag = _packet.ReadBool();
		_packet.ReadInt();
		string scene = _packet.ReadString();
		string guid = _packet.ReadString();
		if (!ContainerLootSync.MatchesPending(scene, guid))
		{
			return;
		}
		if (!flag)
		{
			ContainerLootSync.RejectPending(scene, guid);
			return;
		}
		MelonLogger.Msg("Host sent that this container is empty!");
		if (MyMod.LastPacketForRepeat == MyMod.ResendPacketType.Container)
		{
			MyMod.DiscardRepeatPacket();
		}
		MyMod.FinishOpeningFakeContainer("");
	}

	public static void MARKSEARCHEDCONTAINERS(Packet _packet)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(_packet.ReadString());
		if (gameObject != null)
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(MyMod.GetGearItemObject("GEAR_SoftWood"), gameObject.transform.position, gameObject.transform.rotation);
			gameObject.GetComponent<Container>().AddGear(gameObject2.GetComponent<GearItem>());
		}
	}

	public static void READYSENDNEXTSLICE(Packet _packet)
	{
	}

	public static void READYSENDNEXTSLICEGEAR(Packet _packet)
	{
		MyMod.SendNextGearCarefulSlice();
	}

	public static void READYSENDNEXTSLICEPHOTO(Packet _packet)
	{
	}

	public static void CHANGEAIM(Packet _packet)
	{
		bool aiming = _packet.ReadBool();
		int index = _packet.ReadInt();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_Aiming = aiming;
		}
	}

	public static void LOADINGSCENEDROPSDONE(Packet _packet)
	{
		MelonLogger.Msg("Host sent scene loading done!");
		MyMod.DiscardRepeatPacket();
		MyMod.RemovePleaseWait();
		if (MyMod.DelayedGearsPickup)
		{
			ProcessDeleyedGearsPickcup();
		}
	}

	public static void GEARNOTEXIST(Packet _packet)
	{
		MelonLogger.Msg("Gear I requested is not exist!");
		MyMod.PickDroppedItem(MyMod.ResendGearSearchKey,-1);
		MyMod.DiscardRepeatPacket();
		MyMod.RemovePleaseWait();
	}

	public static void DOORLOCKEDMSG(Packet _packet)
	{
		MyMod.DiscardRepeatPacket();
		MyMod.RemovePleaseWait();
		string text = _packet.ReadString();
		HUDMessage.AddMessage(text);
		if (text == "Incorrect key!")
		{
			GameAudioManager.PlaySound("Play_SndMechDoorWoodLocked01", GameAudioManager.Instance.gameObject);
		}
	}

	public static void USEOPENABLE(Packet _packet)
	{
		string gUID = _packet.ReadString();
		bool state = _packet.ReadBool();
		Shared.ChangeOpenableThingState(MyMod.level_guid, gUID, state);
	}

	public static void TRYDIAGNISISPLAYER(Packet _packet)
	{
		MelonLogger.Msg(ConsoleColor.Green, "TRYDIAGNISISPLAYER");
		int playerSendTo = _packet.ReadInt();
		Condition conditionComponent = GameManager.GetConditionComponent();
		Hunger hungerComponent = GameManager.GetHungerComponent();
		Thirst thirstComponent = GameManager.GetThirstComponent();
		MyMod.SendMyAffictions(playerSendTo, conditionComponent.m_CurrentHP, conditionComponent.m_MaxHP, thirstComponent.m_CurrentThirst, hungerComponent.m_CurrentReserveCalories, hungerComponent.m_MaxReserveCalories);
		MelonLogger.Msg(ConsoleColor.Green, "SendMyAffictions(" + playerSendTo + ", " + GameManager.GetConditionComponent().m_CurrentHP + ");");
	}

	public static void SENDMYAFFLCTIONS(Packet _packet)
	{
		int playerID = _packet.ReadInt();
		int num = _packet.ReadInt();
		float health = _packet.ReadFloat();
		float maxhealth = _packet.ReadFloat();
		float thirst = _packet.ReadFloat();
		float hunger = _packet.ReadFloat();
		float hungermax = _packet.ReadFloat();
		System.Collections.Generic.List<DataStr.AffictionSync> list = new System.Collections.Generic.List<DataStr.AffictionSync>();
		for (int i = 0; i < num; i++)
		{
			DataStr.AffictionSync item = _packet.ReadAffiction();
			list.Add(item);
		}
		MyMod.CheckOtherPlayer(list, playerID, health, maxhealth, thirst, hunger, hungermax);
	}

	public static void CUREAFFLICTION(Packet _packet)
	{
		DataStr.AffictionSync aff = _packet.ReadAffiction();
		int firstAidTier = _packet.ReadInt();
		bool medkit = _packet.ReadBool();
		MyMod.OtherPlayerCuredMyAffiction(_packet.ReadInt(), aff, firstAidTier, medkit);
	}

	public static void ANIMALCORPSE(Packet _packet)
	{
		MyMod.ProcessAnimalCorpseSync(_packet.ReadAnimalCorpse());
	}

	public static void GOTRABBIT(Packet _packet)
	{
		MyMod.FinallyPickupRabbit(_packet.ReadInt(), _packet.ReadString());
	}

	public static void RELEASERABBIT(Packet _packet)
	{
		MyMod.OnReleaseRabbit(_packet.ReadInt(), _packet.ReadString());
	}

	public static void HITRABBIT(Packet _packet)
	{
		string guid = _packet.ReadString();
		string scene = _packet.UnreadLength() >= 4 ? _packet.ReadString() : MyMod.level_guid;
		if (MyMod.WildlifeSceneReady() && WildlifeSpawnAuthorityPolicy.SameScene(scene, MyMod.level_guid))
			MyMod.OnHitRabbit(guid);
	}

	public static void RABBITREVIVED(Packet _packet)
	{
		Vector3 v = _packet.ReadVector3();
		if (MyMod.CanSpawnWildlifeInCurrentScene())
		{
			MyMod.OnRabbitRevived(v, _packet.ReadString(), _packet.ReadString());
		}
	}

	public static void REQUESTANIMALCORPSE(Packet _packet)
	{
		float num = _packet.ReadFloat();
		int gutAvailableUnits = _packet.ReadInt();
		int hideAvailableUnits = _packet.ReadInt();
		if (num != -1f)
		{
			if ((bool)MyMod.GoingToHarvest)
			{
				MyMod.GoingToHarvest.m_MeatAvailableKG = GameCompat.Kilograms(num);
				MyMod.GoingToHarvest.m_GutAvailableUnits = gutAvailableUnits;
				MyMod.GoingToHarvest.m_HideAvailableUnits = hideAvailableUnits;
				MyMod.DiscardRepeatPacket();
				MyMod.OpenBodyHarvest(MyMod.GoingToHarvest);
			}
		}
		else
		{
			MyMod.DiscardRepeatPacket();
			MyMod.RemovePleaseWait();
			HUDMessage.AddMessage("Nothing to harvest");
		}
	}

	public static void QUARTERANIMAL(Packet _packet)
	{
		string text = _packet.ReadString();
		MelonLogger.Msg("QUARTERANIMAL " + text);
		MyMod.SpawnQuartedMess(text);
	}

	public static void ANIMALAUDIO(Packet _packet)
	{
		bool num = _packet.ReadBool();
		string gUID = _packet.ReadString();
		if (num)
		{
			int soundID = _packet.ReadInt();
			int index = _packet.ReadInt();
			if (MyMod.playersData[index] != null && MyMod.level_guid == MyMod.playersData[index].m_LevelGuid)
			{
				Pathes.Play3dAudioOnAnimal(gUID, soundID);
			}
		}
		else
		{
			string soundID2 = _packet.ReadString();
			int index2 = _packet.ReadInt();
			if (MyMod.playersData[index2] != null && MyMod.level_guid == MyMod.playersData[index2].m_LevelGuid)
			{
				Pathes.Play3dAudioOnAnimal(gUID, soundID2);
			}
		}
	}

	public static void MELEESTART(Packet _packet)
	{
		int index = _packet.ReadInt();
		if (MyMod.players[index] != null && MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
		{
			MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>().MeleeAttack();
		}
	}

	public static void TRYBORROWGEAR(Packet _packet)
	{
		string gearName = _packet.ReadString();
		int borrower = _packet.ReadInt();
		MyMod.GiveBorrowedItem(gearName, borrower);
	}

	public static void CHALLENGEINIT(Packet _packet)
	{
		int iD = _packet.ReadInt();
		int currentTask = _packet.ReadInt();
		MyMod.LoadCustomChallenge(iD, currentTask);
	}

	public static void CHALLENGEUPDATE(Packet _packet)
	{
		MyMod.CurrentCustomChalleng = _packet.ReadChallengeData();
	}

	public static void CHALLENGETRIGGER(Packet _packet)
	{
		MyMod.ProcessCustomChallengeTrigger(_packet.ReadString());
	}

	public static void ADDDEATHCONTAINER(Packet _packet)
	{
		DataStr.DeathContainerData deathContainerData = _packet.ReadDeathContainer();
		if (MyMod.level_guid == deathContainerData.m_LevelKey)
		{
			MyMod.MakeDeathCreate(deathContainerData);
		}
	}

	public static void DEATHCREATEEMPTYNOW(Packet _packet)
	{
		string gUID = _packet.ReadString();
		string scene = _packet.ReadString();
		MyMod.RemoveDeathContainer(gUID, scene);
	}

	public static void SPAWNREGIONBANCHECK(Packet _packet)
	{
		string guid = _packet.ReadString();
		bool banned = _packet.ReadBool();
		var region = WildlifeSpawnerAudit.ResolveRegion(guid);
		if (region != null) region.SetBanned(banned);
	}

	public static void CAIRNS(Packet _packet)
	{
		int num = _packet.ReadInt();
		for (int i = 0; i < num; i++)
		{
			MyMod.AddFoundCairn(_packet.ReadInt());
		}
		MyMod.CreateCairnsSearchList();
	}

	public static void BENEFITINIT(Packet _packet)
	{
		int index = _packet.ReadInt();
		Supporters.SupporterBenefits supporterBenefits = _packet.ReadSupporterBenefits();
		if (MyMod.playersData[index] != null)
		{
			MyMod.playersData[index].m_SupporterBenefits = supporterBenefits;
			for (int i = 0; i < supporterBenefits.m_Flairs.Count; i++)
			{
				Utils.Log("[BENEFITINIT] From Client " + index + " Slot" + i + " FlairID " + supporterBenefits.m_Flairs[i]);
			}
		}
	}

	public static void ADDDOORLOCK(Packet _packet)
	{
		MyMod.AddLocksToDoorsByGUID(_packet.ReadString());
	}

	public static void ENTERDOOR(Packet _packet)
	{
		MyMod.DiscardRepeatPacket();
		MyMod.RemovePleaseWait();
		MyMod.EnterDoorsByGUID(_packet.ReadString());
	}

	public static void REMOVEDOORLOCK(Packet _packet)
	{
		MyMod.DiscardRepeatPacket();
		MyMod.RemovePleaseWait();
		MyMod.RemoveLocksFromDoorsByGUID(_packet.ReadString());
	}

	public static void LOCKPICK(Packet _packet)
	{
		MyMod.SwearOnLockpickingDone = _packet.ReadBool();
	}

	public static void RPC(Packet _packet)
	{
		uConsole.RunCommand(_packet.ReadString());
	}

	public static void REQUESTLOCKSMITH(Packet _packet)
	{
		MyMod.RemovePleaseWait();
		MyMod.DiscardRepeatPacket();
		if (_packet.ReadInt() != -1)
		{
			if ((bool)MyMod.PendingLocksmithObject)
			{
				_ = MyMod.PendingLocksmithObject.GetComponent<Comps.DroppedGearDummy>().m_Extra.m_GearName;
				DataStr.PriorityActionForOtherPlayer customAction = MyMod.GetCustomAction("Locksmith" + MyMod.PendingLocksmithAction);
				MyMod.DoLongAction(MyMod.PendingLocksmithObject, customAction.m_ProcessText, customAction.m_Action, customAction.m_ActionDuration, customAction.m_Hold);
			}
		}
		else
		{
			HUDMessage.AddMessage("Someone already work on this!");
			GameAudioManager.PlayGUIError();
		}
	}

	public static void KNOCKKNOCK(Packet _packet)
	{
		HUDMessage.AddMessage("Somebody's knocking on the door");
	}

	public static void KNOCKENTER(Packet _packet)
	{
		MyMod.EnterDoorsByScene(_packet.ReadString());
	}

	public static void PEEPHOLE(Packet _packet)
	{
		MyMod.RemovePleaseWait();
		if (MyMod.TempDoor != null)
		{
			MyMod.ShowKnockersPicker(MyMod.TempDoor, _packet.ReadIntList());
		}
	}

	public static void RESTART(Packet _packet)
	{
		MyMod.RemovePleaseWait();
		MyMod.SetRepeatPacket(MyMod.ResendPacketType.ServerRestart);
		MyMod.RepeatLastRequest();
	}

    private static string syncedWeatherScene;
    private static float syncedWeatherUntil;
    internal static bool HasCurrentWeatherSync => MyMod.InOnline() && syncedWeatherScene == MyMod.level_guid &&
        Time.realtimeSinceStartup < syncedWeatherUntil;

    public static void DoWeatherSync(float start, int seed, float duration, WeatherStage type, int index,
        System.Collections.Generic.List<float> durations, System.Collections.Generic.List<float> transitions,
        int tod, float high, float low, int previousStage, string setName)
    {
        if (MyMod.LoadingScreenIsOn || !WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_name) ||
            !float.IsFinite(start) || !float.IsFinite(duration) || duration <= 0) return;
        var weather = GameManager.m_Weather;
        var transition = GameManager.m_WeatherTransition;
        if (weather == null || transition == null || transition.m_CurrentWeatherSet == null) return;
        var sets = weather.m_WeatherSetsForScene;
        if (sets == null || string.IsNullOrEmpty(setName)) return;
        WeatherSetData selected = null;
        for (int i = 0; i < sets.Count; i++)
        {
            var candidate = sets[i];
            if (candidate != null && candidate.name == setName && candidate.m_CharacterizingType == type)
            {
                selected = candidate;
                break;
            }
        }
        var current = transition.m_CurrentWeatherSet;
        if (selected == null && current.name == setName && current.m_CharacterizingType == type) selected = current;
        if (selected == null || selected.m_WeatherStages == null || selected.m_WeatherStages.Length == 0)
        {
            syncedWeatherUntil = 0;
            return;
        }
        selected.m_CurrentSetDuration = duration;
        for (int i = 0; i < selected.m_WeatherStages.Length && i < durations.Count && i < transitions.Count; i++)
        {
            selected.m_WeatherStages[i].m_CurrentDuration = durations[i];
            selected.m_WeatherStages[i].m_CurrentTransitionTime = transitions[i];
        }
        var previous = (WeatherStage)previousStage;
        if (current.name == setName)
            current.Activate(Mathf.Clamp01(start), previous);
        else
            transition.ActivateWeatherSet(selected, Mathf.Clamp01(start), previous);
        weather.m_TempHigh = high;
        weather.m_TempLow = low;
        syncedWeatherScene = MyMod.level_guid;
        syncedWeatherUntil = Time.realtimeSinceStartup + 4f;
    }

    public static void DEDICATEDWEATHER(Packet _packet)
	{
		WeatherStage sT = (WeatherStage)_packet.ReadInt();
		int indx = _packet.ReadInt();
		float startAtFrac = _packet.ReadFloat();
		int weatherSeed = _packet.ReadInt();
		float duration = _packet.ReadFloat();
		System.Collections.Generic.List<float> durations = _packet.ReadFloatList();
		System.Collections.Generic.List<float> transitions = _packet.ReadFloatList();
		int tOD = _packet.ReadInt();
		float high = _packet.ReadFloat();
		float low = _packet.ReadFloat();
		int previousStage = _packet.ReadInt();
		DoWeatherSync(startAtFrac, weatherSeed, duration, sT, indx, durations, transitions, tOD, high, low, previousStage, _packet.ReadString());
	}

	public static void WEATHERVOLUNTEER(Packet _packet)
	{
		int num = _packet.ReadInt();
		if (MyMod.level_name != "Boot" && MyMod.level_name != "Empty" && GameManager.m_Wind != null && GameManager.m_Wind.m_ActiveSettings != null && GameManager.m_Weather != null && GameManager.m_WeatherTransition != null && GameManager.GetUniStorm() != null && RegionCompat.GetCurrentRegion() == (Shared.GameRegion)num)
		{
			using (Packet packet = new Packet(151))
			{
				packet.Write(num);
				MyMod.SendUDPData(packet);
			}
		}
	}

	public static void REREGISTERWEATHER(Packet _packet)
	{
		int num = _packet.ReadInt();
		if (MyMod.level_name != "Boot" && MyMod.level_name != "Empty" && GameManager.m_Wind != null && GameManager.m_Wind.m_ActiveSettings != null && GameManager.m_Weather != null && GameManager.m_WeatherTransition != null && GameManager.GetUniStorm() != null && RegionCompat.GetCurrentRegion() == (Shared.GameRegion)num)
		{
			Pathes.SendWeatherVolunteerData();
		}
	}

	public static void REMOVEKEYBYSEED(Packet _packet)
	{
		MyMod.RemoveKey(_packet.ReadString());
	}

	public static void ADDHUDMSG(Packet _packet)
	{
		HUDMessage.AddMessage(_packet.ReadString());
	}

	public static void CHANGECONTAINERSTATE(Packet _packet)
	{
		string text = _packet.ReadString();
		int state = _packet.ReadInt();
		MelonLogger.Msg("Signal to container " + text + " state changed to " + state);
		MyMod.RemoveLootFromContainer(text, state);
	}

	public static void FINISHEDSENDINGCONTAINER(Packet _packet)
	{
		bool flag = _packet.ReadBool();
		MelonLogger.Msg("FINISHEDSENDINGCONTAINER Error " + flag);
		string text;
		string guid;
		string checksum;
		try
		{
			text = _packet.ReadString();
			guid = _packet.ReadString();
			checksum = _packet.ReadString();
		}
		catch (Exception)
		{
			MelonLogger.Warning("[Container loot] Ignored ACK without upload identity");
			return;
		}
		if (!ContainerLootSync.UploadMatches(text, guid, checksum))
		{
			MelonLogger.Warning("[Container loot] Ignored stale/mismatched upload ACK");
			return;
		}
		if (!flag)
		{
			ContainerLootSync.CompleteUpload();
			return;
		}
		string containerGUIDDataBackup = Shared.ContainerGUIDDataBackup;
		if (!string.IsNullOrEmpty(Shared.ContainerDecompressedDataBackup) && !string.IsNullOrEmpty(containerGUIDDataBackup))
		{
			MelonLogger.Warning("[Container flow] Retrying unconfirmed contents upload.");
			Shared.SendContainerData(Shared.CompressString(Shared.ContainerDecompressedDataBackup), text, containerGUIDDataBackup, Shared.ContainerDecompressedDataBackup);
		}
	}

	public static void TRIGGEREMOTE(Packet _packet)
	{
		int index = _packet.ReadInt();
		int iD = _packet.ReadInt();
		if (MyMod.playersData[index] == null || !(MyMod.players[index] != null) || !(MyMod.playersData[index].m_LevelGuid == MyMod.level_guid))
		{
			return;
		}
		Comps.MultiplayerPlayerAnimator component = MyMod.players[index].GetComponent<Comps.MultiplayerPlayerAnimator>();
		if (component != null)
		{
			DataStr.MultiplayerEmote emoteByID = MyMod.GetEmoteByID(iD);
			if (emoteByID.m_LeftHandEmote)
			{
				component.DoLeftHandEmote(emoteByID.m_Animation);
			}
		}
	}

	public static void EXPEDITIONSYNC(Packet _packet)
	{
		string text = _packet.ReadUnicodeString();
		string text2 = _packet.ReadUnicodeString();
		int num = _packet.ReadInt();
		string lastExpeditionAlias = _packet.ReadString();
		if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(text2) && num == 0)
		{
			MyMod.OnExpedition = false;
			return;
		}
		MyMod.OnExpedition = true;
		MyMod.ExpeditionLastName = text;
		MyMod.ExpeditionLastTaskText = text2;
		MyMod.ExpeditionLastTime = num;
		MyMod.LastExpeditionAlias = lastExpeditionAlias;
	}

	public static void EXPEDITIONRESULT(Packet _packet)
	{
		MyMod.DoExpeditionState(_packet.ReadInt());
	}

	public static void REQUESTEXPEDITIONINVITES(Packet _packet)
	{
		MyMod.RemovePleaseWait();
		MyMod.DiscardRepeatPacket();
		MyMod.ShowInvitesPicker(null, _packet.ReadInvitesList());
	}

	public static void NEWPLAYEREXPEDITION(Packet _packet)
	{
		MyMod.NewPlayerInExpedition(_packet.ReadString());
	}

	public static void NEWEXPEDITIONINVITE(Packet _packet)
	{
		MyMod.NewExpeditionInvite(_packet.ReadString());
	}

	public static void BASE64SLICE(Packet _packet)
	{
		Shared.AddBase64Slice(_packet.ReadSlicedBase64Data());
	}

	public static void COUGARSTATE(Packet _packet)
	{
		FarTerritory.CougarSync.Apply(_packet.ReadCougarState());
	}

	public static void TRADERSTATE(Packet _packet)
	{
		FarTerritory.TraderSync.Apply(_packet.ReadTraderState().m_SerializedState);
	}

	public static void TRAVOISSYNC(Packet _packet)
	{
		FarTerritory.TravoisSyncing.Receive(_packet.ReadTravoisSync());
	}

	public static void NOISEMAKERIGNITE(Packet _packet)
	{
		string guid = _packet.ReadString();
		string levelGuid = _packet.ReadString();
		int levelId = _packet.ReadInt();
		FarTerritory.NoiseMakerSync.Receive(guid, levelGuid, levelId);
	}

	public static void ADDROCKCACH(Packet _packet)
	{
		MyMod.AddRockCache(_packet.ReadFakeRockCache());
	}

	public static void REMOVEROCKCACH(Packet _packet)
	{
		DataStr.FakeRockCacheVisualData data = _packet.ReadFakeRockCache();
		switch (_packet.ReadInt())
		{
		case -1:
		{
			MyMod.RemovePleaseWait();
			MyMod.PendingRockCahceRemove = null;
			using Packet packet = new Packet(52);
			packet.Write(_value: true);
			MyMod.SendUDPData(packet);
			break;
		}
		case 0:
			MyMod.RemovePleaseWait();
			MyMod.ContinueRemovingRockCache();
			break;
		case 1:
			MyMod.RemoveRockCache(data);
			break;
		}
	}

	public static void ADDUNIVERSALSYNCABLE(Packet _packet)
	{
		MyMod.SpawnUniversalSyncableObject(_packet.ReadUniversalSyncable());
	}

	public static void REMOVEUNIVERSALSYNCABLE(Packet _packet)
	{
		MyMod.RemoveObjectByGUID(_packet.ReadString());
	}

	public static void CUSTOMSOUNDEVENT(Packet _packet)
	{
		Vector3 position = _packet.ReadVector3();
		string sound = _packet.ReadString();
		string prefab = _packet.ReadString();
		MyMod.PlayCustomSoundEvent(position, sound, prefab);
	}

	public static void REQUESTSPECIALEXPEDITION(Packet _packet)
	{
		if (_packet.ReadBool())
		{
			if (!string.IsNullOrEmpty(MyMod.DeleteGearOnExpeditionConfirmed))
			{
				GameManager.GetInventoryComponent().RemoveGearFromInventory(MyMod.DeleteGearOnExpeditionConfirmed, 1);
				MyMod.DeleteGearOnExpeditionConfirmed = "";
			}
			MyMod.PlayRadioOver();
		}
		MyMod.RemovePleaseWait();
	}

	public static void REQUESTSPHOTOAGAIN(Packet _packet)
	{
		string gUID = _packet.ReadString();
		string text = MPSaveManager.LoadPhoto(gUID);
		if (string.IsNullOrEmpty(text))
		{
			text = MPSaveManager.LoadPhoto(gUID, Cached: true);
		}
		Shared.SendSlicedBase64Data(Shared.GetBase64Sliced(text, gUID, DataStr.SlicedBase64Purpose.Photo));
	}

	public static void REQUESTEXPEDITIONSPROGRESS(Packet _packet)
	{
		System.Collections.Generic.List<int> list = _packet.ReadIntList();
		System.Collections.Generic.List<int> list2 = _packet.ReadIntList();
		MelonLogger.Msg(ConsoleColor.Green, "Total Progress: " + _packet.ReadInt());
		for (int i = 0; i < list.Count; i++)
		{
			MelonLogger.Msg(ConsoleColor.Green, ExpeditionBuilder.GetRegionString(list[i]) + ": " + list2[i]);
		}
	}

	public static void REGISTERSPEICALITEM(Packet _packet)
	{
		ExpeditionManager.AddSpecialItem(_packet.ReadSpecialExpeditionItem());
	}

	public static void REQUESTSPECIALITEMS(Packet _packet)
	{
		System.Collections.Generic.List<string> itemNames = _packet.ReadStringList();
		MyMod.DiscardRepeatPacket();
		MyMod.ShowCluesPicker(itemNames);
	}

	public static void ANIMALSTOPSFEEDING(Packet _packet)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(_packet.ReadString());
		if (gameObject == null)
		{
			BaseAi component = gameObject.GetComponent<BaseAi>();
			if ((bool)component)
			{
				component.m_TargetBodyHarvest = null;
			}
		}
	}
}
