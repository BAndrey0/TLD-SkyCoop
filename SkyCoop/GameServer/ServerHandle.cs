using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using SkyCoop;
using UnityEngine;

namespace GameServer;

internal class ServerHandle
{
	public static void Log(string TXT, Shared.LoggerColor Color = Shared.LoggerColor.White)
	{
		MelonLogger.Msg(TXT, MyMod.ConvertLoggerColor(Color));
	}

	public static void WelcomeReceived(int _fromClient, Packet _packet)
	{
		if (CoopReturnToMenu.HostIsClosing)
		{
			return;
		}
		int num = _packet.ReadInt();
		string text = _packet.ReadString();
		string text2 = "";
		if (_packet.Length() > 1)
		{
			text2 = _packet.ReadString();
		}
		if (_fromClient != num)
		{
			Log($"Player \"{text}\" (ID: {_fromClient}) has assumed the wrong client ID ({num})!");
		}
		else if (text2 != "RCON")
		{
			if (text2 != "0.15.5")
			{
				ServerSend.KICKMESSAGE(_fromClient, "Wrong mod version! Server using version 0.15.5");
				Shared.SendMessageToChat(new DataStr.MultiplayerChatMessage
				{
					m_Type = 0,
					m_By = text,
					m_Message = text + " can't join because has different mod version!"
				});
				Log("Client " + _fromClient + " has different version! Processing disconnect!");
				Server.clients[_fromClient].udp.Disconnect();
				return;
			}
			Log("[Steam join] Welcome acknowledgement received for client " + _fromClient);
			if (Server.clients[_fromClient].ConnectionInitialized)
			{
				ServerSend.SERVERCFG(_fromClient);
				Shared.SendSlotData(_fromClient);
				return;
			}
			Server.clients[_fromClient].Ready = true;
			string text3 = _packet.ReadString();
			Supporters.SupporterBenefits desired = _packet.ReadSupporterBenefits();
			MyMod.playersData[_fromClient].m_SupporterBenefits = Supporters.VerifyBenefitsWithConfig(text3, desired);
			MyMod.playersData[_fromClient].m_SteamOrEGSID = text3;
			long num2 = _packet.ReadLong();
			if (MyMod.ServerConfig.m_CheckModsValidation)
			{
				ModsValidation.ModValidationData modsHash = ModsValidation.GetModsHash();
				if (modsHash.m_Hash != num2)
				{
					ServerSend.MODSLIST(_fromClient, modsHash.m_FullStringBase64);
					ServerSend.KICKMESSAGE(_fromClient, "Wrong mods installed! You have that different from server's mods, or you missing some mods that installed on the server, check logs!");
					Shared.SendMessageToChat(new DataStr.MultiplayerChatMessage
					{
						m_Type = 0,
						m_By = text,
						m_Message = text + " can't join because has diferent mods installed!"
					});
					Log("Client " + _fromClient + " can't join because has diferent mods installed!");
					Server.clients[_fromClient].udp.Disconnect();
					return;
				}
			}
			Supporters.ApplyFlairsForModel(_fromClient, MyMod.playersData[_fromClient].m_SupporterBenefits.m_Flairs);
			Shared.WebhookPlayerJoin(text);
			ServerSend.BENEFITINIT(_fromClient, MyMod.playersData[_fromClient].m_SupporterBenefits);
			ServerSend.GAMETIME(MyMod.OveridedTime);
			MyMod.playersData[_fromClient].m_Name = text;
			MPStats.AddPlayer(Server.clients[_fromClient].SubNetworkGUID, text);
			Log("Client " + _fromClient + " with user name " + text + " connected!");
			Log("Sending init data to new client...");
			Shared.AddLoadingClient(_fromClient);
			ServerSend.SERVERCFG(_fromClient);
			ServerSend.ROPELIST(_fromClient);
			ServerSend.ALLSHELTERS(_fromClient);
			ServerSend.COUGARSTATE(_fromClient, FarTerritory.CougarSync.Capture());
			string text4 = FarTerritory.TraderSync.Capture();
			if (!string.IsNullOrEmpty(text4))
			{
				ServerSend.TRADERSTATE(_fromClient, text4);
			}
			foreach (ExpeditionManager.SpecialExpeditionItem specialItem in ExpeditionManager.m_SpecialItems)
			{
				ServerSend.REGISTERSPEICALITEM(specialItem, _fromClient);
			}
			if (!MyMod.DedicatedServerAppMode)
			{
				Log("[Init data] Client 0 -> Client " + _fromClient + " Data from host player object");
				int voicePersona = (int)PlayerManager.m_VoicePersona;
				ServerSend.SELECTEDCHARACTER(0, voicePersona, toEveryOne: false, _fromClient);
				ServerSend.XYZ(0, GameManager.GetPlayerTransform().position, toEveryOne: false, _fromClient);
				ServerSend.XYZW(0, GameManager.GetPlayerTransform().rotation, toEveryOne: false, _fromClient);
				ServerSend.LEVELID(0, MyMod.levelid, toEveryOne: false, _fromClient);
				ServerSend.LEVELGUID(0, MyMod.level_guid, toEveryOne: false, _fromClient);
				ServerSend.LIGHTSOURCE(0, MyMod.MyLightSource, toEveryOne: false, _fromClient);
				ServerSend.LIGHTSOURCENAME(0, MyMod.MyLightSourceName, toEveryOne: false, _fromClient);
				ServerSend.BENEFITINIT(0, Supporters.ConfiguratedBenefits, _fromClient);
				DataStr.PlayerEquipmentData playerEquipmentData = new DataStr.PlayerEquipmentData();
				playerEquipmentData.m_HasAxe = MyMod.MyHasAxe;
				playerEquipmentData.m_HasMedkit = MyMod.MyHasMedkit;
				playerEquipmentData.m_HasRevolver = MyMod.MyHasRevolver;
				playerEquipmentData.m_HasRifle = MyMod.MyHasRifle;
				playerEquipmentData.m_Arrows = MyMod.MyArrows;
				ServerSend.EQUIPMENT(0, playerEquipmentData, toEveryOne: false, _fromClient);
				DataStr.PlayerClothingData playerClothingData = new DataStr.PlayerClothingData();
				playerClothingData.m_Hat = MyMod.MyHat;
				playerClothingData.m_Top = MyMod.MyTop;
				playerClothingData.m_Bottom = MyMod.MyBottom;
				ServerSend.CLOTH(0, playerClothingData, toEveryOne: false, _fromClient);
			}
			for (int i = 1; i <= Server.MaxPlayers; i++)
			{
				if (Server.clients[i].IsBusy() && MyMod.playersData[i] != null && i != _fromClient)
				{
					int num3 = i;
					Log("[Init data] Client " + i + " -> Client " + _fromClient + " Data from playersData[" + num3 + "]");
					int num4 = i;
					int onlyFor = _fromClient;
					DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[i];
					ServerSend.XYZ(num4, multiPlayerClientData.m_Position, toEveryOne: false, onlyFor);
					ServerSend.XYZW(num4, multiPlayerClientData.m_Rotation, toEveryOne: false, onlyFor);
					ServerSend.LEVELID(num4, multiPlayerClientData.m_Levelid, toEveryOne: false, onlyFor);
					ServerSend.LEVELGUID(num4, multiPlayerClientData.m_LevelGuid, toEveryOne: true);
					ServerSend.LIGHTSOURCE(num4, multiPlayerClientData.m_PlayerEquipmentData.m_LightSourceOn, toEveryOne: false, onlyFor);
					ServerSend.LIGHTSOURCENAME(num4, multiPlayerClientData.m_PlayerEquipmentData.m_HoldingItem, toEveryOne: false, onlyFor);
					ServerSend.EQUIPMENT(num4, multiPlayerClientData.m_PlayerEquipmentData, toEveryOne: false, onlyFor);
					ServerSend.CLOTH(num4, multiPlayerClientData.m_PlayerClothingData, toEveryOne: false, onlyFor);
					ServerSend.SELECTEDCHARACTER(num4, multiPlayerClientData.m_Character, toEveryOne: false, onlyFor);
					ServerSend.BENEFITINIT(num4, multiPlayerClientData.m_SupporterBenefits, _fromClient);
				}
			}
			Shared.SendSlotData(_fromClient);
			Server.clients[_fromClient].ConnectionInitialized = true;
			MyMod.NoHostResponceSeconds = 0;
			MyMod.NeedTryReconnect = false;
			MyMod.TryingReconnect = false;
			DataStr.MultiplayerChatMessage multiplayerChatMessage = new DataStr.MultiplayerChatMessage();
			multiplayerChatMessage.m_Type = 0;
			multiplayerChatMessage.m_By = text;
			multiplayerChatMessage.m_Message = text + " join the server";
			if (!MyMod.DedicatedServerAppMode)
			{
				Shared.SendMessageToChat(multiplayerChatMessage, needSync: false);
			}
			ServerSend.CHAT(_fromClient, multiplayerChatMessage);
			if (MyMod.CurrentCustomChalleng.m_Started)
			{
				ServerSend.CHALLENGEINIT(MyMod.CurrentChallengeRules.m_ID, MyMod.CurrentCustomChalleng.m_CurrentTask);
			}
		}
		else
		{
			ServerSend.KICKMESSAGE(_fromClient, "Outdated RCON protocol!");
		}
	}

