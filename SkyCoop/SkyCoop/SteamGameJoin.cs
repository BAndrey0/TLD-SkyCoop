using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameServer;
using HarmonyLib;
using MelonLoader;
using Steamworks;

namespace SkyCoop;

public static class SteamGameJoin
{
	[HarmonyPatch(typeof(SteamConnect.Main), "OnEnterLobby")]
	private static class LobbyEntered
	{
		private static void Postfix(LobbyEnter_t __0)
		{
			if (__0.m_EChatRoomEnterResponse == 1 && phase == "suspended")
			{
				phase = "idle";
				Status = "Ожидание запуска мира хостом";
			}
		}
	}

	[HarmonyPatch(typeof(ClientHandle), "WelcomeReceived")]
	private static class WelcomeAcknowledged
	{
		private static void Prefix(List<string> __0)
		{
			welcomeWhiteList = new List<string>(__0);
			if (phase != "welcome")
			{
				phase = "welcome";
				deadline = clock.Elapsed.TotalSeconds + 45.0;
				nextRetry = clock.Elapsed.TotalSeconds + 3.0;
			}
			Status = "Получаем данные мира…";
			MelonLogger.Msg("[Steam join] Stage=WELCOME_ACK_SENT client=" + ClientUser.myId);
		}
	}

	[HarmonyPatch(typeof(ClientHandle), "SAVEDATA")]
	private static class SaveArrived
	{
		private static void Prefix()
		{
			phase = "world-data";
			welcomeWhiteList = null;
			Status = "Выбор персонажа и загрузка мира…";
			MelonLogger.Msg("[Steam join] Stage=WORLD_DATA_RECEIVED");
		}
	}

	private static readonly Stopwatch clock = Stopwatch.StartNew();

	private static readonly Dictionary<string, int> traces = new Dictionary<string, int>();

	private static string target = "";

	private static string phase = "idle";

	private static double deadline;

	private static double nextRetry;

	private static double nextPoll;

	private static List<string> welcomeWhiteList;

	public static bool NeedsWorldData => phase != "world-data";

	public static string Status { get; private set; } = "Ожидание запуска мира хостом";

	public static void Suspend()
	{
		target = "";
		welcomeWhiteList = null;
		phase = "suspended";
		Status = "Отключено. Нажмите «Войти в игру» для подключения";
	}

	public static bool Begin(string host)
	{
		if (MyMod.sendMyPosition || MyMod.iAmHost)
		{
			return false;
		}
		if (target == host && phase == "connecting")
		{
			return false;
		}
		target = host;
		phase = "connecting";
		deadline = clock.Elapsed.TotalSeconds + 45.0;
		nextRetry = clock.Elapsed.TotalSeconds + 3.0;
		Status = "Подключение к хосту…";
		MelonLogger.Msg("[Steam join] Stage=CONNECTING");
		return true;
	}

	public static bool TryJoin(bool manual)
	{
		if (phase == "suspended" && !manual)
		{
			return false;
		}
		if (!SteamConnect.CanUseSteam || MyMod.iAmHost || !Shared.IsMainMenuScene(MyMod.level_name))
		{
			return false;
		}
		if (MyMod.sendMyPosition)
		{
			if (manual && welcomeWhiteList != null)
			{
				phase = "welcome";
				deadline = clock.Elapsed.TotalSeconds + 45.0;
				nextRetry = clock.Elapsed.TotalSeconds + 3.0;
				ClientHandle.WelcomeReceived(welcomeWhiteList);
				Status = "Повторно запрашиваем данные мира…";
				return true;
			}
			return false;
		}
		if (!ulong.TryParse(MyMod.MyLobby, out var result) || result == 0L)
		{
			Status = "Сначала войдите в лобби Steam";
			return false;
		}
		CSteamID steamIDLobby = new CSteamID(result);
		CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(steamIDLobby);
		if (lobbyOwner.m_SteamID == 0L || lobbyOwner == SteamUser.GetSteamID())
		{
			return false;
		}
		if (!SteamMatchmaking.GetLobbyGameServer(steamIDLobby, out var _, out var _, out var psteamIDGameServer) || psteamIDGameServer.m_SteamID == 0)
		{
			if (SteamMatchmaking.GetLobbyData(steamIDLobby, "State") != "Playing")
			{
				Status = "Хост ещё не загрузил мир";
				SteamMatchmaking.RequestLobbyData(steamIDLobby);
				return false;
			}
			psteamIDGameServer = lobbyOwner;
		}
		if (manual && phase == "connecting")
		{
			Status = "Подключение уже идёт…";
			return false;
		}
		if (manual)
		{
			phase = "idle";
		}
		SteamConnect.Main.ConnectToHost(psteamIDGameServer.ToString());
		return phase == "connecting";
	}

