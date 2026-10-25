using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using GameServer;
using Il2Cpp;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoop;

internal class SteamConnect
{
	public class Main
	{
		public class SteamLobbyElement
		{
			public CSteamID m_LobbyID;

			public CSteamID m_Owner;

			public string m_OwnerName;

			public int m_Players;

			public int m_PlayersMax;

			public string m_State = "";
		}

		private static Callback<P2PSessionRequest_t> sessionRequest;

		private static Callback<P2PSessionConnectFail_t> sessionFailure;

		private static Callback<GameRichPresenceJoinRequested_t> _GameRichPresenceJoinRequested_t;

		private static Callback<GameLobbyJoinRequested_t> _GameLobbyJoinRequested_t;

		private static Callback<LobbyEnter_t> _LobbyEnter_t;

		private static CallResult<LobbyCreated_t> OnLobbyCreatedCallResult;

		private static CallResult<LobbyMatchList_t> OnLobbyMatchListCallResult;

		private static Callback<LobbyGameCreated_t> _LobbyGameCreated_t;

		private static Callback<LobbyChatUpdate_t> _LobbyChatUpdate_t;

		public static List<CSteamID> FoundServers = new List<CSteamID>();

		public static Dictionary<CSteamID, SteamLobbyElement> ServersData = new Dictionary<CSteamID, SteamLobbyElement>();

		public static List<GameObject> ServerObjects = new List<GameObject>();

		public static bool VoiceDebug = false;

		public static bool VoiceRadioDebug = false;

		public static int StartRecordTime = 0;

		private static int LastLobbyType = 0;

		private static int LastLobbyLimit = 0;

		public static string JoinStatus => SteamGameJoin.Status;

		public static bool JoinNeedsWorldData => SteamGameJoin.NeedsWorldData;

		public static bool TryJoinLobbyGame(bool manual)
		{
			return SteamGameJoin.TryJoin(manual);
		}

		public static void Run()
		{
			sessionRequest = Callback<P2PSessionRequest_t>.Create(SteamGameJoin.OnSessionRequest);
			sessionFailure = Callback<P2PSessionConnectFail_t>.Create(SteamGameJoin.OnSessionFailure);
			SteamNetworking.AllowP2PPacketRelay(bAllow: true);
			_GameRichPresenceJoinRequested_t = Callback<GameRichPresenceJoinRequested_t>.Create(OnP2PSessionAccept);
			_LobbyEnter_t = Callback<LobbyEnter_t>.Create(OnEnterLobby);
			_GameLobbyJoinRequested_t = Callback<GameLobbyJoinRequested_t>.Create(ShouldJoinLobby);
			OnLobbyCreatedCallResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
			_LobbyGameCreated_t = Callback<LobbyGameCreated_t>.Create(OnLobbyServerCreated);
			_LobbyChatUpdate_t = Callback<LobbyChatUpdate_t>.Create(OnLobbyPlayersUpdate);
			OnLobbyMatchListCallResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
			string personaName = SteamFriends.GetPersonaName();
			string text = SteamUser.GetSteamID().ToString();
			MelonLogger.Msg("[SteamWorks.NET] Logins as " + personaName + " SteamID " + text);
			if (text == "76561198613952355")
			{
				MyMod.LoadChatName("Hello, I am pirate");
			}
			else
			{
				MyMod.LoadChatName(personaName);
			}
			Supporters.SetID(text);
		}

		public static void OnP2PSessionAccept(GameRichPresenceJoinRequested_t request)
		{
			if (!Shared.IsMainMenuScene(MyMod.level_name))
			{
				return;
			}
			MelonLogger.Msg("[SteamWorks.NET] Got accpeted invite message");
			if (request.m_rgchConnect != null)
			{
				MelonLogger.Msg("[SteamWorks.NET] m_rgchConnect " + request.m_rgchConnect);
				if (request.m_rgchConnect.Contains("+connect_lobby"))
				{
					JoinToLobby(request.m_rgchConnect.Replace("+connect_lobby ", ""));
				}
			}
			else
			{
				CSteamID steamIDFriend = request.m_steamIDFriend;
				string steamServerWorks = steamIDFriend.ToString();
				MyMod.ConnectedSteamWorks = true;
				MyMod.SteamServerWorks = steamServerWorks;
				MyMod.DoWaitForConnect(Steam: true);
				ConnectToHost(MyMod.SteamServerWorks);
			}
		}

		public static void ProcessLobbyUi()
		{
			MyMod.m_Panel_Sandbox.Enable(enable: true);
			MyMod.m_Panel_Sandbox.m_TitleLocalizationId = "MULTIPLAYER";
			MyMod.m_Panel_Sandbox.m_BasicMenu.UpdateTitle("MULTIPLAYER");
			MenuChange.ChangeMenuItems("Lobby");
		}

