using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoop;

public class DataStr
{
	public class ServerConfigData
	{
		public bool m_FastConsumption = true;

		public bool m_DuppedSpawns;

		public bool m_DuppedContainers;

		public int m_PlayersSpawnType;

		public int m_FireSync = 2;

		public int m_CheatsMode = 2;

		public bool m_CheckModsValidation;

		public bool m_SaveScamProtection;

		public bool m_PVP = true;
	}

	public class ServerSettingsData
	{
		public ServerConfigData m_CFG = new ServerConfigData();

		public int m_MaxPlayers = 2;

		public int m_Port = 26950;

		public int m_Accessibility;

		public bool m_P2P = true;
	}

	public class SlicedJsonData
	{
		public bool m_Last;

		public string m_Str = "";

		public int m_Hash;

		public long m_CheckHash;

		public string m_GearName = "";

		public int m_SendTo;

		public ExtraDataForDroppedGear m_Extra = new ExtraDataForDroppedGear();
	}

	public class SlicedBytesData
	{
		public bool m_Last;

		public byte[] m_Data;

		public int m_Length;

		public int m_Hash;

		public int m_SendTo;

		public string m_Action = "";

		public int m_ExtraInt;
	}

	public class SlicedJsonDroppedGear
	{
		public string m_GearName = "";

		public string m_Json = "";

		public ExtraDataForDroppedGear m_Extra = new ExtraDataForDroppedGear();
	}

	public class ExtraDataForDroppedGear
	{
		public int m_DroppedTime;

		public int m_GoalTime;

		public string m_Dropper = "";

		public int m_Variant;

		public string m_GearName = "";

		public string m_PhotoGUID = "";

		public string m_ExpeditionNote = "";
	}

	public class AnimalArrow
	{
		public float m_Condition = 100f;

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Vector3 m_Angle = new Vector3(0f, 0f, 0f);

		public float m_Depth;

		public string m_LocaName = "";
	}

	public class AnimalsSpawnsShort
	{
		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public SpawnRegion m_SPR;

		public string m_GUID = "";

		public AnimalsSpawnsShort(SpawnRegion spR)
		{
			m_Position = spR.m_Center;
			m_SPR = spR;
			m_GUID = spR.GetComponent<ObjectGuid>().Get();
		}
	}

	public class AnimalCompactData
	{
		public string m_PrefabName = "";

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public string m_GUID = "";

		public int m_LastSeen;

		public float m_Health = 100f;

		public bool m_Bleeding;

		public int m_TimeOfBleeding;

		public string m_RegionGUID = "";

		public int m_LastController;

		public int m_LastAiMode;

		public int m_ArrowsCount;

		public List<AnimalArrow> m_Arrows = new List<AnimalArrow>();
	}

	public class AnimalSync
	{
		public Vector3 m_position = new Vector3(0f, 0f, 0f);

		public Quaternion m_rotation = new Quaternion(0f, 0f, 0f, 0f);

		public string m_guid = "";

		public string m_name = "";

		public float m_Hp = 100f;

		public bool m_Bleeding;

		public int m_Controller;

		public string m_ProxySave = "";

		public int m_LevelD;

		public string m_SpawnRegionGUID = "";
	}

	public class AnimalAnimsSync
	{
		public float AP_TurnAngle;

		public float AP_TurnSpeed;

		public float AP_Speed;

		public float AP_Wounded;

		public float AP_Roll;

		public float AP_Pitch;

		public float AP_TargetHeading;

		public float AP_TargetHeadingSmooth;

		public float AP_TapMeter;

		public int AP_AiState;

		public bool AP_Corpse;

		public bool AP_Dead;

		public int AP_DeadSide;

		public int AP_DamageBodyPart;

		public int AP_AttackId;

		public bool AP_Stunned;
	}

	public class AnimalKilled
	{
		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public string m_PrefabName = "";

		public string m_GUID = "";

		public string m_LevelGUID = "";

		public int m_CreatedTime;

		public float m_Meat;

		public int m_Guts;

		public int m_Hide;

		public bool m_Knocked;

		public string m_RegionGUID = "";
	}

	public class AnimalAligner
	{
		public string m_Proxy = "";

		public string m_Guid = "";
	}