	public static void Tick()
	{
		if (MyMod.IReallyWantToQuit || phase == "suspended")
		{
			return;
		}
		double totalSeconds = clock.Elapsed.TotalSeconds;
		if (phase == "connecting")
		{
			if (MyMod.sendMyPosition)
			{
				phase = "welcome";
				Status = "Получаем данные мира…";
				MelonLogger.Msg("[Steam join] Stage=WELCOME_RECEIVED");
			}
			else if (totalSeconds >= deadline)
			{
				phase = "failed";
				Status = "Хост не ответил. Нажмите «Войти в игру» для повтора";
				MyMod.RemoveWaitForConnect();
				MyMod.RemovePleaseWait();
				if (MyMod.LobbyUI != null)
				{
					MyMod.LobbyUI.SetActive(value: true);
				}
				MelonLogger.Warning("[Steam join] Timed out before Welcome. " + Session(target));
			}
			else if (totalSeconds >= nextRetry)
			{
				nextRetry = totalSeconds + 3.0;
				SteamNetworking.AcceptP2PSessionWithUser(new CSteamID(ulong.Parse(target)));
				MyMod.DoSteamWorksConnect(SteamUser.GetSteamID().ToString());
				MelonLogger.Msg("[Steam join] Retrying CONNECTSTEAM. " + Session(target));
			}
		}
		if (phase == "welcome" && welcomeWhiteList != null)
		{
			if (totalSeconds >= deadline)
			{
				phase = "failed";
				Status = "Нет данных мира. Нажмите «Войти в игру» для повтора";
				MyMod.RemoveWaitForConnect();
				MyMod.RemovePleaseWait();
				MelonLogger.Warning("[Steam join] Timed out after Welcome, before world data. " + Session(target));
			}
			else if (totalSeconds >= nextRetry)
			{
				nextRetry = totalSeconds + 3.0;
				ClientHandle.WelcomeReceived(welcomeWhiteList);
				MelonLogger.Msg("[Steam join] Retrying Welcome acknowledgement, awaiting world data.");
			}
		}
		if (!(totalSeconds < nextPoll))
		{
			nextPoll = totalSeconds + 2.0;
			if (phase != "failed" && phase != "connecting" && !MyMod.sendMyPosition && !string.IsNullOrEmpty(MyMod.MyLobby))
			{
				TryJoin(manual: false);
			}
		}
	}

	private static bool Allowed(CSteamID peer)
	{
		if (MyMod.ConnectedSteamWorks && peer.ToString() == MyMod.SteamServerWorks)
		{
			return true;
		}
		if (MyMod.iAmHost)
		{
			foreach (KeyValuePair<int, Client> client in Server.clients)
			{
				if (client.Value.udp != null && client.Value.udp.sid == peer.ToString())
				{
					return true;
				}
			}
		}
		if (!ulong.TryParse(MyMod.MyLobby, out var result) || result == 0L)
		{
			return false;
		}
		CSteamID steamIDLobby = new CSteamID(result);
		int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(steamIDLobby);
		for (int i = 0; i < numLobbyMembers; i++)
		{
			if (SteamMatchmaking.GetLobbyMemberByIndex(steamIDLobby, i) == peer)
			{
				return true;
			}
		}
		return false;
	}

	public static void OnSessionRequest(P2PSessionRequest_t request)
	{
		bool flag = Allowed(request.m_steamIDRemote);
		bool flag2 = flag && SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
		MelonLogger.Msg("[Steam join] Session request allowed=" + flag + " accepted=" + flag2);
	}

	public static void OnSessionFailure(P2PSessionConnectFail_t failure)
	{
		MelonLogger.Warning("[Steam join] P2P failure code=" + failure.m_eP2PSessionError + " " + Session(failure.m_steamIDRemote.ToString()));
		if (failure.m_steamIDRemote.ToString() == target && !MyMod.sendMyPosition)
		{
			Status = "Ошибка соединения Steam: " + failure.m_eP2PSessionError;
		}
	}

	private static string Session(string peer)
	{
		if (!ulong.TryParse(peer, out var result))
		{
			return "session=invalid-peer";
		}
		try
		{
			if (!SteamNetworking.GetP2PSessionState(new CSteamID(result), out var pConnectionState))
			{
				return "session=unavailable";
			}
			return "active=" + pConnectionState.m_bConnectionActive + " connecting=" + pConnectionState.m_bConnecting + " error=" + pConnectionState.m_eP2PSessionError + " relay=" + pConnectionState.m_bUsingRelay + " queued=" + pConnectionState.m_nPacketsQueuedForSend;
		}
		catch (Exception ex)
		{
			return "session=" + ex.GetType().Name;
		}
	}

	public static void TracePacket(CSteamID peer, byte[] data, bool incoming, bool sent)
	{
		string obj = (incoming ? "in:" : "out:");
		CSteamID cSteamID = peer;
		string key = obj + cSteamID.ToString();
		traces.TryGetValue(key, out var value);
		traces[key] = value + 1;
		if ((value >= 12 && sent) || value >= 20)
		{
			return;
		}
		string text = "invalid";
		try
		{
			using Packet packet = new Packet(data);
			bool num = (incoming ? MyMod.iAmHost : (!MyMod.iAmHost));
			if (num)
			{
				packet.ReadInt();
				packet.ReadString();
			}
			packet.ReadInt();
			int num2 = packet.ReadInt();
			string text2;
			if (!num)
			{
				ServerPackets serverPackets = (ServerPackets)num2;
				text2 = serverPackets.ToString();
			}
			else
			{
				ClientPackets clientPackets = (ClientPackets)num2;
				text2 = clientPackets.ToString();
			}
			text = text2;
		}
		catch (Exception ex)
		{
			text = "decode-" + ex.GetType().Name;
		}
		MelonLogger.Msg("[Steam wire] " + (incoming ? "RX" : "TX") + " packet=" + text + " bytes=" + data.Length + " sent=" + sent);
		if (!incoming && !sent)
		{
			MelonLogger.Warning("[Steam wire] Send rejected. " + Session(peer.ToString()));
		}
	}
}
