using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Il2Cpp;
using MelonLoader;
using SkyCoop;

namespace GameServer;

internal class Server
{
	public delegate void PacketHandler(int _fromClient, Packet _packet);

	public static Dictionary<int, Client> clients = new Dictionary<int, Client>();

	public static Dictionary<int, string> p2pclients = new Dictionary<int, string>();

	public static Dictionary<int, PacketHandler> packetHandlers;

	public static bool UsingSteamWorks = false;

	public static UdpClient udpListener;

	public static int MaxPlayers { get; private set; }

	public static int Port { get; private set; }

	public static void Log(string TXT, Shared.LoggerColor Color = Shared.LoggerColor.White)
	{
		MelonLogger.Msg(MyMod.ConvertLoggerColor(Color), TXT);
	}

	public static void Start(int _maxPlayers, int _port = 26950)
	{
		MaxPlayers = _maxPlayers;
		MyMod.MaxPlayers = MaxPlayers;
		Port = _port;
		Log("Starting server...");
		InitializeServerData();
		string environmentVariable = Environment.GetEnvironmentVariable("SKYCOOP_BIND_ADDRESS");
		udpListener = new UdpClient(new IPEndPoint(string.IsNullOrEmpty(environmentVariable) ? IPAddress.Any : IPAddress.Parse(environmentVariable), Port));
		udpListener.BeginReceive(UDPReceiveCallback, null);
		Log($"Server started on port {Port}.");
		MPStats.Start();
		MPStats.AddPlayer(MPSaveManager.GetSubNetworkGUID(), MyMod.MyChatName);
	}

	public static void StartSteam(int _maxPlayers, string[] whitelist = null)
	{
		MPSaveManager.LoadNonUnloadables();
		MaxPlayers = _maxPlayers;
		MyMod.MaxPlayers = MaxPlayers;
		Log("[SteamWorks.NET] Starting multiplayer...");
		InitializeServerData();
		UsingSteamWorks = true;
		Shared.InitAllPlayers();
		MyMod.iAmHost = true;
		MyMod.AnimalsController = true;
		MyMod.WildlifeAuthorityScene = "";
		MyMod.OverridedHourse = GameManager.GetTimeOfDayComponent().GetHour();
		MyMod.OverridedMinutes = GameManager.GetTimeOfDayComponent().GetMinutes();
		MyMod.OveridedTime = MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes;
		MyMod.NeedSyncTime = true;
		MyMod.DisableOriginalAnimalSpawns(host: true);
		MyMod.SetFixedSpawn();
		MyMod.KillConsole();
		SteamConnect.Main.SetLobbyServer();
		SteamConnect.Main.SetLobbyState("Playing");
		Log("Server has been runned with InGame time: " + GameManager.GetTimeOfDayComponent().GetHour() + ":" + GameManager.GetTimeOfDayComponent().GetMinutes() + " seed " + GameManager.m_SceneTransitionData.m_GameRandomSeed);
		MPStats.Start();
		MPStats.AddPlayer(MPSaveManager.GetSubNetworkGUID(), MyMod.MyChatName);
	}