	public class AnimalTrigger
	{
		public string m_Guid = "";

		public int m_Trigger;
	}

	public class PlayerEquipmentData
	{
		public string m_HoldingItem = "";

		public bool m_LightSourceOn;

		public bool m_HasAxe;

		public bool m_HasRifle;

		public bool m_HasRevolver;

		public bool m_HasMedkit;

		public int m_Arrows;

		public int m_Flares;

		public int m_BlueFlares;
	}

	public class PlayerClothingData
	{
		public string m_Hat = "";

		public string m_Top = "";

		public string m_Bottom = "";

		public string m_Boots = "";

		public string m_Scarf = "";

		public string m_Balaclava = "";
	}

	public class MultiPlayerClientData
	{
		public PlayerEquipmentData m_PlayerEquipmentData = new PlayerEquipmentData();

		public PlayerClothingData m_PlayerClothingData = new PlayerClothingData();

		public string m_Name = "";

		public Vector3 m_Position;

		public Quaternion m_Rotation;

		public int m_Levelid;

		public string m_AnimState = "Idle";

		public int m_SleepHours;

		public string m_HarvestingAnimal = "";

		public bool m_Mimic;

		public bool m_Used;

		public bool m_Dead;

		public int m_TicksOnScene;

		public int m_PreviousLevelId;

		public string m_LevelGuid = "";

		public string m_BrakingSounds = "";

		public BrokenFurnitureSync m_BrakingObject = new BrokenFurnitureSync();

		public bool m_HeavyBreath;

		public int m_BloodLosts;

		public bool m_NeedAntiseptic;

		public ContainerOpenSync m_Container;

		public string m_Plant = "";

		public bool m_Female;

		public int m_Character;

		public ShowShelterByOther m_Shelter;

		public bool m_Aiming;

		public float m_RadioFrequency;

		public Supporters.SupporterBenefits m_SupporterBenefits = new Supporters.SupporterBenefits();

		public bool m_IsLoading;

		public int m_LastWeatherRegion = 6;

		public int m_LastRegion = 6;

		public bool m_IsSafe;

		public string m_SteamOrEGSID = "";

		public bool m_FirstBoot = true;
	}

	public class MultiPlayerClientStatus
	{
		public int m_ID;

		public string m_Name = "";

		public bool m_Sleep;

		public bool m_Dead;

		public bool m_IsLoading;
	}

	public class PriorityActionForOtherPlayer
	{
		public string m_Action = "";

		public string m_DisplayText = "";

		public string m_ProcessText = "";

		public bool m_CancleOnMove;

		public float m_ActionDuration = 3f;

		public bool m_Hold = true;

		public bool m_EscCancle;
	}

	public class AffictionSync
	{
		public int m_Type;

		public int m_Location;

		public string m_Case = "";

		public bool m_ShouldBeTreated = true;
	}

	public class DeathContainerData
	{
		public string m_Guid = "";

		public string m_LevelKey = "";

		public string m_Owner = "";

		public string m_ContainerPrefab = "CONTAINER_BackPack";

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public int m_DeathTime = -1;