		public static void OnEnterLobby(LobbyEnter_t request)
		{
			MyMod.RemovePleaseWait();
			if (request.m_EChatRoomEnterResponse == 1)
			{
				Transform parent = GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, 0, 0, 5);
				GameCompat.SetChildActive(parent, 1, state: true);
				GameCompat.SetChildActive(parent, 2, state: true);
				GameCompat.SetChildActive(parent, 4, state: true);
				GameCompat.SetChildActive(parent, 5, state: true);
				if (MyMod.ServerBrowser != null)
				{
					MyMod.ServerBrowser.SetActive(value: false);
				}
				MelonLogger.Msg("[SteamWorks.NET] Jointed to Lobby " + request.m_ulSteamIDLobby);
				MyMod.MyLobby = request.m_ulSteamIDLobby.ToString();
				if (SteamMatchmaking.GetLobbyOwner(new CSteamID(request.m_ulSteamIDLobby)) != SteamUser.GetSteamID())
				{
					SteamFriends.SetRichPresence("steam_player_group", request.m_ulSteamIDLobby.ToString());
					SteamFriends.SetRichPresence("steam_player_group_size", SteamMatchmaking.GetNumLobbyMembers(new CSteamID(request.m_ulSteamIDLobby)).ToString());
					ulong steamID = ulong.Parse(SteamUser.GetSteamID().ToString());
					if (!MyMod.LobbyContains(steamID))
					{
						int mediumFriendAvatar = SteamFriends.GetMediumFriendAvatar(SteamUser.GetSteamID());
						if (mediumFriendAvatar != 0)
						{
							Texture2D imageFromDescripter = GetImageFromDescripter(mediumFriendAvatar, 64, 64);
							MyMod.AddPersonToLobby(SteamFriends.GetPersonaName(), steamID, imageFromDescripter);
						}
						else
						{
							MelonLogger.Msg(ConsoleColor.Yellow, "[SteamWorks.NET] Player has not avatar ");
						}
					}
					int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(new CSteamID(request.m_ulSteamIDLobby));
					int lobbyMemberLimit = SteamMatchmaking.GetLobbyMemberLimit(new CSteamID(request.m_ulSteamIDLobby));
					SteamFriends.SetRichPresence("status", "Playing Online " + numLobbyMembers + "/" + lobbyMemberLimit);
					if (SteamMatchmaking.GetLobbyGameServer(new CSteamID(request.m_ulSteamIDLobby), out var _, out var _, out var psteamIDGameServer))
					{
						MelonLogger.Msg("[SteamWorks.NET] Going to connect to server of the lobby");
						SteamFriends.SetRichPresence("connect", "+connect_lobby " + request.m_ulSteamIDLobby);
						MyMod.ConnectedSteamWorks = true;
						MyMod.SteamServerWorks = psteamIDGameServer.ToString();
						MyMod.RemovePleaseWait();
						MyMod.DoWaitForConnect(Steam: true);
						ConnectToHost(MyMod.SteamServerWorks);
						if (MyMod.LobbyUI != null)
						{
							MyMod.LobbyUI.SetActive(value: false);
						}
					}
					else
					{
						MyMod.RemovePleaseWait();
						MelonLogger.Msg("[SteamWorks.NET] Server isn't started yet, processing lobby");
						ProcessLobbyUi();
					}
				}
			}
			else
			{
				MelonLogger.Msg(ConsoleColor.Red, "[SteamWorks.NET] Can't joing to lobby!");
				MyMod.RemovePleaseWait();
				if (MyMod.m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
				{
					GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "Can't join this lobby", "\nServer is no more available to join", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
				}
			}
			if (MyMod.DedicatedServerAppMode && IsMyLobby)
			{
				MyMod.FinishStartingDsServer();
			}
		}

		public static void ShouldJoinLobby(GameLobbyJoinRequested_t request)
		{
			CSteamID steamIDLobby = request.m_steamIDLobby;
			MelonLogger.Msg("[SteamWorks.NET] Trying to join to lobby from invite " + steamIDLobby.ToString());
			if (MyMod.SteamServerWorks == "" && MyMod.MyLobby == "" && !MyMod.iAmHost && !MyMod.sendMyPosition)
			{
				SteamMatchmaking.JoinLobby(request.m_steamIDLobby);
			}
			else if (MyMod.m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
			{
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "YOU ALREADY ON SERVER", "\nYou already on the server, restart the game if you want to join to another server", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
			}
		}

		public static Texture2D GetImageFromDescripter(int AvatarDescripter, int width, int height)
		{
			int num = width * height * 4;
			byte[] array = new byte[num];
			SteamUtils.GetImageRGBA(AvatarDescripter, array, num);
			Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: true);
			texture2D.LoadRawTextureData(array);
			texture2D.Apply();
			return texture2D;
		}