	private static void UDPReceiveCallback(IAsyncResult _result)
	{
		try
		{
			IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
			byte[] array = null;
			try
			{
				array = udpListener.EndReceive(_result, ref remoteEP);
			}
			catch (SocketException)
			{
				udpListener.BeginReceive(UDPReceiveCallback, null);
				return;
			}
			if (array == null)
			{
				return;
			}
			udpListener.BeginReceive(UDPReceiveCallback, null);
			if (array.Length < 4)
			{
				return;
			}
			using Packet packet = new Packet(array);
			int num = packet.ReadInt();
			string text = "";
			if (num != -2 && num != -1)
			{
				if (packet.UnreadLength() <= 12)
				{
					Log("Client without SubNetworkGUID trying to connect, rejecting, _clientId " + num);
					ServerSend.KICKMESSAGE(remoteEP, "Your version of the mod, isn't supported\nHost using version 0.14.3");
					return;
				}
				text = packet.ReadString();
				if (MPSaveManager.BannedUsers.TryGetValue(text, out var value))
				{
					if (string.IsNullOrEmpty(value))
					{
						ServerSend.KICKMESSAGE(remoteEP, "You has been banned from the server.");
					}
					else
					{
						ServerSend.KICKMESSAGE(remoteEP, "You has been banned from the server.\nReason: " + value);
					}
					return;
				}
				if (MyMod.DebugTrafficCheck)
				{
					Log("[DebugTrafficCheck] _clientId " + num);
					Log("[DebugTrafficCheck] SubNetworkGUID " + text);
				}
			}
			switch (num)
			{
			case 0:
			{
				int num2 = FindUdpClientSlot(remoteEP, text);
				if (num2 < 0)
				{
					ServerSend.KICKMESSAGE(remoteEP, "Server is full.");
					return;
				}
				Client client = clients[num2];
				bool flag = client.udp.endPoint != null;
				client.RCON = false;
				client.Ready = false;
				client.TimeOutTime = 0;
				client.udp.endPoint = null;
				if (MyMod.playersData[num2] != null)
				{
					MyMod.playersData[num2].m_FirstBoot = false;
				}
				Log("[UDP] " + (flag ? "Reconnecting " : "New connection ") + remoteEP?.ToString() + " as client " + num2);
				num = num2;
				break;
			}
			case -1:
				Log("[RCON] Request by " + remoteEP.Address.ToString());
				if (packet.UnreadLength() <= 4)
				{
					return;
				}
				if (packet.ReadString() == MyMod.RCON)
				{
					if (packet.UnreadLength() > 4)
					{
						string sTR = Shared.ExecuteCommand(packet.ReadUnicodeString());
						ServerSend.RCONSENDSTRING(remoteEP, sTR);
					}
				}
				else
				{
					ServerSend.RCONSENDSTRING(remoteEP, "Request rejected!");
				}
				return;
			case -2:
				ServerSend.PINGSERVER(remoteEP);
				return;
			}
			if (num > 0 && num < MaxPlayers && clients.ContainsKey(num))
			{
				if (clients[num].udp.endPoint == null)
				{
					clients[num].SubNetworkGUID = text;
					clients[num].udp.Connect(remoteEP);
				}
				else if (clients[num].udp.endPoint.ToString() == remoteEP.ToString())
				{
					clients[num].udp.HandleData(packet);
				}
			}
		}
		catch (Exception value2)
		{
			Log($"Error receiving UDP data: {value2}");
		}
	}

	public static void SendUDPData(IPEndPoint _clientEndPoint, Packet _packet, bool IgnoreReady = true)
	{
		try
		{
			if (_clientEndPoint != null)
			{
				udpListener.BeginSend(_packet.ToArray(), _packet.Length(), _clientEndPoint, null, null);
			}
		}
		catch (Exception value)
		{
			Log($"Error sending data to {_clientEndPoint} via UDP: {value}");
		}
	}

	public static void SendP2PData(string _client, Packet _packet)
	{
		if (_client != "" && UsingSteamWorks && SteamConnect.CanUseSteam)
		{
			SteamConnect.Main.SendUDPData(_packet, _client);
		}
	}

	public static string GetMACByID(int ID)
	{
		if (ID == 0)
		{
			return MPSaveManager.GetSubNetworkGUID();
		}
		if (clients.TryGetValue(ID, out var value))
		{
			return value.SubNetworkGUID;
		}
		return "";
	}

	public static List<string> GetMACsOfPlayers()
	{
		List<string> list = new List<string>();
		list.Add(MPSaveManager.GetSubNetworkGUID());
		for (int i = 1; i <= MaxPlayers; i++)
		{
			if (clients[i].IsBusy())
			{
				list.Add(clients[i].SubNetworkGUID);
			}
		}
		return list;
	}

	public static int GetIDByMAC(string MAC)
	{
		if (MAC == MPSaveManager.GetSubNetworkGUID())
		{
			return 0;
		}
		foreach (KeyValuePair<int, Client> client in clients)
		{
			if (client.Value.SubNetworkGUID == MAC)
			{
				return client.Key;
			}
		}
		return -1;
	}

	private static int FindUdpClientSlot(IPEndPoint endPoint, string subNetworkGuid)
	{
		for (int i = 1; i < MaxPlayers; i++)
		{
			if (clients.TryGetValue(i, out var value) && !value.RCON && value.udp.endPoint != null && value.udp.endPoint.Equals(endPoint) && value.SubNetworkGUID == subNetworkGuid)
			{
				return i;
			}
		}
		for (int j = 1; j < MaxPlayers; j++)
		{
			if (clients.TryGetValue(j, out var value2) && !value2.IsBusy())
			{
				return j;
			}
		}
		return -1;
	}

