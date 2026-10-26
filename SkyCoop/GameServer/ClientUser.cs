using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MelonLoader;
using SkyCoop;

namespace GameServer;

internal class ClientUser
{
	public delegate void PacketHandler(Packet _packet);

	public class PingData
	{
		public string ServerData = "";

		public int PlayersMax;

		public int Players;

		public bool CanJoin;

		public string Error = "";
	}

	public class UDP
	{
		public UdpClient socket;

		public IPEndPoint endPoint;

		public UDP()
		{
			endPoint = new IPEndPoint(IPAddress.Parse(ip), ConnectPort);
		}

		public void Connect(int _localPort, string ip)
		{
			endPoint = new IPEndPoint(IPAddress.Parse(ip), ConnectPort);
			socket = new UdpClient(_localPort);
			socket.Connect(endPoint);
			MelonLogger.Msg("Open socket for " + endPoint.Address?.ToString() + ":" + endPoint.Port + " local port is " + _localPort);
			socket.BeginReceive(ReceiveCallback, null);
			using Packet packet = new Packet();
			packet.Write(myId);
			SendData(packet);
		}

		public void SendData(Packet _packet, bool IgnoreQuit = false)
		{
			if (!IgnoreQuit && MyMod.QuitWhenSaveOperationFinished)
			{
				return;
			}
			try
			{
				_packet.WriteLength();
				_packet.InsertClientInfo(myId, SubNetworkClientGUID);
				if (socket != null)
				{
					socket.BeginSend(_packet.ToArray(), _packet.Length(), null, null);
				}
			}
			catch (Exception value)
			{
				MelonLogger.Msg($"Error sending data to server via UDP: {value}");
			}
		}

		private void ReceiveCallback(IAsyncResult _result)
		{
			if (MyMod.QuitWhenSaveOperationFinished)
			{
				return;
			}
			try
			{
				byte[] array = socket.EndReceive(_result, ref endPoint);
				socket.BeginReceive(ReceiveCallback, null);
				if (array.Length < 4)
				{
					endPoint = null;
					socket.Close();
					MyMod.Disconnect();
					MelonLogger.Msg("Connection failed");
				}
				else
				{
					HandleData(array);
				}
			}
			catch
			{
				endPoint = null;
				socket.Close();
				MyMod.Disconnect();
				MelonLogger.Msg("Connection failed");
			}
		}

		private void HandleData(byte[] _data)
		{
			using (Packet packet = new Packet(_data))
			{
				int length = packet.ReadInt();
				_data = packet.ReadBytes(length);
			}
			ThreadManager.ExecuteOnMainThread(delegate
			{
				using Packet packet2 = new Packet(_data);
				int key = packet2.ReadInt();
				if (MyMod.DebugTrafficCheck)
				{
					MelonLogger.Msg(ConsoleColor.Yellow, "[DebugTrafficCheck] Got packet ID " + key);
					MelonLogger.Msg(ConsoleColor.Yellow, "[DebugTrafficCheck] Packet size " + packet2.ReturnSize() + " bytes");
				}
				packetHandlers[key](packet2);
			});
		}

		private void Disconnect()
		{
			Disconnect();
			endPoint = null;
			socket = null;
		}
	}

	public static int dataBufferSize = 4096;

	public static string ip = "127.0.0.1";

	public static string LastConnectedIp = "";

	public static string PendingConnectionIp = "";

	public static int ConnectPort = 26950;

	public static int LocalPort = 26951;

	public static string SubNetworkClientGUID = "";

	public static int myId = 0;

	public static UDP udp;

	public static Dictionary<int, PacketHandler> packetHandlers;