		public static void OnLobbyCreated(LobbyCreated_t pCallback, bool bIOFailure)
		{
			if (!bIOFailure)
			{
				if (pCallback.m_eResult == EResult.k_EResultOK)
				{
					MelonLogger.Msg("[SteamWorks.NET] Lobby created " + pCallback.m_ulSteamIDLobby);
					SteamFriends.SetRichPresence("steam_player_group", pCallback.m_ulSteamIDLobby.ToString());
					SteamFriends.SetRichPresence("steam_player_group_size", "1");
					MyMod.MyLobby = pCallback.m_ulSteamIDLobby.ToString();
					IsMyLobby = true;
					SetSpawnStyle();
					SetLobbyName();
				}
				else
				{
					MelonLogger.Msg(ConsoleColor.Red, "[SteamWorks.NET] Can't create lobby: Error " + pCallback.m_eResult);
					MelonLogger.Msg(ConsoleColor.Green, "[SteamWorks.NET] Going to try again in 5 seconds");
					MyMod.TryMakeLobbyAgain = 5;
				}
			}
			else
			{
				MelonLogger.Msg(ConsoleColor.Red, "[SteamWorks.NET] Can't create lobby: Error bIOFailure");
				MelonLogger.Msg(ConsoleColor.Green, "[SteamWorks.NET] Going to try again in 5 seconds");
				MyMod.TryMakeLobbyAgain = 5;
			}
			if (MyMod.DedicatedServerAppMode || !MyMod.LobbyUI)
			{
				return;
			}
			ulong steamID = ulong.Parse(SteamUser.GetSteamID().ToString());
			if (!MyMod.LobbyContains(steamID))
			{
				int mediumFriendAvatar = SteamFriends.GetMediumFriendAvatar(SteamUser.GetSteamID());
				if (mediumFriendAvatar != 0)
				{
					Texture2D imageFromDescripter = GetImageFromDescripter(mediumFriendAvatar, 64, 64);
					MyMod.AddPersonToLobby(SteamFriends.GetPersonaName(), steamID, imageFromDescripter);
				}
				else
				{
					MelonLogger.Msg(ConsoleColor.Yellow, "[SteamWorks.NET] Player has not avatar ");
				}
			}
		}

		public static string GetStateString(string State)
		{
			return State switch
			{
				"StartingGame" => "Starting game", 
				"SelectedSave" => "Going to load save", 
				"SelectedNewSave" => "Going to create new save", 
				_ => State, 
			};
		}

		public static void ClickJoin(CSteamID LobbyID)
		{
			MyMod.DoPleaseWait("Connecting", "Trying to join...");
			if (MyMod.SteamServerWorks == "" && MyMod.MyLobby == "" && !MyMod.iAmHost && !MyMod.sendMyPosition)
			{
				SteamMatchmaking.JoinLobby(LobbyID);
			}
		}

		public static void AddServerToList(SteamLobbyElement Data)
		{
			if (!(MyMod.ServerBrowser == null))
			{
				GameObject gameObject = MyMod.SpawnModAsset(MyMod.BundleAsset<GameObject>("MP_Server"), MyMod.ServerBrowser.transform.GetChild(1).GetChild(0).GetChild(0));
				Button component = gameObject.transform.GetChild(0).gameObject.GetComponent<Button>();
				Text component2 = gameObject.transform.GetChild(1).gameObject.GetComponent<Text>();
				Text component3 = gameObject.transform.GetChild(2).gameObject.GetComponent<Text>();
				Text component4 = gameObject.transform.GetChild(3).gameObject.GetComponent<Text>();
				component2.text = Data.m_OwnerName;
				component3.text = "Players: " + Data.m_Players + "/" + Data.m_PlayersMax;
				component4.text = "Status: " + Data.m_State;
				ServerObjects.Add(gameObject);
				Action action = delegate
				{
					ClickJoin(Data.m_LobbyID);
				};
				component.onClick.AddListener(action);
			}
		}

		public static void OnLobbyMatchList(LobbyMatchList_t pCallback, bool bIOFailure)
		{
			if (bIOFailure)
			{
				return;
			}
			MelonLogger.Msg("[SteamWorks.NET] " + pCallback.m_nLobbiesMatching + " games found");
			FoundServers = new List<CSteamID>();
			ServersData = new Dictionary<CSteamID, SteamLobbyElement>();
			if (ServerObjects.Count > 0)
			{
				for (int num = ServerObjects.Count; num > 0; num--)
				{
					GameObject obj = ServerObjects[num];
					ServerObjects.RemoveAt(num);
					UnityEngine.Object.Destroy(obj);
				}
			}
			if (pCallback.m_nLobbiesMatching == 0)
			{
				return;
			}
			for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
			{
				CSteamID lobbyByIndex = SteamMatchmaking.GetLobbyByIndex(i);
				FoundServers.Add(lobbyByIndex);
				SteamMatchmaking.RequestLobbyData(lobbyByIndex);
			}
			foreach (CSteamID foundServer in FoundServers)
			{
				AddServerToList(new SteamLobbyElement
				{
					m_LobbyID = foundServer,
					m_Owner = SteamMatchmaking.GetLobbyOwner(foundServer),
					m_Players = SteamMatchmaking.GetNumLobbyMembers(foundServer),
					m_PlayersMax = SteamMatchmaking.GetLobbyMemberLimit(foundServer),
					m_State = SteamMatchmaking.GetLobbyData(foundServer, "State"),
					m_OwnerName = SteamMatchmaking.GetLobbyData(foundServer, "Name")
				});
			}
		}

