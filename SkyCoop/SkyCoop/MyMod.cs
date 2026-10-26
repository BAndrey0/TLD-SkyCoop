using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using Il2CppAK;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppMissionTypes;
using Il2CppNodeCanvas.Framework;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.AI;
using Il2CppTLD.AddressableAssets;
using Il2CppTLD.BigCarry;
using Il2CppTLD.Gear;
using Il2CppTLD.SaveState;
using Il2CppTLD.Stats;
using Il2CppVoice;
using MelonLoader;
using TinyJSON;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCoop;

public class MyMod : MelonMod
{
	public static class BuildInfo
	{
		public const string Name = "Sky Co-op LTS";

		public const string Description = "Multiplayer mod";

		public const string Author = "Filigrani";

		public const string Company = null;

		public const string Version = "0.14.9";

		public const string DownloadLink = null;

		public const int RandomGenVersion = 5;
	}

	private enum SaveExitAction
	{
		Quit,
		Disconnect
	}

	public enum CustomSkills
	{
		FirstAid = 10
	}

	public enum ResendPacketType
	{
		None,
		Scene,
		Container,
		GearPlace,
		Examine,
		GearPickup,
		ServerRestart,
		ExpeditionInvites,
		Cancel
	}

	public class BoardGameSession
	{
		public string m_GameType = "";

		public string m_GUID = "";

		public int m_Turn;

		public int m_CurrentPlayer;

		public bool m_Started;

		public System.Collections.Generic.List<int> m_Players = new System.Collections.Generic.List<int>();

		public System.Collections.Generic.List<System.Collections.Generic.List<int>> m_Decks = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();

		public BoardGameSession(string guid, string gt)
		{
			m_GameType = gt;
			m_GUID = guid;
		}

		public bool Equals(BoardGameSession other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_GUID == other.m_GUID)
			{
				return true;
			}
			return false;
		}

		public System.Collections.Generic.List<int> ShuffleDeck(System.Collections.Generic.List<int> Deck)
		{
			System.Random random = new System.Random();
			for (int i = 0; i < Deck.Count; i++)
			{
				int value = Deck[i];
				int index = random.Next(0, Deck.Count);
				Deck[i] = Deck[index];
				Deck[index] = value;
			}
			return Deck;
		}

		public void InitDecks()
		{
			m_Decks = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
			if (m_GameType == "GoldSapper")
			{
				m_Decks.Add(ShuffleDeck(CreateDeck(36)));
			}
		}

		public System.Collections.Generic.List<int> CreateDeck(int numCards)
		{
			System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
			for (int i = 1; i <= numCards; i++)
			{
				list.Add(i);
			}
			return list;
		}

		public bool AddPlayers(int ID)
		{
			if (!m_Players.Contains(ID))
			{
				m_Players.Add(ID);
				return true;
			}
			return false;
		}

		public void RemovePlayer(int ID)
		{
			m_Players.Remove(ID);
		}

		public void StartGame()
		{
			m_Turn = 0;
			m_CurrentPlayer = 0;
			m_Started = true;
		}

		public void NextTurn()
		{
			m_Turn++;
			m_CurrentPlayer++;
			if (m_Players.Count - 1 < m_CurrentPlayer)
			{
				m_CurrentPlayer = 0;
			}
		}
	}

	public enum KeysAction
	{
		OPEN,
		LOCK,
		UNLOCK
	}

	public static int LastLoadedGenVersion = 0;

	public static bool CantBeUsedForMP = false;

	public static int SelectedSaveSeed = 0;

	public static string SelectedSaveName = "";

	public static int LobbyStartingRegion = -1;

	public static int LobbyStartingExperience = -1;

	public static bool HostFromLobbyAfterLoader = false;

	public static string LobbyState = "Idle";

	public static bool StartServerAfterSelectSave = false;

	public static int PortsToHostOn = 26950;

	public static bool LastSafeStatus = false;

	public static bool OnExpedition = false;

	public static string ExpeditionLastTaskText = "";

	public static string ExpeditionLastName = "";

	public static int ExpeditionLastTime = 0;

	public static string LastExpeditionAlias = "";

	public static bool isRuning = false;

	public static bool sendMyPosition = false;

	public static bool iAmHost = false;

	public static bool IamShatalker = false;

	public static string MyUGUID = "";

	public static GameObject playerbody = null;

	public static System.Collections.Generic.List<GameObject> players = new System.Collections.Generic.List<GameObject>();

	public static System.Collections.Generic.List<DataStr.MultiPlayerClientData> playersData = new System.Collections.Generic.List<DataStr.MultiPlayerClientData>();

	public static GameObject MyPlayerDoll = null;

	public static GameObject DollCameraDummy = null;

	public static bool IsDead = false;

	public static int levelid = 0;

	public static string level_name = "";

	public static string level_guid = "";

	public static string previous_level_guid = "";

	public static bool NeedSyncTime = false;

	public static bool LightSource = false;

	public static bool PreviousLightSource = false;

	public static bool MyLightSource = false;

	public static string MyLightSourceName = "";

	public static bool MyLastLightSource = false;

	public static string MyLastLightSourceName = "";

	public static string LastSelectedGearName = "";

	public static GearItem LastSelectedGear = null;

	public static bool NeedRefreshInv = false;

	public static string MyAnimState = "Idle";

	public static DataStr.MultiplayerEmote MyEmote = null;

	public static string MyPreviousAnimState = "Idle";

	public static bool MyHasRifle = false;

	public static bool HasRevolver = false;

	public static bool MyHasRevolver = false;

	public static bool MyHasAxe = false;

	public static bool MyHasMedkit = false;

	public static bool MyIsAiming = false;

	public static int CycleSkip = 0;

	public static int MyCycleSkip = 0;

	public static bool IsSleeping = false;

	public static bool PreviousSleeping = false;

	public static bool IsCycleSkiping = false;

	public static int MyArrows = 0;

	public static int MyFlares = 0;

	public static int MyBlueFlares = 0;

	public static int StepState = 0;

	public static bool AnimalsController = true;

	public static string WildlifeAuthorityScene = "";

	public static int MyTicksOnScene = 0;

	public static int DeltaAnimalsMultiplayer = 12;

	public static string DebugAnimalGUID = "";

	public static string DebugAnimalGUIDLast = "";

	public static GameObject DebugLastAnimal = null;

	public static string HarvestingAnimal = "";

	public static string PreviousHarvestingAnimal = "";

	public static bool ALWAYS_FUCKING_CURSOR_ON = false;

	public static GameObject PistolBulletPrefab = null;

	public static GameObject RevolverBulletPrefab = null;

	public static bool NeedTryReconnect = false;

	public static bool TryingReconnect = false;

	public static int NoHostResponceSeconds = 0;

	public static GearItem SaveThrowingItem = null;

	public static int OverridedHourse = 12;

	public static int OverridedMinutes = 0;

	public static string OveridedTime = "12:0";

	public static int PlayedHoursInOnline = 0;

	public static bool ConnectedSteamWorks = false;

	public static string SteamServerWorks = "";

	public static string SteamLobbyToJoin = "";

	public static AssetBundle LoadedBundle = null;

	public static int MaxPlayers = 1;

	public static int GiveItemTo = -1;

	public static string MyHat = "";

	public static string MyTop = "";

	public static string MyBottom = "";

	public static string MyBoots = "";

	public static string MyScarf = "";

	public static string MyBalaclava = "";

	public static System.Collections.Generic.List<DataStr.PickedGearSync> RecentlyPickedGears = new System.Collections.Generic.List<DataStr.PickedGearSync>();

	public static System.Collections.Generic.List<DataStr.ClimbingRopeSync> DeployedRopes = new System.Collections.Generic.List<DataStr.ClimbingRopeSync>();

	public static System.Collections.Generic.List<DataStr.DeathContainerData> DeathCreates = new System.Collections.Generic.List<DataStr.DeathContainerData>();

	public static System.Collections.Generic.List<DataStr.ShowShelterByOther> ShowSheltersBuilded = new System.Collections.Generic.List<DataStr.ShowShelterByOther>();

	public static bool IsDrinking = false;

	public static bool PreviousIsDrinking = false;

	public static bool IsEating = false;

	public static bool PreviousIsEating = false;

	public static DataStr.ServerConfigData ServerConfig = new DataStr.ServerConfigData();

	public static bool NowHeavyBreath = false;

	public static bool PreviousNowHeavyBreath = false;

	public static int BloodLosts = 0;

	public static int PreviousBloodLosts = 0;

	public static PlayerControlMode PreviousControlModeBeforeAction = PlayerControlMode.Normal;

	public static Comps.MultiplayerPlayer PlayerInteractionWith = null;

	public static GearItem EmergencyStimBeforeUse = null;

	public static bool HasInfecitonRisk = false;

	public static bool PreviousHasInfectionRisk = false;

	public static DataStr.ContainerOpenSync MyContainer = null;

	public static int UpdatePickedPlants = -1;

	public static int NeedConnectAfterLoad = -1;

	public static int NeedLoadSaveAfterLoad = -1;

	public static int UpdateSnowshelters = -1;

	public static int UpdateRopesAndFurns = -1;

	public static int UpdateCampfires = -1;

	public static int UpdateEverything = -1;

	public static int RemoveAttachedGears = -1;

	public static int StartDSAfterLoad = -1;

	public static int SendAfterLoadingFinished = -1;

	public static int TryMakeLobbyAgain = -1;

	public static int RegularUpdateSeconds = 7;

	public static int MinutesFromStartServer = 0;

	public static int TimeOutSeconds = 300;

	public static int TimeOutSecondsForLoaders = 300;

	public static int TimeToWorryAboutLastRequest = 10;

	public static bool SkipEverythingForConnect = false;

	public static int PlayersOnServer = 0;

	public static GameObject UIHostMenu = null;

	public static GameObject MicrophoneIdicator = null;

	public static GameObject RadioIdicator = null;

	public static GameObject LobbyUI = null;

	public static GameObject LobbyDeitails = null;

	public static GameObject ServerBrowser = null;

	public static UtilsPanelChoose.DetailsObjets LobbyDeitailsStrct = null;

	public static GameObject LobbyNewGame = null;

	public static GameObject EmoteWheel = null;

	public static GameObject NewFlairNotification = null;

	public static GameObject CustomizeUi = null;

	public static Text CustomizeUiTabLable = null;

	public static Transform CustomizeUiScrollContentRoot = null;

	public static int TargetFlairSlot = 1;

	public static string NotificationString = "";

	public static GameObject ExpeditionEditorUI = null;

	public static GameObject ExpeditionEditorSelectUI = null;

	public static int ForcedShowExpeditionHUDSeconds = 0;

	public static string CustomServerName = "";

	public static string MyLobby = "";

	public static bool ApplyOtherCampfires = false;

	public static System.Collections.Generic.Dictionary<long, string> SlicedJsonDataBuffer = new System.Collections.Generic.Dictionary<long, string>();

	public static System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<byte>> SlicedBytesDataBuffer = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<byte>>();

	public static bool DebugTrafficCheck = false;

	public static System.Collections.Generic.Dictionary<string, bool> OpenableThings = new System.Collections.Generic.Dictionary<string, bool>();

	public static System.Collections.Generic.Dictionary<int, GameObject> DroppedGearsObjs = new System.Collections.Generic.Dictionary<int, GameObject>();

	public static System.Collections.Generic.Dictionary<int, GameObject> TrackableDroppedGearsObjs = new System.Collections.Generic.Dictionary<int, GameObject>();

	public static int OverrideLampReduceFuel = -1;

	public static System.Collections.Generic.Dictionary<string, float> BooksResearched = new System.Collections.Generic.Dictionary<string, float>();

	public static System.Collections.Generic.Dictionary<string, LoadScene> DoorsObjs = new System.Collections.Generic.Dictionary<string, LoadScene>();

	public static bool DelayedGearsPickup = false;

	public static System.Collections.Generic.List<DataStr.PickedGearSync> PickedGearsBackup = new System.Collections.Generic.List<DataStr.PickedGearSync>();

	public static System.Collections.Generic.List<DataStr.BrokenFurnitureSync> BrokenFurnsBackup = new System.Collections.Generic.List<DataStr.BrokenFurnitureSync>();

	public static Vector3 LastPickedVisualPosition = new Vector3(0f, 0f, 0f);

	public static Quaternion LastPickedVisualRotation = new Quaternion(0f, 0f, 0f, 0f);

	public static bool DoingRecord = false;

	public static GameObject ModdedHandsBook = null;

	public static GameObject ViewModelRadio = null;

	private static bool LoggedMissingRadio;

	public static GameObject ViewModelRadioNeedle = null;

	public static GameObject ViewModelRadioLED = null;

	public static GameObject MyRadioAudio = null;

	public static GameObject ViewModelHatchet = null;

	public static GameObject ViewModelHatchet2 = null;

	public static GameObject ViewModelKnife = null;

	public static GameObject ViewModelKnife2 = null;

	public static GameObject ViewModelJeremiahKnife = null;

	public static GameObject ViewModelPrybar = null;

	public static GameObject ViewModelHammer = null;

	public static GameObject ViewModelShovel = null;

	public static GameObject ViewModelBolt = null;

	public static GameObject ViewModelStone = null;

	public static GameObject ViewModelFireAxe = null;

	public static GameObject ViewModelHackSaw = null;

	public static GameObject ViewModelPhoto = null;

	public static GameObject ViewModelMap = null;

	public static GameObject ViewModelNote = null;

	public static SkinnedMeshRenderer ViewModelRifle = null;

	public static GameObject ViewModelHands = null;

	public static bool UseBoltInsteadOfStone = false;

	public static bool OriginalRadioSeaker = false;

	public static bool VanilaRadio = false;

	public static GameObject ViewModelDummy = null;

	public static float RadioFrequency = 0f;

	public static float PreviousRadioFrequency = 0f;

	public static Vector3 LastSleepV3 = new Vector3(0f, 0f, 0f);

	public static Quaternion LastSleepQuat = new Quaternion(0f, 0f, 0f, 0f);

	public static bool AtBed = false;

	public static Vector3 OutOfBedPosition = new Vector3(0f, 0f, 0f);

	public static System.Collections.Generic.Dictionary<string, int> GearIDList = new System.Collections.Generic.Dictionary<string, int>();

	public static bool GearIDListReady = false;

	public static bool InterloperHook = false;

	public static string OverridedSceneForSpawn = "";

	public static Vector3 OverridedPositionForSpawn = Vector3.zero;

	public static string SavedSceneForSpawn = "";

	public static Vector3 SavedPositionForSpawn = Vector3.zero;

	public static bool FixedPlaceLoaded = false;

	public static float InteractTimer = 0f;

	public static GameObject ObjectInteractWith = null;

	public static bool InteractionInprocess = false;

	public static string InteractionType = "";

	public static bool InteractHold = false;

	public static string AutoStartSlot = "";

	public static System.Collections.Generic.List<string> AutoCMDs = new System.Collections.Generic.List<string>();

	public static bool CrazyPatchesLogger = false;

	public static bool KillOnUpdate = false;

	public static bool KillEverySecond = false;

	public static bool KillEveryInGameMinute = false;

	public static bool NoAnimalSync = false;

	public static string RCON = "12345";

	public static bool SwearOnLockpickingDone = false;

	public static bool DedicatedServerAppMode = false;

	public static System.Collections.Generic.List<BoardGameSession> BoardGamesSessions = new System.Collections.Generic.List<BoardGameSession>();

	private const int CHUNK_SIZE = 500;

	public static bool SetP2PToLobbyForDSAfterLoad = false;

	public static bool HostUDDSAfterLoad = false;

	public static int PortForDSLoad = 0;

	public static int DsSavePerioud = 60;

	public static int RestartPerioud = 10800;

	public static bool QuitWithoutSaving = false;

	public static bool IReallyWantToQuit = false;

	public static bool NoMelonQuit = false;

	private static Il2CppSystem.Func<bool> SaveQuitGate;

	private static bool SaveBeforeQuitRequested;

	private static SaveExitAction PendingSaveExitAction = SaveExitAction.Quit;

	private static System.Action AfterSavedDisconnect;

	public const int CustomSkillsStart = 9;

	private static readonly HashSet<string> ReportedMissingAssets = new HashSet<string>();

	public static bool FoundSomethingToBreak = false;

	public static System.Collections.Generic.Dictionary<long, GameObject> PreSpawnedGears = new System.Collections.Generic.Dictionary<long, GameObject>();

	public static System.Collections.Generic.List<GameObject> SpawnedByOtherShelters = new System.Collections.Generic.List<GameObject>();

	public static BodyHarvest GoingToHarvest = null;

	public static System.Collections.Generic.Dictionary<string, Comps.AnimalActor> ActorsList = new System.Collections.Generic.Dictionary<string, Comps.AnimalActor>();

	public static System.Collections.Generic.Dictionary<string, Comps.AnimalCorpseObject> AnimalCorplesList = new System.Collections.Generic.Dictionary<string, Comps.AnimalCorpseObject>();

	public static System.Collections.Generic.Dictionary<string, int> BannedSpawnRegions = new System.Collections.Generic.Dictionary<string, int>();

	private static Vector3 previoustickpos;

	private static Quaternion previoustickrot;

	public static System.Collections.Generic.List<DataStr.FireSourcesSync> FireSources = new System.Collections.Generic.List<DataStr.FireSourcesSync>();

	public static bool DsServerIsUp = false;

	public static int SecondsWithoutSaving = 0;

	public static int SecondsWithoutRestart = 0;

	public static bool NeedApplyAutoCMDs = false;

	public static bool AutoHostWhenLoaded = false;

	public static int ApplyAutoThingsAfterLoaed = 0;

	public static float MinimalDistanceForSpawn = 300f;

	public static float MaximalDistanceForAnimalRender = 260f;

	public static float LastResponceTime = 0f;

	public static bool RebootSceneAfterSave = false;

	public static bool CloseGameOnNextDSSave = false;

	public static bool NoDsSaves = false;

	public static float nextActionTime = 0f;

	public static float period = 5f;

	public static float nextActionTimeAniamls = 0f;

	public static float periodAniamls = 0.3f;

	public static float nextActionTimeSecond = 0f;

	public static float periodSecond = 1f;

	public static DataStr.SaveSlotSync PendingSave = null;

	public static bool ShouldCreateSaveForHost = false;

	public static SaveSlotInfo PendingSaveSlotInfo = null;

	public static bool DefaultIsRussian = false;

	public static bool NotNeedToPauseUntilLoaded = false;

	public static Canvas UiCanvas = null;

	public static bool CanvasUisReady = false;

	private static float CanvasUiRetryAt = 0f;

	public static System.Collections.Generic.List<DataStr.MultiplayerChatMessage> ChatMessages = new System.Collections.Generic.List<DataStr.MultiplayerChatMessage>();

	public static int MaxChatMessages = 50;

	public static string MyChatName = "Player";

	public static InputField chatInput = null;

	public static GameObject chatPanel = null;

	public static GameObject ChatObject = null;

	public static GameObject StatusPanel = null;

	public static GameObject StatusObject = null;

	public static ScrollRect chatScroller = null;

	public static int HideChatTimer = 0;

	public static bool PendingRespawn = false;

	public static System.Collections.Generic.List<GameObject> StatusTexes = new System.Collections.Generic.List<GameObject>();

	public static System.Collections.Generic.Dictionary<ulong, GameObject> LobbyElements = new System.Collections.Generic.Dictionary<ulong, GameObject>();

	public static GameObject new_button = null;

	public static GameObject new_button2 = null;

	public static GameObject SleepingButtons = null;

	public static GameObject WaitForSleepLable = null;

	public static bool HasWaitForConnect = false;

	public static InterfaceManager m_InterfaceManager;

	public static Panel_SelectSurvivor m_Panel_SelectSurvivor;

	public static Panel_MainMenu m_Panel_MainMenu;

	public static Panel_SelectRegion_Map m_Panel_SelectRegion;

	public static Panel_Sandbox m_Panel_Sandbox;

	public static Panel_ChooseSandbox m_Panel_ChooseSandbox;

	public static Panel_ChallengeComplete m_Panel_ChallengeComplete;

	public static ResendPacketType LastPacketForRepeat = ResendPacketType.None;

	public static int SecondsLeftUntilWorryAboutPacket = -1;

	public static int ResendAttempts = 0;

	public static string ResendGearLvlKey = "";

	public static int ResendGearSearchKey = 0;

	public static float RecordStartTime;

	public static float RecordReleseButtonHold;

	public static DataStr.FireSourcesSync DebugFireSource = null;

	public static GameObject DiagnosisDummy = null;

	public static int CurePlayerID = 0;

	public static bool DebugDiagnosis = false;

	public static System.Collections.Generic.Dictionary<string, GameObject> OpenablesObjs = new System.Collections.Generic.Dictionary<string, GameObject>();

	public static System.Collections.Generic.List<DataStr.SlicedJsonData> CarefulSlicesBuffer = new System.Collections.Generic.List<DataStr.SlicedJsonData>();

	public static System.Collections.Generic.List<DataStr.SlicedJsonData> CarefulSlicesGearBuffer = new System.Collections.Generic.List<DataStr.SlicedJsonData>();

	public static System.Collections.Generic.List<DataStr.SlicedJsonData> CarefulSlicesPhotoBuffer = new System.Collections.Generic.List<DataStr.SlicedJsonData>();

	public static int CarefulSlicesSent = 0;

	public static Container GoingToOpenContinaer = null;

	public static string PreviousEquipmentCheck = "";

	public static float nextLEDBlink = 0f;

	public static float LastSearchDistance = MaxRadioSearchDistance;

	public static float MaxRadioSearchDistance = 400f;

	public static float BlinkRateConstant = 210f;

	public static float MinimalBlink = 0.03f;

	public static bool SearchModeActive = false;

	public static bool ShouldPerformAttack = false;

	public static bool ShouldReEquipFaster = false;

	public static int SkipItemEvent = 0;

	public static float MeleeRange = 1.9f;

	public static bool IgnoreEmoteKeyDownUntilReleased = false;

	public static bool QuitWhenSaveOperationFinished = false;

	public static long PendingSaveHashToSend = 0L;

	public static int SaveCommitDrainAfterFrame;

	public static string PendingSlotDiskCommit;

	public static bool Flag1 = true;

	public static bool Flag2 = true;

	public static bool Flag3 = true;

	public static bool Flag4 = true;

	public static bool Flag5 = true;

	public static bool Flag6 = true;

	public static bool Flag7 = true;

	public static bool Flag8 = true;

	public static bool Flag9 = true;

	public static bool Flag10 = true;

	public static bool Flag11 = true;

	public static bool Flag12 = true;

	public static bool Flag13 = true;

	public static bool IgnoreFeebackKeyDownUntilReleased = false;

	public static System.Collections.Generic.Dictionary<string, GameObject> SemiPrefabs = new System.Collections.Generic.Dictionary<string, GameObject>();

	public static bool DonePreload = false;

	public static bool DonePreloadNextFrame = false;

	public static bool DumpOnNextFrame = false;

	public static string[] scenes;

	public static int sceneCount = -1;

	public static int CurrentSearchIndex = 5;

	public static bool DumpingGearSpawns = false;

	public static DataStr.BufferedGearSpawners AllGearSpawners = new DataStr.BufferedGearSpawners();

	public static bool NoUI = false;

	public static bool ForcedUiOn = true;

	public static bool AtHostMenu = false;

	public static bool AtDebug = false;

	public static string UIDebugType = "";

	public static System.Collections.Generic.List<string> ItemsForDebug = new System.Collections.Generic.List<string>();

	public static int ItemForDebug = -1;

	public static string AdvancedDebugMode = "";

	public static bool DebugGUI = false;

	public static bool DebugBind = false;

	public static string PendingKeySeed = "";

	public static string PendingKeyName = "";

	public static GameObject PendingRegisterKey = null;

	public static bool LoadingScreenIsOn = true;

	public static System.Collections.Generic.List<GameObject> HostMenuHints = new System.Collections.Generic.List<GameObject>();

	public static System.Collections.Generic.Dictionary<int, bool> FoundCairns = new System.Collections.Generic.Dictionary<int, bool>();

	public static System.Collections.Generic.Dictionary<int, Vector3> PossibleCairns = new System.Collections.Generic.Dictionary<int, Vector3>();

	public static DataStr.CustomChallengeData CurrentCustomChalleng = new DataStr.CustomChallengeData();

	public static DataStr.CustomChallengeRules CurrentChallengeRules = new DataStr.CustomChallengeRules();

	public static KeysAction PendingKeysAction = KeysAction.LOCK;

	public static string DeleteGearOnExpeditionConfirmed = "";

	public static int PendingLocksmithAction = 0;

	public static GameObject PendingLocksmithObject = null;

	public static GameObject TempDoor = null;

	public const int PhotoQuality = 60;

	public static Comps.FakeRockCache PendingRockCahceRemove = null;

	public static bool SaveExitPending
	{
		get
		{
			if (!Pathes.QuitOnSave && !SaveBeforeQuitRequested)
			{
				return QuitWhenSaveOperationFinished;
			}
			return true;
		}
	}

	public static bool WildlifeSceneReady()
	{
		if (WildlifeSpawnAuthorityPolicy.Ready(connected: true, LoadingScreenIsOn, IsDead, knocked: false, MyTicksOnScene, level_guid) && !Shared.IsMainMenuScene(level_name) && level_name != "Boot" && level_name != "Empty")
		{
			return GameManager.GetPlayerManagerComponent() != null;
		}
		return false;
	}

	public static bool CanSpawnWildlifeInCurrentScene()
	{
		if (AnimalsController && WildlifeSceneReady())
		{
			if (!iAmHost)
			{
				if (sendMyPosition)
				{
					return WildlifeAuthorityScene == level_guid;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static void KillConsole()
	{
		if (ServerConfig.m_CheatsMode != 2 && (!iAmHost || ServerConfig.m_CheatsMode != 1) && InOnline() && !DedicatedServerAppMode && uConsole.m_Instance != null)
		{
			uConsole.m_Instance.m_Activate = KeyCode.None;
		}
	}

	public static void AddSlicedJsonData(DataStr.SlicedJsonData jData)
	{
		if (SlicedJsonDataBuffer.ContainsKey(jData.m_Hash))
		{
			string value = "";
			if (SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out value))
			{
				string value2 = value + jData.m_Str;
				SlicedJsonDataBuffer.Remove(jData.m_Hash);
				SlicedJsonDataBuffer.Add(jData.m_Hash, value2);
			}
			else
			{
				SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
			}
		}
		else
		{
			SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
		}
		if (jData.m_Last)
		{
			string value3 = "";
			if (SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out value3))
			{
				SlicedJsonDataBuffer.Remove(jData.m_Hash);
				GiveRecivedItem(new DataStr.GearItemDataPacket
				{
					m_GearName = jData.m_GearName,
					m_DataProxy = value3
				});
			}
		}
	}

	public static void PatchBookReadTime(GearItem __instance)
	{
		if (__instance.m_ResearchItem == null)
		{
			return;
		}
		if (__instance.m_ObjectGuid == null)
		{
			__instance.m_ObjectGuid = __instance.gameObject.AddComponent<ObjectGuid>();
			__instance.m_ObjectGuid.MaybeRuntimeRegister();
			return;
		}
		string key = __instance.m_ObjectGuid.Get();
		if (BooksResearched.ContainsKey(key))
		{
			if (BooksResearched.TryGetValue(key, out var value))
			{
				__instance.m_ResearchItem.m_ElapsedHours = value;
			}
		}
		else
		{
			__instance.m_ResearchItem.m_ElapsedHours = 0f;
		}
		if (__instance.m_ResearchItem.IsResearchComplete())
		{
			__instance.m_ResearchItem.m_SkillType = SkillType.Firestarting;
		}
		else
		{
			__instance.m_ResearchItem.m_SkillType = SkillType.ToolRepair;
		}
	}

	public static void AddSlicedJsonDataForPicker(DataStr.SlicedJsonData jData, bool place)
	{
		if (SlicedJsonDataBuffer.ContainsKey(jData.m_Hash))
		{
			string value = "";
			if (SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out value))
			{
				string value2 = value + jData.m_Str;
				SlicedJsonDataBuffer.Remove(jData.m_Hash);
				SlicedJsonDataBuffer.Add(jData.m_Hash, value2);
			}
			else
			{
				SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
			}
		}
		else
		{
			SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
		}
		if (!jData.m_Last)
		{
			return;
		}
		string value3 = "";
		DiscardRepeatPacket();
		RemovePleaseWait();
		if (!SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out value3))
		{
			return;
		}
		SlicedJsonDataBuffer.Remove(jData.m_Hash);
		MelonLogger.Msg(System.ConsoleColor.Green, "Finished getting data for:" + jData.m_Hash);
		string gearName = jData.m_Extra.m_GearName;
		MelonLogger.Msg(System.ConsoleColor.Cyan, "Going to get " + jData.m_Extra.m_GearName);
		GameObject gearItemObject = GetGearItemObject(gearName);
		if (gearItemObject == null)
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + gearName + " not exist! Maybe you miss an modded item pack?");
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f));
		DisableObjectForXPMode.RemoveDisabler(gameObject.gameObject);
		if (gameObject == null)
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + gearName + " not exist! Maybe you miss an modded item pack?");
			return;
		}
		gameObject.name = CloneTrimer(gameObject.name);
		DataStr.ExtraDataForDroppedGear extra = jData.m_Extra;
		GearItem component = gameObject.GetComponent<GearItem>();
		if (gameObject.GetComponent<KeroseneLampItem>() != null && extra != null)
		{
			int num = (OverrideLampReduceFuel = MinutesFromStartServer - extra.m_DroppedTime);
			MelonLogger.Msg(System.ConsoleColor.Cyan, "Lamp been dropped " + num + " minutes");
		}
		bool flag = false;
		if (!string.IsNullOrEmpty(value3))
		{
			gameObject.GetComponent<GearItem>().DeserializeFromJson(value3);
		}
		else
		{
			flag = true;
		}
		ThrownFlareSync.ApplyPickupBurn(component, extra);
		MelonLogger.Msg(System.ConsoleColor.Cyan, "UseLastVisualDropPos " + flag);
		if (!string.IsNullOrEmpty(jData.m_Extra.m_PhotoGUID))
		{
			GearItem component2 = gameObject.GetComponent<GearItem>();
			if (component2.m_ObjectGuid == null)
			{
				string photoGUID = jData.m_Extra.m_PhotoGUID;
				ObjectGuid objectGuid = gameObject.AddComponent<ObjectGuid>();
				objectGuid.m_Guid = photoGUID;
				component2.m_ObjectGuid = objectGuid;
				if (component2.GetGearName() != "GEAR_SCMapReveal")
				{
					MelonLogger.Msg("Going render photo " + photoGUID);
					Texture2D photoTexture = MPSaveManager.GetPhotoTexture(photoGUID, component2.GetGearName());
					if ((bool)photoTexture)
					{
						gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = photoTexture;
					}
				}
			}
		}
		gameObject.GetComponent<GearItem>().m_BeenInPlayerInventory = true;
		Comps.DropFakeOnLeave dropFakeOnLeave = gameObject.AddComponent<Comps.DropFakeOnLeave>();
		if (!flag)
		{
			dropFakeOnLeave.m_OldPossition = gameObject.gameObject.transform.position;
			dropFakeOnLeave.m_OldRotation = gameObject.gameObject.transform.rotation;
		}
		else
		{
			dropFakeOnLeave.m_OldPossition = LastPickedVisualPosition;
			dropFakeOnLeave.m_OldRotation = LastPickedVisualRotation;
		}
		if (extra != null)
		{
			if (extra.m_GoalTime != 0 && extra.m_GoalTime != -1)
			{
				GearItem component3 = gameObject.GetComponent<GearItem>();
				if (component3.m_EvolveItem != null)
				{
					int num2 = MinutesFromStartServer - extra.m_DroppedTime;
					component3.m_EvolveItem.m_TimeSpentEvolvingGameHours = (float)num2 / 60f;
					MelonLogger.Msg(System.ConsoleColor.Blue, "Saving minutesOnDry " + num2);
					MelonLogger.Msg(System.ConsoleColor.Blue, "m_TimeSpentEvolvingGameHours " + component3.m_EvolveItem.m_TimeSpentEvolvingGameHours);
				}
			}
			if (!string.IsNullOrEmpty(extra.m_ExpeditionNote))
			{
				CreateCustomNote(extra.m_ExpeditionNote, gameObject.GetComponent<GearItem>());
			}
		}
		if (!place)
		{
			bool flag2 = false;
			if (component.m_Bed != null && component.m_Bed.GetState() == BedRollState.Placed)
			{
				component.m_Bed.SetState(BedRollState.Rolled);
				flag2 = true;
			}
			component.m_LastUpdatedTODHours = 0f;
			component.ManualStart();
			component.ManualUpdate();
			if (component.GetGearName() == "GEAR_SCNote" && string.IsNullOrEmpty(extra.m_Dropper))
			{
				flag2 = true;
				Pathes.DisplayNote(component);
			}
			if (!flag2)
			{
				GameManager.GetPlayerManagerComponent().InspectDroppedGearCompat(component);
			}
			else
			{
				GameManager.GetPlayerManagerComponent().ProcessPickupItemInteraction(component, forceEquip: false, forceEquipImmediate: false, skipAudio: false);
			}
		}
		else
		{
			component.PlayPickUpClip();
			GameManager.GetPlayerManagerComponent().StartPlaceMesh(gameObject, PlaceMeshFlags.None);
		}
		PatchBookReadTime(component);
	}

	public static void ShowCFGData()
	{
		MelonLogger.Msg(System.ConsoleColor.Green, "[Server Config Data] Data updated");
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_FastConsumption: " + ServerConfig.m_FastConsumption);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_DuppedSpawns: " + ServerConfig.m_DuppedSpawns);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_DuppedContainers: " + ServerConfig.m_DuppedContainers);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_PlayersSpawnType: " + ServerConfig.m_PlayersSpawnType);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_FireSync: " + ServerConfig.m_FireSync);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_CheatsMode: " + ServerConfig.m_CheatsMode);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_CheckModsValidation: " + ServerConfig.m_CheckModsValidation);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_SaveScamProtection: " + ServerConfig.m_SaveScamProtection);
		MelonLogger.Msg(System.ConsoleColor.Blue, "m_PVP: " + ServerConfig.m_PVP);
	}

	public static DataStr.PriorityActionForOtherPlayer GetCustomAction(string ActionName)
	{
		DataStr.PriorityActionForOtherPlayer priorityActionForOtherPlayer = new DataStr.PriorityActionForOtherPlayer();
		switch (ActionName)
		{
		case "Lockpick":
			priorityActionForOtherPlayer.m_Action = "Lockpick";
			priorityActionForOtherPlayer.m_DisplayText = priorityActionForOtherPlayer.m_Action;
			priorityActionForOtherPlayer.m_ProcessText = "Lockpicking...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 15f;
			priorityActionForOtherPlayer.m_Hold = false;
			priorityActionForOtherPlayer.m_EscCancle = true;
			break;
		case "Excision":
			priorityActionForOtherPlayer.m_Action = "Excision";
			priorityActionForOtherPlayer.m_DisplayText = priorityActionForOtherPlayer.m_Action;
			priorityActionForOtherPlayer.m_ProcessText = "Excision...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 10f;
			break;
		case "Locksmith0":
			priorityActionForOtherPlayer.m_Action = "Locksmith0";
			priorityActionForOtherPlayer.m_DisplayText = "Sawing";
			priorityActionForOtherPlayer.m_ProcessText = "Sawing...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 10f;
			priorityActionForOtherPlayer.m_Hold = false;
			break;
		case "Locksmith1":
			priorityActionForOtherPlayer.m_Action = "Locksmith1";
			priorityActionForOtherPlayer.m_DisplayText = "Grinding";
			priorityActionForOtherPlayer.m_ProcessText = "Grinding...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 10f;
			priorityActionForOtherPlayer.m_Hold = false;
			break;
		case "Locksmith5":
			priorityActionForOtherPlayer.m_Action = "Locksmith5";
			priorityActionForOtherPlayer.m_DisplayText = "Casting";
			priorityActionForOtherPlayer.m_ProcessText = "Casting...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 1.5f;
			priorityActionForOtherPlayer.m_Hold = false;
			break;
		}
		return priorityActionForOtherPlayer;
	}

	public static DataStr.PriorityActionForOtherPlayer GetActionForOtherPlayer(string ActName)
	{
		DataStr.PriorityActionForOtherPlayer priorityActionForOtherPlayer = new DataStr.PriorityActionForOtherPlayer();
		switch (ActName)
		{
		case "Revive":
			priorityActionForOtherPlayer.m_Action = "Revive";
			priorityActionForOtherPlayer.m_DisplayText = priorityActionForOtherPlayer.m_Action;
			priorityActionForOtherPlayer.m_ProcessText = "Reviving...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 5f;
			break;
		case "Stim":
			priorityActionForOtherPlayer.m_Action = "Stim";
			priorityActionForOtherPlayer.m_DisplayText = "Inject";
			priorityActionForOtherPlayer.m_ProcessText = "Injecting...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 1f;
			break;
		case "Invite":
			priorityActionForOtherPlayer.m_Action = "Invite";
			priorityActionForOtherPlayer.m_DisplayText = "Invite to expedition";
			priorityActionForOtherPlayer.m_ProcessText = "Inviting...";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 1f;
			break;
		case "Lit":
			priorityActionForOtherPlayer.m_Action = "Lit";
			priorityActionForOtherPlayer.m_DisplayText = "Ignite from player's fire";
			priorityActionForOtherPlayer.m_ProcessText = "";
			priorityActionForOtherPlayer.m_CancleOnMove = false;
			priorityActionForOtherPlayer.m_ActionDuration = 0f;
			break;
		case "Examine":
			priorityActionForOtherPlayer.m_Action = "Examine";
			priorityActionForOtherPlayer.m_DisplayText = "Examine";
			priorityActionForOtherPlayer.m_ProcessText = "Diagnosing...";
			priorityActionForOtherPlayer.m_CancleOnMove = true;
			priorityActionForOtherPlayer.m_ActionDuration = 1f;
			break;
		case "Borrow":
			priorityActionForOtherPlayer.m_Action = "Borrow";
			priorityActionForOtherPlayer.m_DisplayText = "Borrow";
			priorityActionForOtherPlayer.m_ProcessText = "Borrowing...";
			priorityActionForOtherPlayer.m_CancleOnMove = true;
			priorityActionForOtherPlayer.m_ActionDuration = 1f;
			break;
		}
		return priorityActionForOtherPlayer;
	}

	public static void LoadChatName(string _name = "")
	{
		string text = MPSaveManager.LoadMyName();
		if (ValidNickName(text))
		{
			MyChatName = text;
		}
		else if (ValidNickName(_name))
		{
			MyChatName = _name;
		}
		else
		{
			MyChatName = "Player";
		}
		MelonLogger.Msg("Your chat name is " + MyChatName);
	}

	public static void ForceLoadSlotByName(string searchname)
	{
		SaveSlotInfo saveSlotInfo = null;
		Il2CppSystem.Collections.Generic.List<SaveSlotInfo> sortedSaveSlots = SaveGameSystem.GetSortedSaveSlots(Episode.One, SaveSlotType.SANDBOX);
		for (int i = 0; i < sortedSaveSlots.Count; i++)
		{
			SaveSlotInfo saveSlotInfo2 = sortedSaveSlots[i];
			if (saveSlotInfo2.m_SaveSlotName == searchname)
			{
				saveSlotInfo = saveSlotInfo2;
				break;
			}
		}
		if (saveSlotInfo != null)
		{
			SaveGameSystem.SetCurrentSaveInfo(saveSlotInfo.m_Episode, saveSlotInfo.m_GameMode, saveSlotInfo.m_GameId, saveSlotInfo.m_SaveSlotName);
			GameManager.LoadSaveGameSlot(saveSlotInfo.m_SaveSlotName, saveSlotInfo.m_SaveChangelistVersion);
		}
	}

	public static void ForceLoadSlotForDs(string searchname, int Region, int Expereince, int Seed)
	{
		SaveGameSlots.LoadAllSavedGameFiles();
		bool flag = false;
		SaveSlotInfo saveSlotInfo = null;
		Il2CppSystem.Collections.Generic.List<SaveSlotInfo> sortedSaveSlots = SaveGameSystem.GetSortedSaveSlots(Episode.One, SaveSlotType.SANDBOX);
		for (int i = 0; i < sortedSaveSlots.Count; i++)
		{
			SaveSlotInfo saveSlotInfo2 = sortedSaveSlots[i];
			if (saveSlotInfo2.m_UserDefinedName == searchname)
			{
				flag = true;
				saveSlotInfo = saveSlotInfo2;
				break;
			}
		}
		if (!flag)
		{
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[Dedicated server] No save file with name " + searchname + " found!");
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[Dedicated server] Found " + SaveGameSlots.m_SaveSlots.Count + " saves");
			ForceCreateSlotForPlayingSilently(Region, Expereince, Seed, searchname);
			return;
		}
		SaveGameSlots.SetBaseNameForSave(saveSlotInfo.m_SaveSlotName, saveSlotInfo.m_SaveSlotName);
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Save found!");
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Save slot base name is " + SaveGameSlots.GetBaseNameForSave(saveSlotInfo.m_SaveSlotName));
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Save slot name " + saveSlotInfo.m_SaveSlotName);
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Save slot user defined name " + SaveGameSlots.GetUserDefinedSlotName(saveSlotInfo.m_SaveSlotName));
		SaveGameSystem.SetCurrentSaveInfo(saveSlotInfo.m_Episode, saveSlotInfo.m_GameMode, saveSlotInfo.m_GameId, saveSlotInfo.m_SaveSlotName);
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Selecting slot " + SaveGameSystem.GetCurrentSaveName());
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Save slot hash " + Shared.StableHash(SaveSlotCompat.LoadDataFromSlot(saveSlotInfo.m_SaveSlotName, "global")));
		GameManager.LoadSaveGameSlot(saveSlotInfo.m_SaveSlotName, saveSlotInfo.m_SaveChangelistVersion);
	}

	public static void ForceCreateSlotForPlayingSilently(int Reg, int Expereince, int Seed, string SlotName)
	{
		if (Seed == 0)
		{
			MelonLogger.Msg(System.ConsoleColor.Green, "[Dedicated server] Creating save slot " + SlotName + " with random seed");
		}
		else
		{
			MelonLogger.Msg(System.ConsoleColor.Green, "[Dedicated server] Creating save slot " + SlotName + " with seed " + Seed);
		}
		SaveGameSystem.SetCurrentSaveInfo(Episode.One, SaveSlotType.SANDBOX, SaveGameSlots.GetUnusedGameId(), null);
		ExperienceCompat.SetExperienceModeType((ExperienceModeType)Expereince);
		RegionCompat.SetStartRegion((Shared.GameRegion)Reg);
		SaveGameSlots.SetSlotDisplayName(SaveGameSystem.GetCurrentSaveName(), SlotName);
		GameManager.Instance().LaunchSandbox();
		if (Seed != 0)
		{
			GameManager.m_SceneTransitionData.m_GameRandomSeed = Seed;
		}
	}

	public static void ForceCreateSlotForPlaying(int Region, int Experience)
	{
		m_Panel_Sandbox.Enable(enable: false);
		PendingSave = new DataStr.SaveSlotSync
		{
			m_Episode = 0,
			m_SaveSlotType = 3,
			m_Seed = 0,
			m_ExperienceMode = Experience,
			m_Location = Region,
			m_FixedSpawnScene = "",
			m_FixedSpawnPosition = Vector3.zero
		};
		ShouldCreateSaveForHost = true;
		HostFromLobbyAfterLoader = true;
		ExperienceCompat.SetExperienceModeType((ExperienceModeType)Experience);
		LetChooseSpawnForClient(PendingSave);
	}

	public static void ForceLoadSlotForPlaying(string searchname)
	{
		bool flag = false;
		SaveSlotInfo saveSlotInfo = null;
		Il2CppSystem.Collections.Generic.List<SaveSlotInfo> sortedSaveSlots = SaveGameSystem.GetSortedSaveSlots(Episode.One, SaveSlotType.SANDBOX);
		for (int i = 0; i < sortedSaveSlots.Count; i++)
		{
			SaveSlotInfo saveSlotInfo2 = sortedSaveSlots[i];
			if (saveSlotInfo2.m_UserDefinedName == searchname)
			{
				flag = true;
				saveSlotInfo = saveSlotInfo2;
				break;
			}
		}
		if (!flag)
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "[-slot] No save file with name " + searchname + " found!");
			return;
		}
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[-slot] Save found! Loading...");
		SaveGameSlots.SetBaseNameForSave(saveSlotInfo.m_SaveSlotName, saveSlotInfo.m_SaveSlotName);
		SaveGameSystem.SetCurrentSaveInfo(saveSlotInfo.m_Episode, saveSlotInfo.m_GameMode, saveSlotInfo.m_GameId, saveSlotInfo.m_SaveSlotName);
		GameManager.LoadSaveGameSlot(saveSlotInfo.m_SaveSlotName, saveSlotInfo.m_SaveChangelistVersion);
	}

	public static void FinishStartingDsServer()
	{
		SteamConnect.Main.SetLobbyName(CustomServerName);
		Server.StartSteam(MaxPlayers);
		MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Server is ready! Have fun!");
	}

	public static void StartDedicatedServer(bool ds = true)
	{
		string text = "[Dedicated server]";
		if (!ds)
		{
			text = "[AutoStart server]";
		}
		DataStr.DedicatedServerData dedicatedServerData = Shared.LoadDedicatedServerConfig();
		if (ds)
		{
			MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Trying to load save file...");
			MyChatName = "DedicatedServer";
		}
		if (!SteamConnect.CanUseSteam && dedicatedServerData.UsingSteam)
		{
			dedicatedServerData.UsingSteam = false;
			MelonLogger.Msg(System.ConsoleColor.Red, text + " In server.json 'UsingSteam' set to true, but you using version of the game that not support Hosting with using Steam! If you use licence version of the game, but still see this, reboot steam.");
			MelonLogger.Msg(System.ConsoleColor.Magenta, text + " Forced changing 'UsingSteam' to false, and running regular server.");
		}
		if (!dedicatedServerData.UsingSteam)
		{
			if (ds)
			{
				PortForDSLoad = dedicatedServerData.Ports;
				HostUDDSAfterLoad = true;
				Shared.InitAllPlayers();
			}
			else
			{
				Shared.HostAServer(dedicatedServerData.Ports);
			}
		}
		else
		{
			CustomServerName = dedicatedServerData.ServerName;
			if (ds)
			{
				Shared.InitAllPlayers();
				SteamConnect.Main.MakeLobby(dedicatedServerData.SteamServerAccessibility, dedicatedServerData.MaxPlayers);
				SetP2PToLobbyForDSAfterLoad = true;
			}
			else
			{
				Server.StartSteam(MaxPlayers);
			}
		}
		if (ds)
		{
			ForceLoadSlotForDs(dedicatedServerData.SaveSlot.ToUpper(), dedicatedServerData.StartRegion, dedicatedServerData.ExperienceMode, dedicatedServerData.Seed);
		}
	}

	public static void Disable()
	{
		KillOnUpdate = true;
		KillEveryInGameMinute = true;
		KillEverySecond = true;
		HarmonyLib.Harmony.UnpatchAll();
	}

	private static void SetCustomSkillLevel(CustomSkills skillType)
	{
		Skill customSkill = GetCustomSkill(skillType);
		if (customSkill == null)
		{
			return;
		}
		int max = 5;
		if (uConsole.GetNumParameters() != 0 && uConsole.GetNumParameters() <= 1)
		{
			int num = Mathf.Clamp(uConsole.GetInt(), 1, max);
			int currentTierNumber = customSkill.GetCurrentTierNumber();
			customSkill.SetTier(num - 1, SkillsManager.PointAssignmentMode.AssignInAnyMode);
			int currentTierNumber2 = customSkill.GetCurrentTierNumber();
			if (currentTierNumber2 > currentTierNumber)
			{
				GameManager.GetSkillNotify().MaybeShowLevelUp(customSkill.m_SkillIconBackground, customSkill.m_DisplayName, GameManager.GetSkillsManager().GetTierName(currentTierNumber2), currentTierNumber2 + 1);
			}
		}
	}

	public static void SetFirstAidTier()
	{
		SetCustomSkillLevel(CustomSkills.FirstAid);
	}

	public static void SkipHourConsole()
	{
		if (iAmHost)
		{
			int h = 1;
			if (uConsole.GetNumParameters() != 0 || uConsole.GetNumParameters() == 1)
			{
				h = uConsole.GetInt();
			}
			Shared.SkipRTTime(h);
		}
	}

	public static void SetSanity()
	{
		SanityManager.m_CurrentSanity = uConsole.GetFloat();
	}

	public static void SetCanSeeSanity()
	{
		SanityManager.m_CanSeeSanity = !SanityManager.m_CanSeeSanity;
	}

	public static void ConsolePlaySound()
	{
		GameAudioManager.PlaySound(uConsole.GetString(), InterfaceManager.GetSoundEmitter());
	}

	public static void ConsoleRPC()
	{
		if (iAmHost && uConsole.GetNumParameters() != 0 && uConsole.GetNumParameters() <= 1)
		{
			string text = "rpc " + uConsole.GetString();
			MelonLogger.Msg("Console RPC request: " + text);
			Shared.ExecuteCommand(text);
		}
	}

	public static void DumpInterloperBans()
	{
		foreach (UnityEngine.Object item in Resources.LoadAll("", Il2CppType.Of<GameObject>()))
		{
			GameObject gameObject = item.Cast<GameObject>();
			if (gameObject != null && (bool)gameObject.GetComponent<DisableObjectForXPMode>() && gameObject.GetComponent<DisableObjectForXPMode>().m_XPModesToDisable.Contains(ExperienceModeType.Interloper))
			{
				MelonLogger.Msg("[DumpInterloperBans] " + item.name);
			}
		}
	}

	public override void OnApplicationStart()
	{
		SetAppBackgroundMode();
		MelonPreferences_Category coopPreferences = MelonPreferences.CreateCategory("SkyCoop");
		ItemDecayPreference = coopPreferences.CreateEntry("EnableItemDecay", false, "Enable item decay", "Co-op forces the decay scale to 0 (no spoilage or wear over time). true keeps the difficulty decay rate for this player.");
		DedicatedServerAppMode = System.Environment.GetCommandLineArgs().Contains("-dedicated") || Application.isBatchMode;
		ModsValidation.GetModsHash(Force: true);
		bool num = System.Environment.GetCommandLineArgs().Contains("-nosteam");
		System.Environment.GetCommandLineArgs().Contains("-noegs");
		PatchLoader.ApplyPatches(base.HarmonyInstance);
		if (SaveQuitGate == null)
		{
			SaveQuitGate = (System.Func<bool>)HandleWantsToQuit;
			Application.add_wantsToQuit(SaveQuitGate);
			MelonLogger.Msg("[SaveQuit] Pre-exit save gate registered.");
		}
		MelonLogger.Msg($"[{base.Info.Name}] Version {base.Info.Version} loaded!");
		LoadedBundle = AssetBundle.LoadFromFile(Path.GetFullPath("Mods/multiplayerstuff.unity3d").Replace(Path.DirectorySeparatorChar, '/'));
		if (LoadedBundle == null)
		{
			ReportUnusableBundle();
		}
		else
		{
			MelonLogger.Msg("Models loaded.");
		}
		if (num)
		{
			MelonLogger.Msg(System.ConsoleColor.DarkMagenta, "[SteamWorks.NET] Force no steam enabled");
		}
		else
		{
			RunOptionalStartupStep("Steam", SteamConnect.Init);
		}
		RunOptionalStartupStep("supporters list", delegate
		{
			Supporters.GetSupportersList();
		});
		if (DedicatedServerAppMode)
		{
			MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Please wait...");
			InputManager.m_InputDisableTime = float.PositiveInfinity;
			MPSaveManager.SaveRecentTimer = 30;
		}
		uConsole.RegisterCommand("oleg", (System.Action)LootEverything);
		uConsole.RegisterCommand("gimmekey", (System.Action)CreateDebugKey);
		uConsole.RegisterCommand("givemekey", (System.Action)CreateDebugKey);
		uConsole.RegisterCommand("skill_firstaid", (System.Action)SetFirstAidTier);
		uConsole.RegisterCommand("skip", (System.Action)SkipHourConsole);
		uConsole.RegisterCommand("rpc", (System.Action)ConsoleRPC);
		uConsole.RegisterCommand("set_sanity", (System.Action)SetSanity);
		uConsole.RegisterCommand("cansee_sanity", (System.Action)SetCanSeeSanity);
		uConsole.RegisterCommand("play_sound", (System.Action)ConsolePlaySound);
		CheatConsole.RegisterCommands();
		Comps.RegisterComponents();
		ExpeditionManager.InitClues();
		Shared.LoadInterloperReplace();
		TFHUD.Enabled = System.Environment.GetCommandLineArgs().Contains("-tfhud");
		MPSaveManager.AddSpecialItem(JSON.Dump(new ExpeditionManager.SpecialExpeditionItem()), "Template");
	}

	public static void LootEverything()
	{
		int num = 0;
		BreakDown[] array = UnityEngine.Object.FindObjectsOfType<BreakDown>();
		if (array != null)
		{
			int num2 = 0;
			BreakDown[] array2 = array;
			foreach (BreakDown breakDown in array2)
			{
				if (breakDown.enabled)
				{
					for (int j = 0; j < breakDown.m_YieldObjectUnits.Count; j++)
					{
						num += breakDown.m_YieldObjectUnits[j];
					}
					breakDown.DoBreakDown(spawnYieldObjects: true);
					num2++;
				}
			}
		}
		for (int k = 0; k < GearManager.m_Gear.Count; k++)
		{
			num++;
			GearItem item = GearManager.m_Gear[k];
			GameManager.GetPlayerManagerComponent().ProcessPickupItemInteraction(item, forceEquip: false, forceEquipImmediate: false, skipAudio: false);
		}
		for (int l = 0; l < ContainerManager.m_Containers.Count; l++)
		{
			Container container = ContainerManager.m_Containers[l];
			container.InstantiateContents();
			container.m_Inspected = true;
			new Il2CppSystem.Collections.Generic.List<GearItem>();
			for (int m = 0; m < container.m_Items.Count; m++)
			{
				GearItem gearItem = container.m_Items[m];
				if (gearItem != null)
				{
					num++;
					GameManager.GetPlayerManagerComponent().ProcessPickupItemInteraction(gearItem, forceEquip: false, forceEquipImmediate: false, skipAudio: false);
				}
			}
			container.m_Items.Clear();
		}
		MelonLogger.Msg("Oleg found and looted " + num + " gears");
		MelonLogger.Msg("Oleg found and looted " + num + " gears");
	}

	public static bool TryResetSaveExitForNewSession()
	{
		if (SaveExitPending)
		{
			return false;
		}
		QuitWithoutSaving = false;
		IReallyWantToQuit = false;
		NoMelonQuit = false;
		PendingSaveHashToSend = 0L;
		PendingSlotDiskCommit = null;
		AfterSavedDisconnect = null;
		PendingSaveExitAction = SaveExitAction.Quit;
		return true;
	}

	public static void RequestSaveBeforeDisconnect(System.Action afterDisconnect = null, bool allowOfflineWorld = false)
	{
		if (!SaveExitPending)
		{
			if (!(level_name != "Empty") || !(level_name != "Boot") || Shared.IsMainMenuScene(level_name) || (!InOnline() && !allowOfflineWorld) || DedicatedServerAppMode || IReallyWantToQuit || QuitWithoutSaving)
			{
				Disconnect(checkpoint: false);
				afterDisconnect?.Invoke();
				return;
			}
			PendingSaveExitAction = SaveExitAction.Disconnect;
			AfterSavedDisconnect = afterDisconnect;
			Pathes.QuitOnSave = true;
			QuitWithoutSaving = true;
			NoMelonQuit = false;
			PendingSaveHashToSend = 0L;
			SaveBeforeQuitRequested = true;
			DiscardRepeatPacket();
			MelonLogger.Msg("[SaveQuit] Save queued before disconnect; world and transport retained until disk commit.");
		}
	}

	internal static bool HandleWantsToQuit()
	{
		if (!IReallyWantToQuit && SaveExitPending)
		{
			RequestSaveBeforeQuit();
			return false;
		}
		if (IReallyWantToQuit || DedicatedServerAppMode || !InOnline() || level_name == "Empty" || level_name == "Boot" || Shared.IsMainMenuScene(level_name))
		{
			return true;
		}
		if (QuitWithoutSaving && !Pathes.QuitOnSave && !SaveBeforeQuitRequested && !QuitWhenSaveOperationFinished)
		{
			return true;
		}
		RequestSaveBeforeQuit();
		return false;
	}

	public static void RequestSaveBeforeQuit()
	{
		PendingSaveExitAction = SaveExitAction.Quit;
		AfterSavedDisconnect = null;
		if (!Pathes.QuitOnSave && !SaveBeforeQuitRequested && !QuitWhenSaveOperationFinished)
		{
			Pathes.QuitOnSave = true;
			QuitWithoutSaving = true;
			NoMelonQuit = false;
			SaveBeforeQuitRequested = true;
			MelonLogger.Msg("[SaveQuit] Save queued before the native quit lifecycle.");
		}
	}

	public override void OnApplicationQuit()
	{
		if ((!(level_name != "Empty") || !(level_name != "Boot") || Shared.IsMainMenuScene(level_name) || !InOnline() || IReallyWantToQuit || DedicatedServerAppMode || (QuitWithoutSaving && !Pathes.QuitOnSave && !SaveBeforeQuitRequested && !QuitWhenSaveOperationFinished)) && InOnline())
		{
			MelonLogger.Msg("[CLIENT] Disconnect cause quit game");
			Disconnect();
		}
	}

	public static Container MakeDeathCreate(DataStr.DeathContainerData data)
	{
		if (ObjectGuidManager.Lookup(data.m_Guid) != null)
		{
			MelonLogger.Msg("Death container  " + data.m_Guid + " already exist");
			return null;
		}
		GameObject gearItemObject = GetGearItemObject(data.m_ContainerPrefab);
		if (gearItemObject == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, data.m_Position, data.m_Rotation);
		InaccessibleGearContainer component = gameObject.GetComponent<InaccessibleGearContainer>();
		if ((bool)component)
		{
			UnityEngine.Object.Destroy(component);
		}
		if (gameObject.GetComponent<GearItem>() != null)
		{
			UnityEngine.Object.Destroy(gameObject.GetComponent<GearItem>());
		}
		if (gameObject.GetComponent<Inspect>() != null)
		{
			UnityEngine.Object.Destroy(gameObject.GetComponent<Inspect>());
		}
		if (gameObject.GetComponent<CarryingCapacityBuff>() != null)
		{
			UnityEngine.Object.Destroy(gameObject.GetComponent<CarryingCapacityBuff>());
		}
		if (gameObject.GetComponent<NarrativeCollectibleItem>() != null)
		{
			UnityEngine.Object.Destroy(gameObject.GetComponent<NarrativeCollectibleItem>());
		}
		if (gameObject.GetComponent<Rigidbody>() != null)
		{
			UnityEngine.Object.Destroy(gameObject.GetComponent<Rigidbody>());
		}
		ObjectGuid objectGuid = gameObject.GetComponent<ObjectGuid>();
		if (objectGuid == null)
		{
			objectGuid = gameObject.AddComponent<ObjectGuid>();
		}
		objectGuid.Set(data.m_Guid);
		gameObject.SetActive(value: true);
		Container container = gameObject.GetComponent<Container>();
		if (container == null)
		{
			container = gameObject.AddComponent<Container>();
			container.m_CloseAudio = "Play_SndGenCanvasBagZipperClose1";
			container.m_SearchAudio = "Play_SearchCloth";
			container.m_OpenAudio = "Play_SndGenCanvasBagZipperOpen1";
			container.m_LocalizedDisplayName = new LocalizedString();
			container.m_LocalizedDisplayName.m_LocalizationID = "GAMEPLAY_TechnicalBackpack";
		}
		container.m_Capacity = GameCompat.Kilograms(0f);
		container.m_Inspected = true;
		container.m_StartInspected = true;
		Comps.DeathDropContainer deathDropContainer = gameObject.GetComponent<Comps.DeathDropContainer>();
		if (deathDropContainer == null)
		{
			deathDropContainer = gameObject.AddComponent<Comps.DeathDropContainer>();
		}
		deathDropContainer.m_Owner = data.m_Owner;
		deathDropContainer.m_DeathTime = data.m_DeathTime;
		BoxCollider component2 = gameObject.GetComponent<BoxCollider>();
		if ((bool)component2)
		{
			component2.isTrigger = true;
		}
		return container;
	}

	public static void RemoveDeathContainer(string GUID, string Scene)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if (gameObject == null)
		{
			MelonLogger.Msg("Death container " + GUID + " not exist, can't be deleted");
		}
		else
		{
			UnityEngine.Object.Destroy(gameObject);
		}
		if (!iAmHost)
		{
			return;
		}
		foreach (DataStr.DeathContainerData deathCreate in DeathCreates)
		{
			if (deathCreate.m_Guid == GUID)
			{
				DeathCreates.Remove(deathCreate);
				break;
			}
		}
	}

	public static void DropAll()
	{
		DataStr.DeathContainerData deathContainerData = new DataStr.DeathContainerData();
		deathContainerData.m_Guid = ObjectGuidManager.GenerateNewGuidString();
		deathContainerData.m_LevelKey = level_guid;
		deathContainerData.m_Position = GameManager.GetPlayerTransform().position;
		deathContainerData.m_Rotation = GameManager.GetPlayerTransform().rotation;
		deathContainerData.m_Owner = MyChatName;
		deathContainerData.m_ContainerPrefab = "CONTAINER_BackPack";
		deathContainerData.m_DeathTime = MinutesFromStartServer;
		bool flag = false;
		if (GameManager.GetIceCrackingManager().FellInRecently())
		{
			MelonLogger.Msg("Died from falling in water");
			deathContainerData.m_Position = GameManager.GetIceCrackingManager().GetRespawnLocation();
			flag = true;
		}
		Container container = MakeDeathCreate(deathContainerData);
		if (!container)
		{
			return;
		}
		Inventory inventoryComponent = GameManager.GetInventoryComponent();
		System.Collections.Generic.List<GearItem> list = new System.Collections.Generic.List<GearItem>();
		for (int i = 0; i < inventoryComponent.m_Items.Count; i++)
		{
			GearItem gearItem = inventoryComponent.m_Items[i];
			if ((bool)gearItem)
			{
				if (flag && (bool)gearItem.m_ClothingItem)
				{
					gearItem.m_ClothingItem.SetFullyWet();
				}
				list.Add(gearItem);
				if ((bool)GameManager.GetPlayerManagerComponent().m_ItemInHands && GameManager.GetPlayerManagerComponent().m_ItemInHands == gearItem)
				{
					GameManager.GetPlayerManagerComponent().UnequipImmediate(saveLastItem: false);
				}
			}
		}
		foreach (GearItem item in list)
		{
			if (!item.m_HandheldShortwaveItem && item.GetGearName() != "GEAR_SCHeatPack" && item.GetGearName() != "GEAR_SCHeatPackC" && item.GetGearName() != "GEAR_SCEmergencyRation")
			{
				if ((bool)item.m_WaterSupply)
				{
					GameCompat.Panel<Panel_PickWater>().TransferAllWaterInventoryToContainer(container, item.m_WaterSupply);
					continue;
				}
				if ((bool)item.m_StackableItem && container.AddToExistingStackable(item, item.GetNormalizedCondition(), item.m_StackableItem.m_Units))
				{
					inventoryComponent.DestroyGear(item.gameObject);
					continue;
				}
				container.AddGear(item);
				inventoryComponent.RemoveGear(item.gameObject, toContainer: true);
			}
		}
		string text = container.Serialize();
		if (iAmHost)
		{
			MPSaveManager.SaveContainer(deathContainerData.m_LevelKey, deathContainerData.m_Guid, Shared.CompressString(text));
			DeathCreates.Add(deathContainerData);
			ServerSend.ADDDEATHCONTAINER(deathContainerData, deathContainerData.m_LevelKey, 0);
		}
		else if (sendMyPosition)
		{
			DoPleaseWait("Please wait...", "Sending container data...");
			Shared.SendContainerData(Shared.CompressString(text), deathContainerData.m_LevelKey, deathContainerData.m_Guid, text);
			using Packet packet = new Packet(122);
			packet.Write(deathContainerData);
			SendUDPData(packet);
		}
		container.DestroyAllGear();
		MelonLogger.Msg("Everything dropped");
	}

	public static System.Collections.Generic.List<int> SaveCustomSkills()
	{
		SkillsManager skillsManager = GameManager.GetSkillsManager();
		System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
		for (int i = 9; i != skillsManager.m_Skills.Count; i++)
		{
			list.Add(skillsManager.m_Skills[i].GetPoints());
		}
		return list;
	}

	public static void LoadCustomSkills(System.Collections.Generic.List<int> SkillPoints)
	{
		SkillsManager skillsManager = GameManager.GetSkillsManager();
		for (int i = 0; i != SkillPoints.Count; i++)
		{
			skillsManager.m_Skills[9 + i].SetPoints(SkillPoints[i], SkillsManager.PointAssignmentMode.AssignInAnyMode);
		}
	}

	public static Skill GetCustomSkill(CustomSkills SkillType)
	{
		Il2CppSystem.Collections.Generic.List<Skill>.Enumerator enumerator = GameManager.GetSkillsManager().m_Skills.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Skill current = enumerator.Current;
			if ((bool)current && current.m_SkillType == (SkillType)SkillType)
			{
				return current;
			}
		}
		return null;
	}

	public static void IncressCustomSkill(CustomSkills Skill, int Points = 1)
	{
		SkillsManager skillsManager = GameManager.GetSkillsManager();
		Skill customSkill = GetCustomSkill(Skill);
		int currentTierNumber = customSkill.GetCurrentTierNumber();
		customSkill.IncrementPoints(Points, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		int currentTierNumber2 = customSkill.GetCurrentTierNumber();
		if (currentTierNumber != currentTierNumber2)
		{
			GameManager.GetSkillNotify().MaybeShowLevelUp(customSkill.m_SkillIcon, customSkill.m_DisplayName, skillsManager.GetTierName(currentTierNumber2), currentTierNumber2);
		}
		else
		{
			GameManager.GetSkillNotify().MaybeShowPointIncrease(customSkill.m_SkillIcon);
		}
	}

	public static void AddNewSkills(SkillsManager Manager)
	{
		Skill_IceFishing skill_IceFishing = new GameObject
		{
			name = "Skill_FirstAid"
		}.AddComponent<Skill_IceFishing>();
		skill_IceFishing.m_LocalizedDisplayName = new LocalizedString();
		skill_IceFishing.m_LocalizedDisplayName.m_LocalizationID = "First Aid";
		skill_IceFishing.m_SkillIcon = "ico_Radial_firstAid";
		skill_IceFishing.m_SkillIconBackground = "ico_skill_large_firstAid";
		skill_IceFishing.m_SkillImage = "ico_skill_large_firstAid";
		skill_IceFishing.m_SkillType = (SkillType)10;
		skill_IceFishing.m_TierLocalizedDescriptions = new LocalizedString[5];
		skill_IceFishing.m_TierLocalizedBenefits = new LocalizedString[5];
		for (int i = 0; i < skill_IceFishing.m_TierLocalizedDescriptions.Length; i++)
		{
			skill_IceFishing.m_TierLocalizedDescriptions[i] = new LocalizedString();
			skill_IceFishing.m_TierLocalizedBenefits[i] = new LocalizedString();
		}
		skill_IceFishing.m_TierLocalizedDescriptions[0].m_LocalizationID = "You have basic knowledge of how to perform first aid. But you need more practice.";
		skill_IceFishing.m_TierLocalizedDescriptions[1].m_LocalizationID = "Your practical knowledge is still limited to just a few incidents. But you can already do a much better job of easier your patient's pain.";
		skill_IceFishing.m_TierLocalizedDescriptions[2].m_LocalizationID = "Your knowledge is confirmed by practice. Those who you help recover much faster.";
		skill_IceFishing.m_TierLocalizedDescriptions[3].m_LocalizationID = "Your knowledge is enough to provide fast and high-quality assistance. Your patients get better right in front of your eyes.";
		skill_IceFishing.m_TierLocalizedDescriptions[4].m_LocalizationID = "You have seen everything. You can consider yourself an expert.";
		string localizationID = "Treated by you patients will get minor regeneration.";
		string text = "Treated by you patients will get improved regeneration.";
		string text2 = "Treated by you patients will get fast regeneration.";
		string text3 = "Treatment instantly restore 3% of health.";
		string text4 = "Treatment instantly restore 5% of health.";
		string text5 = "Treating yourself gives minor regeneration.";
		string text6 = "Your treatment is most effective even without medkit.";
		skill_IceFishing.m_TierLocalizedBenefits[0].m_LocalizationID = localizationID;
		skill_IceFishing.m_TierLocalizedBenefits[1].m_LocalizationID = text;
		skill_IceFishing.m_TierLocalizedBenefits[2].m_LocalizationID = text + "\n" + text3;
		skill_IceFishing.m_TierLocalizedBenefits[3].m_LocalizationID = text + "\n" + text4 + "\n" + text5;
		skill_IceFishing.m_TierLocalizedBenefits[4].m_LocalizationID = text2 + "\n" + text4 + "\n" + text5 + "\n" + text6;
		skill_IceFishing.m_TierPoints = new int[5];
		skill_IceFishing.m_TierPoints[0] = 0;
		skill_IceFishing.m_TierPoints[1] = 20;
		skill_IceFishing.m_TierPoints[2] = 35;
		skill_IceFishing.m_TierPoints[3] = 45;
		skill_IceFishing.m_TierPoints[4] = 55;
		Manager.m_Skills.Add(skill_IceFishing);
	}

	public override void OnLevelWasInitialized(int level)
	{
		if (KillOnUpdate)
		{
			return;
		}
		OpenablesObjs.Clear();
		FarTerritory.OnSceneChanged();
		MelonLogger.Msg("Level initialized: " + level);
		levelid = level;
		if (levelid == 0 && sendMyPosition)
		{
			using Packet packet = new Packet(139);
			packet.Write(_value: true);
			SendUDPData(packet);
		}
		MelonLogger.Msg("Level name: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
		level_name = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		MyTicksOnScene = 0;
		CoopContainerFlow.Reset();
		SleepRendezvousFix.Reset();
		WildlifeAuthorityScene = "";
		if (sendMyPosition && !iAmHost)
		{
			AnimalsController = false;
		}
		if (!Shared.IsMainMenuScene(level_name) && level_name != "Boot" && level_name != "Empty")
		{
			CheckHaveBookMod();
		}
		Shared.IsMainMenuScene(level_name);
		DisableOriginalAnimalSpawns();
		Shared.InitAllPlayers();
	}

	private static void RunOptionalStartupStep(string what, System.Action step)
	{
		try
		{
			step();
		}
		catch (System.Exception ex)
		{
			MelonLogger.Warning("[SkyCoop] Skipped " + what + " during startup: " + ex.GetType().Name + ": " + ex.Message);
			MelonLogger.Warning("[SkyCoop] The rest of the mod is unaffected.");
		}
	}

	private static void ReportUnusableBundle()
	{
		MelonLogger.Error("[SkyCoop] Could not load Mods\\multiplayerstuff.unity3d.");
		if (!File.Exists("Mods\\multiplayerstuff.unity3d"))
		{
			MelonLogger.Error("[SkyCoop] The file is not in the Mods folder. Multiplayer UI and player models are unavailable until it is.");
			return;
		}
		MelonLogger.Error("[SkyCoop] The bundle exists but failed to load. Check its absolute path, file integrity and Unity player log. Tested v13.2 assets load on Unity " + Application.unityVersion + ".");
		MelonLogger.Error("[SkyCoop] Multiplayer UI and player models are unavailable until the load succeeds.");
	}

	public static T BundleAsset<T>(string name) where T : UnityEngine.Object
	{
		if (LoadedBundle == null)
		{
			return null;
		}
		T val = LoadedBundle.LoadAsset<T>(name);
		if (val == null && ReportedMissingAssets.Add(name))
		{
			MelonLogger.Warning("[Object loader] Bundle has no asset named " + name);
		}
		return val;
	}

	public static GameObject SpawnModAsset(GameObject prefab)
	{
		if (!(prefab == null))
		{
			return UnityEngine.Object.Instantiate(prefab);
		}
		return null;
	}

	public static GameObject SpawnModAsset(GameObject prefab, Transform parent)
	{
		if (!(prefab == null))
		{
			return UnityEngine.Object.Instantiate(prefab, parent);
		}
		return null;
	}

	public static GameObject SpawnModAsset(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
	{
		if (!(prefab == null))
		{
			return UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
		}
		return null;
	}

	public static GameObject MakeModObject(string _name, Transform newparent = null)
	{
		GameObject gameObject = BundleAsset<GameObject>(_name);
		if (gameObject == null)
		{
			MelonLogger.Msg("[Object loader] Can't load asset. Has try load " + _name);
			return null;
		}
		GameObject gameObject2 = null;
		gameObject2 = ((!(newparent == null)) ? SpawnModAsset(gameObject, newparent) : SpawnModAsset(gameObject));
		if (gameObject2 == null)
		{
			MelonLogger.Msg("[Object loader] Object is null  " + _name);
		}
		return gameObject2;
	}

	public static bool IsZero(float a)
	{
		return Mathf.Abs(a) <= 0.0001f;
	}

	public static void SimUDPHandle(int _packetId, Packet _packet, int _from)
	{
		Server.packetHandlers[_packetId](_from, _packet);
	}

	public static void SimUDPHandle_Client(int _packetId, Packet _packet)
	{
		System.Collections.Generic.Dictionary<int, ClientUser.PacketHandler> packetHandlers = ClientUser.packetHandlers;
		if (packetHandlers != null && packetHandlers.TryGetValue(_packetId, out var value))
		{
			value?.Invoke(_packet);
		}
	}

	public static void NoSyncFurtitureDestory(BreakDown breakDown, string GUID, bool ReBake)
	{
		breakDown.gameObject.SetActive(value: false);
		if ((bool)breakDown.gameObject.transform.parent && (bool)breakDown.gameObject.transform.parent.GetComponent<RadialObjectSpawner>())
		{
			BreakDown.m_BreakDownObjects.Remove(breakDown);
			breakDown.gameObject.transform.parent.GetComponent<RadialObjectSpawner>().RemoveFromSpawns(breakDown.gameObject);
			RadialSpawnManager.ReturnToObjectPool(breakDown.gameObject);
		}
		breakDown.StickSurfaceObjectsToGround(ignoreGearItems: false);
		if (ReBake)
		{
			BakePreSpawnedGearsList();
		}
		MissionUtils.PostObjectEvent(breakDown.gameObject, MissionObjectEvent.ObjectBrokenDown);
		MelonLogger.Msg("Furn " + GUID + " removed");
	}

	public static void NoSyncFurtitureRepair(BreakDown breakDown, string GUID)
	{
		breakDown.gameObject.SetActive(value: true);
		MelonLogger.Msg("Furn " + GUID + " repaired");
	}

	public static string GetBreakDownSound(DataStr.BrokenFurnitureSync FindData)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(FindData.m_Guid);
		if (gameObject != null)
		{
			if ((bool)gameObject.GetComponent<BreakDown>())
			{
				if (!(FindData.m_ParentGuid != ""))
				{
					return gameObject.GetComponent<BreakDown>().m_BreakDownAudio;
				}
				if (gameObject.transform.parent != null && gameObject.transform.parent.GetComponent<ObjectGuid>() != null && gameObject.transform.parent.GetComponent<ObjectGuid>().Get() == FindData.m_ParentGuid)
				{
					return gameObject.GetComponent<BreakDown>().m_BreakDownAudio;
				}
			}
			else if ((bool)gameObject.GetComponent<Comps.FakeRockCache>())
			{
				return "Play_RockCache";
			}
		}
		return "";
	}

	public static void RemoveBrokenFurniture(string GUID, string ParentGUID, bool ReBake = true)
	{
		MelonLogger.Msg("Going to remove furn " + GUID + ParentGUID);
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if (!(gameObject != null))
		{
			return;
		}
		if (ParentGUID != "")
		{
			if (gameObject.transform.parent != null && gameObject.transform.parent.GetComponent<ObjectGuid>() != null && gameObject.transform.parent.GetComponent<ObjectGuid>().Get() == ParentGUID)
			{
				NoSyncFurtitureDestory(gameObject.GetComponent<BreakDown>(), GUID + ParentGUID, ReBake);
				FoundSomethingToBreak = true;
			}
		}
		else
		{
			NoSyncFurtitureDestory(gameObject.GetComponent<BreakDown>(), GUID + ParentGUID, ReBake);
			FoundSomethingToBreak = true;
		}
	}

	public static void RepairBrokenFurniture(string GUID, string ParentGUID)
	{
		MelonLogger.Msg("Going to repair furn " + GUID + ParentGUID);
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if (!(gameObject != null))
		{
			return;
		}
		if (ParentGUID != "")
		{
			if (gameObject.transform.parent != null && gameObject.transform.parent.GetComponent<ObjectGuid>() != null && gameObject.transform.parent.GetComponent<ObjectGuid>().Get() == ParentGUID)
			{
				NoSyncFurtitureRepair(gameObject.GetComponent<BreakDown>(), GUID + ParentGUID);
				FoundSomethingToBreak = true;
			}
		}
		else
		{
			NoSyncFurtitureRepair(gameObject.GetComponent<BreakDown>(), GUID + ParentGUID);
			FoundSomethingToBreak = true;
		}
	}

	public static void BakePreSpawnedGearsList()
	{
		PreSpawnedGears.Clear();
		for (int i = 0; i < GearManager.m_Gear.Count; i++)
		{
			GearItem gearItem = GearManager.m_Gear[i];
			GameObject gameObject = gearItem.gameObject;
			long pickedGearKey = MPSaveManager.GetPickedGearKey(gameObject.transform.position);
			if (!gearItem.m_BeenInPlayerInventory && gameObject.activeSelf && !gearItem.m_InsideContainer && !PreSpawnedGears.ContainsKey(pickedGearKey))
			{
				PreSpawnedGears.Add(pickedGearKey, gameObject);
			}
		}
	}

	public static void RemovePickedGear(long Key)
	{
		RemovePickedGear(Key, Vector3.zero, "");
	}

	// The exact position key misses items that were not in the list baked at scene load (sticks and stones the game
	// spawns around players later) or that settled a little differently on the other machine; those are matched by
	// name and distance instead.
	public static void RemovePickedGear(long Key, Vector3 position, string gearName)
	{
		if (ServerConfig.m_DuppedSpawns)
		{
			return;
		}
		if (PreSpawnedGears.TryGetValue(Key, out var value) && value != null && (bool)value.GetComponent<GearItem>() && !value.GetComponent<GearItem>().m_BeenInPlayerInventory && value.gameObject.activeSelf)
		{
			value.gameObject.SetActive(value: false);
			MelonLogger.Msg("[RemovePickedGear] Key " + Key);
			return;
		}
		if (string.IsNullOrEmpty(gearName) || position == Vector3.zero)
		{
			MelonLogger.Msg("[RemovePickedGear] Key " + Key + " not found");
			return;
		}
		GearItem best = null;
		float bestDistance = 0.75f;
		for (int i = 0; i < GearManager.m_Gear.Count; i++)
		{
			GearItem gearItem = GearManager.m_Gear[i];
			if (gearItem == null || gearItem.m_BeenInPlayerInventory || gearItem.m_InsideContainer || !gearItem.gameObject.activeInHierarchy || gearItem.GetComponent<Comps.DroppedGearDummy>() != null)
			{
				continue;
			}
			if (!string.Equals(gearItem.GetGearName(), gearName, System.StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			float distance = Vector3.Distance(gearItem.transform.position, position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = gearItem;
			}
		}
		if (best != null)
		{
			RadialObjectSpawner spawner = (best.transform.parent != null) ? best.transform.parent.GetComponent<RadialObjectSpawner>() : null;
			if (spawner != null)
			{
				spawner.RemoveFromSpawns(best.gameObject);
			}
			best.gameObject.SetActive(value: false);
			MelonLogger.Msg("[RemovePickedGear] " + gearName + " matched by distance " + bestDistance.ToString("0.00") + (spawner != null ? " (radial spawn)" : ""));
		}
		else
		{
			MelonLogger.Msg("[RemovePickedGear] Key " + Key + " " + gearName + " not found");
		}
	}

	public static void UpdateDeployedRopes()
	{
		for (int i = 0; i < RopeAnchorPoint.s_RopeAnchorPoints.Count; i++)
		{
			RopeAnchorPoint ropeAnchorPoint = RopeAnchorPoint.s_RopeAnchorPoints[i];
			DataStr.ClimbingRopeSync climbingRopeSync = new DataStr.ClimbingRopeSync();
			climbingRopeSync.m_LevelID = levelid;
			climbingRopeSync.m_LevelGUID = level_guid;
			climbingRopeSync.m_Position = ropeAnchorPoint.gameObject.transform.position;
			if (!DeployedRopes.Contains(climbingRopeSync))
			{
				continue;
			}
			DataStr.ClimbingRopeSync climbingRopeSync2 = new DataStr.ClimbingRopeSync();
			for (int j = 0; j < DeployedRopes.Count; j++)
			{
				DataStr.ClimbingRopeSync climbingRopeSync3 = DeployedRopes[j];
				if (climbingRopeSync3.m_Position == climbingRopeSync.m_Position && climbingRopeSync3.m_LevelID == levelid && climbingRopeSync3.m_LevelGUID == level_guid)
				{
					climbingRopeSync2 = climbingRopeSync3;
					break;
				}
			}
			if (!(ropeAnchorPoint.gameObject.transform.position == climbingRopeSync2.m_Position))
			{
				continue;
			}
			MelonLogger.Msg("Processing synced rope " + climbingRopeSync2.m_Position.x + " Y " + climbingRopeSync2.m_Position.y + " Z " + climbingRopeSync2.m_Position.z);
			if (climbingRopeSync2.m_Deployed == ropeAnchorPoint.m_RopeDeployed && climbingRopeSync2.m_Snapped == ropeAnchorPoint.m_RopeSnapped)
			{
				MelonLogger.Msg("Rope already has desired state");
			}
			else if (climbingRopeSync2.m_Deployed)
			{
				ropeAnchorPoint.SetRopeActiveState(climbingRopeSync2.m_Deployed, isInteraction: false);
				ropeAnchorPoint.SetRopeActiveState(state: true, isInteraction: true);
				ropeAnchorPoint.m_RopeDeployed = true;
				if (climbingRopeSync2.m_Snapped && !ropeAnchorPoint.m_RopeSnapped)
				{
					ropeAnchorPoint.SetRopeActiveState(state: false, isInteraction: false);
					if (ropeAnchorPoint.m_RopeShort != null && ropeAnchorPoint.m_RopeShort.gameObject != null)
					{
						ropeAnchorPoint.m_RopeShort.gameObject.SetActive(value: false);
					}
					ropeAnchorPoint.m_RopeSnapped = true;
				}
			}
			else
			{
				if (climbingRopeSync2.m_Deployed)
				{
					continue;
				}
				ropeAnchorPoint.m_RopeDeployed = false;
				ropeAnchorPoint.SetRopeActiveState(state: false, isInteraction: true);
				if (ropeAnchorPoint.m_RopeSnapped && !climbingRopeSync2.m_Snapped)
				{
					if (ropeAnchorPoint.m_RopeShort != null && ropeAnchorPoint.m_RopeShort.gameObject != null)
					{
						ropeAnchorPoint.m_RopeShort.gameObject.SetActive(value: false);
					}
					ropeAnchorPoint.m_RopeSnapped = false;
				}
			}
		}
	}

	public static void CheckHaveBookMod()
	{
		GameObject gameObject = GameObject.Find("/CHARACTER_FPSPlayer/WeaponView/WeaponCamera/hands_with_book(Clone)");
		if ((bool)gameObject)
		{
			ModdedHandsBook = gameObject;
		}
		string text = "/CHARACTER_FPSPlayer/NEW_FPHand_Rig/GAME_DATA/Origin/HipJoint/Chest_Joint/Camera_Weapon_Offset/Shoulder_Joint/Shoulder_Joint_Offset/Right_Shoulder_Joint_Offset/RightClavJoint/RightShoulderJoint/RightElbowJoint/RightWristJoint/RightPalm/right_prop_point/";
		GameObject gameObject2 = GameObject.Find(text);
		if (gameObject2 == null)
		{
			return;
		}
		if (!VanilaRadio && (bool)gameObject2 && ViewModelRadio == null)
		{
			GameObject gearItemObject = GetGearItemObject("GEAR_HandheldShortwave");
			if (gearItemObject == null)
			{
				return;
			}
			ViewModelRadio = UnityEngine.Object.Instantiate(gearItemObject, gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
			ViewModelRadio.name = "FPH_HandheldRadio";
			ViewModelRadio.SetActive(value: false);
			ViewModelRadioNeedle = ViewModelRadio.transform.GetChild(0).GetChild(0).GetChild(1)
				.gameObject;
			if (ViewModelRadio.GetComponent<GearItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelRadio.GetComponent<GearItem>());
			}
			if (ViewModelRadio.GetComponent<FirstPersonItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelRadio.GetComponent<FirstPersonItem>());
			}
			if (ViewModelRadio.GetComponent<HoverIconsToShow>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelRadio.GetComponent<HoverIconsToShow>());
			}
			if (ViewModelRadio.GetComponent<Inspect>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelRadio.GetComponent<Inspect>());
			}
			if (ViewModelRadio.GetComponent<Rigidbody>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelRadio.GetComponent<Rigidbody>());
			}
			if (ViewModelRadio.GetComponent<LODGroup>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelRadio.GetComponent<LODGroup>());
			}
			GameObject gameObject3 = new GameObject("LIGHT");
			gameObject3.transform.SetParent(ViewModelRadio.transform);
			gameObject3.AddComponent<Light>().color = Color.green;
			gameObject3.SetActive(value: false);
			ViewModelRadioLED = gameObject3;
		}
		GameObject.Find("/CHARACTER_FPSPlayer");
		ViewModelHatchet = GameObject.Find(text + "mesh_hatchet");
		ViewModelHatchet2 = GameObject.Find(text + "mesh_Hatchet_improvised");
		ViewModelKnife = GameObject.Find(text + "mesh_knife");
		ViewModelKnife2 = GameObject.Find(text + "mesh_knife_improvised");
		ViewModelPrybar = GameObject.Find(text + "mesh_prybar");
		ViewModelHammer = GameObject.Find(text + "mesh_hammer");
		ViewModelStone = GameObject.Find(text + "FPH_Stone(Clone)");
		ViewModelDummy = GameObject.Find(text + "FPH_Flare(Clone)");
		if (ViewModelShovel == null)
		{
			GameObject gearItemObject2 = GetGearItemObject("GEAR_Shovel");
			if (gearItemObject2 == null)
			{
				return;
			}
			ViewModelShovel = UnityEngine.Object.Instantiate(gearItemObject2, gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
			ViewModelShovel.name = "FPH_Shovel";
			ViewModelShovel.SetActive(value: false);
			if (ViewModelShovel.GetComponent<GearItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelShovel.GetComponent<GearItem>());
			}
			if (ViewModelShovel.GetComponent<FirstPersonItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelShovel.GetComponent<FirstPersonItem>());
			}
			if (ViewModelShovel.GetComponent<HoverIconsToShow>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelShovel.GetComponent<HoverIconsToShow>());
			}
			if (ViewModelShovel.GetComponent<Inspect>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelShovel.GetComponent<Inspect>());
			}
			if (ViewModelShovel.GetComponent<Rigidbody>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelShovel.GetComponent<Rigidbody>());
			}
			if (ViewModelShovel.GetComponent<LODGroup>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelShovel.GetComponent<LODGroup>());
			}
			ViewModelShovel.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
		}
		if (ViewModelFireAxe == null)
		{
			GameObject gearItemObject3 = GetGearItemObject("GEAR_FireAxe");
			if (gearItemObject3 == null)
			{
				return;
			}
			ViewModelFireAxe = UnityEngine.Object.Instantiate(gearItemObject3, gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
			ViewModelFireAxe.name = "FPH_FireAxe";
			ViewModelFireAxe.SetActive(value: false);
			if (ViewModelFireAxe.GetComponent<GearItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<GearItem>());
			}
			if (ViewModelFireAxe.GetComponent<ForageItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<ForageItem>());
			}
			if (ViewModelFireAxe.GetComponent<HoverIconsToShow>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<HoverIconsToShow>());
			}
			if (ViewModelFireAxe.GetComponent<Inspect>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<Inspect>());
			}
			if (ViewModelFireAxe.GetComponent<DegradeOnUse>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<DegradeOnUse>());
			}
			if (ViewModelFireAxe.GetComponent<Repairable>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<Repairable>());
			}
			if (ViewModelFireAxe.GetComponent<BodyHarvestItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<BodyHarvestItem>());
			}
			if (ViewModelFireAxe.GetComponent<Rigidbody>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<Rigidbody>());
			}
			if (ViewModelFireAxe.GetComponent<CanOpeningItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<CanOpeningItem>());
			}
			if (ViewModelFireAxe.GetComponent<LODGroup>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelFireAxe.GetComponent<LODGroup>());
			}
			ViewModelFireAxe.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
			ViewModelFireAxe.transform.localPosition = new Vector3(0f, 0.15f, 0.01f);
		}
		if (ViewModelJeremiahKnife == null)
		{
			GameObject gearItemObject4 = GetGearItemObject("GEAR_JeremiahKnife");
			if (gearItemObject4 == null)
			{
				return;
			}
			ViewModelJeremiahKnife = UnityEngine.Object.Instantiate(ViewModelKnife, ViewModelKnife.transform.position, ViewModelKnife.transform.rotation, gameObject2.transform);
			ViewModelJeremiahKnife.name = "FPH_JeremiahKnife";
			ViewModelJeremiahKnife.SetActive(value: false);
			ViewModelJeremiahKnife.GetComponent<MeshRenderer>().SetMaterial(gearItemObject4.transform.GetChild(0).GetComponent<MeshRenderer>().materials[0]);
		}
		if (ViewModelBolt == null)
		{
			ViewModelBolt = SpawnModAsset(BundleAsset<GameObject>("Bolt"), gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
			ViewModelBolt.name = "FPH_Bolt";
			ViewModelBolt.transform.localPosition = new Vector3(0.02f, 0.02f, -0.01f);
			ViewModelBolt.transform.localRotation = new Quaternion(0.3304f, 0.9077f, -0.0885f, -0.2432f);
			ViewModelBolt.transform.localScale = new Vector3(0.045f, 0.045f, 0.045f);
			ViewModelBolt.SetActive(value: false);
		}
		if (ViewModelHackSaw == null)
		{
			GameObject gearItemObject5 = GetGearItemObject("GEAR_Hacksaw");
			if (gearItemObject5 == null)
			{
				return;
			}
			ViewModelHackSaw = UnityEngine.Object.Instantiate(gearItemObject5, gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
			ViewModelHackSaw.name = "FPH_Hacksaw";
			ViewModelHackSaw.SetActive(value: false);
			if (ViewModelHackSaw.GetComponent<GearItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<GearItem>());
			}
			if (ViewModelHackSaw.GetComponent<ForageItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<ForageItem>());
			}
			if (ViewModelHackSaw.GetComponent<HoverIconsToShow>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<HoverIconsToShow>());
			}
			if (ViewModelHackSaw.GetComponent<Inspect>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<Inspect>());
			}
			if (ViewModelHackSaw.GetComponent<DegradeOnUse>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<DegradeOnUse>());
			}
			if (ViewModelHackSaw.GetComponent<Repairable>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<Repairable>());
			}
			if (ViewModelHackSaw.GetComponent<BodyHarvestItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<BodyHarvestItem>());
			}
			if (ViewModelHackSaw.GetComponent<Rigidbody>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<Rigidbody>());
			}
			if (ViewModelHackSaw.GetComponent<CanOpeningItem>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<CanOpeningItem>());
			}
			if (ViewModelHackSaw.GetComponent<LODGroup>() != null)
			{
				UnityEngine.Object.Destroy(ViewModelHackSaw.GetComponent<LODGroup>());
			}
			ViewModelHackSaw.transform.localEulerAngles = new Vector3(0f, 0f, 90f);
			ViewModelHackSaw.transform.localPosition = new Vector3(0f, 0.15f, 0.01f);
		}
		if (ViewModelPhoto == null)
		{
			GameObject gearItemObject6 = GetGearItemObject("GEAR_SCPhoto");
			if (gearItemObject6 == null)
			{
				return;
			}
			ViewModelPhoto = UnityEngine.Object.Instantiate(gearItemObject6, gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
			ViewModelPhoto.name = "FPH_Photo";
			ViewModelPhoto.SetActive(value: false);
			foreach (Component component in ViewModelPhoto.GetComponents<Component>())
			{
				string name = component.GetIl2CppType().Name;
				if (name != Il2CppType.Of<LODGroup>().Name && name != Il2CppType.Of<Transform>().Name && name != Il2CppType.Of<MeshRenderer>().Name && name != Il2CppType.Of<SkinnedMeshRenderer>().Name)
				{
					UnityEngine.Object.Destroy(component);
				}
			}
			ViewModelPhoto.transform.localEulerAngles = new Vector3(325f, 300f, 60f);
			ViewModelPhoto.transform.localPosition = new Vector3(0.045f, 0.085f, 0.007f);
		}
		if (ViewModelMap == null)
		{
			GameObject gearItemObject7 = GetGearItemObject("GEAR_SCMapPiece");
			if (gearItemObject7 == null)
			{
				return;
			}
			ViewModelMap = UnityEngine.Object.Instantiate(gearItemObject7, gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
			ViewModelMap.name = "FPH_Map";
			ViewModelMap.SetActive(value: false);
			foreach (Component component2 in ViewModelMap.GetComponents<Component>())
			{
				string name2 = component2.GetIl2CppType().Name;
				if (name2 != Il2CppType.Of<LODGroup>().Name && name2 != Il2CppType.Of<Transform>().Name && name2 != Il2CppType.Of<MeshRenderer>().Name && name2 != Il2CppType.Of<SkinnedMeshRenderer>().Name)
				{
					UnityEngine.Object.Destroy(component2);
				}
			}
			ViewModelMap.transform.localEulerAngles = new Vector3(325f, 300f, 60f);
			ViewModelMap.transform.localPosition = new Vector3(0.045f, 0.085f, 0.007f);
			ViewModelBolt.transform.localScale = new Vector3(85f, 85f, 85f);
		}
		if (!(ViewModelNote == null))
		{
			return;
		}
		GameObject gearItemObject8 = GetGearItemObject("GEAR_SCNote");
		if (gearItemObject8 == null)
		{
			return;
		}
		ViewModelNote = UnityEngine.Object.Instantiate(gearItemObject8, gameObject2.transform.position, gameObject2.transform.rotation, gameObject2.transform);
		ViewModelNote.name = "FPH_Note";
		ViewModelNote.SetActive(value: false);
		foreach (Component component3 in ViewModelNote.GetComponents<Component>())
		{
			string name3 = component3.GetIl2CppType().Name;
			if (name3 != Il2CppType.Of<LODGroup>().Name && name3 != Il2CppType.Of<Transform>().Name && name3 != Il2CppType.Of<MeshRenderer>().Name && name3 != Il2CppType.Of<SkinnedMeshRenderer>().Name)
			{
				UnityEngine.Object.Destroy(component3);
			}
		}
		ViewModelNote.transform.localEulerAngles = new Vector3(325f, 300f, 60f);
		ViewModelNote.transform.localPosition = new Vector3(0.03f, 0.067f, 0.01f);
	}

	public static void AddHarvastedPlant(string harvGUID)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(harvGUID);
		if (!gameObject)
		{
			return;
		}
		Harvestable component = gameObject.GetComponent<Harvestable>();
		if (!(component == null))
		{
			component.gameObject.SetActive(value: true);
			if (component.m_ActivateObjectPostHarvest != null)
			{
				component.m_ActivateObjectPostHarvest.SetActive(value: false);
			}
			component.m_Harvested = false;
		}
	}

	public static void RemoveHarvastedPlant(string harvGUID)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(harvGUID);
		if (!gameObject)
		{
			return;
		}
		Harvestable component = gameObject.GetComponent<Harvestable>();
		if (component == null)
		{
			return;
		}
		if (component.m_DestroyObjectOnHarvest)
		{
			UnityEngine.Object.Destroy(component.gameObject);
			return;
		}
		component.gameObject.SetActive(value: false);
		if (component.m_ActivateObjectPostHarvest != null)
		{
			component.m_ActivateObjectPostHarvest.SetActive(value: true);
		}
	}

	public static void RemoveLootFromContainer(GameObject obj, int State = 0)
	{
		if (!obj)
		{
			return;
		}
		if ((bool)obj.GetComponent<ObjectGuid>())
		{
			obj.GetComponent<ObjectGuid>().Get();
		}
		Container component = obj.GetComponent<Container>();
		if ((bool)component)
		{
			component.DestroyAllGear();
			component.m_Inspected = true;
			Comps.ContainersSync containersSync = obj.GetComponent<Comps.ContainersSync>();
			if (containersSync == null)
			{
				containersSync = obj.AddComponent<Comps.ContainersSync>();
				containersSync.m_Obj = obj;
			}
			switch (State)
			{
			case 0:
				containersSync.m_Empty = true;
				break;
			case 1:
				containersSync.m_Empty = false;
				break;
			case -1:
			case 2:
				containersSync.m_Empty = true;
				component.m_Inspected = false;
				component.PopulateWithRandomGear();
				break;
			}
		}
	}

	public static void RemoveLootFromContainer(string GUID, int State = 0)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if ((bool)gameObject)
		{
			RemoveLootFromContainer(gameObject, State);
		}
	}

	public static void SendBrokeFurniture(string guid, string parentguid, int lvl, string lvlguid)
	{
		DataStr.BrokenFurnitureSync brokenFurnitureSync = new DataStr.BrokenFurnitureSync();
		brokenFurnitureSync.m_Guid = guid;
		brokenFurnitureSync.m_ParentGuid = parentguid;
		brokenFurnitureSync.m_LevelID = lvl;
		brokenFurnitureSync.m_LevelGUID = lvlguid;
		if (sendMyPosition)
		{
			using Packet packet = new Packet(49);
			packet.Write(brokenFurnitureSync);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ServerSend.FURNBROKEN(0, brokenFurnitureSync, toEveryOne: true);
			MPSaveManager.AddBrokenFurn(brokenFurnitureSync);
		}
	}

	public static void SpawnSnowShelterByOther(DataStr.ShowShelterByOther shelterData)
	{
		for (int i = 0; i < SpawnedByOtherShelters.Count; i++)
		{
			GameObject gameObject = SpawnedByOtherShelters[i];
			if (gameObject != null && gameObject.transform.position == shelterData.m_Position)
			{
				return;
			}
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(GameManager.GetSnowShelterManager().m_SnowShelterPrefab.gameObject);
		gameObject2.name = GameManager.GetSnowShelterManager().m_SnowShelterPrefab.name;
		gameObject2.transform.position = shelterData.m_Position;
		gameObject2.transform.rotation = shelterData.m_Rotation;
		gameObject2.AddComponent<Comps.DoNotSerializeThis>();
		SpawnedByOtherShelters.Add(gameObject2);
	}

	public static void RemoveSnowShelterByOther(DataStr.ShowShelterByOther shelterData)
	{
		for (int i = 0; i < SpawnedByOtherShelters.Count; i++)
		{
			GameObject gameObject = SpawnedByOtherShelters[i];
			if (gameObject != null && gameObject.transform.position == shelterData.m_Position)
			{
				SpawnedByOtherShelters.Remove(gameObject);
				UnityEngine.Object.Destroy(gameObject);
				return;
			}
		}
		for (int j = 0; j < SnowShelterManager.m_SnowShelters.Count; j++)
		{
			GameObject gameObject2 = SnowShelterManager.m_SnowShelters[j].gameObject;
			if (gameObject2 != null && gameObject2.transform.position == shelterData.m_Position)
			{
				SnowShelterManager.m_SnowShelters.RemoveAt(j);
				UnityEngine.Object.Destroy(gameObject2);
				break;
			}
		}
	}

	public static void LoadAllSnowSheltersByOther()
	{
		for (int i = 0; i < ShowSheltersBuilded.Count; i++)
		{
			DataStr.ShowShelterByOther showShelterByOther = ShowSheltersBuilded[i];
			if (showShelterByOther != null)
			{
				SpawnSnowShelterByOther(showShelterByOther);
			}
		}
	}

	public static Animator RemoveAnimalComponents(GameObject animal)
	{
		if (animal == null)
		{
			return null;
		}
		if (animal.transform.parent != null && animal.transform.parent.GetComponent<MoveAgent>() != null)
		{
			UnityEngine.Object.Destroy(animal.transform.parent.GetComponent<MoveAgent>());
		}
		if (animal.transform.parent != null && animal.transform.parent.GetComponent<NavMeshAgent>() != null)
		{
			UnityEngine.Object.Destroy(animal.transform.parent.GetComponent<NavMeshAgent>());
		}
		if (animal.GetComponent<AiTarget>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<AiTarget>());
		}
		if (animal.GetComponent<AiWolf>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<AiWolf>());
		}
		if (animal.GetComponent<AiStag>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<AiStag>());
		}
		if (animal.GetComponent<AiRabbit>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<AiRabbit>());
		}
		if (animal.GetComponent<AiMoose>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<AiMoose>());
		}
		if (animal.GetComponent<AiBear>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<AiBear>());
		}
		FarTerritory.StripAi(animal);
		WildlifeCompat.StripAdditionalAi(animal);
		if (animal.GetComponent<CharacterController>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<CharacterController>());
		}
		if (animal.GetComponent<Blackboard>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<Blackboard>());
		}
		if (animal.GetComponent<TLDBehaviourTreeOwner>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<TLDBehaviourTreeOwner>());
		}
		if (animal.GetComponent<Comps.AnimalUpdates>() != null)
		{
			UnityEngine.Object.Destroy(animal.GetComponent<Comps.AnimalUpdates>());
		}
		if (animal.GetComponent<BaseAi>() != null)
		{
			Animator animator = animal.GetComponent<BaseAi>().m_Animator;
			UnityEngine.Object.Destroy(animal.GetComponent<BaseAi>());
			return animator;
		}
		return null;
	}

	public static void LongActionCancleCauseMoved(Comps.MultiplayerPlayer mP)
	{
		if (mP.m_InteractTimer < 2f && mP.m_IsBeingInteractedWith && PlayerInteractionWith == mP && GetActionForOtherPlayer(mP.m_ActionType).m_CancleOnMove)
		{
			if (playersData[mP.m_ID] != null)
			{
				HUDMessage.AddMessage(playersData[mP.m_ID].m_Name + " MOVED, ACTION CANCELED");
			}
			LongActionCanceled(mP);
		}
	}

	public static void DoLongAction(GameObject Obj, string actionString, string actionType, float InteractTime, bool Hold, string Audio = "")
	{
		ObjectInteractWith = Obj;
		InteractHold = Hold;
		InteractionType = actionType;
		InteractTimer = InteractTime;
		PreviousControlModeBeforeAction = GameManager.GetPlayerManagerComponent().GetControlMode();
		GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Locked);
		InteractionInprocess = true;
		GameCompat.Panel<Panel_HUD>().StartItemProgressBar(InteractTime, actionString, null, (System.Action)EmptyFn);
		switch (actionType)
		{
		case "Excision":
			if ((bool)GameCompat.Panel<Panel_Inventory_Examine>() && (bool)Obj && (bool)Obj.GetComponent<GearItem>() && (bool)Obj.GetComponent<GearItem>().m_Sharpenable)
			{
				GameCompat.Panel<Panel_Inventory_Examine>().m_ProgressBarAudio = GameAudioManager.PlaySound(Obj.GetComponent<GearItem>().m_Sharpenable.m_SharpenAudio, InterfaceManager.GetSoundEmitter());
			}
			break;
		case "Lockpick":
			if ((bool)GameCompat.Panel<Panel_Inventory_Examine>())
			{
				GameCompat.Panel<Panel_Inventory_Examine>().m_ProgressBarAudio = GameAudioManager.PlaySound("PLAY_CRAFTINGGENERIC", InterfaceManager.GetSoundEmitter());
			}
			break;
		case "Locksmith0":
			if ((bool)GameCompat.Panel<Panel_Inventory_Examine>())
			{
				GameCompat.Panel<Panel_Inventory_Examine>().m_ProgressBarAudio = GameAudioManager.PlaySound("PLAY_HARVESTINGMETALSAW", InterfaceManager.GetSoundEmitter());
			}
			break;
		case "Locksmith1":
			if ((bool)GameCompat.Panel<Panel_Inventory_Examine>())
			{
				GameCompat.Panel<Panel_Inventory_Examine>().m_ProgressBarAudio = GameAudioManager.PlaySound("PLAY_CRAFTINGARROW", InterfaceManager.GetSoundEmitter());
			}
			break;
		case "ExpInt":
			if (!string.IsNullOrEmpty(Audio))
			{
				GameCompat.Panel<Panel_Inventory_Examine>().m_ProgressBarAudio = GameAudioManager.PlaySound(Audio, InterfaceManager.GetSoundEmitter());
			}
			break;
		}
	}

	public static void DoLongAction(Comps.MultiplayerPlayer mP, string actionString, string actionType)
	{
		mP.m_ActionType = actionType;
		mP.m_IsBeingInteractedWith = true;
		mP.m_InteractTimer = GetActionForOtherPlayer(mP.m_ActionType).m_ActionDuration;
		PlayerInteractionWith = mP;
		PreviousControlModeBeforeAction = GameManager.GetPlayerManagerComponent().GetControlMode();
		GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Locked);
		GameCompat.Panel<Panel_HUD>().StartItemProgressBar(mP.m_InteractTimer, actionString, null, (System.Action)EmptyFn);
		if (!(actionType != "Revive"))
		{
			return;
		}
		if (sendMyPosition)
		{
			using Packet packet = new Packet(63);
			packet.Write(_value: true);
			packet.Write(mP.m_ID);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			using (new Packet(63))
			{
				ServerSend.DONTMOVEWARNING(0, _msg: true, toEveryOne: false, mP.m_ID);
			}
		}
	}

	public static void SendInteractingGUID(string GUID)
	{
		DataStr.BrokenFurnitureSync brokenFurnitureSync = new DataStr.BrokenFurnitureSync();
		brokenFurnitureSync.m_Guid = GUID;
		brokenFurnitureSync.m_ParentGuid = "";
		brokenFurnitureSync.m_LevelID = levelid;
		brokenFurnitureSync.m_LevelGUID = level_guid;
		if (sendMyPosition)
		{
			using Packet packet = new Packet(51);
			packet.Write(brokenFurnitureSync);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ServerSend.FURNBREAKINGGUID(0, brokenFurnitureSync, toEveryOne: true);
		}
	}

	public static void SendRemoveObjectGroup(string group)
	{
		if (sendMyPosition)
		{
			using Packet packet = new Packet(180);
			packet.Write(group);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ExpeditionManager.RemoveObjectGroup(group);
		}
	}

	public static void SendInteractionDone(string GUID)
	{
		if (sendMyPosition)
		{
			using Packet packet = new Packet(179);
			packet.Write(GUID);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ExpeditionManager.RegisterInteractionDone(GUID);
			ServerSend.REMOVEUNIVERSALSYNCABLE(GUID, level_guid);
		}
	}

	public static void LongActionCanceled(Comps.MultiplayerPlayer mP)
	{
		if (GameManager.GetPlayerManagerComponent().GetControlMode() == PlayerControlMode.Locked)
		{
			GameManager.GetPlayerManagerComponent().SetControlMode(PreviousControlModeBeforeAction);
		}
		if (mP.m_ActionType == "Stim" && EmergencyStimBeforeUse != null)
		{
			GameManager.GetPlayerManagerComponent().UseInventoryItem(EmergencyStimBeforeUse);
			EmergencyStimBeforeUse = null;
		}
		mP.m_ActionType = "";
		mP.m_IsBeingInteractedWith = false;
		mP.m_InteractTimer = 0f;
		PlayerInteractionWith = null;
		GameCompat.Panel<Panel_HUD>().CancelItemProgressBar();
	}

	public static void LongActionCanceled()
	{
		if (GameManager.GetPlayerManagerComponent().GetControlMode() == PlayerControlMode.Locked)
		{
			GameManager.GetPlayerManagerComponent().SetControlMode(PreviousControlModeBeforeAction);
		}
		ObjectInteractWith = null;
		InteractionInprocess = false;
		InteractTimer = 0f;
		InteractHold = false;
		GameCompat.Panel<Panel_HUD>().CancelItemProgressBar();
		if ((InteractionType == "Excision" || InteractionType == "Lockpick" || InteractionType == "Locksmith0" || InteractionType == "Locksmith1" || InteractionType == "ExpInt") && (bool)GameCompat.Panel<Panel_Inventory_Examine>())
		{
			GameCompat.Panel<Panel_Inventory_Examine>().StopProgressBarAudio();
		}
		SendInteractingGUID("");
	}

	public static void LongActionFinished(GameObject obj, string ActionType)
	{
		if (ActionType == "Excision" && (bool)obj)
		{
			GearItem gearItem = GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_JeremiahKnife", 1);
			DisableObjectForXPMode.RemoveDisabler(gearItem.gameObject);
			gearItem.m_RolledSpawnChance = true;
			gearItem.m_BeenInPlayerInventory = true;
			gearItem.m_LastUpdatedTODHours = 0f;
			gearItem.ManualStart();
			GameManager.GetPlayerManagerComponent().EquipItem(gearItem, fromDeserialize: false);
			GameManager.GetInventoryComponent().RemoveGear(obj);
			return;
		}
		switch (ActionType)
		{
		case "Lockpick":
			if (SwearOnLockpickingDone)
			{
				GameAudioManager.PlaySound("Play_SndInvCrowbarPrybar", InterfaceManager.GetSoundEmitter());
				GameManager.GetPlayerVoiceComponent().Play("PLAY_FIREFAIL", Il2CppVoice.Priority.Critical);
			}
			else
			{
				GameAudioManager.PlaySound("PLAY_SNDMECHSAFETUMBLERFALL", InterfaceManager.GetSoundEmitter());
				GameManager.GetPlayerVoiceComponent().Play("PLAY_FIRESUCCESS", Il2CppVoice.Priority.Critical);
			}
			break;
		case "Locksmith0":
		case "Locksmith1":
		case "Locksmith5":
		{
			if (!obj || !obj.GetComponent<Comps.DroppedGearDummy>())
			{
				break;
			}
			int searchKey = obj.GetComponent<Comps.DroppedGearDummy>().m_SearchKey;
			int num = 0;
			switch (ActionType)
			{
			case "Locksmith0":
				num = 0;
				break;
			case "Locksmith1":
				num = 1;
				break;
			case "Locksmith5":
				num = 5;
				GameManager.GetInventoryComponent().RemoveGearFromInventory("GEAR_SCLiquidLead", 1);
				break;
			}
			if (iAmHost)
			{
				if ((bool)PendingRegisterKey)
				{
					MPSaveManager.ApplyToolOnBlank(searchKey, num, PendingKeyName, PendingKeySeed);
				}
				else
				{
					MPSaveManager.ApplyToolOnBlank(searchKey, num);
				}
			}
			if (!sendMyPosition)
			{
				break;
			}
			if ((bool)PendingRegisterKey)
			{
				using (Packet packet = new Packet(142))
				{
					packet.Write(searchKey);
					packet.Write(num);
					packet.Write(_value: true);
					packet.Write(PendingKeyName);
					packet.Write(PendingKeySeed);
					SendUDPData(packet);
					break;
				}
			}
			using Packet packet2 = new Packet(142);
			packet2.Write(searchKey);
			packet2.Write(num);
			packet2.Write(_value: false);
			SendUDPData(packet2);
			break;
		}
		case "ExpInt":
		{
			DataStr.ExpeditionInteractiveData data = obj.GetComponent<Comps.ExpeditionInteractive>().m_Data;
			if (!string.IsNullOrEmpty(data.m_Tool))
			{
				GearItem lowestConditionGearThatMatchesName = GameManager.GetInventoryComponent().GetLowestConditionGearThatMatchesName(data.m_Tool);
				if ((bool)lowestConditionGearThatMatchesName && (bool)lowestConditionGearThatMatchesName.m_ToolsItem)
				{
					lowestConditionGearThatMatchesName.DegradeOnUse();
				}
			}
			if (!string.IsNullOrEmpty(data.m_Material) && data.m_MaterialCount > 0)
			{
				GameManager.GetInventoryComponent().RemoveGearFromInventory(data.m_Material, data.m_MaterialCount);
			}
			if (!string.IsNullOrEmpty(data.m_Yield) && data.m_YieldCount > 0)
			{
				GameManager.GetPlayerManagerComponent().AddItemCONSOLE(data.m_Yield, data.m_YieldCount);
				if (data.m_YieldCount > 1)
				{
					GearMessage.AddMessage(GearItem.GetGearDisplayName(data.m_Yield), Localization.Get("GAMEPLAY_Added"), data.m_Yield + " (" + data.m_YieldCount + ")");
				}
				else
				{
					GearMessage.AddMessage(GearItem.GetGearDisplayName(data.m_Yield), Localization.Get("GAMEPLAY_Added"), data.m_Yield);
				}
			}
			if (!string.IsNullOrEmpty(data.m_ObjectGroupToRemove))
			{
				SendRemoveObjectGroup(data.m_ObjectGroupToRemove);
			}
			SendInteractionDone(obj.GetComponent<ObjectGuid>().Get());
			SendInteractingGUID("");
			obj.SetActive(value: false);
			break;
		}
		case "ExpFeedback":
			GUIUtility.systemCopyBuffer = LastExpeditionAlias;
			GameAudioManager.PlayGUIError();
			HUDMessage.AddMessage("Feedback form opened in your browser");
			Application.OpenURL("https://docs.google.com/forms/d/e/1FAIpQLSesMoV4idTJNIholI1qW2datDhLQIrI8QtziWCF1X6PHMshSg/viewform");
			break;
		}
	}

	public static void LongActionFinished(Comps.MultiplayerPlayer mP, string ActionType)
	{
		if (sendMyPosition)
		{
			using Packet packet = new Packet(62);
			packet.Write(ActionType);
			packet.Write(mP.m_ID);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			using (new Packet(62))
			{
				ServerSend.APPLYACTIONONPLAYER(0, ActionType, toEveryOne: false, mP.m_ID);
			}
		}
		if (ActionType == "Bandage")
		{
			GameManager.GetInventoryComponent().RemoveGearFromInventory("GEAR_HeavyBandage", 1);
		}
		switch (ActionType)
		{
		case "Sterilize":
		{
			GearItem bestGearItemWithName = GameManager.GetInventoryComponent().GetBestGearItemWithName("GEAR_BottleHydrogenPeroxide");
			bestGearItemWithName.m_LiquidItem.m_Liquid = bestGearItemWithName.m_LiquidItem.m_Liquid - GameCompat.Liters(0.1f);
			if (bestGearItemWithName.m_LiquidItem.m_Liquid < GameCompat.Liters(0.1f))
			{
				GameManager.GetInventoryComponent().DestroyGear(bestGearItemWithName.gameObject);
			}
			break;
		}
		case "Revive":
			GameManager.GetInventoryComponent().RemoveGearFromInventory("GEAR_MedicalSupplies_hangar", 1);
			break;
		case "Stim":
			if (GameManager.m_PlayerManager != null && GameManager.m_PlayerManager.m_ItemInHands != null && GameManager.m_PlayerManager.m_ItemInHands.m_EmergencyStim != null)
			{
				GameManager.GetPlayerManagerComponent().UseInventoryItem(GameManager.m_PlayerManager.m_ItemInHands);
			}
			GameManager.GetInventoryComponent().RemoveGearFromInventory("GEAR_EmergencyStim", 1);
			EmergencyStimBeforeUse = null;
			break;
		case "Examine":
			TryToCheckPlayer(mP.m_ID);
			break;
		}
	}

	public static void ApplyTreatmentRegeneration(float HpPerHour, float NumHours, string Cause)
	{
		Pathes.OverrideConditionOverTimeCause = Cause;
		GameManager.GetPlayerManagerComponent().ApplyConditionOverTimeBuff(HpPerHour, NumHours, NumHours);
	}

	public static string GetPlayerName(int ID)
	{
		if (ID == 0)
		{
			if (iAmHost)
			{
				return MyChatName;
			}
			if (playersData[ID] != null)
			{
				return playersData[ID].m_Name;
			}
		}
		else
		{
			if (ClientUser.myId == ID)
			{
				return MyChatName;
			}
			if (playersData[ID] != null)
			{
				return playersData[ID].m_Name;
			}
		}
		return "Unknown";
	}

	public static void OtherPlayerApplyActionOnMe(string ActionType, int FromId)
	{
		if (ActionType == "Stim")
		{
			GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_EmergencyStim", 1).m_EmergencyStim.OnInject();
		}
	}

	public static void EmptyFn()
	{
	}

	public static void PlayersUpdateManager()
	{
		if (playersData.Count <= 0)
		{
			return;
		}
		int num = System.Math.Min(MaxPlayers, System.Math.Min(playersData.Count, players.Count));
		for (int i = 0; i < num; i++)
		{
			if (playersData[i] != null && !(players[i] == null))
			{
				GameObject gameObject = players[i];
				DataStr.MultiPlayerClientData multiPlayerClientData = playersData[i];
				if (levelid == multiPlayerClientData.m_Levelid && level_guid == multiPlayerClientData.m_LevelGuid && !multiPlayerClientData.m_IsLoading)
				{
					gameObject.SetActive(value: true);
				}
				else
				{
					gameObject.SetActive(value: false);
				}
			}
		}
	}

	public static void ApplyDamageZones(GameObject p, Comps.MultiplayerPlayer mP)
	{
		if (p != null)
		{
			Transform child = p.transform.GetChild(3).GetChild(8);
			GameObject gameObject = child.GetChild(0).GetChild(0).GetChild(0)
				.gameObject;
			GameObject gameObject2 = gameObject.transform.GetChild(0).GetChild(0).gameObject;
			GameObject gameObject3 = gameObject.transform.GetChild(0).GetChild(0).GetChild(0)
				.gameObject;
			GameObject gameObject4 = gameObject.transform.GetChild(1).GetChild(0).gameObject;
			GameObject gameObject5 = gameObject.transform.GetChild(1).GetChild(0).GetChild(0)
				.gameObject;
			GameObject gameObject6 = gameObject.transform.GetChild(2).GetChild(0).gameObject;
			GameObject gameObject7 = child.GetChild(2).gameObject;
			GameObject gameObject8 = child.GetChild(1).gameObject;
			gameObject.AddComponent<Comps.PlayerBulletDamage>();
			gameObject2.AddComponent<Comps.PlayerBulletDamage>();
			gameObject3.AddComponent<Comps.PlayerBulletDamage>();
			gameObject4.AddComponent<Comps.PlayerBulletDamage>();
			gameObject5.AddComponent<Comps.PlayerBulletDamage>();
			gameObject6.AddComponent<Comps.PlayerBulletDamage>();
			gameObject8.AddComponent<Comps.PlayerBulletDamage>();
			gameObject7.AddComponent<Comps.PlayerBulletDamage>();
			gameObject.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject, mP, 1);
			gameObject2.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject2, mP, 2);
			gameObject3.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject3, mP, 2);
			gameObject4.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject4, mP, 3);
			gameObject5.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject5, mP, 3);
			gameObject6.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject6, mP, 0);
			gameObject8.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject8, mP, 4);
			gameObject7.GetComponent<Comps.PlayerBulletDamage>().SetLocaZone(gameObject7, mP, 5);
		}
	}

	public static void AddLocalizedSprain(AfflictionBodyArea location, string causeID)
	{
		bool flag = false;
		switch (location)
		{
		case AfflictionBodyArea.LegLeft:
			location = AfflictionBodyArea.FootLeft;
			flag = true;
			break;
		case AfflictionBodyArea.LegRight:
			location = AfflictionBodyArea.FootRight;
			flag = true;
			break;
		case AfflictionBodyArea.ArmLeft:
			location = AfflictionBodyArea.HandLeft;
			break;
		case AfflictionBodyArea.ArmRight:
			location = AfflictionBodyArea.HandRight;
			break;
		}
		if (flag)
		{
			SprainedAnkle sprainedAnkleComponent = GameManager.GetSprainedAnkleComponent();
			sprainedAnkleComponent.m_CausesLocIDs.Add(causeID);
			sprainedAnkleComponent.m_Locations.Add((int)location);
			sprainedAnkleComponent.m_ElapsedHoursList.Add(0f);
			sprainedAnkleComponent.m_DurationHoursList.Add(UnityEngine.Random.Range(sprainedAnkleComponent.m_DurationHoursMin, sprainedAnkleComponent.m_DurationHoursMax));
			sprainedAnkleComponent.m_ElapsedRestList.Add(0f);
			sprainedAnkleComponent.m_SecondsSinceLastPainAudio = 0f;
			sprainedAnkleComponent.DoStumbleEffects();
			PlayerDamageEvent.SpawnDamageEvent(sprainedAnkleComponent.m_LocalizedDisplayName.m_LocalizationID, "GAMEPLAY_Affliction", "ico_injury_sprainedAnkle", InterfaceManager.m_FirstAidRedColor, fadeout: true, GameCompat.Panel<Panel_HUD>().m_DamageEventDisplaySeconds, GameCompat.Panel<Panel_HUD>().m_DamageEventFadeOutSeconds);
			GameAudioManager.PlaySound(sprainedAnkleComponent.m_SprainedAnkleAudioEvent, sprainedAnkleComponent.gameObject);
			GameAudioManager.PlaySound(sprainedAnkleComponent.m_SprainedAnkleSFXEvent, sprainedAnkleComponent.gameObject);
			GameManager.GetLogComponent().AddAffliction(AfflictionType.SprainedAnkle, causeID);
			StatsManager.IncrementValue(StatID.Sprains_Ankle);
		}
		else
		{
			SprainedWrist sprainedWristComponent = GameManager.GetSprainedWristComponent();
			sprainedWristComponent.m_CausesLocIDs.Add(causeID);
			sprainedWristComponent.m_Locations.Add((int)location);
			sprainedWristComponent.m_ElapsedHoursList.Add(0f);
			sprainedWristComponent.m_DurationHoursList.Add(UnityEngine.Random.Range(sprainedWristComponent.m_DurationHoursMin, sprainedWristComponent.m_DurationHoursMax));
			sprainedWristComponent.m_ElapsedRestList.Add(0f);
			sprainedWristComponent.DoStumbleEffects();
			PlayerDamageEvent.SpawnDamageEvent(sprainedWristComponent.m_LocalizedDisplayName.m_LocalizationID, "GAMEPLAY_Affliction", "ico_injury_sprainedWrist", InterfaceManager.m_FirstAidRedColor, fadeout: true, GameCompat.Panel<Panel_HUD>().m_DamageEventDisplaySeconds, GameCompat.Panel<Panel_HUD>().m_DamageEventFadeOutSeconds);
			GameManager.GetPlayerVoiceComponent().Play(sprainedWristComponent.m_SprainedWristVO, Il2CppVoice.Priority.Critical);
			GameAudioManager.PlaySound(sprainedWristComponent.m_SprainedWristSFX, sprainedWristComponent.gameObject);
			GameManager.GetLogComponent().AddAffliction(AfflictionType.SprainedWrist, causeID);
			StatsManager.IncrementValue(StatID.Sprains_Wrist);
		}
		GameManager.GetSprainPainComponent().ApplyAffliction(location, causeID, AfflictionOptions.PlayFX);
	}

	public static void DamageByBullet(float damage, int from, int bodypart, bool Meele, string MeleeWeapon)
	{
		if (damage <= 0f)
		{
			return;
		}
		DataStr.MeleeDescripter meleeDescripter;
		if (Meele)
		{
			meleeDescripter = GetMeelePlayerInfo(MeleeWeapon);
		}
		else
		{
			meleeDescripter = new DataStr.MeleeDescripter();
			meleeDescripter.m_PlayerDamage = damage;
			meleeDescripter.m_AnimalDamage = 0f;
			meleeDescripter.m_BloodLoss = true;
			meleeDescripter.m_ClothingTearing = true;
			meleeDescripter.m_Pain = false;
		}
		string text = "Other player";
		if (playersData[from] != null)
		{
			text = playersData[from].m_Name;
			string text2 = " shoot you";
			if (Meele)
			{
				text2 = " hit you";
			}
			text += text2;
		}
		bool flag = false;
		bool flag2 = false;
		if (bodypart == 1)
		{
			flag = GameManager.GetDamageProtection().HasBallisticVest();
			if (flag)
			{
				damage /= 10f;
			}
		}
		if (bodypart == 0 && GetClothForSlot(ClothingRegion.Head, ClothingLayer.Mid) == "GEAR_CookingPot")
		{
			damage = 20f * damage / 100f;
			flag2 = true;
		}
		GameManager.GetConditionComponent().AddHealth(0f - damage, DamageSource.BulletWound);
		AfflictionBodyArea afflictionBodyArea = AfflictionBodyArea.Head;
		switch (bodypart)
		{
		case 0:
			afflictionBodyArea = AfflictionBodyArea.Head;
			break;
		case 1:
			afflictionBodyArea = AfflictionBodyArea.Chest;
			break;
		case 2:
			afflictionBodyArea = AfflictionBodyArea.ArmRight;
			break;
		case 3:
			afflictionBodyArea = AfflictionBodyArea.ArmLeft;
			break;
		case 4:
			afflictionBodyArea = AfflictionBodyArea.LegRight;
			break;
		case 5:
			afflictionBodyArea = AfflictionBodyArea.LegLeft;
			break;
		}
		if (!flag && !flag2)
		{
			if (meleeDescripter.m_BloodLoss)
			{
				GameManager.GetBloodLossComponent().BloodLossStartOverrideArea(afflictionBodyArea, text, displayIcon: true, AfflictionOptions.PlayFX);
			}
			if (meleeDescripter.m_Pain)
			{
				switch (afflictionBodyArea)
				{
				case AfflictionBodyArea.Head:
					Pathes.OverrideHeadacheCause = text;
					GameManager.GetHeadacheComponent().ApplyHeadache(GameManager.GetHeadacheComponent().m_LegacyHeadacheData);
					break;
				case AfflictionBodyArea.ArmLeft:
				case AfflictionBodyArea.ArmRight:
				case AfflictionBodyArea.LegLeft:
				case AfflictionBodyArea.LegRight:
					AddLocalizedSprain(afflictionBodyArea, text);
					break;
				}
			}
			ClothingRegion region = ClothingRegion.Chest;
			switch (afflictionBodyArea)
			{
			case AfflictionBodyArea.Head:
				region = ClothingRegion.Head;
				break;
			case AfflictionBodyArea.ArmLeft:
				region = ClothingRegion.Hands;
				break;
			case AfflictionBodyArea.ArmRight:
				region = ClothingRegion.Hands;
				break;
			case AfflictionBodyArea.Chest:
				region = ClothingRegion.Chest;
				break;
			case AfflictionBodyArea.LegLeft:
				region = ClothingRegion.Legs;
				break;
			case AfflictionBodyArea.LegRight:
				region = ClothingRegion.Legs;
				break;
			}
			if (meleeDescripter.m_ClothingTearing)
			{
				int num = new System.Random().Next(20, 40);
				GameManager.GetPlayerManagerComponent().ApplyDamageToWornClothingRegion(region, num);
			}
		}
		else
		{
			int num2 = new System.Random().Next(0, 100);
			if (!Meele && num2 <= 5)
			{
				GameManager.GetBrokenRibComponent().BrokenRibStart(text, displayIcon: true, noVO: false, isMinor: true, autoSave: false);
			}
			if (flag2)
			{
				GameManager.GetHeadacheComponent().ApplyHeadache(GameManager.GetHeadacheComponent().m_LegacyHeadacheData);
			}
		}
		GameManager.GetPlayerVoiceComponent().Play("PLAY_PLAYERDAMAGE", Il2CppVoice.Priority.Critical);
		Transform playerTransform = GameManager.GetPlayerTransform();
		GameObject playerObject = GameManager.GetPlayerObject();
		GameAudioManager.SetMaterialSwitch("Flesh", playerObject);
		AkSoundEngine.PostEvent(EVENTS.PLAY_BULLETIMPACTS, GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject()));
		GameAudioManager.SetAudioSourceTransform(playerObject, playerTransform);
	}

	public static void DoColisionForArrows()
	{
		Il2CppSystem.Collections.Generic.List<ArrowItem> arrowItems = ArrowManager.m_ArrowItems;
		bool isTrigger = true;
		for (int i = 0; i < arrowItems.Count; i++)
		{
			if (arrowItems[i] != null && arrowItems[i].InFlight(onlyConsiderFirstImpact: false))
			{
				isTrigger = false;
				break;
			}
		}
		for (int j = 0; j < MaxPlayers; j++)
		{
			if (!(players[j] != null) || !(players[j].GetComponent<Comps.MultiplayerPlayer>() != null))
			{
				continue;
			}
			System.Collections.Generic.List<GameObject> damageColiders = players[j].GetComponent<Comps.MultiplayerPlayer>().m_DamageColiders;
			for (int k = 0; k < damageColiders.Count; k++)
			{
				if (damageColiders[k] != null && damageColiders[k].GetComponent<BoxCollider>() != null)
				{
					damageColiders[k].GetComponent<BoxCollider>().isTrigger = isTrigger;
				}
			}
		}
	}

	public static void DisableOriginalAnimalSpawns(bool host = false)
	{
		if (GameManager.m_SpawnRegionManager == null || !host)
		{
			return;
		}
		for (int i = BaseAiManager.m_BaseAis.Count - 1; i >= 0; i--)
		{
			BaseAi baseAi = BaseAiManager.m_BaseAis[i];
			GameObject gameObject = (baseAi != null) ? baseAi.gameObject : null;
			if (gameObject != null)
			{
				// An AI without ObjectGuid used to throw here and abort Server.StartSteam before the lobby was marked Playing.
				ObjectGuid objectGuid = gameObject.GetComponent<ObjectGuid>();
				if (objectGuid != null)
				{
					ObjectGuidManager.UnRegisterGuid(objectGuid.Get());
				}
				UnityEngine.Object.Destroy(gameObject);
			}
		}
	}

	public static void SimulateSpawnFromRegionSpawn(string GUID, SpawnRegion spRobj)
	{
		if (!(spRobj != null) || !(spRobj.GetComponent<SpawnRegion>() != null))
		{
			return;
		}
		SpawnRegion component = spRobj.GetComponent<SpawnRegion>();
		BaseAi baseAi = component.InstantiateAndPlaceSpawn(component.m_WildlifeMode);
		WildlifeCompat.RecordRegionSpawn(GUID, component, baseAi, "native-return");
		if (baseAi != null && !baseAi.gameObject.activeInHierarchy && !component.gameObject.activeInHierarchy)
		{
			baseAi.ReparentBaseAi(null);
			WildlifeCompat.RecordRegionSpawn(GUID, component, baseAi, "detached-inactive-region");
		}
		if (baseAi != null)
		{
			GameObject gameObject = baseAi.gameObject;
			if (gameObject.GetComponent<ObjectGuid>() == null)
			{
				gameObject.AddComponent<ObjectGuid>();
			}
			string value = ObjectGuidManager.GenerateNewGuidString();
			gameObject.GetComponent<ObjectGuid>().Set(value);
			if (gameObject.GetComponent<Comps.AnimalUpdates>() != null)
			{
				gameObject.GetComponent<Comps.AnimalUpdates>().m_RegionGUID = GUID;
				gameObject.GetComponent<Comps.AnimalUpdates>().m_Animal = gameObject;
			}
			else
			{
				Comps.AnimalUpdates animalUpdates = gameObject.AddComponent<Comps.AnimalUpdates>();
				animalUpdates.m_RegionGUID = GUID;
				animalUpdates.m_Animal = gameObject;
			}
			if ((bool)spRobj.GetComponent<Comps.SpawnRegionSimple>())
			{
				spRobj.GetComponent<Comps.SpawnRegionSimple>().m_Spawned++;
			}
			BaseAi component2 = gameObject.GetComponent<BaseAi>();
			if (component2 != null)
			{
				component2.TeleportToRandomWaypointAndPathfind();
			}
		}
	}

	public static void SetAnimationDataForAnimal(Animator AN, DataStr.AnimalAnimsSync obj)
	{
		int id = Animator.StringToHash("TurnAngle");
		int id2 = Animator.StringToHash("TurnSpeed");
		int id3 = Animator.StringToHash("Speed");
		int id4 = Animator.StringToHash("Wounded");
		int id5 = Animator.StringToHash("Roll");
		int id6 = Animator.StringToHash("Pitch");
		int id7 = Animator.StringToHash("TargetHeading");
		int id8 = Animator.StringToHash("TargetHeadingSmooth");
		int id9 = Animator.StringToHash("TapMeter");
		int id10 = Animator.StringToHash("AiState");
		int id11 = Animator.StringToHash("Corpse");
		int id12 = Animator.StringToHash("Dead");
		int id13 = Animator.StringToHash("DamageSide");
		int id14 = Animator.StringToHash("DamageBodyPart");
		int id15 = Animator.StringToHash("AttackId");
		AN.SetFloat(id, obj.AP_TurnAngle);
		AN.SetFloat(id2, obj.AP_TurnSpeed);
		AN.SetFloat(id3, obj.AP_Speed);
		AN.SetFloat(id4, obj.AP_Wounded);
		AN.SetFloat(id5, obj.AP_Roll);
		AN.SetFloat(id6, obj.AP_Pitch);
		AN.SetFloat(id7, obj.AP_TargetHeading);
		AN.SetFloat(id8, obj.AP_TargetHeadingSmooth);
		AN.SetFloat(id9, obj.AP_TapMeter);
		AN.SetInteger(id10, obj.AP_AiState);
		AN.SetBool(id11, obj.AP_Corpse);
		AN.SetBool(id12, obj.AP_Dead);
		AN.SetInteger(id13, obj.AP_DeadSide);
		AN.SetInteger(id14, obj.AP_DamageBodyPart);
		AN.SetInteger(id15, obj.AP_AttackId);
	}

	public static void LoadAniamlCorpsesForScene()
	{
		System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
		foreach (System.Collections.Generic.KeyValuePair<string, DataStr.AnimalKilled> item in Shared.AnimalsKilled)
		{
			if (item.Value.m_PrefabName != "DeadBody")
			{
				if (item.Value.m_CreatedTime + 14400 < MinutesFromStartServer)
				{
					list.Add(item.Key);
				}
				else if (item.Value.m_LevelGUID == level_guid)
				{
					ProcessAnimalCorpseSync(item.Value);
				}
			}
		}
		foreach (string item2 in list)
		{
			Shared.AnimalsKilled.Remove(item2);
		}
	}

	public static void FinallyPickupRabbit(int result)
	{
		RemovePleaseWait();
		switch (result)
		{
		case 0:
			HUDMessage.AddMessage("Can't pick this up");
			break;
		case 1:
		{
			GameObject gearItemObject = GetGearItemObject("gear_rabbitcarcass");
			if ((bool)gearItemObject)
			{
				GameObject gameObject2 = UnityEngine.Object.Instantiate(gearItemObject, GameManager.GetPlayerTransform().transform.position, GameManager.GetPlayerTransform().transform.rotation);
				gameObject2.name = gearItemObject.name;
				if ((bool)gameObject2 && (bool)gameObject2.GetComponent<GearItem>())
				{
					Comps.DropFakeOnLeave dropFakeOnLeave = gameObject2.AddComponent<Comps.DropFakeOnLeave>();
					dropFakeOnLeave.m_OldPossition = gameObject2.transform.position;
					dropFakeOnLeave.m_OldRotation = gameObject2.transform.rotation;
					GameManager.GetPlayerManagerComponent().ProcessPickupItemInteractionCompat(gameObject2.GetComponent<GearItem>());
				}
			}
			break;
		}
		case 2:
		{
			GameObject prefab = WildlifeCompat.GetPrefab("WILDLIFE_Rabbit");
			if ((bool)prefab)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(prefab, GameManager.GetPlayerTransform().transform.position, GameManager.GetPlayerTransform().transform.rotation);
				Comps.AnimalUpdates component = gameObject.GetComponent<Comps.AnimalUpdates>();
				if (component == null)
				{
					gameObject.AddComponent<Comps.AnimalUpdates>();
					component = gameObject.GetComponent<Comps.AnimalUpdates>();
					component.m_Animal = gameObject;
					component.m_PickedUp = true;
				}
				else
				{
					component.m_PickedUp = true;
				}
				if ((bool)gameObject.GetComponent<WildlifeItem>())
				{
					GameManager.GetPlayerManagerComponent().ProcessPickupItemInteraction(gameObject.GetComponent<WildlifeItem>().m_GearItem, forceEquip: false, forceEquipImmediate: false, skipAudio: false);
				}
			}
			break;
		}
		}
	}

	public static void OnHitRabbit(string GUID)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if ((bool)gameObject && (bool)gameObject.GetComponent<BaseAi>())
		{
			gameObject.GetComponent<BaseAi>().Stun(gameObject.transform.position);
		}
	}

	public static void OnRabbitRevived(Vector3 v3)
	{
		if (!AnimalsController)
		{
			return;
		}
		GameObject prefab = WildlifeCompat.GetPrefab("WILDLIFE_Rabbit");
		if (prefab == null)
		{
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab);
		gameObject.transform.position = v3;
		BaseAi component = gameObject.GetComponent<BaseAi>();
		if (!(component == null))
		{
			if (component.CreateMoveAgent(null) && !component.GetMoveAgent().Warp(v3, 1f, findClosestPoint: true, -1))
			{
				component.Despawn();
			}
			else
			{
				component.m_SpawnPos = v3;
				ObjectGuid.MaybeAttachObjectGuidAndRegister(component.gameObject, ObjectGuidManager.GenerateNewGuidString());
			}
			component.FleeFrom(v3);
		}
	}

	public static void OnReleaseRabbit(int from)
	{
		if (!AnimalsController)
		{
			return;
		}
		GameObject prefab = WildlifeCompat.GetPrefab("WILDLIFE_Rabbit");
		if (prefab == null)
		{
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab);
		if (gameObject == null)
		{
			return;
		}
		gameObject.name = prefab.name;
		float num = 5f;
		Vector3 position = GameManager.GetMainCamera().transform.position;
		Vector3 forward = GameManager.GetMainCamera().transform.forward;
		if (from != ClientUser.myId && players[from] != null)
		{
			Transform child = players[from].transform.GetChild(3).GetChild(8).GetChild(0)
				.GetChild(0)
				.GetChild(0)
				.transform.GetChild(2).GetChild(0);
			position = child.position;
			forward = child.forward;
		}
		if (!Physics.Raycast(position + forward * num + 1000f * Vector3.up, -Vector3.up, out var hitInfo, float.PositiveInfinity, Utils.m_PhysicalCollisionLayerMask))
		{
			return;
		}
		gameObject.transform.position = hitInfo.point;
		BaseAi component = gameObject.GetComponent<BaseAi>();
		if (!(component == null))
		{
			if (component.CreateMoveAgent(null) && !component.GetMoveAgent().Warp(hitInfo.point, 1f, findClosestPoint: true, -1))
			{
				component.Despawn();
			}
			else
			{
				component.m_SpawnPos = hitInfo.point;
				ObjectGuid.MaybeAttachObjectGuidAndRegister(component.gameObject, ObjectGuidManager.GenerateNewGuidString());
			}
			component.FleeFrom(hitInfo.point);
		}
	}

	public static void AttemptToPickupRabbit(string GUID)
	{
		if (sendMyPosition)
		{
			DoPleaseWait("Trying to pickup little thing", "Please wait...");
			SetRepeatPacket(ResendPacketType.Cancel, 10);
			using Packet packet = new Packet(111);
			packet.Write(GUID);
			SendUDPData(packet);
			return;
		}
		if (iAmHost)
		{
			FinallyPickupRabbit(Shared.PickUpRabbit(GUID));
		}
	}

	public static void SpawnQuartedMess(string GUID)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if (!gameObject || !(gameObject.GetComponent<Comps.AnimalCorpseObject>() != null) || !(gameObject.GetComponent<BodyHarvest>() != null))
		{
			return;
		}
		BodyHarvest component = gameObject.GetComponent<BodyHarvest>();
		if (component.CanSpawnCarcassSite())
		{
			component.MaybeSpawnOrRefreshCarcassSite();
			if (component.m_CarcassSite != null)
			{
				component.m_CarcassSite.AddPostHarvestMessFx(component.transform.position);
			}
		}
		UnityEngine.Object.Destroy(gameObject);
	}

	public static void OpenBodyHarvest(BodyHarvest bh)
	{
		RemovePleaseWait();
		if (GameManager.GetPlayerManagerComponent().GetControlMode() != PlayerControlMode.InSnowShelter)
		{
			if (bh.m_Ravaged && bh.m_StartRavaged)
			{
				bh.m_CanQuarter = false;
			}
			GameCompat.Panel<Panel_BodyHarvest>().Enable(enable: true, bh, playBookEndAnim: false, ComingFromScreenCategory.NotUI);
		}
	}

	public static DataStr.AnimalKilled GetAnimalMeatForFeeding(BodyHarvest bh)
	{
		if ((bool)bh.gameObject.GetComponent<ObjectGuid>())
		{
			string key = bh.gameObject.GetComponent<ObjectGuid>().Get();
			if (iAmHost && Shared.AnimalsKilled.TryGetValue(key, out var value))
			{
				bh.m_MeatAvailableKG = GameCompat.Kilograms(value.m_Meat);
				bh.m_GutAvailableUnits = value.m_Guts;
				bh.m_HideAvailableUnits = value.m_Hide;
				return value;
			}
		}
		return null;
	}

	public static void RequestAnimalCorpseInteration(BodyHarvest bh)
	{
		if ((bool)bh.gameObject.GetComponent<ObjectGuid>())
		{
			string text = bh.gameObject.GetComponent<ObjectGuid>().Get();
			if (iAmHost)
			{
				if (Shared.AnimalsKilled.TryGetValue(text, out var value))
				{
					bh.m_MeatAvailableKG = GameCompat.Kilograms(value.m_Meat);
					bh.m_GutAvailableUnits = value.m_Guts;
					bh.m_HideAvailableUnits = value.m_Hide;
					OpenBodyHarvest(bh);
				}
				else if (bh.m_Ravaged && bh.m_StartRavaged)
				{
					value = new DataStr.AnimalKilled();
					DataStr.BodyHarvestUnits bodyHarvestUnits = Shared.GetBodyHarvestUnits("WILDLIFE_Stag");
					value.m_GUID = text;
					value.m_Guts = bodyHarvestUnits.m_Guts;
					value.m_Hide = 1;
					value.m_Meat = bodyHarvestUnits.m_Meat;
					value.m_PrefabName = "DeadBody";
					Shared.AnimalsKilled.Add(text, value);
					MPSaveManager.AnimalsKilledChanged = true;
				}
				else
				{
					HUDMessage.AddMessage("Can't interact with this");
				}
			}
			else if (sendMyPosition)
			{
				DoPleaseWait("Downloading animal carcass", "Please wait...");
				SetRepeatPacket(ResendPacketType.Cancel, 10);
				GoingToHarvest = bh;
				using Packet packet = new Packet(108);
				packet.Write(text);
				packet.Write(bh.m_Ravaged && bh.m_StartRavaged);
				SendUDPData(packet);
			}
		}
		else
		{
			HUDMessage.AddMessage("Can't interact with this");
		}
	}

	public static DataStr.AnimalCompactData GetCompactDataForAnimal(BaseAi AI, System.Collections.Generic.List<DataStr.AnimalArrow> Arrows)
	{
		if (AI != null && AI.gameObject != null && AI.gameObject.GetComponent<ObjectGuid>() != null)
		{
			GameObject gameObject = AI.gameObject;
			DataStr.AnimalCompactData animalCompactData = new DataStr.AnimalCompactData();
			animalCompactData.m_PrefabName = GetAnimalPrefabName(gameObject.name);
			animalCompactData.m_GUID = AI.gameObject.GetComponent<ObjectGuid>().Get();
			animalCompactData.m_Position = gameObject.transform.position;
			animalCompactData.m_Rotation = gameObject.transform.rotation;
			animalCompactData.m_LastSeen = MinutesFromStartServer;
			animalCompactData.m_Health = AI.m_CurrentHP;
			animalCompactData.m_Bleeding = AI.m_BleedingOut;
			animalCompactData.m_TimeOfBleeding = MinutesFromStartServer + (int)AI.m_ElapsedBleedingOutMinutes;
			animalCompactData.m_LastAiMode = (int)AI.GetAiMode();
			animalCompactData.m_ArrowsCount = Arrows.Count;
			animalCompactData.m_Arrows = Arrows;
			if (gameObject.GetComponent<Comps.AnimalUpdates>() != null)
			{
				animalCompactData.m_RegionGUID = gameObject.GetComponent<Comps.AnimalUpdates>().m_RegionGUID;
			}
			else
			{
				animalCompactData.m_RegionGUID = "";
			}
			return animalCompactData;
		}
		return null;
	}

	public static int IntToTrigger(int i, BaseAi _AI)
	{
		return i switch
		{
			1 => _AI.m_AnimParameter_Attack_Trigger, 
			2 => _AI.m_AnimParameter_DamageImpact_Trigger, 
			3 => _AI.m_AnimParameter_Flinch_Trigger, 
			4 => _AI.m_AnimParameter_Howl_Trigger, 
			5 => _AI.m_AnimParameter_MooseStomp_Trigger, 
			6 => _AI.m_AnimParameter_RandomId_Trigger, 
			7 => _AI.m_AnimParameter_Roar_Trigger, 
			8 => _AI.m_AnimParameter_Stunned_Trigger, 
			9 => _AI.m_AnimParameter_Trigger_PassingAttack, 
			10 => _AI.m_AnimParameter_Trigger_PassingAttackNpc, 
			11 => _AI.m_AnimParameter_Trigger_Spear_Exit_Fail, 
			12 => _AI.m_AnimParameter_Trigger_Spear_Exit_Success, 
			13 => _AI.m_AnimParameter_Trigger_Spear_Exit_Success_Death, 
			14 => _AI.m_AnimParameter_Trigger_Spear_Struggle_Entry, 
			15 => _AI.m_AnimParameter_Trigger_Timberwolf_Attack, 
			_ => 0, 
		};
	}

	public static bool IsEligibleWildlifeController(int id, int sceneId, string sceneGuid)
	{
		if (id < 0 || id >= playersData.Count || playersData[id] == null)
		{
			return false;
		}
		DataStr.MultiPlayerClientData multiPlayerClientData = playersData[id];
		if (id == ClientUser.myId)
		{
			if (!LoadingScreenIsOn && !IsDead && levelid == sceneId)
			{
				return level_guid == sceneGuid;
			}
			return false;
		}
		Client value;
		bool connected = ((!iAmHost) ? (sendMyPosition && multiPlayerClientData.m_Used) : (Server.clients.TryGetValue(id, out value) && value != null && value.IsBusy() && !value.RCON));
		return WildlifeOwnershipPolicy.Eligible(connected, multiPlayerClientData.m_IsLoading, multiPlayerClientData.m_Dead, multiPlayerClientData.m_AnimState == "Knock", multiPlayerClientData.m_Levelid, multiPlayerClientData.m_LevelGuid, sceneId, sceneGuid);
	}

	public static int GetClosestPlayerToAnimal(GameObject animal, int ReTakeChill, int CurrentController, Vector3 V3_Controller, int LevelID_Controller)
	{
		Vector3 position = animal.transform.position;
		BaseAi component = animal.GetComponent<BaseAi>();
		float num = 15f;
		float num2 = Vector3.Distance(position, V3_Controller);
		int result = CurrentController;
		if (component.GetAiMode() == AiMode.Dead || component.GetAiMode() == AiMode.Struggle || component.m_CurrentHP <= 0f || component.m_HasEnteredStruggleOnLastAttack || ReTakeChill > 0)
		{
			return CurrentController;
		}
		for (int i = 0; i < playersData.Count; i++)
		{
			if (i != CurrentController && IsEligibleWildlifeController(i, LevelID_Controller, level_guid))
			{
				Vector3 position2;
				int num3;
				if (iAmHost && i == 0)
				{
					position2 = GameManager.GetPlayerTransform().position;
					num3 = levelid;
				}
				else if (i == ClientUser.myId)
				{
					position2 = GameManager.GetPlayerTransform().position;
					num3 = levelid;
				}
				else
				{
					position2 = playersData[i].m_Position;
					num3 = playersData[i].m_Levelid;
				}
				float num4 = Vector3.Distance(V3_Controller, position2);
				float num5 = Vector3.Distance(position, position2);
				if (num4 > num && playersData[i].m_AnimState != "Knock" && num3 == LevelID_Controller && position2 != new Vector3(0f, 0f, 0f) && num5 < num2)
				{
					num2 = num5;
					result = i;
				}
			}
		}
		return result;
	}

	public static void SendAnimalForValidPlayers(DataStr.AnimalCompactData data, DataStr.AnimalAnimsSync anim)
	{
		if (NoAnimalSync)
		{
			return;
		}
		for (int i = 0; i < playersData.Count; i++)
		{
			if (playersData[i] == null || i == ClientUser.myId || playersData[i].m_Levelid != levelid || !(playersData[i].m_LevelGuid == level_guid))
			{
				continue;
			}
			if (iAmHost)
			{
				ServerSend.ANIMALTEST(0, data, anim, i);
			}
			if (sendMyPosition)
			{
				using Packet packet = new Packet(105);
				packet.Write(data);
				packet.Write(anim);
				packet.Write(i);
				SendUDPData(packet);
			}
		}
	}

	public static DataStr.AnimalAnimsSync GetAnimationDataFromAnimal(BaseAi _AI)
	{
		if ((bool)_AI)
		{
			Animator animator = _AI.m_Animator;
			if ((bool)animator)
			{
				return new DataStr.AnimalAnimsSync
				{
					AP_TurnAngle = animator.GetFloat(_AI.m_AnimParameter_TurnAngle),
					AP_TurnSpeed = animator.GetFloat(_AI.m_AnimParameter_TurnSpeed),
					AP_Speed = animator.GetFloat(_AI.m_AnimParameter_Speed),
					AP_Wounded = animator.GetFloat(_AI.m_AnimParameter_Wounded),
					AP_Roll = animator.GetFloat(_AI.m_AnimParameter_Roll),
					AP_Pitch = animator.GetFloat(_AI.m_AnimParameter_Pitch),
					AP_TargetHeading = animator.GetFloat(_AI.m_AnimParameter_TargetHeading),
					AP_TargetHeadingSmooth = animator.GetFloat(_AI.m_AnimParameter_TargetHeadingSmooth),
					AP_TapMeter = animator.GetFloat(_AI.m_AnimParameter_TapMeter),
					AP_AiState = animator.GetInteger(_AI.m_AnimParameter_AiState),
					AP_Corpse = animator.GetBool(_AI.m_AnimParameter_Corpse),
					AP_Dead = animator.GetBool(_AI.m_AnimParameter_Dead),
					AP_DeadSide = animator.GetInteger(_AI.m_AnimParameter_DamageSide),
					AP_DamageBodyPart = animator.GetInteger(_AI.m_AnimParameter_DamageBodyPart),
					AP_AttackId = animator.GetInteger(_AI.m_AnimParameter_AttackId),
					AP_Stunned = animator.GetBool(_AI.m_AnimParameter_Stunned)
				};
			}
			return null;
		}
		return null;
	}

	public static bool RecreateAnimalToActor(GameObject animal, System.Collections.Generic.List<DataStr.AnimalArrow> Arrows, int nextController)
	{
		if (animal == null)
		{
			return false;
		}
		ObjectGuid component = animal.GetComponent<ObjectGuid>();
		Comps.AnimalUpdates component2 = animal.GetComponent<Comps.AnimalUpdates>();
		string text = ((component != null) ? component.Get() : "");
		if (component2 == null || string.IsNullOrEmpty(text))
		{
			return false;
		}
		string text2 = ObjectGuidManager.GenerateNewGuidString();
		GameObject gameObject = null;
		bool flag = false;
		try
		{
			gameObject = SpawnAnimalActor(GetAnimalPrefabName(animal.name), animal.transform.position, animal.transform.rotation, text2, component2.m_RegionGUID, Arrows);
			if (gameObject == null)
			{
				return false;
			}
			Comps.AnimalActor component3 = gameObject.GetComponent<Comps.AnimalActor>();
			if (component3 == null || component3.m_Animator == null || !gameObject.activeInHierarchy)
			{
				throw new System.InvalidOperationException("Replacement wildlife replica is not ready");
			}
			component3.m_ClientController = nextController;
			component3.m_RegionGUID = component2.m_RegionGUID;
			component3.m_Hp = animal.GetComponent<BaseAi>().m_CurrentHP;
			component.Set(ObjectGuidManager.GenerateNewGuidString());
			flag = true;
			gameObject.GetComponent<ObjectGuid>().Set(text);
			ActorsList.Remove(text2);
			ActorsList[text] = component3;
			return true;
		}
		catch (System.Exception ex)
		{
			ActorsList.Remove(text2);
			if (gameObject != null)
			{
				ObjectGuid component4 = gameObject.GetComponent<ObjectGuid>();
				if (component4 != null)
				{
					component4.Set(ObjectGuidManager.GenerateNewGuidString());
				}
				UnityEngine.Object.Destroy(gameObject);
			}
			if (flag)
			{
				component.Set(text);
			}
			MelonLogger.Warning("[SkyCoop] Wildlife handoff retained native owner " + text + ": " + ex.Message);
			return false;
		}
	}

	public static void AddFakeArrowToAnimal(Comps.AnimalUpdates AU, int index)
	{
		if (!(AU != null) || AU.m_Arrows[index] == null)
		{
			return;
		}
		DataStr.AnimalArrow animalArrow = AU.m_Arrows[index];
		GameObject gearItemObject = GetGearItemObject("GEAR_Arrow");
		if (gearItemObject != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, Vector3.zero, Quaternion.identity);
			ArrowItem component = gameObject.GetComponent<ArrowItem>();
			if (component != null)
			{
				component.ParentToObject(AU.m_Animal.transform.Find(animalArrow.m_LocaName));
				gameObject.transform.localPosition = animalArrow.m_Position;
				gameObject.transform.localEulerAngles = animalArrow.m_Angle;
				Utils.SetIsKinematic(component.m_Rigidbody, enable: true);
			}
			UnityEngine.Object.Destroy(gameObject.GetComponent<ArrowItem>());
			UnityEngine.Object.Destroy(gameObject.GetComponent<GearItem>());
		}
	}

	public static void AddFakeArrowToAnimal(Comps.AnimalActor AU, int index)
	{
		if (!(AU != null) || AU.m_Arrows[index] == null)
		{
			return;
		}
		DataStr.AnimalArrow animalArrow = AU.m_Arrows[index];
		GameObject gearItemObject = GetGearItemObject("GEAR_Arrow");
		if (gearItemObject != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, Vector3.zero, Quaternion.identity);
			ArrowItem component = gameObject.GetComponent<ArrowItem>();
			if (component != null)
			{
				component.ParentToObject(AU.m_Animal.transform.Find(animalArrow.m_LocaName));
				gameObject.transform.localPosition = animalArrow.m_Position;
				gameObject.transform.localEulerAngles = animalArrow.m_Angle;
				Utils.SetIsKinematic(component.m_Rigidbody, enable: true);
			}
			UnityEngine.Object.Destroy(gameObject.GetComponent<ArrowItem>());
			UnityEngine.Object.Destroy(gameObject.GetComponent<GearItem>());
		}
	}

	public static int CountKnownWildlifeInRegion(string regionGuid, System.Collections.Generic.Dictionary<string, GameObject> regionAnimals)
	{
		if (string.IsNullOrEmpty(regionGuid))
		{
			return 0;
		}
		HashSet<string> hashSet = new HashSet<string>(System.StringComparer.Ordinal);
		if (regionAnimals != null)
		{
			foreach (System.Collections.Generic.KeyValuePair<string, GameObject> regionAnimal in regionAnimals)
			{
				if (regionAnimal.Value != null && (regionAnimal.Value.activeInHierarchy || regionAnimal.Value.GetComponent<Comps.AnimalCorpseObject>() != null) && !string.IsNullOrEmpty(regionAnimal.Key))
				{
					hashSet.Add(regionAnimal.Key);
				}
			}
		}
		foreach (System.Collections.Generic.KeyValuePair<string, Comps.AnimalActor> actors in ActorsList)
		{
			if (actors.Value != null && actors.Value.m_Animal != null && actors.Value.m_Animal.activeInHierarchy && actors.Value.m_RegionGUID == regionGuid && !string.IsNullOrEmpty(actors.Key))
			{
				hashSet.Add(actors.Key);
			}
		}
		foreach (System.Collections.Generic.KeyValuePair<string, Comps.AnimalCorpseObject> animalCorples in AnimalCorplesList)
		{
			if (animalCorples.Value != null && animalCorples.Value.m_Animal != null && animalCorples.Value.m_RegionGUID == regionGuid && !string.IsNullOrEmpty(animalCorples.Key))
			{
				hashSet.Add(animalCorples.Key);
			}
		}
		Il2CppSystem.Collections.Generic.List<BaseAi>.Enumerator enumerator4 = BaseAiManager.m_BaseAis.GetEnumerator();
		while (enumerator4.MoveNext())
		{
			BaseAi current4 = enumerator4.Current;
			if (current4 == null || current4.gameObject == null || !current4.gameObject.activeInHierarchy)
			{
				continue;
			}
			Comps.AnimalUpdates component = current4.gameObject.GetComponent<Comps.AnimalUpdates>();
			ObjectGuid component2 = current4.gameObject.GetComponent<ObjectGuid>();
			if (component != null && component.m_RegionGUID == regionGuid && component2 != null)
			{
				string text = component2.Get();
				if (!string.IsNullOrEmpty(text))
				{
					hashSet.Add(text);
				}
			}
		}
		return hashSet.Count;
	}

	public static void SwitchToAnimalController()
	{
		System.Collections.Generic.Dictionary<string, int> dictionary = new System.Collections.Generic.Dictionary<string, int>();
		if (ActorsList.Count > 0)
		{
			foreach (System.Collections.Generic.KeyValuePair<string, Comps.AnimalActor> actors in ActorsList)
			{
				actors.Value.m_ClientController = ClientUser.myId;
				string regionGUID = actors.Value.m_RegionGUID;
				if (regionGUID != "")
				{
					if (!dictionary.TryGetValue(regionGUID, out var value))
					{
						dictionary.Add(regionGUID, 1);
						continue;
					}
					dictionary.Remove(regionGUID);
					dictionary.Add(regionGUID, value + 1);
				}
			}
		}
		if (BaseAiManager.m_BaseAis.Count > 0)
		{
			Il2CppSystem.Collections.Generic.List<BaseAi>.Enumerator enumerator2 = BaseAiManager.m_BaseAis.GetEnumerator();
			while (enumerator2.MoveNext())
			{
				BaseAi current2 = enumerator2.Current;
				if (!(current2 != null) || !(current2.gameObject != null) || !current2.gameObject.GetComponent<Comps.AnimalUpdates>())
				{
					continue;
				}
				string regionGUID2 = current2.gameObject.GetComponent<Comps.AnimalUpdates>().m_RegionGUID;
				if (regionGUID2 != "")
				{
					if (!dictionary.TryGetValue(regionGUID2, out var value2))
					{
						dictionary.Add(regionGUID2, 1);
						continue;
					}
					dictionary.Remove(regionGUID2);
					dictionary.Add(regionGUID2, value2 + 1);
				}
			}
		}
		if (AnimalCorplesList.Count > 0)
		{
			foreach (System.Collections.Generic.KeyValuePair<string, Comps.AnimalCorpseObject> animalCorples in AnimalCorplesList)
			{
				string regionGUID3 = animalCorples.Value.m_RegionGUID;
				if (regionGUID3 != "")
				{
					if (!dictionary.TryGetValue(regionGUID3, out var value3))
					{
						dictionary.Add(regionGUID3, 1);
						continue;
					}
					dictionary.Remove(regionGUID3);
					dictionary.Add(regionGUID3, value3 + 1);
				}
			}
		}
		if (dictionary.Count <= 0)
		{
			return;
		}
		foreach (System.Collections.Generic.KeyValuePair<string, int> item in dictionary)
		{
			GameObject gameObject = ObjectGuidManager.Lookup(item.Key);
			if ((bool)gameObject && gameObject.GetComponent<Comps.SpawnRegionSimple>() != null)
			{
				gameObject.GetComponent<Comps.SpawnRegionSimple>().m_Spawned = item.Value;
			}
		}
	}

	public static bool RecreateAnimalToSyncable(GameObject animal, string RegionGUID, float health, System.Collections.Generic.List<DataStr.AnimalArrow> Arrows)
	{
		if (animal == null)
		{
			return false;
		}
		ObjectGuid component = animal.GetComponent<ObjectGuid>();
		string text = ((component != null) ? component.Get() : "");
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		Vector3 position = animal.transform.position;
		Quaternion rotation = animal.transform.rotation;
		if (!AiUtils.IsNavmeshPosValid(position, 0.5f, 1f))
		{
			return false;
		}
		GameObject prefab = WildlifeCompat.GetPrefab(GetAnimalPrefabName(animal.name));
		if (prefab == null)
		{
			return false;
		}
		GameObject gameObject = null;
		bool flag = false;
		try
		{
			gameObject = UnityEngine.Object.Instantiate(prefab, position, rotation);
			BaseAi component2 = gameObject.GetComponent<BaseAi>();
			if (component2 == null)
			{
				throw new System.InvalidOperationException("Wildlife prefab has no native BaseAi");
			}
			BaseAiManager.CreateMoveAgent(component2.transform, component2, position);
			Comps.AnimalUpdates animalUpdates = gameObject.GetComponent<Comps.AnimalUpdates>();
			if (animalUpdates == null)
			{
				animalUpdates = gameObject.AddComponent<Comps.AnimalUpdates>();
			}
			animalUpdates.m_Animal = gameObject;
			animalUpdates.m_ToGo = position;
			animalUpdates.m_MyControlled = true;
			animalUpdates.m_RegionGUID = RegionGUID;
			animalUpdates.m_Arrows = Arrows ?? new System.Collections.Generic.List<DataStr.AnimalArrow>();
			if (health != -1f)
			{
				animalUpdates.m_Hp = health;
				component2.m_CurrentHP = health;
			}
			animalUpdates.ProcessArrows();
			gameObject.transform.position = position;
			if (!gameObject.activeInHierarchy || component2.m_Animator == null)
			{
				throw new System.InvalidOperationException("Replacement native wildlife is inactive or has no animator");
			}
			ObjectGuid objectGuid = gameObject.GetComponent<ObjectGuid>();
			if (objectGuid == null)
			{
				objectGuid = gameObject.AddComponent<ObjectGuid>();
			}
			component.Set(ObjectGuidManager.GenerateNewGuidString());
			flag = true;
			objectGuid.Set(text);
			if (ActorsList.TryGetValue(text, out var value) && value != null && value.m_Animal == animal)
			{
				ActorsList.Remove(text);
			}
			return true;
		}
		catch (System.Exception ex)
		{
			if (gameObject != null)
			{
				ObjectGuid component3 = gameObject.GetComponent<ObjectGuid>();
				if (component3 != null)
				{
					component3.Set(ObjectGuidManager.GenerateNewGuidString());
				}
				Comps.AnimalUpdates component4 = gameObject.GetComponent<Comps.AnimalUpdates>();
				if (component4 != null)
				{
					component4.m_Banned = true;
					component4.m_MarkToDestroy = true;
				}
				UnityEngine.Object.Destroy(gameObject);
			}
			if (flag)
			{
				component.Set(text);
			}
			MelonLogger.Warning("[SkyCoop] Wildlife takeover retained replica " + text + ": " + ex.Message);
			return false;
		}
	}

	public static int CalculateTargetPopulation(SpawnRegion m_Region)
	{
		return 1;
	}

	public static bool SpawnInBlizzard(SpawnRegion m_Region)
	{
		if (GameManager.GetWeatherComponent().IsBlizzard())
		{
			return m_Region.m_CanSpawnInBlizzard;
		}
		return true;
	}

	public static void ExitHarvesting()
	{
		Panel_BodyHarvest panel_BodyHarvest = GameCompat.Panel<Panel_BodyHarvest>();
		if (panel_BodyHarvest.m_CurrentHarvestAction != Panel_BodyHarvest.HarvestAction.None)
		{
			panel_BodyHarvest.OnCancel();
		}
		else
		{
			panel_BodyHarvest.OnBack();
		}
	}

	public static GearItem GetGearItemPrefab(string name)
	{
		GameObject gearItemObject = GetGearItemObject(name);
		if (!(gearItemObject == null))
		{
			return gearItemObject.GetComponent<GearItem>();
		}
		return null;
	}

	public static GameObject GetGearItemObject(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		GameObject gameObject = Resources.Load<GameObject>(name);
		if (gameObject != null)
		{
			return gameObject;
		}
		if (string.IsNullOrEmpty(level_name) || level_name == "Empty" || level_name == "Boot")
		{
			return null;
		}
		MelonLogger.Msg("[Gear load] Native lookup " + name + " scene=" + level_name);
		if (name.StartsWith("GEAR_", System.StringComparison.Ordinal))
		{
			GearItem gearItem = GearItem.LoadGearItemPrefab(name);
			if (gearItem != null)
			{
				return gearItem.gameObject;
			}
		}
		AssetHelper.TryLoadAsset<GameObject>(name, out var asset);
		return asset;
	}

	private static void DoneDisconnect(System.IAsyncResult _result)
	{
		if (ClientUser.udp != null && ClientUser.udp.socket != null)
		{
			ClientUser.udp.endPoint = null;
			ClientUser.udp.socket.Client.Close();
		}
	}

	public static void Disconnect(bool checkpoint = true)
	{
		if (checkpoint && !IReallyWantToQuit && !QuitWithoutSaving && !DedicatedServerAppMode && InOnline() && level_name != "Empty" && level_name != "Boot" && !Shared.IsMainMenuScene(level_name))
		{
			RequestSaveBeforeDisconnect();
		}
		else
		{
			if ((checkpoint && SaveExitPending && !IReallyWantToQuit) || (!iAmHost && !sendMyPosition))
			{
				return;
			}
			if (SteamServerWorks != "" && SteamConnect.CanUseSteam)
			{
				MelonLogger.Msg("[Steamworks.NET] Disconnecting...");
				string steamServerWorks = SteamServerWorks;
				try
				{
					SteamConnect.Main.Disconnect(steamServerWorks);
					return;
				}
				finally
				{
					sendMyPosition = false;
					ClientUser.myId = 0;
					ConnectedSteamWorks = false;
					SteamServerWorks = "";
					SteamGameJoin.Suspend();
				}
			}
			if (ClientUser.udp == null)
			{
				MelonLogger.Msg("UDP is already closed.");
				return;
			}
			if (ClientUser.udp.socket == null)
			{
				MelonLogger.Msg("Socket of udp is closed.");
				return;
			}
			if (ClientUser.udp != null && ClientUser.udp.socket != null && ClientUser.udp.socket.Client != null)
			{
				ClientUser.udp.socket.Client.BeginDisconnect(reuseSocket: true, DoneDisconnect, null);
			}
			HUDMessage.AddMessage("DISCONNECTED FROM SERVER");
			MelonLogger.Msg("[UDP] Disconnected from server");
			sendMyPosition = false;
			ClientUser.myId = 0;
		}
	}

	public static void SendUDPData(Packet _packet, bool IgnoreQuit = false)
	{
		if (SteamConnect.CanUseSteam && ConnectedSteamWorks)
		{
			_packet.WriteLength();
			_packet.InsertClientInfo(ClientUser.myId, ClientUser.SubNetworkClientGUID);
			SteamConnect.Main.SendUDPData(_packet, SteamServerWorks);
		}
		else
		{
			ClientUser.udp.SendData(_packet, IgnoreQuit);
		}
	}

	public static bool IsEquippable(GearItem gi)
	{
		if ((bool)gi.m_MatchesItem || (bool)gi.m_KeroseneLampItem || (bool)gi.m_FlareItem || (bool)gi.m_NoiseMakerItem || (bool)gi.m_TorchItem || (bool)gi.m_EmergencyStim || (bool)gi.m_FlashlightItem || (bool)gi.m_FirstPersonItem)
		{
			return true;
		}
		return false;
	}

	public static void GiveRecivedItem(DataStr.GearItemDataPacket gearData)
	{
		GearItemSaveDataProxy gearItemSaveDataProxy = Utils.DeserializeObject<GearItemSaveDataProxy>(gearData.m_DataProxy);
		string gearName = gearData.m_GearName;
		string text = "";
		string text2 = "";
		bool flag = false;
		LiquidQuality quality = LiquidQuality.Potable;
		if (gearName.Contains("(Clone)"))
		{
			int startIndex = gearName.Length - 7;
			text = gearName.Remove(startIndex, 7);
		}
		else
		{
			text = gearName;
		}
		if (text == "GEAR_WaterSupplyPotable")
		{
			flag = true;
		}
		if (text == "GEAR_WaterSupplyNotPotable")
		{
			flag = true;
			quality = LiquidQuality.NonPotable;
		}
		if (!flag)
		{
			GearItem closestMatchStackable = GameManager.GetInventoryComponent().GetClosestMatchStackable(text, gearItemSaveDataProxy.m_NormalizedCondition);
			if (closestMatchStackable == null)
			{
				MelonLogger.Msg("Not stack for item " + text + ", creating new item");
				GearItem gearItem = GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory(text, 1);
				DisableObjectForXPMode.RemoveDisabler(gearItem.gameObject);
				Utils.DeserializeObject<GearItemSaveDataProxy>(gearData.m_DataProxy);
				gearItem.m_LastUpdatedTODHours = 0f;
				gearItem.DeserializeFromJson(gearData.m_DataProxy);
				text2 = gearItem.DisplayName;
				if (GameManager.GetPlayerManagerComponent().m_ItemInHands == null && IsEquippable(gearItem))
				{
					GameManager.GetPlayerManagerComponent().EquipItem(gearItem, fromDeserialize: false);
				}
				PatchBookReadTime(gearItem.GetComponent<GearItem>());
			}
			else
			{
				closestMatchStackable.m_StackableItem.m_Units++;
				MelonLogger.Msg("Found stack for " + text + ", now units " + closestMatchStackable.m_StackableItem.m_Units);
				if (GameManager.GetPlayerManagerComponent().m_ItemInHands == null && IsEquippable(closestMatchStackable))
				{
					GameManager.GetPlayerManagerComponent().EquipItem(closestMatchStackable, fromDeserialize: false);
				}
			}
		}
		else
		{
			string displayName = Resources.Load(text).Cast<GameObject>().GetComponent<GearItem>()
				.DisplayName;
			MelonLogger.Msg("Got water " + gearData.m_Water);
			text2 = ((gearData.m_Water != 0.5f) ? (gearData.m_Water + " of " + displayName) : ("half liter of " + displayName));
			GameManager.GetInventoryComponent().AddToWaterSupply(GameCompat.Liters(gearData.m_Water), quality);
		}
		HUDMessage.AddMessage("Other player gave you " + text2 + ".");
		MelonLogger.Msg("Other player gave you item " + text);
	}

	public static void SimRevive()
	{
		GameCompat.Panel<Panel_Log>().Enable(enable: false);
		SetRevivedStats();
		GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Normal);
	}

	public static void DeleteAnimal(string _guid)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(_guid);
		if (gameObject != null)
		{
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	// A guest that loses the controller role used to keep its real, now unsynced animals: frozen, unkillable
	// for it and unknown to the new controller. Remove them; the controller's updates recreate them as actors.
	public static void DropLocallyControlledAnimals()
	{
		if (iAmHost || BaseAiManager.m_BaseAis == null)
		{
			return;
		}
		int removed = 0;
		for (int i = BaseAiManager.m_BaseAis.Count - 1; i >= 0; i--)
		{
			BaseAi baseAi = BaseAiManager.m_BaseAis[i];
			if (baseAi == null || baseAi.gameObject == null || baseAi.gameObject.GetComponent<Comps.AnimalActor>() != null || baseAi.GetAiMode() == AiMode.Dead)
			{
				continue;
			}
			ObjectGuid objectGuid = baseAi.gameObject.GetComponent<ObjectGuid>();
			if (objectGuid != null)
			{
				ObjectGuidManager.UnRegisterGuid(objectGuid.Get());
			}
			UnityEngine.Object.Destroy(baseAi.gameObject);
			removed++;
		}
		MelonLogger.Msg("[Wildlife] Lost controller role, removed " + removed + " local animals");
	}

	public static void DoAnimalDamage(string guid, float damage)
	{
		MelonLogger.Msg("Other player damage animal " + guid + " on " + damage);
		GameObject gameObject = ObjectGuidManager.Lookup(guid);
		if (!gameObject)
		{
			MelonLogger.Warning("[Wildlife] Damage for unknown animal " + guid + " (controller=" + AnimalsController + ", scene=" + level_guid + ")");
		}
		if ((bool)gameObject)
		{
			BaseAi component = gameObject.GetComponent<BaseAi>();
			if ((bool)component)
			{
				component.ApplyDamage(damage, DamageSource.Player, "");
				MelonLogger.Msg("After apply damage current HP Is " + component.m_CurrentHP);
			}
		}
	}

	public static void SetAnimationParams(GameObject animal, DataStr.AnimalAnimsSync obj)
	{
		Comps.AnimalActor component = animal.GetComponent<Comps.AnimalActor>();
		if ((bool)component)
		{
			component.AP_TurnAngle = obj.AP_TurnAngle;
			component.AP_TurnSpeed = obj.AP_TurnSpeed;
			component.AP_Speed = obj.AP_Speed;
			component.AP_Wounded = obj.AP_Wounded;
			component.AP_Roll = obj.AP_Roll;
			component.AP_Pitch = obj.AP_Pitch;
			component.AP_TargetHeading = obj.AP_TargetHeading;
			component.AP_TargetHeadingSmooth = obj.AP_TargetHeadingSmooth;
			component.AP_TapMeter = obj.AP_TapMeter;
			component.AP_AiState = obj.AP_AiState;
			component.AP_Corpse = obj.AP_Corpse;
			component.AP_Dead = obj.AP_Dead;
			component.AP_DeadSide = obj.AP_DeadSide;
			component.AP_DamageBodyPart = obj.AP_DamageBodyPart;
			component.AP_AttackId = obj.AP_AttackId;
			component.AP_Stunned = obj.AP_Stunned;
		}
	}

	public static void SetAnimalPosition(GameObject animal, Vector3 xyz, Quaternion xyzw)
	{
		Comps.AnimalActor component = animal.GetComponent<Comps.AnimalActor>();
		if ((bool)component)
		{
			component.m_ToGo = xyz;
			component.m_ToRotate = xyzw;
		}
	}

	public static void DoAnimalSync(DataStr.AnimalCompactData dat, DataStr.AnimalAnimsSync anim)
	{
		if (NoAnimalSync)
		{
			return;
		}
		GameObject gameObject = ObjectGuidManager.Lookup(dat.m_GUID);
		if ((bool)gameObject)
		{
			if (!(gameObject.GetComponent<Comps.AnimalActor>() != null))
			{
				return;
			}
			Comps.AnimalActor component = gameObject.GetComponent<Comps.AnimalActor>();
			if (component.m_MarkToDestroy)
			{
				return;
			}
			component.NoResponce = 5;
			component.m_Hp = dat.m_Health;
			component.m_RegionGUID = dat.m_RegionGUID;
			component.m_ClientController = dat.m_LastController;
			SetAnimationParams(gameObject, anim);
			SetAnimalPosition(gameObject, dat.m_Position, dat.m_Rotation);
			component.m_NextMode = (AiMode)dat.m_LastAiMode;
			component.SetAiMode();
			int count = component.m_Arrows.Count;
			if (count < dat.m_Arrows.Count)
			{
				component.m_Arrows = dat.m_Arrows;
				for (int i = count; i < component.m_Arrows.Count; i++)
				{
					AddFakeArrowToAnimal(component, i);
				}
			}
		}
		else
		{
			gameObject = SpawnAnimalActor(dat.m_PrefabName, dat.m_Position, dat.m_Rotation, dat.m_GUID, dat.m_RegionGUID, dat.m_Arrows);
			if ((bool)gameObject && (bool)gameObject.GetComponent<Comps.AnimalActor>())
			{
				Comps.AnimalActor component2 = gameObject.GetComponent<Comps.AnimalActor>();
				component2.NoResponce = 5;
				component2.m_Hp = dat.m_Health;
				component2.m_RegionGUID = dat.m_RegionGUID;
				component2.m_ClientController = dat.m_LastController;
				SetAnimationParams(gameObject, anim);
				SetAnimalPosition(gameObject, dat.m_Position, dat.m_Rotation);
				component2.m_NextMode = (AiMode)dat.m_LastAiMode;
				component2.SetAiMode();
			}
		}
	}

	public static void SetAnimalTriggers(DataStr.AnimalTrigger obj)
	{
		string guid = obj.m_Guid;
		int trigger = obj.m_Trigger;
		GameObject gameObject = ObjectGuidManager.Lookup(guid);
		if (gameObject != null && gameObject.GetComponent<Comps.AnimalActor>() != null)
		{
			Animator animator = gameObject.GetComponent<Comps.AnimalActor>().m_Animator;
			if (trigger == Animator.StringToHash("StruggleEnd"))
			{
				animator.ResetTrigger(Animator.StringToHash("StruggleStart"));
			}
			else if (trigger == Animator.StringToHash("StruggleStart"))
			{
				animator.ResetTrigger(Animator.StringToHash("StruggleEnd"));
			}
			animator.ResetTrigger(trigger);
		}
	}

	public static void DoShootFX(Vector3 pos)
	{
		GameObject gameObject = new GameObject();
		gameObject.transform.position = pos;
		Light light = gameObject.AddComponent<Light>();
		light.type = LightType.Point;
		light.range = 5f;
		light.intensity = 5f;
		light.color = new Color(1f, 0.5623099f, 0.3268814f, 1f);
		UnityEngine.Object.Destroy(gameObject, 0.1f);
	}

	public static void DoShootSync(DataStr.ShootSync shoot, int from)
	{
		if (shoot.m_sceneguid != level_guid)
		{
			return;
		}
		if (shoot.m_projectilename == "GEAR_FlareGunAmmoSingle")
		{
			FlareGunRoundItem.SpawnAndFire(GetGearItemObject("GEAR_FlareGunAmmoSingle"), shoot.m_position, shoot.m_rotation);
			if (shoot.m_lookat)
			{
				GameManager.GetPlayerAnimationComponent().LookAt(shoot.m_position);
			}
			return;
		}
		if (shoot.m_projectilename == "GEAR_Arrow")
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(GetGearItemObject("GEAR_Arrow"), shoot.m_position, shoot.m_rotation);
			GearItem component = gameObject.GetComponent<GearItem>();
			gameObject.name = "GEAR_Arrow";
			gameObject.transform.parent = null;
			component.m_InPlayerInventory = false;
			component.m_StackableItem.m_Units = 1;
			component.m_CurrentHP = 100f;
			component.m_ArrowItem.SetPlacementHelperEnabled(enabled: true);
			gameObject.AddComponent<Comps.DestoryArrowOnHit>();
			Utils.ChangeLayersForGearItem(gameObject, 17);
			component.m_ArrowItem.Fire(1f);
			return;
		}
		if (shoot.m_projectilename == "GEAR_Stone")
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(GetGearItemObject("GEAR_Stone"), shoot.m_position, shoot.m_rotation);
			float throwForce = GameManager.m_PlayerManager.m_ThrowForce;
			float throwTorque = GameManager.m_PlayerManager.m_ThrowTorque;
			GearItem component2 = gameObject2.GetComponent<GearItem>();
			component2.m_StoneItem.PrepareForThrow();
			component2.m_StoneItem.SetThrown(thrown: true);
			throwForce = component2.m_StoneItem.m_ThrowForce;
			throwTorque = component2.m_StoneItem.m_ThrowForce;
			Rigidbody component3 = gameObject2.GetComponent<Rigidbody>();
			component3.isKinematic = false;
			component3.velocity = shoot.m_camera_forward * throwForce;
			Vector3 vector = shoot.m_camera_right + UnityEngine.Random.Range(-0.2f, 0.2f) * shoot.m_camera_up;
			vector.Normalize();
			component3.angularVelocity = vector * throwTorque;
			component3.angularDrag = 0f;
			component3.drag = 0f;
			Comps.DestoryStoneOnStop destoryStoneOnStop = gameObject2.AddComponent<Comps.DestoryStoneOnStop>();
			destoryStoneOnStop.m_Obj = gameObject2;
			destoryStoneOnStop.m_RB = component3;
			destoryStoneOnStop.m_Gear = component2;
			return;
		}
		if (shoot.m_projectilename == "GEAR_NoiseMaker")
		{
			GameObject gameObject3 = UnityEngine.Object.Instantiate(GetGearItemObject("GEAR_NoiseMaker"), shoot.m_position, shoot.m_rotation);
			float throwForce2 = GameManager.m_PlayerManager.m_ThrowForce;
			float throwTorque2 = GameManager.m_PlayerManager.m_ThrowTorque;
			GearItem component4 = gameObject3.GetComponent<GearItem>();
			if ((bool)component4)
			{
				NoiseMakerItem noiseMakerItem = component4.m_NoiseMakerItem;
				if ((bool)noiseMakerItem)
				{
					noiseMakerItem.m_CanThrow = false;
					noiseMakerItem.Ignite();
					noiseMakerItem.m_PlayerDamageInflictionInRadius = 15f;
					noiseMakerItem.m_PlayerDamageRadius = noiseMakerItem.m_AIDamageRadius;
					if (shoot.m_skill == 0f)
					{
						MelonLogger.Msg("GEAR_NoiseMaker Throw sync");
						noiseMakerItem.PrepareForThrow();
						noiseMakerItem.m_Thrown = true;
						throwForce2 = noiseMakerItem.m_ThrowForce;
						throwTorque2 = noiseMakerItem.m_ThrowTorque;
						Rigidbody component5 = component4.GetComponent<Rigidbody>();
						if (!(component5 == null))
						{
							Utils.SetIsKinematic(component5, enable: false);
							component5.velocity = shoot.m_camera_forward * throwForce2;
							Vector3 vector2 = shoot.m_camera_right + UnityEngine.Random.Range(-0.2f, 0.2f) * shoot.m_camera_up;
							vector2.Normalize();
							component5.angularVelocity = vector2 * throwTorque2;
							component5.angularDrag = 0f;
							component5.drag = 0f;
						}
					}
					else
					{
						MelonLogger.Msg("GEAR_NoiseMaker in hand detonation sync");
						noiseMakerItem.PerformDetonation((ContactPoint[]?)null);
					}
				}
				else
				{
					MelonLogger.Msg(System.ConsoleColor.Red, "GEAR_NoiseMaker NoiseMakerItem!!!");
				}
			}
			else
			{
				MelonLogger.Msg(System.ConsoleColor.Red, "GEAR_NoiseMaker Has not GearItem!!!");
			}
			return;
		}
		if (shoot.m_projectilename == "Melee")
		{
			DoMeleeHitFX(shoot.m_position, shoot.m_camera_forward, shoot.m_rotation, players[from]);
			return;
		}
		GameObject gameObject4 = null;
		GearItem gearItem = null;
		if (shoot.m_projectilename == "PistolBullet")
		{
			gearItem = GetGearItemPrefab("GEAR_Rifle");
			gameObject4 = UnityEngine.Object.Instantiate(PistolBulletPrefab, shoot.m_position, shoot.m_rotation);
			gameObject4.AddComponent<Comps.ClientProjectile>();
			gameObject4.GetComponent<Comps.ClientProjectile>().m_ClientID = from;
			PlayRemoteGunshot("PLAY_RIFLEFIRE", from);
			DoShootFX(shoot.m_position);
			if (players[from] != null && players[from].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
			{
				string preAnimStateHands = "RifleShoot";
				if (playersData[from] != null && playersData[from].m_AnimState == "Ctrl")
				{
					preAnimStateHands = "RifleShoot_Sit";
				}
				players[from].GetComponent<Comps.MultiplayerPlayerAnimator>().m_PreAnimStateHands = preAnimStateHands;
			}
		}
		else if (shoot.m_projectilename == "RevolverBullet")
		{
			gearItem = GetGearItemPrefab("GEAR_Revolver");
			gameObject4 = UnityEngine.Object.Instantiate(RevolverBulletPrefab, shoot.m_position, shoot.m_rotation);
			gameObject4.AddComponent<Comps.ClientProjectile>();
			gameObject4.GetComponent<Comps.ClientProjectile>().m_ClientID = from;
			PlayRemoteGunshot("PLAY_REVOLVERFIRE", from);
			DoShootFX(shoot.m_position);
		}
		if (gearItem == null || gameObject4 == null)
		{
			return;
		}
		gameObject4.hideFlags = HideFlags.HideInHierarchy;
		vp_Bullet component6 = gameObject4.GetComponent<vp_Bullet>();
		if ((bool)gearItem)
		{
			GunItem component7 = gearItem.GetComponent<GunItem>();
			if ((bool)component7)
			{
				component6.m_GunType = component7.m_GunType;
				component6.MinDistanceForAimAssist = component7.m_MinDistanceForAimAssist;
				component6.Damage = 0f;
				component6.Accuracy = ((component6.m_GunType != GunType.Rifle) ? component7.m_AccuracyRange : shoot.m_skill);
				component6.m_ImpactAudio = component7.m_ImpactAudio;
			}
		}
		if (players[from] != null)
		{
			GameAudioManager.NotifyAiAudioEvent(players[from], players[from].transform.position, GameAudioAiEvent.Gunshot);
		}
	}

	public static void DoSyncContainer(DataStr.ContainerOpenSync sync)
	{
		Il2CppSystem.Collections.Generic.List<Container> containers = ContainerManager.m_Containers;
		for (int i = 0; i < containers.Count; i++)
		{
			Container container = containers[i];
			if (container != null && container.GetComponent<Comps.ContainersSync>() != null && container.GetComponent<Comps.ContainersSync>().m_Guid == sync.m_Guid)
			{
				if (sync.m_State)
				{
					container.GetComponent<Comps.ContainersSync>().Open();
				}
				else
				{
					container.GetComponent<Comps.ContainersSync>().Close();
				}
				break;
			}
		}
	}

	private static void MainThread()
	{
	}

	public static Vector3 SnappedPosition(Vector3 pointToSnap, Vector3 blockCenterPosition)
	{
		Vector3 vector = pointToSnap - blockCenterPosition;
		if (vector.x == 0.5f)
		{
			return blockCenterPosition + Vector3.right;
		}
		if (vector.x == -0.5f)
		{
			return blockCenterPosition + Vector3.left;
		}
		if (vector.y == 0.5f)
		{
			return blockCenterPosition + Vector3.up;
		}
		if (vector.y == -0.5f)
		{
			return blockCenterPosition + Vector3.down;
		}
		if (vector.z == 0.5f)
		{
			return blockCenterPosition + Vector3.forward;
		}
		if (vector.z == -0.5f)
		{
			return blockCenterPosition + Vector3.back;
		}
		return Vector3.zero;
	}

	public static void MaybeLeaveFootPrint(Vector3 footPos, GameObject ply, bool bothFeet = false, float forceDeepFrac = 0f, bool leftfoot = false)
	{
		bool flag = false;
		bool flag2 = leftfoot;
		float num = 0f;
		if (GameManager.GetWeatherComponent().IsIndoorEnvironment())
		{
			return;
		}
		if (flag && !bothFeet)
		{
			flag = false;
			return;
		}
		footPos.y += 1f;
		Transform transform = ply.transform;
		Transform transform2 = ply.transform;
		int num2 = ((!bothFeet) ? 1 : 2);
		bool flag3 = flag2;
		Vector3 vector = transform.right * ((!flag2) ? num : (0f - num));
		for (int i = 0; i < num2; i++)
		{
			Vector3 vector2 = transform.forward * 0f;
			Vector3 heelPos = footPos - vector2 + vector;
			if (i > 0)
			{
				heelPos -= vector2 * (UnityEngine.Random.value * 0.25f);
			}
			if (GameManager.GetFootstepTrailManager().IsFootprintPositionValid(heelPos, vector2, 0f, out var point, out var normal))
			{
				if (SnowPatchManager.m_Active)
				{
					GameManager.GetFootstepTrailManager().AddPlayerFootstep(transform2.position, point, normal, transform.rotation.eulerAngles.y, flag3, forceDeepFrac);
				}
				vector = -vector;
				flag3 = !flag3;
			}
		}
		flag = bothFeet;
	}

	public static void SimpleSleepWithNoSleep(int hours)
	{
		Rest restComponent = GameManager.GetRestComponent();
		GameManager.GetPlayerManagerComponent().m_God = true;
		restComponent.m_Sleeping = true;
		restComponent.m_ForceSleepLength = true;
		restComponent.m_ForcedSleepLength = hours;
		restComponent.m_PassTimeOptions = Rest.PassTimeOptions.None;
		restComponent.AccelerateTimeOfDay((int)((double)hours * 60.0), restComponent.m_SleepFadeOutSeconds, useFade: false);
		restComponent.m_NumSecondsSleeping = 0f;
		restComponent.m_SleepDurationHours = hours;
		restComponent.m_SleepDurationSeconds = (float)((double)hours * 60.0 * 60.0);
		restComponent.m_ShouldInterruptWhenFreezing = false;
		GameManager.GetRestComponent().m_WakeUpAtFullRest = false;
		MelonLogger.Msg("Called fake sleep for " + hours + " hours");
	}

	public static void SendConsume(bool IsDrink)
	{
		if (sendMyPosition)
		{
			using Packet packet = new Packet(57);
			packet.Write(IsDrink);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ServerSend.CONSUME(0, IsDrink, toEveryOne: true);
		}
		if (IsDrink)
		{
			PushActionToMyDoll("Drink");
		}
		else
		{
			PushActionToMyDoll("Eat");
		}
	}

	public static void SendStopConsume()
	{
		if (sendMyPosition)
		{
			using Packet packet = new Packet(59);
			packet.Write(MyAnimState);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ServerSend.STOPCONSUME(0, MyAnimState, toEveryOne: true);
		}
	}

	public static void MakeFakeFire(Fire fire)
	{
		fire.m_StartedByPlayer = false;
		if (fire.m_FireState != FireState.FullBurn)
		{
			fire.FireStateSet(FireState.FullBurn);
		}
		fire.m_HeatSource.TurnOn();
		fire.m_FX.TriggerStage(FireState.FullBurn, shouldPlay: true, stopOtherStages: true);
		fire.m_FuelHeatIncrease = fire.m_HeatSource.m_MaxTempIncrease;
		fire.m_ElapsedOnTODSeconds = 0f;
		fire.m_ElapsedOnTODSecondsUnmodified = 0f;
		fire.ForceBurnTimeInMinutes(5f);
		fire.PlayFireLoop(100f);
		if (fire.m_Campfire != null)
		{
			Campfire component = fire.m_Campfire.GetComponent<Campfire>();
			if (component.m_State != CampfireState.Lit)
			{
				component.SetState(CampfireState.Lit);
			}
		}
	}

	public static void MakeFakeCampfire(DataStr.FireSourcesSync SyncData)
	{
		Fire fire = GameManager.GetFireManagerComponent().InstantiateCampFire();
		GameObject obj = ((fire != null) ? fire.gameObject : null);
		obj.name = "CAMPFIRE";
		obj.transform.position = SyncData.m_Position;
		obj.transform.rotation = SyncData.m_Rotation;
		Fire component = obj.GetComponent<Fire>();
		MakeFakeFire(component);
		FireHeatSync.Apply(component, SyncData);
		Campfire component2 = obj.GetComponent<Campfire>();
		if (component2.m_State != CampfireState.Lit)
		{
			component2.SetState(CampfireState.Lit);
		}
	}

	public static bool IsSameFire(DataStr.FireSourcesSync FindData, DataStr.FireSourcesSync SyncData)
	{
		if (SyncData.m_IsCampfire)
		{
			if (SyncData.m_Position == FindData.m_Position)
			{
				return true;
			}
		}
		else if (FindData.m_Guid != "")
		{
			if (SyncData.m_Guid == FindData.m_Guid)
			{
				return true;
			}
		}
		else if (SyncData.m_Position == FindData.m_Position)
		{
			return true;
		}
		return false;
	}

	public static void ApplyOtherFireSource(DataStr.FireSourcesSync SyncData)
	{
		if (!ApplyOtherCampfires || ServerConfig.m_FireSync == 0)
		{
			return;
		}
		bool flag = false;
		for (int i = 0; i < FireManager.m_Fires.Count; i++)
		{
			Fire fire = FireManager.m_Fires[i];
			if (!(fire != null))
			{
				continue;
			}
			DataStr.FireSourcesSync fireSourcesSync = new DataStr.FireSourcesSync();
			fireSourcesSync.m_LevelId = levelid;
			fireSourcesSync.m_LevelGUID = level_guid;
			fireSourcesSync.m_Position = fire.gameObject.transform.position;
			fireSourcesSync.m_Rotation = fire.gameObject.transform.rotation;
			fireSourcesSync.m_Guid = "";
			if (fire.gameObject.GetComponent<ObjectGuid>() != null)
			{
				fireSourcesSync.m_Guid = fire.gameObject.GetComponent<ObjectGuid>().Get();
			}
			if (IsSameFire(fireSourcesSync, SyncData))
			{
				if (fire.m_FireState == FireState.Off || !fire.m_StartedByPlayer)
				{
					MakeFakeFire(fire);
					FireHeatSync.Apply(fire, SyncData);
					flag = true;
				}
				else
				{
					flag = true;
				}
				break;
			}
		}
		if (!flag && SyncData.m_IsCampfire)
		{
			MakeFakeCampfire(SyncData);
		}
	}

	public static void AddFuelNoSync(Fire fire, GearItem fuel)
	{
		if (fire.gameObject != null && fuel != null)
		{
			MelonLogger.Msg("[Fire][AddFuelNoSync] " + fire.gameObject.name + " fuel is " + fuel.GetGearName());
		}
		else if (fire.gameObject != null && fuel == null)
		{
			MelonLogger.Msg("[Fire][AddFuelNoSync] " + fire.gameObject.name + " fuel is null");
		}
		else if (fire.gameObject == null)
		{
			MelonLogger.Msg("[Fire][AddFuelNoSync] firesource is null");
		}
		fire.OnFuelBurnt(fuel);
		if (fire.IsEmbers())
		{
			fire.m_FX.TriggerStage(FireState.FullBurn, shouldPlay: true);
			fire.m_HeatSource.TurnOn();
		}
		float num = (float)((double)fuel.m_FuelSourceItem.GetModifiedBurnDurationHours(fuel.GetNormalizedCondition()) * 60.0 * 60.0);
		if (!fire.m_IsPerpetual)
		{
			fire.m_MaxOnTODSeconds += num;
			float num2 = fire.m_MaxOnTODSeconds - (float)((double)GameManager.GetFireManagerComponent().m_MaxDurationHoursOfFire * 60.0 * 60.0);
			if ((double)num2 > 0.0)
			{
				fire.m_ElapsedOnTODSeconds -= num2;
				fire.m_ElapsedOnTODSeconds = Mathf.Clamp(fire.m_ElapsedOnTODSeconds, 0f, float.PositiveInfinity);
			}
			fire.m_MaxOnTODSeconds = Mathf.Clamp(fire.m_MaxOnTODSeconds, 0f, (float)((double)GameManager.GetFireManagerComponent().m_MaxDurationHoursOfFire * 60.0 * 60.0));
		}
		fire.m_FuelHeatIncrease += Mathf.Min(Mathf.Clamp(GameManager.GetFireManagerComponent().m_MaxHeatIncreaseOfFire - fire.m_FuelHeatIncrease, 0f, float.PositiveInfinity), fuel.m_FuelSourceItem.m_HeatIncrease * fuel.GetNormalizedCondition());
		if (fire.m_ApplyToHeatSource)
		{
			fire.m_HeatSource.m_MaxTempIncrease = fire.m_FuelHeatIncrease;
			fire.m_HeatSource.m_MaxTempIncreaseInnerRadius = Mathf.Max(fuel.m_FuelSourceItem.m_HeatInnerRadius, fire.m_HeatSource.m_MaxTempIncreaseInnerRadius);
			fire.m_HeatSource.m_MaxTempIncreaseOuterRadius = Mathf.Max(fuel.m_FuelSourceItem.m_HeatOuterRadius * fire.GetFireOuterRadiusScale(), fire.m_HeatSource.m_MaxTempIncreaseOuterRadius);
		}
		fire.m_FX.TriggerFlareupLarge();
		fire.m_StartedByPlayer = true;
		if (fuel != null && (bool)fuel.gameObject)
		{
			UnityEngine.Object.Destroy(fuel.gameObject);
		}
	}

	public static void AddOtherFuel(DataStr.FireSourcesSync SyncData, string fuelName)
	{
		if (!ApplyOtherCampfires || FireManager.m_Fires.Count <= 0)
		{
			return;
		}
		for (int i = 0; i < FireManager.m_Fires.Count; i++)
		{
			Fire fire = FireManager.m_Fires[i];
			if (!(fire != null))
			{
				continue;
			}
			DataStr.FireSourcesSync fireSourcesSync = new DataStr.FireSourcesSync();
			fireSourcesSync.m_LevelId = levelid;
			fireSourcesSync.m_LevelGUID = level_guid;
			fireSourcesSync.m_Position = fire.gameObject.transform.position;
			fireSourcesSync.m_Rotation = fire.gameObject.transform.rotation;
			fireSourcesSync.m_Guid = "";
			if (fire.gameObject.GetComponent<ObjectGuid>() != null)
			{
				fireSourcesSync.m_Guid = fire.gameObject.GetComponent<ObjectGuid>().Get();
			}
			if (!IsSameFire(fireSourcesSync, SyncData))
			{
				continue;
			}
			if (fire.m_FireState != FireState.FullBurn)
			{
				break;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load(fuelName)).Cast<GameObject>();
			if ((bool)gameObject)
			{
				GearItem component = gameObject.GetComponent<GearItem>();
				if ((bool)component)
				{
					AddFuelNoSync(fire, component);
				}
			}
			break;
		}
	}

	public static void MayAddFireSources(DataStr.FireSourcesSync fire)
	{
		if (fire.m_LevelId != levelid || fire.m_LevelGUID != level_guid)
		{
			return;
		}
		fire.m_RemoveIn = 5;
		for (int i = 0; i < FireSources.Count; i++)
		{
			DataStr.FireSourcesSync fireSourcesSync = FireSources[i];
			bool flag = false;
			if (fireSourcesSync.m_LevelId == fire.m_LevelId && fireSourcesSync.m_LevelGUID == fire.m_LevelGUID)
			{
				if (fireSourcesSync.m_Guid != "")
				{
					if (fireSourcesSync.m_Guid == fire.m_Guid)
					{
						flag = true;
					}
				}
				else if (fireSourcesSync.m_Position == fire.m_Position)
				{
					flag = true;
				}
			}
			if (flag)
			{
				FireSources[i].m_Fuel = fire.m_Fuel;
				FireSources[i].m_CurrentHeat = fire.m_CurrentHeat;
				FireSources[i].m_MaxHeat = fire.m_MaxHeat;
				FireSources[i].m_HeatInnerRadius = fire.m_HeatInnerRadius;
				FireSources[i].m_HeatOuterRadius = fire.m_HeatOuterRadius;
				FireSources[i].m_HeatMasked = fire.m_HeatMasked;
				FireSources[i].m_RemoveIn = fire.m_RemoveIn;
				return;
			}
		}
		FireSources.Add(fire);
	}

	public static void SendMyFire(Fire fireTosend, string fuel = "")
	{
		if (ServerConfig.m_FireSync == 0 || (fuel != "" && ServerConfig.m_FireSync == 1))
		{
			return;
		}
		DataStr.FireSourcesSync fireSourcesSync = new DataStr.FireSourcesSync();
		FireHeatSync.Capture(fireTosend, fireSourcesSync);
		fireSourcesSync.m_Guid = "";
		if (fireTosend.gameObject.GetComponent<ObjectGuid>() != null)
		{
			fireSourcesSync.m_Guid = fireTosend.gameObject.GetComponent<ObjectGuid>().Get();
		}
		fireSourcesSync.m_LevelGUID = level_guid;
		fireSourcesSync.m_LevelId = levelid;
		fireSourcesSync.m_Position = fireTosend.gameObject.transform.position;
		fireSourcesSync.m_Rotation = fireTosend.gameObject.transform.rotation;
		if (fireTosend.m_Campfire == null)
		{
			fireSourcesSync.m_IsCampfire = false;
		}
		else
		{
			fireSourcesSync.m_IsCampfire = true;
			fireSourcesSync.m_Guid = "";
		}
		if (fuel == "")
		{
			if (sendMyPosition)
			{
				using Packet packet = new Packet(77);
				packet.Write(fireSourcesSync);
				SendUDPData(packet);
			}
			if (iAmHost)
			{
				ServerSend.FIRE(0, fireSourcesSync, toEveryOne: true);
			}
			return;
		}
		fireSourcesSync.m_FuelName = fuel;
		if (sendMyPosition)
		{
			using Packet packet2 = new Packet(84);
			packet2.Write(fireSourcesSync);
			SendUDPData(packet2);
		}
		if (iAmHost)
		{
			ServerSend.FIREFUEL(0, fireSourcesSync, toEveryOne: true);
		}
	}

	public static int AnyOneClose(float minimalDistance, Vector3 point)
	{
		if (Vector3.Distance(GameManager.GetPlayerTransform().position, point) <= minimalDistance)
		{
			return ClientUser.myId;
		}
		for (int i = 0; i < playersData.Count; i++)
		{
			if (playersData[i] != null && playersData[i].m_Levelid == levelid && playersData[i].m_LevelGuid == level_guid && Vector3.Distance(playersData[i].m_Position, point) <= minimalDistance)
			{
				return i;
			}
		}
		return -1;
	}

	public static float GetClosestDistanceFromEveryone(Vector3 point)
	{
		float num = float.PositiveInfinity;
		float num2 = Vector3.Distance(GameManager.GetPlayerTransform().position, point);
		if (num2 <= num)
		{
			num = num2;
		}
		for (int i = 0; i < playersData.Count; i++)
		{
			if (playersData[i] != null && playersData[i].m_Levelid == levelid && playersData[i].m_LevelGuid == level_guid)
			{
				float num3 = Vector3.Distance(playersData[i].m_Position, point);
				if (num3 < num)
				{
					num = num3;
				}
			}
		}
		return num;
	}

	public static int GetAnimalCount()
	{
		return BaseAiManager.m_BaseAis.Count + ActorsList.Count;
	}

	public static void EverySecond()
	{
		if (DsServerIsUp)
		{
			SecondsWithoutSaving++;
			if (RestartPerioud != -1)
			{
				SecondsWithoutRestart++;
			}
			if (SecondsWithoutSaving > DsSavePerioud)
			{
				SecondsWithoutSaving = 0;
				if (!CloseGameOnNextDSSave)
				{
					if (RestartPerioud != -1 && SecondsWithoutRestart >= RestartPerioud)
					{
						CloseGameOnNextDSSave = true;
						DataStr.MultiplayerChatMessage multiplayerChatMessage = new DataStr.MultiplayerChatMessage();
						multiplayerChatMessage.m_Type = 0;
						multiplayerChatMessage.m_By = "";
						multiplayerChatMessage.m_Message = "[SERVER] Server reboot in 30 seconds!";
						Shared.SendMessageToChat(multiplayerChatMessage);
						DataStr.MultiplayerChatMessage message = new DataStr.MultiplayerChatMessage
						{
							m_Type = 0,
							m_By = "",
							m_Message = "[SERVER] You can stay on server to being auto-reconnect, once server is up again. Or you can quit to process auto-save."
						};
						Shared.SendMessageToChat(multiplayerChatMessage);
						Shared.SendMessageToChat(message);
						DsSavePerioud = 30;
					}
					if (!NoDsSaves)
					{
						GameManager.ForceSaveGame();
						MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Server saved! Next save in " + DsSavePerioud + " seconds later!");
					}
				}
				else
				{
					MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Restarting...");
					ServerSend.RESTART();
					QuitWithoutSaving = true;
					Application.Quit();
				}
			}
		}
		if (sendMyPosition)
		{
			using Packet packet = new Packet(39);
			packet.Write(_value: true);
			SendUDPData(packet);
		}
		if (CurrentCustomChalleng.m_Started)
		{
			CustomChallengeUpdate();
			if (iAmHost)
			{
				if (CurrentCustomChalleng.m_Time > 0)
				{
					CurrentCustomChalleng.m_Time--;
				}
				ServerSend.CHALLENGEUPDATE(CurrentCustomChalleng);
			}
		}
		if (MyLobby != "")
		{
			SteamConnect.Main.OnRegularUpdate();
		}
		if (RegularUpdateSeconds > 0)
		{
			RegularUpdateSeconds--;
			if (RegularUpdateSeconds == 0)
			{
				RegularUpdateSeconds = 7;
				SendRegularAlignData();
			}
		}
		if (SendAfterLoadingFinished > 0)
		{
			SendAfterLoadingFinished--;
			if (SendAfterLoadingFinished == 0)
			{
				FinishedLoading();
			}
		}
		if (SecondsLeftUntilWorryAboutPacket != -1 && SecondsLeftUntilWorryAboutPacket > 0)
		{
			SecondsLeftUntilWorryAboutPacket--;
			if (SecondsLeftUntilWorryAboutPacket == 0)
			{
				RepeatLastRequest();
			}
		}
		if (TryMakeLobbyAgain > 0)
		{
			TryMakeLobbyAgain--;
			if (TryMakeLobbyAgain == 0 && MyLobby == "")
			{
				SteamConnect.Main.MakeLobby();
			}
		}
		if (!DedicatedServerAppMode)
		{
			PlayersUpdateManager();
			for (int i = 0; i < FireSources.Count; i++)
			{
				if (FireSources[i] == null)
				{
					continue;
				}
				if (FireSources[i].m_RemoveIn > 0)
				{
					FireSources[i].m_RemoveIn = FireSources[i].m_RemoveIn - 1;
					if (FireSources[i].m_LevelId == levelid && FireSources[i].m_LevelGUID == level_guid)
					{
						ApplyOtherFireSource(FireSources[i]);
					}
				}
				else
				{
					FireHeatSync.Expire(FireSources[i]);
					FireSources.RemoveAt(i--);
				}
			}
			if (FireManager.m_Fires.Count > 0)
			{
				for (int j = 0; j < FireManager.m_Fires.Count; j++)
				{
					Fire fire = FireManager.m_Fires[j];
					if (fire != null && fire.m_FireState == FireState.FullBurn && fire.m_StartedByPlayer)
					{
						SendMyFire(fire);
					}
				}
			}
		}
		if (Shared.IsMainMenuScene(level_name))
		{
			if (NeedConnectAfterLoad > 0)
			{
				NeedConnectAfterLoad--;
				if (NeedConnectAfterLoad == 0)
				{
					NeedConnectAfterLoad = -1;
					if (SteamConnect.CanUseSteam)
					{
						SteamConnect.Main.JoinToLobby(SteamLobbyToJoin);
					}
				}
			}
			if (NeedLoadSaveAfterLoad > 0)
			{
				NeedLoadSaveAfterLoad--;
				if (NeedLoadSaveAfterLoad == 0)
				{
					NeedLoadSaveAfterLoad = -1;
					ForceLoadSlotForPlaying(AutoStartSlot);
				}
			}
			if (StartDSAfterLoad > 0)
			{
				StartDSAfterLoad--;
				if (StartDSAfterLoad == 0)
				{
					StartDSAfterLoad = -1;
					StartDedicatedServer();
				}
			}
		}
		if (DsServerIsUp && DedicatedServerAppMode)
		{
			levelid = 0;
			level_guid = "null";
		}
		if (level_name != "Empty" && level_name != "Boot" && !Shared.IsMainMenuScene(level_name))
		{
			if (DedicatedServerAppMode)
			{
				if (!DsServerIsUp)
				{
					DsServerIsUp = true;
					if (SetP2PToLobbyForDSAfterLoad)
					{
						SetP2PToLobbyForDSAfterLoad = false;
						if (MyLobby != "")
						{
							FinishStartingDsServer();
						}
					}
					if (HostUDDSAfterLoad)
					{
						HostUDDSAfterLoad = false;
						Shared.HostAServer(PortForDSLoad);
						MelonLogger.Msg(System.ConsoleColor.Magenta, "[Dedicated server] Server is ready! Have fun!");
					}
				}
			}
			else
			{
				UpdateMyClothing();
				ApplyOpenables();
			}
			if (ApplyAutoThingsAfterLoaed == 0 && (NeedApplyAutoCMDs || AutoHostWhenLoaded))
			{
				if (NeedApplyAutoCMDs || AutoHostWhenLoaded)
				{
					MelonLogger.Msg(System.ConsoleColor.Magenta, "[Start-ups] Going to apply some parameters after load save");
				}
				ApplyAutoThingsAfterLoaed = 3;
			}
			if (ApplyAutoThingsAfterLoaed > 0)
			{
				ApplyAutoThingsAfterLoaed--;
				if (ApplyAutoThingsAfterLoaed == 0)
				{
					if (NeedApplyAutoCMDs)
					{
						NeedApplyAutoCMDs = false;
						if (AutoCMDs.Count > 0)
						{
							MelonLogger.Msg(System.ConsoleColor.Magenta, "[-cmd] Going to apply auto commands...");
							for (int k = 0; k < AutoCMDs.Count; k++)
							{
								MelonLogger.Msg(System.ConsoleColor.Magenta, "[-cmd] " + AutoCMDs[k]);
								uConsole.RunCommandSilent(AutoCMDs[k]);
							}
						}
					}
					if (AutoHostWhenLoaded)
					{
						AutoHostWhenLoaded = false;
						MelonLogger.Msg(System.ConsoleColor.Magenta, "[-host] Going to start server with using server.json");
						StartDedicatedServer(ds: false);
					}
				}
			}
			if (UpdateSnowshelters > 0)
			{
				UpdateSnowshelters--;
				if (UpdateSnowshelters == 0)
				{
					UpdateSnowshelters = -1;
					LoadAllSnowSheltersByOther();
				}
			}
			if (UpdateRopesAndFurns > 0)
			{
				UpdateRopesAndFurns--;
				ApplyOtherCampfires = true;
				if (UpdateRopesAndFurns == 0)
				{
					UpdateDeployedRopes();
				}
			}
			if (UpdateCampfires > 0)
			{
				UpdateCampfires--;
				if (UpdateCampfires == 0)
				{
					ApplyOtherCampfires = true;
				}
			}
			if (UpdateEverything > 0)
			{
				UpdateEverything--;
				if (UpdateEverything == 0)
				{
					UpdateEverything = -1;
					Pathes.LoadEverything();
				}
			}
			if (RemoveAttachedGears > 0)
			{
				RemoveAttachedGears--;
				if (RemoveAttachedGears == 0)
				{
					BakePreSpawnedGearsList();
					foreach (DataStr.PickedGearSync item in PickedGearsBackup)
					{
						Shared.AddPickedGear(item.m_Spawn, item.m_LevelID, item.m_LevelGUID, -1, item.m_MyInstanceID, item.m_GearName, needSync: false);
					}
					PickedGearsBackup.Clear();
				}
			}
			if (GameManager.m_Thirst != null)
			{
				if (GameManager.GetThirstComponent().m_LitersLeftToDrink > GameCompat.Liters(0f))
				{
					IsDrinking = true;
				}
				else
				{
					IsDrinking = false;
				}
				if (PreviousIsDrinking != IsDrinking)
				{
					PreviousIsDrinking = IsDrinking;
					if (IsDrinking)
					{
						MelonLogger.Msg("Drinking");
						SendConsume(IsDrink: true);
					}
					else
					{
						MelonLogger.Msg("Finished Drinking");
						SendStopConsume();
					}
				}
			}
			if (GameManager.m_Hunger != null)
			{
				Hunger hungerComponent = GameManager.GetHungerComponent();
				if (hungerComponent.m_CaloriesLeftToAdd > 0f)
				{
					IsEating = true;
				}
				else
				{
					IsEating = false;
				}
				if (PreviousIsEating != IsEating)
				{
					PreviousIsEating = IsEating;
					if (IsEating)
					{
						if (hungerComponent.m_FoodItemProvidingCalories != null)
						{
							MelonLogger.Msg("Eating " + hungerComponent.m_FoodItemProvidingCalories.m_GearItem.GetGearName());
							SendConsume(hungerComponent.m_FoodItemProvidingCalories.m_IsDrink);
						}
					}
					else
					{
						MelonLogger.Msg("Finished Eating");
						SendStopConsume();
					}
				}
			}
			if ((bool)GameManager.m_PlayerObject)
			{
				GearItem itemInHands = GameManager.GetPlayerManagerComponent().m_ItemInHands;
				if ((bool)itemInHands && (bool)itemInHands.m_HandheldShortwaveItem && CairnsSearchActive())
				{
					DoRadioSearch();
				}
			}
			if (GameCompat.Panel<Panel_HUD>() != null && !GameCompat.Panel<Panel_HUD>().IsShowingCollectibleNote() && ForcedShowExpeditionHUDSeconds > 0)
			{
				ForcedShowExpeditionHUDSeconds--;
			}
		}
		if (chatInput != null && !chatInput.gameObject.activeSelf && ChatObject != null && ChatObject.activeSelf)
		{
			HideChatTimer--;
			if (HideChatTimer <= 0 && ChatObject != null)
			{
				ChatObject.SetActive(value: false);
			}
		}
		if (!InOnline())
		{
			return;
		}
		int num = 0;
		if (GameManager.m_PlayerManager != null)
		{
			num = (int)PlayerManager.m_VoicePersona;
		}
		if (iAmHost)
		{
			ServerSend.SELECTEDCHARACTER(0, num, toEveryOne: true);
		}
		if (sendMyPosition)
		{
			if (LastResponceTime == 0f)
			{
				LastResponceTime = Time.time;
			}
			using Packet packet2 = new Packet(72);
			packet2.Write(num);
			SendUDPData(packet2);
		}
	}

	private static void EveryInGameMinute()
	{
		SanityManager.EveryInGameMinute();
		if (iAmHost && IsCycleSkiping && GameManager.m_PlayerManager != null && (!GameManager.GetPlayerManagerComponent().PlayerIsSleeping() || IsDead))
		{
			IsCycleSkiping = false;
		}
		if (TrackableDroppedGearsObjs.Count <= 0)
		{
			return;
		}
		foreach (System.Collections.Generic.KeyValuePair<int, GameObject> trackableDroppedGearsObj in TrackableDroppedGearsObjs)
		{
			if ((bool)trackableDroppedGearsObj.Value && (bool)trackableDroppedGearsObj.Value.GetComponent<Comps.DroppedGearDummy>() && trackableDroppedGearsObj.Value.GetComponent<Comps.DroppedGearDummy>().m_Extra.m_GoalTime - MinutesFromStartServer <= 0)
			{
				trackableDroppedGearsObj.Value.transform.GetChild(0).gameObject.SetActive(value: false);
			}
		}
	}

	public static bool InOnline()
	{
		if (iAmHost || sendMyPosition)
		{
			return true;
		}
		return false;
	}

	public static void WriteDownMesh(GameObject animal)
	{
	}

	public static string GetAnimalPrefabName(string _name)
	{
		return WildlifeCompat.PrefabNameFromInstance(_name);
	}

	public static void MakeAnimalActive(GameObject animal, bool active)
	{
		if (!(animal == null))
		{
			if (animal.transform.parent != null && animal.transform.parent.GetComponent<MoveAgent>() != null)
			{
				animal.transform.parent.GetComponent<MoveAgent>().enabled = active;
			}
			if (animal.transform.parent != null && animal.transform.parent.GetComponent<NavMeshAgent>() != null)
			{
				animal.transform.parent.GetComponent<NavMeshAgent>().enabled = active;
			}
			if (animal.GetComponent<AiTarget>() != null)
			{
				animal.GetComponent<AiTarget>().enabled = active;
			}
			if (animal.GetComponent<AiWolf>() != null)
			{
				animal.GetComponent<AiWolf>().enabled = active;
			}
			if (animal.GetComponent<AiStag>() != null)
			{
				animal.GetComponent<AiStag>().enabled = active;
			}
			if (animal.GetComponent<AiRabbit>() != null)
			{
				animal.GetComponent<AiRabbit>().enabled = active;
			}
			if (animal.GetComponent<AiMoose>() != null)
			{
				animal.GetComponent<AiMoose>().enabled = active;
			}
			if (animal.GetComponent<AiBear>() != null)
			{
				animal.GetComponent<AiBear>().enabled = active;
			}
			FarTerritory.SetAiEnabled(animal, active);
			WildlifeCompat.SetAdditionalAiEnabled(animal, active);
			if (animal.GetComponent<CharacterController>() != null)
			{
				animal.GetComponent<CharacterController>().enabled = active;
			}
			if (animal.GetComponent<Blackboard>() != null)
			{
				animal.GetComponent<Blackboard>().enabled = active;
			}
			if (animal.GetComponent<TLDBehaviourTreeOwner>() != null)
			{
				animal.GetComponent<TLDBehaviourTreeOwner>().enabled = active;
			}
			if (animal.GetComponent<BaseAi>() != null)
			{
				animal.GetComponent<BaseAi>().enabled = active;
			}
		}
	}

	public static GameObject SpawnAnimalActor(string prefabName, Vector3 v3, Quaternion rot, string GUID, string RegionGUID, System.Collections.Generic.List<DataStr.AnimalArrow> Arrows)
	{
		GameObject prefab = WildlifeCompat.GetPrefab(prefabName);
		if (prefab == null)
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Can't create animal actor with prefab name " + prefabName);
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, v3, rot);
		Comps.AnimalActor animalActor = gameObject.AddComponent<Comps.AnimalActor>();
		animalActor.m_Animal = gameObject;
		animalActor.m_Animator = RemoveAnimalComponents(gameObject);
		animalActor.m_ToGo = v3;
		animalActor.m_ToRotate = rot;
		animalActor.m_Arrows = Arrows;
		animalActor.ProcessArrows();
		Light[] array = gameObject.GetComponentsInChildren<Light>(includeInactive: true);
		if (array != null)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = false;
			}
		}
		if (gameObject.GetComponent<ObjectGuid>() == null)
		{
			gameObject.AddComponent<ObjectGuid>();
			gameObject.GetComponent<ObjectGuid>().Set(GUID);
		}
		else
		{
			gameObject.GetComponent<ObjectGuid>().Set(GUID);
		}
		if (!ActorsList.ContainsKey(GUID))
		{
			ActorsList.Add(GUID, animalActor);
		}
		return gameObject;
	}

	public static void SpawnAnimalCorpse(string prefab, Vector3 v3, Quaternion rot, string GUID, string SpawnRegionGUID)
	{
		MelonLogger.Msg("Going to spawn animal corpse " + GUID + " for Region " + SpawnRegionGUID);
		GameObject prefab2 = WildlifeCompat.GetPrefab(prefab);
		if (prefab2 == null)
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Can't create animal body prefab name " + prefab);
			return;
		}
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if ((bool)gameObject)
		{
			if (gameObject.GetComponent<Comps.AnimalCorpseObject>() != null)
			{
				MelonLogger.Msg("Corpse already exist, because been spawned myself");
				return;
			}
			MelonLogger.Msg("Replace original animal with corpse " + GUID);
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(prefab2, v3, rot);
		gameObject2.name = prefab2.name;
		gameObject2.layer = 19;
		if ((bool)gameObject2.GetComponent<BaseAi>())
		{
			gameObject2.GetComponent<BaseAi>().ChangeCollisionCapsulesLayer(19);
		}
		Animator animator = RemoveAnimalComponents(gameObject2);
		if (gameObject2.name.Contains("Rabbit") && (bool)gameObject2.GetComponent<BodyHarvest>())
		{
			UnityEngine.Object.Destroy(gameObject2.GetComponent<BodyHarvest>());
		}
		animator.SetBool(Animator.StringToHash("Dead"), value: true);
		animator.SetInteger(Animator.StringToHash("DamageSide"), 1);
		animator.SetTrigger(Animator.StringToHash("DamageImpact"));
		Comps.AnimalCorpseObject animalCorpseObject = gameObject2.AddComponent<Comps.AnimalCorpseObject>();
		animalCorpseObject.m_Animal = gameObject2;
		animalCorpseObject.m_RegionGUID = SpawnRegionGUID;
		if ((bool)gameObject2.GetComponent<BodyHarvest>())
		{
			gameObject2.GetComponent<BodyHarvest>().enabled = true;
		}
		if (gameObject != null)
		{
			ObjectGuid component = gameObject.GetComponent<ObjectGuid>();
			if (component != null)
			{
				component.Set(ObjectGuidManager.GenerateNewGuidString());
			}
			Comps.AnimalActor component2 = gameObject.GetComponent<Comps.AnimalActor>();
			if (component2 != null)
			{
				if (ActorsList.TryGetValue(GUID, out var value) && value == component2)
				{
					ActorsList.Remove(GUID);
				}
				component2.m_MarkToDestroy = true;
			}
			Comps.AnimalUpdates component3 = gameObject.GetComponent<Comps.AnimalUpdates>();
			if (component3 != null)
			{
				component3.m_MarkToDestroy = true;
			}
		}
		if (gameObject2.GetComponent<ObjectGuid>() == null)
		{
			gameObject2.AddComponent<ObjectGuid>();
			gameObject2.GetComponent<ObjectGuid>().Set(GUID);
		}
		else
		{
			gameObject2.GetComponent<ObjectGuid>().Set(GUID);
		}
		animalCorpseObject.Update();
		Light[] array = gameObject2.GetComponentsInChildren<Light>(includeInactive: true);
		if (array != null)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i].enabled = false;
			}
		}
		MatchTransform.EnableCollidersForAllActive(enable: true);
		if (!AnimalCorplesList.ContainsKey(GUID))
		{
			AnimalCorplesList.Add(GUID, animalCorpseObject);
		}
		MelonLogger.Msg("Animal corpse spawned " + GUID);
	}

	public static void ProcessAnimalCorpseSync(DataStr.AnimalKilled Sync)
	{
		if (Sync.m_LevelGUID == level_guid)
		{
			SpawnAnimalCorpse(Sync.m_PrefabName, Sync.m_Position, Sync.m_Rotation, Sync.m_GUID, Sync.m_RegionGUID);
		}
	}

	public static bool CheckDistanceAgnistEveryPlayer(Vector3 pos, float dis, bool lesser)
	{
		for (int i = 0; i < playersData.Count; i++)
		{
			if (playersData[i] == null || playersData[i].m_Levelid != levelid || !(playersData[i].m_LevelGuid == level_guid))
			{
				continue;
			}
			float num = Vector3.Distance(playersData[i].m_Position, pos);
			if (lesser)
			{
				if (num > dis)
				{
					return false;
				}
			}
			else if (num < dis)
			{
				return false;
			}
		}
		return true;
	}

	public static void SelectNameForHostSaveFile()
	{
		if (m_Panel_MainMenu != null)
		{
			m_Panel_MainMenu.ShowNameSaveSlotPopup();
			GameCompat.Panel<Panel_Confirmation>().m_CurrentGroup.m_InputField.SetText(System.DateTime.Now.ToString());
		}
	}

	public static void SelectBagesForConnection()
	{
		GameAudioManager.PlayGuiConfirm();
		if (m_Panel_MainMenu != null)
		{
			if (PendingSave.m_SaveSlotType == 3 && m_Panel_MainMenu.GetNumUnlockedFeats() > 0)
			{
				m_Panel_MainMenu.SelectWindow(m_Panel_MainMenu.m_SelectFeatWindow);
			}
			else if (!ShouldCreateSaveForHost)
			{
				ForcedCreateSave(PendingSave);
			}
			else
			{
				SelectNameForHostSaveFile();
			}
		}
	}

	public static void SelectGenderForConnection()
	{
		if (m_Panel_SelectSurvivor != null)
		{
			m_Panel_SelectSurvivor.Enable(enable: true);
			m_Panel_SelectSurvivor.m_BasicMenu.m_OnClickBackAction = null;
		}
	}

	public static void VerifiedSave(bool Valid)
	{
		if (Valid)
		{
			if (PendingSaveSlotInfo == null)
			{
				LetChooseSpawnForClient(PendingSave);
				return;
			}
			SaveSlotInfo pendingSaveSlotInfo = PendingSaveSlotInfo;
			SaveGameSlots.SetBaseNameForSave(pendingSaveSlotInfo.m_SaveSlotName, pendingSaveSlotInfo.m_SaveSlotName);
			MelonLogger.Msg("Save slot base name is " + SaveGameSlots.GetBaseNameForSave(pendingSaveSlotInfo.m_SaveSlotName));
			MelonLogger.Msg("Save slot name " + pendingSaveSlotInfo.m_SaveSlotName);
			MelonLogger.Msg("Save slot user defined name " + SaveGameSlots.GetUserDefinedSlotName(pendingSaveSlotInfo.m_SaveSlotName));
			SaveGameSystem.SetCurrentSaveInfo(pendingSaveSlotInfo.m_Episode, pendingSaveSlotInfo.m_GameMode, pendingSaveSlotInfo.m_GameId, pendingSaveSlotInfo.m_SaveSlotName);
			MelonLogger.Msg("Selecting slot " + SaveGameSystem.GetCurrentSaveName());
			GameManager.LoadSaveGameSlot(pendingSaveSlotInfo.m_SaveSlotName, pendingSaveSlotInfo.m_SaveChangelistVersion);
			PendingSaveSlotInfo = null;
		}
		else
		{
			if (PendingSaveSlotInfo != null)
			{
				SaveGameSlotHelper.DeleteSaveSlotInfo(PendingSaveSlotInfo);
				PendingSaveSlotInfo = null;
			}
			LetChooseSpawnForClient(PendingSave);
		}
	}

	public static void SendVerifySave(string UGUID, long Hash)
	{
		DoPleaseWait("Verifying...", " Host checking my save file...");
		using Packet packet = new Packet(137);
		packet.Write(UGUID);
		packet.Write(Hash);
		SendUDPData(packet);
	}

	public static void CheckHaveSaveFileToJoin(DataStr.SaveSlotSync Data)
	{
		AnimalsController = false;
		bool flag = false;
		int episode = Data.m_Episode;
		SaveSlotType saveSlotType = (SaveSlotType)Data.m_SaveSlotType;
		int seed = Data.m_Seed;
		SaveSlotInfo saveSlotInfo = null;
		Il2CppSystem.Collections.Generic.List<SaveSlotInfo> sortedSaveSlots = SaveGameSystem.GetSortedSaveSlots((Episode)episode, saveSlotType);
		for (int i = 0; i < sortedSaveSlots.Count; i++)
		{
			SaveSlotInfo saveSlotInfo2 = sortedSaveSlots[i];
			if (saveSlotInfo2.m_UserDefinedName == (seed.ToString() ?? ""))
			{
				int rNGGen = MenuChange.GetRNGGen(saveSlotInfo2.m_SaveSlotName);
				if (rNGGen == 5)
				{
					MelonLogger.Msg("Found slot to load");
					MelonLogger.Msg("Loading save file with seed: " + seed);
					flag = true;
					saveSlotInfo = saveSlotInfo2;
					break;
				}
				MelonLogger.Msg(System.ConsoleColor.DarkRed, "Found slot to load but it saved on outdated version of the game");
				MelonLogger.Msg(System.ConsoleColor.DarkRed, "Generation version of this slot " + rNGGen + ". Release of mod you using right now has Generation version " + 5);
			}
		}
		if (flag)
		{
			KillConsole();
			MelonLogger.Msg("Trying loading save slot...");
			long saveFingerprint = Shared.GetSaveFingerprint(SaveSlotCompat.LoadDataFromSlot(saveSlotInfo.m_SaveSlotName, "global"));
			MelonLogger.Msg("Save slot hash " + saveFingerprint);
			string text = SaveSlotCompat.LoadDataFromSlot(saveSlotInfo.m_SaveSlotName, "UserGUID");
			string text2 = "";
			if (text != null)
			{
				text2 = JSON.Load(text).Make<string[]>()[0];
			}
			MelonLogger.Msg("My UGUID " + text2);
			PendingSaveSlotInfo = saveSlotInfo;
			if (string.IsNullOrEmpty(text2))
			{
				SendVerifySave("", 0L);
			}
			else
			{
				SendVerifySave(text2, saveFingerprint);
			}
		}
		else
		{
			SendVerifySave("", 0L);
		}
	}

	public static void LetChooseSpawnForClient(DataStr.SaveSlotSync Data)
	{
		MelonLogger.Msg("Prepare ui for creating save slot");
		InterfaceManager.TrySetPanelEnabled<Panel_Sandbox>(enable: false);
		InterfaceManager.TrySetPanelEnabled<Panel_ChooseSandbox>(enable: false);
		InterfaceManager.TrySetPanelEnabled<Panel_ChooseSandbox>(enable: false);
		if (ServerConfig.m_PlayersSpawnType == 0)
		{
			SelectGenderForConnection();
		}
		else if (ServerConfig.m_PlayersSpawnType == 1)
		{
			InterfaceManager.TrySetPanelEnabled<Panel_SelectRegion_Map>(enable: true);
		}
		else if (ServerConfig.m_PlayersSpawnType == 2)
		{
			Data.m_Location = 6;
			SelectGenderForConnection();
		}
		else if (ServerConfig.m_PlayersSpawnType == 3)
		{
			SelectGenderForConnection();
		}
	}

	public static void ForcedCreateSave(DataStr.SaveSlotSync Data, string Name = "")
	{
		Episode episode = (Episode)Data.m_Episode;
		SaveSlotType saveSlotType = (SaveSlotType)Data.m_SaveSlotType;
		int seed = Data.m_Seed;
		int experienceMode = Data.m_ExperienceMode;
		Shared.GameRegion location = (Shared.GameRegion)Data.m_Location;
		KillConsole();
		MelonLogger.Msg("Creating save slot " + seed);
		ExperienceCompat.SetExperienceModeType((ExperienceModeType)experienceMode);
		if (experienceMode == 10)
		{
			MelonLogger.Msg("Custom Experience Mode Key is " + Data.m_CustomExperienceStr);
			if (GameManager.GetExperienceModeManagerComponent().SetCurrentCustomModeString(Data.m_CustomExperienceStr))
			{
				MelonLogger.Msg("Custom Experience Mode recreated from string!");
			}
			else
			{
				MelonLogger.Msg(System.ConsoleColor.Red, "Failed to recreate custom experience from string!");
			}
		}
		string displayName = ((seed == 0) ? Name : seed.ToString());
		uint unusedGameId = SaveGameSlots.GetUnusedGameId();
		SaveGameSystem.SetCurrentSaveInfo(episode, saveSlotType, unusedGameId, null);
		string currentSaveName = SaveGameSystem.GetCurrentSaveName();
		SlotData slotData = SaveGameSlots.GetSaveSlotFromName(currentSaveName);
		if (slotData == null)
		{
			slotData = SaveGameSlots.CreateSlot(currentSaveName, saveSlotType, unusedGameId, episode);
			if (slotData == null)
			{
				throw new System.InvalidOperationException("Could not create the native save slot " + currentSaveName);
			}
			SaveGameSlots.AddSlotData(slotData);
		}
		slotData.m_DisplayName = displayName;
		SaveGameSlots.SetSlotDisplayName(currentSaveName, displayName);
		MelonLogger.Msg("Save slot created!");
		MelonLogger.Msg("Save slot current name " + SaveGameSystem.GetCurrentSaveName());
		MelonLogger.Msg("Save slot userdefined name " + SaveGameSlots.GetUserDefinedSlotName(SaveGameSystem.GetCurrentSaveName()));
		RegionCompat.SetStartRegion(location);
		InterloperHook = true;
		OverridedSceneForSpawn = Data.m_FixedSpawnScene;
		OverridedPositionForSpawn = Data.m_FixedSpawnPosition;
		GameManager.Instance().LaunchSandbox();
		if (seed != 0)
		{
			GameManager.m_SceneTransitionData.m_GameRandomSeed = seed;
		}
		PendingSave = null;
		ShouldCreateSaveForHost = false;
	}

	// Remote shots used legacy 3D event names that no longer match 2.55 weapons; post the weapon's own fire
	// event on the remote player so it sounds like the local gun, positioned at the shooter.
	// The *_SHOOT_3D events the mod used are not in the 2.55 sound banks, and GunItem.m_FireAudio on the prefab
	// is not the shot either; the game's own shot events (Play_RifleFire / Play_RevolverFire) are positioned on the shooter.
	public static void PlayRemoteGunshot(string sound, int from)
	{
		PlayMultiplayer3dAduio(sound, from);
	}

	public static void PlayMultiplayer3dAduio(string sound, int from)
	{
		if (playersData.Count > 0 && playersData[from] != null && playersData[from].m_Levelid == levelid && players[from] != null && playersData[from].m_LevelGuid == level_guid)
		{
			GameAudioManager.Play3DSound(sound, players[from]);
		}
	}

	public static void SendMultiplayerAudio(string sound)
	{
		if (sendMyPosition)
		{
			using Packet packet = new Packet(30);
			packet.Write(sound);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			using (new Packet(30))
			{
				ServerSend.MULTISOUND(0, sound, toEveryOne: true);
			}
		}
	}

	public static bool ValidNickName(string name)
	{
		switch (name)
		{
		default:
			if (!Shared.HasNonASCIIChars(name))
			{
				return true;
			}
			goto case "";
		case "":
		case " ":
		case "Player":
			return false;
		}
	}

	public static void BeginRespawn()
	{
		MelonLogger.Msg("BeginRespawn");
		GameManager.GetConditionComponent().m_CurrentHP = GameManager.GetConditionComponent().GetAdjustedMaxHP();
		GameManager.GetFreezingComponent().m_CurrentFreezing = 0f;
		GameManager.GetHungerComponent().m_CurrentReserveCalories = GameManager.GetHungerComponent().m_MaxReserveCalories;
		GameManager.GetThirstComponent().m_CurrentThirst = 0f;
		GameManager.GetFatigueComponent().m_CurrentFatigue = 0f;
		BooksResearched = new System.Collections.Generic.Dictionary<string, float>();
		if ((bool)GameManager.m_PlayerManager)
		{
			GameManager.m_PlayerManager.SetControlMode(PlayerControlMode.Normal);
		}
		else
		{
			MelonLogger.Msg("m_PlayerManager is null");
		}
		GameCompat.Panel<Panel_Log>().ExitInterface();
		GameManager.GetInventoryComponent().DestroyAllGear();
		GameManager.GetPlayerManagerComponent().m_StartGear.AddAllToInventory();
		GameManager.GetSkillArchery().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillRifle().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillFireStarting().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillCarcassHarvesting().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillCooking().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillIceFishing().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillClothingRepair().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillRevolver().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GameManager.GetSkillGunsmithing().SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		GetCustomSkill(CustomSkills.FirstAid).SetPoints(0, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
		ConsoleManager.CONSOLE_afflictions_cure();
		GameManager.GetVpFPSCamera().enabled = true;
		if (!GameManager.IsOutDoorsScene(GameManager.m_ActiveScene))
		{
			string lastOutdoorScene = GameManager.m_SceneTransitionData.m_LastOutdoorScene;
			LoadScene loadScene = new GameObject().AddComponent<LoadScene>();
			loadScene.m_SceneToLoad = lastOutdoorScene;
			loadScene.Activate();
			return;
		}
		GameObject gameObject = PlayerManager.PickRandomSpawnPoint();
		if ((bool)gameObject)
		{
			GameManager.GetPlayerManagerComponent().TeleportPlayer(gameObject.transform.position, gameObject.transform.rotation);
		}
		SaveGameSystem.SetAsyncEnabled(enabled: false);
		PendingRespawn = true;
		GameManager.ForceSaveGame();
	}

	public static void FindPlayerToTrade()
	{
		if (GameManager.m_PlayerObject == null)
		{
			GiveItemTo = -1;
			return;
		}
		Vector3 position = GameManager.GetPlayerTransform().position;
		float num = float.PositiveInfinity;
		int giveItemTo = -1;
		for (int i = 0; i < playersData.Count; i++)
		{
			if (playersData[i] != null && playersData[i].m_Levelid == levelid && playersData[i].m_LevelGuid == level_guid && Vector3.Distance(position, playersData[i].m_Position) < 15f && i != ClientUser.myId)
			{
				float num2 = Vector3.Distance(position, playersData[i].m_Position);
				if (num2 < num)
				{
					giveItemTo = i;
					num = num2;
				}
			}
		}
		GiveItemTo = giveItemTo;
	}

	public static void AddPersonToLobby(string name, ulong SteamID, Texture2D Avatar)
	{
		if (!DedicatedServerAppMode && !LobbyElements.ContainsKey(SteamID))
		{
			GameObject prefab = BundleAsset<GameObject>("MP_PlayerLobby");
			if (!(LobbyUI == null))
			{
				GameObject gameObject = SpawnModAsset(prefab, LobbyUI.transform.GetChild(0).GetChild(0));
				Sprite overrideSprite = Sprite.Create(Avatar, new Rect(0f, 0f, 64f, -64f), new Vector2(0f, 0f));
				gameObject.transform.GetChild(1).gameObject.GetComponent<Image>().overrideSprite = overrideSprite;
				LobbyElements.Add(SteamID, gameObject);
				gameObject.AddComponent<Comps.LobbyHoverNickname>();
				gameObject.GetComponent<Comps.LobbyHoverNickname>().m_Btn = gameObject.transform.GetChild(2).gameObject.GetComponent<Button>();
			}
		}
	}

	public static bool LobbyContains(ulong SteamID)
	{
		return LobbyElements.ContainsKey(SteamID);
	}

	public static void RemovePersonFromLobby(ulong SteamID)
	{
		if (!DedicatedServerAppMode && LobbyElements.TryGetValue(SteamID, out var value))
		{
			LobbyElements.Remove(SteamID);
			UnityEngine.Object.Destroy(value);
		}
	}

	public static void SetPersonNameFromLobby(ulong SteamID, string name)
	{
		if (!DedicatedServerAppMode && LobbyElements.TryGetValue(SteamID, out var value))
		{
			value.GetComponent<Comps.LobbyHoverNickname>().m_Name = name;
		}
	}

	public static void UpdatePlayerStatusMenu(System.Collections.Generic.List<DataStr.MultiPlayerClientStatus> MPs)
	{
		if (!(StatusPanel != null) || MPs.Count <= 0)
		{
			return;
		}
		for (int i = 0; i < MaxPlayers; i++)
		{
			if (StatusTexes.Count < MaxPlayers)
			{
				GameObject gameObject = SpawnModAsset(BundleAsset<GameObject>("MP_PlayerText"), StatusPanel.transform);
				gameObject.GetComponent<Text>().text = i + ".";
				StatusTexes.Add(gameObject);
			}
		}
		for (int j = 0; j < StatusTexes.Count; j++)
		{
			StatusTexes[j].SetActive(value: false);
		}
		for (int k = 0; k < MPs.Count; k++)
		{
			if (StatusTexes[k] != null)
			{
				Text component = StatusTexes[k].GetComponent<Text>();
				component.text = MPs[k].m_ID + ". " + MPs[k].m_Name;
				if (MPs[k].m_IsLoading)
				{
					component.text += "(Loading)";
				}
				StatusTexes[k].SetActive(value: true);
			}
		}
	}

	public static void SendMyEQ()
	{
		DataStr.PlayerEquipmentData playerEquipmentData = new DataStr.PlayerEquipmentData();
		playerEquipmentData.m_HasAxe = MyHasAxe;
		playerEquipmentData.m_HasMedkit = MyHasMedkit;
		playerEquipmentData.m_HasRevolver = MyHasRevolver;
		playerEquipmentData.m_HasRifle = MyHasRifle;
		playerEquipmentData.m_Arrows = MyArrows;
		playerEquipmentData.m_Flares = MyFlares;
		playerEquipmentData.m_BlueFlares = MyBlueFlares;
		if (sendMyPosition)
		{
			using Packet packet = new Packet(41);
			packet.Write(playerEquipmentData);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			using (new Packet(41))
			{
				ServerSend.EQUIPMENT(0, playerEquipmentData, toEveryOne: true);
			}
		}
	}

	public static void DoWaitForConnect(bool Steam = false)
	{
		if (!HasWaitForConnect && m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			if (m_Panel_MainMenu != null && m_Panel_MainMenu != null && m_Panel_MainMenu.m_Sprite_FadeOverlay != null)
			{
				m_Panel_MainMenu.m_Sprite_FadeOverlay.gameObject.SetActive(value: false);
			}
			string messageLocID = (Steam ? "\nPlease wait a moment..." : ((!DefaultIsRussian) ? "\nPlease wait, if you using hamachi/radmin or any other lan emulator and you have connection problems, please don't ask us for help." : "\nЕсли вы используйте hamachi/radmin или другой эмулятор локалки, и у вас проблемы с подключением, не простите о помощи нас."));
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Waiting, "Connecting...", messageLocID, Panel_Confirmation.ButtonLayout.Button_0, Panel_Confirmation.Background.Transperent, null, null);
			HasWaitForConnect = true;
		}
	}

	public static void SetRepeatPacket(ResendPacketType Pak, int TimeOut = -1)
	{
		LastPacketForRepeat = Pak;
		if (TimeOut == -1)
		{
			TimeOut = TimeToWorryAboutLastRequest;
		}
		SecondsLeftUntilWorryAboutPacket = TimeOut;
	}

	public static void DiscardRepeatPacket()
	{
		SecondsLeftUntilWorryAboutPacket = -1;
		LastPacketForRepeat = ResendPacketType.None;
		ResendGearLvlKey = "";
		ResendGearSearchKey = 0;
		ResendAttempts = 0;
	}

	public static void SimulateAnotherRequest(ResendPacketType request)
	{
		switch (request)
		{
		case ResendPacketType.Scene:
			Pathes.RequestDropsForScene();
			break;
		case ResendPacketType.Examine:
			DiscardRepeatPacket();
			RemovePleaseWait();
			HUDMessage.AddMessage("Diagnosis failed!");
			break;
		case ResendPacketType.GearPlace:
		{
			using Packet packet4 = new Packet(91);
			packet4.Write(ResendGearSearchKey);
			packet4.Write(ResendGearLvlKey);
			SetRepeatPacket(ResendPacketType.GearPlace);
			SendUDPData(packet4);
			break;
		}
		case ResendPacketType.GearPickup:
		{
			using Packet packet3 = new Packet(88);
			packet3.Write(ResendGearSearchKey);
			packet3.Write(ResendGearLvlKey);
			SetRepeatPacket(ResendPacketType.GearPickup);
			SendUDPData(packet3);
			break;
		}
		case ResendPacketType.ServerRestart:
		{
			using Packet packet2 = new Packet(148);
			SetRepeatPacket(ResendPacketType.ServerRestart);
			SendUDPData(packet2);
			break;
		}
		case ResendPacketType.ExpeditionInvites:
		{
			using Packet packet = new Packet(165);
			SetRepeatPacket(ResendPacketType.ExpeditionInvites);
			packet.Write(_value: true);
			SendUDPData(packet);
			break;
		}
		case ResendPacketType.Cancel:
			DiscardRepeatPacket();
			RemovePleaseWait();
			HUDMessage.AddMessage("Request failed!");
			break;
		case ResendPacketType.Container:
			if (GoingToOpenContinaer != null)
			{
				CoopContainerFlow.RetryPending();
				break;
			}
			DiscardRepeatPacket();
			RemovePleaseWait();
			HUDMessage.AddMessage("Request failed!");
			break;
		default:
			MelonLogger.Msg("Can't simulate request " + request);
			DiscardRepeatPacket();
			DoPleaseWait("Request failed", "Something wrong with #" + request.ToString() + " request, please reconnect to the server!");
			break;
		}
	}

	public static void RepeatLastRequest()
	{
		ResendAttempts++;
		RemovePleaseWait();
		if (ResendAttempts >= 11)
		{
			DiscardRepeatPacket();
			if (!SaveExitPending)
			{
				QuitWithoutSaving = false;
			}
			if (m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
			{
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "DISCONNECT TIMEOUT", "\nHost did not respond after 10 attempts.\nYour current progress will be saved before the game exits.", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
			}
		}
		else if (LastPacketForRepeat != ResendPacketType.None)
		{
			SlicedJsonDataBuffer.Clear();
			SecondsLeftUntilWorryAboutPacket = TimeToWorryAboutLastRequest;
			string title;
			string text;
			if (LastPacketForRepeat != ResendPacketType.ServerRestart)
			{
				title = "Something wrong";
				text = "Timed out on response from host for last request. Trying to repeat request to correct the situation, please wait...\nAttempt #" + ResendAttempts;
			}
			else
			{
				title = "Waiting for server restart";
				text = "Please wait...\nPing Attempt #" + ResendAttempts;
			}
			DoPleaseWait(title, text);
			SimulateAnotherRequest(LastPacketForRepeat);
		}
	}

	public static void DoPleaseWait(string title, string text)
	{
		if (m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Waiting, title, "\n" + text, Panel_Confirmation.ButtonLayout.Button_0, Panel_Confirmation.Background.Transperent, null, null);
		}
	}

	public static void RemovePleaseWait()
	{
		if (m_InterfaceManager != null && m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			GameCompat.Panel<Panel_Confirmation>().OnCancel();
		}
	}

	public static void RemoveWaitForConnect()
	{
		HasWaitForConnect = false;
		if (m_InterfaceManager != null && m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			GameCompat.Panel<Panel_Confirmation>().OnCancel();
		}
	}

	public static void DoKickMessage(string txt, bool integrityFailure = false)
	{
		RemoveWaitForConnect();
		if (m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "DISCONNECTED", "\n" + txt, Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
		}
		bool flag = integrityFailure && ServerConfig.m_SaveScamProtection;
		if (!Shared.IsMainMenuScene(level_name) && flag && !SaveExitPending)
		{
			QuitWithoutSaving = true;
			GameManager.CancelPendingSave();
		}
		else if (!SaveExitPending)
		{
			QuitWithoutSaving = false;
		}
		MelonLogger.Msg("Kicked by server, message from host: " + txt + "; integrityDiscard=" + flag);
		ClientUser.LastConnectedIp = "";
		Disconnect();
	}

	public static void DoOKMessage(string title, string txt)
	{
		if (m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, title, "\n" + txt, Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
		}
	}

	public static void CancleDismantling()
	{
		DataStr.ShowShelterByOther showShelterByOther = new DataStr.ShowShelterByOther();
		Panel_SnowShelterInteract panel_SnowShelterInteract = GameCompat.Panel<Panel_SnowShelterInteract>();
		if (!(panel_SnowShelterInteract.m_SnowShelter != null) || !(panel_SnowShelterInteract.m_SnowShelter.gameObject != null))
		{
			return;
		}
		showShelterByOther.m_Position = panel_SnowShelterInteract.m_SnowShelter.gameObject.transform.position;
		showShelterByOther.m_Rotation = panel_SnowShelterInteract.m_SnowShelter.gameObject.transform.rotation;
		showShelterByOther.m_LevelID = levelid;
		showShelterByOther.m_LevelGUID = level_guid;
		if (!panel_SnowShelterInteract.m_IsDismantling)
		{
			return;
		}
		for (int i = 0; i < playersData.Count; i++)
		{
			if (playersData[i] != null)
			{
				DataStr.ShowShelterByOther shelter = playersData[i].m_Shelter;
				if (level_guid == shelter.m_LevelGUID && levelid == shelter.m_LevelID && showShelterByOther.m_Position == shelter.m_Position)
				{
					panel_SnowShelterInteract.OnCancel();
					HUDMessage.AddMessage(playersData[i].m_Name + " INSIDE, CAN'T DISMANTLE THIS!");
					break;
				}
			}
		}
	}

	public static void PlayVoiceFromPlayerObject(AudioSource audioSource, byte[] voiceData, bool RadioEffect = false)
	{
		if (audioSource != null)
		{
			audioSource.clip = AudioClip.Create(UnityEngine.Random.Range(100, 1000000).ToString(), 22050, 1, 22050, stream: false);
			float[] array = new float[22050];
			for (int i = 0; i < array.Length; i++)
			{
				float num = System.BitConverter.ToInt16(voiceData, i * 2);
				array[i] = num / 32767f;
			}
			audioSource.clip.SetData(array, 0);
			audioSource.Play();
		}
	}

	public static void ProcessRadioChatData(byte[] CompressedData, uint WriteSize, float RecordTime)
	{
		byte[] array = SteamConnect.Main.DecompressVoice(CompressedData, WriteSize);
		if (array != null && MyRadioAudio != null)
		{
			Comps.MultiplayerPlayerVoiceChatPlayer component = MyRadioAudio.transform.GetChild(1).gameObject.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>();
			DataStr.VoiceChatQueueElement item = new DataStr.VoiceChatQueueElement
			{
				m_VoiceData = array,
				m_Length = RecordTime
			};
			component.VoiceQueue.Add(item);
		}
	}

	public static void ProcessVoiceChatData(int from, byte[] CompressedData, uint WriteSize, float RecordTime, bool Radio = false)
	{
		byte[] array = SteamConnect.Main.DecompressVoice(CompressedData, WriteSize);
		if (array != null && !DedicatedServerAppMode && players[from] != null && playersData[from].m_Levelid == levelid && playersData[from].m_LevelGuid == level_guid)
		{
			Comps.MultiplayerPlayerVoiceChatPlayer multiplayerPlayerVoiceChatPlayer = (Radio ? players[from].transform.GetChild(4).GetChild(2).gameObject.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>() : players[from].transform.GetChild(4).GetChild(1).gameObject.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>());
			if ((bool)multiplayerPlayerVoiceChatPlayer)
			{
				DataStr.VoiceChatQueueElement voiceChatQueueElement = new DataStr.VoiceChatQueueElement();
				voiceChatQueueElement.m_VoiceData = array;
				voiceChatQueueElement.m_Length = RecordTime;
				multiplayerPlayerVoiceChatPlayer.VoiceQueue.Add(voiceChatQueueElement);
			}
		}
	}

	public static byte[] Compress(byte[] data)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Compress);
		gZipStream.Write(data, 0, data.Length);
		gZipStream.Close();
		return memoryStream.ToArray();
	}

	public static byte[] Decompress(byte[] data)
	{
		using MemoryStream stream = new MemoryStream(data);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream();
		gZipStream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	public static void PlayRadioOver()
	{
		if (!(MyRadioAudio == null))
		{
			AudioSource component = MyRadioAudio.transform.GetChild(0).gameObject.GetComponent<AudioSource>();
			AudioClip clip = BundleAsset<AudioClip>("RadioOver");
			if (!component.isPlaying)
			{
				component.PlayOneShot(clip);
			}
		}
	}

	public static void TrackWhenRecordOver(bool Radio)
	{
		if (GameManager.m_PlayerObject == null)
		{
			return;
		}
		bool doingRecord = DoingRecord;
		DoingRecord = KeyboardUtilities.InputManager.GetKey(KeyCode.V);
		if ((chatInput != null && chatInput.gameObject.activeSelf) || (uConsole.m_Instance != null && uConsole.m_On))
		{
			DoingRecord = false;
		}
		if (doingRecord != DoingRecord)
		{
			if (!DoingRecord)
			{
				RecordReleseButtonHold = Time.time + 0.5f;
				if ((bool)ViewModelRadio && !ViewModelRadio.activeSelf)
				{
				}
			}
			else
			{
				RecordStartTime = Time.time;
			}
		}
		SteamConnect.Main.VoiceUpdate();
		if (MicrophoneIdicator != null)
		{
			Image component = MicrophoneIdicator.GetComponent<Image>();
			float a = 1f;
			if (!DoingRecord)
			{
				a = Mathf.Lerp(component.color.a, 0f, 5f * Time.deltaTime);
			}
			else if (Radio)
			{
				a = 0f;
			}
			component.color = new Color(component.color.r, component.color.g, component.color.b, a);
		}
		if (RadioIdicator != null)
		{
			Image component2 = RadioIdicator.GetComponent<Image>();
			float a2 = 1f;
			if (!DoingRecord)
			{
				a2 = Mathf.Lerp(component2.color.a, 0f, 5f * Time.deltaTime);
			}
			else if (!Radio)
			{
				a2 = 0f;
			}
			component2.color = new Color(component2.color.r, component2.color.g, component2.color.b, a2);
			Text component3 = RadioIdicator.transform.GetChild(0).gameObject.GetComponent<Text>();
			component3.color = new Color(component3.color.r, component3.color.g, component3.color.b, a2);
			component3.text = GetRadioFrequency(RadioFrequency);
		}
	}

	public static void SendMyRadioAudio(byte[] CompressedArray, int ArraySize, float RecordTime, int Sender)
	{
		if (sendMyPosition && ConnectedSteamWorks)
		{
			using (Packet packet = new Packet(81))
			{
				packet.Write(ArraySize);
				packet.Write(CompressedArray.Length);
				packet.Write(CompressedArray);
				packet.Write(RecordTime);
				packet.Write(Sender);
				SendUDPData(packet);
			}
		}
	}

	public static void SendMyVoice(byte[] CompressedArray, int ArraySize, float RecordTime)
	{
		if (iAmHost && Server.UsingSteamWorks)
		{
			ServerSend.VOICECHAT(0, CompressedArray, ArraySize, RecordTime, level_guid, RadioFrequency, 0);
		}
		if (sendMyPosition && ConnectedSteamWorks)
		{
			using (Packet packet = new Packet(81))
			{
				packet.Write(ArraySize);
				packet.Write(CompressedArray.Length);
				packet.Write(CompressedArray);
				packet.Write(RecordTime);
				packet.Write(ClientUser.myId);
				SendUDPData(packet);
			}
		}
	}

	public static void FinishedLoading()
	{
		RegularUpdateSeconds = 2;
		if (HostFromLobbyAfterLoader)
		{
			HostFromLobbyAfterLoader = false;
			Server.StartSteam(MaxPlayers);
		}
		if (StartServerAfterSelectSave)
		{
			StartServerAfterSelectSave = false;
			Shared.HostAServer(PortsToHostOn);
		}
	}

	public static void DedicatedServerUpdate()
	{
		if (GameCompat.Panel<Panel_Loading>() != null)
		{
			Panel_Loading panel_Loading = GameCompat.Panel<Panel_Loading>();
			panel_Loading.m_HoldScreenAfterLoad = false;
			panel_Loading.m_ContinueToGame = true;
			panel_Loading.m_ShowQuoteAfterLoad = false;
		}
		if (GameManager.m_PlayerManager != null)
		{
			GameManager.m_PlayerManager.m_God = true;
			GameManager.m_PlayerManager.m_Ghost = true;
			GameManager.m_PlayerObject.transform.position = new Vector3(0f, -300f, 0f);
			IsDead = true;
			GameAudioManager.SetRTPCValue(GAME_PARAMETERS.GLOBALVOLUME, 0f, null);
		}
	}

	public static string CloneTrimer(string name)
	{
		string result = name;
		if (name.Contains("(Clone)"))
		{
			int startIndex = name.Length - 7;
			result = name.Remove(startIndex, 7);
		}
		if (name.Contains(" "))
		{
			char separator = System.Convert.ToChar(" ");
			result = name.Split(separator)[0];
		}
		return result;
	}

	public static bool SnareCanTrap(Vector3 pos, string prefabname)
	{
		SpawnRegionManager spawnRegionManager = GameManager.GetSpawnRegionManager();
		for (int i = 0; i < spawnRegionManager.m_SpawnRegions.Count; i++)
		{
			float num = Vector3.Distance(pos, spawnRegionManager.m_SpawnRegions[i].m_Center);
			float radius = spawnRegionManager.m_SpawnRegions[i].m_Radius;
			string spawnablePrefabName = spawnRegionManager.m_SpawnRegions[i].GetSpawnablePrefabName();
			if (num <= radius && prefabname == spawnablePrefabName)
			{
				return true;
			}
		}
		return false;
	}

	public static void FakeDropItem(int GearID, Vector3 v3, Quaternion rot, int Hash, DataStr.ExtraDataForDroppedGear extra)
	{
		bool flag = false;
		if (extra.m_Variant == -2)
		{
			flag = true;
		}
		if (DedicatedServerAppMode)
		{
			return;
		}
		string gearName = extra.m_GearName;
		if (!flag && DroppedGearsObjs.ContainsKey(Hash))
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Gear with hash " + Hash + " already exist!");
			return;
		}
		GameObject gearItemObject = GetGearItemObject(gearName);
		if (gearItemObject == null)
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + gearName + " not exist! Maybe you miss an modded item pack?");
			return;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, v3, rot);
		if (gameObject != null)
		{
			DisableObjectForXPMode.RemoveDisabler(gameObject);
			string localizedDisplayName = "";
			bool flag2 = false;
			bool flag3 = false;
			ScentRangeCategory scentCategory = ScentRangeCategory.RAW_FISH;
			GearItem component = gameObject.GetComponent<GearItem>();
			if (component != null)
			{
				component.m_RolledSpawnChance = true;
				component.m_StartHasBeenCalled = true;
				component.m_MarkedForNextUpdateDestroy = false;
				if (component.m_FoodItem != null && extra.m_Variant == 1 && gameObject.GetComponent<MeshSwapItem>() != null)
				{
					gameObject.GetComponent<MeshSwapItem>().m_MeshObjOpened.SetActive(value: true);
					gameObject.GetComponent<MeshSwapItem>().m_MeshObjUnopened.SetActive(value: false);
				}
				localizedDisplayName = (flag ? Localization.Get(extra.m_ExpeditionNote) : component.DisplayName);
			}
			if (gameObject.GetComponent<Bed>() != null && extra.m_Variant == 1)
			{
				gameObject.GetComponent<Bed>().SetState(BedRollState.Placed);
				flag2 = true;
			}
			if (gameObject.GetComponent<Scent>() != null)
			{
				flag3 = true;
				scentCategory = gameObject.GetComponent<Scent>().m_ScentCategory;
			}
			if (gameObject.GetComponent<KeroseneLampItem>() != null && extra.m_Variant == 1)
			{
				MelonLogger.Msg("Ignited lamp dropped");
				KeroseneLampItem component2 = gameObject.GetComponent<KeroseneLampItem>();
				if (GameManager.GetWeatherComponent().UseOutdoorLightingForLightSources())
				{
					component2.m_LightOutdoor.enabled = true;
					component2.m_LightIndoor.enabled = false;
					component2.m_LightIndoorCore.enabled = false;
				}
				else
				{
					component2.m_LightIndoor.enabled = true;
					component2.m_LightIndoorCore.enabled = true;
					component2.m_LightOutdoor.enabled = false;
				}
				if (extra.m_GoalTime - MinutesFromStartServer <= 0)
				{
					gameObject.transform.GetChild(0).gameObject.SetActive(value: false);
				}
				else
				{
					gameObject.transform.GetChild(0).gameObject.SetActive(value: true);
				}
				TrackableDroppedGearsObjs.Add(Hash, gameObject);
			}
			if (gameObject.GetComponent<SnareItem>() != null)
			{
				int num = 0;
				num = ((extra.m_Variant == 4) ? 1 : extra.m_Variant);
				gameObject.GetComponent<SnareItem>().SetState((SnareState)num);
			}
			ThrownFlareSync.PrepareReplica(gameObject, extra);
			foreach (Component component3 in gameObject.GetComponents<Component>())
			{
				string name = component3.GetIl2CppType().Name;
				if (name != Il2CppType.Of<BoxCollider>().Name && name != Il2CppType.Of<SphereCollider>().Name && name != Il2CppType.Of<CapsuleCollider>().Name && name != Il2CppType.Of<MeshCollider>().Name && name != Il2CppType.Of<MeshFilter>().Name && name != Il2CppType.Of<LODGroup>().Name && name != Il2CppType.Of<Transform>().Name && name != Il2CppType.Of<Rigidbody>().Name && name != Il2CppType.Of<MeshRenderer>().Name && name != Il2CppType.Of<SkinnedMeshRenderer>().Name)
				{
					UnityEngine.Object.Destroy(component3);
				}
			}
			if (!flag)
			{
				if (!string.IsNullOrEmpty(extra.m_PhotoGUID) && extra.m_GearName != "gear_scmapreveal")
				{
					MelonLogger.Msg("Photo " + extra.m_PhotoGUID);
					Texture2D photoTexture = MPSaveManager.GetPhotoTexture(extra.m_PhotoGUID, extra.m_GearName);
					if ((bool)photoTexture)
					{
						gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = photoTexture;
					}
					else
					{
						Shared.RequestPhoto(extra.m_PhotoGUID);
						MelonLogger.Msg("Don't have texture for it, request from host");
					}
				}
				Comps.DroppedGearDummy droppedGearDummy = gameObject.AddComponent<Comps.DroppedGearDummy>();
				droppedGearDummy.m_SearchKey = Hash;
				droppedGearDummy.m_Extra = extra;
				droppedGearDummy.m_LocalizedDisplayName = localizedDisplayName;
				if (flag2)
				{
					gameObject.AddComponent<Comps.FakeBed>();
				}
				if (flag3)
				{
					GameObject gearItemObject2 = GetGearItemObject("GEAR_SCDecoy");
					if ((bool)gearItemObject2)
					{
						GameObject gameObject2 = UnityEngine.Object.Instantiate(gearItemObject2, v3, rot);
						if ((bool)gameObject2)
						{
							droppedGearDummy.m_DecoyDummy = gameObject2.GetComponent<GearItem>();
							if (droppedGearDummy.m_DecoyDummy.m_Scent == null)
							{
								droppedGearDummy.m_DecoyDummy.m_Scent = gameObject2.AddComponent<Scent>();
								gameObject2.GetComponent<BoxCollider>().enabled = false;
							}
							droppedGearDummy.m_DecoyDummy.m_Scent.m_ScentCategory = scentCategory;
							gameObject2.AddComponent<Comps.FakeDecoy>().m_DropGearDummy = droppedGearDummy;
							GearManager.MaybeAddToDroppedDecoys(droppedGearDummy.m_DecoyDummy);
							MelonLogger.Msg(System.ConsoleColor.Cyan, "Gear prefab with name " + gearName + " registered as decoy");
						}
					}
				}
				gameObject.SetActive(value: true);
				DroppedGearsObjs.Add(Hash, gameObject);
			}
			else
			{
				Comps.SpecialItemDummy specialItemDummy = gameObject.AddComponent<Comps.SpecialItemDummy>();
				specialItemDummy.m_Extra = extra;
				specialItemDummy.m_LocalizedDisplayName = localizedDisplayName;
				gameObject.SetActive(value: true);
			}
		}
		else
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + gearName + " not exist! Maybe you miss an modded item pack?");
		}
	}

	public static void PlaceDroppedGear(GameObject obj)
	{
		string name = obj.name;
		string text = CloneTrimer(name).ToLower();
		string text2 = CloneTrimer(name);
		Vector3 position = obj.transform.position;
		Quaternion rotation = obj.transform.rotation;
		int num = 0;
		string text3 = level_guid;
		LastPickedVisualPosition = obj.transform.position;
		LastPickedVisualRotation = obj.transform.rotation;
		if (obj.GetComponent<Comps.DroppedGearDummy>() != null)
		{
			num = obj.GetComponent<Comps.DroppedGearDummy>().m_SearchKey;
			MelonLogger.Msg("Searching for " + text + " with hash " + num);
			if (sendMyPosition)
			{
				DoPleaseWait("Downloading Gear", "Please wait..");
				using Packet packet = new Packet(91);
				packet.Write(num);
				packet.Write(text3);
				ResendGearSearchKey = num;
				ResendGearLvlKey = text3;
				SetRepeatPacket(ResendPacketType.GearPlace);
				SendUDPData(packet);
				return;
			}
			DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = MPSaveManager.RequestSpecificGear(num, level_guid);
			if (slicedJsonDroppedGear != null)
			{
				MelonLogger.Msg("Found " + num);
				string text4 = text2;
				if (text4 == "gear_knife" && Supporters.ConfiguratedBenefits.m_Knife)
				{
					text4 = "GEAR_JeremiahKnife";
				}
				GameObject gearItemObject = GetGearItemObject(text4);
				if (gearItemObject == null)
				{
					MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + text4 + " not exist! Maybe you miss an modded item pack?");
					return;
				}
				GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, position, rotation);
				DisableObjectForXPMode.RemoveDisabler(gameObject.gameObject);
				DataStr.ExtraDataForDroppedGear extra = obj.GetComponent<Comps.DroppedGearDummy>().m_Extra;
				if (gameObject.GetComponent<KeroseneLampItem>() != null && obj != null)
				{
					int num2 = (OverrideLampReduceFuel = MinutesFromStartServer - extra.m_DroppedTime);
					MelonLogger.Msg(System.ConsoleColor.Cyan, "Lamp been dropped " + num2 + " minutes");
				}
				if (gameObject == null)
				{
					MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + text4 + " not exist! Maybe you miss an modded item pack?");
					return;
				}
				gameObject.name = CloneTrimer(gameObject.name);
				if (!string.IsNullOrEmpty(slicedJsonDroppedGear.m_Json))
				{
					gameObject.GetComponent<GearItem>().DeserializeFromJson(slicedJsonDroppedGear.m_Json);
					ThrownFlareSync.ApplyPickupBurn(gameObject.GetComponent<GearItem>(), obj.GetComponent<Comps.DroppedGearDummy>()?.m_Extra);
				}
				if (!string.IsNullOrEmpty(extra.m_PhotoGUID))
				{
					GearItem component = gameObject.GetComponent<GearItem>();
					if (component.m_ObjectGuid == null)
					{
						string photoGUID = extra.m_PhotoGUID;
						ObjectGuid objectGuid = gameObject.AddComponent<ObjectGuid>();
						objectGuid.m_Guid = photoGUID;
						component.m_ObjectGuid = objectGuid;
						if (component.GetGearName() != "GEAR_SCMapReveal")
						{
							MelonLogger.Msg("Going render photo " + photoGUID);
							Texture2D photoTexture = MPSaveManager.GetPhotoTexture(photoGUID, component.GetGearName());
							if ((bool)photoTexture)
							{
								gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = photoTexture;
							}
						}
					}
				}
				gameObject.GetComponent<GearItem>().m_BeenInPlayerInventory = true;
				Comps.DropFakeOnLeave dropFakeOnLeave = gameObject.AddComponent<Comps.DropFakeOnLeave>();
				dropFakeOnLeave.m_OldPossition = gameObject.gameObject.transform.position;
				dropFakeOnLeave.m_OldRotation = gameObject.gameObject.transform.rotation;
				gameObject.GetComponent<GearItem>().PlayPickUpClip();
				GameManager.GetPlayerManagerComponent().StartPlaceMesh(gameObject, PlaceMeshFlags.None);
				DroppedGearsObjs.Remove(num);
				TrackableDroppedGearsObjs.Remove(num);
				if (obj.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy != null)
				{
					GearManager.RemovedFromDroppedDecoys(obj.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy);
					UnityEngine.Object.Destroy(obj.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy.gameObject);
				}
				UnityEngine.Object.Destroy(obj);
				ServerSend.PICKDROPPEDGEAR(0, num, toEveryOne: true);
			}
			else
			{
				MelonLogger.Msg("Gear with hash " + num + " is missing!");
			}
		}
		else
		{
			GearItem component2 = obj.GetComponent<GearItem>();
			if (component2 != null && !component2.m_BeenInPlayerInventory && !ServerConfig.m_DuppedSpawns)
			{
				Comps.DropFakeOnLeave dropFakeOnLeave2 = obj.AddComponent<Comps.DropFakeOnLeave>();
				dropFakeOnLeave2.m_OldPossition = obj.transform.position;
				dropFakeOnLeave2.m_OldRotation = obj.transform.rotation;
				MelonLogger.Msg("Trying place pre-spawned gear, so dropping fake on cancle");
			}
		}
	}

	public static void UseFakeBed(GameObject obj)
	{
		string text = CloneTrimer(obj.name).ToLower();
		Vector3 position = obj.transform.position;
		Quaternion rotation = obj.transform.rotation;
		int num = 0;
		_ = level_guid;
		if (obj.GetComponent<Comps.DroppedGearDummy>() != null)
		{
			num = obj.GetComponent<Comps.DroppedGearDummy>().m_SearchKey;
			MelonLogger.Msg("Searching for " + text + " with hash " + num);
			GameObject gameObject = UnityEngine.Object.Instantiate(GetGearItemObject(text), position, rotation);
			gameObject.name = CloneTrimer(gameObject.name);
			gameObject.GetComponent<GearItem>().m_BeenInPlayerInventory = true;
			gameObject.GetComponent<GearItem>().m_Bed.SetState(BedRollState.Placed);
			gameObject.AddComponent<Comps.FakeBedDummy>().m_LinkedFakeObject = obj;
			gameObject.GetComponent<GearItem>().m_Bed.PerformInteraction();
		}
		else
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "DroppedGearDummy by somereason...");
		}
	}

	public static void PickupSpecialItem(GameObject obj, string ReferenceName)
	{
		HUDMessage.AddMessage("Youn found [50C878]special item[-] use it to start advanced expedition via handheld radio.");
		UnityEngine.Object.Destroy(obj);
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(186))
			{
				packet.Write(ReferenceName);
				SendUDPData(packet);
				return;
			}
		}
		if (iAmHost)
		{
			ExpeditionManager.GivePlayerSpeicalItem(0, ReferenceName);
		}
	}

	public static void PickupDroppedGear(GameObject obj, bool SkipLocksmith = false)
	{
		string name = obj.name;
		string text = CloneTrimer(name).ToLower();
		string text2 = CloneTrimer(name);
		if (text == "gear_technicalbackpack" && GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_TechnicalBackpack"))
		{
			HUDMessage.AddMessage("You already have technical backpack!");
			return;
		}
		Vector3 position = obj.transform.position;
		Quaternion rotation = obj.transform.rotation;
		int num = 0;
		if (obj.GetComponent<Comps.DroppedGearDummy>() != null)
		{
			num = obj.GetComponent<Comps.DroppedGearDummy>().m_SearchKey;
			if (!SkipLocksmith)
			{
				if (Shared.IsLocksmithItem(text) && obj.GetComponent<Comps.DroppedGearDummy>().m_Extra.m_Variant == 4)
				{
					ShowBlankworkingPicker(obj);
					return;
				}
				if (text == "gear_scwoodform" && obj.GetComponent<Comps.DroppedGearDummy>().m_Extra.m_Variant == 5)
				{
					ShowLeadCastingPicker(obj);
					return;
				}
			}
			MelonLogger.Msg("Searching for " + text + " with hash " + num);
			LastPickedVisualPosition = obj.transform.position;
			LastPickedVisualRotation = obj.transform.rotation;
			if (sendMyPosition)
			{
				DoPleaseWait("Please wait...", "Downloading gear...");
				using Packet packet = new Packet(88);
				packet.Write(num);
				packet.Write(level_guid);
				ResendGearSearchKey = num;
				ResendGearLvlKey = level_guid;
				SetRepeatPacket(ResendPacketType.GearPickup);
				SendUDPData(packet);
				return;
			}
			DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = MPSaveManager.RequestSpecificGear(num, level_guid);
			if (slicedJsonDroppedGear != null)
			{
				MelonLogger.Msg("Found " + num);
				string text3 = text2;
				if (text3 == "gear_knife" && Supporters.ConfiguratedBenefits.m_Knife)
				{
					text3 = "GEAR_JeremiahKnife";
				}
				GameObject gearItemObject = GetGearItemObject(text3);
				if (gearItemObject == null)
				{
					MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + text3 + " not exist! Maybe you miss an modded item pack?");
					return;
				}
				GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, position, rotation);
				DisableObjectForXPMode.RemoveDisabler(gameObject.gameObject);
				if (gameObject == null)
				{
					MelonLogger.Msg(System.ConsoleColor.Red, "Gear prefab with name " + text3 + " not exist! Maybe you miss an modded item pack?");
					return;
				}
				gameObject.name = CloneTrimer(gameObject.name);
				Comps.DroppedGearDummy component = obj.GetComponent<Comps.DroppedGearDummy>();
				if (gameObject.GetComponent<KeroseneLampItem>() != null)
				{
					int num2 = (OverrideLampReduceFuel = MinutesFromStartServer - component.m_Extra.m_DroppedTime);
					MelonLogger.Msg(System.ConsoleColor.Cyan, "Lamp been dropped " + num2 + " minutes");
				}
				if (!string.IsNullOrEmpty(slicedJsonDroppedGear.m_Json))
				{
					gameObject.GetComponent<GearItem>().DeserializeFromJson(slicedJsonDroppedGear.m_Json);
					ThrownFlareSync.ApplyPickupBurn(gameObject.GetComponent<GearItem>(), obj.GetComponent<Comps.DroppedGearDummy>()?.m_Extra);
				}
				if (!string.IsNullOrEmpty(component.m_Extra.m_PhotoGUID))
				{
					GearItem component2 = gameObject.GetComponent<GearItem>();
					if (component2.m_ObjectGuid == null)
					{
						string photoGUID = component.m_Extra.m_PhotoGUID;
						ObjectGuid objectGuid = gameObject.AddComponent<ObjectGuid>();
						objectGuid.m_Guid = photoGUID;
						component2.m_ObjectGuid = objectGuid;
						if (component.m_Extra.m_GearName != "gear_scmapreveal")
						{
							MelonLogger.Msg("Going render photo " + photoGUID);
							Texture2D photoTexture = MPSaveManager.GetPhotoTexture(photoGUID, component.m_Extra.m_GearName);
							if ((bool)photoTexture)
							{
								gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = photoTexture;
							}
						}
					}
				}
				if (!string.IsNullOrEmpty(component.m_Extra.m_ExpeditionNote))
				{
					CreateCustomNote(component.m_Extra.m_ExpeditionNote, gameObject.GetComponent<GearItem>());
				}
				gameObject.GetComponent<GearItem>().m_BeenInPlayerInventory = true;
				Comps.DropFakeOnLeave dropFakeOnLeave = gameObject.AddComponent<Comps.DropFakeOnLeave>();
				dropFakeOnLeave.m_OldPossition = gameObject.gameObject.transform.position;
				dropFakeOnLeave.m_OldRotation = gameObject.gameObject.transform.rotation;
				if (component != null && component.m_Extra.m_GoalTime != 0 && component.m_Extra.m_GoalTime != -1)
				{
					GearItem component3 = gameObject.GetComponent<GearItem>();
					if (component3.m_EvolveItem != null)
					{
						int num3 = MinutesFromStartServer - component.m_Extra.m_DroppedTime;
						component3.m_EvolveItem.m_TimeSpentEvolvingGameHours = (float)num3 / 60f;
						MelonLogger.Msg(System.ConsoleColor.Blue, "Saving minutesOnDry " + num3);
						MelonLogger.Msg(System.ConsoleColor.Blue, "m_TimeSpentEvolvingGameHours " + component3.m_EvolveItem.m_TimeSpentEvolvingGameHours);
					}
				}
				bool flag = false;
				if (gameObject.GetComponent<GearItem>().m_Bed != null)
				{
					gameObject.GetComponent<GearItem>().m_Bed.SetState(BedRollState.Rolled);
					flag = true;
				}
				gameObject.GetComponent<GearItem>().m_LastUpdatedTODHours = 0f;
				gameObject.GetComponent<GearItem>().ManualStart();
				gameObject.GetComponent<GearItem>().ManualUpdate();
				if (gameObject.GetComponent<GearItem>().GetGearName() == "GEAR_SCNote" && string.IsNullOrEmpty(component.m_Extra.m_Dropper))
				{
					Pathes.DisplayNote(gameObject.GetComponent<GearItem>());
					flag = true;
				}
				if (!flag)
				{
					GameManager.GetPlayerManagerComponent().InspectDroppedGearCompat(gameObject.GetComponent<GearItem>());
				}
				else
				{
					GameManager.GetPlayerManagerComponent().ProcessPickupItemInteraction(gameObject.GetComponent<GearItem>(), forceEquip: false, forceEquipImmediate: false, skipAudio: false);
				}
				PatchBookReadTime(gameObject.GetComponent<GearItem>());
				DroppedGearsObjs.Remove(num);
				TrackableDroppedGearsObjs.Remove(num);
				if (obj.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy != null)
				{
					GearManager.RemovedFromDroppedDecoys(obj.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy);
					UnityEngine.Object.Destroy(obj.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy.gameObject);
				}
				UnityEngine.Object.Destroy(obj);
				ServerSend.PICKDROPPEDGEAR(0, num, toEveryOne: true);
			}
			else
			{
				MelonLogger.Msg("Gear with hash " + num + " is missing!");
			}
		}
		else
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "DroppedGearDummy by somereason...");
		}
	}

	public static void PickDroppedItem(int Hash, int Picker)
	{
		DroppedGearsObjs.TryGetValue(Hash, out var value);
		if (value != null)
		{
			DroppedGearsObjs.Remove(Hash);
			TrackableDroppedGearsObjs.Remove(Hash);
			if (value.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy != null)
			{
				GearManager.RemovedFromDroppedDecoys(value.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy);
				UnityEngine.Object.Destroy(value.GetComponent<Comps.DroppedGearDummy>().m_DecoyDummy.gameObject);
			}
			UnityEngine.Object.Destroy(value);
		}
		if (Picker != -1 && !DedicatedServerAppMode && players[Picker] != null && players[Picker].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
		{
			players[Picker].GetComponent<Comps.MultiplayerPlayerAnimator>().Pickup();
		}
	}

	public static System.Collections.Generic.List<DataStr.AffictionSync> BuildMyAfflictionList()
	{
		System.Collections.Generic.List<Affliction> list = new System.Collections.Generic.List<Affliction>();
		Panel_Affliction.RefreshtListOfShownAfflictionTypes();
		Il2CppSystem.Collections.Generic.List<AfflictionType>.Enumerator enumerator = Panel_Affliction.s_ShownAfflictionTypes.GetEnumerator();
		while (enumerator.MoveNext())
		{
			AfflictionType current = enumerator.Current;
			int afflictionsCount = Panel_Affliction.GetAfflictionsCount(current);
			for (int i = 0; i < afflictionsCount; i++)
			{
				Affliction currentAffliction = Panel_Affliction.GetCurrentAffliction(current, i);
				list.Add(currentAffliction);
			}
		}
		System.Collections.Generic.List<DataStr.AffictionSync> list2 = new System.Collections.Generic.List<DataStr.AffictionSync>();
		for (int j = 0; j < list.Count; j++)
		{
			DataStr.AffictionSync affictionSync = new DataStr.AffictionSync();
			affictionSync.m_Type = (int)list[j].m_AfflictionType;
			affictionSync.m_Location = (int)list[j].m_Location;
			if (list[j].m_Cause == null)
			{
				affictionSync.m_Case = "Unknown";
			}
			else
			{
				affictionSync.m_Case = list[j].m_Cause;
			}
			affictionSync.m_Case = list[j].m_Cause;
			affictionSync.m_ShouldBeTreated = Panel_Affliction.CanBeTreated(list[j]);
			list2.Add(affictionSync);
		}
		return list2;
	}

	public static void SendMyAffictions(int PlayerSendTo, float hp, float hpmax, float thirst, float hunger, float hungermax)
	{
		System.Collections.Generic.List<DataStr.AffictionSync> list = BuildMyAfflictionList();
		if (iAmHost)
		{
			ServerSend.SENDMYAFFLCTIONS(PlayerSendTo, list, hp, hpmax, thirst, hunger, hungermax, 0);
		}
		if (!sendMyPosition)
		{
			return;
		}
		using Packet packet = new Packet(103);
		packet.Write(PlayerSendTo);
		packet.Write(list.Count);
		packet.Write(hp);
		packet.Write(hpmax);
		packet.Write(thirst);
		packet.Write(hunger);
		packet.Write(hungermax);
		for (int i = 0; i < list.Count; i++)
		{
			packet.Write(list[i]);
		}
		SendUDPData(packet);
	}

	public static void OtherPlayerCuredMyAffiction(int CureBy, DataStr.AffictionSync Aff, int FirstAidTier = 1, bool Medkit = false)
	{
		bool flag = true;
		if (Aff.m_Type == 5 || Aff.m_Type == 7 || Aff.m_Type == 17 || Aff.m_Type == 27)
		{
			flag = false;
		}
		if (flag)
		{
			int num = 0;
			if (Medkit)
			{
				num = 2;
			}
			switch (FirstAidTier)
			{
			case 1:
				ApplyTreatmentRegeneration(30 + num, 0.25f, "Treatment by " + GetPlayerName(CureBy));
				break;
			case 2:
				ApplyTreatmentRegeneration(33 + num, 0.25f, "Treatment by " + GetPlayerName(CureBy));
				break;
			case 3:
				ApplyTreatmentRegeneration(33 + num, 0.25f, "Treatment by " + GetPlayerName(CureBy));
				GameManager.GetConditionComponent().AddHealth(3f, DamageSource.FirstAid);
				break;
			case 4:
				ApplyTreatmentRegeneration(34 + num, 0.25f, "Treatment by " + GetPlayerName(CureBy));
				GameManager.GetConditionComponent().AddHealth(5f, DamageSource.FirstAid);
				break;
			case 5:
				ApplyTreatmentRegeneration(36f, 0.25f, "Treatment by " + GetPlayerName(CureBy));
				GameManager.GetConditionComponent().AddHealth(5f, DamageSource.FirstAid);
				break;
			}
		}
		Panel_Affliction.RefreshtListOfShownAfflictionTypes();
		if (Aff.m_Type == 4)
		{
			GameManager.GetFoodPoisoningComponent().TakeAntibiotics();
		}
		else if (Aff.m_Type == 2)
		{
			GameManager.GetDysenteryComponent().TakeAntibiotics();
			GameManager.GetDysenteryComponent().DrinkCleanWater(GameCompat.Liters(2f));
		}
		else if (Aff.m_Type == 5)
		{
			for (int i = 0; i < GameManager.GetSprainedAnkleComponent().m_Locations.Count; i++)
			{
				if (GameManager.GetSprainedAnkleComponent().m_Locations[i] == Aff.m_Location)
				{
					GameManager.GetSprainedAnkleComponent().ApplyBandage(i);
					break;
				}
			}
		}
		else if (Aff.m_Type == 7)
		{
			for (int j = 0; j < GameManager.GetSprainedWristComponent().m_Locations.Count; j++)
			{
				if (GameManager.GetSprainedWristComponent().m_Locations[j] == Aff.m_Location)
				{
					GameManager.GetSprainedWristComponent().ApplyBandage(j);
					break;
				}
			}
		}
		else if (Aff.m_Type == 27)
		{
			for (int k = 0; k < GameManager.GetSprainPainComponent().m_ActiveInstances.Count; k++)
			{
				if (GameManager.GetSprainPainComponent().m_ActiveInstances[k].m_Location == (AfflictionBodyArea)Aff.m_Location)
				{
					GameManager.GetSprainPainComponent().TakePainKillers(k);
					break;
				}
			}
		}
		else if (Aff.m_Type == 1)
		{
			GameManager.GetBurnsComponent().ApplyBandage();
			GameManager.GetBurnsComponent().TakePainKillers();
		}
		else if (Aff.m_Type == 20)
		{
			GameManager.GetBurnsElectricComponent().ApplyBandage();
			GameManager.GetBurnsElectricComponent().TakePainKillers();
		}
		else if (Aff.m_Type == 0)
		{
			for (int l = 0; l < GameManager.GetBloodLossComponent().m_Locations.Count; l++)
			{
				if (GameManager.GetBloodLossComponent().m_Locations[l] == Aff.m_Location)
				{
					GameManager.GetBloodLossComponent().ApplyBandage(l);
					break;
				}
			}
		}
		else if (Aff.m_Type == 21)
		{
			for (int m = 0; m < GameManager.GetBrokenRibComponent().m_Locations.Count; m++)
			{
				if (GameManager.GetBrokenRibComponent().m_Locations[m] == Aff.m_Location)
				{
					GameManager.GetBrokenRibComponent().ApplyBandage(m, 2);
					GameManager.GetBrokenRibComponent().TakePainKillers(m, 4);
					break;
				}
			}
		}
		else if (Aff.m_Type == 6)
		{
			GameManager.GetInfectionRiskComponent().ApplyAntisepticToLocation((AfflictionBodyArea)Aff.m_Location);
		}
		else if (Aff.m_Type == 3)
		{
			for (int n = 0; n < GameManager.GetInfectionComponent().m_Locations.Count; n++)
			{
				if (GameManager.GetInfectionComponent().m_Locations[n] == Aff.m_Location)
				{
					GameManager.GetInfectionComponent().TakeAntibiotics(n);
					break;
				}
			}
		}
		else if (Aff.m_Type == 16)
		{
			GameManager.GetIntestinalParasitesComponent().TakeAntibiotics();
		}
	}

	public static bool HasMedkit()
	{
		if (GameManager.m_Inventory != null)
		{
			return GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_MedicalSupplies_hangar");
		}
		return false;
	}

	public static int GetFirstAidSkillTier()
	{
		if (GameManager.GetSkillsManager() != null)
		{
			return GetCustomSkill(CustomSkills.FirstAid).GetCurrentTierNumber() + 1;
		}
		return 1;
	}

	public static void SendCureAffliction(DataStr.AffictionSync Aff)
	{
		if (DebugDiagnosis)
		{
			OtherPlayerCuredMyAffiction(0, Aff, GetFirstAidSkillTier(), HasMedkit());
			return;
		}
		if (iAmHost)
		{
			ServerSend.CUREAFFLICTION(CurePlayerID, 0, Aff, GetFirstAidSkillTier(), HasMedkit());
		}
		if (!sendMyPosition)
		{
			return;
		}
		using Packet packet = new Packet(104);
		packet.Write(Aff);
		packet.Write(GetFirstAidSkillTier());
		packet.Write(HasMedkit());
		packet.Write(CurePlayerID);
		SendUDPData(packet);
	}

	public static void TryToCheckPlayer(int PlayerID)
	{
		SetRepeatPacket(ResendPacketType.Examine, 10);
		DoPleaseWait("Diagnosis player", "Please wait...");
		MelonLogger.Msg("Trying diagnosis client " + PlayerID);
		if (iAmHost)
		{
			ServerSend.TRYDIAGNISISPLAYER(PlayerID, ClientUser.myId);
		}
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(102))
			{
				packet.Write(PlayerID);
				SendUDPData(packet);
			}
		}
	}

	public static void CheckOtherPlayer(System.Collections.Generic.List<DataStr.AffictionSync> Affs, int PlayerID, float health, float maxhealth, float thirst, float hunger, float hungermax)
	{
		if (DiagnosisDummy != null)
		{
			UnityEngine.Object.Destroy(DiagnosisDummy);
		}
		CurePlayerID = PlayerID;
		DiagnosisDummy = new GameObject();
		NPC nPC = DiagnosisDummy.AddComponent<NPC>();
		NPCAfflictions nPCAfflictions = DiagnosisDummy.AddComponent<NPCAfflictions>();
		NPCCondition nPCCondition = DiagnosisDummy.AddComponent<NPCCondition>();
		BodyBigCarryItem bodyBigCarryItem = DiagnosisDummy.AddComponent<BodyBigCarryItem>();
		NPCThirst nPCThirst = DiagnosisDummy.AddComponent<NPCThirst>();
		NPCVoice voice = DiagnosisDummy.AddComponent<NPCVoice>();
		NPCFreezing nPCFreezing = DiagnosisDummy.AddComponent<NPCFreezing>();
		nPC.m_AfflictionsComponent = nPCAfflictions;
		nPC.m_Condition = nPCCondition;
		nPC.m_BodyBigCarryItem = bodyBigCarryItem;
		nPC.m_Thirst = nPCThirst;
		nPC.m_Voice = voice;
		nPC.m_Freezing = nPCFreezing;
		nPCAfflictions.m_NPC = nPC;
		nPCThirst.m_NPC = nPC;
		nPCFreezing.m_NPC = nPC;
		nPCAfflictions.m_Afflictions.Clear();
		bool flag = false;
		bool flag2 = false;
		for (int i = 0; i < Affs.Count; i++)
		{
			AfflictionDefinition afflictionDefinitionByType = GameManager.GetAfflictionDefinitionTable().GetAfflictionDefinitionByType((AfflictionType)Affs[i].m_Type);
			NPCAffliction nPCAffliction = new NPCAffliction();
			if (!Affs[i].m_ShouldBeTreated)
			{
				nPCAffliction.m_RestHours = 1f;
			}
			else
			{
				flag2 = true;
			}
			nPCAffliction.m_Definition = afflictionDefinitionByType;
			nPCAffliction.m_Location = (AfflictionBodyArea)Affs[i].m_Location;
			nPCAffliction.m_CauseLocId = Affs[i].m_Case;
			nPCAfflictions.m_Afflictions.Add(nPCAffliction);
			if (afflictionDefinitionByType.m_AfflictionType == AfflictionType.BloodLoss)
			{
				flag = true;
			}
		}
		nPCCondition.m_CurrentHP = health;
		nPCCondition.m_MaxHP = maxhealth;
		nPCThirst.m_CurrentThirstPercentage = thirst;
		nPCThirst.m_InitialThirstPercentage = thirst;
		nPCThirst.m_ThirstIncreasePerHour = 0f;
		nPCFreezing.m_CurrentFreezing = 100f - Mathf.Clamp(0.5f + 100f * hunger / hungermax, 0f, 100f);
		nPCFreezing.m_MaxFreezing = hungermax;
		Panel_Diagnosis panel_Diagnosis = GameCompat.Panel<Panel_Diagnosis>();
		RemovePleaseWait();
		DiscardRepeatPacket();
		panel_Diagnosis.Enable(enable: true, nPC);
		GameObject gameObject = panel_Diagnosis.gameObject.transform.GetChild(2).gameObject;
		GameObject gameObject2 = panel_Diagnosis.gameObject.transform.GetChild(3).gameObject;
		gameObject.SetActive(value: true);
		gameObject2.transform.GetChild(1).GetChild(0).GetChild(0)
			.GetChild(6)
			.gameObject.GetComponent<UISprite>().mSpriteName = "ico_status_hunger1";
		gameObject2.transform.GetChild(1).GetChild(0).GetChild(0)
			.GetChild(7)
			.gameObject.GetComponent<UISprite>().mSpriteName = "ico_status_hunger3";
		gameObject2.SetActive(value: true);
		string eventName = "PLAY_SURVIVORDIAGNOSIS";
		if (PlayerManager.m_VoicePersona == VoicePersona.Male)
		{
			if (!flag2)
			{
				eventName = "PLAY_SNDVOSMMAC1750";
			}
			else if (flag)
			{
				eventName = "PLAY_SNDVOSMMAC1820";
			}
			else
			{
				System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
				list.Add("PLAY_SNDVOSMMAC2150");
				list.Add("PLAY_SNDVOSMMACE422950_01");
				list.Add("PLAY_SNDVOSMMAC1770");
				System.Random random = new System.Random();
				eventName = list[random.Next(list.Count)];
			}
		}
		GameManager.GetPlayerVoiceComponent().Play(eventName, Il2CppVoice.Priority.Critical);
	}

	public static int TimeToDry(string gearName)
	{
		if (gearName.Contains("GEAR_WolfPelt"))
		{
			return 10080;
		}
		if (gearName.Contains("GEAR_BearHide"))
		{
			return 17280;
		}
		if (gearName.Contains("GEAR_Gut"))
		{
			return 7200;
		}
		if (gearName.Contains("GEAR_MooseHide"))
		{
			return 14400;
		}
		if (gearName.Contains("GEAR_LeatherHide"))
		{
			return 7200;
		}
		if (gearName.Contains("GEAR_RabbitPelt"))
		{
			return 4320;
		}
		if (gearName.Contains("GEAR_Leather"))
		{
			return 7200;
		}
		if (gearName.Contains("GEAR_MapleSapling"))
		{
			return 8640;
		}
		if (gearName.Contains("GEAR_BirchSapling"))
		{
			return 5760;
		}
		if (gearName.Contains("GEAR_Snare"))
		{
			return 720;
		}
		if (gearName.Contains("GEAR_SCWoodFormB"))
		{
			return 60;
		}
		return 7200;
	}

	public static void SendDropItem(GearItem gear, int nums = 0, int total = 0, bool samepose = false, int variant = 0, GameObject Around = null)
	{
		if (!(gear != null) || !(gear.gameObject != null))
		{
			return;
		}
		GameObject gameObject = gear.gameObject;
		string text = "";
		if (!samepose)
		{
			if (Around == null)
			{
				gear.StickToGroundAtPlayerFeet(GameManager.GetPlayerTransform().position);
			}
			else
			{
				Vector3 position = Around.transform.position;
				float num = UnityEngine.Random.Range(0f, 1.1f);
				Vector3 vector = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 359), 0f) * Vector3.forward;
				gear.StickToGroundAndOrientOnSlope(position + vector * num, NavMeshCheck.IgnoreNavMesh, 0.5f);
			}
		}
		else if (gameObject.GetComponent<Comps.DropFakeOnLeave>() != null)
		{
			Comps.DropFakeOnLeave component = gameObject.GetComponent<Comps.DropFakeOnLeave>();
			gameObject.transform.position = component.m_OldPossition;
			gameObject.transform.rotation = component.m_OldRotation;
		}
		Vector3 position2 = gear.gameObject.transform.position;
		Quaternion rotation = gear.gameObject.transform.rotation;
		if (Shared.IsLocksmithItem(gear.GetGearName().ToLower()))
		{
			if (gear.GetGearName().ToLower().Contains("gear_scmetalblank"))
			{
				gameObject.transform.position = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y + 0.15f, gameObject.transform.position.z);
			}
			variant = 0;
			if (Physics.Raycast(gameObject.transform.position, gameObject.transform.TransformDirection(Vector3.down), out var hitInfo, 8f) && (bool)hitInfo.collider && (bool)hitInfo.collider.gameObject.GetComponent<WorkBench>())
			{
				variant = 4;
			}
		}
		else if (gear.GetGearName() == "GEAR_SCWoodForm")
		{
			variant = 5;
		}
		if (IsUserGeneratedHandItem(gear.GetGearName()) && gear.m_ObjectGuid != null && !string.IsNullOrEmpty(gear.m_ObjectGuid.m_Guid))
		{
			text = gear.m_ObjectGuid.m_Guid;
			MelonLogger.Msg("Dropped photo " + text);
		}
		string name = gameObject.name;
		string text2 = CloneTrimer(name).ToLower();
		string text3 = CloneTrimer(name);
		int vectorHash = Shared.GetVectorHash(position2);
		int quaternionHash = Shared.GetQuaternionHash(rotation);
		int hashCode = Shared.StableHash(level_guid);
		int hash = vectorHash + quaternionHash + hashCode;
		string text4 = level_guid;
		if (nums > 0 && gear.m_StackableItem != null)
		{
			gear.m_StackableItem.m_Units = nums;
		}
		string text5 = gear.SerializeToString();
		int num2 = 0;
		int num3 = 0;
		if (gear.m_EvolveItem != null)
		{
			if (gear.m_EvolveItem.CanEvolve())
			{
				int num4 = System.Convert.ToInt32(gear.m_EvolveItem.m_TimeToEvolveGameDays) * 24 * 60;
				if (gear.GetGearName() == "GEAR_SCWoodFormB")
				{
					num4 = 60;
				}
				if (gear.m_EvolveItem.m_TimeSpentEvolvingGameHours != 0f)
				{
					num3 = (int)gear.m_EvolveItem.m_TimeSpentEvolvingGameHours * 60;
				}
				num2 = num4;
				MelonLogger.Msg("m_TimeSpentEvolvingGameHours " + gear.m_EvolveItem.m_TimeSpentEvolvingGameHours);
				MelonLogger.Msg("MinuteToSkip " + num3);
				MelonLogger.Msg("NeedToDry " + num2);
			}
			else
			{
				num2 = -1;
			}
		}
		if (gear.m_SnareItem != null)
		{
			variant = (int)gear.m_SnareItem.m_State;
			if (variant == 1)
			{
				if (SnareCanTrap(position2, gear.m_SnareItem.m_RabbitPrefab.name))
				{
					num2 = 720;
					if (GameManager.GetFeatExpertTrapper().IsUnlockedAndEnabled())
					{
						variant = 4;
					}
				}
				else
				{
					num2 = -1;
				}
			}
		}
		MelonLogger.Msg("Dropping " + text2 + " Hash " + hash + " variant " + variant);
		if (nums == 0)
		{
			UnityEngine.Object.Destroy(gameObject);
		}
		else if (gear.m_StackableItem != null)
		{
			gear.m_StackableItem.m_Units = total - nums;
			GameManager.GetInventoryComponent().AddGear(gameObject.GetComponent<GearItem>());
		}
		DataStr.DroppedGearItemDataPacket droppedGearItemDataPacket = new DataStr.DroppedGearItemDataPacket();
		droppedGearItemDataPacket.m_GearID = -1;
		droppedGearItemDataPacket.m_Position = position2;
		droppedGearItemDataPacket.m_Rotation = rotation;
		droppedGearItemDataPacket.m_LevelID = levelid;
		droppedGearItemDataPacket.m_LevelGUID = level_guid;
		droppedGearItemDataPacket.m_Hash = hash;
		DataStr.ExtraDataForDroppedGear extraDataForDroppedGear = new DataStr.ExtraDataForDroppedGear();
		extraDataForDroppedGear.m_Dropper = MyChatName;
		extraDataForDroppedGear.m_DroppedTime = MinutesFromStartServer - num3;
		extraDataForDroppedGear.m_Variant = variant;
		extraDataForDroppedGear.m_PhotoGUID = text;
		if (text3 != "")
		{
			extraDataForDroppedGear.m_GearName = text3;
		}
		if (num2 != 0 && num2 != -1)
		{
			extraDataForDroppedGear.m_GoalTime = MinutesFromStartServer + num2;
		}
		else
		{
			extraDataForDroppedGear.m_GoalTime = num2;
		}
		if (gear.m_KeroseneLampItem != null)
		{
			if (variant == 1)
			{
				float num5 = gear.m_KeroseneLampItem.m_CurrentFuelLiters.ToLiters();
				float num6 = gear.m_KeroseneLampItem.m_FuelBurnPerHour.ToLiters() / 60f;
				float num7 = num5 / num6;
				extraDataForDroppedGear.m_GoalTime = MinutesFromStartServer + (int)System.Math.Round(num7);
			}
			else
			{
				extraDataForDroppedGear.m_GoalTime = 0;
			}
		}
		ThrownFlareSync.FillDropExtra(gear, extraDataForDroppedGear);
		droppedGearItemDataPacket.m_Extra = extraDataForDroppedGear;
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(85))
			{
				packet.Write(droppedGearItemDataPacket);
				SendUDPData(packet);
			}
			byte[] bytes = Encoding.UTF8.GetBytes(text5);
			if (bytes.Length > 500)
			{
				System.Collections.Generic.List<byte> list = new System.Collections.Generic.List<byte>();
				list.AddRange(bytes);
				while (list.Count >= 500)
				{
					byte[] bytes2 = list.GetRange(0, 499).ToArray();
					list.RemoveRange(0, 499);
					string str = Encoding.UTF8.GetString(bytes2);
					DataStr.SlicedJsonData slicedJsonData = new DataStr.SlicedJsonData();
					slicedJsonData.m_GearName = text4;
					slicedJsonData.m_SendTo = -1;
					slicedJsonData.m_Hash = hash;
					slicedJsonData.m_Str = str;
					if (list.Count != 0)
					{
						slicedJsonData.m_Last = false;
					}
					else
					{
						slicedJsonData.m_Last = true;
					}
					AddGearCarefulSlice(slicedJsonData);
				}
				if (list.Count < 500 && list.Count != 0)
				{
					byte[] bytes3 = list.GetRange(0, list.Count).ToArray();
					list.RemoveRange(0, list.Count);
					string str2 = Encoding.UTF8.GetString(bytes3);
					DataStr.SlicedJsonData slicedJsonData2 = new DataStr.SlicedJsonData();
					slicedJsonData2.m_GearName = text4;
					slicedJsonData2.m_SendTo = -1;
					slicedJsonData2.m_Hash = hash;
					slicedJsonData2.m_Str = str2;
					slicedJsonData2.m_Last = true;
					slicedJsonData2.m_Extra = extraDataForDroppedGear;
					if (sendMyPosition)
					{
						AddGearCarefulSlice(slicedJsonData2);
					}
					SendNextGearCarefulSlice();
				}
			}
			else
			{
				DataStr.SlicedJsonData slicedJsonData3 = new DataStr.SlicedJsonData();
				slicedJsonData3.m_GearName = text4;
				slicedJsonData3.m_SendTo = -1;
				slicedJsonData3.m_Hash = hash;
				slicedJsonData3.m_Str = text5;
				slicedJsonData3.m_Last = true;
				slicedJsonData3.m_Extra = extraDataForDroppedGear;
				if (sendMyPosition)
				{
					using Packet packet2 = new Packet(86);
					packet2.Write(slicedJsonData3);
					SendUDPData(packet2);
				}
			}
		}
		if (iAmHost)
		{
			Shared.AddDroppedGear(-1, hash, text5, text4, extraDataForDroppedGear);
			ServerSend.DROPITEM(0, droppedGearItemDataPacket, toEveryOne: true);
			Shared.FakeDropItem(droppedGearItemDataPacket);
		}
	}

	public static void CreateFolderIfNotExist(string path)
	{
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
		}
	}

	public static bool ApplyingRemoteOpenable;

	// Remote open/close through the game's own Open/Close, so the door's state, animation direction, collision and
	// navmesh stay consistent. Playing the animation by hand left doors whose "closed" pose looked open.
	public static void FakeClose(OpenClose OpCl)
	{
		if (!OpCl.IsOpen())
		{
			return;
		}
		ApplyingRemoteOpenable = true;
		try
		{
			OpCl.Close(isImmediate: false);
		}
		finally
		{
			ApplyingRemoteOpenable = false;
		}
		MelonLogger.Msg("Forcing openable thing to close");
	}

	public static void FakeOpen(OpenClose OpCl)
	{
		if (OpCl.IsOpen() || (OpCl.m_Safe != null && !OpCl.m_Safe.m_Cracked))
		{
			return;
		}
		ApplyingRemoteOpenable = true;
		try
		{
			OpCl.Open(isImmediate: false);
		}
		finally
		{
			ApplyingRemoteOpenable = false;
		}
		MelonLogger.Msg("Forcing openable thing to open");
	}

	public static void ApplyOpenables()
	{
		if (OpenCloseManager.s_ActiveOpenClosers != null && OpenablesObjs.Count < OpenCloseManager.s_ActiveOpenClosers.Count)
		{
			OpenablesObjs.Clear();
			for (int i = 0; i < OpenCloseManager.s_ActiveOpenClosers.Count; i++)
			{
				OpenClose openClose = OpenCloseManager.s_ActiveOpenClosers[i];
				string text = "";
				if (!(openClose != null) || !(openClose.gameObject != null))
				{
					continue;
				}
				GameObject gameObject = openClose.gameObject;
				if (gameObject.GetComponent<ObjectGuid>() != null)
				{
					text = gameObject.GetComponent<ObjectGuid>().Get();
					if (!OpenablesObjs.ContainsKey(text))
					{
						OpenablesObjs.Add(text, gameObject);
					}
				}
			}
		}
		foreach (System.Collections.Generic.KeyValuePair<string, bool> openableThing in OpenableThings)
		{
			if (!OpenablesObjs.TryGetValue(openableThing.Key, out var value) || !(value != null))
			{
				continue;
			}
			OpenClose component = value.GetComponent<OpenClose>();
			// Forcing a door that is still animating, or that this player just used, played a second open/close
			// animation over the native one and left the door turned the wrong way.
			if (component == null || component.m_Animating || Time.realtimeSinceStartup - LastLocalOpenableUse(openableThing.Key) < 2f)
			{
				continue;
			}
			if (component.IsOpen() != openableThing.Value)
			{
				if (!openableThing.Value)
				{
					FakeClose(component);
				}
				else
				{
					FakeOpen(component);
				}
			}
		}
	}

	private static readonly System.Collections.Generic.Dictionary<string, float> LocalOpenableUse = new System.Collections.Generic.Dictionary<string, float>();

	public static void MarkLocalOpenableUse(string GUID)
	{
		LocalOpenableUse[GUID] = Time.realtimeSinceStartup;
	}

	private static float LastLocalOpenableUse(string GUID)
	{
		if (!LocalOpenableUse.TryGetValue(GUID, out var value))
		{
			return -100f;
		}
		return value;
	}

	public static void SendOpenableThing(string Scene, string GUID, bool state)
	{
		using Packet packet = new Packet(101);
		packet.Write(Scene);
		packet.Write(GUID);
		packet.Write(state);
		SendUDPData(packet);
	}

	public static void AddCarefulSlice(DataStr.SlicedJsonData slice)
	{
		CarefulSlicesBuffer.Add(slice);
	}

	public static void AddGearCarefulSlice(DataStr.SlicedJsonData slice)
	{
		CarefulSlicesGearBuffer.Add(slice);
	}

	public static void AddPhotoCarefulSlice(DataStr.SlicedJsonData slice)
	{
		CarefulSlicesPhotoBuffer.Add(slice);
	}

	public static void SendNextGearCarefulSlice()
	{
		if (CarefulSlicesGearBuffer.Count > 0)
		{
			DataStr.SlicedJsonData obj = CarefulSlicesGearBuffer[0];
			using (Packet packet = new Packet(86))
			{
				packet.Write(obj);
				SendUDPData(packet);
			}
			CarefulSlicesGearBuffer.Remove(CarefulSlicesGearBuffer[0]);
			CarefulSlicesSent++;
		}
		else
		{
			MelonLogger.Msg("Finished sending all " + CarefulSlicesSent + " slices");
			CarefulSlicesSent = 0;
		}
	}

	public static string KitstartFilterContainer(string Data)
	{
		ContainerSaveDataProxy containerSaveDataProxy = Utils.DeserializeObject<ContainerSaveDataProxy>(Data);
		for (int num = containerSaveDataProxy.m_SerializedItems.Count - 1; num >= 0; num--)
		{
			ContainerItemSaveData containerItemSaveData = containerSaveDataProxy.m_SerializedItems[num];
			if (containerItemSaveData != null)
			{
				Shared.GetInterloperReplace(containerItemSaveData.m_PrefabName);
				if (containerItemSaveData.m_PrefabName == "GEAR_SCHeatPack" || containerItemSaveData.m_PrefabName == "GEAR_SCHeatPackC" || containerItemSaveData.m_PrefabName == "GEAR_SCEmergencyRation")
				{
					containerSaveDataProxy.m_SerializedItems.RemoveAt(num);
				}
			}
		}
		return Utils.SerializeObject(containerSaveDataProxy);
	}

	public static string InterloperFilterContainer(string Data)
	{
		Data = KitstartFilterContainer(Data);
		if (ExperienceModeManager.GetCurrentExperienceModeType() != ExperienceModeType.Interloper)
		{
			return Data;
		}
		ContainerSaveDataProxy containerSaveDataProxy = Utils.DeserializeObject<ContainerSaveDataProxy>(Data);
		for (int num = containerSaveDataProxy.m_SerializedItems.Count - 1; num >= 0; num--)
		{
			ContainerItemSaveData containerItemSaveData = containerSaveDataProxy.m_SerializedItems[num];
			if (containerItemSaveData != null)
			{
				string interloperReplace = Shared.GetInterloperReplace(containerItemSaveData.m_PrefabName);
				if (!(containerItemSaveData.m_PrefabName == interloperReplace))
				{
					if (string.IsNullOrEmpty(interloperReplace))
					{
						containerSaveDataProxy.m_SerializedItems.RemoveAt(num);
					}
					else
					{
						containerItemSaveData.m_SerializedGear = containerItemSaveData.m_SerializedGear.Replace(containerItemSaveData.m_PrefabName, interloperReplace);
						containerItemSaveData.m_PrefabName = interloperReplace;
					}
				}
			}
		}
		return Utils.SerializeObject(containerSaveDataProxy);
	}

	public static void FinishOpeningFakeContainer(string CompressedData)
	{
		if (!CoopContainerFlow.BufferReply(CompressedData))
		{
			MelonLogger.Msg("Finish Opening Fake Container");
			string text = "";
			if (CompressedData != "")
			{
				text = Shared.DecompressString(CompressedData);
			}
			Container goingToOpenContinaer = GoingToOpenContinaer;
			GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Normal);
			GameManager.GetPlayerManagerComponent().m_Container = null;
			GameCompat.Panel<Panel_HUD>().CancelItemProgressBar();
			goingToOpenContinaer.m_SearchInProgress = false;
			goingToOpenContinaer.m_OpenInProgress = false;
			goingToOpenContinaer.m_Inspected = true;
			goingToOpenContinaer.m_StartInspected = true;
			if (text == "")
			{
				MelonLogger.Msg("Container empty");
				goingToOpenContinaer.DestroyAllGear();
			}
			else
			{
				MelonLogger.Msg("Opening with loot, loading loot to the container");
				Il2CppSystem.Collections.Generic.List<GearItem> loadedItems = new Il2CppSystem.Collections.Generic.List<GearItem>();
				goingToOpenContinaer.Deserialize(InterloperFilterContainer(text), loadedItems);
			}
			GameCompat.Panel<Panel_Container>().SetContainer(goingToOpenContinaer, goingToOpenContinaer.m_LocalizedDisplayName.Text());
			ContainerLootSync.AcceptedOpen(goingToOpenContinaer);
			CoopContainerFlow.Present(goingToOpenContinaer);
			goingToOpenContinaer.m_Inspected = true;
			goingToOpenContinaer.m_StartInspected = true;
			GoingToOpenContinaer = null;
		}
	}

	public static void OpenFakeContainer(Container box)
	{
		string guid = box.GetComponent<ObjectGuid>().Get();
		if (sendMyPosition)
		{
			CoopContainerFlow.Activate(box);
			return;
		}
		string orInitialize = ContainerLootSync.GetOrInitialize(level_guid, guid, ContainerLootSync.NativeSnapshot(box));
		string text = "";
		if (orInitialize != "")
		{
			text = Shared.DecompressString(orInitialize);
		}
		GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Normal);
		GameManager.GetPlayerManagerComponent().m_Container = null;
		GameCompat.Panel<Panel_HUD>().CancelItemProgressBar();
		box.m_SearchInProgress = false;
		box.m_OpenInProgress = false;
		box.m_Inspected = true;
		box.m_StartInspected = true;
		if (text == "")
		{
			box.DestroyAllGear();
		}
		else
		{
			Il2CppSystem.Collections.Generic.List<GearItem> loadedItems = new Il2CppSystem.Collections.Generic.List<GearItem>();
			box.Deserialize(InterloperFilterContainer(text), loadedItems);
		}
		box.m_Inspected = true;
		box.m_StartInspected = true;
		GameCompat.Panel<Panel_Container>().SetContainer(box, box.m_LocalizedDisplayName.Text());
		ContainerLootSync.AcceptedOpen(box);
		CoopContainerFlow.Present(box);
	}

	public static void CloseFakeContainer(Container box)
	{
		if (!ContainerLootSync.TryBeginClose(box))
		{
			return;
		}
		string text = box.GetComponent<ObjectGuid>().Get();
		box.m_Inspected = true;
		box.m_StartInspected = true;
		string text2 = box.Serialize();
		string text3 = Shared.CompressString(text2);
		if (!string.IsNullOrEmpty(DeBugMenu.ContainerOverride))
		{
			text3 = DeBugMenu.ContainerOverride;
		}
		bool flag = false;
		int state = 0;
		if (box.gameObject.GetComponent<Comps.DeathDropContainer>() != null)
		{
			if (box.m_Items != null && box.m_Items.Count <= 2)
			{
				for (int num = box.m_Items.Count - 1; num >= 0; num--)
				{
					GearItemObject gearItemObject = box.m_Items[num];
					if ((bool)gearItemObject.m_GearItem && (bool)gearItemObject.m_GearItem.m_WaterSupply && gearItemObject.m_GearItem.m_WaterSupply.m_VolumeInLiters == GameCompat.Liters(0f))
					{
						box.RemoveGear(gearItemObject.m_GearItem);
					}
				}
			}
			flag = true;
		}
		bool flag2 = box.IsEmpty();
		box.DestroyAllGear();
		if (!flag2)
		{
			state = 1;
		}
		MelonLogger.Msg("[CloseFakeContainer] " + text + " Is Empty " + flag2);
		if (GameManager.GetInventoryComponent().GetNumGearWithName("GEAR_TechnicalBackpack") > 1)
		{
			Il2CppSystem.Collections.Generic.List<GearItemObject> items = GameManager.GetInventoryComponent().m_Items;
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i].m_GearItemName == "GEAR_TechnicalBackpack")
				{
					items[i].m_GearItem.Drop(1);
					break;
				}
			}
		}
		Shared.AddLootedContainer(new DataStr.ContainerOpenSync
		{
			m_Guid = text,
			m_LevelGUID = level_guid,
			m_Inspected = true
		}, needSync: true, ClientUser.myId, state);
		if (box != null && box.GetComponent<Comps.ContainersSync>() != null)
		{
			box.GetComponent<Comps.ContainersSync>().m_Empty = flag2;
		}
		if (flag2)
		{
			MelonLogger.Msg("[CloseFakeContainer] Removing container");
			if (flag)
			{
				RemoveDeathContainer(text, level_guid);
			}
			if (sendMyPosition)
			{
				using Packet packet = new Packet(123);
				packet.Write(text);
				packet.Write(level_guid);
				packet.Write(flag);
				SendUDPData(packet);
			}
			else
			{
				MPSaveManager.RemoveContainer(level_guid, text);
			}
		}
		else
		{
			MelonLogger.Msg("[CloseFakeContainer] Saving container");
			if (sendMyPosition)
			{
				ContainerLootSync.AwaitUpload();
				Shared.SendContainerData(text3, level_guid, text, text2);
				return;
			}
			MPSaveManager.SaveContainer(level_guid, text, text3);
		}
		if (box != null)
		{
			box.BeginContainerClose();
			if (box.m_CloseAudio.Length == 0)
			{
				GameAudioManager.PlayGUIButtonBack();
			}
			if (box.GetComponent<Comps.ContainersSync>() != null)
			{
				box.GetComponent<Comps.ContainersSync>().m_Empty = flag2;
			}
		}
		GameManager.GetPlayerManagerComponent().MaybeRevealPolaroidDiscoveryOnClose();
		GameCompat.Panel<Panel_Container>().Enable(enable: false);
	}

	public static void InitAudio()
	{
		Camera mainCamera = GameManager.GetMainCamera();
		if ((bool)mainCamera && (bool)mainCamera.GetComponent<AudioListener>())
		{
			mainCamera.gameObject.AddComponent<AudioListener>();
		}
	}

	public static void UpdateAPIStates()
	{
		if (iAmHost)
		{
			API.m_ClientState = API.SkyCoopClientState.HOST;
		}
		else if (sendMyPosition)
		{
			API.m_ClientState = API.SkyCoopClientState.CLIENT;
		}
		else
		{
			API.m_ClientState = API.SkyCoopClientState.NONE;
		}
		API.m_MyClientID = ClientUser.myId;
		if (iAmHost)
		{
			API.IsP2PServer = Server.UsingSteamWorks;
		}
		else if (sendMyPosition)
		{
			API.IsP2PServer = ConnectedSteamWorks;
		}
		API.Port = Server.Port;
	}

	public static void BuildSleepScreen()
	{
		if (SleepingButtons == null)
		{
			SleepingButtons = GameCompat.Panel<Panel_Rest>().gameObject.transform.GetChild(3).gameObject;
			WaitForSleepLable = UnityEngine.Object.Instantiate(SleepingButtons.transform.GetChild(2).GetChild(1).gameObject, GameCompat.Panel<Panel_Rest>().gameObject.transform);
			UnityEngine.Object.Destroy(WaitForSleepLable.GetComponent<UILocalize>());
			WaitForSleepLable.GetComponent<UILabel>().text = "WAITING FOR OTHER PLAYERS TO SLEEP";
			WaitForSleepLable.SetActive(value: false);
		}
		if (new_button == null)
		{
			new_button = UnityEngine.Object.Instantiate(GameCompat.Panel<Panel_Rest>().m_SleepButton, GameCompat.Panel<Panel_Rest>().m_SleepButton.transform.parent);
			if (new_button2 == null)
			{
				new_button.transform.position = new Vector3(0f, -0.59f, 0f);
			}
			else
			{
				new_button.transform.position = new Vector3(-5f / 18f, -0.59f, 0f);
			}
			new_button.name = "WaitForEveryoneButton";
			GameObject gameObject = new_button.transform.GetChild(0).GetChild(0).gameObject;
			UnityEngine.Object.Destroy(gameObject.GetComponent<UILocalize>());
			gameObject.GetComponent<UILabel>().text = "WAIT FOR EVERYONE";
			new_button.GetComponent<GenericButtonMouseSpawner>().onClick = null;
			new_button.transform.GetChild(0).gameObject.GetComponent<UIButton>().onClick = null;
		}
		SleepRendezvousFix.Bind(GameCompat.Panel<Panel_Rest>());
	}

	public static void UpdateSleepScreen()
	{
		SleepRendezvousFix.Tick(GameCompat.Panel<Panel_Rest>());
	}

	public static void SelectFlairSlot(int SlotIndex)
	{
		TargetFlairSlot = SlotIndex;
		CustomizeUiPanel("Flairs");
	}

	public static void EquipFlair(int FlairIndex)
	{
		Supporters.EquipFlair(TargetFlairSlot, FlairIndex);
		CustomizeUiPanel("Main");
	}

	public static void AddFlairToList(int ID, Transform Content)
	{
		GameObject gameObject = SpawnModAsset(BundleAsset<GameObject>("MP_FlairGrid"), Content);
		Sprite overrideSprite = Sprite.Create(BundleAsset<Texture2D>("FlairIcon" + ID), new Rect(0f, 0f, 128f, 128f), new Vector2(0f, 0f));
		gameObject.transform.GetChild(1).gameObject.GetComponent<Image>().overrideSprite = overrideSprite;
		System.Action action = delegate
		{
			EquipFlair(ID);
		};
		gameObject.transform.GetChild(2).gameObject.GetComponent<Button>().onClick.AddListener(action);
		MelonLogger.Msg("Add flair to the list " + ID);
	}

	public static void CreateFlairsList()
	{
		Transform customizeUiScrollContentRoot = CustomizeUiScrollContentRoot;
		if (customizeUiScrollContentRoot.childCount != 0)
		{
			return;
		}
		AddFlairToList(-1, customizeUiScrollContentRoot);
		for (int i = 0; i < Supporters.FlairsIDs.Count; i++)
		{
			if (Supporters.AvailableBenefits.m_Flairs.Contains(i) || Supporters.IsFlairForEveryone(i))
			{
				AddFlairToList(i, customizeUiScrollContentRoot);
			}
		}
	}

	public static void CustomizeUiPanel(string Panel)
	{
		switch (Panel)
		{
		case "Main":
			TargetFlairSlot = -1;
			CustomizeUi.SetActive(value: true);
			CustomizeUiTabLable.text = "Customization";
			CustomizeUi.transform.GetChild(1).gameObject.SetActive(value: true);
			CustomizeUi.transform.GetChild(2).gameObject.SetActive(value: false);
			if (Supporters.IsLoaded())
			{
				Supporters.EnsureFlairSlots();
				Transform slots = CustomizeUi.transform.GetChild(1);
				for (int i = 1; i <= Supporters.FlairSpots && i < slots.childCount; i++)
				{
					Texture2D icon = BundleAsset<Texture2D>("FlairIcon" + Supporters.ConfiguratedBenefits.m_Flairs[i - 1]);
					slots.GetChild(i).GetChild(1).gameObject.GetComponent<Image>().overrideSprite = (icon == null) ? null : Sprite.Create(icon, new Rect(0f, 0f, 128f, 128f), new Vector2(0f, 0f));
				}
			}
			break;
		case "Flairs":
			CustomizeUi.SetActive(value: true);
			CustomizeUiTabLable.text = "Flair Slot #" + TargetFlairSlot;
			CustomizeUi.transform.GetChild(1).gameObject.SetActive(value: false);
			CustomizeUi.transform.GetChild(2).gameObject.SetActive(value: true);
			CreateFlairsList();
			break;
		case "Close":
			CustomizeUi.SetActive(value: false);
			break;
		}
	}

	public static void BuildCanvasUIs()
	{
		if (string.IsNullOrEmpty(level_name) || level_name == "Empty" || level_name == "Boot" || LoadedBundle == null || CanvasUisReady || Time.realtimeSinceStartup < CanvasUiRetryAt)
		{
			return;
		}
		CanvasUiRetryAt = Time.realtimeSinceStartup + 5f;
		GameObject gameObject = new GameObject("SkyCoopCanvas");
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		UiCanvas = gameObject.AddComponent<Canvas>();
		UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
		UiCanvas.sortingOrder = 100;
		CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		gameObject.AddComponent<GraphicRaycaster>();
		if (EventSystem.current == null)
		{
			GameObject gameObject2 = new GameObject("SkyCoopEventSystem");
			UnityEngine.Object.DontDestroyOnLoad(gameObject2);
			gameObject2.AddComponent<EventSystem>();
			gameObject2.AddComponent<StandaloneInputModule>();
		}
		try
		{
			MelonLogger.Msg("[UI] Created independent Canvas");
			ChatObject = SpawnModAsset(BundleAsset<GameObject>("MP_Chat"), UiCanvas.transform);
			chatScroller = ChatObject.transform.GetChild(1).GetComponent<ScrollRect>();
			chatInput = ChatObject.transform.GetChild(1).gameObject.GetComponent<InputField>();
			chatPanel = ChatObject.transform.GetChild(0).GetChild(0).GetChild(0)
				.gameObject;
			ChatObject.SetActive(value: false);
			chatInput.gameObject.SetActive(value: false);
			MelonLogger.Msg("[UI] Chat object created!");
			StatusObject = SpawnModAsset(BundleAsset<GameObject>("MP_Status"), UiCanvas.transform);
			StatusPanel = StatusObject.transform.GetChild(0).GetChild(0).GetChild(0)
				.gameObject;
			StatusObject.SetActive(value: false);
			MelonLogger.Msg("[UI] Status object created!");
			MicrophoneIdicator = SpawnModAsset(BundleAsset<GameObject>("MP_VoiceChat"), UiCanvas.transform);
			if (MicrophoneIdicator != null)
			{
				MelonLogger.Msg("[UI] Microphone Indicator created!");
				MicrophoneIdicator.SetActive(value: true);
				Image component = MicrophoneIdicator.GetComponent<Image>();
				component.color = new Color(component.color.r, component.color.g, component.color.b, 0f);
			}
			LobbyUI = SpawnModAsset(BundleAsset<GameObject>("MP_Lobby"), UiCanvas.transform);
			if (LobbyUI != null)
			{
				MelonLogger.Msg("[UI] Lobby panel created!");
				if (LobbyUI != null)
				{
					LobbyUI.SetActive(value: false);
				}
			}
			ServerBrowser = SpawnModAsset(BundleAsset<GameObject>("MP_ServerBrowser"), UiCanvas.transform);
			if (ServerBrowser != null)
			{
				ServerBrowser.SetActive(value: false);
			}
			RadioIdicator = SpawnModAsset(BundleAsset<GameObject>("MP_VoiceChatRadio"), UiCanvas.transform);
			if (RadioIdicator != null)
			{
				MelonLogger.Msg("[UI] Radio Indicator created!");
				RadioIdicator.SetActive(value: true);
				Image component2 = RadioIdicator.GetComponent<Image>();
				component2.color = new Color(component2.color.r, component2.color.g, component2.color.b, 0f);
				Text component3 = RadioIdicator.transform.GetChild(0).gameObject.GetComponent<Text>();
				component3.color = new Color(component3.color.r, component3.color.g, component3.color.b, 0f);
			}
			EmoteWheel = SpawnModAsset(BundleAsset<GameObject>("MP_EmoteWheel"), UiCanvas.transform);
			if (EmoteWheel != null)
			{
				EmoteWheel.SetActive(value: false);
				for (int i = 0; i <= 7; i++)
				{
					int AnimID = i;
					Button component4 = EmoteWheel.transform.GetChild(i).GetChild(0).gameObject.GetComponent<Button>();
					System.Action action = delegate
					{
						PerformEmote(AnimID);
					};
					component4.onClick.AddListener(action);
				}
			}
			NewFlairNotification = SpawnModAsset(BundleAsset<GameObject>("MP_NewFlair"), UiCanvas.transform);
			if (NewFlairNotification != null)
			{
				NewFlairNotification.SetActive(value: false);
				System.Action action2 = delegate
				{
					ShowNotiy();
				};
				NewFlairNotification.GetComponent<Button>().onClick.AddListener(action2);
			}
			CustomizeUi = SpawnModAsset(BundleAsset<GameObject>("MP_Customization"), UiCanvas.transform);
			if (CustomizeUi != null)
			{
				CustomizeUi.SetActive(value: false);
				CustomizeUiTabLable = CustomizeUi.transform.GetChild(0).gameObject.GetComponent<Text>();
				CustomizeUiScrollContentRoot = CustomizeUi.transform.GetChild(2).GetChild(0).GetChild(0);
				Transform child = CustomizeUi.transform.GetChild(1);
				for (int num = 1; num <= 4; num++)
				{
					int INdex = num;
					System.Action action3 = delegate
					{
						SelectFlairSlot(INdex);
					};
					child.GetChild(num).GetChild(2).gameObject.GetComponent<Button>().onClick.AddListener(action3);
				}
			}
			ExpeditionEditorUI = SpawnModAsset(BundleAsset<GameObject>("MP_ExpeditionEditor"), UiCanvas.transform);
			if (ExpeditionEditorUI != null)
			{
				ExpeditionEditorUI.SetActive(value: false);
				System.Action action4 = delegate
				{
					ExpeditionEditor.AutoSelectRegion();
				};
				ExpeditionEditorUI.transform.GetChild(2).GetComponent<Button>().onClick.AddListener(action4);
				System.Action action5 = delegate
				{
					ExpeditionEditor.AutoSelectScene();
				};
				ExpeditionEditorUI.transform.GetChild(5).GetComponent<Button>().onClick.AddListener(action5);
				System.Action action6 = delegate
				{
					ExpeditionEditor.AutoPositionSelect();
				};
				ExpeditionEditorUI.transform.GetChild(8).GetComponent<Button>().onClick.AddListener(action6);
				System.Action action7 = delegate
				{
					ExpeditionEditor.ToggleContainersList();
				};
				ExpeditionEditorUI.transform.GetChild(12).GetComponent<Button>().onClick.AddListener(action7);
				System.Action action8 = delegate
				{
					ExpeditionEditor.ToggleGearsList();
				};
				ExpeditionEditorUI.transform.GetChild(11).GetComponent<Button>().onClick.AddListener(action8);
				System.Action action9 = delegate
				{
					ExpeditionEditor.RemoveGearVariant();
				};
				ExpeditionEditorUI.transform.GetChild(14).GetChild(5).gameObject.GetComponent<Button>().onClick.AddListener(action9);
				System.Action action10 = delegate
				{
					ExpeditionEditor.SaveExpeditionTemplate();
				};
				ExpeditionEditorUI.transform.GetChild(15).gameObject.GetComponent<Button>().onClick.AddListener(action10);
				System.Action action11 = delegate
				{
					ExpeditionEditor.BackToSelect(ExpeditionEditorUI.transform.GetChild(1).GetComponent<Dropdown>().m_Value - Shared.GameRegionNegativeOffset);
				};
				ExpeditionEditorUI.transform.GetChild(16).gameObject.GetComponent<Button>().onClick.AddListener(action11);
				System.Action action12 = delegate
				{
					ExpeditionEditor.ToggleExtraPanel();
				};
				ExpeditionEditorUI.transform.GetChild(14).GetChild(6).gameObject.GetComponent<Button>().onClick.AddListener(action12);
				System.Action action13 = delegate
				{
					ExpeditionEditor.SetObjectiveGear();
				};
				ExpeditionEditorUI.transform.GetChild(14).GetChild(7).GetChild(3)
					.GetComponent<Button>()
					.onClick.AddListener(action13);
				System.Action action14 = delegate
				{
					ExpeditionEditor.ToggleHarvestablesList();
				};
				ExpeditionEditorUI.transform.GetChild(25).GetComponent<Button>().onClick.AddListener(action14);
				System.Action action15 = delegate
				{
					ExpeditionEditor.ToggleBreakdownsList();
				};
				ExpeditionEditorUI.transform.GetChild(26).GetComponent<Button>().onClick.AddListener(action15);
				System.Action action16 = delegate
				{
					ExpeditionEditor.ToggleObjectsList();
				};
				ExpeditionEditorUI.transform.GetChild(30).GetComponent<Button>().onClick.AddListener(action16);
				System.Action action17 = delegate
				{
					ExpeditionEditor.ToggleObjectSettingsPanel();
				};
				ExpeditionEditorUI.transform.GetChild(31).GetChild(3).GetComponent<Button>()
					.onClick.AddListener(action17);
				System.Action action18 = delegate
				{
					ExpeditionEditor.AddObject();
				};
				ExpeditionEditorUI.transform.GetChild(31).GetChild(6).GetComponent<Button>()
					.onClick.AddListener(action18);
				System.Action action19 = delegate
				{
					ExpeditionEditor.VisualizeObjects();
				};
				ExpeditionEditorUI.transform.GetChild(31).GetChild(7).GetComponent<Button>()
					.onClick.AddListener(action19);
				System.Action action20 = delegate
				{
					ExpeditionEditor.PlaceVisualizedObject();
				};
				ExpeditionEditorUI.transform.GetChild(31).GetChild(8).GetComponent<Button>()
					.onClick.AddListener(action20);
				System.Action action21 = delegate
				{
					ExpeditionEditor.SetContainerContent();
				};
				ExpeditionEditorUI.transform.GetChild(31).GetChild(4).GetChild(2)
					.GetComponent<Button>()
					.onClick.AddListener(action21);
				System.Action action22 = delegate
				{
					ExpeditionEditor.ApplyObjectSettings();
				};
				ExpeditionEditorUI.transform.GetChild(31).GetChild(9).GetChild(15)
					.gameObject.GetComponent<Button>().onClick.AddListener(action22);
				Dropdown component5 = ExpeditionEditorUI.transform.GetChild(3).GetComponent<Dropdown>();
				component5.ClearOptions();
				component5.options.Add(new Dropdown.OptionData("Scene"));
				component5.options.Add(new Dropdown.OptionData("Zone"));
				component5.options.Add(new Dropdown.OptionData("Collect"));
				component5.options.Add(new Dropdown.OptionData("Flaregun"));
				component5.options.Add(new Dropdown.OptionData("Charcoal"));
				component5.options.Add(new Dropdown.OptionData("Stay"));
				component5.options.Add(new Dropdown.OptionData("Crashsit"));
				component5.options.Add(new Dropdown.OptionData("AutoComplete"));
				component5.options.Add(new Dropdown.OptionData("Interact"));
				Dropdown component6 = ExpeditionEditorUI.transform.GetChild(1).GetComponent<Dropdown>();
				component6.ClearOptions();
				for (int num2 = -Shared.GameRegionNegativeOffset; num2 <= Shared.GameRegionPositiveOffset; num2++)
				{
					component6.options.Add(new Dropdown.OptionData(ExpeditionBuilder.GetRegionString(num2)));
				}
			}
			ExpeditionEditorSelectUI = SpawnModAsset(BundleAsset<GameObject>("MP_ExpeditionSelect"), UiCanvas.transform);
			if (ExpeditionEditorSelectUI != null)
			{
				ExpeditionEditorSelectUI.SetActive(value: false);
				System.Action action23 = delegate
				{
					ExpeditionEditor.LoadExpedition(new ExpeditionBuilder.ExpeditionTaskTemplate());
				};
				ExpeditionEditorSelectUI.transform.GetChild(2).gameObject.GetComponent<Button>().onClick.AddListener(action23);
				System.Action action24 = delegate
				{
					ExpeditionEditor.RefreshExpeditionsList();
				};
				ExpeditionEditorSelectUI.transform.GetChild(3).gameObject.GetComponent<Button>().onClick.AddListener(action24);
				System.Action action25 = delegate
				{
					ExpeditionEditor.BackToSelect(6);
				};
				ExpeditionEditorSelectUI.transform.GetChild(4).GetComponent<Button>().onClick.AddListener(action25);
			}
			if (TFHUD.Enabled)
			{
				TFHUD.Init(LoadedBundle, UiCanvas.transform);
			}
			CanvasUisReady = ChatObject != null && chatInput != null && LobbyUI != null && ServerBrowser != null;
			MelonLogger.Msg("[UI] Canvas UI ready=" + CanvasUisReady);
		}
		catch (System.Exception ex)
		{
			UnityEngine.Object.Destroy(gameObject);
			UiCanvas = null;
			CanvasUisReady = false;
			MelonLogger.Error("[UI] Could not initialize co-op UI: " + ex);
		}
	}

	public static bool IsHoldingRadio()
	{
		if ((bool)GameManager.m_PlayerManager && (bool)GameManager.GetPlayerManagerComponent().m_ItemInHands && GameManager.GetPlayerManagerComponent().m_ItemInHands.GetGearName() == "GEAR_HandheldShortwave")
		{
			return true;
		}
		return false;
	}

	public static void UpdateCanvasUis()
	{
		if (!ConsoleIsOpen() && !Cursor.visible && chatInput != null)
		{
			if (InputManager.GetKeyDown(InputManager.m_CurrentContext, KeyCode.Return))
			{
				if (!chatInput.gameObject.activeSelf)
				{
					if (ChatObject != null)
					{
						ChatObject.SetActive(value: true);
					}
					chatInput.gameObject.SetActive(value: true);
					chatInput.ActivateInputField();
					HideChatTimer = 5;
					if (GameManager.GetPlayerManagerComponent().GetControlMode() == PlayerControlMode.Normal)
					{
						GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Locked);
					}
				}
				else
				{
					if (chatInput.text != "")
					{
						Shared.SendMessageToChat(new DataStr.MultiplayerChatMessage
						{
							m_By = MyChatName,
							m_Type = 1,
							m_Message = chatInput.text,
							m_Global = IsHoldingRadio()
						});
						chatInput.text = "";
					}
					chatInput.gameObject.SetActive(value: false);
					if (GameManager.GetPlayerManagerComponent().GetControlMode() == PlayerControlMode.Locked)
					{
						GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Normal);
					}
				}
			}
			if (GameCompat.Panel<Panel_PauseMenu>().isActiveAndEnabled && !Shared.IsMainMenuScene(level_name))
			{
				if (InOnline())
				{
					if (MyLobby == "")
					{
						if (StatusObject != null)
						{
							StatusObject.SetActive(value: true);
						}
					}
					else if (LobbyUI != null)
					{
						LobbyUI.SetActive(value: true);
					}
				}
				else
				{
					if (StatusObject != null)
					{
						StatusObject.SetActive(value: false);
					}
					if (LobbyUI != null)
					{
						LobbyUI.SetActive(value: false);
					}
				}
			}
			else
			{
				if (!Shared.IsMainMenuScene(level_name) && LobbyUI != null)
				{
					LobbyUI.SetActive(value: false);
				}
				if (StatusObject != null)
				{
					StatusObject.SetActive(value: false);
				}
			}
		}
		if (LobbyUI != null)
		{
			LobbyUI.transform.GetChild(3).gameObject.SetActive(value: false);
		}
		if (UIHostMenu != null)
		{
			GameObject gameObject = UIHostMenu.transform.GetChild(4).gameObject;
			GameObject gameObject2 = UIHostMenu.transform.GetChild(8).gameObject;
			GameObject gameObject3 = UIHostMenu.transform.GetChild(11).gameObject;
			if (gameObject.GetComponent<Toggle>().isOn)
			{
				gameObject2.SetActive(value: false);
				gameObject3.SetActive(value: true);
			}
			else
			{
				gameObject2.SetActive(value: true);
				gameObject3.SetActive(value: false);
			}
		}
		if (ExpeditionEditorUI != null)
		{
			int value = ExpeditionEditorUI.transform.GetChild(3).GetComponent<Dropdown>().m_Value;
			ExpeditionEditorUI.transform.GetChild(6).gameObject.SetActive(value != 0);
			ExpeditionEditorUI.transform.GetChild(7).gameObject.SetActive(value != 0);
			ExpeditionEditorUI.transform.GetChild(8).gameObject.SetActive(value != 0);
			ExpeditionEditorUI.transform.GetChild(6).GetChild(0).gameObject.GetComponent<Text>().text = ExpeditionEditorUI.transform.GetChild(6).gameObject.GetComponent<Slider>().m_Value.ToString();
			ExpeditionEditorUI.transform.GetChild(20).gameObject.SetActive(value == 2);
			ExpeditionEditorUI.transform.GetChild(7).gameObject.GetComponent<Text>().text = ExpeditionEditor.m_Center.x.ToString(CultureInfo.InvariantCulture) + ", " + ExpeditionEditor.m_Center.y.ToString(CultureInfo.InvariantCulture) + ", " + ExpeditionEditor.m_Center.z.ToString(CultureInfo.InvariantCulture);
			ExpeditionEditorUI.transform.GetChild(14).GetChild(3).GetChild(0)
				.gameObject.GetComponent<Text>().text = ExpeditionEditorUI.transform.GetChild(14).GetChild(3).gameObject.GetComponent<Slider>().m_Value * 100f + "%";
			if (ExpeditionEditor.m_LastChance != ExpeditionEditorUI.transform.GetChild(14).GetChild(3).gameObject.GetComponent<Slider>().m_Value)
			{
				ExpeditionEditor.m_LastChance = ExpeditionEditorUI.transform.GetChild(14).GetChild(3).gameObject.GetComponent<Slider>().m_Value;
				ExpeditionEditor.ChangeChance();
			}
			if (ExpeditionEditor.m_LastRadious != ExpeditionEditorUI.transform.GetChild(6).gameObject.GetComponent<Slider>().m_Value)
			{
				ExpeditionEditor.m_LastRadious = ExpeditionEditorUI.transform.GetChild(6).gameObject.GetComponent<Slider>().m_Value;
				ExpeditionEditor.ExpeditionRadiousChanged();
			}
			ExpeditionEditorUI.transform.GetChild(14).GetChild(2).gameObject.GetComponent<Text>().text = "GUID: " + ExpeditionEditor.m_LastSpawnerGUID;
			if (ExpeditionEditor.m_LastType != ExpeditionEditorUI.transform.GetChild(3).gameObject.GetComponent<Dropdown>().m_Value)
			{
				ExpeditionEditor.m_LastType = ExpeditionEditorUI.transform.GetChild(3).gameObject.GetComponent<Dropdown>().m_Value;
				ExpeditionEditor.ExpeditionTypeChanged();
			}
			GameObject gameObject4 = ExpeditionEditorUI.transform.GetChild(29).gameObject;
			if (value == 5 || value == 7)
			{
				gameObject4.SetActive(value: true);
				if (value == 5)
				{
					gameObject4.transform.GetChild(2).gameObject.GetComponent<Text>().text = "Stay(Seconds)";
				}
				else
				{
					gameObject4.transform.GetChild(2).gameObject.GetComponent<Text>().text = "Wait(Seconds)";
				}
			}
			else
			{
				gameObject4.SetActive(value: false);
			}
			if (ExpeditionEditor.LastOnlyShowFirstTasksState != ExpeditionEditorSelectUI.transform.GetChild(5).gameObject.GetComponent<Toggle>().isOn)
			{
				ExpeditionEditor.LastOnlyShowFirstTasksState = ExpeditionEditorSelectUI.transform.GetChild(5).gameObject.GetComponent<Toggle>().isOn;
				ExpeditionEditor.RefreshExpeditionsList(ExpeditionEditor.LastSelectedRegion);
			}
		}
		TFHUD.Update();
	}

	public static void DebugCrap()
	{
		if (InputManager.GetReloadPressed(InputManager.m_CurrentContext) && ExpeditionEditorUI != null && ExpeditionEditorUI.activeSelf && (bool)GameManager.m_PlayerManager && (bool)GameManager.GetPlayerManagerComponent().GetInteractiveObjectUnderCrosshair() && (bool)GameManager.GetPlayerManagerComponent().GetInteractiveObjectUnderCrosshair().GetComponent<Comps.DroppedGearDummy>())
		{
			Comps.DroppedGearDummy component = GameManager.GetPlayerManagerComponent().GetInteractiveObjectUnderCrosshair().GetComponent<Comps.DroppedGearDummy>();
			ExpeditionEditor.AddGear(component.m_Extra.m_GearName, component.gameObject.transform.position, component.gameObject.transform.rotation, component.m_Extra.m_PhotoGUID);
		}
		if (InputManager.GetKeyDown(InputManager.m_CurrentContext, KeyCode.H) && ExpeditionEditorUI != null && ExpeditionEditorUI.activeSelf)
		{
			if ((bool)GameManager.m_PlayerManager && (bool)GameManager.GetPlayerManagerComponent().GetInteractiveObjectUnderCrosshair())
			{
				if ((bool)GameManager.GetPlayerManagerComponent().GetInteractiveObjectUnderCrosshair().GetComponent<Comps.DroppedGearDummy>())
				{
					ExpeditionEditor.AddGearVariant(GameManager.GetPlayerManagerComponent().GetInteractiveObjectUnderCrosshair().GetComponent<Comps.DroppedGearDummy>()
						.m_Extra.m_GearName);
				}
				else if (ExpeditionEditor.m_LastObjectGUID != "")
				{
					ExpeditionEditor.PlaceVisualizedObject();
				}
			}
			else if (ExpeditionEditor.m_LastObjectGUID != "")
			{
				ExpeditionEditor.PlaceVisualizedObject();
			}
		}
		if (!DebugGUI)
		{
			return;
		}
		Transform transform = GameManager.GetMainCamera().transform;
		if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out var hitInfo, 50f))
		{
			if ((bool)hitInfo.collider.gameObject.GetComponent<BaseAi>() && (bool)hitInfo.collider.gameObject.GetComponent<ObjectGuid>())
			{
				DebugAnimalGUID = hitInfo.collider.gameObject.GetComponent<ObjectGuid>().Get();
				DebugAnimalGUIDLast = DebugAnimalGUID;
				DebugLastAnimal = hitInfo.collider.gameObject;
			}
		}
		else
		{
			DebugAnimalGUID = "";
		}
		if (ALWAYS_FUCKING_CURSOR_ON)
		{
			InputManager.m_CursorState = InputManager.CursorState.Show;
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
		}
		if (!DebugBind)
		{
			return;
		}
		if (InputManager.GetKeyDown(InputManager.m_CurrentContext, KeyCode.B) && Physics.Raycast(GameManager.GetMainCamera().transform.position, GameManager.GetMainCamera().transform.forward, out var hitInfo2, 50f))
		{
			Vector3 position = SnappedPosition(hitInfo2.point, hitInfo2.collider.transform.position);
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject.transform.position = position;
			if (sendMyPosition)
			{
				using Packet packet = new Packet(4);
				packet.Write(gameObject.transform.position);
				SendUDPData(packet);
			}
			if (iAmHost)
			{
				using (new Packet(4))
				{
					ServerSend.BLOCK(0, gameObject.transform.position, toEveryOne: true);
				}
			}
		}
		if (InputManager.GetKeyDown(InputManager.m_CurrentContext, KeyCode.K))
		{
			SimRevive();
		}
		ALWAYS_FUCKING_CURSOR_ON = KeyboardUtilities.InputManager.GetKey(KeyCode.L);
		if (InputManager.GetKeyDown(InputManager.m_CurrentContext, KeyCode.LeftAlt))
		{
			GameManager.GetVpFPSPlayer().Controller.Jump();
		}
	}

	public static void UpdateMyEquipment()
	{
		FlashlightSync.PollHeld();
		string text = "";
		MyHasRifle = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_Rifle");
		MyArrows = GameManager.GetInventoryComponent().NumGearInInventory("GEAR_Arrow");
		MyFlares = GameManager.GetInventoryComponent().NumGearInInventory("GEAR_FlareA");
		MyBlueFlares = GameManager.GetInventoryComponent().NumGearInInventory("GEAR_FlareBlue");
		MyHasRevolver = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_Revolver");
		MyHasMedkit = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_MedicalSupplies_hangar");
		bool num = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_Hatchet");
		bool flag = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_FireAxe");
		bool flag2 = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_HatchetImprovised");
		if (num || flag || flag2)
		{
			MyHasAxe = true;
		}
		else
		{
			MyHasAxe = false;
		}
		text = MyHasRifle.ToString() + MyArrows + MyFlares + MyBlueFlares + MyHasRevolver + MyHasMedkit + MyHasAxe;
		if (PreviousEquipmentCheck != text)
		{
			PreviousEquipmentCheck = text;
			SendMyEQ();
		}
		if (MyLightSourceName != MyLastLightSourceName)
		{
			if (SkipItemEvent > 0)
			{
				SkipItemEvent--;
			}
			else
			{
				MyLastLightSourceName = MyLightSourceName;
				if (playersData[0].m_Mimic && players[0] != null)
				{
					playersData[0].m_PlayerEquipmentData.m_HoldingItem = MyLightSourceName;
				}
				MelonLogger.Msg("Holding item: " + MyLastLightSourceName);
				if (sendMyPosition)
				{
					using Packet packet = new Packet(10);
					packet.Write(MyLightSourceName);
					SendUDPData(packet);
				}
				if (iAmHost)
				{
					using (new Packet(10))
					{
						ServerSend.LIGHTSOURCENAME(0, MyLightSourceName, toEveryOne: true);
					}
				}
			}
		}
		if (MyLightSource != MyLastLightSource)
		{
			MelonLogger.Msg("Lightchanged " + MyLightSource);
			MyLastLightSource = MyLightSource;
			if (sendMyPosition)
			{
				using Packet packet2 = new Packet(9);
				packet2.Write(MyLightSource);
				SendUDPData(packet2);
			}
			if (iAmHost)
			{
				using (new Packet(9))
				{
					ServerSend.LIGHTSOURCE(0, MyLightSource, toEveryOne: true);
				}
			}
		}
		if (!GameCompat.Panel<Panel_Map>().IsEnabled())
		{
			if (GameManager.GetPlayerManagerComponent().m_ItemInHands != null)
			{
				MyLightSourceName = GameManager.GetPlayerManagerComponent().m_ItemInHands.name;
			}
			else if (ModdedHandsBook == null)
			{
				MyLightSourceName = "";
			}
			else if (!ModdedHandsBook.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Offscreen"))
			{
				MyLightSourceName = "Book";
			}
			else
			{
				MyLightSourceName = "";
			}
		}
		else
		{
			MyLightSourceName = "Map";
		}
	}

	public static void GiveBorrowedItem(string GearName, int Borrower)
	{
		if (!GameManager.GetInventoryComponent())
		{
			return;
		}
		for (int i = 0; i < GameManager.GetInventoryComponent().m_Items.Capacity; i++)
		{
			GearItem gearItem = GameManager.GetInventoryComponent().m_Items[i];
			if (gearItem.GetGearName() == GearName && gearItem != GameManager.GetPlayerManagerComponent().m_ItemInHands)
			{
				SendGivingItem(gearItem.SerializeToString(), gearItem, Borrower, Borrow: true);
				break;
			}
		}
	}

	public static void SendGivingItem(string saveProxyData, GearItem _gear, int GiveTo, bool Borrow)
	{
		DataStr.GearItemDataPacket gearItemDataPacket = new DataStr.GearItemDataPacket();
		gearItemDataPacket.m_GearName = _gear.GetGearName();
		gearItemDataPacket.m_SendedTo = GiveTo;
		bool flag = false;
		float liters = 0f;
		WaterSupply waterSupply = null;
		if (LastSelectedGearName == "GEAR_SCNote" || LastSelectedGearName == "GEAR_SCPhoto" || LastSelectedGearName == "GEAR_SCMapPiece")
		{
			return;
		}
		if (LastSelectedGearName == "GEAR_WaterSupplyPotable" || LastSelectedGearName == "GEAR_WaterSupplyNotPotable")
		{
			flag = true;
			if (LastSelectedGearName == "GEAR_WaterSupplyPotable")
			{
				waterSupply = GameManager.GetInventoryComponent().m_WaterSupplyPotable.m_WaterSupply;
			}
			if (LastSelectedGearName == "GEAR_WaterSupplyNotPotable")
			{
				waterSupply = GameManager.GetInventoryComponent().m_WaterSupplyNotPotable.m_WaterSupply;
			}
			float num = waterSupply.m_VolumeInLiters.ToLiters();
			liters = (gearItemDataPacket.m_Water = ((!(num >= 0.5f)) ? num : 0.5f));
		}
		else
		{
			gearItemDataPacket.m_DataProxy = saveProxyData;
		}
		if (flag)
		{
			if (iAmHost)
			{
				using (new Packet(7))
				{
					ServerSend.GOTITEM(GiveItemTo, gearItemDataPacket);
				}
			}
			if (sendMyPosition)
			{
				using Packet packet = new Packet(7);
				packet.Write(gearItemDataPacket);
				SendUDPData(packet);
			}
		}
		else
		{
			byte[] bytes = Encoding.UTF8.GetBytes(saveProxyData);
			if (bytes.Length > 500)
			{
				System.Collections.Generic.List<byte> list = new System.Collections.Generic.List<byte>();
				list.AddRange(bytes);
				while (list.Count >= 500)
				{
					byte[] bytes2 = list.GetRange(0, 499).ToArray();
					list.RemoveRange(0, 499);
					string str = Encoding.UTF8.GetString(bytes2);
					DataStr.SlicedJsonData slicedJsonData = new DataStr.SlicedJsonData();
					slicedJsonData.m_GearName = _gear.GetGearName();
					slicedJsonData.m_SendTo = GiveItemTo;
					slicedJsonData.m_Hash = Shared.StableHash(saveProxyData);
					slicedJsonData.m_Str = str;
					if (list.Count != 0)
					{
						slicedJsonData.m_Last = false;
					}
					else
					{
						slicedJsonData.m_Last = true;
					}
					if (iAmHost)
					{
						ServerSend.GOTITEMSLICE(GiveItemTo, slicedJsonData);
					}
					if (sendMyPosition)
					{
						using Packet packet2 = new Packet(80);
						packet2.Write(slicedJsonData);
						SendUDPData(packet2);
					}
				}
				if (list.Count < 500 && list.Count != 0)
				{
					byte[] bytes3 = list.GetRange(0, list.Count).ToArray();
					list.RemoveRange(0, list.Count);
					string str2 = Encoding.UTF8.GetString(bytes3);
					DataStr.SlicedJsonData slicedJsonData2 = new DataStr.SlicedJsonData();
					slicedJsonData2.m_GearName = _gear.GetGearName();
					slicedJsonData2.m_SendTo = GiveItemTo;
					slicedJsonData2.m_Hash = Shared.StableHash(saveProxyData);
					slicedJsonData2.m_Str = str2;
					slicedJsonData2.m_Last = true;
					if (iAmHost)
					{
						ServerSend.GOTITEMSLICE(GiveItemTo, slicedJsonData2);
					}
					if (sendMyPosition)
					{
						using Packet packet3 = new Packet(80);
						packet3.Write(slicedJsonData2);
						SendUDPData(packet3);
					}
				}
			}
			else
			{
				DataStr.SlicedJsonData slicedJsonData3 = new DataStr.SlicedJsonData();
				slicedJsonData3.m_GearName = _gear.GetGearName();
				slicedJsonData3.m_SendTo = GiveItemTo;
				slicedJsonData3.m_Hash = Shared.StableHash(saveProxyData);
				slicedJsonData3.m_Str = saveProxyData;
				slicedJsonData3.m_Last = true;
				if (iAmHost)
				{
					ServerSend.GOTITEMSLICE(GiveItemTo, slicedJsonData3);
				}
				if (sendMyPosition)
				{
					using Packet packet4 = new Packet(80);
					packet4.Write(slicedJsonData3);
					SendUDPData(packet4);
				}
			}
		}
		if (flag)
		{
			string text = "half liter of " + _gear.DisplayName;
			if (waterSupply.m_VolumeInLiters == GameCompat.Liters(liters))
			{
				waterSupply.m_VolumeInLiters = GameCompat.Liters(0f);
			}
			else
			{
				waterSupply.m_VolumeInLiters -= GameCompat.Liters(liters);
				text = liters + " liter of " + _gear.DisplayName;
			}
			if (!Borrow)
			{
				HUDMessage.AddMessage("You gave " + text + " to " + playersData[GiveItemTo].m_Name);
			}
		}
		else
		{
			if (!Borrow)
			{
				HUDMessage.AddMessage("You gave " + _gear.DisplayName + " to " + playersData[GiveItemTo].m_Name);
			}
			GameManager.GetInventoryComponent().RemoveUnits(_gear, 1);
		}
		if (!Borrow)
		{
			MelonLogger.Msg("You gave " + LastSelectedGearName + " to " + playersData[GiveItemTo].m_Name);
		}
		GameCompat.Panel<Panel_Inventory>().m_IsDirty = true;
		GameCompat.Panel<Panel_Inventory>().Update();
	}

	public static void ProcessGivingItem(bool SelectOnly = false)
	{
		if (!(m_InterfaceManager != null) || !(GameCompat.Panel<Panel_Inventory>() != null) || !GameCompat.Panel<Panel_Inventory>().IsEnabled())
		{
			return;
		}
		Panel_Inventory panel_Inventory = GameCompat.Panel<Panel_Inventory>();
		if (panel_Inventory.m_SelectedItemIndex != -1)
		{
			int selectedItemIndex = panel_Inventory.m_SelectedItemIndex;
			selectedItemIndex += panel_Inventory.m_FirstItemDisplayedIndex;
			_ = panel_Inventory.m_FilteredInventoryList;
			if (panel_Inventory.m_FilteredInventoryList.Count > 0)
			{
				GearItem gearItem = panel_Inventory.m_FilteredInventoryList[selectedItemIndex].m_GearItem;
				if (gearItem != null)
				{
					string gearName = gearItem.GetGearName();
					if (gearName.Contains("(Clone)"))
					{
						int startIndex = gearName.Length - 7;
						LastSelectedGearName = gearName.Remove(startIndex, 7);
					}
					else
					{
						LastSelectedGearName = gearName;
					}
					if (LastSelectedGearName != "gg")
					{
						LastSelectedGear = gearItem;
					}
				}
				else
				{
					LastSelectedGearName = "";
				}
			}
			else
			{
				LastSelectedGearName = "";
			}
		}
		if (SelectOnly || !(LastSelectedGearName != "") || !(LastSelectedGear != null) || !GameCompat.Panel<Panel_Inventory>().IsEnabled() || GiveItemTo == -1)
		{
			return;
		}
		Il2CppSystem.Collections.Generic.List<GearItemObject> items = GameManager.GetInventoryComponent().m_Items;
		GearItem gearItem2 = null;
		string saveProxyData = "";
		for (int i = 0; i < items.Count; i++)
		{
			if (items[i].m_GearItem == LastSelectedGear)
			{
				gearItem2 = items[i].m_GearItem;
				if (gearItem2.m_ClothingItem != null && gearItem2.m_ClothingItem.IsWearing())
				{
					gearItem2.m_ClothingItem.TakeOff();
				}
				if (gearItem2.m_TorchItem != null && GiveItemTo != -1 && playersData[GiveItemTo] != null && playersData[GiveItemTo].m_PlayerEquipmentData.m_HoldingItem != "")
				{
					gearItem2.m_TorchItem.Extinguish(TorchState.Extinguished);
				}
				if (GameManager.GetPlayerManagerComponent().m_ItemInHands == gearItem2)
				{
					GameManager.GetPlayerManagerComponent().UnequipItemInHandsSkipAnimation();
				}
				int units = 0;
				bool flag = false;
				if (gearItem2.m_StackableItem != null)
				{
					units = gearItem2.m_StackableItem.m_Units;
				}
				flag = gearItem2.m_InPlayerInventory;
				gearItem2.m_InPlayerInventory = false;
				if (gearItem2.m_StackableItem != null)
				{
					gearItem2.m_StackableItem.m_Units = 1;
				}
				saveProxyData = gearItem2.SerializeToString();
				if (gearItem2.m_StackableItem != null)
				{
					gearItem2.m_StackableItem.m_Units = units;
				}
				gearItem2.m_InPlayerInventory = flag;
				break;
			}
		}
		if (gearItem2 != null)
		{
			SendGivingItem(saveProxyData, gearItem2, GiveItemTo, Borrow: false);
		}
	}

	public static void UpdateVisualSyncs()
	{
		if (GameManager.GetFatigueComponent().GetHeavyBreathingState() == HeavyBreathingState.Heavy)
		{
			NowHeavyBreath = true;
		}
		else
		{
			NowHeavyBreath = false;
		}
		BloodLosts = GameManager.GetBloodLossComponent().GetAfflictionsCount();
		if (PreviousBloodLosts != BloodLosts)
		{
			PreviousBloodLosts = BloodLosts;
			if (sendMyPosition)
			{
				using Packet packet = new Packet(61);
				packet.Write(BloodLosts);
				SendUDPData(packet);
			}
			if (iAmHost)
			{
				using (new Packet(61))
				{
					ServerSend.BLOODLOSTS(0, BloodLosts, toEveryOne: true);
				}
			}
		}
		if (PreviousNowHeavyBreath == NowHeavyBreath)
		{
			return;
		}
		PreviousNowHeavyBreath = NowHeavyBreath;
		if (sendMyPosition)
		{
			using Packet packet2 = new Packet(60);
			packet2.Write(NowHeavyBreath);
			SendUDPData(packet2);
		}
		if (iAmHost)
		{
			using (new Packet(60))
			{
				ServerSend.HEAVYBREATH(0, NowHeavyBreath, toEveryOne: true);
			}
		}
	}

	public static void SyncMovement(bool InFight, Transform target)
	{
		if (!IsDead && !InFight)
		{
			if (!GameManager.GetPlayerManagerComponent().PlayerIsClimbing())
			{
				if (GameManager.GetPlayerManagerComponent().PlayerCanSprint() && GameManager.GetPlayerManagerComponent().PlayerIsSprinting())
				{
					MyAnimState = "Run";
				}
				else if (GameManager.GetPlayerManagerComponent().PlayerIsCrouched())
				{
					MyAnimState = "Ctrl";
				}
				else
				{
					MyAnimState = "Walk";
				}
			}
			else
			{
				MyAnimState = "RopeMoving";
			}
		}
		Vector3 position = target.position;
		if (GameManager.GetRestComponent().IsSleeping() && GameManager.GetRestComponent().m_Bed != null)
		{
			Bed bed = GameManager.GetRestComponent().m_Bed;
			if (bed != null && bed.m_BodyPlacementTransform != null)
			{
				position = bed.m_BodyPlacementTransform.position;
			}
		}
		if (sendMyPosition)
		{
			using Packet packet = new Packet(2);
			packet.Write(position);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ServerSend.XYZ(0, position, toEveryOne: true);
		}
	}

	public static void SyncRotation(Transform target)
	{
		Quaternion quaternion = target.rotation;
		if (GameManager.GetRestComponent().IsSleeping() && GameManager.GetRestComponent().m_Bed != null)
		{
			Bed bed = GameManager.GetRestComponent().m_Bed;
			if (bed != null)
			{
				quaternion = GetBedRotation(bed);
			}
		}
		if (sendMyPosition)
		{
			using Packet packet = new Packet(3);
			packet.Write(quaternion);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			using (new Packet(3))
			{
				ServerSend.XYZW(0, quaternion, toEveryOne: true);
			}
		}
	}

	public static void SetCurrentAnimation(bool InFight)
	{
		if (IsDead || InFight)
		{
			if (IsDead)
			{
				MyAnimState = "Knock";
			}
			else
			{
				MyAnimState = "Fight";
			}
		}
		else if (MyEmote != null)
		{
			MyAnimState = MyEmote.m_Animation;
		}
		else if (!GameManager.GetPlayerManagerComponent().PlayerIsClimbing())
		{
			if (GameManager.GetPlayerManagerComponent().PlayerIsSleeping())
			{
				MyAnimState = "Sleep";
			}
			else if (GameManager.GetPlayerInVehicle().m_InVehicle)
			{
				MyAnimState = "Sit";
			}
			else if (!GameCompat.Panel<Panel_Map>().IsEnabled())
			{
				if (GameCompat.Panel<Panel_BreakDown>().IsBreakingDown())
				{
					BreakDown breakDown = GameCompat.Panel<Panel_BreakDown>().m_BreakDown;
					if (breakDown != null && breakDown.gameObject != null)
					{
						GameObject gameObject = GameCompat.Panel<Panel_BreakDown>().m_BreakDown.gameObject;
						float y = GameManager.GetMainCamera().transform.position.y;
						if (gameObject.transform.position.y > y)
						{
							MyAnimState = "HarvestingStanding";
						}
						else
						{
							MyAnimState = "Harvesting";
						}
					}
					else
					{
						MyAnimState = "Harvesting";
					}
					return;
				}
				if (PlayerInteractionWith != null || (GameCompat.Panel<Panel_Diagnosis>() != null && GameCompat.Panel<Panel_Diagnosis>().TreatmentInProgress()))
				{
					if (GameManager.GetPlayerManagerComponent().PlayerIsCrouched())
					{
						MyAnimState = "Harvesting";
					}
					else
					{
						MyAnimState = "HarvestingStanding";
					}
					return;
				}
				if (GameCompat.Panel<Panel_BodyHarvest>() != null && GameCompat.Panel<Panel_BodyHarvest>().m_BodyHarvest != null && GameCompat.Panel<Panel_BodyHarvest>().m_BodyHarvest.gameObject != null)
				{
					MyAnimState = "Harvesting";
					return;
				}
				if ((GameCompat.Panel<Panel_SnowShelterBuild>() != null && GameCompat.Panel<Panel_SnowShelterBuild>().m_IsBuilding) || (GameCompat.Panel<Panel_SnowShelterInteract>() != null && GameCompat.Panel<Panel_SnowShelterInteract>().m_IsDismantling))
				{
					MyAnimState = "Harvesting";
					return;
				}
				if (Pathes.FakeRockCacheCallback != null)
				{
					MyAnimState = "Harvesting";
					return;
				}
				if (GameManager.GetPlayerManagerComponent().m_Harvestable != null)
				{
					if (GameManager.GetPlayerManagerComponent().PlayerIsCrouched())
					{
						MyAnimState = "Harvesting";
						return;
					}
					GameObject gameObject2 = GameManager.GetPlayerManagerComponent().m_Harvestable.gameObject;
					if (gameObject2.name.Contains("Cattail"))
					{
						MyAnimState = "HarvestingStanding";
						return;
					}
					float y2 = GameManager.GetMainCamera().transform.position.y;
					if (gameObject2.transform.position.y > y2)
					{
						MyAnimState = "HarvestingStanding";
					}
					else
					{
						MyAnimState = "Harvesting";
					}
					return;
				}
				PlayerControlMode controlMode = GameManager.GetPlayerManagerComponent().GetControlMode();
				if (GameManager.GetPlayerManagerComponent().PlayerIsCrouched())
				{
					switch (controlMode)
					{
					case PlayerControlMode.AimRevolver:
						MyAnimState = "HoldGun_Sit";
						break;
					case PlayerControlMode.StartingFire:
						MyAnimState = "Igniting";
						break;
					case PlayerControlMode.DeployRope:
					case PlayerControlMode.TakeRope:
						MyAnimState = "Harvesting";
						break;
					default:
						MyAnimState = "Ctrl";
						break;
					}
				}
				else
				{
					switch (controlMode)
					{
					case PlayerControlMode.AimRevolver:
						MyAnimState = "HoldGun";
						break;
					case PlayerControlMode.StartingFire:
						MyAnimState = "Igniting";
						break;
					case PlayerControlMode.DeployRope:
					case PlayerControlMode.TakeRope:
						MyAnimState = "HarvestingStanding";
						break;
					default:
						MyAnimState = "Idle";
						break;
					}
				}
			}
			else
			{
				MyAnimState = "Map";
			}
		}
		else
		{
			MyAnimState = "RopeIdle";
		}
	}

	public static void UpdateSceneGUID()
	{
		if (GameManager.m_SceneTransitionData != null)
		{
			level_guid = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
			if (level_guid != previous_level_guid)
			{
				previous_level_guid = level_guid;
				MelonLogger.Msg("Scene GUID " + GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent);
			}
		}
	}

	public static void SendMyAnimation()
	{
		if (MyPreviousAnimState != MyAnimState)
		{
			MyPreviousAnimState = MyAnimState;
			if (playersData[0].m_Mimic && players[0] != null)
			{
				playersData[0].m_AnimState = MyAnimState;
			}
			if (sendMyPosition)
			{
				using Packet packet = new Packet(11);
				packet.Write(MyAnimState);
				SendUDPData(packet);
			}
			if (iAmHost)
			{
				using (new Packet(11))
				{
					ServerSend.ANIMSTATE(0, MyAnimState, toEveryOne: true);
				}
			}
		}
		bool flag = false;
		if (GameManager.m_PlayerManager != null && GameManager.m_PlayerManager.m_ItemInHands != null && GameManager.m_PlayerManager.m_ItemInHands.m_GunItem != null && GameManager.m_PlayerManager.m_ItemInHands.m_GunItem.IsAiming())
		{
			flag = true;
		}
		if (MyIsAiming == flag)
		{
			return;
		}
		MyIsAiming = flag;
		if (playersData[0].m_Mimic && players[0] != null)
		{
			playersData[0].m_Aiming = MyIsAiming;
		}
		if (sendMyPosition)
		{
			using Packet packet2 = new Packet(98);
			packet2.Write(MyIsAiming);
			SendUDPData(packet2);
		}
		if (iAmHost)
		{
			ServerSend.CHANGEAIM(0, MyIsAiming, toEveryOne: true);
		}
	}

	public static Quaternion GetBedRotation(Bed bed)
	{
		GameObject gameObject = bed.gameObject;
		Quaternion result = new Quaternion(0f, 0f, 0f, 0f);
		if ((bool)gameObject)
		{
			if (gameObject.GetComponent<Bed>().m_Bedroll == null)
			{
				return Quaternion.Inverse(gameObject.GetComponent<Bed>().m_BodyPlacementTransform.rotation);
			}
			return gameObject.transform.rotation;
		}
		return result;
	}

	public static void SyncSleeping()
	{
		if (WaitForSleepLable != null && WaitForSleepLable.activeSelf)
		{
			IsSleeping = true;
		}
		else
		{
			IsSleeping = GameManager.GetPlayerManagerComponent().PlayerIsSleeping();
		}
		if (PreviousSleeping == IsSleeping)
		{
			return;
		}
		PreviousSleeping = IsSleeping;
		if (GameManager.GetRestComponent().IsSleeping() || (WaitForSleepLable != null && WaitForSleepLable.activeSelf))
		{
			MelonLogger.Msg("Going to sleep");
			MelonLogger.Msg("Skiping Cycle time " + MyCycleSkip);
			Bed bed = GameManager.GetRestComponent().m_Bed;
			if (bed != null && bed.m_BodyPlacementTransform != null)
			{
				SyncRotation(GameManager.GetPlayerObject().transform);
				SyncMovement(InFight: false, GameManager.GetPlayerObject().transform);
			}
		}
		else
		{
			MyCycleSkip = 0;
			MelonLogger.Msg("Has wakeup or cancle sleep");
			SyncRotation(GameManager.GetPlayerObject().transform);
			SyncMovement(InFight: false, GameManager.GetPlayerObject().transform);
		}
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(12))
			{
				packet.Write(MyCycleSkip);
				SendUDPData(packet);
			}
		}
	}

	public static void SendHarvestingAnimal()
	{
		if (!(PreviousHarvestingAnimal != HarvestingAnimal))
		{
			return;
		}
		PreviousHarvestingAnimal = HarvestingAnimal;
		if (sendMyPosition)
		{
			using Packet packet = new Packet(26);
			packet.Write(HarvestingAnimal);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			using (new Packet(26))
			{
				ServerSend.HARVESTINGANIMAL(0, HarvestingAnimal, toEveryOne: false);
			}
		}
	}

	public static void CancleIfSomeoneDoSame()
	{
		string text = null;
		string text2 = null;
		string text3 = null;
		string text4 = "";
		string text5 = "";
		bool flag = false;
		Container container = GameManager.GetPlayerManagerComponent().m_Container;
		if (container != null && container.gameObject != null && container.gameObject.GetComponent<ObjectGuid>() != null)
		{
			text = container.gameObject.GetComponent<ObjectGuid>().Get();
		}
		Harvestable harvestable = GameManager.GetPlayerManagerComponent().m_Harvestable;
		if (harvestable != null && harvestable.gameObject != null && harvestable.gameObject.GetComponent<ObjectGuid>() != null)
		{
			text2 = harvestable.gameObject.GetComponent<ObjectGuid>().Get();
		}
		Panel_BodyHarvest panel_BodyHarvest = GameCompat.Panel<Panel_BodyHarvest>();
		if (panel_BodyHarvest != null && panel_BodyHarvest.isActiveAndEnabled && panel_BodyHarvest.m_BodyHarvest != null && panel_BodyHarvest.m_BodyHarvest.gameObject != null && panel_BodyHarvest.m_BodyHarvest.gameObject.GetComponent<ObjectGuid>() != null)
		{
			text3 = panel_BodyHarvest.m_BodyHarvest.gameObject.GetComponent<ObjectGuid>().Get();
		}
		Panel_BreakDown panel_BreakDown = GameCompat.Panel<Panel_BreakDown>();
		if (panel_BreakDown != null && panel_BreakDown.isActiveAndEnabled && panel_BreakDown.m_BreakDown != null && panel_BreakDown.m_BreakDown.gameObject != null)
		{
			if (panel_BreakDown.m_BreakDown.gameObject.activeSelf)
			{
				GameObject gameObject = panel_BreakDown.m_BreakDown.gameObject;
				ObjectGuid component = gameObject.GetComponent<ObjectGuid>();
				if (component != null)
				{
					text4 = component.Get();
				}
				if (gameObject.transform.parent != null)
				{
					ObjectGuid component2 = gameObject.transform.parent.GetComponent<ObjectGuid>();
					if (component2 != null)
					{
						text5 = component2.Get();
					}
				}
				flag = true;
			}
			else
			{
				if (panel_BreakDown.m_IsBreakingDown)
				{
					panel_BreakDown.m_IsBreakingDown = false;
					panel_BreakDown.StopAudioAndRumbleEffects();
				}
				panel_BreakDown.OnCancel();
			}
		}
		for (int i = 0; i < playersData.Count; i++)
		{
			if (playersData[i] == null || i == ClientUser.myId)
			{
				continue;
			}
			if (text == null && text2 == null && text3 == null && text4 == null && text5 == null)
			{
				break;
			}
			if (playersData[i].m_Container != null)
			{
				DataStr.ContainerOpenSync container2 = playersData[i].m_Container;
				if (container2.m_Guid == text && container2.m_LevelID == levelid && container2.m_LevelGUID == level_guid)
				{
					text = null;
					container.CancelSearch();
				}
			}
			string plant = playersData[i].m_Plant;
			if (text2 != null && plant == text2)
			{
				HUDMessage.AddMessage(playersData[i].m_Name + " IS ALREADY COLLECTING THIS");
				text2 = null;
				harvestable.CancelHarvest();
			}
			string harvestingAnimal = playersData[i].m_HarvestingAnimal;
			if (text3 != null && harvestingAnimal == text3)
			{
				MelonLogger.Msg("OtherPlayerGUID " + harvestingAnimal);
				MelonLogger.Msg("harvestAnimalGUID " + text3);
				MelonLogger.Msg("index " + i);
				text3 = null;
				ExitHarvesting();
			}
			if (!flag)
			{
				continue;
			}
			DataStr.BrokenFurnitureSync brakingObject = playersData[i].m_BrakingObject;
			if (brakingObject.m_Guid == text4 && brakingObject.m_ParentGuid == text5 && brakingObject.m_LevelID == levelid && brakingObject.m_LevelGUID == level_guid)
			{
				flag = false;
				HUDMessage.AddMessage(playersData[i].m_Name + " IS BREAKING THIS");
				GameAudioManager.PlayGUIError();
				if (panel_BreakDown.m_IsBreakingDown)
				{
					panel_BreakDown.m_IsBreakingDown = false;
					panel_BreakDown.StopAudioAndRumbleEffects();
				}
				panel_BreakDown.OnCancel();
			}
		}
	}

	public static void SendNewRadioFrequency()
	{
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(116))
			{
				packet.Write(RadioFrequency);
				SendUDPData(packet);
			}
		}
	}

	public static string GetRadioFrequency(float ID)
	{
		return (325f + ID).ToString();
	}

	public static void DoRadioSearch()
	{
		LastSearchDistance = GetDistanceToCairn(GameManager.GetPlayerTransform().position);
	}

	public static void DoRadioBeep()
	{
		AudioClip clip = BundleAsset<AudioClip>("RadioBeep");
		if ((bool)MyRadioAudio)
		{
			MyRadioAudio.transform.GetChild(0).gameObject.GetComponent<AudioSource>().PlayOneShot(clip);
		}
	}

	public static bool CairnsSearchActive()
	{
		if (CurrentCustomChalleng.m_Started && CurrentChallengeRules.m_Name == "Lost in action" && CurrentCustomChalleng.m_CurrentTask == 0 && SearchModeActive)
		{
			return true;
		}
		return false;
	}

	public static void UpdateRadio()
	{
		if ((bool)ViewModelRadioNeedle)
		{
			Quaternion localRotation = ViewModelRadioNeedle.transform.localRotation;
			localRotation.eulerAngles = new Vector3(localRotation.eulerAngles.x, localRotation.eulerAngles.y, RadioFrequency);
			ViewModelRadioNeedle.transform.localRotation = localRotation;
		}
		float axisScrollWheel = InputManager.GetAxisScrollWheel(InputManager.m_CurrentContext);
		if (axisScrollWheel < 0f && RadioFrequency > -25f)
		{
			RadioFrequency -= 0.1f;
			RadioFrequency = Mathf.Round(RadioFrequency * 10f) * 0.1f;
			HUDMessage.ShowMessage(message: new HUDMessage.HUDMessageInfo
			{
				m_Text = "Frequency " + GetRadioFrequency(RadioFrequency)
			}, hud: GameCompat.Panel<Panel_HUD>());
			SendNewRadioFrequency();
		}
		if (axisScrollWheel > 0f && RadioFrequency < 25f)
		{
			RadioFrequency += 0.1f;
			RadioFrequency = Mathf.Round(RadioFrequency * 10f) * 0.1f;
			HUDMessage.HUDMessageInfo hUDMessageInfo = new HUDMessage.HUDMessageInfo();
			hUDMessageInfo.m_Text = "Frequency " + GetRadioFrequency(RadioFrequency);
			HUDMessage.ShowMessage(GameCompat.Panel<Panel_HUD>(), hUDMessageInfo);
			SendNewRadioFrequency();
		}
		bool flag = ViewModelRadioLED.activeSelf;
		if (InputManager.GetAltFirePressed(InputManager.m_CurrentContext))
		{
			Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
			if ((bool)panel_ActionPicker && !panel_ActionPicker.IsEnabled())
			{
				if (MPSaveManager.NeverSeenExpeditions())
				{
					ExpeditionHint();
				}
				else
				{
					ShowRadioActionPicker(null);
				}
			}
		}
		if (InputManager.GetReloadPressed(InputManager.m_CurrentContext) && MouseActionIsAllowed())
		{
			ExpeditionHint();
		}
		if (LastSearchDistance != float.PositiveInfinity && CairnsSearchActive())
		{
			float radioBeepRate = GetRadioBeepRate(LastSearchDistance);
			if (nextLEDBlink == 0f)
			{
				nextLEDBlink = Time.time + radioBeepRate;
			}
			if (Time.time > nextLEDBlink)
			{
				nextLEDBlink = Time.time + radioBeepRate;
				if (!flag)
				{
					DoRadioBeep();
					flag = true;
				}
				else
				{
					flag = false;
				}
			}
		}
		else
		{
			flag = false;
		}
		ViewModelRadioLED.SetActive(flag);
	}

	public static bool IsCustomHandItem(string item)
	{
		switch (item)
		{
		case "GEAR_Hatchet":
		case "GEAR_HatchetImprovised":
		case "GEAR_Hammer":
		case "GEAR_Knife":
		case "GEAR_KnifeImprovised":
		case "GEAR_KnifeScrapMetal":
		case "GEAR_JeremiahKnife":
		case "GEAR_Prybar":
		case "GEAR_FireAxe":
		case "GEAR_Shovel":
			return true;
		default:
			return false;
		}
	}

	public static bool IsUserGeneratedHandItem(string item)
	{
		if (item == "GEAR_SCPhoto" || item == "GEAR_SCMapPiece")
		{
			return true;
		}
		return false;
	}

	public static void RetakeItem()
	{
		GearItem itemInHands = GameManager.GetPlayerManagerComponent().m_ItemInHands;
		if ((bool)itemInHands)
		{
			SkipItemEvent = 2;
			GameManager.GetPlayerManagerComponent().UnequipItemInHandsSkipAnimation();
			GameManager.GetPlayerManagerComponent().UseInventoryItem(itemInHands);
			PlayerAnimation playerAnimationComponent = GameManager.GetPlayerAnimationComponent();
			ShouldReEquipFaster = true;
			float retakeTime = GetMeelePlayerInfo(itemInHands.GetGearName()).m_RetakeTime;
			playerAnimationComponent.SetFloat(playerAnimationComponent.m_AnimParameter_PlaybackSpeedMultiplier, retakeTime);
			if ((bool)playerAnimationComponent.m_EquippedFirstPersonWeaponRightHand && (bool)playerAnimationComponent.m_EquippedFirstPersonWeaponRightHand.m_Animator)
			{
				playerAnimationComponent.m_EquippedFirstPersonWeaponRightHand.m_Animator.speed = retakeTime;
			}
			playerAnimationComponent.m_Animator.speed = retakeTime;
		}
	}

	private static BulletImpactEffectType GetImpactEffectTypeBasedOnMaterial(string tag)
	{
		switch (tag)
		{
		case "Snow":
			return BulletImpactEffectType.BulletImpactEffect_Snow;
		case "Wood":
			return BulletImpactEffectType.BulletImpactEffect_Wood;
		case "Straw":
			return BulletImpactEffectType.BulletImpactEffect_Fabric;
		case "Ice":
			return BulletImpactEffectType.BulletImpactEffect_Ice;
		case "Rug":
			return BulletImpactEffectType.BulletImpactEffect_Fabric;
		case "Stone":
			return BulletImpactEffectType.BulletImpactEffect_Stone;
		case "Metal":
			return BulletImpactEffectType.BulletImpactEffect_Metal;
		case "WoodSolid":
			return BulletImpactEffectType.BulletImpactEffect_Wood;
		case "Road":
			return BulletImpactEffectType.BulletImpactEffect_Stone;
		case "Flesh":
		case "BaseAi":
			return BulletImpactEffectType.BulletImpactEffect_Flesh;
		default:
			return BulletImpactEffectType.BulletImpactEffect_Untagged;
		case "Glass":
			return BulletImpactEffectType.BulletImpactEffect_Glass;
		}
	}

	public static DataStr.MeleeDescripter GetMeelePlayerInfo(string weapon)
	{
		DataStr.MeleeDescripter meleeDescripter = new DataStr.MeleeDescripter();
		switch (weapon)
		{
		case "GEAR_Hatchet":
		case "GEAR_HatchetImprovised":
			meleeDescripter.m_PlayerDamage = 20f;
			meleeDescripter.m_AnimalDamage = 45f;
			meleeDescripter.m_BloodLoss = true;
			meleeDescripter.m_Pain = false;
			meleeDescripter.m_ClothingTearing = true;
			meleeDescripter.m_AttackSpeed = 1.1f;
			meleeDescripter.m_RetakeTime = 1.1f;
			return meleeDescripter;
		case "GEAR_FireAxe":
			meleeDescripter.m_PlayerDamage = 40f;
			meleeDescripter.m_AnimalDamage = 70f;
			meleeDescripter.m_BloodLoss = true;
			meleeDescripter.m_Pain = true;
			meleeDescripter.m_ClothingTearing = true;
			meleeDescripter.m_AttackSpeed = 1.3f;
			meleeDescripter.m_RetakeTime = 1.3f;
			return meleeDescripter;
		case "GEAR_Hammer":
			meleeDescripter.m_PlayerDamage = 25f;
			meleeDescripter.m_AnimalDamage = 55f;
			meleeDescripter.m_BloodLoss = false;
			meleeDescripter.m_Pain = true;
			meleeDescripter.m_ClothingTearing = false;
			meleeDescripter.m_AttackSpeed = 1f;
			meleeDescripter.m_RetakeTime = 1f;
			return meleeDescripter;
		case "GEAR_Prybar":
			meleeDescripter.m_PlayerDamage = 15f;
			meleeDescripter.m_AnimalDamage = 40f;
			meleeDescripter.m_BloodLoss = false;
			meleeDescripter.m_Pain = true;
			meleeDescripter.m_ClothingTearing = false;
			meleeDescripter.m_AttackSpeed = 1.2f;
			meleeDescripter.m_RetakeTime = 1f;
			return meleeDescripter;
		case "GEAR_Knife":
		case "GEAR_KnifeImprovised":
		case "GEAR_KnifeScrapMetal":
		case "GEAR_JeremiahKnife":
			meleeDescripter.m_PlayerDamage = 17f;
			meleeDescripter.m_AnimalDamage = 57f;
			meleeDescripter.m_BloodLoss = true;
			meleeDescripter.m_Pain = false;
			meleeDescripter.m_ClothingTearing = true;
			meleeDescripter.m_AttackSpeed = 10f;
			meleeDescripter.m_RetakeTime = 8f;
			return meleeDescripter;
		case "GEAR_Shovel":
			meleeDescripter.m_PlayerDamage = 18f;
			meleeDescripter.m_AnimalDamage = 48f;
			meleeDescripter.m_BloodLoss = false;
			meleeDescripter.m_Pain = true;
			meleeDescripter.m_ClothingTearing = true;
			meleeDescripter.m_AttackSpeed = 1f;
			meleeDescripter.m_RetakeTime = 1f;
			break;
		}
		if (weapon == "GEAR_RawSmallMouthBass")
		{
			meleeDescripter.m_PlayerDamage = 3f;
			meleeDescripter.m_AnimalDamage = 5f;
			meleeDescripter.m_BloodLoss = false;
			meleeDescripter.m_Pain = false;
			meleeDescripter.m_ClothingTearing = false;
			meleeDescripter.m_AttackSpeed = 1.2f;
			meleeDescripter.m_RetakeTime = 1f;
		}
		return meleeDescripter;
	}

	public static bool MouseActionIsAllowed()
	{
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		if (InterfaceManager.IsOverlayActiveImmediate() || ((bool)GameCompat.Panel<Panel_HUD>().m_TwoButtonsChoiceUI && GameCompat.Panel<Panel_HUD>().m_TwoButtonsChoiceUI.IsManagingInput()) || (UICamera.currentScheme != UICamera.ControlScheme.Controller && (bool)playerManagerComponent.GetInteractiveObjectUnderCrosshair() && !playerManagerComponent.PlayerIsZooming() && (bool)playerManagerComponent.GetInteractiveObjectUnderCrosshair().GetComponent<BreakDown>()) || playerManagerComponent.IsInPlacementMode() || InputManager.IsClickHoldActive() || InteractionInprocess || ConsoleIsOpen() || ChatIsOpen())
		{
			return false;
		}
		return true;
	}

	public static void PerformMeleeAttack(string Weapon)
	{
		if (!GameManager.GetPlayerAnimationComponent().CanTransitionToState(PlayerAnimation.State.Throwing) || !MouseActionIsAllowed())
		{
			return;
		}
		ShouldPerformAttack = true;
		PlayerAnimation playerAnimationComponent = GameManager.GetPlayerAnimationComponent();
		float attackSpeed = GetMeelePlayerInfo(Weapon).m_AttackSpeed;
		playerAnimationComponent.Trigger_Generic_Throw(null, null);
		playerAnimationComponent.SetFloat(playerAnimationComponent.m_AnimParameter_PlaybackSpeedMultiplier, attackSpeed);
		if ((bool)playerAnimationComponent.m_EquippedFirstPersonWeaponRightHand && (bool)playerAnimationComponent.m_EquippedFirstPersonWeaponRightHand.m_Animator)
		{
			playerAnimationComponent.m_EquippedFirstPersonWeaponRightHand.m_Animator.speed = attackSpeed;
		}
		playerAnimationComponent.m_Animator.speed = attackSpeed;
		if (iAmHost)
		{
			ServerSend.MELEESTART(0);
		}
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(117))
			{
				packet.Write(_value: true);
				SendUDPData(packet);
			}
		}
	}

	public static void ReadNoteInHands()
	{
		if (MouseActionIsAllowed() && !GameCompat.Panel<Panel_HUD>().m_CollectibleNoteObject.activeSelf)
		{
			Pathes.DisplayNote(GameManager.GetPlayerManagerComponent().m_ItemInHands);
		}
	}

	public static void DoMeleeHitFX(Vector3 pos, Vector3 forward, Quaternion rot, GameObject PlayerObj)
	{
		int layerMask = Utils.m_WeaponProjectileCollisionLayerMask | 0x8000000;
		if (AiUtils.RaycastWithAimAssist(pos, forward, out var hit, MeleeRange, MeleeRange, MeleeRange, 30f, layerMask))
		{
			string materialTagForObjectAtPosition = Utils.GetMaterialTagForObjectAtPosition(hit.collider.gameObject, hit.point);
			GameAudioManager.SetMaterialSwitch(materialTagForObjectAtPosition, PlayerObj);
			GameAudioManager.Play3DSound("Play_StoneImpacts", PlayerObj);
			GameAudioManager.NotifyAiAudioEvent(PlayerObj, hit.point, GameAudioAiEvent.Generic);
			MaterialEffectType impactEffectType = ImpactDecals.MapSurfaceTagToMaterialEffectType(materialTagForObjectAtPosition);
			GameManager.GetDynamicDecalsManager().AddImpactDecal(ProjectileType.Arrow, impactEffectType, hit.point, hit.normal);
			BulletImpactEffectType impactEffectTypeBasedOnMaterial = GetImpactEffectTypeBasedOnMaterial(materialTagForObjectAtPosition);
			BulletImpactEffectPool bulletImpactEffectPool = GameManager.GetEffectPoolManager().GetBulletImpactEffectPool();
			if (impactEffectTypeBasedOnMaterial == BulletImpactEffectType.BulletImpactEffect_Untagged)
			{
				bulletImpactEffectPool.SpawnUntilParticlesDone(BulletImpactEffectType.BulletImpactEffect_Stone, hit.point, rot);
			}
			else
			{
				bulletImpactEffectPool.SpawnUntilParticlesDone(impactEffectTypeBasedOnMaterial, hit.point, rot);
			}
			MaterialEffectType impactEffectType2 = ImpactDecals.MapBulletImpactEffectTypeToMaterialEffectType(impactEffectTypeBasedOnMaterial);
			GameManager.GetDynamicDecalsManager().AddImpactDecal(ProjectileType.Bullet, impactEffectType2, hit.point, forward);
			if ((bool)hit.collider && (bool)hit.collider.gameObject)
			{
				GameAudioManager.SetMaterialSwitch(materialTagForObjectAtPosition, hit.collider.gameObject);
				Transform transform = PlayerObj.transform;
				AkSoundEngine.PostEvent(EVENTS.PLAY_BULLETIMPACTS, GameAudioManager.GetSoundEmitterFromGameObject(PlayerObj));
				GameAudioManager.SetAudioSourceTransform(PlayerObj, transform);
			}
		}
	}

	public static void MeleeHit()
	{
		PushActionToMyDoll("Melee");
		GameObject gameObject = UnityEngine.Object.Instantiate(GetGearItemObject("GEAR_Arrow"), new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f));
		gameObject.GetComponent<GearItem>().m_ArrowItem.Fire(1f);
		UnityEngine.Object.Destroy(gameObject);
		DataStr.ShootSync shootSync = new DataStr.ShootSync();
		shootSync.m_position = GameManager.GetVpFPSCamera().transform.position;
		shootSync.m_rotation = GameManager.GetVpFPSCamera().transform.rotation;
		shootSync.m_projectilename = "Melee";
		shootSync.m_skill = 0f;
		shootSync.m_camera_forward = GameManager.GetVpFPSCamera().transform.forward;
		shootSync.m_camera_right = GameManager.GetVpFPSCamera().transform.right;
		shootSync.m_camera_up = GameManager.GetVpFPSCamera().transform.up;
		shootSync.m_sceneguid = level_guid;
		if (sendMyPosition)
		{
			using Packet packet = new Packet(24);
			packet.Write(shootSync);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			ServerSend.SHOOTSYNC(0, shootSync, toEveryOne: true);
		}
		int layerMask = Utils.m_WeaponProjectileCollisionLayerMask | 0x8000000;
		string text = "";
		if (GameManager.GetPlayerManagerComponent().m_ItemInHands != null)
		{
			text = GameManager.GetPlayerManagerComponent().m_ItemInHands.GetGearName();
		}
		if (!AiUtils.RaycastWithAimAssist(GameManager.GetVpFPSCamera().transform.position, GameManager.GetVpFPSCamera().transform.forward, out var hit, MeleeRange, MeleeRange, MeleeRange, 30f, layerMask))
		{
			return;
		}
		string materialTagForObjectAtPosition = Utils.GetMaterialTagForObjectAtPosition(hit.collider.gameObject, hit.point);
		GameAudioManager.SetMaterialSwitch(materialTagForObjectAtPosition, GameManager.GetPlayerObject());
		GameAudioManager.Play3DSound("Play_StoneImpacts", GameManager.GetPlayerObject());
		GameAudioManager.NotifyAiAudioEvent(GameManager.GetPlayerObject(), hit.point, GameAudioAiEvent.Generic);
		MaterialEffectType impactEffectType = ImpactDecals.MapSurfaceTagToMaterialEffectType(materialTagForObjectAtPosition);
		GameManager.GetDynamicDecalsManager().AddImpactDecal(ProjectileType.Arrow, impactEffectType, hit.point, hit.normal);
		BulletImpactEffectType impactEffectTypeBasedOnMaterial = GetImpactEffectTypeBasedOnMaterial(materialTagForObjectAtPosition);
		BulletImpactEffectPool bulletImpactEffectPool = GameManager.GetEffectPoolManager().GetBulletImpactEffectPool();
		if (impactEffectTypeBasedOnMaterial == BulletImpactEffectType.BulletImpactEffect_Untagged)
		{
			bulletImpactEffectPool.SpawnUntilParticlesDone(BulletImpactEffectType.BulletImpactEffect_Stone, hit.point, GameManager.GetVpFPSCamera().transform.rotation);
		}
		else
		{
			bulletImpactEffectPool.SpawnUntilParticlesDone(impactEffectTypeBasedOnMaterial, hit.point, GameManager.GetVpFPSCamera().transform.rotation);
		}
		MaterialEffectType impactEffectType2 = ImpactDecals.MapBulletImpactEffectTypeToMaterialEffectType(impactEffectTypeBasedOnMaterial);
		GameManager.GetDynamicDecalsManager().AddImpactDecal(ProjectileType.Bullet, impactEffectType2, hit.point, GameManager.GetVpFPSCamera().transform.forward);
		if (!hit.collider || !hit.collider.gameObject)
		{
			return;
		}
		GameAudioManager.SetMaterialSwitch(materialTagForObjectAtPosition, hit.collider.gameObject);
		Transform playerTransform = GameManager.GetPlayerTransform();
		GameObject playerObject = GameManager.GetPlayerObject();
		AkSoundEngine.PostEvent(EVENTS.PLAY_BULLETIMPACTS, GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject()));
		GameAudioManager.SetAudioSourceTransform(playerObject, playerTransform);
		Comps.PlayerBulletDamage component = hit.collider.gameObject.GetComponent<Comps.PlayerBulletDamage>();
		if (component != null)
		{
			float num = (float)(component.m_Damage / 3) + GetMeelePlayerInfo(text).m_PlayerDamage;
			MelonLogger.Msg("You damaged other player on " + num);
			int type = component.m_Type;
			if (sendMyPosition)
			{
				using Packet packet2 = new Packet(29);
				packet2.Write(num);
				packet2.Write(type);
				packet2.Write(component.m_ClientId);
				packet2.Write(_value: true);
				packet2.Write(text);
				SendUDPData(packet2);
			}
			if (!iAmHost)
			{
				return;
			}
			using (new Packet(29))
			{
				ServerSend.BULLETDAMAGE(component.m_ClientId, num, type, 0, Melee: true, text);
				return;
			}
		}
		BaseAi baseAiFromObject = AiUtils.GetBaseAiFromObject(hit.collider.gameObject);
		Comps.AnimalActor animalActor = ((!hit.collider.gameObject) ? null : ((hit.collider.gameObject.layer == 16) ? hit.collider.gameObject.GetComponent<Comps.AnimalActor>() : ((hit.collider.gameObject.layer != 27) ? null : hit.collider.gameObject.transform.GetComponentInParent<Comps.AnimalActor>())));
		if (baseAiFromObject != null)
		{
			MelonLogger.Msg("This is baseAI animal");
			LocalizedDamage component2 = hit.collider.GetComponent<LocalizedDamage>();
			float bleedOutMinutes = component2.GetBleedOutMinutes(WeaponSource.Rifle);
			float num2 = GetMeelePlayerInfo(text).m_AnimalDamage * component2.GetDamageScale(WeaponSource.Rifle);
			if (!Utils.IsZero(num2) || baseAiFromObject.ForceApplyDamage())
			{
				if (baseAiFromObject.GetAiMode() != AiMode.Dead)
				{
					GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.CarcassHarvesting, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
				}
				baseAiFromObject.SetupDamageForAnim(hit.collider.transform.position, GameManager.GetPlayerTransform().position, component2);
				baseAiFromObject.ApplyDamage(num2, bleedOutMinutes, DamageSource.Player, hit.collider.name);
			}
		}
		else
		{
			if (!(animalActor != null))
			{
				return;
			}
			LocalizedDamage component3 = hit.collider.GetComponent<LocalizedDamage>();
			component3.GetBleedOutMinutes(WeaponSource.Rifle);
			float num3 = GetMeelePlayerInfo(text).m_AnimalDamage * component3.GetDamageScale(WeaponSource.Rifle);
			if (num3 > 0f && animalActor.m_Hp > 0f)
			{
				GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.CarcassHarvesting, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
			}
			if (iAmHost)
			{
				ServerSend.ANIMALDAMAGE(0, animalActor.gameObject.GetComponent<ObjectGuid>().Get(), num3);
			}
			else if (sendMyPosition)
			{
				using (Packet packet3 = new Packet(83))
				{
					packet3.Write(animalActor.gameObject.GetComponent<ObjectGuid>().Get());
					packet3.Write(num3);
					SendUDPData(packet3);
				}
			}
		}
	}

	public static DataStr.MultiplayerEmote GetEmoteByID(int ID)
	{
		DataStr.MultiplayerEmote multiplayerEmote = new DataStr.MultiplayerEmote();
		switch (ID)
		{
		case 0:
			multiplayerEmote.m_Name = "Maraschino";
			multiplayerEmote.m_Animation = "Cringe1";
			multiplayerEmote.m_ForceStandup = true;
			break;
		case 1:
			multiplayerEmote.m_Name = "Breakdance";
			multiplayerEmote.m_Animation = "Cringe2";
			multiplayerEmote.m_ForceStandup = true;
			break;
		case 2:
			multiplayerEmote.m_Name = "Samba";
			multiplayerEmote.m_Animation = "Flex";
			multiplayerEmote.m_ForceStandup = true;
			break;
		case 3:
			multiplayerEmote.m_Name = "Thumbs Up";
			multiplayerEmote.m_Animation = "DoThumbsUp";
			multiplayerEmote.m_LeftHandEmote = true;
			break;
		case 4:
			multiplayerEmote.m_Name = "Middle Finger";
			multiplayerEmote.m_Animation = "DoMiddleFinger";
			multiplayerEmote.m_LeftHandEmote = true;
			break;
		case 5:
			multiplayerEmote.m_Name = "Point";
			multiplayerEmote.m_Animation = "DoPoint";
			multiplayerEmote.m_LeftHandEmote = true;
			break;
		case 6:
			multiplayerEmote.m_Name = "Wave";
			multiplayerEmote.m_Animation = "DoWave";
			multiplayerEmote.m_LeftHandEmote = true;
			break;
		default:
			multiplayerEmote.m_Name = "Maraschino";
			multiplayerEmote.m_Animation = "Cringe1";
			multiplayerEmote.m_ForceStandup = true;
			break;
		}
		return multiplayerEmote;
	}

	public static void PerformEmote(int EmoteID)
	{
		if ((bool)MyPlayerDoll)
		{
			MyPlayerDoll.transform.rotation = GameManager.GetPlayerTransform().rotation;
		}
		IgnoreEmoteKeyDownUntilReleased = true;
		DataStr.MultiplayerEmote emoteByID = GetEmoteByID(EmoteID);
		if (!emoteByID.m_LeftHandEmote)
		{
			MyEmote = GetEmoteByID(EmoteID);
			return;
		}
		if ((bool)MyPlayerDoll)
		{
			MyPlayerDoll.GetComponent<Comps.MultiplayerPlayerAnimator>().DoLeftHandEmote(emoteByID.m_Animation);
		}
		if (iAmHost)
		{
			ServerSend.TRIGGEREMOTE(0, EmoteID);
		}
		if (!sendMyPosition)
		{
			return;
		}
		using Packet packet = new Packet(157);
		packet.Write(EmoteID);
		SendUDPData(packet);
	}

	public static bool ConsoleIsOpen()
	{
		if (uConsole.m_Instance != null)
		{
			return uConsole.m_On;
		}
		return false;
	}

	public static bool ChatIsOpen()
	{
		if (chatInput != null)
		{
			return chatInput.gameObject.activeSelf;
		}
		return false;
	}

	public static void UpdateEmoteWheel()
	{
		if (!EmoteWheel)
		{
			return;
		}
		bool flag = KeyboardUtilities.InputManager.GetKey(KeyCode.T);
		if ((chatInput != null && chatInput.gameObject.activeSelf) || (uConsole.m_Instance != null && uConsole.m_On))
		{
			flag = false;
		}
		if (flag && !IgnoreEmoteKeyDownUntilReleased)
		{
			EmoteWheel.SetActive(value: true);
			InputManager.m_CursorState = InputManager.CursorState.Show;
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			int num = -1;
			for (int i = 0; i <= 7; i++)
			{
				if (EmoteWheel.transform.GetChild(i).GetChild(0).gameObject.GetComponent<Button>()._isPointerInside_k__BackingField)
				{
					num = i;
					break;
				}
			}
			if (num != -1)
			{
				EmoteWheel.transform.GetChild(8).gameObject.SetActive(value: true);
				EmoteWheel.transform.GetChild(8).GetChild(1).gameObject.GetComponent<Text>().text = GetEmoteByID(num).m_Name;
			}
			else
			{
				EmoteWheel.transform.GetChild(8).gameObject.SetActive(value: false);
			}
		}
		else
		{
			EmoteWheel.SetActive(value: false);
		}
		if (IgnoreEmoteKeyDownUntilReleased && !flag)
		{
			IgnoreEmoteKeyDownUntilReleased = false;
		}
	}

	public static void PushActionToMyDoll(string Act)
	{
		if (!MyPlayerDoll)
		{
			return;
		}
		Comps.MultiplayerPlayerAnimator component = MyPlayerDoll.GetComponent<Comps.MultiplayerPlayerAnimator>();
		if (!component)
		{
			return;
		}
		switch (Act)
		{
		case "Pickup":
			component.Pickup();
			break;
		case "Eat":
		case "Drink":
			component.m_IsDrink = Act == "Drink";
			component.Consumption();
			break;
		case "Shoot":
			if (MyLightSourceName == "GEAR_Rifle")
			{
				string preAnimStateHands = "RifleShoot";
				if (MyAnimState == "Ctrl")
				{
					preAnimStateHands = "RifleShoot_Sit";
				}
				component.m_PreAnimStateHands = preAnimStateHands;
			}
			if (MyPlayerDoll.activeSelf)
			{
				DoShootFX(MyPlayerDoll.transform.position);
			}
			break;
		case "Melee":
			component.MeleeAttack();
			break;
		}
	}

	public static void UpdateDoll()
	{
		if (!MyPlayerDoll || !(GameManager.m_PlayerObject != null))
		{
			return;
		}
		if ((bool)CustomizeUi && CustomizeUi.activeSelf)
		{
			MyPlayerDoll.SetActive(value: true);
			return;
		}
		MyPlayerDoll.SetActive(IsFlyModeActive());
		if (IsFlyModeActive())
		{
			MyPlayerDoll.transform.position = GameManager.GetPlayerTransform().position;
			MyPlayerDoll.transform.rotation = GameManager.GetPlayerTransform().rotation;
		}
	}

	public static bool IsFlyModeActive()
	{
		return CameraVisibility.IsFlyModeEntered;
	}

	private static float DiskCommitWaitStarted = -1f;

	public static void SaveCheck()
	{
		if (SaveBeforeQuitRequested)
		{
			if (SaveGameData.IsAnyOperationPending() || SaveGameSystem.IsAsyncSaveRunning() || SaveGameSystem.IsWaitingForScreenshot())
			{
				return;
			}
			SaveBeforeQuitRequested = false;
			try
			{
				DoPleaseWait("Saving", (PendingSaveExitAction == SaveExitAction.Disconnect) ? "Saving before disconnect..." : "Saving before exit...");
				if (iAmHost)
				{
					MPSaveManager.SaveRecentStuff();
				}
				GameManager.ForceSaveGame();
			}
			catch (System.Exception ex)
			{
				MelonLogger.Error("[SaveQuit] Save request failed; the game will remain open: " + ex);
				Pathes.ResetPendingQuitAfterSaveFailure();
			}
		}
		if (!QuitWhenSaveOperationFinished || string.IsNullOrEmpty(PendingSlotDiskCommit))
		{
			DiskCommitWaitStarted = -1f;
		}
		else if (DiskCommitWaitStarted < 0f)
		{
			DiskCommitWaitStarted = Time.realtimeSinceStartup;
		}
		else if (Time.realtimeSinceStartup - DiskCommitWaitStarted > 10f)
		{
			// Once seen in a session: a save flag never cleared after the in-memory save and the game hung with the
			// world never written. Log what is holding and commit the slot anyway.
			DiskCommitWaitStarted = -1f;
			MelonLogger.Warning("[SaveQuit] Disk commit for " + PendingSlotDiskCommit + " stuck 10s: operationPending=" + SaveGameData.IsAnyOperationPending() + " asyncRunning=" + SaveGameSystem.IsAsyncSaveRunning() + " waitingScreenshot=" + SaveGameSystem.IsWaitingForScreenshot() + "; forcing commit");
			if (SaveGameSystem.IsWaitingForScreenshot())
			{
				SaveGameSystem.AbortScreenshot();
			}
			try
			{
				bool committed = SaveGameSlots.WriteSlotToDisk(PendingSlotDiskCommit);
				MelonLogger.Msg("[SaveQuit] Forced WriteSlotToDisk(" + PendingSlotDiskCommit + ") = " + committed);
				if (committed)
				{
					PendingSlotDiskCommit = null;
					SaveCommitDrainAfterFrame = Time.frameCount + 2;
				}
			}
			catch (System.Exception ex3)
			{
				MelonLogger.Error("[SaveQuit] Forced disk commit failed: " + ex3);
			}
		}
		if (!QuitWhenSaveOperationFinished || Time.frameCount < SaveCommitDrainAfterFrame || SaveGameData.IsAnyOperationPending() || SaveGameSystem.IsAsyncSaveRunning() || SaveGameSystem.IsWaitingForScreenshot())
		{
			return;
		}
		if (!string.IsNullOrEmpty(PendingSlotDiskCommit))
		{
			string pendingSlotDiskCommit = PendingSlotDiskCommit;
			try
			{
				if (!SaveGameSlots.WriteSlotToDisk(pendingSlotDiskCommit))
				{
					MelonLogger.Error("[SaveQuit] Native slot commit rejected; keeping the game open.");
					Pathes.ResetPendingQuitAfterSaveFailure();
					return;
				}
			}
			catch (System.Exception ex2)
			{
				MelonLogger.Error("[SaveQuit] Native slot commit failed; keeping the game open: " + ex2);
				Pathes.ResetPendingQuitAfterSaveFailure();
				return;
			}
			PendingSlotDiskCommit = null;
			SaveCommitDrainAfterFrame = Time.frameCount + 2;
			MelonLogger.Msg("SAVE_DISK_COMMIT_QUEUED slot=" + pendingSlotDiskCommit);
			return;
		}
		if (PendingSaveExitAction == SaveExitAction.Quit)
		{
			if (SaveGameSystem.IsAsyncEnabled())
			{
				SaveGameSystem.SetAsyncEnabled(enabled: false);
			}
			MelonLogger.Msg("SAVE_ASYNC_DISABLED_FOR_QUIT");
		}
		MelonLogger.Msg("Saving on disk done");
		MelonLogger.Msg("SAVE_DRAINED");
		if (sendMyPosition)
		{
			if (PendingSaveHashToSend != 0L)
			{
				using Packet packet = new Packet(138);
				packet.Write(MyUGUID);
				packet.Write(PendingSaveHashToSend);
				packet.Write(_value: true);
				try
				{
					SendUDPData(packet, IgnoreQuit: true);
				}
				catch (System.Exception ex3)
				{
					MelonLogger.Warning("[SaveQuit] Save hash notification failed after disk commit: " + ex3.Message);
				}
			}
			MelonLogger.Msg("[SaveQuit] Save notification processed; completing " + PendingSaveExitAction);
		}
		QuitWhenSaveOperationFinished = false;
		NoMelonQuit = false;
		Pathes.QuitOnSave = false;
		if (PendingSaveExitAction == SaveExitAction.Disconnect)
		{
			System.Action afterSavedDisconnect = AfterSavedDisconnect;
			AfterSavedDisconnect = null;
			PendingSaveHashToSend = 0L;
			Disconnect(checkpoint: false);
			QuitWithoutSaving = false;
			RemovePleaseWait();
			MelonLogger.Msg("SAVE_DISCONNECT_COMPLETED");
			afterSavedDisconnect?.Invoke();
		}
		else
		{
			IReallyWantToQuit = true;
			Application.Quit();
		}
	}

	public static void UIsWorking()
	{
		if (!CanvasUisReady)
		{
			BuildCanvasUIs();
		}
		else
		{
			UpdateCanvasUis();
		}
		if (m_InterfaceManager != null)
		{
			if (NeedConnectAfterLoad != -1 && GameCompat.Panel<Panel_Confirmation>() != null)
			{
				DoWaitForConnect(Steam: true);
			}
			if (GameCompat.Panel<Panel_SnowShelterInteract>() != null)
			{
				CancleDismantling();
			}
			if (GameCompat.Panel<Panel_Rest>() != null && GameCompat.Panel<Panel_Rest>().isActiveAndEnabled)
			{
				if (SleepingButtons == null)
				{
					BuildSleepScreen();
				}
				else
				{
					UpdateSleepScreen();
				}
			}
			if (GameCompat.Panel<Panel_BodyHarvest>() != null && GameCompat.Panel<Panel_BodyHarvest>().m_BodyHarvest != null && GameCompat.Panel<Panel_BodyHarvest>().m_BodyHarvest.gameObject != null && GameCompat.Panel<Panel_BodyHarvest>().m_BodyHarvest.gameObject.GetComponent<ObjectGuid>() != null)
			{
				HarvestingAnimal = GameCompat.Panel<Panel_BodyHarvest>().m_BodyHarvest.gameObject.GetComponent<ObjectGuid>().Get();
			}
			else
			{
				HarvestingAnimal = "";
			}
			SendHarvestingAnimal();
			if ((bool)GameManager.m_PlayerManager && InOnline())
			{
				CancleIfSomeoneDoSame();
			}
			if (CurrentCustomChalleng.m_Started || OnExpedition)
			{
				if (GameCompat.Panel<Panel_Actions>() != null && GameCompat.Panel<Panel_Actions>().isActiveAndEnabled)
				{
					GameCompat.Panel<Panel_Actions>().m_MissionObjectWithTimer.SetActive(value: true);
					GameCompat.Panel<Panel_Actions>().m_MissionObjectiveWithTimerLabel.gameObject.SetActive(value: true);
				}
				if (OnExpedition)
				{
					ExpeditionUI(ExpeditionLastName, ExpeditionLastTaskText, ExpeditionLastTime);
				}
				else if (CurrentCustomChalleng.m_Started)
				{
					PatchChallange();
				}
			}
			UpdateEmoteWheel();
		}
		if (MyEmote != null)
		{
			if (MyEmote.m_FollowDollCamera)
			{
				vp_FPSCamera vpFPSCamera = GameManager.GetVpFPSCamera();
				Quaternion rotation = DollCameraDummy.transform.rotation;
				float pitch = rotation.eulerAngles.x + vpFPSCamera.m_BasePitch;
				float y = rotation.eulerAngles.y;
				vpFPSCamera.SetAngle(y, pitch);
			}
			bool flag = GameManager.GetPlayerManagerComponent().PlayerIsCrouched();
			if (MyEmote.m_ForceStandup)
			{
				if (flag)
				{
					GameManager.GetVpFPSPlayer().InputCrouch();
				}
			}
			else if (MyEmote.m_ForceCrouch && !flag)
			{
				GameManager.GetVpFPSPlayer().InputCrouch();
			}
		}
		if (Shared.IsMainMenuScene(level_name) && (bool)NewFlairNotification)
		{
			if ((bool)m_Panel_MainMenu && (bool)m_Panel_MainMenu.m_MainPanel && m_Panel_MainMenu.m_MainPanel.gameObject.activeSelf && (bool)GameCompat.Panel<Panel_Confirmation>() && !GameCompat.Panel<Panel_Confirmation>().isActiveAndEnabled)
			{
				NewFlairNotification.SetActive(NotificationString != "");
				if (NotificationString != "")
				{
					NewFlairNotification.transform.GetChild(0).gameObject.GetComponent<Text>().text = NotificationString;
				}
			}
			else
			{
				NewFlairNotification.SetActive(value: false);
			}
		}
		if ((bool)CustomizeUi && CustomizeUi.activeSelf && Shared.IsMainMenuScene(level_name))
		{
			UpdateDoll();
		}
		if (ForcedShowExpeditionHUDSeconds > 0)
		{
			GameCompat.Panel<Panel_Actions>().Enable(enable: true);
		}
	}

	public static void CustomWeapons()
	{
		if (!(GameManager.m_PlayerManager != null) || !GameManager.GetPlayerManagerComponent().m_ItemInHands)
		{
			return;
		}
		string gearName = GameManager.GetPlayerManagerComponent().m_ItemInHands.GetGearName();
		if ((bool)ViewModelRadio)
		{
			if (GameManager.GetPlayerManagerComponent().m_ItemInHands.GetFPSMeshID() == 17)
			{
				ViewModelRadio.SetActive(value: true);
				if (!OriginalRadioSeaker)
				{
					UpdateRadio();
				}
			}
			else
			{
				ViewModelRadio.SetActive(value: false);
				SearchModeActive = false;
			}
		}
		else
		{
			if (!LoggedMissingRadio)
			{
				LoggedMissingRadio = true;
				MelonLogger.Msg("No ViewModelRadio mesh");
			}
		}
		if (IsCustomHandItem(gearName))
		{
			if ((bool)ViewModelDummy)
			{
				ViewModelDummy.SetActive(value: false);
			}
			else
			{
				MelonLogger.Msg("No ViewModelDummy mesh");
			}
			if (InputManager.GetFirePressed(InputManager.m_CurrentContext))
			{
				bool flag = true;
				if (ShouldReEquipFaster)
				{
					switch (gearName)
					{
					case "GEAR_Knife":
					case "GEAR_KnifeImprovised":
					case "GEAR_KnifeScrapMetal":
					case "GEAR_JeremiahKnife":
						flag = false;
						break;
					}
				}
				if (flag)
				{
					PerformMeleeAttack(gearName);
				}
			}
		}
		else if (gearName == "GEAR_SCNote" && InputManager.GetFirePressed(InputManager.m_CurrentContext))
		{
			ReadNoteInHands();
		}
		if ((bool)ViewModelHatchet)
		{
			ViewModelHatchet.SetActive(gearName == "GEAR_Hatchet");
		}
		if ((bool)ViewModelHatchet2)
		{
			ViewModelHatchet2.SetActive(gearName == "GEAR_HatchetImprovised");
		}
		if ((bool)ViewModelHammer)
		{
			ViewModelHammer.SetActive(gearName == "GEAR_Hammer");
		}
		if ((bool)ViewModelKnife)
		{
			ViewModelKnife.SetActive(gearName == "GEAR_Knife");
		}
		if ((bool)ViewModelJeremiahKnife)
		{
			ViewModelJeremiahKnife.SetActive(gearName == "GEAR_JeremiahKnife");
		}
		if ((bool)ViewModelKnife2)
		{
			ViewModelKnife2.SetActive(gearName == "GEAR_KnifeImprovised" || gearName == "GEAR_KnifeScrapMetal");
		}
		if ((bool)ViewModelPrybar)
		{
			ViewModelPrybar.SetActive(gearName == "GEAR_Prybar");
		}
		if ((bool)ViewModelShovel)
		{
			ViewModelShovel.SetActive(gearName == "GEAR_Shovel");
		}
		if ((bool)ViewModelFireAxe)
		{
			ViewModelFireAxe.SetActive(gearName == "GEAR_FireAxe");
		}
		if ((bool)ViewModelHackSaw)
		{
			ViewModelHackSaw.SetActive(gearName == "GEAR_Hacksaw");
		}
		if ((bool)ViewModelPhoto)
		{
			ViewModelPhoto.SetActive(gearName == "GEAR_SCPhoto");
		}
		if ((bool)ViewModelMap)
		{
			ViewModelMap.SetActive(gearName == "GEAR_SCMapPiece");
		}
		if ((bool)ViewModelStone)
		{
			ViewModelStone.SetActive(gearName == "GEAR_Stone");
		}
		if ((bool)ViewModelNote)
		{
			ViewModelNote.SetActive(gearName == "GEAR_SCNote");
		}
		if (UseBoltInsteadOfStone)
		{
			if ((bool)ViewModelStone)
			{
				ViewModelStone.SetActive(value: false);
			}
			if ((bool)ViewModelBolt)
			{
				ViewModelBolt.SetActive(gearName == "GEAR_Stone");
			}
		}
	}

	public static void ProcessVoice()
	{
		if (SteamServerWorks != "" || Server.UsingSteamWorks)
		{
			bool radio = false;
			if ((bool)GameManager.m_PlayerManager && (bool)GameManager.m_PlayerManager.m_ItemInHands && GameManager.m_PlayerManager.m_ItemInHands.GetFPSMeshID() == 17)
			{
				radio = true;
			}
			TrackWhenRecordOver(radio);
		}
	}

	public void UpdateDeathFlag()
	{
		if ((bool)GameManager.m_Condition)
		{
			IsDead = GameManager.m_Condition.m_CurrentHP <= 0f;
		}
	}

	public void SendPlayerData()
	{
		if (!(GameManager.m_PlayerObject != null))
		{
			return;
		}
		Transform playerTransform = GameManager.GetPlayerTransform();
		bool flag = GameManager.GetPlayerStruggleComponent().InStruggle();
		UpdateMyEquipment();
		UpdateVisualSyncs();
		if ((bool)playerTransform)
		{
			if (previoustickpos != playerTransform.position)
			{
				MyEmote = null;
				previoustickpos = GameManager.GetPlayerTransform().position;
				SyncMovement(flag, playerTransform);
			}
			else
			{
				SetCurrentAnimation(flag);
			}
			if (previoustickrot != playerTransform.rotation)
			{
				previoustickrot = GameManager.GetPlayerTransform().rotation;
				SyncRotation(playerTransform);
			}
		}
		SendMyAnimation();
		SyncSleeping();
		if (InputManager.GetInteractReleased(InputManager.m_CurrentContext))
		{
			if (PlayerInteractionWith != null)
			{
				LongActionCanceled(PlayerInteractionWith);
			}
			if (InteractionInprocess && InteractHold)
			{
				LongActionCanceled();
			}
		}
	}

	public static void InputActions()
	{
		if (GameManager.m_PlayerManager == null)
		{
			return;
		}
		PlayerManager playerManager = GameManager.m_PlayerManager;
		if (InputManager.GetKeyDown(InputManager.m_CurrentContext, KeyCode.G))
		{
			FindPlayerToTrade();
			ProcessGivingItem();
		}
		if (InputManager.GetPutBackPressed(InputManager.m_CurrentContext) && !CoopContainerFlow.HandlesContainerInspectInput && playerManager.m_Gear != null && playerManager.m_InspectModeActive)
		{
			GearItem gear = playerManager.m_Gear;
			GameObject gameObject = gear.gameObject;
			int variant = 0;
			if ((gameObject.GetComponent<FoodItem>() != null && gameObject.GetComponent<FoodItem>().m_Opened) || (gameObject.GetComponent<SmashableItem>() != null && gameObject.GetComponent<SmashableItem>().m_HasBeenSmashed))
			{
				variant = 1;
			}
			if (playerManager.m_Container != null)
			{
				MelonLogger.Msg("Refuse taking gear, from container, make it fake");
				SendDropItem(gear, 0, 0, samepose: false, variant);
			}
			else if (gear.gameObject.GetComponent<Comps.DropFakeOnLeave>() != null)
			{
				MelonLogger.Msg("Refuse from picking fake gear, make it fake again");
				DroppedInspectSync.ReturnOnce(gear, variant);
			}
		}
		if (InteractionInprocess)
		{
			InteractTimer -= Time.deltaTime;
			if (ObjectInteractWith == null)
			{
				LongActionCanceled();
				return;
			}
			if (InteractionType == "ExpInt")
			{
				string text = ObjectInteractWith.GetComponent<ObjectGuid>().Get();
				for (int i = 0; i < playersData.Count; i++)
				{
					if (playersData[i] != null && playersData[i].m_BrakingObject != null && text == playersData[i].m_BrakingObject.m_Guid)
					{
						GameAudioManager.PlayGUIError();
						GameCompat.Panel<Panel_GenericProgressBar>().Cancel();
						LongActionCanceled();
						break;
					}
				}
			}
			if ((double)InteractTimer <= 0.0)
			{
				if ((bool)ObjectInteractWith)
				{
					LongActionFinished(ObjectInteractWith, InteractionType);
				}
				LongActionCanceled();
			}
		}
		if (!(Pathes.FakeRockCacheCallback != null))
		{
			return;
		}
		for (int j = 0; j < playersData.Count; j++)
		{
			if (playersData[j] != null)
			{
				if (playersData[j].m_BrakingObject != null && Pathes.FakeRockCacheCallback.m_GUID == playersData[j].m_BrakingObject.m_Guid)
				{
					HUDMessage.AddMessage(playersData[j].m_Name + " IS BREAKING THIS!");
					GameAudioManager.PlayGUIError();
					GameCompat.Panel<Panel_GenericProgressBar>().Cancel();
					break;
				}
				if (playersData[j].m_Container != null && Pathes.FakeRockCacheCallback.m_GUID == playersData[j].m_Container.m_Guid)
				{
					HUDMessage.AddMessage(playersData[j].m_Name + " IS USING THIS!");
					GameAudioManager.PlayGUIError();
					GameCompat.Panel<Panel_GenericProgressBar>().Cancel();
					break;
				}
			}
		}
	}

	private static MelonPreferences_Entry<bool> ItemDecayPreference;

	private static readonly System.Collections.Generic.Dictionary<System.IntPtr, float> OriginalDecayScales = new System.Collections.Generic.Dictionary<System.IntPtr, float>();

	public static void SetTime()
	{
		if (Utils.TryParseTOD(OveridedTime, out var normalizedTOD))
		{
			GameManager.GetTimeOfDayComponent().SetNormalizedTime(normalizedTOD);
		}
		if (GameManager.m_ExperienceModeManager != null)
		{
			Il2CppTLD.Gameplay.ExperienceMode currentExperienceMode = GameManager.m_ExperienceModeManager.GetCurrentExperienceMode();
			if (currentExperienceMode != null)
			{
				if (currentExperienceMode.m_DecayScale > 0f && !OriginalDecayScales.ContainsKey(currentExperienceMode.Pointer))
				{
					OriginalDecayScales[currentExperienceMode.Pointer] = currentExperienceMode.m_DecayScale;
				}
				currentExperienceMode.m_DecayScale = (ItemDecayPreference != null && ItemDecayPreference.Value && OriginalDecayScales.TryGetValue(currentExperienceMode.Pointer, out float decayScale)) ? decayScale : 0f;
			}
		}
	}

	public static void SetAppBackgroundMode()
	{
		if (!Application.runInBackground)
		{
			Application.runInBackground = true;
		}
	}

	public override void OnUpdate()
	{
		SpawnSafety.Tick();
		SetAppBackgroundMode();
		if (!KillOnUpdate)
		{
			ModUpdate();
			Shared.OnUpdate();
			FarTerritory.Update();
		}
		if (!KillEverySecond && Time.time > nextActionTimeSecond)
		{
			nextActionTimeSecond += periodSecond;
			if (!KillEverySecond)
			{
				EverySecond();
				Shared.EverySecond();
			}
		}
		if (!KillEveryInGameMinute && Time.time > nextActionTime)
		{
			nextActionTime += period;
			if (NeedSyncTime)
			{
				EveryInGameMinute();
				if (iAmHost)
				{
					Shared.EveryInGameMinute();
				}
			}
		}
		SanityManager.Update();
	}

	public static void ExpeditionFeedbackReport()
	{
		bool key = KeyboardUtilities.InputManager.GetKey(KeyCode.F3);
		if (key && !InteractionInprocess && !IgnoreFeebackKeyDownUntilReleased)
		{
			IgnoreFeebackKeyDownUntilReleased = true;
			DoLongAction(GameManager.GetPlayerObject(), "Opening form...", "ExpFeedback", 1.5f, Hold: false);
		}
		else if (!key && InteractionInprocess && InteractionType == "ExpFeedback")
		{
			IgnoreFeebackKeyDownUntilReleased = false;
			LongActionCanceled();
		}
		if (IgnoreFeebackKeyDownUntilReleased && !key)
		{
			IgnoreFeebackKeyDownUntilReleased = false;
		}
	}

	public static void AddSemiPrefab(string Name, GameObject CloneTarget)
	{
		if (CloneTarget != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(CloneTarget);
			gameObject.SetActive(value: false);
			MelonLogger.Msg(System.ConsoleColor.Cyan, "SemiPrefab " + Name + " ready!");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			SemiPrefabs.Add(Name, gameObject);
		}
		else
		{
			MelonLogger.Msg(System.ConsoleColor.Red, "Can't register SemiPrefab " + Name + "! Clone target not exist!");
		}
	}

	public static GameObject GetSemiPrefab(string Name)
	{
		if (SemiPrefabs.TryGetValue(Name, out var value))
		{
			return value;
		}
		return null;
	}

	public static void StartDumpGearSpawns()
	{
		sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
		scenes = new string[sceneCount];
		for (int i = 0; i < sceneCount; i++)
		{
			scenes[i] = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
		}
		MelonLogger.Msg(System.ConsoleColor.Cyan, "[DumpGearSpawns] " + sceneCount + " scenes in game, starting dumping...");
		DumpingGearSpawns = true;
	}

	public static void DumpGearSpawns()
	{
		if (!DumpingGearSpawns)
		{
			return;
		}
		if (!DumpOnNextFrame)
		{
			if (CurrentSearchIndex < sceneCount)
			{
				string text = scenes[CurrentSearchIndex];
				MelonLogger.Msg(System.ConsoleColor.Cyan, "[DumpGearSpawns] Preloading scene " + text);
				UnityEngine.SceneManagement.SceneManager.LoadScene(text, LoadSceneMode.Single);
				DumpOnNextFrame = true;
			}
			else
			{
				DumpingGearSpawns = false;
				MelonLogger.Msg(System.ConsoleColor.Cyan, "[DumpGearSpawns] Finished!");
				MPSaveManager.SaveData("IndependantGearSpawners.json", JSON.Dump(AllGearSpawners), 0, "Mods\\IndependantGearSpawners.json");
			}
			return;
		}
		string text2 = scenes[CurrentSearchIndex];
		DataStr.SceneGearSpawners sceneGearSpawners = new DataStr.SceneGearSpawners();
		sceneGearSpawners.SceneName = text2;
		MelonLogger.Msg(System.ConsoleColor.Cyan, "[DumpGearSpawns] Searching on " + text2 + " (" + CurrentSearchIndex + "/" + sceneCount + ")");
		foreach (RandomSpawnObject item in Resources.FindObjectsOfTypeAll<RandomSpawnObject>())
		{
			MelonLogger.Msg(System.ConsoleColor.Gray, "[DumpGearSpawns] RandomSpawnObject");
			DataStr.RandomSpawnObjectDescriptor randomSpawnObjectDescriptor = new DataStr.RandomSpawnObjectDescriptor();
			randomSpawnObjectDescriptor.SpawnOnPiligrim = item.m_NumObjectsToEnablePilgrim;
			randomSpawnObjectDescriptor.SpawnOnVoyageur = item.m_NumObjectsToEnableVoyageur;
			randomSpawnObjectDescriptor.SpawnOnInterloper = item.m_NumObjectsToEnableInterloper;
			for (int i = 0; i < item.m_ObjectList.Count; i++)
			{
				GameObject gameObject = item.m_ObjectList[i];
				if (gameObject != null)
				{
					GearItem component = gameObject.GetComponent<GearItem>();
					if (component != null)
					{
						DataStr.GearSpawnerElement gearSpawnerElement = new DataStr.GearSpawnerElement();
						gearSpawnerElement.GearName = component.gameObject.name;
						gearSpawnerElement.Weight = item.m_Weights[i];
						gearSpawnerElement.Chance = component.m_SpawnChance;
						gearSpawnerElement.Position = item.transform.position;
						gearSpawnerElement.Rotation = item.transform.rotation;
						MelonLogger.Msg(System.ConsoleColor.DarkCyan, "[DumpGearSpawns] GearName " + gearSpawnerElement.GearName + " Weight " + gearSpawnerElement.Weight);
						randomSpawnObjectDescriptor.Gears.Add(gearSpawnerElement);
					}
				}
			}
			sceneGearSpawners.RandomSpawns.Add(randomSpawnObjectDescriptor);
		}
		foreach (PrefabSpawn item2 in Resources.FindObjectsOfTypeAll<PrefabSpawn>())
		{
			MelonLogger.Msg(System.ConsoleColor.Gray, "[DumpGearSpawns] PrefabSpawn");
			DataStr.PrefabSpawnDescriptor prefabSpawnDescriptor = new DataStr.PrefabSpawnDescriptor();
			prefabSpawnDescriptor.ChanceOfNoSpawn = item2.m_ChanceOfNoSpawn;
			prefabSpawnDescriptor.Min = item2.m_NumToSpawnMin;
			prefabSpawnDescriptor.Max = item2.m_NumToSpawnMax;
			foreach (PrefabSpawn.Element prefab2 in item2.m_PrefabList)
			{
				GameObject prefab = prefab2.m_Prefab;
				if (prefab != null)
				{
					GearItem component2 = prefab.GetComponent<GearItem>();
					if (component2 != null)
					{
						DataStr.GearSpawnerElement gearSpawnerElement2 = new DataStr.GearSpawnerElement();
						gearSpawnerElement2.GearName = component2.gameObject.name;
						gearSpawnerElement2.Weight = prefab2.m_SpawnWeight;
						gearSpawnerElement2.Chance = component2.m_SpawnChance;
						gearSpawnerElement2.Position = item2.transform.position + prefab2.m_Offset;
						gearSpawnerElement2.Rotation = Quaternion.Euler(prefab2.m_Rotation);
						MelonLogger.Msg(System.ConsoleColor.DarkCyan, "[DumpGearSpawns] GearName " + gearSpawnerElement2.GearName + " Weight " + gearSpawnerElement2.Weight);
						prefabSpawnDescriptor.Gears.Add(gearSpawnerElement2);
					}
				}
			}
			sceneGearSpawners.PrefabSpawns.Add(prefabSpawnDescriptor);
		}
		AllGearSpawners.GearSpawners.Add(text2, sceneGearSpawners);
		CurrentSearchIndex++;
		DumpOnNextFrame = false;
	}

	public static void PreloadStuff()
	{
		if (DonePreload || !Shared.IsMainMenuScene(level_name))
		{
			return;
		}
		if (!DonePreloadNextFrame)
		{
			MelonLogger.Msg(System.ConsoleColor.Cyan, "Starting preload...");
			try
			{
				UnityEngine.SceneManagement.SceneManager.LoadScene("RuralRegion_STORY", LoadSceneMode.Additive);
			}
			catch (System.Exception ex)
			{
				MelonLogger.Warning("[SkyCoop] Could not preload RuralRegion_STORY: " + ex.Message);
			}
			DonePreloadNextFrame = true;
			return;
		}
		try
		{
			AddSemiPrefab("FireSignal", GameObject.Find("/Art/CommunityHall/OBJ_FireSignal_A_Placed_Prefab"));
			AddSemiPrefab("SnowPileB", GameObject.Find("/Design/Mine SnowPile/TRN_Mine_SnowPile_B_Prefab"));
			AddSemiPrefab("Smoke", GameObject.Find("Art/FX/PlaneCrash/Smoke"));
			AddSemiPrefab("FireSmall", GameObject.Find("Art/FX/PlaneCrash/Fire/FX_FirePoint (2)"));
			AddSemiPrefab("FireTiny", GameObject.Find("Art/FX/PlaneCrash/Fire/FX_FirePoint (5)"));
			AddSemiPrefab("FireLarge", GameObject.Find("Art/FX/PlaneCrash/Fire/FX_FirePoint (7)"));
			AddSemiPrefab("FireLight", GameObject.Find("Art/FX/PlaneCrash/Lighting/Over (2)"));
			AddSemiPrefab("RedLamp", GameObject.Find("Art/CommunityHall/OBJ_StormLantern_Prefab"));
			AddSemiPrefab("RockDummy", GameObject.Find("Art/Story/TRN_RockCliffBig06_ClimbA_Left_Prefab"));
		}
		catch (System.Exception ex2)
		{
			MelonLogger.Warning("[SkyCoop] Preload could not register every semi-prefab: " + ex2.Message);
		}
		finally
		{
			Scene sceneByName = UnityEngine.SceneManagement.SceneManager.GetSceneByName("RuralRegion_STORY");
			if (sceneByName.IsValid() && sceneByName.isLoaded)
			{
				UnityEngine.SceneManagement.SceneManager.UnloadScene("RuralRegion_STORY");
			}
			DonePreload = true;
			MelonLogger.Msg(System.ConsoleColor.Cyan, "Done");
		}
	}

	public void ModUpdate()
	{
		SaveCheck();
		CheatConsole.Tick();
		ItemSpawner.Tick();
		SteamFriendsWindow.Tick();
		RadialSpawnSync.Tick();
		CoopContainerFlow.Tick();
		CoopReturnToMenu.Tick();
		if (!DedicatedServerAppMode)
		{
			if (Flag1)
			{
				UIsWorking();
			}
			if (Flag2)
			{
				DebugCrap();
			}
			if (Flag3)
			{
				CustomWeapons();
			}
			if (Flag4)
			{
				InitAudio();
			}
			if (Flag5)
			{
				ProcessVoice();
			}
			if (Flag6)
			{
				UpdateDeathFlag();
			}
			if (level_name != "Empty" && level_name != "Boot" && !Shared.IsMainMenuScene(level_name))
			{
				if (Flag7)
				{
					UpdateDoll();
				}
				if (Flag8)
				{
					SendPlayerData();
				}
				if (Flag9)
				{
					InputActions();
				}
			}
		}
		else
		{
			DedicatedServerUpdate();
		}
		if (InOnline() && Flag10)
		{
			SetTime();
		}
		if (Flag11)
		{
			ThreadManager.UpdateMain();
		}
		if (Flag12)
		{
			UpdateAPIStates();
		}
		if (Flag13)
		{
			SteamConnect.DoUpdate();
		}
		ExpeditionFeedbackReport();
		PreloadStuff();
		DumpGearSpawns();
	}

	public static void LoadAllOpenableThingsForScene()
	{
		string text = level_guid;
		MelonLogger.Msg(System.ConsoleColor.Blue, "Trying to load openables for scene " + text);
		System.Collections.Generic.Dictionary<string, bool> dictionary = MPSaveManager.LoadOpenableThings(text);
		if (dictionary == null)
		{
			return;
		}
		foreach (System.Collections.Generic.KeyValuePair<string, bool> item in dictionary)
		{
			if (!OpenableThings.ContainsKey(item.Key))
			{
				OpenableThings.Add(item.Key, item.Value);
			}
		}
	}

	public static void LoadAllDropsForScene()
	{
		string text = level_guid;
		MelonLogger.Msg(System.ConsoleColor.Blue, "Trying to load drops for scene " + text);
		Shared.ModifyDynamicGears(text);
		System.Collections.Generic.Dictionary<int, DataStr.DroppedGearItemDataPacket> dictionary = MPSaveManager.LoadDropVisual(text);
		if (MPSaveManager.LoadDropData(text) == null || dictionary == null)
		{
			return;
		}
		foreach (System.Collections.Generic.KeyValuePair<int, DataStr.DroppedGearItemDataPacket> item in dictionary)
		{
			Shared.FakeDropItem(item.Value, JustLoad: true);
		}
	}

	public static void SetFixedSpawn()
	{
		if (ServerConfig.m_PlayersSpawnType == 3)
		{
			MelonLogger.Msg("[FixedPlaceSpawns] SetFixedSpawn()");
			if (!FixedPlaceLoaded)
			{
				SavedSceneForSpawn = level_name;
				SavedPositionForSpawn = GameManager.GetPlayerTransform().position;
				MelonLogger.Msg("[FixedPlaceSpawns] Isn't saved yet, set current position");
			}
			MelonLogger.Msg("[FixedPlaceSpawns] Scene: " + SavedSceneForSpawn + " position X " + SavedPositionForSpawn.x + " Y " + SavedPositionForSpawn.y + " Z " + SavedPositionForSpawn.z);
		}
	}

	public static void DoIPConnectWindow()
	{
		if (!sendMyPosition)
		{
			if (SteamServerWorks == "")
			{
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "Input server address", "127.0.0.1", Panel_Confirmation.ButtonLayout.Button_2, "Connect", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
			}
			else if (SteamConnect.CanUseSteam)
			{
				SteamConnect.Main.JoinToLobby(SteamServerWorks);
			}
		}
		else
		{
			HUDMessage.AddMessage("YOU ALREADY CONNECTED!!!!");
		}
	}

	public override void OnGUI()
	{
		DeBugMenu.Render();
		CoopContainerFlow.Draw();
		CoopReturnToMenu.Draw();
		CheatConsole.Draw();
		ItemSpawner.Draw();
		SteamFriendsWindow.Draw();
	}

	public static void SetRevivedStats(bool health = true)
	{
		if (health)
		{
			GameManager.GetConditionComponent().m_CurrentHP = 25f;
		}
		IsDead = false;
		GameManager.GetPlayerManagerComponent().m_Ghost = false;
		GameManager.GetFatigueComponent().m_CurrentFatigue = 25f;
		GameManager.GetThirstComponent().m_CurrentThirst = 25f;
		GameManager.GetHungerComponent().m_CurrentReserveCalories = GameManager.GetHungerComponent().m_MaxReserveCalories / 4f;
	}

	public static bool RollChanceSeeded(float percent, System.Random RNG)
	{
		if (!Utils.IsZero(percent))
		{
			if (!Utils.Approximately(percent, 100f))
			{
				return (double)RNG.Next(0, 100) < (double)percent;
			}
			return true;
		}
		return false;
	}

	public static string GenerateSeededGUID(int gameSeed, Vector3 v3)
	{
		int num = (int)v3.x;
		int num2 = (int)v3.y;
		int num3 = (int)v3.z;
		int num4 = num + num2 + num3;
		int seed = gameSeed + num4;
		string text = "abcdefghijklmnopqrstuvwxyz1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		System.Random random = new System.Random(seed);
		string text2 = "";
		for (int i = 1; i < 36; i++)
		{
			if (i == 9 || i == 14 || i == 19 || i == 24)
			{
				text2 += "-";
			}
			int index = random.Next(0, text.Length);
			text2 += text[index];
		}
		return text2;
	}

	public static void AddLocksToDoorsByGUID(string GUID)
	{
		Il2CppSystem.Collections.Generic.List<LoadScene>.Enumerator enumerator = LoadScene.m_LoadScenesList.GetEnumerator();
		while (enumerator.MoveNext())
		{
			LoadScene current = enumerator.Current;
			if (current.m_GUID == GUID)
			{
				AddLock(current);
			}
		}
	}

	public static void EnterDoorsByGUID(string GUID)
	{
		Il2CppSystem.Collections.Generic.List<LoadScene>.Enumerator enumerator = LoadScene.m_LoadScenesList.GetEnumerator();
		while (enumerator.MoveNext())
		{
			LoadScene current = enumerator.Current;
			if (current.m_GUID == GUID)
			{
				current.Activate();
				break;
			}
		}
	}

	public static void EnterDoorsByScene(string Scene)
	{
		Il2CppSystem.Collections.Generic.List<LoadScene>.Enumerator enumerator = LoadScene.m_LoadScenesList.GetEnumerator();
		while (enumerator.MoveNext())
		{
			LoadScene current = enumerator.Current;
			if (current.m_SceneToLoad + "_" + current.m_GUID == Scene)
			{
				current.Activate();
				break;
			}
		}
	}

	public static void RemoveLocksFromDoorsByGUID(string GUID)
	{
		Il2CppSystem.Collections.Generic.List<LoadScene>.Enumerator enumerator = LoadScene.m_LoadScenesList.GetEnumerator();
		while (enumerator.MoveNext())
		{
			LoadScene current = enumerator.Current;
			if (current.m_GUID == GUID)
			{
				if ((bool)current.Lock)
				{
					UnityEngine.Object.Destroy(current.gameObject.GetComponent<Lock>());
				}
				if ((bool)current.gameObject.GetComponent<HoverIconsToShow>())
				{
					UnityEngine.Object.Destroy(current.gameObject.GetComponent<HoverIconsToShow>());
				}
				if ((bool)current.gameObject.GetComponent<Comps.DoorLockedOnKey>())
				{
					UnityEngine.Object.Destroy(current.gameObject.GetComponent<Comps.DoorLockedOnKey>());
				}
			}
		}
	}

	public static void TryLockDoor(LoadScene Door, string KeySeed)
	{
		if (!Door || !Door.m_SceneCanBeInstanced || string.IsNullOrEmpty(Door.m_GUID) || !Door.m_Active)
		{
			return;
		}
		GameAudioManager.PlaySound("PLAY_SNDMECHSAFETUMBLERFALL", InterfaceManager.GetSoundEmitter());
		if (iAmHost)
		{
			TryingLockDoor(Door, KeySeed, level_guid);
		}
		else if (sendMyPosition)
		{
			DoPleaseWait("Please wait...", "Locking door...");
			SetRepeatPacket(ResendPacketType.Cancel, 10);
			using Packet packet = new Packet(131);
			packet.Write(Door.GetSceneToLoad() + "_" + Door.m_GUID);
			packet.Write(KeySeed);
			packet.Write(level_guid);
			SendUDPData(packet);
		}
	}

	public static bool AddLock(LoadScene Door)
	{
		GameObject gameObject = Door.gameObject;
		if ((bool)gameObject)
		{
			Lock obj = (Door.Lock = gameObject.AddComponent<Lock>());
			obj.m_LockStateRolled = true;
			obj.m_LockState = LockState.Locked;
			obj.m_BreakOnUse = false;
			obj.m_ModeFilter = GameModeFilter.All;
			obj.m_HideToolRequiredToForceOpenHintText = false;
			obj.m_GearPrefabToForceLock = null;
			obj.m_LockedAudio = "Play_SndMechDoorWoodLocked01";
			gameObject.AddComponent<Comps.DoorLockedOnKey>().m_DoorKey = Door.GetSceneToLoad() + "_" + Door.m_GUID;
			return true;
		}
		return false;
	}

	public static void BeginRegisterKey(GameObject key, bool Force = false)
	{
		if ((bool)key)
		{
			PendingRegisterKey = key;
		}
		if (!Force && !GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_SharpeningStone"))
		{
			HUDMessage.AddMessage("You need whetstone!");
			GameAudioManager.PlayGUIError();
		}
		else
		{
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "Input secret seed for key", "", Panel_Confirmation.ButtonLayout.Button_2, "Next", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
		}
	}

	public static void CreateDebugKey()
	{
		GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "DATA FOR DEBUG KEY", "", Panel_Confirmation.ButtonLayout.Button_2, "CRAFT", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
	}

	public static void TryingLockDoor(LoadScene Door, string KeySeed, string Scene)
	{
		string doorKey = Door.GetSceneToLoad() + "_" + Door.m_GUID;
		switch (MPSaveManager.AddLockedDoor(Scene, doorKey, KeySeed))
		{
		case MPSaveManager.UseKeyStatus.Done:
			ServerSend.ADDDOORLOCK(-1, Door.m_GUID, Scene);
			AddLocksToDoorsByGUID(Door.m_GUID);
			break;
		case MPSaveManager.UseKeyStatus.KeyUsed:
			HUDMessage.AddMessage("This key already used for other door!");
			break;
		case MPSaveManager.UseKeyStatus.DoorAlreadyLocked:
			HUDMessage.AddMessage("This door is already locked!");
			break;
		}
	}

	public static void SendRegularAlignData()
	{
		if (level_name == "Empty" || level_name == "Boot" || Shared.IsMainMenuScene(level_name) || LoadingScreenIsOn)
		{
			return;
		}
		Vector3 position = GameManager.GetPlayerTransform().position;
		Quaternion quaternion = GameManager.GetPlayerTransform().rotation;
		if (GameManager.GetRestComponent().IsSleeping() && GameManager.GetRestComponent().m_Bed != null)
		{
			Bed bed = GameManager.GetRestComponent().m_Bed;
			if (bed != null)
			{
				quaternion = GetBedRotation(bed);
				position = bed.m_BodyPlacementTransform.position;
			}
		}
		if (iAmHost)
		{
			ServerSend.XYZ(0, position, toEveryOne: true);
			ServerSend.XYZW(0, quaternion, toEveryOne: true);
			ServerSend.LEVELID(0, levelid, toEveryOne: true);
			ServerSend.LEVELGUID(0, level_guid, toEveryOne: true);
			ServerSend.ANIMSTATE(0, MyAnimState, toEveryOne: true);
		}
		if (!sendMyPosition)
		{
			return;
		}
		using (Packet packet = new Packet(2))
		{
			packet.Write(position);
			SendUDPData(packet);
		}
		using (Packet packet2 = new Packet(3))
		{
			packet2.Write(quaternion);
			SendUDPData(packet2);
		}
		using (Packet packet3 = new Packet(6))
		{
			packet3.Write(levelid);
			SendUDPData(packet3);
		}
		using (Packet packet4 = new Packet(48))
		{
			packet4.Write(level_guid);
			SendUDPData(packet4);
		}
		using Packet packet5 = new Packet(11);
		packet5.Write(MyAnimState);
		SendUDPData(packet5);
	}

	public static void SendSpawnData(bool AskResponce = false)
	{
		if (DedicatedServerAppMode || LoadingScreenIsOn || level_name == "Empty" || level_name == "Boot" || Shared.IsMainMenuScene(level_name) || GameManager.GetPlayerTransform() == null)
		{
			return;
		}
		if (!AskResponce)
		{
			MelonLogger.Msg("Sending my spawn data to all players");
		}
		else
		{
			MelonLogger.Msg("Sending spawn data as responce");
		}
		DataStr.PlayerClothingData playerClothingData = new DataStr.PlayerClothingData();
		playerClothingData.m_Hat = MyHat;
		playerClothingData.m_Top = MyTop;
		playerClothingData.m_Bottom = MyBottom;
		playerClothingData.m_Boots = MyBoots;
		int voicePersona = (int)PlayerManager.m_VoicePersona;
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(72))
			{
				packet.Write(voicePersona);
				SendUDPData(packet);
			}
			using (Packet packet2 = new Packet(6))
			{
				packet2.Write(levelid);
				SendUDPData(packet2);
			}
			using (Packet packet3 = new Packet(48))
			{
				packet3.Write(level_guid);
				SendUDPData(packet3);
			}
			SendMyEQ();
			using (Packet packet4 = new Packet(9))
			{
				packet4.Write(MyLightSource);
				SendUDPData(packet4);
			}
			using (Packet packet5 = new Packet(10))
			{
				packet5.Write(MyLightSourceName);
				SendUDPData(packet5);
			}
			using (Packet packet6 = new Packet(2))
			{
				packet6.Write(GameManager.GetPlayerTransform().position);
				SendUDPData(packet6);
			}
			using (Packet packet7 = new Packet(3))
			{
				packet7.Write(GameManager.GetPlayerTransform().rotation);
				SendUDPData(packet7);
			}
			using (Packet packet8 = new Packet(11))
			{
				packet8.Write(MyAnimState);
				SendUDPData(packet8);
			}
			using (Packet packet9 = new Packet(46))
			{
				packet9.Write(playerClothingData);
				SendUDPData(packet9);
			}
			if (AskResponce)
			{
				using Packet packet10 = new Packet(47);
				packet10.Write(levelid);
				SendUDPData(packet10);
			}
		}
		if (iAmHost)
		{
			SendMyEQ();
			ServerSend.SELECTEDCHARACTER(0, voicePersona, toEveryOne: true);
			ServerSend.LIGHTSOURCE(0, MyLightSource, toEveryOne: true);
			ServerSend.LIGHTSOURCENAME(0, MyLightSourceName, toEveryOne: true);
			ServerSend.XYZ(0, GameManager.GetPlayerTransform().position, toEveryOne: true);
			ServerSend.XYZW(0, GameManager.GetPlayerTransform().rotation, toEveryOne: true);
			ServerSend.ANIMSTATE(0, MyAnimState, toEveryOne: true);
			ServerSend.LEVELID(0, levelid, toEveryOne: true);
			ServerSend.LEVELGUID(0, level_guid, toEveryOne: true);
			ServerSend.CLOTH(0, playerClothingData, toEveryOne: true);
			if (AskResponce)
			{
				ServerSend.ASKSPAWNDATA(0, levelid, toEveryOne: true);
			}
		}
	}

	public static void DoSteamWorksConnect(string sid)
	{
		ClientUser.PendingConnectionIp = SteamServerWorks;
		using Packet packet = new Packet(44);
		packet.Write(sid);
		SendUDPData(packet);
	}

	public static bool CanSleep(bool domessage)
	{
		Panel_Rest panel_Rest = GameCompat.Panel<Panel_Rest>();
		if (panel_Rest == null)
		{
			return false;
		}
		if (GameManager.m_BlockAbilityToRest)
		{
			if (string.IsNullOrEmpty(GameManager.m_BlockedRestLocID))
			{
				return false;
			}
			if (domessage)
			{
				GameAudioManager.PlayGUIError();
				HUDMessage.AddMessage(Localization.Get(GameManager.m_BlockedRestLocID));
			}
			return false;
		}
		if (GameManager.GetCabinFeverComponent().HasCabinFever() && GameManager.GetPlayerManagerComponent().InHibernationPreventionIndoorEnvironment())
		{
			if (domessage)
			{
				GameAudioManager.PlayGUIError();
				HUDMessage.AddMessage(Localization.Get("GAMEPLAY_NoSleepIndoorsCabinFever"));
			}
			return false;
		}
		if (!GameManager.GetRestComponent().AllowUnlimitedSleep() && (double)GameManager.GetFatigueComponent().m_CurrentFatigue <= (double)panel_Rest.m_AllowRestFatigueThreshold && !GameManager.GetRestComponent().RestNeededForAffliction())
		{
			if (domessage)
			{
				GameAudioManager.PlayGUIError();
				HUDMessage.AddMessage(Localization.Get("GAMEPLAY_YouAreNotTired"));
			}
			return false;
		}
		if (GameManager.GetIceCrackingManager().IsInsideTrigger())
		{
			if (domessage)
			{
				GameAudioManager.PlayGUIError();
				HUDMessage.AddMessage(Localization.Get("GAMEPLAY_NoRestOnThinIce"));
			}
			return false;
		}
		return true;
	}

	public static string GetClothForSlot(ClothingRegion s, ClothingLayer l)
	{
		if (GameManager.m_PlayerManager == null || GameManager.m_PlayerManager.m_Clothing == null)
		{
			return "";
		}
		GearItem clothingInSlot = GameManager.GetPlayerManagerComponent().GetClothingInSlot(s, l);
		if (clothingInSlot != null)
		{
			string gearName = clothingInSlot.GetGearName();
			string text = "";
			if (gearName.Contains("(Clone)"))
			{
				int startIndex = gearName.Length - 7;
				return gearName.Remove(startIndex, 7);
			}
			return gearName;
		}
		return "";
	}

	public static string GetClothToDisplay(string bottom, string mid, string top = "", string top2 = "")
	{
		if (top == "" && top2 == "")
		{
			if (mid == "")
			{
				return bottom;
			}
			return mid;
		}
		if (top2 != "")
		{
			return top2;
		}
		if (top != "")
		{
			return top;
		}
		if (mid != "")
		{
			return mid;
		}
		if (bottom != "")
		{
			return bottom;
		}
		return "";
	}

	public static bool IsScarf(string name)
	{
		if (name.Contains("Scarf"))
		{
			return true;
		}
		return false;
	}

	public static bool IsBalaclava(string name)
	{
		if (name.Contains("Balaclava"))
		{
			return true;
		}
		return false;
	}

	public static void HostMenuClose()
	{
		if (UIHostMenu != null)
		{
			UnityEngine.Object.Destroy(UIHostMenu);
			HostMenuHints = new System.Collections.Generic.List<GameObject>();
		}
		if ((bool)m_Panel_Sandbox && m_Panel_Sandbox.isActiveAndEnabled)
		{
			MenuChange.MenuMode = "Multiplayer";
			Transform parent = GameCompat.ResolveChildPath(m_Panel_Sandbox, 0, 0, 5);
			GameCompat.SetChildActive(parent, 1, state: true);
			GameCompat.SetChildActive(parent, 2, state: true);
			GameCompat.SetChildActive(parent, 4, state: true);
			GameCompat.SetChildActive(parent, 5, state: true);
		}
	}

	public static void HostMenuHost()
	{
		bool duppedSpawns;
		bool duppedContainers;
		int playersSpawnType;
		int maxPlayers;
		bool flag;
		int num;
		int fireSync;
		int cheatsMode;
		int num2;
		if (UIHostMenu == null)
		{
			DataStr.ServerSettingsData serverSettingsData = MPSaveManager.RequestServerCFG();
			if (serverSettingsData == null)
			{
				serverSettingsData = new DataStr.ServerSettingsData();
			}
			duppedSpawns = serverSettingsData.m_CFG.m_DuppedSpawns;
			duppedContainers = serverSettingsData.m_CFG.m_DuppedContainers;
			playersSpawnType = serverSettingsData.m_CFG.m_PlayersSpawnType;
			maxPlayers = serverSettingsData.m_MaxPlayers;
			flag = serverSettingsData.m_P2P;
			num = serverSettingsData.m_Port;
			fireSync = serverSettingsData.m_CFG.m_FireSync;
			cheatsMode = serverSettingsData.m_CFG.m_CheatsMode;
			num2 = serverSettingsData.m_Accessibility;
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[SkyCoop] No host settings panel (the asset bundle did not load), hosting with the saved settings instead:");
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[SkyCoop]   max players " + maxPlayers + ", port " + num + ", " + (flag ? "Steam lobby" : "direct IP"));
		}
		else
		{
			GameObject gameObject = UIHostMenu.transform.GetChild(0).gameObject;
			GameObject gameObject2 = UIHostMenu.transform.GetChild(1).gameObject;
			duppedSpawns = gameObject.GetComponent<Toggle>().isOn;
			duppedContainers = gameObject2.GetComponent<Toggle>().isOn;
			playersSpawnType = UIHostMenu.transform.GetChild(2).gameObject.GetComponent<Dropdown>().m_Value;
			maxPlayers = UIHostMenu.transform.GetChild(3).gameObject.GetComponent<Dropdown>().m_Value + 2;
			flag = UIHostMenu.transform.GetChild(4).gameObject.GetComponent<Toggle>().isOn;
			num = System.Convert.ToInt32(UIHostMenu.transform.GetChild(8).gameObject.GetComponent<InputField>().text);
			fireSync = UIHostMenu.transform.GetChild(9).gameObject.GetComponent<Dropdown>().m_Value;
			cheatsMode = UIHostMenu.transform.GetChild(10).gameObject.GetComponent<Dropdown>().m_Value;
			num2 = UIHostMenu.transform.GetChild(11).gameObject.GetComponent<Dropdown>().m_Value;
		}
		ServerConfig.m_DuppedSpawns = duppedSpawns;
		ServerConfig.m_DuppedContainers = duppedContainers;
		ServerConfig.m_PlayersSpawnType = playersSpawnType;
		ServerConfig.m_FireSync = fireSync;
		ServerConfig.m_CheatsMode = cheatsMode;
		ServerConfig.m_PVP = true;
		MaxPlayers = maxPlayers;
		ApplyOtherCampfires = true;
		Shared.InitAllPlayers();
		if (m_Panel_Sandbox != null)
		{
			Transform parent = GameCompat.ResolveChildPath(m_Panel_Sandbox, 0, 0, 5);
			GameCompat.SetChildActive(parent, 1, state: true);
			GameCompat.SetChildActive(parent, 2, state: true);
			GameCompat.SetChildActive(parent, 4, state: true);
			GameCompat.SetChildActive(parent, 5, state: true);
		}
		if (flag && !SteamConnect.CanUseSteam)
		{
			MelonLogger.Warning("[SkyCoop] Steam hosting was requested but Steam is not available; hosting on port " + num + " instead. Others join by IP.");
			flag = false;
		}
		if (!flag)
		{
			StartServerAfterSelectSave = true;
			PortsToHostOn = num;
			MenuChange.ChangeMenuItems("LobbySettings");
		}
		else
		{
			SteamConnect.Main.MakeLobby(num2, MaxPlayers);
			MenuChange.ChangeMenuItems("Lobby");
		}
		if (UIHostMenu != null)
		{
			UnityEngine.Object.Destroy(UIHostMenu);
		}
		HostMenuHints = new System.Collections.Generic.List<GameObject>();
		GameManager.m_IsPaused = false;
		MPSaveManager.SaveServerCFG(new DataStr.ServerSettingsData
		{
			m_CFG = ServerConfig,
			m_MaxPlayers = MaxPlayers,
			m_P2P = flag,
			m_Accessibility = num2,
			m_Port = num
		});
	}

	public static void ShowNotiy()
	{
		if (m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			string titleLocID = "YOU GOT A NEW FLAIR!";
			if (int.Parse(NotificationString) > 1)
			{
				titleLocID = "YOU GOT NEW FLAIRS!";
			}
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Confirm, titleLocID, "\nOpen flairs customization menu?", Panel_Confirmation.ButtonLayout.Button_2, Panel_Confirmation.Background.Transperent, null, null);
		}
	}

	public static void OpenHint(GameObject obj)
	{
		if (!obj.activeSelf)
		{
			for (int i = 0; i < HostMenuHints.Count; i++)
			{
				HostMenuHints[i].SetActive(value: false);
			}
			obj.SetActive(value: true);
		}
		else
		{
			obj.SetActive(value: false);
		}
	}

	public static void AddHintScript(GameObject obj)
	{
		if (obj == null)
		{
			MelonLogger.Msg("[AddHintScript] Got null object. Why?");
			return;
		}
		GameObject gameObject = obj.transform.GetChild(0).gameObject;
		if (gameObject == null)
		{
			MelonLogger.Msg("[AddHintScript] " + obj.name + " have not hint button!");
			return;
		}
		GameObject hintObj = gameObject.transform.GetChild(0).gameObject;
		if (hintObj == null)
		{
			MelonLogger.Msg("[AddHintScript] " + obj.name + " have not hint panel!");
			return;
		}
		HostMenuHints.Add(hintObj);
		System.Action action = delegate
		{
			OpenHint(hintObj);
		};
		gameObject.GetComponent<Button>().onClick.AddListener(action);
	}

	public static void HostMenu(bool Ignore = false)
	{
		if (!Ignore && (CantBeUsedForMP || LastLoadedGenVersion != 5))
		{
			MelonLogger.Msg("CantBeUsedForMP " + CantBeUsedForMP + " LastLoadedGenVersion " + LastLoadedGenVersion + " RandomGenVersion " + 5);
		}
		else
		{
			if (!(UIHostMenu == null))
			{
				return;
			}
			if (UiCanvas != null)
			{
				UIHostMenu = MakeModObject("MP_HostSettings", UiCanvas.transform);
			}
			if (UIHostMenu == null)
			{
				MelonLogger.Msg(System.ConsoleColor.Yellow, "[SkyCoop] Host settings panel unavailable, hosting directly.");
				HostMenuHost();
				return;
			}
			DataStr.ServerSettingsData serverSettingsData = MPSaveManager.RequestServerCFG();
			if (serverSettingsData != null)
			{
				ServerConfig = serverSettingsData.m_CFG;
			}
			else
			{
				serverSettingsData = new DataStr.ServerSettingsData();
			}
			GameObject gameObject = UIHostMenu.transform.GetChild(6).gameObject;
			System.Action action = delegate
			{
				HostMenuClose();
			};
			gameObject.GetComponent<Button>().onClick.AddListener(action);
			GameObject gameObject2 = UIHostMenu.transform.GetChild(7).gameObject;
			System.Action action2 = delegate
			{
				HostMenuHost();
			};
			gameObject2.GetComponent<Button>().onClick.AddListener(action2);
			GameObject gameObject3 = UIHostMenu.transform.GetChild(0).gameObject;
			GameObject gameObject4 = UIHostMenu.transform.GetChild(1).gameObject;
			gameObject3.GetComponent<Toggle>().Set(ServerConfig.m_DuppedSpawns);
			gameObject4.GetComponent<Toggle>().Set(ServerConfig.m_DuppedContainers);
			GameObject gameObject5 = UIHostMenu.transform.GetChild(2).gameObject;
			gameObject5.GetComponent<Dropdown>().Set(ServerConfig.m_PlayersSpawnType);
			GameObject gameObject6 = UIHostMenu.transform.GetChild(4).gameObject;
			GameObject gameObject7 = UIHostMenu.transform.GetChild(8).gameObject;
			GameObject gameObject8 = UIHostMenu.transform.GetChild(9).gameObject;
			GameObject gameObject9 = UIHostMenu.transform.GetChild(10).gameObject;
			GameObject gameObject10 = UIHostMenu.transform.GetChild(11).gameObject;
			gameObject8.GetComponent<Dropdown>().Set(ServerConfig.m_FireSync);
			gameObject9.GetComponent<Dropdown>().Set(ServerConfig.m_CheatsMode);
			if (!SteamConnect.CanUseSteam)
			{
				gameObject6.GetComponent<Toggle>().Set(value: false);
				gameObject6.SetActive(value: false);
				gameObject10.SetActive(value: false);
				gameObject7.SetActive(value: true);
			}
			else
			{
				gameObject6.GetComponent<Toggle>().Set(serverSettingsData.m_P2P);
				if (serverSettingsData.m_P2P)
				{
					gameObject10.SetActive(value: true);
					gameObject10.GetComponent<Dropdown>().Set(serverSettingsData.m_Accessibility);
					gameObject7.SetActive(value: false);
				}
				else
				{
					gameObject10.SetActive(value: false);
					gameObject7.SetActive(value: true);
					gameObject7.GetComponent<InputField>().SetText(serverSettingsData.m_Port.ToString());
				}
			}
			AddHintScript(gameObject3);
			AddHintScript(gameObject4);
			AddHintScript(gameObject5);
			AddHintScript(gameObject8);
			AddHintScript(gameObject6);
			AddHintScript(gameObject7);
			AddHintScript(gameObject9);
			AddHintScript(gameObject10);
		}
	}

	public static void UpdateMyClothing()
	{
		string text = "";
		string text2 = "";
		string text3 = "";
		string text4 = "";
		string myScarf = "";
		string myBalaclava = "";
		bool flag = true;
		if (GameManager.m_PlayerManager == null || GameManager.m_Inventory == null)
		{
			return;
		}
		string clothForSlot = GetClothForSlot(ClothingRegion.Head, ClothingLayer.Base);
		string clothForSlot2 = GetClothForSlot(ClothingRegion.Head, ClothingLayer.Mid);
		string bottom = "";
		string mid = "";
		if (IsBalaclava(clothForSlot))
		{
			myBalaclava = clothForSlot;
		}
		else if (IsScarf(clothForSlot))
		{
			myScarf = clothForSlot;
		}
		else
		{
			bottom = clothForSlot;
		}
		if (IsScarf(clothForSlot2))
		{
			myScarf = clothForSlot2;
		}
		else
		{
			mid = clothForSlot2;
		}
		text = GetClothToDisplay(bottom, mid);
		string clothForSlot3 = GetClothForSlot(ClothingRegion.Chest, ClothingLayer.Base);
		string clothForSlot4 = GetClothForSlot(ClothingRegion.Chest, ClothingLayer.Mid);
		string clothForSlot5 = GetClothForSlot(ClothingRegion.Chest, ClothingLayer.Top);
		string clothForSlot6 = GetClothForSlot(ClothingRegion.Chest, ClothingLayer.Top2);
		text2 = GetClothToDisplay(clothForSlot3, clothForSlot4, clothForSlot5, clothForSlot6);
		GetClothForSlot(ClothingRegion.Hands, ClothingLayer.Base);
		string clothForSlot7 = GetClothForSlot(ClothingRegion.Legs, ClothingLayer.Base);
		string clothForSlot8 = GetClothForSlot(ClothingRegion.Legs, ClothingLayer.Mid);
		string clothForSlot9 = GetClothForSlot(ClothingRegion.Legs, ClothingLayer.Top);
		string clothForSlot10 = GetClothForSlot(ClothingRegion.Legs, ClothingLayer.Top2);
		text3 = GetClothToDisplay(clothForSlot7, clothForSlot8, clothForSlot9, clothForSlot10);
		string clothForSlot11 = GetClothForSlot(ClothingRegion.Feet, ClothingLayer.Base);
		string clothForSlot12 = GetClothForSlot(ClothingRegion.Feet, ClothingLayer.Mid);
		string clothForSlot13 = GetClothForSlot(ClothingRegion.Feet, ClothingLayer.Top);
		text4 = GetClothToDisplay(clothForSlot11, clothForSlot12, clothForSlot13);
		if (!flag)
		{
			return;
		}
		MyHat = text;
		MyTop = text2;
		MyBottom = text3;
		MyBoots = text4;
		MyScarf = myScarf;
		MyBalaclava = myBalaclava;
		DataStr.PlayerClothingData playerClothingData = new DataStr.PlayerClothingData();
		playerClothingData.m_Hat = MyHat;
		playerClothingData.m_Top = MyTop;
		playerClothingData.m_Bottom = MyBottom;
		playerClothingData.m_Boots = MyBoots;
		playerClothingData.m_Scarf = MyScarf;
		playerClothingData.m_Balaclava = MyBalaclava;
		if (sendMyPosition)
		{
			using Packet packet = new Packet(46);
			packet.Write(playerClothingData);
			SendUDPData(packet);
		}
		if (!iAmHost)
		{
			return;
		}
		using (new Packet(46))
		{
			ServerSend.CLOTH(0, playerClothingData, toEveryOne: true);
		}
	}

	public static BoardGameSession GetBoardGameSession(string GUID)
	{
		BoardGameSession item = new BoardGameSession(GUID, "");
		if (BoardGamesSessions.Contains(item))
		{
			return null;
		}
		for (int i = 0; i < BoardGamesSessions.Count; i++)
		{
			if (BoardGamesSessions[i] != null && BoardGamesSessions[i].m_GUID == GUID)
			{
				return BoardGamesSessions[i];
			}
		}
		return null;
	}

	public static void StartBoardGameSessions(string GameType, string GUID)
	{
		if (GetBoardGameSession(GUID) == null)
		{
			BoardGamesSessions.Add(new BoardGameSession(GUID, GameType));
		}
		else
		{
			MelonLogger.Msg("Boardgame session with GUID " + GUID + " already exist");
		}
	}

	public static void CreateCairnsSearchList()
	{
		Il2CppArrayBase<Cairn> il2CppArrayBase = Resources.FindObjectsOfTypeAll<Cairn>();
		PossibleCairns = new System.Collections.Generic.Dictionary<int, Vector3>();
		for (int i = 0; i < il2CppArrayBase.Count; i++)
		{
			if ((bool)il2CppArrayBase[i].gameObject)
			{
				Vector3 position = il2CppArrayBase[i].gameObject.transform.position;
				int journalEntryNumber = il2CppArrayBase[i].m_JournalEntryNumber;
				if (!FoundCairns.ContainsKey(journalEntryNumber))
				{
					PossibleCairns.Add(journalEntryNumber, position);
				}
			}
		}
	}

	public static void RemoveCairnFromSearch(int ID)
	{
		if (PossibleCairns.ContainsKey(ID))
		{
			PossibleCairns.Remove(ID);
			DoRadioSearch();
		}
	}

	public static float GetDistanceToCairn(Vector3 playerPos)
	{
		float num = float.PositiveInfinity;
		foreach (System.Collections.Generic.KeyValuePair<int, Vector3> possibleCairn in PossibleCairns)
		{
			float num2 = Vector3.Distance(playerPos, possibleCairn.Value);
			if (num2 < MaxRadioSearchDistance && num > num2)
			{
				num = num2;
			}
		}
		return num;
	}

	public static float GetRadioBeepRate(float Distance)
	{
		float num = Distance / BlinkRateConstant;
		if (num < MinimalBlink)
		{
			num = MinimalBlink;
		}
		return num;
	}

	public static void AddFoundCairn(int CairnID, bool sync = true)
	{
		if (!FoundCairns.ContainsKey(CairnID))
		{
			FoundCairns.Add(CairnID, value: true);
			RemoveCairnFromSearch(CairnID);
		}
		if (CurrentCustomChalleng.m_Started && CurrentChallengeRules.m_Name == "Lost in action" && CurrentCustomChalleng.m_CurrentTask == 0)
		{
			if (iAmHost)
			{
				CurrentCustomChalleng.m_Done[0] = FoundCairns.Count;
			}
			if (sync)
			{
				SendCustomChallengeTrigger("CairnID" + CairnID);
			}
		}
	}

	public static DataStr.CustomChallengeRules GetCustomChallengeByID(int ID)
	{
		DataStr.CustomChallengeRules customChallengeRules = new DataStr.CustomChallengeRules();
		switch (ID)
		{
		case 1:
			customChallengeRules.m_Name = "Cold Allergic";
			customChallengeRules.m_Description = "This island is worst nightmare for you. Getting crash on frozen hell with having Cold urticaria, how ironic.";
			customChallengeRules.m_Tasks.Add(new DataStr.CustomChallengeTaskData("Get to the Desolation Point without getting risk of Hypothermia", 5400, 1));
			customChallengeRules.m_Tasks.Add(new DataStr.CustomChallengeTaskData("Enter any building", 600, 1));
			customChallengeRules.m_Lineal = true;
			customChallengeRules.m_CompetitiveMode = false;
			customChallengeRules.m_ID = 1;
			break;
		case 2:
			customChallengeRules.m_Name = "Lost in action";
			customChallengeRules.m_Description = "Find cairn of poor souls who had bad luck to end up here for forever.";
			customChallengeRules.m_Tasks.Add(new DataStr.CustomChallengeTaskData("Find cairns", 5400, 30));
			customChallengeRules.m_Tasks.Add(new DataStr.CustomChallengeTaskData("Meet with other survivors on Camp Office", 2200, 1));
			customChallengeRules.m_Lineal = true;
			customChallengeRules.m_CompetitiveMode = false;
			customChallengeRules.m_ID = 2;
			break;
		}
		return customChallengeRules;
	}

	public static void LoadCustomChallenge(int ID, int CurrentTask)
	{
		DataStr.CustomChallengeRules customChallengeRules = (CurrentChallengeRules = GetCustomChallengeByID(ID));
		CurrentCustomChalleng.m_CurrentTask = CurrentTask;
		CurrentCustomChalleng.m_Time = customChallengeRules.m_Tasks[CurrentTask].m_Timer;
		CurrentCustomChalleng.m_Started = false;
		CurrentCustomChalleng.m_Done = new System.Collections.Generic.List<int>();
		for (int i = 0; i < customChallengeRules.m_Tasks.Count; i++)
		{
			CurrentCustomChalleng.m_Done.Add(0);
		}
		if (iAmHost)
		{
			ServerSend.CHALLENGEINIT(ID, CurrentTask);
		}
	}

	public static void StartCustomChallenge()
	{
		CurrentCustomChalleng.m_Started = true;
	}

	public static void NextCustomTask()
	{
		CurrentCustomChalleng.m_CurrentTask++;
		CurrentCustomChalleng.m_Time = CurrentChallengeRules.m_Tasks[CurrentCustomChalleng.m_CurrentTask].m_Timer;
	}

	public static void CustomChallengeUpdate()
	{
		if (CurrentChallengeRules.m_Name == "Cold Allergic")
		{
			if (CurrentCustomChalleng.m_CurrentTask == 0)
			{
				if (RegionCompat.GetCurrentRegion() == Shared.GameRegion.DesolationPoint)
				{
					NextCustomTask();
				}
			}
			else if (CurrentCustomChalleng.m_CurrentTask == 1 && !GameManager.IsOutDoorsScene(level_name) && !level_name.Contains("Cave") && !level_name.Contains("Mine"))
			{
				CurrentCustomChalleng.m_Started = false;
				ProcessCustomChallengeTrigger("Win");
				SendCustomChallengeTrigger("Win");
			}
			if (level_name != "Boot" && level_name != "Empty" && !Shared.IsMainMenuScene(level_name) && GameManager.GetFreezingComponent().m_CurrentFreezing >= 100f)
			{
				CurrentCustomChalleng.m_Started = false;
				ProcessCustomChallengeTrigger("Lose");
				SendCustomChallengeTrigger("Lose");
			}
		}
		else
		{
			if (!(CurrentChallengeRules.m_Name == "Lost in action") || CurrentCustomChalleng.m_CurrentTask != 1 || !iAmHost)
			{
				return;
			}
			int num = 0;
			int num2 = 1;
			if (levelid == 10)
			{
				num++;
			}
			for (int i = 1; i <= Server.MaxPlayers; i++)
			{
				if (Server.clients[i].IsBusy())
				{
					num2++;
					if (!Shared.ClientIsLoading(i) && playersData[i].m_Levelid == 10)
					{
						num++;
					}
				}
			}
			if (num == num2)
			{
				CurrentCustomChalleng.m_Started = false;
				ProcessCustomChallengeTrigger("Win");
				SendCustomChallengeTrigger("Win");
			}
		}
	}

	public static void ProcessCustomChallengeTrigger(string TRIGGER)
	{
		MelonLogger.Msg("[ProcessCustomChallengeTrigger] " + TRIGGER);
		if (TRIGGER == "Lose")
		{
			if (m_Panel_ChallengeComplete == null)
			{
				m_Panel_ChallengeComplete = InterfaceManager.LoadPanel<Panel_ChallengeComplete>();
			}
			m_Panel_ChallengeComplete.ShowPanel(Panel_ChallengeComplete.Options.ShowFailStatInfo);
			m_Panel_ChallengeComplete.SetStatInfoText("Ехать ты лох конечно", "Оставшиеся время:", CurrentCustomChalleng.m_Time, "Обкакался раз:", 99);
		}
		else if (TRIGGER == "Win")
		{
			if (m_Panel_ChallengeComplete == null)
			{
				m_Panel_ChallengeComplete = InterfaceManager.LoadPanel<Panel_ChallengeComplete>();
			}
			m_Panel_ChallengeComplete.ShowPanel(Panel_ChallengeComplete.Options.Succeeded);
			m_Panel_ChallengeComplete.SetStatInfoText("Ну молодец", "Оставшиеся время:", CurrentCustomChalleng.m_Time, "Обкакался раз:", 0);
		}
		if (TRIGGER.StartsWith("CairnID"))
		{
			AddFoundCairn(int.Parse(TRIGGER.Replace("CairnID", "")), sync: false);
		}
	}

	public static void SendCustomChallengeTrigger(string TRIGGER)
	{
		if (iAmHost)
		{
			ServerSend.CHALLENGETRIGGER(TRIGGER);
		}
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(121))
			{
				packet.Write(TRIGGER);
				SendUDPData(packet);
			}
		}
	}

	public static string GetFormatedTextForChallange()
	{
		return System.TimeSpan.FromSeconds(CurrentCustomChalleng.m_Time).ToString("hh\\:mm\\:ss");
	}

	public static string GetFormatedTimeForExpedition(int Seconds)
	{
		return System.TimeSpan.FromSeconds(Seconds).ToString("hh\\:mm\\:ss");
	}

	public static int CalculateHowManyTaskDone()
	{
		int num = 0;
		for (int i = 0; i < CurrentCustomChalleng.m_Done.Count; i++)
		{
			if (CurrentCustomChalleng.m_Done[i] >= CurrentChallengeRules.m_Tasks[i].m_GoalVal)
			{
				num++;
			}
		}
		return num;
	}

	public static void ExpeditionUI(string ExpeditionName, string desc, int TimeLeft)
	{
		Panel_Log panel_Log = GameCompat.Panel<Panel_Log>();
		Panel_ActionsRadial panel_ActionsRadial = GameCompat.Panel<Panel_ActionsRadial>();
		Panel_Actions panel_Actions = GameCompat.Panel<Panel_Actions>();
		string formatedTimeForExpedition = GetFormatedTimeForExpedition(TimeLeft);
		desc = ExpeditionName + "\n" + desc;
		if (ForcedShowExpeditionHUDSeconds > 0)
		{
			desc = (DefaultIsRussian ? (desc + "\n\nВы можете зажать кнопку [50C878]TAB[-] чтобы показать эту панель ещё раз, когда она пропадёт.") : (desc + "\n\nYou can hold [50C878]TAB[-] key to see this panel again, once it close."));
		}
		if ((bool)panel_ActionsRadial)
		{
			panel_ActionsRadial.m_MissionObjectiveLabel.text = desc;
			panel_ActionsRadial.m_MissionTimerLabel.text = formatedTimeForExpedition;
		}
		if ((bool)panel_Actions)
		{
			panel_Actions.m_MissionObjectiveWithTimerLabel.text = desc;
			panel_Actions.m_MissionTimerLabel.text = formatedTimeForExpedition;
			panel_Actions.m_MissionTimerLabel.gameObject.SetActive(value: true);
		}
		if ((bool)panel_Log)
		{
			panel_Log.m_MissionNameHeaderLabel.text = ExpeditionName;
			panel_Log.m_MissionNameLabel.text = ExpeditionName;
			panel_Log.m_MissionDescriptionLabel.text = desc;
			panel_Log.m_TimerLabel.text = formatedTimeForExpedition;
		}
	}

	public static void PatchChallange()
	{
		if (CurrentChallengeRules.m_ID == 0)
		{
			return;
		}
		string name = CurrentChallengeRules.m_Name;
		string formatedTextForChallange = GetFormatedTextForChallange();
		string text;
		int num;
		if (CurrentChallengeRules.m_Lineal)
		{
			text = CurrentChallengeRules.m_Tasks[CurrentCustomChalleng.m_CurrentTask].m_Task;
			num = CurrentCustomChalleng.m_CurrentTask + 1;
			int goalVal = CurrentChallengeRules.m_Tasks[CurrentCustomChalleng.m_CurrentTask].m_GoalVal;
			int num2 = CurrentCustomChalleng.m_Done[CurrentCustomChalleng.m_CurrentTask];
			if (goalVal > 1)
			{
				text = text + " " + num2 + "/" + goalVal;
			}
		}
		else
		{
			text = "";
			for (int i = 0; i < CurrentChallengeRules.m_Tasks.Count; i++)
			{
				int goalVal2 = CurrentChallengeRules.m_Tasks[i].m_GoalVal;
				int num3 = CurrentCustomChalleng.m_Done[i];
				string task = CurrentChallengeRules.m_Tasks[i].m_Task;
				string text2 = "[";
				if (i != 0)
				{
					text2 += "\n";
				}
				text2 += "[";
				text2 = ((num3 < goalVal2) ? (text2 + " ] ") : (text2 + "X] "));
				text2 += task;
				text += text2;
			}
			num = CalculateHowManyTaskDone();
		}
		Panel_Log panel_Log = GameCompat.Panel<Panel_Log>();
		Panel_ActionsRadial panel_ActionsRadial = GameCompat.Panel<Panel_ActionsRadial>();
		Panel_Actions panel_Actions = GameCompat.Panel<Panel_Actions>();
		if ((bool)panel_ActionsRadial)
		{
			panel_ActionsRadial.m_MissionObjectiveLabel.text = text;
			panel_ActionsRadial.m_MissionTimerLabel.text = formatedTextForChallange;
		}
		if ((bool)panel_Actions)
		{
			panel_Actions.m_MissionObjectiveWithTimerLabel.text = text;
			panel_Actions.m_MissionTimerLabel.text = formatedTextForChallange;
			panel_Actions.m_MissionTimerLabel.gameObject.SetActive(value: true);
		}
		if ((bool)panel_Log)
		{
			panel_Log.m_MissionNameHeaderLabel.text = name;
			panel_Log.m_MissionNameLabel.text = name;
			if (CurrentChallengeRules.m_Tasks.Count > 1)
			{
				panel_Log.m_MissionNameHeaderLabel.text = name + " " + num + "/" + CurrentChallengeRules.m_Tasks.Count;
			}
			panel_Log.m_MissionDescriptionLabel.text = text;
			panel_Log.m_TimerLabel.text = formatedTextForChallange;
		}
	}

	public static void AddSpray(DecalProjectorInstance Replica)
	{
		DynamicDecalsManager dynamicDecalsManager = GameManager.GetDynamicDecalsManager();
		DecalProjectorInstance decalProjectorInstance = dynamicDecalsManager.AddSprayPaintDecal(Replica.m_ProjectileType, MaterialEffectType.MaterialEffect_Untagged, Replica.m_Pos, Replica.m_Normal);
		decalProjectorInstance.m_Guid = Replica.m_Guid;
		decalProjectorInstance.m_DecalName = Replica.m_DecalName;
		decalProjectorInstance.m_Indoors = Replica.m_Indoors;
		decalProjectorInstance.m_Yaw = Replica.m_Yaw;
		decalProjectorInstance.m_Alpha = Replica.m_Alpha;
		decalProjectorInstance.m_ColorTint = Replica.m_ColorTint;
		decalProjectorInstance.m_RevealAmount = Replica.m_RevealAmount;
		decalProjectorInstance.m_RevealedOnMap = Replica.m_RevealedOnMap;
		decalProjectorInstance.m_IsRevealing = true;
		decalProjectorInstance.m_IsPlacing = false;
		dynamicDecalsManager.PlaceDownSprayPaintDecal(decalProjectorInstance);
	}

	public static int GetPasswordFromGear(string name)
	{
		if (name.Contains("CanneryCodeNote"))
		{
			return 1540;
		}
		if (name.Contains("BlackrockCodeNote"))
		{
			return 3008;
		}
		return 0;
	}

	public static void RestoreCodeFromGears()
	{
		MelonLogger.Msg(System.ConsoleColor.Blue, "[Papers codes] Checking all papers to make sure if we have code");
		Il2CppSystem.Collections.Generic.List<GearItemObject>.Enumerator enumerator = GameManager.GetInventoryComponent().m_Items.GetEnumerator();
		while (enumerator.MoveNext())
		{
			int passwordFromGear = GetPasswordFromGear(enumerator.Current.m_GearItemName);
			if (passwordFromGear != 0)
			{
				RememoberCode(passwordFromGear);
			}
		}
	}

	public static void RememoberCode(int code)
	{
		if (GameManager.GetPlayerManagerComponent().m_KnownCodes.Contains(code))
		{
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[Papers codes] Code " + code + " is already known");
			return;
		}
		MelonLogger.Msg(System.ConsoleColor.Green, "[Papers codes] Code " + code + " added to the player's memory");
		GameManager.GetPlayerManagerComponent().m_KnownCodes.Add(code);
	}

	public static void UseKey(string KeySeed, GameObject Door, bool LeadKey)
	{
		if (!Door || !Door.GetComponent<LoadScene>())
		{
			return;
		}
		if (PendingKeysAction == KeysAction.LOCK)
		{
			TryLockDoor(Door.GetComponent<LoadScene>(), KeySeed);
		}
		else if (PendingKeysAction == KeysAction.OPEN)
		{
			if (iAmHost)
			{
				if (LeadKey && !MPSaveManager.TryUseLeadKey())
				{
					RemoveKey(KeySeed);
					HUDMessage.AddMessage("The key broke!");
				}
				else if (MPSaveManager.TryUseKey(level_guid, Door.GetComponent<Comps.DoorLockedOnKey>().m_DoorKey, KeySeed))
				{
					Door.GetComponent<LoadScene>().Activate();
				}
				else
				{
					HUDMessage.AddMessage("Incorrect key!");
					Door.GetComponent<Lock>().PlayLockedAudio();
				}
			}
			else if (sendMyPosition)
			{
				DoPleaseWait("Please wait...", "Trying open this door...");
				SetRepeatPacket(ResendPacketType.Cancel, 10);
				using Packet packet = new Packet(133);
				packet.Write(Door.GetComponent<Comps.DoorLockedOnKey>().m_DoorKey);
				packet.Write(KeySeed);
				packet.Write(level_guid);
				packet.Write(LeadKey);
				SendUDPData(packet);
			}
		}
		else
		{
			if (PendingKeysAction != KeysAction.UNLOCK)
			{
				return;
			}
			if (iAmHost)
			{
				if (MPSaveManager.TryUseKey(level_guid, Door.GetComponent<Comps.DoorLockedOnKey>().m_DoorKey, KeySeed))
				{
					MPSaveManager.RemoveLockedDoor(level_guid, Door.GetComponent<Comps.DoorLockedOnKey>().m_DoorKey);
					return;
				}
				HUDMessage.AddMessage("Incorrect key!");
				Door.GetComponent<Lock>().PlayLockedAudio();
			}
			else if (sendMyPosition)
			{
				DoPleaseWait("Please wait...", "Trying unlock this door...");
				SetRepeatPacket(ResendPacketType.Cancel, 10);
				using Packet packet2 = new Packet(133);
				packet2.Write(Door.GetComponent<Comps.DoorLockedOnKey>().m_DoorKey);
				packet2.Write(KeySeed);
				packet2.Write(level_guid);
				packet2.Write(LeadKey);
				SendUDPData(packet2);
			}
		}
	}

	public static System.Collections.Generic.List<DataStr.DoorKey> GetKeysList()
	{
		System.Collections.Generic.List<DataStr.DoorKey> list = new System.Collections.Generic.List<DataStr.DoorKey>();
		Il2CppSystem.Collections.Generic.List<GearItemObject>.Enumerator enumerator = GameManager.GetInventoryComponent().m_Items.GetEnumerator();
		while (enumerator.MoveNext())
		{
			GearItemObject current = enumerator.Current;
			if ((current.m_GearItemName == "GEAR_SCDoorKey" || current.m_GearItemName == "GEAR_SCDoorKeyLead") && (bool)current.m_GearItem.m_ObjectGuid && !string.IsNullOrEmpty(current.m_GearItem.m_ObjectGuid.m_Guid))
			{
				string guid = current.m_GearItem.m_ObjectGuid.m_Guid;
				if (guid.Contains('_'))
				{
					string[] array = guid.Split('_');
					list.Add(new DataStr.DoorKey(array[1], array[0], current.m_GearItemName == "GEAR_SCDoorKeyLead"));
				}
			}
		}
		return list;
	}

	public static void RemoveKey(string Seed)
	{
		Il2CppSystem.Collections.Generic.List<GearItemObject>.Enumerator enumerator = GameManager.GetInventoryComponent().m_Items.GetEnumerator();
		while (enumerator.MoveNext())
		{
			GearItemObject current = enumerator.Current;
			if (current.m_GearItemName == "GEAR_SCDoorKeyLead" && (bool)current.m_GearItem.m_ObjectGuid && !string.IsNullOrEmpty(current.m_GearItem.m_ObjectGuid.m_Guid))
			{
				string guid = current.m_GearItem.m_ObjectGuid.m_Guid;
				if (guid.Contains('_') && guid.Split('_')[1] == Seed)
				{
					UnityEngine.Object.Destroy(current.m_GearItem.gameObject);
					break;
				}
			}
		}
	}

	public static int GetLockpicks()
	{
		return GameManager.GetInventoryComponent().GetNumGearWithName("GEAR_SCLockpick");
	}

	public static void SelectKeys(GameObject Door, KeysAction Act)
	{
		PendingKeysAction = Act;
		ShowKeysActionsPicker(Door, Act);
	}

	public static void LockPick(GameObject objectInteractedWith)
	{
		DataStr.PriorityActionForOtherPlayer customAction = GetCustomAction("Lockpick");
		DoLongAction(objectInteractedWith, customAction.m_ProcessText, customAction.m_Action, customAction.m_ActionDuration, customAction.m_Hold);
		GameManager.GetInventoryComponent().RemoveUnits(GameManager.GetInventoryComponent().GetBestGearItemWithName("GEAR_SCLockpick"), 1);
		if (!objectInteractedWith.GetComponent<Comps.DoorLockedOnKey>())
		{
			return;
		}
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(135))
			{
				packet.Write(objectInteractedWith.GetComponent<Comps.DoorLockedOnKey>().m_DoorKey);
				packet.Write(level_guid);
				SendUDPData(packet);
				return;
			}
		}
		MPSaveManager.TryLockPick(level_guid, objectInteractedWith.GetComponent<Comps.DoorLockedOnKey>().m_DoorKey, 0);
	}

	public static void RequestExpedition()
	{
		PlayRadioOver();
		Shared.GameRegion currentRegion = RegionCompat.GetCurrentRegion();
		MelonLogger.Msg("Trying to get expedition on " + ExpeditionBuilder.GetRegionString((int)currentRegion));
		if (iAmHost)
		{
			ExpeditionManager.StartNewExpedition(MPSaveManager.GetSubNetworkGUID(), (int)currentRegion);
			return;
		}
		using Packet packet = new Packet(163);
		packet.Write((int)currentRegion);
		SendUDPData(packet);
	}

	public static void ExpeditionHint()
	{
		Panel_TutorialPopup.TutorialPopupRequest tutorialPopupRequest = new Panel_TutorialPopup.TutorialPopupRequest();
		if (!DefaultIsRussian)
		{
			tutorialPopupRequest.m_TitleLocID = "Expeditions Starter Guide";
			tutorialPopupRequest.m_MessageLocID = "The [27AE60]Handheld Radio[-] can be used to start or join expeditions.\nClick [00FFFF]Right Mouse Button[-] when you holding Handheld Radio to open Expeditions Menu.\nTo start [FFB233]Random Expedition[-] click [FFB233]«Start Expedition»[-] in Expeditions Menu.\n If you found any [50C878]Special Items[-] you can start [50C878]Special Expedition[-], an advanced challenges with a much higher reward.\n[50C878]Special Items[-] can be found in any container, click [50C878]«Special Expedition»[-] in Expeditions Menu to see if you have any of them to start expedition.\nYou can also [00FFFF]Invite[-] other players to your Expedition, to do so, just click on them when you holding [27AE60]Handheld Radio[-].\nAny received [00FFFF]Invites[-] you can find in Expedition Menu.\nIf you already in Expedition, you can hold [27AE60]TAB[-] key to see Expedition Objective.";
		}
		else
		{
			tutorialPopupRequest.m_TitleLocID = "Начальное Руководство По Экспедициям";
			tutorialPopupRequest.m_MessageLocID = "Используя [27AE60]Рацию[-] Вы можете начинать экспедиции и присоединяться к ним.\nКликните [00FFFF]Правой Кнопкой Мыши[-] когда держите Рацию в руках, чтобы открыть Меню Экспедиций.\nЧтобы начать [FFB233]Случайную Экспедицию[-] кликните [FFB233]«Start Expedition»[-] в Меню Экспедиций.\nЕсли вы найдёте какие либо [50C878]Специальные Предметы[-] Вы сможете начать [50C878]Специальную Экспедицию[-], сложные испытания с более весомой наградой.\n[50C878]Специальные Предметы[-] случайно могут быть найдеты в любом контейнере, кликните [50C878]«Special Expedition»[-] в Меню Экспедиций чтобы узнать есть ли у Вас какие либо [50C878]Специальные Предметы[-] для старта экспедиции.\nА также Вы можете [00FFFF]Приглашать[-] других игроков в Вашу Экспедицию, чтобы это сделать, просто кликните по ним держа в руках [27AE60]Рацию[-].\nВсе полученные [00FFFF]Приглашения[-] Вы можете найти в меню Экспедиций.\nЕсли же вы уже в экспедиции, вы можете зажать кнопку [27AE60]TAB[-] чтобы увидеть Цель текущей Экспедиции.";
		}
		GameCompat.Panel<Panel_TutorialPopup>().QueueTutorialPopupRequest(tutorialPopupRequest);
		GameCompat.Panel<Panel_TutorialPopup>().ShowCurrentTutorialPopup();
		MPSaveManager.SeenExpeditions();
	}

	public static void ShowRadioActionPicker(GameObject objectInteractedWith)
	{
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if ((bool)panel_ActionPicker)
		{
			panel_ActionPicker.Enable(enable: true);
			panel_ActionPicker.m_ActionPickerItemDataList.Clear();
			System.Action action = delegate
			{
				RequestExpedition();
			};
			System.Action action2 = delegate
			{
				ShowInvitesAfterPicker();
			};
			System.Action action3 = delegate
			{
				ShowCluesAfterPicker();
			};
			System.Action action4 = delegate
			{
				ShowCluesAfterPicker();
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_map", "Start Expedition", action));
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_log_Collectibles", "Special Expedition", action3));
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_knowledge_people", "Expedition Invites", action2));
			if (false)
			{
				panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("icoMap_willAirplane", "Invistigate\nCrash Site", action4));
			}
			panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
			panel_ActionPicker.EnableWithCurrentList();
		}
	}

	public static void SendInvietToPlayer(int ClientID)
	{
		if (iAmHost)
		{
			if (ClientID != -1)
			{
				ExpeditionManager.CreateInviteToExpedition(Shared.GetMacAddress(), Server.GetMACByID(ClientID));
			}
			return;
		}
		using Packet packet = new Packet(166);
		packet.Write(ClientID);
		SendUDPData(packet);
	}

	public static void AcceptInvite(ExpeditionManager.ExpeditionInvite Invite)
	{
		if (iAmHost)
		{
			ExpeditionManager.AcceptInvite(Invite.m_PersonToInviteMAC, Invite.m_InviterMAC);
		}
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(164))
			{
				packet.Write(Invite);
				SendUDPData(packet);
			}
		}
	}

	public static void StartExpeditionWithClue(ExpeditionManager.SpecialExpeditionItem Clue)
	{
		if (iAmHost)
		{
			ExpeditionManager.StartNewExpedition(Server.GetMACByID(0), 0, Clue.m_ExpeditionAlias, NoMessage: false, Special: true);
		}
		else if (sendMyPosition)
		{
			DoPleaseWait("Please wait...", "Coordinating...");
			using Packet packet = new Packet(181);
			packet.Write(Clue.m_ExpeditionAlias);
			SetRepeatPacket(ResendPacketType.Cancel, 5);
			SendUDPData(packet);
		}
	}

	public static void ShowCluesPicker()
	{
		if (iAmHost)
		{
			ShowCluesPicker(ExpeditionManager.GetAllSpeicalItemsOfPlayer(MPSaveManager.GetSubNetworkGUID()));
		}
		else if (sendMyPosition)
		{
			SetRepeatPacket(ResendPacketType.Cancel, 5);
			DoPleaseWait("Please wait...", "Looking in pockets...");
			using Packet packet = new Packet(185);
			packet.Write(_value: true);
			SendUDPData(packet);
		}
	}

	public static void ShowCluesPicker(System.Collections.Generic.List<string> ItemNames)
	{
		System.Collections.Generic.List<ExpeditionManager.SpecialExpeditionItem> list = new System.Collections.Generic.List<ExpeditionManager.SpecialExpeditionItem>();
		foreach (string ItemName in ItemNames)
		{
			ExpeditionManager.SpecialExpeditionItem specialItem = ExpeditionManager.GetSpecialItem(ItemName);
			if (specialItem != null)
			{
				list.Add(specialItem);
			}
		}
		ShowCluesPicker(list);
	}

	public static void ShowCluesPicker(System.Collections.Generic.List<ExpeditionManager.SpecialExpeditionItem> Items)
	{
		if (Items.Count == 0)
		{
			HUDMessage.AddMessage("You don't have any [50C878]special items[-] to start advanced expedition.");
			return;
		}
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if (!panel_ActionPicker)
		{
			return;
		}
		string spriteName = "ico_log_Notes";
		panel_ActionPicker.Enable(enable: true);
		panel_ActionPicker.m_ActionPickerItemDataList.Clear();
		foreach (ExpeditionManager.SpecialExpeditionItem Item in Items)
		{
			System.Action action = delegate
			{
				StartExpeditionWithClue(Item);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData(spriteName, Item.m_GearName, action));
		}
		panel_ActionPicker.m_ObjectInteractedWith = null;
		panel_ActionPicker.EnableWithCurrentList();
	}

	public static void ShowInvitesPicker(GameObject objectInteractedWith, System.Collections.Generic.List<ExpeditionManager.ExpeditionInvite> Invites)
	{
		if (Invites.Count == 0)
		{
			HUDMessage.AddMessage("You have not any invites.");
			return;
		}
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if (!panel_ActionPicker)
		{
			return;
		}
		string spriteName = "ico_knowledge_people";
		panel_ActionPicker.Enable(enable: true);
		panel_ActionPicker.m_ActionPickerItemDataList.Clear();
		foreach (ExpeditionManager.ExpeditionInvite Invite in Invites)
		{
			System.Action action = delegate
			{
				AcceptInvite(Invite);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData(spriteName, "Invite by " + Invite.m_InviterName, action));
		}
		panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
		panel_ActionPicker.EnableWithCurrentList();
	}

	public static void ShowInvitesAfterPicker()
	{
		Pathes.ShowInvitesAfterPicker = true;
	}

	public static void ShowCluesAfterPicker()
	{
		Pathes.ShowCluesAfterPicker = true;
	}

	public static void KnockKnock(GameObject Door)
	{
		if (!Door || !Door.GetComponent<LoadScene>())
		{
			return;
		}
		string text = Door.GetComponent<LoadScene>().m_SceneToLoad + "_" + Door.GetComponent<LoadScene>().m_GUID;
		if (sendMyPosition)
		{
			using Packet packet = new Packet(143);
			packet.Write(text);
			SendUDPData(packet);
		}
		if (iAmHost)
		{
			MPSaveManager.AddKnockDoorRequest(0, text);
		}
	}

	public static void PickupBlank(GameObject objectInteractedWith)
	{
		PickupDroppedGear(objectInteractedWith, SkipLocksmith: true);
	}

	public static void LocksmithWork(GameObject objectInteractedWith, int Tool)
	{
		if (Tool == 0 && !GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_Hacksaw"))
		{
			HUDMessage.AddMessage("You need hacksaw!");
			GameAudioManager.PlayGUIError();
			return;
		}
		if (Tool == 1 && !GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_SharpeningStone"))
		{
			HUDMessage.AddMessage("You need whetstone!");
			GameAudioManager.PlayGUIError();
			return;
		}
		if (Tool == 5 && !GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_SCLiquidLead"))
		{
			HUDMessage.AddMessage("You need liquid lead!");
			GameAudioManager.PlayGUIError();
			return;
		}
		if ((bool)objectInteractedWith.GetComponent<Comps.DroppedGearDummy>())
		{
			int searchKey = objectInteractedWith.GetComponent<Comps.DroppedGearDummy>().m_SearchKey;
			if (iAmHost)
			{
				if (!MPSaveManager.CanWorkOnBlank(searchKey))
				{
					HUDMessage.AddMessage("Someone already work on this!");
					GameAudioManager.PlayGUIError();
				}
				else
				{
					MPSaveManager.ChangeBlankState(searchKey, -1);
				}
			}
			if (sendMyPosition)
			{
				PendingLocksmithAction = Tool;
				PendingLocksmithObject = objectInteractedWith;
				DoPleaseWait("Please wait...", "Requesting data about blank...");
				SetRepeatPacket(ResendPacketType.Cancel, 10);
				using Packet packet = new Packet(141);
				packet.Write(searchKey);
				SendUDPData(packet);
				return;
			}
		}
		DataStr.PriorityActionForOtherPlayer customAction = GetCustomAction("Locksmith" + Tool);
		DoLongAction(objectInteractedWith, customAction.m_ProcessText, customAction.m_Action, customAction.m_ActionDuration, customAction.m_Hold);
	}

	public static void ShowBlankworkingPicker(GameObject objectInteractedWith)
	{
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if (!panel_ActionPicker)
		{
			return;
		}
		panel_ActionPicker.Enable(enable: true);
		panel_ActionPicker.m_ActionPickerItemDataList.Clear();
		panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
		PendingRegisterKey = null;
		System.Action action = delegate
		{
			PickupBlank(objectInteractedWith);
		};
		System.Action action2 = delegate
		{
			LocksmithWork(objectInteractedWith, 0);
		};
		System.Action action3 = delegate
		{
			LocksmithWork(objectInteractedWith, 1);
		};
		if (objectInteractedWith.name.ToLower().StartsWith("gear_scdoorkeytemp") || objectInteractedWith.name.ToLower().StartsWith("gear_scdoorkeyleadtemp"))
		{
			action3 = delegate
			{
				BeginRegisterKey(objectInteractedWith);
			};
		}
		panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_BearSpear", "Sawing", action2));
		panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_injury_majorBruising", "Grinding", action3));
		panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_climb", "Pickup", action));
		panel_ActionPicker.EnableWithCurrentList();
	}

	public static void ShowLeadCastingPicker(GameObject objectInteractedWith)
	{
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if ((bool)panel_ActionPicker)
		{
			panel_ActionPicker.Enable(enable: true);
			panel_ActionPicker.m_ActionPickerItemDataList.Clear();
			panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
			System.Action action = delegate
			{
				PickupBlank(objectInteractedWith);
			};
			System.Action action2 = delegate
			{
				LocksmithWork(objectInteractedWith, 5);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_forge", "Cast Lead", action2));
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_climb", "Pickup", action));
			panel_ActionPicker.EnableWithCurrentList();
		}
	}

	public static void LetThemEnter(int ClientID, string Scene)
	{
		if (iAmHost)
		{
			MPSaveManager.ApplyEnterFromKnock(ClientID, Scene);
		}
		if (sendMyPosition)
		{
			using (Packet packet = new Packet(145))
			{
				packet.Write(ClientID);
				packet.Write(Scene);
				SendUDPData(packet);
			}
		}
	}

	public static void CharCoalDraw(CharcoalItem Coal)
	{
		if (!Coal.m_IsActive && GameManager.GetPlayerAnimationComponent().CanTransitionToState(PlayerAnimation.State.Stowing))
		{
			if (GameManager.GetWeatherComponent().IsIndoorScene())
			{
				GameAudioManager.PlayGUIError();
				HUDMessage.AddMessage(Localization.Get("GAMEPLAY_CannotMapLocalAreaIndoors"));
				return;
			}
			if (!CharcoalItem.HasSurveyVisibility(Coal.GetSurveyExtendedTimeSkillBonus()))
			{
				GameAudioManager.PlayGUIError();
				HUDMessage.AddMessage(Localization.Get("GAMEPLAY_DetailSurveyNoVisibility"));
				return;
			}
			Coal.m_IsActive = true;
			CharcoalItem.m_CharcoalItemInUseForSurvey = Coal;
			Coal.m_TimeSpentSurveying = 0f;
			Coal.AccelerateTimeOfDay();
			Coal.m_SurveyAudioID = GameAudioManager.PlaySound(Coal.m_SurveyLoopAudio, InterfaceManager.GetSoundEmitter());
		}
	}

	public static void CreateCustomNote(string Message, GearItem Note = null)
	{
		string guid = Shared.CompressString(Message);
		if (Note == null)
		{
			Note = GameManager.GetPlayerManagerComponent().AddItemCONSOLE("GEAR_SCNote", 1);
		}
		if (Note.m_ObjectGuid == null)
		{
			Note.m_ObjectGuid = Note.gameObject.AddComponent<ObjectGuid>();
		}
		Note.m_ObjectGuid.m_Guid = guid;
	}

	public static void WriteNote(CharcoalItem Coal)
	{
		CharcoalItem.m_CharcoalItemInUseForSurvey = Coal;
		GameManager.GetPlayerAnimationComponent().Trigger_Generic_Stow();
		Coal.m_SurveyAudioID = GameAudioManager.PlaySound(Coal.m_SurveyLoopAudio, InterfaceManager.GetSoundEmitter());
		GameCompat.Panel<Panel_Log>().EnableStatsView();
		GameCompat.Panel<Panel_Log>().OnEnterGeneralNotes();
		GameCompat.Panel<Panel_Log>().m_SectionNav.SetActive(value: false);
		GameCompat.Panel<Panel_Log>().m_TabsNotesObject.SetActive(value: false);
		GameCompat.Panel<Panel_Log>().m_NotesTextField.SetText("");
	}

	public static void ShowCharCoalPicker(GameObject objectInteractedWith, CharcoalItem Coal)
	{
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if ((bool)panel_ActionPicker)
		{
			panel_ActionPicker.Enable(enable: true);
			panel_ActionPicker.m_ActionPickerItemDataList.Clear();
			System.Action action = delegate
			{
				CharCoalDraw(Coal);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_map", "Survey local area", action));
			System.Action action2 = delegate
			{
				WriteNote(Coal);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_log_Notes", "Write a note", action2));
			panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
			panel_ActionPicker.EnableWithCurrentList();
		}
	}

	public static void ShowKnockersPicker(GameObject objectInteractedWith, System.Collections.Generic.List<int> Knockers)
	{
		if (Knockers.Count == 0)
		{
			HUDMessage.AddMessage("No body knocking.");
			return;
		}
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if (!panel_ActionPicker)
		{
			return;
		}
		string spriteName = "ico_unlocked";
		panel_ActionPicker.Enable(enable: true);
		panel_ActionPicker.m_ActionPickerItemDataList.Clear();
		foreach (int Idx in Knockers)
		{
			System.Action action = delegate
			{
				LetThemEnter(Idx, level_guid);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData(spriteName, "Invite " + playersData[Idx].m_Name, action));
		}
		panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
		panel_ActionPicker.EnableWithCurrentList();
	}

	public static void ShowKeysActionsPicker(GameObject objectInteractedWith, KeysAction ActType)
	{
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if (!panel_ActionPicker)
		{
			return;
		}
		System.Collections.Generic.List<DataStr.DoorKey> keysList = GetKeysList();
		int lockpicks = GetLockpicks();
		string spriteName = "ico_locked";
		bool flag = false;
		if (ActType != KeysAction.LOCK)
		{
			spriteName = "ico_unlocked";
			flag = true;
		}
		if (!flag && keysList.Count == 0)
		{
			HUDMessage.AddMessage("You don't have any keys to lock this door!");
			return;
		}
		panel_ActionPicker.Enable(enable: true);
		panel_ActionPicker.m_ActionPickerItemDataList.Clear();
		foreach (DataStr.DoorKey Key in keysList)
		{
			System.Action action = delegate
			{
				UseKey(Key.m_Seed, objectInteractedWith, Key.m_Lead);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData(spriteName, Key.m_Name, action));
		}
		if (flag)
		{
			if (lockpicks > 0)
			{
				System.Action action2 = delegate
				{
					LockPick(objectInteractedWith);
				};
				panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_Radial_tools", "Lockpick (" + lockpicks + ")", action2));
			}
			System.Action action3 = delegate
			{
				KnockKnock(objectInteractedWith);
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_fist", "Knock", action3));
		}
		panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
		panel_ActionPicker.EnableWithCurrentList();
	}

	public static int GetCurrentRegionIfPossible()
	{
		int result = -1;
		if (GameManager.m_Weather != null && GameManager.m_WeatherTransition != null && GameManager.m_WeatherTransition.m_CurrentWeatherSet != null && GameManager.GetUniStorm() != null)
		{
			result = (int)RegionCompat.GetCurrentRegion();
		}
		return result;
	}

	public static System.ConsoleColor ConvertLoggerColor(Shared.LoggerColor Color)
	{
		return Color switch
		{
			Shared.LoggerColor.Red => System.ConsoleColor.Red, 
			Shared.LoggerColor.Green => System.ConsoleColor.Green, 
			Shared.LoggerColor.Blue => System.ConsoleColor.Green, 
			Shared.LoggerColor.Yellow => System.ConsoleColor.Yellow, 
			Shared.LoggerColor.Magenta => System.ConsoleColor.Magenta, 
			Shared.LoggerColor.White => System.ConsoleColor.White, 
			_ => System.ConsoleColor.White, 
		};
	}

	public static void DoExpeditionState(int State)
	{
		if (State == 0 || State == 1 || State == -1 || State == 4 || State == -2)
		{
			OnExpedition = false;
			if (GameCompat.Panel<Panel_Actions>() != null)
			{
				GameCompat.Panel<Panel_Actions>().m_MissionObjectWithTimer.SetActive(value: false);
				GameCompat.Panel<Panel_Actions>().m_MissionObjectiveWithTimerLabel.gameObject.SetActive(value: false);
				GameCompat.Panel<Panel_Actions>().m_MissionTimerLabel.gameObject.SetActive(value: false);
			}
		}
		ForcedShowExpeditionHUDSeconds = 20;
		GameCompat.Panel<Panel_Actions>().Enable(enable: true);
		if ((bool)m_InterfaceManager && (bool)GameCompat.Panel<Panel_HUD>())
		{
			switch (State)
			{
			case 0:
				GameCompat.Panel<Panel_HUD>().ShowBuffLossNotification("Expedition Cancled", "Time Over", "ico_map");
				break;
			case 1:
				GameCompat.Panel<Panel_HUD>().ShowBuffNotification("Expedition Finished", "Loot Your Reward", "ico_map");
				break;
			case 2:
				GameCompat.Panel<Panel_HUD>().ShowBuffNotification("Expedition Started", "Objective Updated", "ico_map");
				break;
			case 3:
				GameCompat.Panel<Panel_HUD>().ShowBuffLossNotification("Task Completed", "Objective Updated", "ico_map");
				break;
			case -1:
				GameCompat.Panel<Panel_HUD>().ShowBuffLossNotification("Crash Site Found", "You Late", "icoMap_willAirplane");
				break;
			case 4:
				GameCompat.Panel<Panel_HUD>().ShowBuffNotification("Crash Site Found", "Loot Your Reward", "icoMap_willAirplane");
				break;
			case 5:
				GameCompat.Panel<Panel_HUD>().ShowBuffNotification("Find Crash Site", "Objective Updated", "icoMap_willAirplane");
				break;
			case -2:
				GameCompat.Panel<Panel_HUD>().ShowBuffLossNotification("Time Over", "You Late", "icoMap_willAirplane");
				break;
			}
		}
	}

	public static void NewPlayerInExpedition(string PlayerName)
	{
		GearMessage.AddMessage("ico_knowledge_people", "Joined Expedition", PlayerName, highPriority: true);
		PlayRadioOver();
	}

	public static void NewExpeditionInvite(string PlayerName)
	{
		GearMessage.AddMessage("ico_map", "Expedition Invite", PlayerName, highPriority: true);
		DoRadioBeep();
	}

	public static DataStr.Vector2Int GetGearResolution(string GearName)
	{
		if (GearName == "GEAR_SCPhoto")
		{
			return new DataStr.Vector2Int(320, 200);
		}
		if (GearName == "GEAR_SCMapPiece")
		{
			return new DataStr.Vector2Int(200, 200);
		}
		return new DataStr.Vector2Int(320, 200);
	}

	public static void LogScreenshotData()
	{
		CameraGlobalRT cameraGlobalRT = GameManager.GetCameraGlobalRT();
		if (!cameraGlobalRT || !cameraGlobalRT.GetRenderTexture())
		{
			return;
		}
		DataStr.Vector2Int gearResolution = GetGearResolution("GEAR_SCPhoto");
		RenderTexture temporary = RenderTexture.GetTemporary(gearResolution.X, gearResolution.Y, 0, RenderTextureFormat.ARGB32);
		Texture2D texture2D = new Texture2D(temporary.width, temporary.height, TextureFormat.RGBA32, mipChain: false);
		Graphics.Blit(cameraGlobalRT.GetRenderTexture(), temporary);
		RenderTexture.active = temporary;
		texture2D.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0, recalculateMipMaps: false);
		texture2D.Apply();
		string text = System.Convert.ToBase64String(texture2D.EncodeToJPG(60));
		string text2 = MPSaveManager.AddPhoto(text, Cached: true);
		MelonLogger.Msg("Made photo " + text2);
		RenderTexture.ReleaseTemporary(temporary);
		UnityEngine.Object.Destroy(texture2D);
		if (iAmHost)
		{
			MPSaveManager.AddPhoto(text);
		}
		else if (sendMyPosition)
		{
			Shared.SendSlicedBase64Data(Shared.GetBase64Sliced(text, text2, DataStr.SlicedBase64Purpose.Photo));
		}
		GearItem gearItem = GameManager.GetPlayerManagerComponent().AddItemCONSOLE("GEAR_SCPhoto", 1);
		if ((bool)gearItem)
		{
			if (gearItem.m_ObjectGuid == null)
			{
				gearItem.m_ObjectGuid = gearItem.gameObject.AddComponent<ObjectGuid>();
			}
			gearItem.m_ObjectGuid.m_Guid = text2;
			Texture2D texture2D2 = new Texture2D(gearResolution.X, gearResolution.Y);
			texture2D2.LoadImage(System.Convert.FromBase64String(text));
			gearItem.gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = texture2D2;
		}
	}

	public static void ShowRockStashActionPicker(GameObject objectInteractedWith)
	{
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		if (!panel_ActionPicker)
		{
			return;
		}
		Comps.FakeRockCache Stash = null;
		if (objectInteractedWith != null && objectInteractedWith.GetComponent<Comps.FakeRockCache>() != null)
		{
			Stash = objectInteractedWith.GetComponent<Comps.FakeRockCache>();
		}
		if (!(Stash == null))
		{
			panel_ActionPicker.Enable(enable: true);
			panel_ActionPicker.m_ActionPickerItemDataList.Clear();
			System.Action action = delegate
			{
				Stash.Open();
			};
			System.Action action2 = delegate
			{
				Stash.Remove();
			};
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_rockCache", "GAMEPLAY_Open", action));
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_dismantle", "GAMEPLAY_Dismantle", action2));
			panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
			panel_ActionPicker.EnableWithCurrentList();
		}
	}

	public static void ContinueRemovingRockCache()
	{
		if (PendingRockCahceRemove != null)
		{
			GameManager.s_IsAISuspended = true;
			Pathes.FakeRockCacheCallback = PendingRockCahceRemove;
			GameCompat.Panel<Panel_GenericProgressBar>().Launch(Localization.Get("GAMEPLAY_BreakingDownProgress"), 2f, 10f, 0f, "Play_RockCache", null, supressHeavyBreathing: false, skipRestoreInHands: false, null);
			PendingRockCahceRemove = null;
		}
	}

	public static void SpawnRockCache(DataStr.FakeRockCacheVisualData Data)
	{
		if (ObjectGuidManager.Lookup(Data.m_GUID) != null)
		{
			MelonLogger.Msg(System.ConsoleColor.Yellow, "RockCache with GUID " + Data.m_GUID + " already exist!");
			return;
		}
		GameObject gameObject = GameManager.GetRockCacheManager().m_RockCachePrefab.gameObject;
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, Data.m_Position, Data.m_Rotation);
		Comps.FakeRockCache fakeRockCache = gameObject2.GetComponent<Comps.FakeRockCache>();
		if (fakeRockCache == null)
		{
			fakeRockCache = gameObject2.AddComponent<Comps.FakeRockCache>();
		}
		fakeRockCache.m_GUID = Data.m_GUID;
		fakeRockCache.m_Owner = Data.m_Owner;
		fakeRockCache.m_Rocks = gameObject.GetComponent<RockCache>().m_NumRocksFromDismantle;
		fakeRockCache.m_Sticks = gameObject.GetComponent<RockCache>().m_NumSticksFromDismantle;
		fakeRockCache.m_VisualData.m_GUID = fakeRockCache.m_GUID;
		fakeRockCache.m_VisualData.m_LevelGUID = level_guid;
		fakeRockCache.m_VisualData.m_Owner = fakeRockCache.m_Owner;
		fakeRockCache.m_VisualData.m_Position = gameObject2.transform.position;
		fakeRockCache.m_VisualData.m_Rotation = gameObject2.transform.rotation;
		ObjectGuid objectGuid = gameObject2.GetComponent<ObjectGuid>();
		if (objectGuid == null)
		{
			objectGuid = gameObject2.AddComponent<ObjectGuid>();
		}
		objectGuid.Set(fakeRockCache.m_GUID);
		UnityEngine.Object.Destroy(gameObject2.GetComponent<RockCache>());
	}

	public static void AddRockCache(DataStr.FakeRockCacheVisualData Data)
	{
		if (Data.m_LevelGUID == level_guid && GameManager.GetRockCacheManager() != null)
		{
			SpawnRockCache(Data);
		}
	}

	public static void RemoveRockCache(DataStr.FakeRockCacheVisualData Data)
	{
		if (Data.m_LevelGUID == level_guid)
		{
			GameObject gameObject = ObjectGuidManager.Lookup(Data.m_GUID);
			if (gameObject != null)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
		}
	}

	public static Shared.GameRegion GetCurrentRegion()
	{
		if (GameManager.m_Weather != null && GameManager.m_WeatherTransition != null && GameManager.m_WeatherTransition.m_CurrentWeatherSet != null && GameManager.GetUniStorm() != null)
		{
			return RegionCompat.GetCurrentRegion();
		}
		return Shared.GameRegion.RandomRegion;
	}

	public static void CustomPrefabWorkAround(GameObject Obj, string Prefab)
	{
		switch (Prefab)
		{
		case "SmokeOld":
		{
			UnityEngine.Object.Destroy(Obj.GetComponent<Fire>());
			UnityEngine.Object.Destroy(Obj.GetComponent<HeatSource>());
			UnityEngine.Object.Destroy(Obj.GetComponent<Campfire>());
			UnityEngine.Object.Destroy(Obj.GetComponent<EffectsControllerFire>());
			GameObject gameObject2 = Obj.transform.GetChild(7).GetChild(1).gameObject;
			ParticleSystem component = gameObject2.GetComponent<ParticleSystem>();
			gameObject2.AddComponent<Comps.ParticleSystemParasite>().m_ParticleSystem = component;
			component.main.startLifetime = 17f;
			component.main.startSize = 2.1f;
			Obj.transform.GetChild(0).gameObject.SetActive(value: false);
			Obj.transform.GetChild(7).gameObject.SetActive(value: true);
			Obj.transform.GetChild(26).gameObject.SetActive(value: false);
			Obj.transform.GetChild(27).gameObject.SetActive(value: false);
			break;
		}
		case "CORPSE_Convict_001":
		case "CORPSE_Convict_002":
		case "CORPSE_Convict_003":
			if (Obj.GetComponent<Container>() == null)
			{
				Obj.AddComponent<Container>();
			}
			break;
		case "INTERACTIVE_CampFireBurntOut":
			UnityEngine.Object.Destroy(Obj.GetComponent<Campfire>());
			break;
		case "CONTAINER_MetalLockerA":
		{
			Container componentInChildren = Obj.GetComponentInChildren<Container>();
			if (componentInChildren != null)
			{
				componentInChildren.m_CanNeverBeOpened = true;
			}
			break;
		}
		case "Smoke":
		{
			Vector3 position = Obj.transform.position;
			for (int i = 0; i <= 2; i++)
			{
				Transform child = Obj.transform.GetChild(i);
				if (i == 0)
				{
					child.position = position;
				}
				else
				{
					child.gameObject.SetActive(value: false);
				}
			}
			break;
		}
		case "FireSmall":
		case "FireTiny":
		case "FireLarge":
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(GetSemiPrefab("FireLight"), Obj.transform);
			gameObject.transform.position = Obj.transform.position;
			gameObject.SetActive(value: true);
			break;
		}
		case "SnowPile":
			Obj.transform.localScale = new Vector3(1.7f, 1.7f, 1.7f);
			Obj.transform.GetChild(0).gameObject.GetComponent<MeshRenderer>().material = GetSemiPrefab("SnowPileB").transform.GetChild(0).gameObject.GetComponent<MeshRenderer>().material;
			break;
		case "RockWall":
			Obj.transform.localScale = new Vector3(1.7f, 1.7f, 1.7f);
			Obj.transform.GetChild(0).gameObject.GetComponent<MeshRenderer>().material = GetSemiPrefab("RockDummy").transform.GetChild(0).gameObject.GetComponent<MeshRenderer>().material;
			break;
		}
	}

	public static void PlayCustomSoundEvent(Vector3 Position, string Sound, string Prefab)
	{
		if (Prefab == "ExpeditionAudioEvent")
		{
			Prefab = "Expedition2DAudioEvent";
		}
		GameObject gameObject = BundleAsset<GameObject>(Prefab);
		AudioClip audioClip = BundleAsset<AudioClip>(Sound);
		if (gameObject == null)
		{
			gameObject = Resources.Load<GameObject>(Prefab);
		}
		if (audioClip == null)
		{
			audioClip = Resources.Load<AudioClip>(Sound);
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, Position, Quaternion.identity);
		if ((bool)gameObject2 && (bool)audioClip)
		{
			AudioSource component = gameObject2.GetComponent<AudioSource>();
			SettingsState state = GameCompat.Panel<Panel_OptionsMenu>().State;
			if ((bool)component)
			{
				component.volume = state.m_SoundVolume;
				component.PlayOneShot(audioClip, state.m_MasterVolume);
			}
		}
	}

	public static System.Collections.Generic.List<T> GetComponentsInChildrenRecursively<T>(Transform _transform, System.Collections.Generic.List<T> _componentList)
	{
		for (int i = 0; i < _transform.childCount; i++)
		{
			Transform child = _transform.GetChild(i);
			foreach (T component in child.GetComponents<T>())
			{
				if (component != null)
				{
					_componentList.Add(component);
				}
			}
			GetComponentsInChildrenRecursively(child, _componentList);
		}
		return _componentList;
	}

	public static GameObject SpawnUniversalSyncableObject(DataStr.UniversalSyncableObject Data, bool DebugPurpose = false)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(Data.m_GUID);
		string text = Data.m_Prefab;
		bool flag = false;
		if (Data.m_Prefab == "SmokeOld")
		{
			text = "INTERACTIVE_CampFire";
			flag = true;
		}
		if (Data.m_Prefab == "Smoke")
		{
			flag = true;
		}
		switch (text)
		{
		case "CORPSE_Convict_001":
		case "CORPSE_Convict_002":
		case "CORPSE_Convict_003":
			flag = true;
			break;
		}
		if (text == "INTERACTIVE_CampFireBurntOut")
		{
			flag = true;
		}
		if (text == "CONTAINER_MetalLockerA")
		{
			flag = true;
		}
		switch (text)
		{
		case "FireSmall":
		case "FireTiny":
		case "FireLarge":
		case "SnowPile":
		case "RockWall":
			flag = true;
			break;
		}
		if (text == "ExpeditionInteractive")
		{
			return SetInteractiveZone(Data.m_InteractiveData, gameObject, DebugPurpose);
		}
		GameObject gameObject2 = GetSemiPrefab(text);
		if (gameObject2 == null)
		{
			gameObject2 = BundleAsset<GameObject>(text);
		}
		if (gameObject2 == null)
		{
			gameObject2 = Resources.Load<GameObject>(text);
		}
		if ((bool)gameObject2)
		{
			GameObject gameObject3 = ((!(gameObject != null)) ? UnityEngine.Object.Instantiate(gameObject2, Data.m_Position, Data.m_Rotation) : gameObject);
			gameObject3.name = Data.m_Prefab;
			gameObject3.SetActive(value: true);
			ObjectGuid objectGuid = gameObject3.GetComponent<ObjectGuid>();
			if (objectGuid == null)
			{
				objectGuid = gameObject3.AddComponent<ObjectGuid>();
			}
			objectGuid.Set(Data.m_GUID);
			System.Collections.Generic.List<Container> componentList = new System.Collections.Generic.List<Container>();
			componentList = GetComponentsInChildrenRecursively(gameObject3.transform, componentList);
			if (gameObject3.GetComponent<Container>() != null)
			{
				componentList.Add(gameObject3.GetComponent<Container>());
			}
			for (int i = 0; i < componentList.Count; i++)
			{
				GameObject gameObject4 = componentList[i].gameObject;
				Container container = componentList[i];
				if (!gameObject4)
				{
					continue;
				}
				ObjectGuid objectGuid2 = gameObject4.GetComponent<ObjectGuid>();
				if (objectGuid2 == null)
				{
					objectGuid2 = gameObject4.AddComponent<ObjectGuid>();
				}
				string value = Data.m_GUID;
				if (i != 0)
				{
					value = Data.m_GUID + i;
				}
				objectGuid2.Set(value);
				if (gameObject4.GetComponent<Lock>() != null)
				{
					gameObject4.GetComponent<Lock>().m_ChanceLocked = 0f;
					gameObject4.GetComponent<Lock>().m_LockStateRolled = false;
					gameObject4.GetComponent<Lock>().RollLockedState();
				}
				container.m_RolledSpawnChance = false;
				container.m_SpawnChance = 100f;
				container.Start();
				if (sendMyPosition)
				{
					MelonLogger.Msg("REQUESTCONTAINERSTATE GUID " + Data.m_GUID);
					using Packet packet = new Packet(177);
					packet.Write(Data.m_Scene);
					packet.Write(Data.m_GUID);
					SendUDPData(packet);
				}
				if (iAmHost)
				{
					int containerState = MPSaveManager.GetContainerState(Data.m_Scene, Data.m_GUID);
					RemoveLootFromContainer(gameObject3, containerState);
				}
			}
			if (flag)
			{
				CustomPrefabWorkAround(gameObject3, Data.m_Prefab);
			}
			gameObject3.SetActive(value: true);
			return gameObject3;
		}
		return null;
	}

	public static void RemoveObjectByGUID(string GUID)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if ((bool)gameObject)
		{
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	public static GameObject SetInteractiveZone(DataStr.ExpeditionInteractiveData Data, GameObject Obj = null, bool DebugPurpose = false)
	{
		Comps.ExpeditionInteractive expeditionInteractive = null;
		if (Obj == null)
		{
			RemoveObjectByGUID(Data.m_GUID);
			Obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
			expeditionInteractive = Obj.AddComponent<Comps.ExpeditionInteractive>();
			Obj.AddComponent<ObjectGuid>().Set(Data.m_GUID);
			Obj.layer = vp_Layer.InteractiveProp;
		}
		else
		{
			expeditionInteractive = Obj.GetComponent<Comps.ExpeditionInteractive>();
		}
		BoxCollider component = Obj.GetComponent<BoxCollider>();
		MeshRenderer component2 = Obj.GetComponent<MeshRenderer>();
		if ((bool)component)
		{
			component.isTrigger = !DebugPurpose;
		}
		if ((bool)component2)
		{
			component2.enabled = DebugPurpose;
		}
		expeditionInteractive.m_Data = Data;
		expeditionInteractive.Load();
		Obj.SetActive(value: true);
		return Obj;
	}

	public static void NoCustomExp()
	{
		if (m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
		{
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "You can't use custom experience mode in online! This case to major desync!", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null);
		}
	}
}
