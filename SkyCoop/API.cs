using System;
using GameServer;
using MelonLoader;

namespace SkyCoop;

public class API
{
	public enum SkyCoopClientState
	{
		NONE = -1,
		CLIENT,
		HOST
	}

	public static SkyCoopClientState m_ClientState = SkyCoopClientState.NONE;

	public static bool m_AllowMods = true;

	public static int m_MyClientID = 0;

	public static bool m_Debug = false;

	public static bool IsP2PServer = false;

	public static int Port = 0;

	public static void CustomEventCallback(Packet _pak, int from)
	{
		if (m_Debug)
		{
			MelonLogger.Msg(ConsoleColor.Blue, "[SkyCoop API] ");
		}
	}

	public static void SendDataToEveryone(Packet _pak, int from, bool ignoreSender)
	{
		if (m_ClientState == SkyCoopClientState.HOST)
		{
			_pak.Write(from);
			if (ignoreSender)
			{
				ServerSend.SendUDPDataToAllButNotSender(_pak, from);
			}
			else
			{
				ServerSend.SendUDPDataToAll(_pak);
			}
		}
		else if (m_ClientState == SkyCoopClientState.CLIENT)
		{
			MelonLogger.Msg(ConsoleColor.Red, "[SkyCoop API] Client trying call SendDataToEveryone, but this host only method.");
		}
	}

	public static void SendDataToClient(Packet _pak, int from, int to)
	{
		if (m_ClientState == SkyCoopClientState.HOST)
		{
			_pak.Write(from);
			ServerSend.SendUDPData(to, _pak);
		}
		else if (m_ClientState == SkyCoopClientState.CLIENT)
		{
			MelonLogger.Msg(ConsoleColor.Red, "[SkyCoop API] Client trying call SendDataToClient, but this host only method.");
		}
	}

	public static void SendToHost(Packet _pak)
	{
		if (m_ClientState == SkyCoopClientState.CLIENT)
		{
			MyMod.SendUDPData(_pak);
		}
		else if (m_ClientState == SkyCoopClientState.HOST)
		{
			MelonLogger.Msg(ConsoleColor.Red, "[SkyCoop API] Host trying call SendToHost, but this client only method.");
		}
	}

	public static void TestCustom()
	{
		using Packet packet = new Packet(78);
		packet.Write("MODNAME_EVENTNAME");
		packet.Write("Hello this is test message");
		if (m_AllowMods)
		{
			if (m_ClientState == SkyCoopClientState.HOST)
			{
				SendDataToEveryone(packet, m_MyClientID, ignoreSender: true);
			}
			else if (m_ClientState == SkyCoopClientState.CLIENT)
			{
				SendToHost(packet);
			}
		}
	}
}