		public static void OnLobbyServerCreated(LobbyGameCreated_t request)
		{
			MelonLogger.Msg("[SteamWorks.NET] Lobby server set to " + request.m_ulSteamIDGameServer);
			int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(new CSteamID(request.m_ulSteamIDLobby));
			int lobbyMemberLimit = SteamMatchmaking.GetLobbyMemberLimit(new CSteamID(request.m_ulSteamIDLobby));
			SteamFriends.SetRichPresence("status", "Playing Online " + numLobbyMembers + "/" + lobbyMemberLimit);
			SteamFriends.SetRichPresence("connect", "+connect_lobby " + request.m_ulSteamIDLobby);
			if (SteamMatchmaking.GetLobbyOwner(new CSteamID(request.m_ulSteamIDLobby)) != SteamUser.GetSteamID())
			{
				MenuChange.ChangeMenuItems("Original");
				MyMod.ConnectedSteamWorks = true;
				MyMod.SteamServerWorks = request.m_ulSteamIDGameServer.ToString();
				MyMod.DoWaitForConnect(Steam: true);
				ConnectToHost(MyMod.SteamServerWorks);
			}
		}

		public static void OnLobbyPlayersUpdate(LobbyChatUpdate_t request)
		{
			if (request.m_rgfChatMemberStateChange == 1)
			{
				MelonLogger.Msg("[SteamWorks.NET] Played joined lobby " + request.m_ulSteamIDUserChanged);
				if (SteamMatchmaking.GetLobbyOwner(new CSteamID(request.m_ulSteamIDLobby)) == SteamUser.GetSteamID())
				{
					SteamNetworking.AcceptP2PSessionWithUser(new CSteamID(request.m_ulSteamIDUserChanged));
				}
			}
			else
			{
				MelonLogger.Msg("[SteamWorks.NET] Player disconnected from lobby " + request.m_ulSteamIDUserChanged);
				if (SteamMatchmaking.GetLobbyOwner(new CSteamID(request.m_ulSteamIDLobby)) == SteamUser.GetSteamID())
				{
					SteamNetworking.CloseP2PSessionWithUser(new CSteamID(request.m_ulSteamIDUserChanged));
				}
				if ((bool)MyMod.LobbyUI)
				{
					MyMod.RemovePersonFromLobby(request.m_ulSteamIDUserChanged);
				}
			}
			SteamFriends.SetRichPresence("steam_player_group", request.m_ulSteamIDLobby.ToString());
			SteamFriends.SetRichPresence("steam_player_group_size", SteamMatchmaking.GetNumLobbyMembers(new CSteamID(request.m_ulSteamIDLobby)).ToString());
			int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(new CSteamID(request.m_ulSteamIDLobby));
			int lobbyMemberLimit = SteamMatchmaking.GetLobbyMemberLimit(new CSteamID(request.m_ulSteamIDLobby));
			SteamFriends.SetRichPresence("status", "Playing Online " + numLobbyMembers + "/" + lobbyMemberLimit);
		}

		public static void TestMyVoice(byte[] DestBuffer, uint BytesWritten, float RecordTime)
		{
			MyMod.ProcessVoiceChatData(0, DestBuffer, BytesWritten, RecordTime, VoiceRadioDebug);
		}

		public static byte[] DecompressVoice(byte[] DestBuffer, uint BytesWritten)
		{
			byte[] array = new byte[44100];
			if (SteamUser.DecompressVoice(DestBuffer, BytesWritten, array, (uint)array.Length, out var nBytesWritten, 22050u) == EVoiceResult.k_EVoiceResultOK && nBytesWritten != 0)
			{
				return array;
			}
			return null;
		}

		public static void VoiceUpdate()
		{
			if (MyMod.DoingRecord || MyMod.RecordReleseButtonHold > Time.time)
			{
				SteamUser.StartVoiceRecording();
			}
			else
			{
				SteamUser.StopVoiceRecording();
			}
			if ((!MyMod.DoingRecord && !(MyMod.RecordReleseButtonHold > Time.time)) || SteamUser.GetAvailableVoice(out var pcbCompressed) != EVoiceResult.k_EVoiceResultOK || pcbCompressed <= 1024)
			{
				return;
			}
			byte[] array = new byte[1024];
			if (SteamUser.GetVoice(bWantCompressed: true, array, 1024u, out var nBytesWritten) == EVoiceResult.k_EVoiceResultOK && nBytesWritten != 0)
			{
				float recordStartTime = MyMod.RecordStartTime;
				float recordTime = (MyMod.RecordStartTime = Time.time) - recordStartTime;
				if (VoiceDebug)
				{
					MelonLogger.Msg("DestBuffer[" + nBytesWritten + "]");
					MelonLogger.Msg("Been recoding " + recordTime);
					TestMyVoice(array, nBytesWritten, recordTime);
				}
				else
				{
					MyMod.SendMyVoice(array, int.Parse(nBytesWritten.ToString()), recordTime);
				}
			}
		}

