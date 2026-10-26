using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Steamworks;
using UnityEngine;

namespace SkyCoop;

public static class CoopReturnToMenu
{
	[HarmonyPatch(typeof(MyMod), "DoKickMessage")]
	private static class HostReturnReceived
	{
		private static bool Prefix(string txt)
		{
			if (txt != "COOP_RETURN_MENU: Host is returning to the main menu.")
			{
				return true;
			}
			Request();
			return false;
		}
	}

	[HarmonyPatch(typeof(MyMod), "SimUDPHandle")]
	private static class IgnorePacketsAfterHostExit
	{
		private static bool Prefix()
		{
			if (MyMod.iAmHost)
			{
				return !unloading;
			}
			return false;
		}
	}

	private const string HostReturnMessage = "COOP_RETURN_MENU: Host is returning to the main menu.";

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	private static readonly HashSet<int> WaitingPeers = new HashSet<int>();

	private static bool pending;

	private static bool wasHost;

	private static bool localSaved;

	private static bool seenCoopWorld;

	private static bool unloading;

	private static double containerDeadline;

	private static double peerDeadline;

	private static double nextCloseAttempt;

	private static double noticeUntil;

	private static string stage = "idle";

	private static string status = "";

	private static string lastNotice = "";

	public static bool InProgress => pending;

	public static bool HostIsClosing
	{
		get
		{
			if (pending)
			{
				return wasHost;
			}
			return false;
		}
	}

	public static string Status => status;

	private static bool IsWorld
	{
		get
		{
			if (!string.IsNullOrEmpty(MyMod.level_name) && MyMod.level_name != "Boot" && MyMod.level_name != "Empty")
			{
				return !Shared.IsMainMenuScene(MyMod.level_name);
			}
			return false;
		}
	}

	public static bool CanRequest
	{
		get
		{
			if (IsWorld && (MyMod.InOnline() || seenCoopWorld) && !MyMod.DedicatedServerAppMode)
			{
				return !MyMod.IReallyWantToQuit;
			}
			return false;
		}
	}

	public static object Snapshot()
	{
		return new
		{
			pending = pending,
			stage = stage,
			status = status,
			wasHost = wasHost,
			localSaved = localSaved,
			unloading = unloading,
			canRequest = CanRequest,
			waitingPeers = new List<int>(WaitingPeers),
			savePending = MyMod.SaveExitPending,
			lastNotice = lastNotice
		};
	}

	public static bool Request()
	{
		if (pending || !CanRequest)
		{
			return false;
		}
		if (MyMod.SaveExitPending)
		{
			status = "Дождитесь завершения текущего сохранения";
			return false;
		}
		SleepRendezvousFix.CancelWaiting(explain: false);
		pending = true;
		wasHost = MyMod.iAmHost;
		localSaved = false;
		stage = "closing-container";
		status = "Подготовка выхода в главное меню…";
		containerDeadline = Clock.Elapsed.TotalSeconds + 20.0;
		nextCloseAttempt = 0.0;
		lastNotice = "";
		WaitingPeers.Clear();
		MyMod.DiscardRepeatPacket();
		CoopContainerFlow.FinishSearch();
		MelonLogger.Msg("[ReturnMenu] Requested role=" + (wasHost ? "host" : "guest"));
		return true;
	}