	public static void XYZ(int _fromClient, Packet _packet)
	{
		Vector3 vector = _packet.ReadVector3();
		Vector3 vector2 = new Vector3(vector.x, vector.y + 0.03f, vector.z);
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_Position = vector2;
			if (!MyMod.DedicatedServerAppMode && MyMod.players[_fromClient] != null && MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayer>() != null)
			{
				MyMod.LongActionCancleCauseMoved(MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayer>());
			}
		}
		ServerSend.XYZ(_fromClient, vector2, toEveryOne: false);
	}

	public static void XYZDW(int _fromClient, Packet _packet)
	{
	}

	public static void XYZW(int _fromClient, Packet _packet)
	{
		Quaternion quaternion = _packet.ReadQuaternion();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_Rotation = quaternion;
		}
		ServerSend.XYZW(_fromClient, quaternion, toEveryOne: false);
	}

	public static void BLOCK(int _fromClient, Packet _packet)
	{
		Vector3 vector = _packet.ReadVector3();
		if (MyMod.playersData[_fromClient] != null && MyMod.playersData[_fromClient].m_Levelid == MyMod.levelid)
		{
			GameObject.CreatePrimitive(PrimitiveType.Cube).transform.position = vector;
		}
		ServerSend.BLOCK(_fromClient, vector, toEveryOne: false);
	}

	public static void LEVELID(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_Levelid = num;
			if (MyMod.playersData[_fromClient].m_Levelid != num)
			{
				Log("Player " + _fromClient + "  transition to level " + num);
			}
			if (num == 0)
			{
				Shared.AddLoadingClient(_fromClient);
			}
		}
		ServerSend.LEVELID(_fromClient, num, toEveryOne: false);
	}

	public static void LEVELGUID(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_LevelGuid = text;
			if (MyMod.playersData[_fromClient].m_LevelGuid != text)
			{
				Log("Player " + _fromClient + "  transition to level with GUID " + text);
			}
		}
		ServerSend.LEVELGUID(_fromClient, text, toEveryOne: false);
	}

	public static void GOTITEM(int _fromClient, Packet _packet)
	{
		DataStr.GearItemDataPacket gearItemDataPacket = _packet.ReadGearData();
		if (gearItemDataPacket.m_SendedTo == 0)
		{
			MyMod.GiveRecivedItem(gearItemDataPacket);
		}
		else
		{
			ServerSend.GOTITEM(gearItemDataPacket.m_SendedTo, gearItemDataPacket);
		}
	}

	public static void GOTITEMSLICE(int _fromClient, Packet _packet)
	{
		DataStr.SlicedJsonData slicedJsonData = _packet.ReadSlicedGear();
		if (slicedJsonData.m_SendTo == 0)
		{
			MyMod.AddSlicedJsonData(slicedJsonData);
		}
		else
		{
			ServerSend.GOTITEMSLICE(slicedJsonData.m_SendTo, slicedJsonData);
		}
	}

	public static void GAMETIME(int _fromClient, Packet _packet)
	{
	}

	public static void LIGHTSOURCENAME(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_HoldingItem = text;
		}
		ServerSend.LIGHTSOURCENAME(_fromClient, text, toEveryOne: false);
	}

	public static void LIGHTSOURCE(int _fromClient, Packet _packet)
	{
		bool flag = _packet.ReadBool();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_LightSourceOn = flag;
		}
		ServerSend.LIGHTSOURCE(_fromClient, flag, toEveryOne: false);
	}

	public static void ANIMSTATE(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_AnimState = text;
		}
		ServerSend.ANIMSTATE(_fromClient, text, toEveryOne: false);
	}

	public static void REVIVE(int _fromClient, Packet _packet)
	{
	}

	public static void REVIVEDONE(int _fromClient, Packet _packet)
	{
	}

	public static void SLEEPHOURS(int _fromClient, Packet _packet)
	{
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_SleepHours = _packet.ReadInt();
		}
	}

	public static void DARKWALKERREADY(int _fromClient, Packet _packet)
	{
	}

	public static void WARDISACTIVE(int _fromClient, Packet _packet)
	{
	}

	public static void REQUESTDWREADYSTATE(int _fromClient, Packet _packet)
	{
	}

	public static void DWCOUNTDOWN(int _fromClient, Packet _packet)
	{
	}

	public static void SHOOTSYNC(int _fromClient, Packet _packet)
	{
		DataStr.ShootSync shootSync = _packet.ReadShoot();
		Vector3 position = new Vector3(0f, 0f, 0f);
		if (MyMod.playersData[_fromClient] != null)
		{
			position = MyMod.playersData[_fromClient].m_Position;
		}
		if (shootSync.m_projectilename == "GEAR_FlareGunAmmoSingle")
		{
			ExpeditionManager.RegisterFlaregunShot(shootSync.m_sceneguid, position);
		}
		MyMod.DoShootSync(shootSync, _fromClient);
		ServerSend.SHOOTSYNC(_fromClient, shootSync, toEveryOne: false);
	}

	public static void PIMPSKILL(int _fromClient, Packet _packet)
	{
		switch (_packet.ReadInt())
		{
		case 1:
			GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Rifle, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
			Log("Got remote skill upgrade Rifle");
			break;
		case 2:
			GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Revolver, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
			Log("Got remote skill upgrade Revolver");
			break;
		}
	}

	public static void HARVESTINGANIMAL(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_HarvestingAnimal = text;
		}
		ServerSend.HARVESTINGANIMAL(_fromClient, text, toEveryOne: false);
	}

	public static void DONEHARVASTING(int _fromClient, Packet _packet)
	{
		DataStr.HarvestStats harvestStats = _packet.ReadHarvest();
		Shared.OnAnimalCorpseChanged(harvestStats.m_Guid, harvestStats.m_Meat, harvestStats.m_Guts, harvestStats.m_Hide);
	}

	public static void ANIMALTEST(int _fromClient, Packet _packet)
	{
		DataStr.AnimalCompactData animalCompactData = _packet.ReadAnimalCompactData();
		DataStr.AnimalAnimsSync anim = _packet.ReadAnimalAnim();
		int num = _packet.ReadInt();
		if (num == 0)
		{
			MyMod.DoAnimalSync(animalCompactData, anim);
		}
		else
		{
			ServerSend.ANIMALTEST(_fromClient, animalCompactData, anim, num);
		}
	}

	public static void ANIMALSYNCTRIGG(int _fromClient, Packet _packet)
	{
		DataStr.AnimalTrigger animalTrigger = _packet.ReadAnimalTrigger();
		MyMod.SetAnimalTriggers(animalTrigger);
		ServerSend.ANIMALSYNCTRIGG(_fromClient, animalTrigger, toEveryOne: true);
	}

	public static void BULLETDAMAGE(int _fromClient, Packet _packet)
	{
		if (!MyMod.ServerConfig.m_PVP)
		{
			ServerSend.DOORLOCKEDMSG(_fromClient, "You cannot attack players on this server.");
			return;
		}
		if (MyMod.playersData[_fromClient] != null && MyMod.playersData[_fromClient].m_IsSafe)
		{
			ServerSend.DOORLOCKEDMSG(_fromClient, "You cannot attack when you in the safe zone!");
			return;
		}
		float num = _packet.ReadFloat();
		int bodypart = _packet.ReadInt();
		int num2 = _packet.ReadInt();
		bool flag = _packet.ReadBool();
		string meleeWeapon = "";
		if (flag)
		{
			meleeWeapon = _packet.ReadString();
		}
		if (num2 == 0)
		{
			if ((bool)GameManager.m_PlayerObject && (SafeZoneManager.SceneIsSafe(MyMod.level_guid) || SafeZoneManager.InsideSafeZone(MyMod.level_guid, GameManager.GetPlayerTransform().position)))
			{
				ServerSend.DOORLOCKEDMSG(_fromClient, "You cannot attack players in the safe zone!");
			}
			else
			{
				MyMod.DamageByBullet(num, _fromClient, bodypart, flag, meleeWeapon);
			}
		}
		else if (MyMod.playersData[num2] != null && MyMod.playersData[num2].m_IsSafe)
		{
			ServerSend.DOORLOCKEDMSG(_fromClient, "You cannot attack players in the safe zone!");
		}
		else
		{
			ServerSend.BULLETDAMAGE(num2, num, bodypart, _fromClient, flag, meleeWeapon);
		}
	}

	public static void MULTISOUND(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		MyMod.PlayMultiplayer3dAduio(text, _fromClient);
		ServerSend.MULTISOUND(_fromClient, text, toEveryOne: false);
	}

	public static void CONTAINEROPEN(int _fromClient, Packet _packet)
	{
		DataStr.ContainerOpenSync containerOpenSync = _packet.ReadContainer();
		MyMod.DoSyncContainer(containerOpenSync);
		ServerSend.CONTAINEROPEN(_fromClient, containerOpenSync, toEveryOne: false);
	}

	public static void LUREPLACEMENT(int _fromClient, Packet _packet)
	{
	}

	public static void LUREISACTIVE(int _fromClient, Packet _packet)
	{
	}

	public static void ALIGNANIMAL(int _fromClient, Packet _packet)
	{
	}

	public static void ASKFORANIMALPROXY(int _fromClient, Packet _packet)
	{
	}

	public static void CARRYBODY(int _fromClient, Packet _packet)
	{
	}

	public static void BODYWARP(int _fromClient, Packet _packet)
	{
	}

	public static void ANIMALDELETE(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		MyMod.DeleteAnimal(text);
		ServerSend.ANIMALDELETE(_fromClient, text);
	}

	public static void KEEPITALIVE(int _fromClient, Packet _packet)
	{
		MyMod.NoHostResponceSeconds = 0;
		int timeOutTime = Server.clients[_fromClient].TimeOutTime;
		Server.clients[_fromClient].TimeOutTime = 0;
		if (timeOutTime > 10)
		{
			Log("Last request from client " + _fromClient + " took longer than expected");
			Log("Client[" + _fromClient + "] KeepItAlive took " + timeOutTime + "s");
		}
	}

	public static void SYNCWEATHER(int _fromClient, Packet _packet)
	{
	}

	public static void EQUIPMENT(int _fromClient, Packet _packet)
	{
		DataStr.PlayerEquipmentData playerEquipmentData = _packet.ReadEQ();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_Arrows = playerEquipmentData.m_Arrows;
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_HasAxe = playerEquipmentData.m_HasAxe;
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_HasMedkit = playerEquipmentData.m_HasMedkit;
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_HasRevolver = playerEquipmentData.m_HasRevolver;
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_HasRifle = playerEquipmentData.m_HasRifle;
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_Flares = playerEquipmentData.m_Flares;
			MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_BlueFlares = playerEquipmentData.m_BlueFlares;
		}
		ServerSend.EQUIPMENT(_fromClient, playerEquipmentData, toEveryOne: false);
	}

	public static void CHAT(int _fromClient, Packet _packet)
	{
		DataStr.MultiplayerChatMessage multiplayerChatMessage = _packet.ReadChat();
		if (multiplayerChatMessage.m_Global)
		{
			Shared.SendMessageToChat(multiplayerChatMessage, needSync: false);
		}
		else if (MyMod.playersData[_fromClient].m_LevelGuid == MyMod.level_guid && Vector3.Distance(GameManager.GetPlayerTransform().position, MyMod.playersData[_fromClient].m_Position) <= Shared.LocalChatMaxDistance)
		{
			Shared.SendMessageToChat(multiplayerChatMessage, needSync: false);
		}
		if (MyMod.playersData[_fromClient] != null)
		{
			ServerSend.CHAT(_fromClient, multiplayerChatMessage, MyMod.playersData[_fromClient].m_Position, MyMod.playersData[_fromClient].m_LevelGuid);
		}
	}

	public static void CHANGENAME(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		DataStr.MultiplayerChatMessage message = new DataStr.MultiplayerChatMessage
		{
			m_Type = 0,
			m_By = MyMod.playersData[_fromClient].m_Name,
			m_Message = MyMod.playersData[_fromClient].m_Name + " changed name to " + text
		};
		MyMod.playersData[_fromClient].m_Name = text;
		Shared.SendMessageToChat(message);
		ServerSend.CHANGENAME(_fromClient, text, toEveryOne: false);
	}

	public static void CLOTH(int _fromClient, Packet _packet)
	{
		DataStr.PlayerClothingData playerClothingData = _packet.ReadClothingData();
		MyMod.playersData[_fromClient].m_PlayerClothingData = playerClothingData;
		ServerSend.CLOTH(_fromClient, playerClothingData, toEveryOne: false);
	}

	private static int FindSteamClientSlot(string sid)
	{
		if (string.IsNullOrEmpty(sid))
		{
			return -1;
		}
		for (int i = 1; i < Server.MaxPlayers; i++)
		{
			if (Server.clients.TryGetValue(i, out var value) && value.udp != null && !value.RCON && value.udp.sid == sid)
			{
				return i;
			}
		}
		for (int j = 1; j < Server.MaxPlayers; j++)
		{
			if (Server.clients.TryGetValue(j, out var value2) && value2.udp != null && !value2.RCON && !value2.IsBusy())
			{
				return j;
			}
		}
		return -1;
	}

	public static void CONNECTSTEAM(int _fromClient, Packet _packet)
	{
		if (!CoopReturnToMenu.HostIsClosing)
		{
			string text = _packet.ReadString();
			int num = FindSteamClientSlot(text);
			if (num < 0)
			{
				Log("[SteamWorks.NET] No available player slot for incoming Steam connection.");
				return;
			}
			Client client = Server.clients[num];
			bool flag = client.udp.sid == text;
			client.udp.sid = text;
			client.SubNetworkGUID = text;
			client.TimeOutTime = 0;
			Log("[SteamWorks.NET] " + (flag ? "Reconnecting" : "Connecting") + " Steam peer as client " + num);
			ServerSend.Welcome(num, Server.MaxPlayers);
		}
	}

	public static void ASKSPAWNDATA(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		if (num == MyMod.levelid)
		{
			MyMod.SendSpawnData();
		}
		ServerSend.ASKSPAWNDATA(_fromClient, num, toEveryOne: false);
	}

	public static void FURNBROKEN(int _fromClient, Packet _packet)
	{
		DataStr.BrokenFurnitureSync brokenFurnitureSync = _packet.ReadFurn();
		if (brokenFurnitureSync.m_LevelGUID == MyMod.level_guid)
		{
			MyMod.RemoveBrokenFurniture(brokenFurnitureSync.m_Guid, brokenFurnitureSync.m_ParentGuid);
		}
		MPSaveManager.AddBrokenFurn(brokenFurnitureSync);
		ServerSend.FURNBROKEN(_fromClient, brokenFurnitureSync, toEveryOne: false);
	}

	public static void FURNBREAKINGGUID(int _fromClient, Packet _packet)
	{
		DataStr.BrokenFurnitureSync brokenFurnitureSync = _packet.ReadFurn();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_BrakingObject = brokenFurnitureSync;
			if (MyMod.playersData[_fromClient].m_LevelGuid == MyMod.level_guid)
			{
				MyMod.playersData[_fromClient].m_BrakingSounds = MyMod.GetBreakDownSound(brokenFurnitureSync);
			}
		}
		ServerSend.FURNBREAKINGGUID(_fromClient, brokenFurnitureSync, toEveryOne: false);
	}

	public static void FURNBREAKINSTOP(int _fromClient, Packet _packet)
	{
		_packet.ReadBool();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_BrakingObject = new DataStr.BrokenFurnitureSync();
			MyMod.playersData[_fromClient].m_BrakingSounds = "";
		}
		ServerSend.FURNBREAKINSTOP(_fromClient, _msg: true, toEveryOne: false);
	}

	public static void GEARPICKUP(int _fromClient, Packet _packet)
	{
		DataStr.PickedGearSync pickedGearSync = _packet.ReadPickedGear();
		if (MyMod.playersData[_fromClient] != null && MyMod.playersData[_fromClient].m_LevelGuid == MyMod.level_guid && !MyMod.DedicatedServerAppMode && MyMod.players[_fromClient] != null && MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
		{
			MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>().Pickup();
		}
		Shared.AddPickedGear(pickedGearSync.m_Spawn, pickedGearSync.m_LevelID, pickedGearSync.m_LevelGUID, _fromClient, pickedGearSync.m_MyInstanceID, pickedGearSync.m_GearName, needSync: false);
		ServerSend.GEARPICKUP(_fromClient, pickedGearSync, toEveryOne: false);
	}

	public static void ROPE(int _fromClient, Packet _packet)
	{
		DataStr.ClimbingRopeSync climbingRopeSync = _packet.ReadRope();
		Shared.AddDeployedRopes(climbingRopeSync.m_Position, climbingRopeSync.m_Deployed, climbingRopeSync.m_Snapped, climbingRopeSync.m_LevelID, climbingRopeSync.m_LevelGUID, needSync: false);
		ServerSend.ROPE(_fromClient, climbingRopeSync, toEveryOne: false);
	}

	public static void CONSUME(int _fromClient, Packet _packet)
	{
		bool flag = _packet.ReadBool();
		if (MyMod.playersData[_fromClient] != null && MyMod.playersData[_fromClient].m_Levelid == MyMod.levelid && MyMod.playersData[_fromClient].m_LevelGuid == MyMod.level_guid && !MyMod.DedicatedServerAppMode && MyMod.players[_fromClient] != null && MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
		{
			MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>().m_IsDrink = flag;
			MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>().Consumption();
		}
		ServerSend.CONSUME(_fromClient, flag, toEveryOne: false);
	}

	public static void STOPCONSUME(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		if (MyMod.playersData[_fromClient] != null)
		{
			if (!MyMod.DedicatedServerAppMode && MyMod.playersData[_fromClient].m_Levelid == MyMod.levelid && MyMod.playersData[_fromClient].m_LevelGuid == MyMod.level_guid && MyMod.players[_fromClient] != null && MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
			{
				MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>().StopConsumption();
			}
			MyMod.playersData[_fromClient].m_AnimState = text;
		}
		ServerSend.STOPCONSUME(_fromClient, text, toEveryOne: false);
	}

	public static void HEAVYBREATH(int _fromClient, Packet _packet)
	{
		bool flag = _packet.ReadBool();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_HeavyBreath = flag;
		}
		ServerSend.HEAVYBREATH(_fromClient, flag, toEveryOne: false);
	}

	public static void BLOODLOSTS(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_BloodLosts = num;
		}
		ServerSend.BLOODLOSTS(_fromClient, num, toEveryOne: false);
	}

	public static void APPLYACTIONONPLAYER(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		int num = _packet.ReadInt();
		if (num == 0)
		{
			MyMod.OtherPlayerApplyActionOnMe(text, _fromClient);
		}
		else
		{
			ServerSend.APPLYACTIONONPLAYER(_fromClient, text, toEveryOne: false, num);
		}
	}

	public static void DONTMOVEWARNING(int _fromClient, Packet _packet)
	{
		_packet.ReadBool();
		int num = _packet.ReadInt();
		if (num == 0)
		{
			if (MyMod.playersData[_fromClient] != null)
			{
				HUDMessage.AddMessage("PLEASE DON'T MOVE, " + MyMod.playersData[_fromClient].m_Name + " IS TENDING YOU");
				GameManager.GetVpFPSPlayer().Controller.Stop();
			}
		}
		else
		{
			ServerSend.DONTMOVEWARNING(_fromClient, _msg: true, toEveryOne: false, num);
		}
	}

	public static void INFECTIONSRISK(int _fromClient, Packet _packet)
	{
		bool flag = _packet.ReadBool();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_NeedAntiseptic = flag;
		}
		ServerSend.INFECTIONSRISK(_fromClient, flag, toEveryOne: false);
	}

	public static void CONTAINERINTERACT(int _fromClient, Packet _packet)
	{
		DataStr.ContainerOpenSync containerOpenSync = _packet.ReadContainer();
		if (MyMod.playersData[_fromClient] != null)
		{
			if (containerOpenSync.m_Guid == "NULL")
			{
				MyMod.playersData[_fromClient].m_Container = null;
			}
			else
			{
				MyMod.playersData[_fromClient].m_Container = containerOpenSync;
			}
		}
		ServerSend.CONTAINERINTERACT(_fromClient, containerOpenSync, toEveryOne: false);
	}

	public static void HARVESTPLANT(int _fromClient, Packet _packet)
	{
		DataStr.HarvestableSyncData harvestableSyncData = _packet.ReadHarvestablePlant();
		if (MyMod.playersData[_fromClient] != null)
		{
			if (harvestableSyncData.m_State == "Start")
			{
				MyMod.playersData[_fromClient].m_Plant = harvestableSyncData.m_Guid;
			}
			else
			{
				MyMod.playersData[_fromClient].m_Plant = "";
			}
		}
		if (harvestableSyncData.m_State == "Done")
		{
			if (MyMod.playersData[_fromClient].m_LevelGuid == MyMod.level_guid)
			{
				MyMod.RemoveHarvastedPlant(harvestableSyncData.m_Guid);
			}
			MPSaveManager.AddHarvestedPlant(harvestableSyncData.m_Guid, MyMod.playersData[_fromClient].m_LevelGuid, _fromClient);
			ServerSend.LOOTEDHARVESTABLE(_fromClient, harvestableSyncData.m_Guid, MyMod.playersData[_fromClient].m_LevelGuid, MyMod.MinutesFromStartServer, toEveryOne: false);
		}
		ServerSend.HARVESTPLANT(_fromClient, harvestableSyncData, toEveryOne: false);
	}

	public static void SELECTEDCHARACTER(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_Character = num;
			if (num == 0)
			{
				MyMod.playersData[_fromClient].m_Female = false;
			}
			if (num == 1)
			{
				MyMod.playersData[_fromClient].m_Female = true;
			}
		}
		ServerSend.SELECTEDCHARACTER(_fromClient, num, toEveryOne: false);
	}

	public static void ADDSHELTER(int _fromClient, Packet _packet)
	{
		DataStr.ShowShelterByOther showShelterByOther = _packet.ReadShelter();
		Shared.ShelterCreated(showShelterByOther.m_Position, showShelterByOther.m_Rotation, showShelterByOther.m_LevelID, showShelterByOther.m_LevelGUID, needSync: false);
		ServerSend.ADDSHELTER(_fromClient, showShelterByOther, toEveryOne: false);
	}

	public static void REMOVESHELTER(int _fromClient, Packet _packet)
	{
		DataStr.ShowShelterByOther showShelterByOther = _packet.ReadShelter();
		Shared.ShelterRemoved(showShelterByOther.m_Position, showShelterByOther.m_LevelID, showShelterByOther.m_LevelGUID, needSync: false);
		ServerSend.REMOVESHELTER(_fromClient, showShelterByOther, toEveryOne: false);
	}

	public static void USESHELTER(int _fromClient, Packet _packet)
	{
		DataStr.ShowShelterByOther showShelterByOther = _packet.ReadShelter();
		if (MyMod.playersData[_fromClient] != null)
		{
			if (showShelterByOther.m_Position == new Vector3(0f, 0f, 0f))
			{
				MyMod.playersData[_fromClient].m_Shelter = null;
			}
			else
			{
				MyMod.playersData[_fromClient].m_Shelter = showShelterByOther;
			}
		}
		ServerSend.USESHELTER(_fromClient, showShelterByOther, toEveryOne: false);
	}

	public static void FIRE(int _fromClient, Packet _packet)
	{
		DataStr.FireSourcesSync fireSourcesSync = _packet.ReadFire();
		MyMod.MayAddFireSources(fireSourcesSync);
		ServerSend.FIRE(_fromClient, fireSourcesSync, toEveryOne: false);
	}

	public static void FIREFUEL(int _fromClient, Packet _packet)
	{
		DataStr.FireSourcesSync fireSourcesSync = _packet.ReadFire();
		if (fireSourcesSync.m_LevelId == MyMod.levelid && fireSourcesSync.m_LevelGUID == MyMod.level_guid)
		{
			MyMod.AddOtherFuel(fireSourcesSync, fireSourcesSync.m_FuelName);
		}
		ServerSend.FIREFUEL(_fromClient, fireSourcesSync, toEveryOne: false);
	}

	public static void CUSTOM(int _fromClient, Packet _packet)
	{
		API.CustomEventCallback(_packet, _fromClient);
	}

	public static void VOICECHAT(int _fromClient, Packet _packet)
	{
		int readLength = _packet.ReadInt();
		int length = _packet.ReadInt();
		byte[] array = _packet.ReadBytes(length);
		float recordTime = _packet.ReadFloat();
		int num = _packet.ReadInt();
		if (MyMod.playersData[_fromClient] == null)
		{
			return;
		}
		string levelGuid = MyMod.playersData[_fromClient].m_LevelGuid;
		float num2 = -66f;
		if (_fromClient == num && MyMod.playersData[_fromClient].m_PlayerEquipmentData.m_HoldingItem == "GEAR_HandheldShortwave")
		{
			num2 = MyMod.playersData[_fromClient].m_RadioFrequency;
		}
		if (levelGuid == MyMod.level_guid)
		{
			bool radio = false;
			if (_fromClient != num)
			{
				radio = true;
			}
			MyMod.ProcessVoiceChatData(_fromClient, array, uint.Parse(readLength.ToString()), recordTime, radio);
		}
		if (num2 == MyMod.RadioFrequency && !MyMod.DoingRecord)
		{
			MyMod.ProcessRadioChatData(array, uint.Parse(readLength.ToString()), recordTime);
			ServerSend.VOICECHAT(0, array, readLength, recordTime, MyMod.level_guid, -66f, num);
		}
		ServerSend.VOICECHAT(_fromClient, array, readLength, recordTime, levelGuid, num2, num);
	}

	public static void SLICEDBYTES(int _fromClient, Packet _packet)
	{
	}

	public static void ANIMALDAMAGE(int _fromClient, Packet _packet)
	{
		string guid = _packet.ReadString();
		float damage = _packet.ReadFloat();
		MyMod.DoAnimalDamage(guid, damage);
		ServerSend.ANIMALDAMAGE(_fromClient, guid, damage);
	}

	public static void DROPITEM(int _fromClient, Packet _packet)
	{
		DataStr.DroppedGearItemDataPacket droppedGearItemDataPacket = _packet.ReadDroppedGearData();
		Shared.FakeDropItem(droppedGearItemDataPacket);
		ServerSend.DROPITEM(_fromClient, droppedGearItemDataPacket, toEveryOne: true);
	}

	public static void GOTDROPSLICE(int _fromClient, Packet _packet)
	{
		Shared.AddSlicedJsonDataForDrop(_packet.ReadSlicedGear(), _fromClient);
	}

	public static void GOTPHOTOSLICE(int _fromClient, Packet _packet)
	{
		Shared.AddBase64Slice(_packet.ReadSlicedBase64Data(), _fromClient);
	}

	public static void REQUESTPICKUP(int _fromClient, Packet _packet)
	{
		int hash = _packet.ReadInt();
		string scene = _packet.ReadString();
		Log("Client " + _fromClient + " trying to pickup gear with hash " + hash);
		Shared.ClientTryPickupItem(hash, _fromClient, scene, place: false);
	}

	public static void REQUESTPLACE(int _fromClient, Packet _packet)
	{
		int hash = _packet.ReadInt();
		string scene = _packet.ReadString();
		Log("Client " + _fromClient + " trying to place gear with hash " + hash);
		Shared.ClientTryPickupItem(hash, _fromClient, scene, place: true);
	}

	public static void SendAllOpenables(int _fromClient, string scene)
	{
		Dictionary<string, bool> dictionary = MPSaveManager.LoadOpenableThings(scene);
		if (dictionary == null)
		{
			return;
		}
		foreach (KeyValuePair<string, bool> item in dictionary)
		{
			string key = item.Key;
			bool value = item.Value;
			ServerSend.USEOPENABLE(_fromClient, key, value);
		}
	}

	public static void SendAnimalCorpse(DataStr.AnimalKilled Animal, int forWho = -1)
	{
		if (forWho != -1)
		{
			ServerSend.ANIMALCORPSE(forWho, Animal);
		}
		else
		{
			ServerSend.ANIMALCORPSE(forWho, Animal, toAll: true);
		}
	}

	public static void RequestAnimalCorpses(int _fromClient, string LevelGUID)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, DataStr.AnimalKilled> item in Shared.AnimalsKilled)
		{
			if (item.Value.m_PrefabName != "DeadBody")
			{
				if (item.Value.m_CreatedTime + 14400 < MyMod.MinutesFromStartServer)
				{
					list.Add(item.Key);
				}
				else if (item.Value.m_LevelGUID == LevelGUID)
				{
					SendAnimalCorpse(item.Value, _fromClient);
				}
			}
		}
		foreach (string item2 in list)
		{
			Shared.AnimalsKilled.Remove(item2);
		}
	}

	public static void REQUESTDROPSFORSCENE(int _fromClient, Packet _packet)
	{
		int levelid = _packet.ReadInt();
		string text = _packet.ReadString();
		DataStr.WeatherVolunteerData weatherVolunteerData = _packet.ReadWeatherVolunteerData();
		int lastRegion = _packet.ReadInt();
		Log("Client " + _fromClient + " request all drops for scene " + text);
		Shared.RegisterWeatherSetForRegion(_fromClient, weatherVolunteerData);
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_Levelid = levelid;
			MyMod.playersData[_fromClient].m_LevelGuid = text;
			MyMod.playersData[_fromClient].m_LastWeatherRegion = weatherVolunteerData.CurrentRegion;
			MyMod.playersData[_fromClient].m_LastRegion = lastRegion;
		}
		SendAllOpenables(_fromClient, text);
		RequestAnimalCorpses(_fromClient, text);
		MPSaveManager.TrySpawnFestives(text);
		if (MyMod.CurrentCustomChalleng.m_Started && MyMod.CurrentChallengeRules.m_Name == "Lost in action")
		{
			ServerSend.CAIRNS(_fromClient);
		}
		foreach (DataStr.DeathContainerData deathCreate in MyMod.DeathCreates)
		{
			if (deathCreate.m_LevelKey == text)
			{
				ServerSend.ADDDEATHCONTAINER(deathCreate, _fromClient);
			}
		}
		foreach (KeyValuePair<string, string> item in MPSaveManager.GetDoorsOnScene(text))
		{
			string doorGUID = item.Key.Split('_')[1];
			ServerSend.ADDDOORLOCK(_fromClient, doorGUID, text);
		}
		Dictionary<string, DataStr.BrokenFurnitureSync> dictionary = MPSaveManager.LoadFurnsData(text);
		if (dictionary != null)
		{
			foreach (KeyValuePair<string, DataStr.BrokenFurnitureSync> item2 in dictionary)
			{
				ServerSend.FURNBROKEN(0, item2.Value, toEveryOne: false, _fromClient);
			}
		}
		Dictionary<long, DataStr.PickedGearSync> dictionary2 = MPSaveManager.LoadPickedGearsData(text);
		if (dictionary2 != null)
		{
			foreach (KeyValuePair<long, DataStr.PickedGearSync> item3 in dictionary2)
			{
				ServerSend.GEARPICKUP(-1, item3.Value, toEveryOne: false, _fromClient);
			}
		}
		Dictionary<string, int> dictionary3 = MPSaveManager.LoadLootedContainersData(text);
		if (dictionary3 != null)
		{
			foreach (KeyValuePair<string, int> item4 in dictionary3)
			{
				new DataStr.ContainerOpenSync
				{
					m_Guid = item4.Key,
					m_LevelGUID = text
				};
				int state = item4.Value;
				if (MPSaveManager.ContainerNotEmpty(text, item4.Key))
				{
					state = 1;
				}
				ServerSend.CHANGECONTAINERSTATE(0, item4.Key, state, text, toEveryOne: false, _fromClient);
			}
		}
		Dictionary<string, int> dictionary4 = MPSaveManager.LoadHarvestedPlants(text);
		if (dictionary4 != null)
		{
			foreach (KeyValuePair<string, int> item5 in dictionary4)
			{
				int value = item5.Value;
				ServerSend.LOOTEDHARVESTABLE(0, item5.Key, text, value, toEveryOne: false, _fromClient);
			}
		}
		foreach (KeyValuePair<string, DataStr.FakeRockCacheVisualData> rockCache in MPSaveManager.GetRockCaches(text))
		{
			ServerSend.ADDROCKCACH(0, rockCache.Value, rockCache.Value.m_LevelGUID);
		}
		foreach (KeyValuePair<string, DataStr.UniversalSyncableObject> item6 in MPSaveManager.GetUniversalSyncablesForScene(text))
		{
			ServerSend.ADDUNIVERSALSYNCABLE(item6.Value, _fromClient);
		}
		Shared.ModifyDynamicGears(text);
		Dictionary<int, DataStr.DroppedGearItemDataPacket> dictionary5 = MPSaveManager.LoadDropVisual(text);
		if (MPSaveManager.LoadDropData(text) == null || dictionary5 == null)
		{
			Log("Requested scene has no drops " + text);
			ServerSend.LOADINGSCENEDROPSDONE(_fromClient, _msg: true);
		}
		else
		{
			int num = 0;
			foreach (KeyValuePair<int, DataStr.DroppedGearItemDataPacket> item7 in dictionary5)
			{
				num++;
				ServerSend.DROPITEM(0, item7.Value, toEveryOne: false, _fromClient);
			}
			Log("Sending done, " + num + " packets has been sent");
			ServerSend.LOADINGSCENEDROPSDONE(_fromClient, _msg: true);
		}
		ExpeditionManager.MaySpawnSpeicalExpeditionItem(_fromClient, text);
		Shared.RemoveLoadingClient(_fromClient);
		if (MyMod.playersData[_fromClient].m_FirstBoot)
		{
			ExpeditionManager.MayNotifyAboutCrashSite(_fromClient);
			MyMod.playersData[_fromClient].m_FirstBoot = false;
		}
	}

	public static void GOTCONTAINERSLICE(int _fromClient, Packet _packet)
	{
	}

	public static void REQUESTOPENCONTAINER(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		string text2 = _packet.ReadString();
		Log("Client " + _fromClient + " request container data for " + text2);
		if (!ContainerLootSync.Reserve(_fromClient, text, text2))
		{
			ServerSend.OPENEMPTYCONTAINER(_fromClient, _msg: false, text, text2);
			return;
		}
		string initial = ((_packet.UnreadLength() > 0) ? _packet.ReadString() : "");
		string orInitialize = ContainerLootSync.GetOrInitialize(text, text2, initial);
		if (orInitialize == "")
		{
			Log("Send to client this is empty");
			ServerSend.OPENEMPTYCONTAINER(_fromClient, _msg: true, text, text2);
		}
		else
		{
			Log("Send to client data about container");
			Shared.SendContainerData(orInitialize, text, text2, "", _fromClient);
		}
	}

	public static void CHANGEAIM(int _fromClient, Packet _packet)
	{
		bool flag = _packet.ReadBool();
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_Aiming = flag;
		}
		ServerSend.CHANGEAIM(_fromClient, flag, toEveryOne: false);
	}

	public static void USEOPENABLE(int _fromClient, Packet _packet)
	{
		string scene = _packet.ReadString();
		string gUID = _packet.ReadString();
		bool state = _packet.ReadBool();
		Shared.ChangeOpenableThingState(scene, gUID, state);
	}

	public static void TRYDIAGNISISPLAYER(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		if (num == 0)
		{
			Condition conditionComponent = GameManager.GetConditionComponent();
			Hunger hungerComponent = GameManager.GetHungerComponent();
			Thirst thirstComponent = GameManager.GetThirstComponent();
			MyMod.SendMyAffictions(_fromClient, conditionComponent.m_CurrentHP, conditionComponent.m_MaxHP, thirstComponent.m_CurrentThirst, hungerComponent.m_CurrentReserveCalories, hungerComponent.m_MaxReserveCalories);
		}
		else
		{
			ServerSend.TRYDIAGNISISPLAYER(num, _fromClient);
		}
	}

	public static void CUREAFFLICTION(int _fromClient, Packet _packet)
	{
		DataStr.AffictionSync aff = _packet.ReadAffiction();
		int num = _packet.ReadInt();
		bool medkit = _packet.ReadBool();
		int num2 = _packet.ReadInt();
		if (num2 == 0)
		{
			MyMod.OtherPlayerCuredMyAffiction(_fromClient, aff, num, medkit);
		}
		else
		{
			ServerSend.CUREAFFLICTION(num2, _fromClient, aff, num, medkit);
		}
	}

	public static void ANIMALKILLED(int _fromClient, Packet _packet)
	{
		DataStr.AnimalKilled animalKilled = _packet.ReadAnimalCorpse();
		Shared.OnAnimalKilled(animalKilled.m_PrefabName, animalKilled.m_Position, animalKilled.m_Rotation, animalKilled.m_GUID, animalKilled.m_LevelGUID, animalKilled.m_RegionGUID, animalKilled.m_Knocked);
		ServerSend.ANIMALCORPSE(0, animalKilled, toAll: true);
	}

	public static void PICKUPRABBIT(int _fromClient, Packet _packet)
	{
		int result = Shared.PickUpRabbit(_packet.ReadString());
		ServerSend.GOTRABBIT(_fromClient, result);
	}

	public static void HITRABBIT(int _fromClient, Packet _packet)
	{
		string gUID = _packet.ReadString();
		int num = _packet.ReadInt();
		if (num != 0)
		{
			ServerSend.HITRABBIT(num, gUID);
		}
		else
		{
			MyMod.OnHitRabbit(gUID);
		}
	}

	public static void RELEASERABBIT(int _fromClient, Packet _packet)
	{
		string lEVELGUID = _packet.ReadString();
		MyMod.OnReleaseRabbit(_fromClient);
		ServerSend.RELEASERABBIT(_fromClient, lEVELGUID);
	}

	public static void SENDMYAFFLCTIONS(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		int num2 = _packet.ReadInt();
		float num3 = _packet.ReadFloat();
		float num4 = _packet.ReadFloat();
		float thirst = _packet.ReadFloat();
		float hunger = _packet.ReadFloat();
		float hungermax = _packet.ReadFloat();
		List<DataStr.AffictionSync> list = new List<DataStr.AffictionSync>();
		for (int i = 0; i < num2; i++)
		{
			DataStr.AffictionSync item = _packet.ReadAffiction();
			list.Add(item);
		}
		Log("Client " + _fromClient + " sent " + num2 + " afflictions, for " + num);
		if (num == 0)
		{
			MyMod.CheckOtherPlayer(list, _fromClient, num3, num4, thirst, hunger, hungermax);
		}
		else
		{
			ServerSend.SENDMYAFFLCTIONS(num, list, num3, num4, thirst, hunger, hungermax, _fromClient);
		}
	}

	public static void REQUESTANIMALCORPSE(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		bool flag = _packet.ReadBool();
		Log("Client " + _fromClient + " requested animal corpse " + text);
		if (Shared.AnimalsKilled.TryGetValue(text, out var value))
		{
			float meat = value.m_Meat;
			int guts = value.m_Guts;
			int hide = value.m_Hide;
			Log("Sending responce");
			ServerSend.REQUESTANIMALCORPSE(_fromClient, meat, guts, hide);
		}
		else if (flag)
		{
			DataStr.BodyHarvestUnits bodyHarvestUnits = Shared.GetBodyHarvestUnits("WILDLIFE_Stag");
			value = new DataStr.AnimalKilled();
			value.m_GUID = text;
			value.m_Guts = bodyHarvestUnits.m_Guts;
			value.m_Hide = 1;
			value.m_Meat = bodyHarvestUnits.m_Meat;
			value.m_PrefabName = "DeadBody";
			Shared.AnimalsKilled.Add(text, value);
			MPSaveManager.AnimalsKilledChanged = true;
			ServerSend.REQUESTANIMALCORPSE(_fromClient, bodyHarvestUnits.m_Meat, bodyHarvestUnits.m_Guts, 1);
		}
		else
		{
			ServerSend.REQUESTANIMALCORPSE(_fromClient, -1f, 0, 0);
		}
	}

	public static void QUARTERANIMAL(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		Log("QUARTERANIMAL " + text);
		Shared.OnAnimalQuarted(text);
		MyMod.SpawnQuartedMess(text);
		ServerSend.QUARTERANIMAL(_fromClient, text);
	}

	public static void ANIMALAUDIO(int _fromClient, Packet _packet)
	{
		bool num = _packet.ReadBool();
		string gUID = _packet.ReadString();
		if (num)
		{
			int soundID = _packet.ReadInt();
			string lEVELGUID = "";
			if (MyMod.playersData[_fromClient] != null)
			{
				lEVELGUID = MyMod.playersData[_fromClient].m_LevelGuid;
				if (MyMod.level_guid == MyMod.playersData[_fromClient].m_LevelGuid)
				{
					Pathes.Play3dAudioOnAnimal(gUID, soundID);
				}
			}
			ServerSend.ANIMALAUDIO(_fromClient, soundID, gUID, lEVELGUID);
			return;
		}
		string soundID2 = _packet.ReadString();
		string lEVELGUID2 = "";
		if (MyMod.playersData[_fromClient] != null)
		{
			lEVELGUID2 = MyMod.playersData[_fromClient].m_LevelGuid;
			if (MyMod.level_guid == MyMod.playersData[_fromClient].m_LevelGuid)
			{
				Pathes.Play3dAudioOnAnimal(gUID, soundID2);
			}
		}
		ServerSend.ANIMALAUDIO(_fromClient, soundID2, gUID, lEVELGUID2);
	}

	public static void CHANGEDFREQUENCY(int _fromClient, Packet _packet)
	{
		double num = Math.Round((double)_packet.ReadFloat() * 10.0) * 0.10000000149011612;
		if (MyMod.playersData[_fromClient] != null)
		{
			MyMod.playersData[_fromClient].m_RadioFrequency = (float)num;
		}
	}

	public static void MELEESTART(int _fromClient, Packet _packet)
	{
		if (MyMod.players[_fromClient] != null && MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
		{
			MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>().MeleeAttack();
		}
		ServerSend.MELEESTART(_fromClient);
	}

	public static void TRYBORROWGEAR(int _fromClient, Packet _packet)
	{
	}

	public static void CHALLENGETRIGGER(int _fromClient, Packet _packet)
	{
		string tRIGGER = _packet.ReadString();
		MyMod.ProcessCustomChallengeTrigger(tRIGGER);
		ServerSend.CHALLENGETRIGGER(tRIGGER);
	}

	public static void ADDDEATHCONTAINER(int _fromClient, Packet _packet)
	{
		DataStr.DeathContainerData deathContainerData = _packet.ReadDeathContainer();
		if (!MyMod.DeathCreates.Contains(deathContainerData))
		{
			MyMod.DeathCreates.Add(deathContainerData);
		}
		if (deathContainerData.m_LevelKey == MyMod.level_guid)
		{
			MyMod.MakeDeathCreate(deathContainerData);
		}
		MPStats.AddDeath(Server.GetMACByID(_fromClient));
		ServerSend.ADDDEATHCONTAINER(deathContainerData, deathContainerData.m_LevelKey, _fromClient);
	}

	public static void DEATHCREATEEMPTYNOW(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		string scene = _packet.ReadString();
		bool num = _packet.ReadBool();
		if (num)
		{
			MyMod.RemoveDeathContainer(text, scene);
		}
		if (num)
		{
			ServerSend.DEATHCREATEEMPTYNOW(text, scene, _fromClient);
		}
		MPSaveManager.SetConstainerState(scene, text, 0, DontNotify: true);
		MPSaveManager.RemoveContainer(scene, text);
	}

	public static void SPAWNREGIONBANCHECK(int _fromClient, Packet _packet)
	{
		string gUID = _packet.ReadString();
		bool result = Shared.CheckSpawnRegionBanned(gUID);
		ServerSend.SPAWNREGIONBANCHECK(gUID, result, _fromClient);
	}

	public static void ADDDOORLOCK(int _fromClient, Packet _packet)
	{
		string doorKey = _packet.ReadString();
		string keySeed = _packet.ReadString();
		string scene = _packet.ReadString();
		Shared.ClientTryingLockDoor(doorKey, keySeed, scene, _fromClient);
	}

	public static void LOCKPICK(int _fromClient, Packet _packet)
	{
		string doorKey = _packet.ReadString();
		MPSaveManager.TryLockPick(_packet.ReadString(), doorKey, _fromClient);
	}

	public static void TRYOPENDOOR(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		string text2 = _packet.ReadString();
		string scene = _packet.ReadString();
		if (_packet.ReadBool() && !MPSaveManager.TryUseLeadKey())
		{
			ServerSend.DOORLOCKEDMSG(_fromClient, "The key broke!");
			ServerSend.REMOVEKEYBYSEED(_fromClient, text2);
		}
		else if (MPSaveManager.TryUseKey(scene, text, text2))
		{
			ServerSend.ENTERDOOR(_fromClient, text.Split('_')[1]);
		}
		else
		{
			ServerSend.DOORLOCKEDMSG(_fromClient, "Incorrect key!");
		}
	}

	public static void VERIFYSAVE(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		long num = _packet.ReadLong();
		Log("[VERIFYSAVE] Client " + _fromClient + " UGUID " + text + " Hash " + num);
		if (MyMod.ServerConfig.m_SaveScamProtection)
		{
			if (text == "" && num == 0L)
			{
				ServerSend.VERIFYSAVE(_fromClient, MPSaveManager.GetNewUGUID(), Valid: false);
			}
			else if (MPSaveManager.VerifySaveHash(text, num))
			{
				ServerSend.VERIFYSAVE(_fromClient, text, Valid: true);
			}
			else
			{
				ServerSend.VERIFYSAVE(_fromClient, MPSaveManager.GetNewUGUID(), Valid: false);
			}
		}
		else
		{
			ServerSend.VERIFYSAVE(_fromClient, text, Valid: true);
		}
	}

	public static void SAVEHASH(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		long num = _packet.ReadLong();
		bool flag = _packet.ReadBool();
		if (MyMod.ServerConfig.m_SaveScamProtection)
		{
			Log("[SAVEHASH] " + _fromClient + " UGUID " + text + " Hash " + num);
			if (text != "" && num != 0L)
			{
				MPSaveManager.SetSaveHash(text, num);
			}
		}
		if (flag && Server.clients[_fromClient] != null)
		{
			Server.clients[_fromClient].TimeOutTime = MyMod.TimeOutSecondsForLoaders + 1;
		}
	}

	public static void RCONCOMMAND(int _fromClient, Packet _packet)
	{
	}

	public static void FORCELOADING(int _fromClient, Packet _packet)
	{
		Shared.AddLoadingClient(_fromClient);
	}

	public static void REQUESTLOCKSMITH(int _fromClient, Packet _packet)
	{
		int hash = _packet.ReadInt();
		if (MPSaveManager.CanWorkOnBlank(hash))
		{
			ServerSend.REQUESTLOCKSMITH(_fromClient, 0);
			MPSaveManager.ChangeBlankState(hash, -1);
		}
		else
		{
			ServerSend.REQUESTLOCKSMITH(_fromClient, -1);
		}
	}

	public static void APPLYTOOLONBLANK(int _fromClient, Packet _packet)
	{
		int hash = _packet.ReadInt();
		int tool = _packet.ReadInt();
		if (!_packet.ReadBool())
		{
			MPSaveManager.ApplyToolOnBlank(hash, tool);
			return;
		}
		string keyName = _packet.ReadString();
		string keySeed = _packet.ReadString();
		MPSaveManager.ApplyToolOnBlank(hash, tool, keyName, keySeed);
	}

	public static void LETENTER(int _fromClient, Packet _packet)
	{
		int clientID = _packet.ReadInt();
		string toScene = _packet.ReadString();
		MPSaveManager.ApplyEnterFromKnock(clientID, toScene);
	}

	public static void KNOCKKNOCK(int _fromClient, Packet _packet)
	{
		string toScene = _packet.ReadString();
		MPSaveManager.AddKnockDoorRequest(_fromClient, toScene);
	}

	public static void PEEPHOLE(int _fromClient, Packet _packet)
	{
		List<int> knocksOnScene = MPSaveManager.GetKnocksOnScene(_packet.ReadString());
		ServerSend.PEEPHOLE(_fromClient, knocksOnScene);
	}

	public static void RESTART(int _fromClient, Packet _packet)
	{
		Log("Incomming reconnect");
	}

	public static void WEATHERVOLUNTEER(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		foreach (Shared.RegionWeatherControler regionWeather in Shared.RegionWeathers)
		{
			if (regionWeather.m_Region == num)
			{
				if (regionWeather.m_WaitsForUpdate && regionWeather.m_SearchingVolunteer)
				{
					regionWeather.m_SearchingVolunteer = false;
					Log("Client " + _fromClient + " want to be weather voluneer for " + num);
					ServerSend.REREGISTERWEATHER(_fromClient, num);
				}
				break;
			}
		}
	}

	public static void REREGISTERWEATHER(int _fromClient, Packet _packet)
	{
		DataStr.WeatherVolunteerData weatherVolunteerData = _packet.ReadWeatherVolunteerData();
		Log("Client " + _fromClient + " sent back weather volunteer data, going to reregister weather for Region " + weatherVolunteerData.CurrentRegion);
		Shared.RegisterWeatherSetForRegion(_fromClient, weatherVolunteerData);
	}

	public static void CHANGECONTAINERSTATE(int _fromClient, Packet _packet)
	{
		string gUID = _packet.ReadString();
		string text = _packet.ReadString();
		int state = _packet.ReadInt();
		MPSaveManager.SetConstainerState(text, gUID, state, DontNotify: true);
		if (text == MyMod.level_guid)
		{
			MyMod.RemoveLootFromContainer(gUID, state);
		}
		ServerSend.CHANGECONTAINERSTATE(_fromClient, gUID, state, text, toEveryOne: false);
	}

	public static void TRIGGEREMOTE(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		if (MyMod.playersData[_fromClient] != null && MyMod.players[_fromClient] != null && MyMod.playersData[_fromClient].m_LevelGuid == MyMod.level_guid)
		{
			Comps.MultiplayerPlayerAnimator component = MyMod.players[_fromClient].GetComponent<Comps.MultiplayerPlayerAnimator>();
			if (component != null)
			{
				DataStr.MultiplayerEmote emoteByID = MyMod.GetEmoteByID(num);
				if (emoteByID.m_LeftHandEmote)
				{
					component.DoLeftHandEmote(emoteByID.m_Animation);
				}
			}
		}
		ServerSend.TRIGGEREMOTE(_fromClient, num);
	}

	public static void PHOTOREQUEST(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		string text2 = MPSaveManager.LoadPhoto(text);
		Log("Client requested photo " + text);
		if (!string.IsNullOrEmpty(text2))
		{
			Shared.SendSlicedBase64Data(Shared.GetBase64Sliced(text2, text, DataStr.SlicedBase64Purpose.Photo), _fromClient);
		}
		else
		{
			Log("Don't have that photo");
		}
	}

	public static void STARTEXPEDITION(int _fromClient, Packet _packet)
	{
		int region = _packet.ReadInt();
		if (MyMod.playersData[_fromClient] != null)
		{
			ExpeditionManager.StartNewExpedition(Server.GetMACByID(_fromClient), region);
		}
	}

	public static void ACCEPTEXPEDITIONINVITE(int _fromClient, Packet _packet)
	{
		ExpeditionManager.ExpeditionInvite expeditionInvite = _packet.ReadExpeditionInvite();
		ExpeditionManager.AcceptInvite(expeditionInvite.m_PersonToInviteMAC, expeditionInvite.m_InviterMAC);
	}

	public static void REQUESTEXPEDITIONINVITES(int _fromClient, Packet _packet)
	{
		ServerSend.REQUESTEXPEDITIONINVITES(_fromClient, ExpeditionManager.GetInviteForClient(Server.GetMACByID(_fromClient)));
	}

	public static void CREATEEXPEDITIONINVITE(int _fromClient, Packet _packet)
	{
		int num = _packet.ReadInt();
		if (num != -1)
		{
			ExpeditionManager.CreateInviteToExpedition(Server.GetMACByID(_fromClient), Server.GetMACByID(num));
		}
	}

	public static void COUGARSTATE(int _fromClient, Packet _packet)
	{
		DataStr.CougarStateSync cougarStateSync = _packet.ReadCougarState();
		FarTerritory.CougarSync.Apply(cougarStateSync);
		ServerSend.COUGARSTATE(cougarStateSync);
	}

	public static void TRADERSTATE(int _fromClient, Packet _packet)
	{
		DataStr.TraderStateSync traderStateSync = _packet.ReadTraderState();
		if (!string.IsNullOrEmpty(traderStateSync.m_SerializedState))
		{
			FarTerritory.TraderSync.Apply(traderStateSync.m_SerializedState);
			ServerSend.TRADERSTATE(traderStateSync.m_SerializedState);
		}
	}

	public static void TRAVOISSYNC(int _fromClient, Packet _packet)
	{
		DataStr.TravoisSync data = _packet.ReadTravoisSync();
		FarTerritory.TravoisSyncing.Receive(data);
		ServerSend.TRAVOISSYNC(_fromClient, data);
	}

	public static void NOISEMAKERIGNITE(int _fromClient, Packet _packet)
	{
		string text = _packet.ReadString();
		string text2 = _packet.ReadString();
		int num = _packet.ReadInt();
		FarTerritory.NoiseMakerSync.Receive(text, text2, num);
		ServerSend.NOISEMAKERIGNITE(_fromClient, text, text2, num);
	}

	public static void ADDROCKCACH(int _fromClient, Packet _packet)
	{
		DataStr.FakeRockCacheVisualData fakeRockCacheVisualData = _packet.ReadFakeRockCache();
		MyMod.AddRockCache(fakeRockCacheVisualData);
		MPSaveManager.AddRockCach(fakeRockCacheVisualData, _fromClient);
	}

	public static void REMOVEROCKCACH(int _fromClient, Packet _packet)
	{
		DataStr.FakeRockCacheVisualData fakeRockCacheVisualData = _packet.ReadFakeRockCache();
		if (MPSaveManager.ContainerNotEmpty(fakeRockCacheVisualData.m_LevelGUID, fakeRockCacheVisualData.m_GUID))
		{
			ServerSend.ADDHUDMESSAGE(_fromClient, "Rock cache should be empty!");
			ServerSend.REMOVEROCKCACH(_fromClient, fakeRockCacheVisualData, -1);
			return;
		}
		if ((MyMod.MyContainer != null && MyMod.MyContainer.m_Guid == fakeRockCacheVisualData.m_GUID) || (Pathes.FakeRockCacheCallback != null && Pathes.FakeRockCacheCallback.m_GUID == fakeRockCacheVisualData.m_GUID))
		{
			ServerSend.ADDHUDMESSAGE(_fromClient, MyMod.MyChatName + " INTERACTING WITH THIS!");
			ServerSend.REMOVEROCKCACH(_fromClient, fakeRockCacheVisualData, -1);
		}
		for (int i = 0; i < MyMod.playersData.Count; i++)
		{
			if (MyMod.playersData[i] != null && i != _fromClient && ((MyMod.playersData[i].m_BrakingObject != null && MyMod.playersData[i].m_BrakingObject.m_Guid == fakeRockCacheVisualData.m_GUID) || (MyMod.playersData[i].m_Container != null && MyMod.playersData[i].m_Container.m_Guid == fakeRockCacheVisualData.m_GUID)))
			{
				ServerSend.ADDHUDMESSAGE(_fromClient, MyMod.playersData[i].m_Name + " INTERACTING WITH THIS!");
				ServerSend.REMOVEROCKCACH(_fromClient, fakeRockCacheVisualData, -1);
				return;
			}
		}
		ServerSend.REMOVEROCKCACH(_fromClient, fakeRockCacheVisualData, 0);
	}

	public static void REMOVEROCKCACHFINISHED(int _fromClient, Packet _packet)
	{
		DataStr.FakeRockCacheVisualData fakeRockCacheVisualData = _packet.ReadFakeRockCache();
		ServerSend.REMOVEROCKCACH(_fromClient, fakeRockCacheVisualData, 1, fakeRockCacheVisualData.m_LevelGUID);
		MyMod.RemoveRockCache(fakeRockCacheVisualData);
		MPSaveManager.RemoveRockCach(fakeRockCacheVisualData, _fromClient);
	}

	public static void CHARCOALDRAW(int _fromClient, Packet _packet)
	{
		string scene = _packet.ReadString();
		Vector3 position = _packet.ReadVector3();
		ExpeditionManager.RegisterCharcoalDrawing(scene, position);
	}

	public static void CHATCOMMAND(int _fromClient, Packet _packet)
	{
		Shared.ChatCommand(_packet.ReadString(), _fromClient);
	}

	public static void REQUESTCONTAINERSTATE(int _fromClient, Packet _packet)
	{
		string scene = _packet.ReadString();
		string gUID = _packet.ReadString();
		int containerState = MPSaveManager.GetContainerState(scene, gUID);
		ServerSend.CHANGECONTAINERSTATE(0, gUID, containerState, scene, toEveryOne: false, _fromClient);
	}

	public static void INTERACTIONDONE(int _fromClient, Packet _packet)
	{
		string gUID = _packet.ReadString();
		ExpeditionManager.RegisterInteractionDone(gUID);
		MyMod.RemoveObjectByGUID(gUID);
	}

	public static void REMOVEOBJECTGROUP(int _fromClient, Packet _packet)
	{
		ExpeditionManager.RemoveObjectGroup(_packet.ReadString());
	}

	public static void REQUESTSPECIALEXPEDITION(int _fromClient, Packet _packet)
	{
		string alias = _packet.ReadString();
		if (ExpeditionManager.StartNewExpedition(Server.GetMACByID(_fromClient), 0, alias, NoMessage: false, Special: true))
		{
			ServerSend.REQUESTSPECIALEXPEDITION(_fromClient);
		}
	}

	public static void REQUESTEXPEDITIONSPROGRESS(int _fromClient, Packet _packet = null)
	{
		MPStats.ExpeditionsProgressData expeditionsProgress = MPStats.GetExpeditionsProgress(Server.GetMACByID(_fromClient));
		ServerSend.REQUESTEXPEDITIONSPROGRESS(_fromClient, expeditionsProgress);
	}

	public static void REQUESTSPECIALITEMS(int _fromClient, Packet _packet)
	{
		ServerSend.REQUESTSPECIALITEMS(ExpeditionManager.GetAllSpeicalItemsOfPlayer(Server.GetMACByID(_fromClient)), _fromClient);
	}

	public static void PICKUPSPECAILITEM(int _fromClient, Packet _packet)
	{
		string item = _packet.ReadString();
		ExpeditionManager.GivePlayerSpeicalItem(_fromClient, item);
	}

	public static void ANIMALDESTORYGEAR(int _fromClient, Packet _packet)
	{
		int hash = _packet.ReadInt();
		string scene = _packet.ReadString();
		Log("Animal trying to destory gear " + hash);
		Shared.AnimalDestoryGear(hash, scene);
	}

	public static void ANIMALBITECORPSE(int _fromClient, Packet _packet)
	{
		string gUID = _packet.ReadString();
		string feeding = _packet.ReadString();
		Shared.OnAnimalCorpseChanged(gUID, 0.016f, 0, 0, feeding);
	}
}