		public static void OnLobbyStateChanged()
		{
			if (MyMod.LobbyUI.activeSelf && MenuChange.MenuMode == "Lobby")
			{
				MenuChange.ChangeMenuItems("Lobby");
			}
			MelonLogger.Msg("LobbyState " + MyMod.LobbyState);
		}

		public static void OnSaveSlotChanged()
		{
			if (MyMod.LobbyUI.activeSelf && MenuChange.MenuMode == "Lobby")
			{
				MenuChange.ChangeMenuItems("Lobby");
			}
		}

		public static void MayUpdatePlayerInLobby(ulong playerID)
		{
			if (MyMod.DedicatedServerAppMode)
			{
				return;
			}
			if (!MyMod.LobbyContains(playerID))
			{
				int mediumFriendAvatar = SteamFriends.GetMediumFriendAvatar(new CSteamID(playerID));
				if (mediumFriendAvatar != 0)
				{
					Texture2D imageFromDescripter = GetImageFromDescripter(mediumFriendAvatar, 64, 64);
					MyMod.AddPersonToLobby(SteamFriends.GetFriendPersonaName(new CSteamID(playerID)), playerID, imageFromDescripter);
				}
			}
			else
			{
				MyMod.SetPersonNameFromLobby(playerID, SteamFriends.GetFriendPersonaName(new CSteamID(playerID)));
			}
		}

		public static void OnRegularUpdate()
		{
			if ((!Shared.IsMainMenuScene(MyMod.level_name) && MyMod.level_name != "Empty" && MyMod.level_name != "Boot") || MyMod.DedicatedServerAppMode || !(MyMod.MyLobby != ""))
			{
				return;
			}
			CSteamID steamIDLobby = new CSteamID(ulong.Parse(MyMod.MyLobby));
			int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(steamIDLobby);
			int lobbyMemberLimit = SteamMatchmaking.GetLobbyMemberLimit(steamIDLobby);
			if (!IsMyLobby)
			{
				string lobbyData = SteamMatchmaking.GetLobbyData(steamIDLobby, "RegionID");
				if (lobbyData != null && lobbyData != "")
				{
					int num = int.Parse(lobbyData);
					if (num != MyMod.LobbyStartingRegion)
					{
						MyMod.LobbyStartingRegion = num;
						OnSaveSlotChanged();
					}
				}
				string lobbyData2 = SteamMatchmaking.GetLobbyData(steamIDLobby, "ExpID");
				if (lobbyData2 != null && lobbyData2 != "")
				{
					int num2 = int.Parse(lobbyData2);
					if (num2 != MyMod.LobbyStartingExperience)
					{
						MyMod.LobbyStartingExperience = num2;
						OnSaveSlotChanged();
					}
				}
				string lobbyData3 = SteamMatchmaking.GetLobbyData(steamIDLobby, "State");
				_ = MyMod.LobbyState;
				if (lobbyData3 != null && lobbyData3 != "")
				{
					SetLobbyState(lobbyData3);
				}
				string lobbyData4 = SteamMatchmaking.GetLobbyData(steamIDLobby, "SpawnStyle");
				if (lobbyData4 != null && lobbyData4 != "")
				{
					MyMod.ServerConfig.m_PlayersSpawnType = int.Parse(lobbyData4);
				}
				else
				{
					MyMod.ServerConfig.m_PlayersSpawnType = 0;
				}
				string lobbyData5 = SteamMatchmaking.GetLobbyData(steamIDLobby, "SaveSeed");
				if (lobbyData5 != null && lobbyData5 != "")
				{
					int num3 = int.Parse(lobbyData5);
					if (MyMod.SelectedSaveSeed != num3)
					{
						MyMod.SelectedSaveSeed = num3;
						OnSaveSlotChanged();
					}
				}
				else if (MyMod.SelectedSaveSeed != 0)
				{
					MyMod.SelectedSaveSeed = 0;
					OnSaveSlotChanged();
				}
			}
			for (int i = 0; i < numLobbyMembers; i++)
			{
				MayUpdatePlayerInLobby(ulong.Parse(SteamMatchmaking.GetLobbyMemberByIndex(steamIDLobby, i).ToString()));
			}
			if (MyMod.LobbyUI != null)
			{
				MyMod.LobbyUI.transform.GetChild(2).GetComponent<Text>().text = "LOBBY " + numLobbyMembers + "/" + lobbyMemberLimit;
			}
		}

		public static void MakeLobby(int type, int limit)
		{
			LastLobbyType = type;
			LastLobbyLimit = limit;
			ELobbyType eLobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
			eLobbyType = type switch
			{
				0 => ELobbyType.k_ELobbyTypePrivate, 
				1 => ELobbyType.k_ELobbyTypeFriendsOnly, 
				2 => ELobbyType.k_ELobbyTypePublic, 
				_ => ELobbyType.k_ELobbyTypeFriendsOnly, 
			};
			IsMyLobby = true;
			MyMod.TryMakeLobbyAgain = 5;
			MelonLogger.Msg("[SteamWorks.NET] Creating new lobby");
			SteamAPICall_t hAPICall = SteamMatchmaking.CreateLobby(eLobbyType, limit);
			OnLobbyCreatedCallResult.Set(hAPICall);
		}