	public static PingData Ping(string IP, int PORT)
	{
		PingData pingData = new PingData();
		UdpClient udpClient = new UdpClient();
		udpClient.Client.ReceiveTimeout = 5000;
		udpClient.Client.SendTimeout = 5000;
		IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(IP), PORT);
		try
		{
			udpClient.Connect(remoteEP);
			byte[] bytes = BitConverter.GetBytes(-2);
			udpClient.Send(bytes.ToArray(), bytes.Length);
			byte[] array = udpClient.Receive(ref remoteEP);
			if (array == null || array.Length < 4)
			{
				pingData.CanJoin = false;
				pingData.Error = "The server is not responding.";
				return pingData;
			}
			int num = 0;
			if (BitConverter.ToInt32(array, num) == 147)
			{
				MelonLogger.Msg("Correct header!");
				pingData.CanJoin = true;
				num += 4;
				int num2 = BitConverter.ToInt32(array, num);
				num += 4;
				string text = Encoding.Unicode.GetString(array, num, num2);
				num += num2;
				int num3 = BitConverter.ToInt32(array, num);
				num += 4;
				string text2 = Encoding.UTF8.GetString(array, num, num3);
				num += num3;
				int num4 = BitConverter.ToInt32(array, num);
				num += 4;
				int num5 = BitConverter.ToInt32(array, num);
				num += 4;
				int count = BitConverter.ToInt32(array, num);
				num += 4;
				string txt = (pingData.ServerData = Encoding.Unicode.GetString(array, num, count));
				pingData.Players = num4;
				pingData.PlayersMax = num5;
				MelonLogger.Msg(text + " version " + text2 + " Players: " + num4 + "/" + num5 + " Server json:");
				MelonLogger.Msg(txt);
				udpClient.Close();
				if (num4 >= num5)
				{
					pingData.CanJoin = false;
					pingData.Error = "Server is full!";
				}
				if (text2 != "0.14.9")
				{
					pingData.CanJoin = false;
					pingData.Error = "Server using " + text2 + " version of the mod!";
				}
				MyMod.MaxPlayers = num5;
				Shared.InitAllPlayers();
				return pingData;
			}
			MelonLogger.Msg("Wrong header!");
			pingData.CanJoin = false;
			pingData.Error = "Server respond incorrect data, make sure client and server use same version of the mod.";
			return pingData;
		}
		catch (Exception ex)
		{
			pingData.Error = ex.Message;
			pingData.CanJoin = false;
			return pingData;
		}
	}

	public static void ConnectToServer()
	{
		myId = 0;
		SubNetworkClientGUID = MPSaveManager.GetSubNetworkGUID();
		udp = new UDP();
		if (ip != "")
		{
			MelonLogger.Msg("My SubNetworkGUID " + SubNetworkClientGUID);
			InitializeClientData();
			udp.Connect(LocalPort, ip);
		}
	}

	// The first DNS answer can be an IPv6 link-local address that the IPv4 UDP socket rejects.
	private static string ResolvePreferIPv4(string host)
	{
		IPAddress[] hostAddresses = Dns.GetHostAddresses(host);
		return (hostAddresses.FirstOrDefault((IPAddress a) => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) ?? hostAddresses.FirstOrDefault())?.ToString() ?? "";
	}

	public static void DoConnectToIp(string _ip)
	{
		if (MyMod.TryResetSaveExitForNewSession())
		{
			MPSaveManager.SaveLastConnectedServer(_ip);
			string text = "";
			string text2 = "";
			if (_ip.Contains(':'))
			{
				string[] array = _ip.Split(':');
				text2 = ResolvePreferIPv4(array[0]);
				text = array[1];
			}
			else
			{
				text2 = ResolvePreferIPv4(_ip);
				text = "26950";
			}
			if (text2 == "" && text == "")
			{
				PendingConnectionIp = _ip;
				ip = _ip;
				MelonLogger.Msg("Going to connect to " + ip + ":" + ConnectPort);
			}
			else
			{
				PendingConnectionIp = text2;
				ip = text2;
				ConnectPort = Convert.ToInt32(text);
				MelonLogger.Msg("Going to connect to " + ip + ":" + ConnectPort);
			}
			MyMod.DoWaitForConnect();
			PingData pingData = Ping(ip, ConnectPort);
			if (pingData.CanJoin)
			{
				ConnectToServer();
				return;
			}
			MyMod.RemoveWaitForConnect();
			MyMod.DoOKMessage("Connection failed", pingData.Error);
			MelonLogger.Msg("Connection failed " + pingData.Error);
		}
	}

	public static void InitializeClientData()
	{
		packetHandlers = new Dictionary<int, PacketHandler>
		{
			{
				1,
				ClientHandle.Welcome
			},
			{
				2,
				ClientHandle.XYZ
			},
			{
				3,
				ClientHandle.XYZW
			},
			{
				4,
				ClientHandle.BLOCK
			},
			{
				5,
				ClientHandle.XYZDW
			},
			{
				6,
				ClientHandle.LEVELID
			},
			{
				7,
				ClientHandle.GOTITEM
			},
			{
				8,
				ClientHandle.GAMETIME
			},
			{
				10,
				ClientHandle.LIGHTSOURCENAME
			},
			{
				9,
				ClientHandle.LIGHTSOURCE
			},
			{
				11,
				ClientHandle.ANIMSTATE
			},
			{
				12,
				ClientHandle.SLEEPHOURS
			},
			{
				13,
				ClientHandle.SYNCWEATHER
			},
			{
				14,
				ClientHandle.REVIVE
			},
			{
				15,
				ClientHandle.REVIVEDONE
			},
			{
				16,
				ClientHandle.ANIMALROLE
			},
			{
				18,
				ClientHandle.DARKWALKERREADY
			},
			{
				19,
				ClientHandle.HOSTISDARKWALKER
			},
			{
				20,
				ClientHandle.WARDISACTIVE
			},
			{
				22,
				ClientHandle.DWCOUNTDOWN
			},
			{
				23,
				ClientHandle.ANIMALSYNCTRIGG
			},
			{
				24,
				ClientHandle.SHOOTSYNC
			},
			{
				25,
				ClientHandle.PIMPSKILL
			},
			{
				26,
				ClientHandle.HARVESTINGANIMAL
			},
			{
				28,
				ClientHandle.SAVEDATA
			},
			{
				29,
				ClientHandle.BULLETDAMAGE
			},
			{
				30,
				ClientHandle.MULTISOUND
			},
			{
				31,
				ClientHandle.CONTAINEROPEN
			},
			{
				32,
				ClientHandle.LUREPLACEMENT
			},
			{
				33,
				ClientHandle.LUREISACTIVE
			},
			{
				35,
				ClientHandle.ALIGNANIMAL
			},
			{
				34,
				ClientHandle.ASKFORANIMALPROXY
			},
			{
				36,
				ClientHandle.CARRYBODY
			},
			{
				37,
				ClientHandle.BODYWARP
			},
			{
				38,
				ClientHandle.ANIMALDELETE
			},
			{
				39,
				ClientHandle.KEEPITALIVE
			},
			{
				40,
				ClientHandle.RQRECONNECT
			},
			{
				41,
				ClientHandle.EQUIPMENT
			},
			{
				42,
				ClientHandle.CHAT
			},
			{
				43,
				ClientHandle.PLAYERSSTATUS
			},
			{
				45,
				ClientHandle.CHANGENAME
			},
			{
				46,
				ClientHandle.CLOTH
			},
			{
				47,
				ClientHandle.ASKSPAWNDATA
			},
			{
				48,
				ClientHandle.LEVELGUID
			},
			{
				49,
				ClientHandle.FURNBROKEN
			},
			{
				50,
				ClientHandle.FURNBROKENLIST
			},
			{
				51,
				ClientHandle.FURNBREAKINGGUID
			},
			{
				52,
				ClientHandle.FURNBREAKINSTOP
			},
			{
				53,
				ClientHandle.GEARPICKUP
			},
			{
				54,
				ClientHandle.GEARPICKUPLIST
			},
			{
				55,
				ClientHandle.ROPE
			},
			{
				56,
				ClientHandle.ROPELIST
			},
			{
				57,
				ClientHandle.CONSUME
			},
			{
				58,
				ClientHandle.SERVERCFG
			},
			{
				59,
				ClientHandle.STOPCONSUME
			},
			{
				60,
				ClientHandle.HEAVYBREATH
			},
			{
				61,
				ClientHandle.BLOODLOSTS
			},
			{
				62,
				ClientHandle.APPLYACTIONONPLAYER
			},
			{
				63,
				ClientHandle.DONTMOVEWARNING
			},
			{
				64,
				ClientHandle.INFECTIONSRISK
			},
			{
				65,
				ClientHandle.CANCLEPICKUP
			},
			{
				66,
				ClientHandle.CONTAINERINTERACT
			},
			{
				67,
				ClientHandle.LOOTEDCONTAINER
			},
			{
				68,
				ClientHandle.LOOTEDCONTAINERLIST
			},
			{
				69,
				ClientHandle.HARVESTPLANT
			},
			{
				70,
				ClientHandle.LOOTEDHARVESTABLE
			},
			{
				71,
				ClientHandle.LOOTEDHARVESTABLEALL
			},
			{
				72,
				ClientHandle.SELECTEDCHARACTER
			},
			{
				73,
				ClientHandle.ADDSHELTER
			},
			{
				74,
				ClientHandle.REMOVESHELTER
			},
			{
				75,
				ClientHandle.ALLSHELTERS
			},
			{
				76,
				ClientHandle.USESHELTER
			},
			{
				77,
				ClientHandle.FIRE
			},
			{
				78,
				ClientHandle.CUSTOM
			},
			{
				79,
				ClientHandle.KICKMESSAGE
			},
			{
				80,
				ClientHandle.GOTITEMSLICE
			},
			{
				81,
				ClientHandle.VOICECHAT
			},
			{
				82,
				ClientHandle.SLICEDBYTES
			},
			{
				83,
				ClientHandle.ANIMALDAMAGE
			},
			{
				84,
				ClientHandle.FIREFUEL
			},
			{
				85,
				ClientHandle.DROPITEM
			},
			{
				87,
				ClientHandle.PICKDROPPEDGEAR
			},
			{
				89,
				ClientHandle.GETREQUESTEDITEMSLICE
			},
			{
				92,
				ClientHandle.GETREQUESTEDFORPLACESLICE
			},
			{
				93,
				ClientHandle.GOTCONTAINERSLICE
			},
			{
				95,
				ClientHandle.OPENEMPTYCONTAINER
			},
			{
				96,
				ClientHandle.MARKSEARCHEDCONTAINERS
			},
			{
				97,
				ClientHandle.READYSENDNEXTSLICE
			},
			{
				98,
				ClientHandle.CHANGEAIM
			},
			{
				99,
				ClientHandle.LOADINGSCENEDROPSDONE
			},
			{
				100,
				ClientHandle.GEARNOTEXIST
			},
			{
				101,
				ClientHandle.USEOPENABLE
			},
			{
				102,
				ClientHandle.TRYDIAGNISISPLAYER
			},
			{
				103,
				ClientHandle.SENDMYAFFLCTIONS
			},
			{
				104,
				ClientHandle.CUREAFFLICTION
			},
			{
				105,
				ClientHandle.ANIMALTEST
			},
			{
				107,
				ClientHandle.ANIMALCORPSE
			},
			{
				108,
				ClientHandle.REQUESTANIMALCORPSE
			},
			{
				109,
				ClientHandle.QUARTERANIMAL
			},
			{
				110,
				ClientHandle.ANIMALAUDIO
			},
			{
				112,
				ClientHandle.GOTRABBIT
			},
			{
				113,
				ClientHandle.RELEASERABBIT
			},
			{
				114,
				ClientHandle.HITRABBIT
			},
			{
				115,
				ClientHandle.RABBITREVIVED
			},
			{
				117,
				ClientHandle.MELEESTART
			},
			{
				118,
				ClientHandle.TRYBORROWGEAR
			},
			{
				119,
				ClientHandle.CHALLENGEINIT
			},
			{
				120,
				ClientHandle.CHALLENGEUPDATE
			},
			{
				121,
				ClientHandle.CHALLENGETRIGGER
			},
			{
				122,
				ClientHandle.ADDDEATHCONTAINER
			},
			{
				123,
				ClientHandle.DEATHCREATEEMPTYNOW
			},
			{
				124,
				ClientHandle.SPAWNREGIONBANCHECK
			},
			{
				125,
				ClientHandle.CAIRNS
			},
			{
				126,
				ClientHandle.BENEFITINIT
			},
			{
				127,
				ClientHandle.MODSLIST
			},
			{
				131,
				ClientHandle.ADDDOORLOCK
			},
			{
				132,
				ClientHandle.DOORLOCKEDMSG
			},
			{
				134,
				ClientHandle.ENTERDOOR
			},
			{
				136,
				ClientHandle.REMOVEDOORLOCK
			},
			{
				135,
				ClientHandle.LOCKPICK
			},
			{
				137,
				ClientHandle.VERIFYSAVE
			},
			{
				140,
				ClientHandle.RPC
			},
			{
				141,
				ClientHandle.REQUESTLOCKSMITH
			},
			{
				143,
				ClientHandle.KNOCKKNOCK
			},
			{
				144,
				ClientHandle.KNOCKENTER
			},
			{
				146,
				ClientHandle.PEEPHOLE
			},
			{
				148,
				ClientHandle.RESTART
			},
			{
				149,
				ClientHandle.READYSENDNEXTSLICEGEAR
			},
			{
				150,
				ClientHandle.DEDICATEDWEATHER
			},
			{
				151,
				ClientHandle.WEATHERVOLUNTEER
			},
			{
				152,
				ClientHandle.REREGISTERWEATHER
			},
			{
				153,
				ClientHandle.REMOVEKEYBYSEED
			},
			{
				154,
				ClientHandle.ADDHUDMSG
			},
			{
				155,
				ClientHandle.CHANGECONTAINERSTATE
			},
			{
				156,
				ClientHandle.FINISHEDSENDINGCONTAINER
			},
			{
				157,
				ClientHandle.TRIGGEREMOTE
			},
			{
				158,
				ClientHandle.EXPEDITIONSYNC
			},
			{
				159,
				ClientHandle.EXPEDITIONRESULT
			},
			{
				162,
				ClientHandle.READYSENDNEXTSLICEPHOTO
			},
			{
				165,
				ClientHandle.REQUESTEXPEDITIONINVITES
			},
			{
				167,
				ClientHandle.NEWPLAYEREXPEDITION
			},
			{
				168,
				ClientHandle.NEWEXPEDITIONINVITE
			},
			{
				169,
				ClientHandle.BASE64SLICE
			},
			{
				170,
				ClientHandle.ADDROCKCACH
			},
			{
				171,
				ClientHandle.REMOVEROCKCACH
			},
			{
				175,
				ClientHandle.ADDUNIVERSALSYNCABLE
			},
			{
				176,
				ClientHandle.REMOVEUNIVERSALSYNCABLE
			},
			{
				178,
				ClientHandle.CUSTOMSOUNDEVENT
			},
			{
				180,
				ClientHandle.REQUESTSPECIALEXPEDITION
			},
			{
				181,
				ClientHandle.REQUESTSPHOTOAGAIN
			},
			{
				182,
				ClientHandle.REQUESTEXPEDITIONSPROGRESS
			},
			{
				183,
				ClientHandle.REGISTERSPEICALITEM
			},
			{
				184,
				ClientHandle.REQUESTSPECIALITEMS
			},
			{
				188,
				ClientHandle.ANIMALSTOPSFEEDING
			},
			{
				189,
				ClientHandle.COUGARSTATE
			},
			{
				190,
				ClientHandle.TRADERSTATE
			},
			{
				191,
				ClientHandle.TRAVOISSYNC
			},
			{
				192,
				ClientHandle.NOISEMAKERIGNITE
			}
		};
		MelonLogger.Msg("Initialized packets.");
	}
}