	public static void Tick()
	{
		if (MyMod.InOnline() && IsWorld)
		{
			seenCoopWorld = true;
		}
		if (unloading && Shared.IsMainMenuScene(MyMod.level_name))
		{
			unloading = false;
			pending = false;
			seenCoopWorld = false;
			stage = "menu";
			status = "";
			MyMod.TryResetSaveExitForNewSession();
			ClosePauseMenu();
			MelonLogger.Msg("[ReturnMenu] Main menu ready; process retained");
		}
		else
		{
			if (!pending || unloading)
			{
				return;
			}
			try
			{
				if (stage == "closing-container")
				{
					if (CoopContainerFlow.HasActiveSearch)
					{
						status = "Завершение обыска…";
						CoopContainerFlow.FinishSearch();
						if (Clock.Elapsed.TotalSeconds >= containerDeadline)
						{
							Fail("Не удалось завершить обыск. Выход отложен, чтобы сохранить найденные вещи.");
						}
						return;
					}
					Panel_Container panel_Container = GameCompat.Panel<Panel_Container>();
					if (ContainerLootSync.HasPendingUpload || (panel_Container != null && panel_Container.isActiveAndEnabled))
					{
						status = (ContainerLootSync.HasPendingUpload ? "Сохранение находок…" : "Завершение обыска…");
						if (!ContainerLootSync.HasPendingUpload && panel_Container != null && panel_Container.isActiveAndEnabled && Clock.Elapsed.TotalSeconds >= nextCloseAttempt)
						{
							nextCloseAttempt = Clock.Elapsed.TotalSeconds + 0.2;
							panel_Container.OnDone();
						}
						if (Clock.Elapsed.TotalSeconds >= containerDeadline)
						{
							Fail("Не удалось завершить обыск. Выход отложен, чтобы сохранить найденные вещи.");
						}
					}
					else if (MyMod.GoingToOpenContinaer != null)
					{
						CoopContainerFlow.FinishSearch();
						if (Clock.Elapsed.TotalSeconds >= containerDeadline)
						{
							Fail("Не удалось завершить обыск. Выход отложен, чтобы сохранить найденные вещи.");
						}
					}
					else
					{
						if (wasHost)
						{
							NotifyGuests();
						}
						stage = "saving";
						status = "Сохранение персонажа и мира…";
						MyMod.RequestSaveBeforeDisconnect(AfterLocalSave, allowOfflineWorld: true);
					}
				}
				else if (stage == "saving" && !MyMod.SaveExitPending && !localSaved)
				{
					Fail("Сохранение не завершилось. Вы остались в игре; попробуйте ещё раз.");
				}
				else
				{
					if (!(stage == "waiting-peers"))
					{
						return;
					}
					WaitingPeers.RemoveWhere((int id) => !Server.clients.ContainsKey(id) || !Server.clients[id].IsBusy() || Server.clients[id].TimeOutTime > MyMod.TimeOutSecondsForLoaders);
					if (WaitingPeers.Count == 0 || Clock.Elapsed.TotalSeconds >= peerDeadline)
					{
						if (WaitingPeers.Count != 0)
						{
							lastNotice = "Хост вышел. Сохранение не ответивших игроков не подтверждено.";
							noticeUntil = Clock.Elapsed.TotalSeconds + 12.0;
							MelonLogger.Warning("[ReturnMenu] Ending host after local checkpoint; unconfirmed guests=" + WaitingPeers.Count);
						}
						LoadMenuAfterCheckpoint();
					}
					else
					{
						status = "Ваш мир сохранён. Ожидание сохранения игроков: " + WaitingPeers.Count;
					}
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[ReturnMenu] " + ex);
				Fail("Не удалось выйти в меню: " + ex.GetType().Name);
			}
		}
	}

	// The host closing the game (QUIT GAME, window closed) used to leave guests in a world without a server.
	public static void NotifyGuestsHostLeaving()
	{
		if (!MyMod.iAmHost)
		{
			return;
		}
		try
		{
			foreach (KeyValuePair<int, Client> client in Server.clients)
			{
				if (client.Key > 0 && client.Value != null && !client.Value.RCON && client.Value.IsBusy())
				{
					ServerSend.KICKMESSAGE(client.Key, "COOP_RETURN_MENU: Host is returning to the main menu.");
				}
			}
			MelonLogger.Msg("[ReturnMenu] Guests told that the host is leaving");
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[ReturnMenu] Notifying guests: " + ex.Message);
		}
	}

	// Guest side: the host left the Steam lobby without the return message (crash, killed process, lost connection).
	public static void HostLeft()
	{
		if (MyMod.iAmHost || pending)
		{
			return;
		}
		MelonLogger.Msg("[ReturnMenu] Host left the session");
		lastNotice = "Хост вышел из игры. Ваш персонаж сохранён.";
		noticeUntil = Clock.Elapsed.TotalSeconds + 15.0;
		if (!Request())
		{
			MelonLogger.Warning("[ReturnMenu] Could not start return after host left; canRequest=" + CanRequest);
		}
		else
		{
			lastNotice = "Хост вышел из игры. Ваш персонаж сохранён.";
		}
	}

	private static void NotifyGuests()
	{
		peerDeadline = Clock.Elapsed.TotalSeconds + 15.0;
		foreach (KeyValuePair<int, Client> client in Server.clients)
		{
			if (client.Key > 0 && client.Value != null && !client.Value.RCON && client.Value.IsBusy())
			{
				WaitingPeers.Add(client.Key);
				ServerSend.KICKMESSAGE(client.Key, "COOP_RETURN_MENU: Host is returning to the main menu.");
			}
		}
		if (SteamConnect.CanUseSteam && !string.IsNullOrEmpty(MyMod.MyLobby))
		{
			try
			{
				SteamMatchmaking.SetLobbyJoinable(new CSteamID(ulong.Parse(MyMod.MyLobby)), bLobbyJoinable: false);
				SteamConnect.Main.SetLobbyState("Closing");
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[ReturnMenu] Lobby closing metadata: " + ex.Message);
			}
		}
	}

	private static void AfterLocalSave()
	{
		localSaved = true;
		MelonLogger.Msg("[ReturnMenu] Local checkpoint committed");
		if (wasHost && WaitingPeers.Count != 0)
		{
			stage = "waiting-peers";
			status = "Ожидание сохранения остальных игроков…";
		}
		else
		{
			LoadMenuAfterCheckpoint();
		}
	}

	private static void LoadMenuAfterCheckpoint()
	{
		if (!localSaved)
		{
			throw new InvalidOperationException("Return-to-menu checkpoint was not committed");
		}
		unloading = true;
		stage = "loading-menu";
		status = "Загрузка главного меню…";
		MyMod.iAmHost = false;
		MyMod.sendMyPosition = false;
		MyMod.ConnectedSteamWorks = false;
		MyMod.SteamServerWorks = "";
		MyMod.NeedSyncTime = false;
		MyMod.isRuning = false;
		MyMod.StartServerAfterSelectSave = false;
		SteamGameJoin.Suspend();
		foreach (KeyValuePair<int, Client> client in Server.clients)
		{
			Client value = client.Value;
			if (value == null || value.udp == null)
			{
				continue;
			}
			string sid = value.udp.sid;
			if (SteamConnect.CanUseSteam && !string.IsNullOrEmpty(sid))
			{
				try
				{
					SteamConnect.Main.Disconnect(sid);
				}
				catch (Exception ex)
				{
					MelonLogger.Warning("[ReturnMenu] Closing peer: " + ex.Message);
				}
			}
			value.udp.Disconnect();
		}
		if (Server.udpListener != null)
		{
			Server.udpListener.Close();
			Server.udpListener = null;
		}
		Server.UsingSteamWorks = false;
		Server.p2pclients.Clear();
		Server.clients.Clear();
		Server.packetHandlers = null;
		lock (ThreadManager.executeOnMainThread)
		{
			ThreadManager.executeOnMainThread.Clear();
			ThreadManager.actionToExecuteOnMainThread = false;
		}
		ThreadManager.executeCopiedOnMainThread.Clear();
		if (ClientUser.udp != null && ClientUser.udp.socket != null)
		{
			ClientUser.udp.socket.Close();
			ClientUser.udp.socket = null;
		}
		ClientUser.myId = 0;
		ClientUser.packetHandlers = null;
		LeaveLobby();
		MyMod.MyContainer = null;
		MyMod.GoingToOpenContinaer = null;
		MyMod.WildlifeAuthorityScene = "";
		MyMod.AnimalsController = false;
		MyMod.MyCycleSkip = 0;
		MyMod.IsSleeping = false;
		MyMod.PlayersOnServer = 0;
		MyMod.DiscardRepeatPacket();
		MyMod.RemovePleaseWait();
		MyMod.SlicedJsonDataBuffer.Clear();
		CoopContainerFlow.Reset();
		foreach (GameObject player in MyMod.players)
		{
			if (player != null)
			{
				player.SetActive(value: false);
			}
		}
		MyMod.playersData.Clear();
		MyMod.ActorsList.Clear();
		MyMod.AnimalCorplesList.Clear();
		MyMod.DroppedGearsObjs.Clear();
		MyMod.TrackableDroppedGearsObjs.Clear();
		MyMod.OpenablesObjs.Clear();
		MyMod.OpenableThings.Clear();
		SleepRendezvousFix.Reset();
		if (MyMod.LobbyUI != null)
		{
			MyMod.LobbyUI.SetActive(value: false);
		}
		if (MyMod.StatusObject != null)
		{
			MyMod.StatusObject.SetActive(value: false);
		}
		if (MyMod.ChatObject != null)
		{
			MyMod.ChatObject.SetActive(value: false);
		}
		if (MyMod.chatInput != null)
		{
			MyMod.chatInput.gameObject.SetActive(value: false);
		}
		if (MyMod.EmoteWheel != null)
		{
			MyMod.EmoteWheel.SetActive(value: false);
		}
		if (MyMod.MicrophoneIdicator != null)
		{
			MyMod.MicrophoneIdicator.SetActive(value: false);
		}
		if (MyMod.RadioIdicator != null)
		{
			MyMod.RadioIdicator.SetActive(value: false);
		}
		MenuChange.MenuMode = "Original";
		ClosePauseMenu();
		MelonLogger.Msg("[ReturnMenu] Loading native main menu after checkpoint");
		GameManager.DoExitToMainMenu();
	}

	// Panels survive the scene change; the pause menu the exit was started from stayed drawn over the main menu.
	private static void ClosePauseMenu()
	{
		try
		{
			Panel_PauseMenu panel = GameCompat.Panel<Panel_PauseMenu>();
			if (panel != null && panel.isActiveAndEnabled)
			{
				panel.Enable(enable: false);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[ReturnMenu] Closing pause menu: " + ex.Message);
		}
	}

	internal static void LeaveLobby()
	{
		if (SteamConnect.CanUseSteam && !string.IsNullOrEmpty(MyMod.MyLobby))
		{
			try
			{
				SteamMatchmaking.LeaveLobby(new CSteamID(ulong.Parse(MyMod.MyLobby)));
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[ReturnMenu] Leave lobby: " + ex.Message);
			}
		}
		MyMod.MyLobby = "";
		SteamConnect.IsMyLobby = false;
	}

	private static void Fail(string text)
	{
		pending = false;
		unloading = false;
		stage = "failed";
		status = text;
		MelonLogger.Warning("[ReturnMenu] " + text);
	}

	public static void Draw()
	{
		if (Shared.IsMainMenuScene(MyMod.level_name))
		{
			if (!string.IsNullOrEmpty(lastNotice) && Clock.Elapsed.TotalSeconds < noticeUntil)
			{
				GUI.Box(new Rect((float)Screen.width / 2f - 300f, Screen.height - 75, 600f, 55f), lastNotice);
			}
			return;
		}
		Panel_PauseMenu panel_PauseMenu = GameCompat.Panel<Panel_PauseMenu>();
		bool flag = CoopContainerFlow.HasActiveSearch || MyMod.GoingToOpenContinaer != null;
		bool pauseOpen = panel_PauseMenu != null && panel_PauseMenu.isActiveAndEnabled;
		if (!CanRequest || (!pending && !flag && !pauseOpen))
		{
			return;
		}
		int depth = GUI.depth;
		GUI.depth = -10000;
		// The pause menu has its own "QUIT TO MAIN MENU" item; this button stays for exiting during a container search.
		if (!pauseOpen && !pending)
		{
			bool enabled = GUI.enabled;
			GUI.enabled = !MyMod.SaveExitPending;
			if (GUI.Button(new Rect((float)Screen.width / 2f - 190f, Screen.height - 85, 380f, 42f), "Выйти в главное меню"))
			{
				Request();
			}
			GUI.enabled = enabled;
		}
		string text = status;
		if (string.IsNullOrEmpty(text) && pauseOpen)
		{
			text = MyMod.iAmHost ? "Выход в меню закроет сервер для всех игроков" : "Перед выходом в меню персонаж будет сохранён";
		}
		if (!string.IsNullOrEmpty(text))
		{
			GUI.Box(new Rect((float)Screen.width / 2f - 300f, Screen.height - 135, 600f, 45f), text);
		}
		GUI.depth = depth;
	}
}
