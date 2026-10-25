using System.Collections.Generic;
using System.Net;
using Il2Cpp;
using SkyCoop;
using UnityEngine;

namespace GameServer;

internal class ServerSend
{
	public static void SendTCPData(int _toClient, Packet _packet, bool IgnoreReady = false)
	{
		SendUDPData(_toClient, _packet, IgnoreReady);
	}

	public static void SendUDPData(int _toClient, Packet _packet, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		if (!Server.clients[_toClient].RCON)
		{
			Server.clients[_toClient].udp.SendData(_packet, IgnoreReady);
		}
	}

	public static void SendUDPDataRCON(int Operator, Packet _packet)
	{
		_packet.WriteLength();
		Server.clients[Operator].udp.SendData(_packet);
	}

	public static void SendUDPData(IPEndPoint _clientEndPoint, Packet _packet)
	{
		_packet.WriteLength();
		Server.SendUDPData(_clientEndPoint, _packet);
	}

	public static void SendUDPDataToAll(Packet _packet, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i <= Server.MaxPlayers; i++)
		{
			if (Server.clients[i].IsBusy() && !Server.clients[i].RCON)
			{
				Server.clients[i].udp.SendData(_packet, IgnoreReady);
			}
		}
	}

	public static void SendUDPDataToAll(Packet _packet, string level_guid, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i < MyMod.playersData.Count; i++)
		{
			if (MyMod.playersData[i] != null && MyMod.playersData[i].m_LevelGuid == level_guid && !Server.clients[i].RCON)
			{
				Server.clients[i].udp.SendData(_packet, IgnoreReady);
			}
		}
	}

	public static void SendUDPDataToAll(Packet _packet, int Region, bool IgnoreLoaders = false, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i < MyMod.playersData.Count; i++)
		{
			if (MyMod.playersData[i] != null && MyMod.playersData[i].m_LastWeatherRegion == Region && !Server.clients[i].RCON)
			{
				if (!IgnoreLoaders)
				{
					Server.clients[i].udp.SendData(_packet);
				}
				else if (!MyMod.playersData[i].m_IsLoading)
				{
					Server.clients[i].udp.SendData(_packet, IgnoreReady);
				}
			}
		}
	}

	public static void SendUDPDataToAllInArea(Packet _packet, int SenderId, Vector3 SpeakerPossition, string SpeakerScene, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i < MyMod.playersData.Count; i++)
		{
			if (i != SenderId && MyMod.playersData[i] != null && !Server.clients[i].RCON && MyMod.playersData[i].m_LevelGuid == SpeakerScene && Vector3.Distance(MyMod.playersData[i].m_Position, SpeakerPossition) <= Shared.LocalChatMaxDistance)
			{
				Server.clients[i].udp.SendData(_packet, IgnoreReady);
			}
		}
	}

	public static void SendUDPDataToAllButNotSender(Packet _packet, int SenderId, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i <= Server.MaxPlayers; i++)
		{
			if (i != SenderId && Server.clients[i].IsBusy() && !Server.clients[i].RCON)
			{
				Server.clients[i].udp.SendData(_packet, IgnoreReady);
			}
		}
	}

	public static void SendUDPDataToAllButNotSender(Packet _packet, int SenderId, string level_guid, bool IsKey = false, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i < MyMod.playersData.Count; i++)
		{
			if (i == SenderId || MyMod.playersData[i] == null || Server.clients[i].RCON)
			{
				continue;
			}
			if (!IsKey)
			{
				if (MyMod.playersData[i].m_LevelGuid == level_guid)
				{
					Server.clients[i].udp.SendData(_packet, IgnoreReady);
				}
			}
			else if (MyMod.playersData[i].m_Levelid + MyMod.playersData[i].m_LevelGuid == level_guid)
			{
				Server.clients[i].udp.SendData(_packet, IgnoreReady);
			}
		}
	}

	public static void SendUDPDataToAllButNotSender(Packet _packet, int SenderId, string level_guid, float RadioF, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i < MyMod.playersData.Count; i++)
		{
			if (i != SenderId && MyMod.playersData[i] != null && !Server.clients[i].RCON && (MyMod.playersData[i].m_LevelGuid == level_guid || MyMod.playersData[i].m_RadioFrequency == RadioF))
			{
				Server.clients[i].udp.SendData(_packet, IgnoreReady);
			}
		}
	}

	public static void SendUDPDataToAll(int _exceptClient, Packet _packet, bool IgnoreReady = false)
	{
		_packet.WriteLength();
		for (int i = 1; i <= Server.MaxPlayers; i++)
		{
			if (i != _exceptClient)
			{
				Server.clients[i].udp.SendData(_packet, IgnoreReady);
			}
		}
	}

	public static void Welcome(int _toClient, int maxPlayers)
	{
		using Packet packet = new Packet(1);
		packet.Write(_toClient);
		packet.Write(maxPlayers);
		packet.Write(ModsValidation.GetModsHash().m_WhiteList);
		SendTCPData(_toClient, packet, IgnoreReady: true);
	}

	public static void XYZ(int _From, Vector3 _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(2);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void XYZDW(int _toClient, Vector3 _msg)
	{
	}

	public static void XYZW(int _From, Quaternion _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(3);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void BLOCK(int _From, Vector3 _msg, bool toEveryOne)
	{
		using Packet packet = new Packet(4);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
	}

	public static void LEVELID(int _From, int _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(6);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void LEVELGUID(int _From, string _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(48);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void GOTITEM(int _toClient, DataStr.GearItemDataPacket _msg)
	{
		using Packet packet = new Packet(7);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void GOTITEMSLICE(int _toClient, DataStr.SlicedJsonData _msg)
	{
		using Packet packet = new Packet(80);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void OPENEMPTYCONTAINER(int _toClient, bool _msg, string scene, string guid)
	{
		using Packet packet = new Packet(95);
		packet.Write(_msg);
		packet.Write(_toClient);
		packet.Write(scene);
		packet.Write(guid);
		SendTCPData(_toClient, packet);
	}

	public static void GETREQUESTEDITEMSLICE(int _toClient, DataStr.SlicedJsonData _msg)
	{
		using Packet packet = new Packet(89);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void GETREQUESTEDFORPLACESLICE(int _toClient, DataStr.SlicedJsonData _msg)
	{
		using Packet packet = new Packet(92);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void MARKSEARCHEDCONTAINERS(int _toClient, string _msg)
	{
		using Packet packet = new Packet(96);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void GAMETIME(string _msg)
	{
		using Packet packet = new Packet(8);
		packet.Write(_msg);
		packet.Write(MyMod.MinutesFromStartServer);
		packet.Write(0);
		SendUDPDataToAll(packet);
	}

	public static void LIGHTSOURCENAME(int _From, string _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(10);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void LIGHTSOURCE(int _From, bool _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(9);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void ANIMSTATE(int _from, string _msg, bool toEveryOne)
	{
		using Packet packet = new Packet(11);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_from);
			SendUDPDataToAllButNotSender(packet, _from);
		}
	}

	public static void SYNCWEATHER(DataStr.WeatherProxies _msg)
	{
		using Packet packet = new Packet(13);
		packet.Write(_msg);
		SendUDPDataToAll(packet);
	}

	public static void REVIVE(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(14);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void REVIVEDONE(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(15);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void ANIMALROLE(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(16);
		packet.Write(_msg);
		packet.Write(_toClient);
		packet.Write(MyMod.playersData[_toClient]?.m_LevelGuid ?? "");
		SendTCPData(_toClient, packet);
	}

	public static void ANIMALTEST(int _From, DataStr.AnimalCompactData _msg, DataStr.AnimalAnimsSync anim, int OnlyFor)
	{
		using Packet packet = new Packet(105);
		packet.Write(_msg);
		packet.Write(anim);
		packet.Write(_From);
		SendUDPData(OnlyFor, packet);
	}

	public static void DARKWALKERREADY(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(18);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void HOSTISDARKWALKER(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(19);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void WARDISACTIVE(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(20);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void DWCOUNTDOWN(int _toClient, float _msg)
	{
		using Packet packet = new Packet(22);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void ANIMALSYNCTRIGG(int _From, DataStr.AnimalTrigger _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(23);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void SHOOTSYNC(int _From, DataStr.ShootSync _msg, bool toEveryOne)
	{
		using Packet packet = new Packet(24);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
	}

	public static void PIMPSKILL(int _toClient, int _msg)
	{
		using Packet packet = new Packet(25);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void HARVESTINGANIMAL(int _From, string _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(26);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void SAVEDATA(int _toClient, DataStr.SaveSlotSync _msg)
	{
		using Packet packet = new Packet(28);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void BULLETDAMAGE(int _toClient, float _msg, int bodypart, int _From, bool Melee = false, string MeleeWeapon = "")
	{
		if (_From == 0)
		{
			if ((bool)GameManager.m_PlayerObject && (SafeZoneManager.SceneIsSafe(MyMod.level_guid) || SafeZoneManager.InsideSafeZone(MyMod.level_guid, GameManager.GetPlayerTransform().position)))
			{
				HUDMessage.AddMessage("You cannot attack when you in the safe zone!");
				return;
			}
			if (MyMod.playersData[_toClient] != null && MyMod.playersData[_toClient].m_IsSafe)
			{
				HUDMessage.AddMessage("You cannot attack players in the safe zone!");
				return;
			}
		}
		using Packet packet = new Packet(29);
		packet.Write(_msg);
		packet.Write(bodypart);
		packet.Write(_From);
		packet.Write(Melee);
		if (Melee && MeleeWeapon != "")
		{
			packet.Write(MeleeWeapon);
		}
		SendTCPData(_toClient, packet);
	}

	public static void MULTISOUND(int _From, string _msg, bool toEveryOne)
	{
		using Packet packet = new Packet(30);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
	}

	public static void CONTAINEROPEN(int _From, DataStr.ContainerOpenSync _msg, bool toEveryOne)
	{
		using Packet packet = new Packet(31);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
	}

	public static void LUREPLACEMENT(int _toClient, bool _msg)
	{
	}

	public static void LUREISACTIVE(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(33);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void ALIGNANIMAL(int _toClient, DataStr.AnimalAligner _msg)
	{
	}

	public static void CARRYBODY(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(36);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void BODYWARP(int _toClient, string _msg)
	{
		using Packet packet = new Packet(37);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void ANIMALDELETE(int from, string _msg)
	{
		using Packet packet = new Packet(38);
		packet.Write(_msg);
		packet.Write(from);
		SendUDPDataToAllButNotSender(packet, from);
	}

	public static void KEEPITALIVE(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(39);
		packet.Write(_msg);
		packet.Write(0);
		SendUDPDataToAll(packet);
	}

	public static void RQRECONNECT(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(40);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void EQUIPMENT(int _From, DataStr.PlayerEquipmentData _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(41);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void CHAT(int _From, DataStr.MultiplayerChatMessage _msg, Vector3 SpeakerPossition = default(Vector3), string SpeakerScene = "")
	{
		using Packet packet = new Packet(42);
		packet.Write(_msg);
		packet.Write(_From);
		if (_msg.m_Global)
		{
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			SendUDPDataToAllInArea(packet, _From, SpeakerPossition, SpeakerScene);
		}
	}

	public static void CHATPM(int OnlyFor, DataStr.MultiplayerChatMessage _msg)
	{
		using Packet packet = new Packet(42);
		packet.Write(_msg);
		packet.Write(0);
		SendUDPData(OnlyFor, packet);
	}

	public static void CHANGENAME(int _From, string _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(45);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void CLOTH(int _From, DataStr.PlayerClothingData _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(46);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void ASKSPAWNDATA(int _From, int _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(47);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void FURNBROKEN(int _From, DataStr.BrokenFurnitureSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(49);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void FURNBREAKINGGUID(int _From, DataStr.BrokenFurnitureSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(51);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void FURNBREAKINSTOP(int _From, bool _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(52);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void GEARPICKUP(int _From, DataStr.PickedGearSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(53);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void ROPE(int _From, DataStr.ClimbingRopeSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(55);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void ROPELIST(int OnlyFor)
	{
		using Packet packet = new Packet(56);
		int count = MyMod.DeployedRopes.Count;
		if (count != 0)
		{
			packet.Write(count);
			for (int i = 0; i < count; i++)
			{
				packet.Write(MyMod.DeployedRopes[i]);
			}
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void CONSUME(int _From, bool _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(57);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void SERVERCFG(int OnlyFor)
	{
		using Packet packet = new Packet(58);
		packet.Write(MyMod.ServerConfig);
		SendUDPData(OnlyFor, packet);
	}

	public static void SERVERCFGUPDATED()
	{
		using Packet packet = new Packet(58);
		packet.Write(MyMod.ServerConfig);
		SendUDPDataToAll(packet);
	}

	public static void STOPCONSUME(int _From, string _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(59);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void HEAVYBREATH(int _From, bool _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(60);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void BLOODLOSTS(int _From, int _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(61);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void APPLYACTIONONPLAYER(int _From, string _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(62);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void DONTMOVEWARNING(int _From, bool _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(63);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void INFECTIONSRISK(int _From, bool _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(63);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void CANCLEPICKUP(int _From, DataStr.PickedGearSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(65);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void CONTAINERINTERACT(int _From, DataStr.ContainerOpenSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(66);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void LOOTEDCONTAINER(int _From, DataStr.ContainerOpenSync _msg, int State, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(67);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			packet.Write(State);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			packet.Write(State);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			packet.Write(State);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void HARVESTPLANT(int _From, DataStr.HarvestableSyncData _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(69);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void LOOTEDHARVESTABLE(int _From, string GUID, string Scene, int HarvestTime, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(70);
		packet.Write(GUID);
		packet.Write(Scene);
		packet.Write(HarvestTime);
		if (toEveryOne)
		{
			SendUDPDataToAll(packet, Scene);
		}
		else if (OnlyFor == -1)
		{
			SendUDPDataToAllButNotSender(packet, _From, Scene);
		}
		else
		{
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void SELECTEDCHARACTER(int _From, int _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(72);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void ADDSHELTER(int _From, DataStr.ShowShelterByOther _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(73);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void REMOVESHELTER(int _From, DataStr.ShowShelterByOther _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(74);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void ALLSHELTERS(int OnlyFor)
	{
		using Packet packet = new Packet(75);
		int count = MyMod.ShowSheltersBuilded.Count;
		if (count != 0)
		{
			packet.Write(count);
			for (int i = 0; i < count; i++)
			{
				packet.Write(MyMod.ShowSheltersBuilded[i]);
			}
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void USESHELTER(int _From, DataStr.ShowShelterByOther _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(76);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void FIRE(int _From, DataStr.FireSourcesSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(77);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void FIREFUEL(int _From, DataStr.FireSourcesSync _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(84);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void DROPITEM(int _From, DataStr.DroppedGearItemDataPacket _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(85);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void PICKDROPPEDGEAR(int _From, int _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(87);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void CUSTOM(int _From, byte _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(78);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void KICKMESSAGE(int _toClient, string _msg)
	{
		using Packet packet = new Packet(79);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void KICKMESSAGE(string _msg)
	{
		using Packet packet = new Packet(79);
		packet.Write(_msg);
		SendUDPDataToAll(packet);
	}

	public static void KICKMESSAGE(IPEndPoint EndPoint, string _msg)
	{
		using Packet packet = new Packet(79);
		packet.Write(_msg);
		SendUDPData(EndPoint, packet);
	}

	public static void MODSLIST(int _toClient, string _msg)
	{
		using Packet packet = new Packet(127);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void VOICECHAT(int _From, byte[] _msg, int ReadLength, float RecordTime, string LevelGUID, float RadioF, int Sender)
	{
		using Packet packet = new Packet(81);
		packet.Write(ReadLength);
		packet.Write(_msg.Length);
		packet.Write(_msg);
		packet.Write(RecordTime);
		packet.Write(RadioF);
		packet.Write(_From);
		packet.Write(Sender);
		SendUDPDataToAllButNotSender(packet, _From, LevelGUID, RadioF);
	}

	public static void ANIMALDAMAGE(int _From, string guid, float damage)
	{
		using Packet packet = new Packet(83);
		packet.Write(guid);
		packet.Write(damage);
		SendUDPDataToAllButNotSender(packet, _From);
	}

	public static void READYSENDNEXTSLICEGEAR(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(149);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void READYSENDNEXTSLICEPHOTO(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(162);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void LOADINGSCENEDROPSDONE(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(99);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void USEOPENABLE(int _toClient, string _GUID, bool state, bool toAll = false)
	{
		using Packet packet = new Packet(101);
		packet.Write(_GUID);
		packet.Write(state);
		packet.Write(_toClient);
		if (!toAll)
		{
			SendTCPData(_toClient, packet);
		}
		else
		{
			SendUDPDataToAll(packet);
		}
	}

	public static void GEARNOTEXIST(int _toClient, bool _msg)
	{
		using Packet packet = new Packet(100);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void TRYDIAGNISISPLAYER(int _toClient, int _msg)
	{
		using Packet packet = new Packet(102);
		packet.Write(_msg);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void TRYBORROWGEAR(int _toClient, int _fromClient, string GearName)
	{
		using Packet packet = new Packet(118);
		packet.Write(GearName);
		packet.Write(_fromClient);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void CUREAFFLICTION(int _toClient, int FromWho, DataStr.AffictionSync Aff, int FirstAidSkill, bool Medkit)
	{
		using Packet packet = new Packet(104);
		packet.Write(Aff);
		packet.Write(FirstAidSkill);
		packet.Write(Medkit);
		packet.Write(FromWho);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void SENDMYAFFLCTIONS(int _toClient, List<DataStr.AffictionSync> _msg, float hp, float hpmax, float thirst, float hunger, float hungermax, int fromWho)
	{
		using Packet packet = new Packet(103);
		packet.Write(fromWho);
		packet.Write(_msg.Count);
		packet.Write(hp);
		packet.Write(hpmax);
		packet.Write(thirst);
		packet.Write(hunger);
		packet.Write(hungermax);
		for (int i = 0; i < _msg.Count; i++)
		{
			packet.Write(_msg[i]);
		}
		SendTCPData(_toClient, packet);
	}

	public static void CHANGEAIM(int _From, bool _msg, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(98);
		if (toEveryOne)
		{
			packet.Write(_msg);
			packet.Write(0);
			SendUDPDataToAll(packet);
		}
		else if (OnlyFor == -1)
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPDataToAllButNotSender(packet, _From);
		}
		else
		{
			packet.Write(_msg);
			packet.Write(_From);
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void ANIMALCORPSE(int _toClient, DataStr.AnimalKilled data, bool toAll = false)
	{
		using Packet packet = new Packet(107);
		packet.Write(data);
		packet.Write(_toClient);
		if (!toAll)
		{
			SendTCPData(_toClient, packet);
		}
		else
		{
			SendUDPDataToAll(packet);
		}
	}

	public static void REQUESTANIMALCORPSE(int _toClient, float Meat, int Guts, int Hide)
	{
		using Packet packet = new Packet(108);
		packet.Write(Meat);
		packet.Write(Guts);
		packet.Write(Hide);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void GOTRABBIT(int _toClient, int result)
	{
		using Packet packet = new Packet(112);
		packet.Write(result);
		packet.Write(_toClient);
		SendTCPData(_toClient, packet);
	}

	public static void QUARTERANIMAL(int _toClient, string data, bool toAll = false)
	{
		using Packet packet = new Packet(109);
		packet.Write(data);
		packet.Write(_toClient);
		if (!toAll)
		{
			SendTCPData(_toClient, packet);
		}
		else
		{
			SendUDPDataToAll(packet);
		}
	}

	public static void ANIMALAUDIO(int _from, string soundID, string GUID, string LEVELGUID)
	{
		using Packet packet = new Packet(110);
		packet.Write(_value: false);
		packet.Write(GUID);
		packet.Write(soundID);
		packet.Write(_from);
		SendUDPDataToAllButNotSender(packet, _from, LEVELGUID);
	}

	public static void ANIMALAUDIO(int _from, int soundID, string GUID, string LEVELGUID)
	{
		using Packet packet = new Packet(110);
		packet.Write(_value: true);
		packet.Write(GUID);
		packet.Write(soundID);
		packet.Write(_from);
		SendUDPDataToAllButNotSender(packet, _from, LEVELGUID);
	}

	public static void RELEASERABBIT(int _from, string LEVELGUID)
	{
		using Packet packet = new Packet(113);
		packet.Write(_from);
		SendUDPDataToAllButNotSender(packet, _from, LEVELGUID);
	}

	public static void HITRABBIT(int For, string GUID)
	{
		using Packet packet = new Packet(114);
		packet.Write(GUID);
		SendUDPData(For, packet);
	}

	public static void RABBITREVIVED(int _from, Vector3 v3, string LEVELGUID)
	{
		using Packet packet = new Packet(115);
		packet.Write(v3);
		SendUDPDataToAllButNotSender(packet, _from, LEVELGUID);
	}

	public static void MELEESTART(int _from)
	{
		using Packet packet = new Packet(117);
		packet.Write(_from);
		SendUDPDataToAllButNotSender(packet, _from);
	}

	public static void CHALLENGEINIT(int ID, int StartFrom)
	{
		using Packet packet = new Packet(119);
		packet.Write(ID);
		packet.Write(StartFrom);
		SendUDPDataToAll(packet);
	}

	public static void CHALLENGEUPDATE(DataStr.CustomChallengeData DAT)
	{
		using Packet packet = new Packet(120);
		packet.Write(DAT);
		SendUDPDataToAll(packet);
	}

	public static void CHALLENGETRIGGER(string TRIGGER)
	{
		using Packet packet = new Packet(121);
		packet.Write(TRIGGER);
		SendUDPDataToAll(packet);
	}

	public static void ADDDEATHCONTAINER(DataStr.DeathContainerData data, string LevelKey, int Sender)
	{
		using Packet packet = new Packet(122);
		packet.Write(data);
		SendUDPDataToAllButNotSender(packet, Sender, LevelKey);
	}

	public static void ADDDEATHCONTAINER(DataStr.DeathContainerData data, int JustFor)
	{
		using Packet packet = new Packet(122);
		packet.Write(data);
		SendUDPData(JustFor, packet);
	}

	public static void DEATHCREATEEMPTYNOW(string data, string Scene, int NotFor)
	{
		using Packet packet = new Packet(123);
		packet.Write(data);
		packet.Write(Scene);
		SendUDPDataToAllButNotSender(packet, NotFor);
	}

	public static void SPAWNREGIONBANCHECK(string GUID, bool Result, int For)
	{
		using Packet packet = new Packet(124);
		packet.Write(GUID);
		packet.Write(Result);
		SendUDPData(For, packet);
	}

	public static void SPAWNREGIONBANCHECK(string GUID)
	{
		using Packet packet = new Packet(124);
		packet.Write(GUID);
		packet.Write(_value: true);
		SendUDPDataToAll(packet);
	}

	public static void CAIRNS(int For = -1)
	{
		using Packet packet = new Packet(125);
		packet.Write(MyMod.FoundCairns.Count);
		foreach (KeyValuePair<int, bool> foundCairn in MyMod.FoundCairns)
		{
			packet.Write(foundCairn.Key);
		}
		if (For == -1)
		{
			SendUDPDataToAll(packet);
		}
		else
		{
			SendUDPData(For, packet);
		}
	}

	public static void BENEFITINIT(int From, Supporters.SupporterBenefits benefits, int For = -1)
	{
		using Packet packet = new Packet(126);
		packet.Write(From);
		packet.Write(benefits);
		if (For == -1)
		{
			SendUDPDataToAll(packet);
		}
		else
		{
			SendUDPData(For, packet);
		}
	}

	public static void RCONCONNECTED(int OperatorID)
	{
		using Packet packet = new Packet(128);
		packet.Write(_value: true);
		SendUDPDataRCON(OperatorID, packet);
	}

	public static void RCONCALLBACK(int OperatorID, string CALLBACK)
	{
		using Packet packet = new Packet(130);
		packet.Write(CALLBACK);
		SendUDPDataRCON(OperatorID, packet);
	}

	public static void ADDDOORLOCK(int SendTo, string DoorGUID, string Scene)
	{
		using Packet packet = new Packet(131);
		packet.Write(DoorGUID);
		if (SendTo == -1)
		{
			SendUDPDataToAll(packet, Scene);
		}
		else
		{
			SendUDPData(SendTo, packet);
		}
	}

	public static void DOORLOCKEDMSG(int SendTo, string Message)
	{
		using Packet packet = new Packet(132);
		packet.Write(Message);
		SendUDPData(SendTo, packet);
	}

	public static void ENTERDOOR(int SendTo, string DoorGUID)
	{
		using Packet packet = new Packet(134);
		packet.Write(DoorGUID);
		SendUDPData(SendTo, packet);
	}

	public static void REMOVEDOORLOCK(int SendTo, string DoorGUID, string Scene)
	{
		using Packet packet = new Packet(136);
		packet.Write(DoorGUID);
		if (SendTo == -1)
		{
			SendUDPDataToAll(packet, Scene);
		}
		else
		{
			SendUDPData(SendTo, packet);
		}
	}

	public static void LOCKPICK(int SendTo, bool Swear)
	{
		using Packet packet = new Packet(135);
		packet.Write(Swear);
		SendUDPData(SendTo, packet);
	}

	public static void VERIFYSAVE(int SendTo, string UGUID, bool Valid)
	{
		using Packet packet = new Packet(137);
		packet.Write(UGUID);
		packet.Write(Valid);
		SendUDPData(SendTo, packet);
	}

	public static void RPC(int SendTo, string RPCDATA)
	{
		using Packet packet = new Packet(140);
		packet.Write(RPCDATA);
		if (SendTo != -1)
		{
			SendUDPData(SendTo, packet);
		}
		else
		{
			SendUDPDataToAll(SendTo, packet);
		}
	}

	public static void REQUESTLOCKSMITH(int SendTo, int State)
	{
		using Packet packet = new Packet(141);
		packet.Write(State);
		SendUDPData(SendTo, packet);
	}

	public static void KNOCKKNOCK(string Scene)
	{
		using Packet packet = new Packet(143);
		SendUDPDataToAll(packet, Scene);
	}

	public static void KNOCKENTER(int SendTo, string Scene)
	{
		using Packet packet = new Packet(144);
		packet.Write(Scene);
		SendUDPData(SendTo, packet);
	}

	public static void PEEPHOLE(int SendTo, List<int> Knockers)
	{
		using Packet packet = new Packet(146);
		packet.Write(Knockers);
		SendUDPData(SendTo, packet);
	}

	public static string GetDifficultyString()
	{
		return ExperienceModeManager.GetCurrentExperienceModeType() switch
		{
			ExperienceModeType.Pilgrim => "Pilgrim", 
			ExperienceModeType.Voyageur => "Voyageur", 
			ExperienceModeType.Stalker => "Stalker", 
			ExperienceModeType.Interloper => "Interloper", 
			_ => "Unknown", 
		};
	}

	public static string GetStatusJson(string Players)
	{
		return string.Concat(string.Concat(string.Concat("{" + "'players':[" + Players + "],", "'experience':'", GetDifficultyString(), "',"), "'pvp':'", MyMod.ServerConfig.m_PVP.ToString(), "'"), "}");
	}

	public static void RCONSENDSTRING(IPEndPoint endPoint, string STR)
	{
		using Packet packet = new Packet(130);
		packet.WriteUnicodeString(STR);
		Server.SendUDPData(endPoint, packet);
	}

	public static void PINGSERVER(IPEndPoint endPoint)
	{
		using Packet packet = new Packet(147);
		string text = "";
		int num = 0;
		int maxPlayers = Server.MaxPlayers;
		foreach (KeyValuePair<int, Client> client in Server.clients)
		{
			if (client.Value.IsBusy())
			{
				if (!client.Value.RCON)
				{
					text = ((!string.IsNullOrEmpty(text)) ? (text + ",'" + MyMod.playersData[client.Key].m_Name.Replace("'", "") + "'") : ("'" + MyMod.playersData[client.Key].m_Name.Replace("'", "") + "'"));
				}
				num++;
			}
		}
		packet.WriteUnicodeString(MyMod.CustomServerName);
		packet.Write("0.14.5");
		packet.Write(num);
		packet.Write(maxPlayers);
		packet.WriteUnicodeString(GetStatusJson(text));
		Server.SendUDPData(endPoint, packet);
	}

	public static void RESTART()
	{
		using Packet packet = new Packet(148);
		SendUDPDataToAll(packet);
	}

	public static void DEDICATEDWEATHER(int Region, int Type, int Indx, float StartAtFrac, int Seed, float Duration, List<float> Durations, List<float> Transitions, int TOD, float High, float Low, int PreviousStage)
	{
		using Packet packet = new Packet(150);
		packet.Write(Type);
		packet.Write(Indx);
		packet.Write(StartAtFrac);
		packet.Write(Seed);
		packet.Write(Duration);
		packet.Write(Durations);
		packet.Write(Transitions);
		packet.Write(TOD);
		packet.Write(High);
		packet.Write(Low);
		packet.Write(PreviousStage);
		SendUDPDataToAll(packet, Region, IgnoreLoaders: true);
	}

	public static void WEATHERVOLUNTEER(int Region)
	{
		using Packet packet = new Packet(151);
		packet.Write(Region);
		SendUDPDataToAll(packet, Region, IgnoreLoaders: true);
	}

	public static void REREGISTERWEATHER(int For, int Region)
	{
		using Packet packet = new Packet(152);
		packet.Write(Region);
		SendUDPData(For, packet);
	}

	public static void REMOVEKEYBYSEED(int SendTo, string Seed)
	{
		using Packet packet = new Packet(153);
		packet.Write(Seed);
		SendUDPData(SendTo, packet);
	}

	public static void ADDHUDMESSAGE(int SendTo, string Message)
	{
		switch (SendTo)
		{
		case -1:
			return;
		case 0:
			HUDMessage.AddMessage(Message);
			return;
		}
		using Packet packet = new Packet(154);
		packet.Write(Message);
		SendUDPData(SendTo, packet);
	}

	public static void CHANGECONTAINERSTATE(int _From, string GUID, int State, string Scene, bool toEveryOne, int OnlyFor = -1)
	{
		using Packet packet = new Packet(155);
		packet.Write(GUID);
		packet.Write(State);
		if (toEveryOne)
		{
			SendUDPDataToAll(packet, Scene);
		}
		else if (OnlyFor == -1)
		{
			SendUDPDataToAllButNotSender(packet, _From, Scene);
		}
		else
		{
			SendUDPData(OnlyFor, packet);
		}
	}

	public static void FINISHEDSENDINGCONTAINER(int For, bool Result, string scene = "", string guid = "", string checksum = "")
	{
		using Packet packet = new Packet(156);
		packet.Write(Result);
		packet.Write(scene);
		packet.Write(guid);
		packet.Write(checksum);
		SendUDPData(For, packet);
	}

	public static void TRIGGEREMOTE(int From, int EmoteID)
	{
		using Packet packet = new Packet(157);
		packet.Write(From);
		packet.Write(EmoteID);
		SendUDPDataToAllButNotSender(packet, From);
	}

	public static void EXPEDITIONSYNC(int For, string Name, string Task, int Time, string Alias)
	{
		using Packet packet = new Packet(158);
		packet.WriteUnicodeString(Name);
		packet.WriteUnicodeString(Task);
		packet.Write(Time);
		packet.Write(Alias);
		SendUDPData(For, packet);
	}

	public static void EXPEDITIONRESULT(int For, int State)
	{
		if (For == 0)
		{
			MyMod.DoExpeditionState(State);
			return;
		}
		using Packet packet = new Packet(159);
		packet.Write(State);
		SendUDPData(For, packet);
	}

	public static void REQUESTEXPEDITIONINVITES(int For, List<ExpeditionManager.ExpeditionInvite> Invites)
	{
		using Packet packet = new Packet(165);
		packet.Write(Invites);
		SendUDPData(For, packet);
	}

	public static void NEWPLAYEREXPEDITION(int For, string PlayerName)
	{
		using Packet packet = new Packet(167);
		packet.Write(PlayerName);
		SendUDPData(For, packet);
	}

	public static void NEWEXPEDITIONINVITE(int For, string PlayerName)
	{
		using Packet packet = new Packet(168);
		packet.Write(PlayerName);
		SendUDPData(For, packet);
	}

	public static void BASE64SLICE(int For, DataStr.SlicedBase64Data Data)
	{
		using Packet packet = new Packet(169);
		packet.Write(Data);
		SendUDPData(For, packet);
	}

	public static void BASE64SLICE(DataStr.SlicedBase64Data Data, string LevelGUID)
	{
		using Packet packet = new Packet(169);
		packet.Write(Data);
		SendUDPDataToAll(packet, LevelGUID);
	}

	public static void COUGARSTATE(DataStr.CougarStateSync Data)
	{
		using Packet packet = new Packet(189);
		packet.Write(Data);
		SendUDPDataToAll(packet);
	}

	public static void COUGARSTATE(int _toClient, DataStr.CougarStateSync Data)
	{
		using Packet packet = new Packet(189);
		packet.Write(Data);
		SendUDPData(_toClient, packet);
	}

	public static void TRADERSTATE(string SerializedState)
	{
		using Packet packet = new Packet(190);
		DataStr.TraderStateSync traderStateSync = new DataStr.TraderStateSync();
		traderStateSync.m_SerializedState = SerializedState;
		packet.Write(traderStateSync);
		SendUDPDataToAll(packet);
	}

	public static void TRADERSTATE(int _toClient, string SerializedState)
	{
		using Packet packet = new Packet(190);
		DataStr.TraderStateSync traderStateSync = new DataStr.TraderStateSync();
		traderStateSync.m_SerializedState = SerializedState;
		packet.Write(traderStateSync);
		SendUDPData(_toClient, packet);
	}

	public static void TRAVOISSYNC(int From, DataStr.TravoisSync Data)
	{
		using Packet packet = new Packet(191);
		packet.Write(Data);
		SendUDPDataToAllButNotSender(packet, From, Data.m_LevelGUID);
	}

	public static void NOISEMAKERIGNITE(int From, string GUID, string LevelGUID, int LevelID)
	{
		using Packet packet = new Packet(192);
		packet.Write(GUID);
		packet.Write(LevelGUID);
		packet.Write(LevelID);
		SendUDPDataToAllButNotSender(packet, From, LevelGUID);
	}

	public static void ADDROCKCACH(int From, DataStr.FakeRockCacheVisualData Data, string LevelGUID)
	{
		using Packet packet = new Packet(170);
		packet.Write(Data);
		SendUDPDataToAllButNotSender(packet, From, LevelGUID);
	}

	public static void REMOVEROCKCACH(int From, DataStr.FakeRockCacheVisualData Data, int State, string LevelGUID)
	{
		using Packet packet = new Packet(171);
		packet.Write(Data);
		packet.Write(State);
		SendUDPDataToAllButNotSender(packet, From, LevelGUID);
	}

	public static void REMOVEROCKCACH(int From, DataStr.FakeRockCacheVisualData Data, int State)
	{
		using Packet packet = new Packet(171);
		packet.Write(Data);
		packet.Write(State);
		SendUDPData(From, packet);
	}

	public static void ADDUNIVERSALSYNCABLE(DataStr.UniversalSyncableObject Data, int For)
	{
		using Packet packet = new Packet(175);
		packet.Write(Data);
		SendUDPData(For, packet);
	}

	public static void ADDUNIVERSALSYNCABLE(DataStr.UniversalSyncableObject Data)
	{
		using Packet packet = new Packet(175);
		packet.Write(Data);
		SendUDPDataToAll(packet, Data.m_Scene);
	}

	public static void REMOVEUNIVERSALSYNCABLE(string GUID, string Scene)
	{
		using Packet packet = new Packet(176);
		packet.Write(GUID);
		SendUDPDataToAll(packet, Scene);
	}

	public static void CUSTOMSOUNDEVENT(Vector3 Position, string SOUND, string Prefab, string Scene)
	{
		using Packet packet = new Packet(178);
		packet.Write(Position);
		packet.Write(SOUND);
		packet.Write(Prefab);
		SendUDPDataToAll(packet, Scene);
	}

	public static void CUSTOMSOUNDEVENT(Vector3 Position, string SOUND, string Prefab, int For)
	{
		using Packet packet = new Packet(178);
		packet.Write(Position);
		packet.Write(SOUND);
		packet.Write(Prefab);
		SendUDPData(For, packet);
	}

	public static void REQUESTSPECIALEXPEDITION(int ForClient)
	{
		using Packet packet = new Packet(180);
		packet.Write(_value: true);
		SendUDPData(ForClient, packet);
	}

	public static void REQUESTSPHOTOAGAIN(int ForClient, string GUID)
	{
		using Packet packet = new Packet(181);
		packet.Write(GUID);
		SendUDPData(ForClient, packet);
	}

	public static void REQUESTEXPEDITIONSPROGRESS(int ForClient, MPStats.ExpeditionsProgressData Data)
	{
		List<int> list = new List<int>();
		List<int> list2 = new List<int>();
		foreach (KeyValuePair<int, int> item in Data.ExpeditionsProgress)
		{
			list.Add(item.Key);
			list2.Add(item.Value);
		}
		using Packet packet = new Packet(182);
		packet.Write(list);
		packet.Write(list2);
		packet.Write(Data.TotalProgress);
		SendUDPData(ForClient, packet);
	}

	public static void REGISTERSPEICALITEM(ExpeditionManager.SpecialExpeditionItem Item, int OnlyFor)
	{
		using Packet packet = new Packet(183);
		packet.Write(Item);
		SendUDPData(OnlyFor, packet);
	}

	public static void REQUESTSPECIALITEMS(List<string> Items, int OnlyFor)
	{
		using Packet packet = new Packet(184);
		packet.Write(Items);
		SendUDPData(OnlyFor, packet);
	}

	public static void ANIMALSTOPSFEEDING(string GUID)
	{
		using Packet packet = new Packet(188);
		packet.Write(GUID);
		SendUDPDataToAll(packet);
	}
}