	private static void InitializeServerData()
	{
		for (int i = 1; i <= MaxPlayers; i++)
		{
			clients.Add(i, new Client(i));
		}
		if (MyMod.DedicatedServerAppMode)
		{
			clients.Add(MaxPlayers + 1, new Client(MaxPlayers + 1));
		}
		packetHandlers = new Dictionary<int, PacketHandler>
		{
			{
				1,
				ServerHandle.WelcomeReceived
			},
			{
				2,
				ServerHandle.XYZ
			},
			{
				3,
				ServerHandle.XYZW
			},
			{
				4,
				ServerHandle.BLOCK
			},
			{
				5,
				ServerHandle.XYZDW
			},
			{
				6,
				ServerHandle.LEVELID
			},
			{
				7,
				ServerHandle.GOTITEM
			},
			{
				8,
				ServerHandle.GAMETIME
			},
			{
				10,
				ServerHandle.LIGHTSOURCENAME
			},
			{
				9,
				ServerHandle.LIGHTSOURCE
			},
			{
				11,
				ServerHandle.ANIMSTATE
			},
			{
				12,
				ServerHandle.SLEEPHOURS
			},
			{
				13,
				ServerHandle.SYNCWEATHER
			},
			{
				14,
				ServerHandle.REVIVE
			},
			{
				15,
				ServerHandle.REVIVEDONE
			},
			{
				23,
				ServerHandle.ANIMALSYNCTRIGG
			},
			{
				18,
				ServerHandle.DARKWALKERREADY
			},
			{
				20,
				ServerHandle.WARDISACTIVE
			},
			{
				21,
				ServerHandle.REQUESTDWREADYSTATE
			},
			{
				22,
				ServerHandle.DWCOUNTDOWN
			},
			{
				24,
				ServerHandle.SHOOTSYNC
			},
			{
				25,
				ServerHandle.PIMPSKILL
			},
			{
				26,
				ServerHandle.HARVESTINGANIMAL
			},
			{
				27,
				ServerHandle.DONEHARVASTING
			},
			{
				29,
				ServerHandle.BULLETDAMAGE
			},
			{
				30,
				ServerHandle.MULTISOUND
			},
			{
				31,
				ServerHandle.CONTAINEROPEN
			},
			{
				32,
				ServerHandle.LUREPLACEMENT
			},
			{
				33,
				ServerHandle.LUREISACTIVE
			},
			{
				35,
				ServerHandle.ALIGNANIMAL
			},
			{
				34,
				ServerHandle.ASKFORANIMALPROXY
			},
			{
				36,
				ServerHandle.CARRYBODY
			},
			{
				37,
				ServerHandle.BODYWARP
			},
			{
				38,
				ServerHandle.ANIMALDELETE
			},
			{
				39,
				ServerHandle.KEEPITALIVE
			},
			{
				41,
				ServerHandle.EQUIPMENT
			},
			{
				42,
				ServerHandle.CHAT
			},
			{
				44,
				ServerHandle.CONNECTSTEAM
			},
			{
				45,
				ServerHandle.CHANGENAME
			},
			{
				46,
				ServerHandle.CLOTH
			},
			{
				47,
				ServerHandle.ASKSPAWNDATA
			},
			{
				48,
				ServerHandle.LEVELGUID
			},
			{
				49,
				ServerHandle.FURNBROKEN
			},
			{
				51,
				ServerHandle.FURNBREAKINGGUID
			},
			{
				52,
				ServerHandle.FURNBREAKINSTOP
			},
			{
				53,
				ServerHandle.GEARPICKUP
			},
			{
				55,
				ServerHandle.ROPE
			},
			{
				57,
				ServerHandle.CONSUME
			},
			{
				59,
				ServerHandle.STOPCONSUME
			},
			{
				60,
				ServerHandle.HEAVYBREATH
			},
			{
				61,
				ServerHandle.BLOODLOSTS
			},
			{
				62,
				ServerHandle.APPLYACTIONONPLAYER
			},
			{
				63,
				ServerHandle.DONTMOVEWARNING
			},
			{
				64,
				ServerHandle.INFECTIONSRISK
			},
			{
				66,
				ServerHandle.CONTAINERINTERACT
			},
			{
				69,
				ServerHandle.HARVESTPLANT
			},
			{
				72,
				ServerHandle.SELECTEDCHARACTER
			},
			{
				73,
				ServerHandle.ADDSHELTER
			},
			{
				74,
				ServerHandle.REMOVESHELTER
			},
			{
				76,
				ServerHandle.USESHELTER
			},
			{
				77,
				ServerHandle.FIRE
			},
			{
				78,
				ServerHandle.CUSTOM
			},
			{
				80,
				ServerHandle.GOTITEMSLICE
			},
			{
				81,
				ServerHandle.VOICECHAT
			},
			{
				82,
				ServerHandle.SLICEDBYTES
			},
			{
				83,
				ServerHandle.ANIMALDAMAGE
			},
			{
				84,
				ServerHandle.FIREFUEL
			},
			{
				85,
				ServerHandle.DROPITEM
			},
			{
				86,
				ServerHandle.GOTDROPSLICE
			},
			{
				88,
				ServerHandle.REQUESTPICKUP
			},
			{
				90,
				ServerHandle.REQUESTDROPSFORSCENE
			},
			{
				91,
				ServerHandle.REQUESTPLACE
			},
			{
				93,
				ServerHandle.GOTCONTAINERSLICE
			},
			{
				94,
				ServerHandle.REQUESTOPENCONTAINER
			},
			{
				98,
				ServerHandle.CHANGEAIM
			},
			{
				101,
				ServerHandle.USEOPENABLE
			},
			{
				102,
				ServerHandle.TRYDIAGNISISPLAYER
			},
			{
				103,
				ServerHandle.SENDMYAFFLCTIONS
			},
			{
				104,
				ServerHandle.CUREAFFLICTION
			},
			{
				105,
				ServerHandle.ANIMALTEST
			},
			{
				106,
				ServerHandle.ANIMALKILLED
			},
			{
				108,
				ServerHandle.REQUESTANIMALCORPSE
			},
			{
				109,
				ServerHandle.QUARTERANIMAL
			},
			{
				110,
				ServerHandle.ANIMALAUDIO
			},
			{
				111,
				ServerHandle.PICKUPRABBIT
			},
			{
				113,
				ServerHandle.RELEASERABBIT
			},
			{
				114,
				ServerHandle.HITRABBIT
			},
			{
				116,
				ServerHandle.CHANGEDFREQUENCY
			},
			{
				117,
				ServerHandle.MELEESTART
			},
			{
				118,
				ServerHandle.TRYBORROWGEAR
			},
			{
				122,
				ServerHandle.ADDDEATHCONTAINER
			},
			{
				123,
				ServerHandle.DEATHCREATEEMPTYNOW
			},
			{
				124,
				ServerHandle.SPAWNREGIONBANCHECK
			},
			{
				121,
				ServerHandle.CHALLENGETRIGGER
			},
			{
				129,
				ServerHandle.RCONCOMMAND
			},
			{
				131,
				ServerHandle.ADDDOORLOCK
			},
			{
				133,
				ServerHandle.TRYOPENDOOR
			},
			{
				135,
				ServerHandle.LOCKPICK
			},
			{
				137,
				ServerHandle.VERIFYSAVE
			},
			{
				138,
				ServerHandle.SAVEHASH
			},
			{
				139,
				ServerHandle.FORCELOADING
			},
			{
				141,
				ServerHandle.REQUESTLOCKSMITH
			},
			{
				142,
				ServerHandle.APPLYTOOLONBLANK
			},
			{
				145,
				ServerHandle.LETENTER
			},
			{
				146,
				ServerHandle.PEEPHOLE
			},
			{
				143,
				ServerHandle.KNOCKKNOCK
			},
			{
				148,
				ServerHandle.RESTART
			},
			{
				151,
				ServerHandle.WEATHERVOLUNTEER
			},
			{
				152,
				ServerHandle.REREGISTERWEATHER
			},
			{
				155,
				ServerHandle.CHANGECONTAINERSTATE
			},
			{
				157,
				ServerHandle.TRIGGEREMOTE
			},
			{
				160,
				ServerHandle.PHOTOREQUEST
			},
			{
				161,
				ServerHandle.GOTPHOTOSLICE
			},
			{
				163,
				ServerHandle.STARTEXPEDITION
			},
			{
				164,
				ServerHandle.ACCEPTEXPEDITIONINVITE
			},
			{
				165,
				ServerHandle.REQUESTEXPEDITIONINVITES
			},
			{
				166,
				ServerHandle.CREATEEXPEDITIONINVITE
			},
			{
				170,
				ServerHandle.ADDROCKCACH
			},
			{
				171,
				ServerHandle.REMOVEROCKCACH
			},
			{
				172,
				ServerHandle.REMOVEROCKCACHFINISHED
			},
			{
				173,
				ServerHandle.CHARCOALDRAW
			},
			{
				174,
				ServerHandle.CHATCOMMAND
			},
			{
				177,
				ServerHandle.REQUESTCONTAINERSTATE
			},
			{
				179,
				ServerHandle.INTERACTIONDONE
			},
			{
				180,
				ServerHandle.REMOVEOBJECTGROUP
			},
			{
				181,
				ServerHandle.REQUESTSPECIALEXPEDITION
			},
			{
				186,
				ServerHandle.PICKUPSPECAILITEM
			},
			{
				185,
				ServerHandle.REQUESTSPECIALITEMS
			},
			{
				187,
				ServerHandle.ANIMALDESTORYGEAR
			},
			{
				188,
				ServerHandle.ANIMALBITECORPSE
			},
			{
				190,
				ServerHandle.COUGARSTATE
			},
			{
				191,
				ServerHandle.TRADERSTATE
			},
			{
				192,
				ServerHandle.TRAVOISSYNC
			},
			{
				193,
				ServerHandle.NOISEMAKERIGNITE
			}
		};
		Log("Initialized packets.");
	}
}