		public static void MakeLobby()
		{
			MelonLogger.Msg("[SteamWorks.NET] Retrying to creating new lobby");
			ELobbyType eLobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
			eLobbyType = LastLobbyType switch
			{
				0 => ELobbyType.k_ELobbyTypePrivate, 
				1 => ELobbyType.k_ELobbyTypeFriendsOnly, 
				2 => ELobbyType.k_ELobbyTypePublic, 
				_ => ELobbyType.k_ELobbyTypeFriendsOnly, 
			};
			MyMod.TryMakeLobbyAgain = 5;
			MelonLogger.Msg("[SteamWorks.NET] Creating new lobby");
			SteamAPICall_t hAPICall = SteamMatchmaking.CreateLobby(eLobbyType, LastLobbyLimit);
			OnLobbyCreatedCallResult.Set(hAPICall);
		}

		public static string GetFriends()
		{
			string text = "";
			int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
			for (int i = 0; i < friendCount; i++)
			{
				CSteamID friendByIndex = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
				text = text + i + ". " + SteamFriends.GetFriendPersonaName(friendByIndex) + "\n";
			}
			return text;
		}

		public static void ProcessWhitelist(string[] whitelist)
		{
			for (int i = 0; i < whitelist.Length; i++)
			{
				ulong ulSteamID = Convert.ToUInt64(whitelist[i]);
				SteamNetworking.AcceptP2PSessionWithUser(new CSteamID(ulSteamID));
				MelonLogger.Msg(ConsoleColor.Blue, "[Dedicated server] Adding user with SID " + ulSteamID + " to the whitelist!");
			}
		}

		public static void LoadSaveForHosting()
		{
			MenuChange.ChangeMenuItems("Original");
			MyMod.HostFromLobbyAfterLoader = true;
			MyMod.ForceLoadSlotByName(MyMod.SelectedSaveName);
		}

		public static void CreateSaveForHosting()
		{
			MenuChange.ChangeMenuItems("Original");
			MyMod.HostFromLobbyAfterLoader = true;
			MyMod.ForceCreateSlotForPlaying(MyMod.LobbyStartingRegion, MyMod.LobbyStartingExperience);
		}

		public static void MakeFriendListUI()
		{
			if (MyMod.MyLobby != "")
			{
				SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(ulong.Parse(MyMod.MyLobby)));
			}
		}

		public static void OpenFriends()
		{
			SteamFriends.ActivateGameOverlay("friends");
		}

		public static void CopyInviteLink()
		{
			GUIUtility.systemCopyBuffer = "steam://joinlobby/305620/" + MyMod.MyLobby + "/" + SteamUser.GetSteamID().ToString();
		}

