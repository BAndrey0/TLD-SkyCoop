using System;
using System.Net;
using MelonLoader;
using SkyCoop;

namespace GameServer;

internal class Client
{
	public class UDP
	{
		public IPEndPoint endPoint;

		private int id;

		public string sid = "";

		public Client client;

		public UDP(int _id)
		{
			id = _id;
		}

		public void Connect(IPEndPoint _endPoint)
		{
			Log($"Incoming connection from {_endPoint.Address}...");
			endPoint = _endPoint;
			ServerSend.Welcome(id, Server.MaxPlayers);
		}

		public void ConnectSteamWorks(string _sid)
		{
			sid = _sid;
			ServerSend.Welcome(id, Server.MaxPlayers);
		}

		public void SendData(Packet _packet, bool IgnoreReady = true)
		{
			if (IgnoreReady || client.Ready)
			{
				if (!Server.UsingSteamWorks)
				{
					Server.SendUDPData(endPoint, _packet);
				}
				else
				{
					Server.SendP2PData(sid, _packet);
				}
			}
		}

		public void HandleData(Packet _packetData)
		{
			int length = _packetData.ReadInt();
			byte[] _packetBytes = _packetData.ReadBytes(length);
			ThreadManager.ExecuteOnMainThread(delegate
			{
				using Packet packet = new Packet(_packetBytes);
				int key = packet.ReadInt();
				if (MyMod.DebugTrafficCheck)
				{
					Log("[DebugTrafficCheck] Got packet ID " + key);
					Log("[DebugTrafficCheck] Packet contains " + packet.ReturnSize() + " bytes");
				}
				Server.packetHandlers[key](id, packet);
			});
		}

		public void Disconnect()
		{
			endPoint = null;
			sid = "";
			client.Ready = false;
			client.ConnectionInitialized = false;
		}
	}

	public static int dataBufferSize = 4096;

	public int id;

	public UDP udp;

	public int TimeOutTime;

	public bool RCON;

	public string SubNetworkGUID = "";

	public bool Ready;

	public bool ConnectionInitialized;

	public Client(int _clientId)
	{
		id = _clientId;
		udp = new UDP(id);
		udp.client = this;
	}

	public bool IsBusy()
	{
		if (udp.endPoint != null || udp.sid != "")
		{
			return true;
		}
		return false;
	}

	public static void Log(string LOG)
	{
		MelonLogger.Msg(ConsoleColor.Blue, LOG);
	}
}