		public bool Equals(DeathContainerData other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_Guid == other.m_Guid)
			{
				return true;
			}
			return false;
		}
	}

	public class ContainerOpenSync : IEquatable<ContainerOpenSync>
	{
		public string m_Guid = "";

		public bool m_State;

		public int m_LevelID;

		public string m_LevelGUID = "";

		public bool m_Inspected = true;

		public bool Equals(ContainerOpenSync other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_Guid == other.m_Guid && m_LevelID == other.m_LevelID && m_LevelGUID == other.m_LevelGUID)
			{
				return true;
			}
			return false;
		}
	}

	public class ShootSync
	{
		public Vector3 m_position = new Vector3(0f, 0f, 0f);

		public Quaternion m_rotation = new Quaternion(0f, 0f, 0f, 0f);

		public string m_projectilename = "";

		public float m_skill;

		public Vector3 m_camera_forward = new Vector3(0f, 0f, 0f);

		public Vector3 m_camera_right = new Vector3(0f, 0f, 0f);

		public Vector3 m_camera_up = new Vector3(0f, 0f, 0f);

		public bool m_lookat;

		public string m_sceneguid = "";
	}

	public class HarvestStats
	{
		public float m_Meat;

		public int m_Guts;

		public int m_Hide;

		public string m_Guid = "";
	}

	public class WeatherProxies
	{
		public string m_WeatherProxy = "";

		public string m_WeatherTransitionProxy = "";

		public string m_WindProxy = "";
	}

	public class SaveSlotSync
	{
		public int m_Episode;

		public int m_SaveSlotType = 3;

		public int m_Seed;

		public int m_ExperienceMode = 2;

		public int m_Location;

		public string m_FixedSpawnScene = "";

		public Vector3 m_FixedSpawnPosition = new Vector3(0f, 0f, 0f);

		public string m_CustomExperienceStr = "";
	}

	public class MultiplayerChatMessage
	{
		public int m_Type;

		public string m_Message = "";

		public string m_By = "";

		public bool m_Global = true;

		public bool m_Private;

		public Text m_TextObj;
	}

	public class GearItemDataPacket
	{
		public string m_GearName = "";

		public string m_DataProxy = "";

		public float m_Water;

		public int m_SendedTo = -1;
	}

	public class DroppedGearItemDataPacket
	{
		public int m_GearID = -1;

		public Vector3 m_Position;

		public Quaternion m_Rotation;

		public int m_LevelID;

		public string m_LevelGUID = "";

		public int m_Hash;

		public ExtraDataForDroppedGear m_Extra = new ExtraDataForDroppedGear();
	}

	public class BrokenFurnitureSync : IEquatable<BrokenFurnitureSync>
	{
		public string m_Guid = "";

		public string m_ParentGuid = "";

		public int m_LevelID;

		public string m_LevelGUID = "";

		public bool m_Broken = true;

		public bool Equals(BrokenFurnitureSync other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_Guid == other.m_Guid && m_ParentGuid == other.m_ParentGuid && m_LevelID == other.m_LevelID && m_LevelGUID == other.m_LevelGUID)
			{
				return true;
			}
			return false;
		}
	}

	public class PickedGearSync : IEquatable<PickedGearSync>
	{
		public Vector3 m_Spawn = new Vector3(0f, 0f, 0f);

		public int m_LevelID;

		public string m_LevelGUID = "";

		public int m_PickerID;

		public int m_Recently;

		public int m_MyInstanceID;

		public string m_GearName = "";

		public bool Equals(PickedGearSync other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_LevelGUID == other.m_LevelGUID && m_Spawn == other.m_Spawn)
			{
				return true;
			}
			return false;
		}
	}

	public class ClimbingRopeSync : IEquatable<ClimbingRopeSync>
	{
		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public int m_LevelID;

		public string m_LevelGUID = "";

		public bool m_Deployed;

		public bool m_Snapped;

		public bool Equals(ClimbingRopeSync other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_LevelID == other.m_LevelID && m_LevelGUID == other.m_LevelGUID && m_Position == other.m_Position)
			{
				return true;
			}
			return false;
		}
	}

	public class HarvestableSyncData : IEquatable<HarvestableSyncData>
	{
		public string m_Guid = "";

		public string m_State = "";

		public bool Equals(HarvestableSyncData other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_Guid == other.m_Guid)
			{
				return true;
			}
			return false;
		}
	}

	public class ShowShelterByOther : IEquatable<ShowShelterByOther>
	{
		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public int m_LevelID;

		public string m_LevelGUID = "";

		public bool Equals(ShowShelterByOther other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_LevelID == other.m_LevelID && m_LevelGUID == other.m_LevelGUID && m_Position == other.m_Position)
			{
				return true;
			}
			return false;
		}
	}

	public class FireSourcesSync : IEquatable<FireSourcesSync>
	{
		public string m_Guid = "";

		public int m_LevelId;

		public string m_LevelGUID = "";

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public float m_Fuel;

		public float m_CurrentHeat;

		public float m_MaxHeat;

		public float m_HeatInnerRadius;

		public float m_HeatOuterRadius;

		public bool m_HeatMasked;

		public int m_RemoveIn;

		public bool m_IsCampfire;

		public string m_FuelName = "";

		public bool Equals(FireSourcesSync other)
		{
			if (other == null)
			{
				return false;
			}
			if (m_LevelId == other.m_LevelId && m_LevelGUID == other.m_LevelGUID && ((m_Guid != "" && m_Guid == other.m_Guid) || m_Position == other.m_Position))
			{
				return true;
			}
			return false;
		}
	}

	public class DictionaryElementToReNew
	{
		public int m_Key;

		public SlicedJsonDroppedGear m_Val;

		public DroppedGearItemDataPacket m_Val2;
	}

	public class ElementToModfy
	{
		public string m_GUID;

		public int m_Time;
	}

	public class BodyHarvestUnits
	{
		public float m_Meat;

		public int m_Guts;
	}

	public class MeleeDescripter
	{
		public float m_PlayerDamage;

		public float m_AnimalDamage;

		public bool m_BloodLoss;

		public bool m_Pain;

		public bool m_ClothingTearing;

		public float m_AttackSpeed = 1f;

		public float m_RetakeTime = 1f;
	}

	public class MultiplayerEmote
	{
		public string m_Name = "Anim";

		public string m_Animation = "Idle";

		public bool m_ForceCrouch;

		public bool m_ForceStandup;

		public bool m_FollowDollCamera;

		public bool m_NeedFreeHands;

		public bool m_LeftHandEmote;
	}

	public class CustomChallengeTaskData
	{
		public string m_Task = "";

		public int m_Timer;

		public int m_GoalVal = 1;

		public CustomChallengeTaskData(string Task, int Timer, int GoalVal)
		{
			m_Task = Task;
			m_Timer = Timer;
			m_GoalVal = GoalVal;
		}
	}

	public class CustomChallengeRules
	{
		public string m_Name = "";

		public string m_Description = "";

		public List<CustomChallengeTaskData> m_Tasks = new List<CustomChallengeTaskData>();

		public bool m_Lineal = true;

		public bool m_CompetitiveMode;

		public int m_ID;
	}

	public class CustomChallengeData
	{
		public int m_CurrentTask;

		public int m_Time;

		public bool m_Started;

		public List<int> m_Done = new List<int>();
	}

	public class DoorKey
	{
		public string m_Seed = "";

		public string m_Name = "";

		public bool m_Lead;

		public DoorKey(string Seed, string Name, bool Lead = false)
		{
			m_Seed = Seed;
			m_Name = Name;
			m_Lead = Lead;
		}
	}

	public class VoiceChatQueueElement
	{
		public byte[] m_VoiceData;

		public float m_Length;
	}

	public class DedicatedServerData
	{
		public string SaveSlot = "UnspecifiedSave";

		public bool ItemDupes;

		public bool ContainersDupes;

		public int SpawnStyle;

		public int MaxPlayers = 2;

		public bool UsingSteam;

		public int Ports = 26950;

		public string[] WhiteList;

		public string ServerName = "";

		public int Cheats = 2;

		public int SteamServerAccessibility = 2;

		public string RCON = "12345";

		public int DropUnloadPeriod = 5;

		public bool SaveScamProtection;

		public bool ModValidationCheck;

		public int ExperienceMode;

		public int StartRegion;

		public int Seed;

		public bool PVP;

		public int SavingPeriod = 60;

		public int RestartPerioud = 10800;
	}

	public enum TimeOfDayStatus
	{
		NightEndToDawn,
		DawnToMorning,
		MorningToMidday,
		MiddayToAfternoon,
		AfternoonToDusk,
		DuskToNightStart,
		NightStartToNightEnd
	}

	public class WeatherVolunteerData
	{
		public int WeatherType;

		public float WeatherDuration = 1f;

		public int CurrentRegion;

		public List<float> StageDuration = new List<float>();

		public List<float> StageTransition = new List<float>();

		public int SetIndex;

		public float HighMin;

		public float HighMax;

		public float LowMin;

		public float LowMax;

		public int CoolingHours;

		public int WarmingHours;

		public int PreviousStage = 10;
	}

	public enum SlicedBase64Purpose
	{
		Photo,
		Container
	}

	public class SlicedBase64Data
	{
		public string m_Slice = "";

		public int m_LastSliceIndex;

		public int m_SliceIndex;

		public string m_CheckSum = "";

		public string m_GUID = "";

		public int m_Purpose;
	}

	public class FakeRockCacheVisualData
	{
		public string m_Owner = "";

		public string m_LevelGUID = "";

		public string m_GUID = "";

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);
	}

	public class CougarStateSync
	{
		public bool m_Enabled;

		public string m_ActiveRegionName = "";

		public List<string> m_RegionNames = new List<string>();

		public List<int> m_ThreatLevels = new List<int>();

		public List<float> m_ThreatCooldowns = new List<float>();
	}

	public class TraderStateSync
	{
		public string m_SerializedState = "";
	}

	public class TravoisSync
	{
		public string m_GUID = "";

		public string m_LevelGUID = "";

		public int m_LevelID;

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public int m_CarriedBy = -1;
	}

	public class UniversalSyncableObject
	{
		public string m_Prefab = "";

		public string m_Scene = "";

		public string m_GUID = "";

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public int m_CreationTime;

		public int m_RemoveTime;

		public string m_ExpeditionBelong = "";

		public ExpeditionInteractiveData m_InteractiveData = new ExpeditionInteractiveData();

		public string m_ObjectGroup = "";
	}

	public class UniversalSyncableObjectSpawner
	{
		public string m_Prefab = "";

		public string m_GUID = "";

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public string m_Content = "";

		public ExpeditionInteractiveData m_InteractiveData = new ExpeditionInteractiveData();

		public string m_ObjectGroup = "";
	}

	public class Vector2Int
	{
		public int X;

		public int Y;

		public Vector2Int(int x, int y)
		{
			X = x;
			Y = y;
		}
	}

	public class WebhookSettings
	{
		public string Name = "";

		public string URL = "";
	}

	public class ExpeditionInteractiveData
	{
		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Vector3 m_Rotation = new Vector3(0f, 0f, 0f);

		public Vector3 m_Scale = new Vector3(1f, 1f, 1f);

		public string m_ObjectText = "Object";

		public string m_InteractText = "Interacting...";

		public float m_InteractTime = 1f;

		public string m_Tool = "";

		public string m_Material = "";

		public int m_MaterialCount = 1;

		public string m_GUID = "";

		public string m_Audio = "";

		public string m_Yield = "";

		public int m_YieldCount = 1;

		public ExpeditionManager.ExpeditionInteractiveImpact m_Impact;

		public string m_ObjectGroupToRemove = "";
	}

	public class Base64SliceBase
	{
		public string[] m_Slices;

		public int m_SlicesGot;

		public int m_SlicesNum;

		public Base64SliceBase(int SlicesNum)
		{
			m_SlicesNum = SlicesNum;
			m_Slices = new string[SlicesNum];
		}

		public void SetSlice(int Index, string Slice)
		{
			if (Index <= m_Slices.Length - 1)
			{
				if (string.IsNullOrEmpty(m_Slices[Index]))
				{
					m_SlicesGot++;
				}
				m_Slices[Index] = Slice;
			}
		}

		public bool IsReady()
		{
			return m_SlicesGot == m_SlicesNum;
		}
	}

	public class BufferedGearSpawners
	{
		public Dictionary<string, SceneGearSpawners> GearSpawners = new Dictionary<string, SceneGearSpawners>();
	}

	public class SceneGearSpawners
	{
		public string SceneName = "";

		public List<PrefabSpawnDescriptor> PrefabSpawns = new List<PrefabSpawnDescriptor>();

		public List<RandomSpawnObjectDescriptor> RandomSpawns = new List<RandomSpawnObjectDescriptor>();
	}

	public class GearSpawnerElement
	{
		public string GearName = "";

		public int Weight;

		public float Chance;

		public Vector3 Position = new Vector3(0f, 0f, 0f);

		public Quaternion Rotation = new Quaternion(0f, 0f, 0f, 0f);
	}

	public class RandomSpawnObjectDescriptor
	{
		public List<GearSpawnerElement> Gears = new List<GearSpawnerElement>();

		public int SpawnOnInterloper;

		public int SpawnOnStalker;

		public int SpawnOnVoyageur;

		public int SpawnOnPiligrim;
	}

	public class PrefabSpawnDescriptor
	{
		public List<GearSpawnerElement> Gears = new List<GearSpawnerElement>();

		public int ChanceOfNoSpawn;

		public int Min;

		public int Max;
	}
}