		public static void SetSaveSlotSeed(int SaveSeed)
		{
			if (MyMod.MyLobby != "")
			{
				SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "SaveSeed", SaveSeed.ToString());
			}
			else
			{
				SteamMatchmaking.DeleteLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "SaveSeed");
			}
		}

		public static bool IsLobbyOwner()
		{
			if (MyMod.MyLobby != "")
			{
				return false;
			}
			if (SteamMatchmaking.GetLobbyOwner(new CSteamID(ulong.Parse(MyMod.MyLobby))) != SteamUser.GetSteamID())
			{
				return false;
			}
			return true;
		}

		public static void SetSpawnStyle()
		{
			SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "SpawnStyle", MyMod.ServerConfig.m_PlayersSpawnType.ToString());
		}

		public static void SetLobbyName(string CustomName = "")
		{
			if (CustomName == "")
			{
				SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "Name", SteamFriends.GetPersonaName());
				SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "State", "WaitForPlayers");
			}
			else
			{
				SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "Name", CustomName);
			}
		}

		public static void SetLobbyServer()
		{
			SteamMatchmaking.SetLobbyGameServer(new CSteamID(ulong.Parse(MyMod.MyLobby)), 0u, 0, SteamUser.GetSteamID());
		}

		public static void SetLobbyState(string s)
		{
			if (s == "Vote")
			{
				s = "WaitForPlayers";
			}
			if (s != "")
			{
				if (IsMyLobby)
				{
					SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "State", s);
				}
				if (MyMod.LobbyState != s)
				{
					MyMod.LobbyState = s;
					OnLobbyStateChanged();
				}
			}
			else
			{
				if (IsMyLobby)
				{
					SteamMatchmaking.DeleteLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "State");
				}
				MyMod.LobbyState = "";
			}
		}

		public static void SetNewGameSettings(int RegionID, int ExpID)
		{
			SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "RegionID", RegionID.ToString());
			SteamMatchmaking.SetLobbyData(new CSteamID(ulong.Parse(MyMod.MyLobby)), "ExpID", ExpID.ToString());
		}

		public static void ConnectToHost(string hostid)
		{
			if (MyMod.TryResetSaveExitForNewSession() && SteamGameJoin.Begin(hostid))
			{
				MyMod.ConnectedSteamWorks = true;
				MyMod.SteamServerWorks = hostid;
				ClientUser.SubNetworkClientGUID = MPSaveManager.GetSubNetworkGUID();
				if ((bool)MyMod.m_Panel_Sandbox && MyMod.m_Panel_Sandbox.isActiveAndEnabled)
				{
					MyMod.m_Panel_Sandbox.Enable(enable: false);
				}
				if ((bool)MyMod.ServerBrowser && MyMod.ServerBrowser != null)
				{
					MyMod.ServerBrowser.SetActive(value: false);
				}
				if ((bool)MyMod.LobbyUI && MyMod.LobbyUI != null)
				{
					MyMod.LobbyUI.SetActive(value: false);
				}
				ClientUser.myId = 0;
				CSteamID steamIDRemote = new CSteamID(ulong.Parse(hostid));
				ClientUser.InitializeClientData();
				SteamNetworking.AcceptP2PSessionWithUser(steamIDRemote);
				MelonLogger.Msg("[SteamWorks.NET] Trying connecting to " + hostid);
				MyMod.DoSteamWorksConnect(SteamUser.GetSteamID().ToString());
			}
		}

		public static void BrowseServers()
		{
			if (MyMod.ServerBrowser != null)
			{
				MyMod.ServerBrowser.SetActive(value: true);
			}
			SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
			SteamAPICall_t hAPICall = SteamMatchmaking.RequestLobbyList();
			OnLobbyMatchListCallResult.Set(hAPICall);
		}

		public static void JoinToLobby(string hostid)
		{
			ClientUser.myId = 0;
			CSteamID steamIDLobby = new CSteamID(ulong.Parse(hostid));
			ClientUser.InitializeClientData();
			SteamMatchmaking.JoinLobby(steamIDLobby);
		}

		public static void SendUDPData(Packet _packet, CSteamID receiver)
		{
			byte[] array = _packet.ToArray();
			bool sent = SteamNetworking.SendP2PPacket(receiver, array, (uint)array.Length, EP2PSend.k_EP2PSendReliable);
			SteamGameJoin.TracePacket(receiver, array, incoming: false, sent);
		}

		public static void SendUDPData(Packet _packet, string receiver)
		{
			CSteamID receiver2 = new CSteamID(ulong.Parse(receiver));
			SendUDPData(_packet, receiver2);
		}

		public static void Disconnect(string hostid)
		{
			SteamNetworking.CloseP2PSessionWithUser(new CSteamID(ulong.Parse(hostid)));
			MelonLogger.Msg("[SteamWorks.NET] Disconnected!");
		}

		public static void HandleData(byte[] _data)
		{
			if (MyMod.iAmHost)
			{
				int _clientID = 0;
				using (Packet packet = new Packet(_data))
				{
					_clientID = packet.ReadInt();
					packet.ReadString();
					int length = packet.ReadInt();
					_data = packet.ReadBytes(length);
				}
				ThreadManager.ExecuteOnMainThread(delegate
				{
					using Packet packet3 = new Packet(_data);
					MyMod.SimUDPHandle(packet3.ReadInt(), packet3, _clientID);
				});
				return;
			}
			Dictionary<int, ClientUser.PacketHandler> sessionHandlers = ClientUser.packetHandlers;
			string sessionHost = MyMod.SteamServerWorks;
			if (!MyMod.ConnectedSteamWorks || sessionHandlers == null || string.IsNullOrEmpty(sessionHost))
			{
				return;
			}
			using (Packet packet2 = new Packet(_data))
			{
				int length2 = packet2.ReadInt();
				_data = packet2.ReadBytes(length2);
			}
			ThreadManager.ExecuteOnMainThread(delegate
			{
				using Packet packet3 = new Packet(_data);
				int packetId = packet3.ReadInt();
				if (!MyMod.iAmHost && MyMod.ConnectedSteamWorks && !(MyMod.SteamServerWorks != sessionHost) && ClientUser.packetHandlers == sessionHandlers)
				{
					MyMod.SimUDPHandle_Client(packetId, packet3);
				}
			});
		}

		public static void ListenData()
		{
			if (!CanUseSteam)
			{
				return;
			}
			uint pcubMsgSize;
			while (SteamNetworking.IsP2PPacketAvailable(out pcubMsgSize))
			{
				byte[] array = new byte[pcubMsgSize];
				if (!SteamNetworking.ReadP2PPacket(array, pcubMsgSize, out var pcubMsgSize2, out var psteamIDRemote))
				{
					continue;
				}
				if (pcubMsgSize2 != array.Length)
				{
					Array.Resize(ref array, checked((int)pcubMsgSize2));
				}
				if (MyMod.iAmHost || (MyMod.ConnectedSteamWorks && ClientUser.packetHandlers != null && !(psteamIDRemote.ToString() != MyMod.SteamServerWorks)))
				{
					SteamGameJoin.TracePacket(psteamIDRemote, array, incoming: true, sent: true);
					if (array.Length > 4)
					{
						HandleData(array);
					}
				}
			}
		}
	}

	public static bool CanUseSteam = false;

	public static string SteamName = "";

	public static bool IsMyLobby = false;

	public static bool HasDLC = false;

	private const string ExpectedSteamworksVersion = "20.2.0";

	private static bool s_ResolverInstalled;

	private static string s_LoggedLibraryPath;

	private static string DescribeSteamworksAssembly()
	{
		try
		{
			Assembly assembly = typeof(SteamAPI).Assembly;
			string text = "";
			try
			{
				text = " at " + assembly.Location;
			}
			catch
			{
			}
			return assembly.GetName().Name + " " + assembly.GetName().Version?.ToString() + text;
		}
		catch (Exception ex)
		{
			return "could not be identified (" + ex.Message + ")";
		}
	}

	private static void InstallSteamDllResolver()
	{
		if (s_ResolverInstalled)
		{
			return;
		}
		s_ResolverInstalled = true;
		try
		{
			System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, delegate(string name, Assembly assembly, DllImportSearchPath? path)
			{
				if (!name.StartsWith("steam_api", StringComparison.OrdinalIgnoreCase))
				{
					return IntPtr.Zero;
				}
				foreach (string item in SteamApiCandidatePaths())
				{
					if (File.Exists(item) && System.Runtime.InteropServices.NativeLibrary.TryLoad(item, out var handle))
					{
						if (s_LoggedLibraryPath != item)
						{
							s_LoggedLibraryPath = item;
							MelonLogger.Msg("[Steamworks.NET] Loaded " + item);
						}
						return handle;
					}
				}
				return IntPtr.Zero;
			});
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Steamworks.NET] Could not install the native library resolver: " + ex.Message);
		}
	}

	private static IEnumerable<string> SteamApiCandidatePaths()
	{
		string root = Directory.GetCurrentDirectory();
		yield return Path.Combine(root, "steam_api64.dll");
		string[] directories;
		try
		{
			directories = Directory.GetDirectories(root, "*_Data");
		}
		catch
		{
			yield break;
		}
		string[] array = directories;
		foreach (string dataDir in array)
		{
			yield return Path.Combine(dataDir, "Plugins", "x86_64", "steam_api64.dll");
			yield return Path.Combine(dataDir, "Plugins", "steam_api64.dll");
		}
	}

	public static void Init()
	{
		try
		{
			InitCore();
		}
		catch (EntryPointNotFoundException ex)
		{
			MelonLogger.Error("[Steamworks.NET] " + ex.Message);
			MelonLogger.Error("[Steamworks.NET] The Steamworks.NET.dll in UserLibs is built against a different Steamworks SDK than this game ships.");
			MelonLogger.Error("[Steamworks.NET]   loaded: " + DescribeSteamworksAssembly());
			MelonLogger.Error("[Steamworks.NET]   needed: version 20.2.0 - copy Steamworks.NET.dll from the mod's bin\\Release over the one in UserLibs.");
			MelonLogger.Error("[Steamworks.NET] Steam lobbies and invites are disabled until then. Direct IP and dedicated servers still work.");
			CanUseSteam = false;
		}
		catch (Exception ex2)
		{
			MelonLogger.Warning("[Steamworks.NET] Steam is unavailable, so Steam lobbies are disabled. Direct IP and dedicated servers still work. (" + ex2.GetType().Name + ": " + ex2.Message + ")");
			CanUseSteam = false;
		}
	}

	private static void InitCore()
	{
		InstallSteamDllResolver();
		if (SteamAPI.RestartAppIfNecessary(new AppId_t(305620u)))
		{
			Application.Quit();
			return;
		}
		MelonLogger.Msg("[SteamWorks.NET] Trying to Init SteamAPI");
		if (!SteamAPI.Init())
		{
			MelonLogger.Warning("[Steamworks.NET] SteamAPI.Init() failed (is Steam running?). Steam lobbies are unavailable. Direct IP and dedicated servers still work.");
			CanUseSteam = false;
			return;
		}
		if (!Packsize.Test())
		{
			MelonLogger.Msg("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.");
		}
		if (!DllCheck.Test())
		{
			MelonLogger.Msg("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.");
		}
		if (!SteamApps.BIsAppInstalled(new AppId_t(305620u)))
		{
			MelonLogger.Warning("[SteamApps] Steam does not report The Long Dark as installed, so Steam lobbies are unavailable. Direct IP and dedicated servers still work.");
			CanUseSteam = false;
		}
		else
		{
			HasDLC = SteamApps.BIsDlcInstalled(new AppId_t(2091330u));
			StartSteam();
			MelonLogger.Msg("[SteamWorks.NET] SteamAPI is initialized");
		}
	}

	public static bool StartSteam()
	{
		SharedSteamCallbacks.Initialize(s_LoggedLibraryPath);
		CanUseSteam = true;
		Main.Run();
		return true;
	}

	public static void DoUpdate()
	{
		if (!CanUseSteam)
		{
			return;
		}
		try
		{
			Main.ListenData();
		}
		catch (Exception ex)
		{
			MelonLogger.Error("[Steam networking] Receive failed: " + ex);
		}
		finally
		{
			SteamAPI.RunCallbacks();
			SteamGameJoin.Tick();
		}
	}
}
