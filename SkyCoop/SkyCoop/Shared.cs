using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GameServer;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using MelonLoader;
using TinyJSON;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoop;

public class Shared
{
	public enum GameRegion
	{
		KeepersPassSouth = -5,
		KeepersPassNorth,
		WindingRiver,
		Ravine,
		CrumblingHighWay,
		MysteryLake,
		CoastalHighWay,
		DesolationPoint,
		PlesantValley,
		TimberwolfMountain,
		ForlornMuskeg,
		RandomRegion,
		FutureRegion,
		MountainTown,
		BrokenRailroad,
		HushedRiverValley,
		BleakInlet,
		AshCanyon,
		Blackrock,
		ForsakenAirfield,
		TransferPass,
		ZoneOfContamination,
		SunderedPass,
		FarRangeBranchLine
	}

	public enum LoggerColor
	{
		Red,
		Green,
		Blue,
		Yellow,
		Magenta,
		White
	}

	public class RegionWeatherControler
	{
		public int m_Region;

		public int m_WeatherType;

		public int m_WeatherSetIndex;

		public float m_Time;

		public float m_Duration;

		public float m_Progress;

		public bool m_WaitsForUpdate;

		public bool m_SearchingVolunteer;

		public System.Collections.Generic.List<float> m_StageDuration = new System.Collections.Generic.List<float>();

		public System.Collections.Generic.List<float> m_TransitionDuration = new System.Collections.Generic.List<float>();

		public int m_WeatherSeed = Guid.NewGuid().GetHashCode();

		public int m_WeatherTimeOfDayCount;

		public DataStr.TimeOfDayStatus m_PreviousTimeOfDayStatus;

		public int m_CoolingHours;

		public int m_WarmingHours;

		public float m_TempHighMin;

		public float m_TempHighMax;

		public float m_TempLowMin;

		public float m_TempLowhMax;

		public float m_HighTemp;

		public float m_LowTemp;

		public bool m_CanUpdateLowTemp;

		public bool m_CanUpdateHighTemp;

		public int m_PreviousStage = 10;

		public void RecalculateTemperture()
		{
			m_HighTemp = NextFloat(m_TempHighMin, m_TempHighMax);
			m_LowTemp = NextFloat(m_TempLowMin, m_TempLowhMax);
		}

		public void MayRecalculate()
		{
			int overridedHourse = MyMod.OverridedHourse;
			if (overridedHourse >= m_WarmingHours && overridedHourse < m_CoolingHours)
			{
				if (m_CanUpdateHighTemp)
				{
					m_HighTemp = NextFloat(m_TempHighMin, m_TempHighMax);
					Log("High Temperture Updated For Region " + m_Region, LoggerColor.Blue);
					m_CanUpdateHighTemp = false;
				}
				m_CanUpdateLowTemp = true;
			}
			else
			{
				if (m_CanUpdateLowTemp)
				{
					m_LowTemp = NextFloat(m_TempLowMin, m_TempLowhMax);
					Log("Low Temperture Updated For Region " + m_Region, LoggerColor.Blue);
					m_CanUpdateLowTemp = false;
				}
				m_CanUpdateHighTemp = true;
			}
		}

		public RegionWeatherControler(DataStr.WeatherVolunteerData Data)
		{
			m_Region = Data.CurrentRegion;
			m_WeatherSetIndex = Data.SetIndex;
			m_Duration = Data.WeatherDuration;
			m_WeatherType = Data.WeatherType;
			m_StageDuration = Data.StageDuration;
			m_TransitionDuration = Data.StageTransition;
			m_TempHighMin = Data.HighMin;
			m_TempHighMax = Data.HighMax;
			m_TempLowMin = Data.LowMin;
			m_TempLowhMax = Data.LowMax;
			m_CoolingHours = Data.CoolingHours;
			m_WarmingHours = Data.WarmingHours;
			m_PreviousStage = Data.PreviousStage;
			RecalculateTemperture();
		}

		public void SetNewSet(DataStr.WeatherVolunteerData Data)
		{
			m_WaitsForUpdate = false;
			m_Time = 0f;
			m_Progress = 0f;
			m_WeatherTimeOfDayCount = 0;
			m_WeatherSetIndex = Data.SetIndex;
			m_Duration = Data.WeatherDuration;
			m_WeatherType = Data.WeatherType;
			m_StageDuration = Data.StageDuration;
			m_TransitionDuration = Data.StageTransition;
			m_TempHighMin = Data.HighMin;
			m_TempHighMax = Data.HighMax;
			m_TempLowMin = Data.LowMin;
			m_TempLowhMax = Data.LowMax;
			m_CoolingHours = Data.CoolingHours;
			m_WarmingHours = Data.WarmingHours;
			m_PreviousStage = Data.PreviousStage;
			RecalculateTemperture();
		}

		public void AddTime(float Val)
		{
			if (m_WaitsForUpdate)
			{
				return;
			}
			if (m_Progress >= 1f)
			{
				m_WaitsForUpdate = true;
				m_SearchingVolunteer = true;
				m_Progress = 1f;
				m_Time = m_Duration;
				if (MyMod.iAmHost)
				{
					if (m_Region == MyMod.GetCurrentRegionIfPossible())
					{
						Log("WeatherSet is over, I am on same region, so going to provide weatherset for Region " + m_Region, LoggerColor.Blue);
						RegisterWeatherSetForRegion(0, Pathes.GetWeatherVolunteerData(DoNext: true));
					}
					else
					{
						Log("WeatherSet is over, searching weather volunteer for Region " + m_Region, LoggerColor.Blue);
						ServerSend.WEATHERVOLUNTEER(m_Region);
					}
				}
			}
			else
			{
				m_Time += Val;
				m_Progress = m_Time / m_Duration;
			}
		}

		public void AddTOD(int Ticks, DataStr.TimeOfDayStatus TODStatus)
		{
			if (m_PreviousTimeOfDayStatus != TODStatus)
			{
				m_PreviousTimeOfDayStatus = TODStatus;
				m_WeatherTimeOfDayCount = 0;
			}
			m_WeatherTimeOfDayCount += Ticks;
		}
	}

	public static int SecondsBeforeUnload = 0;

	public static int ExperienceForDS = 2;

	public static int StartingRegionDS = 0;

	public static System.Collections.Generic.Dictionary<string, DataStr.AnimalKilled> AnimalsKilled = new System.Collections.Generic.Dictionary<string, DataStr.AnimalKilled>();

	public static System.Collections.Generic.Dictionary<string, int> StunnedRabbits = new System.Collections.Generic.Dictionary<string, int>();

	public static System.Collections.Generic.List<RegionWeatherControler> RegionWeathers = new System.Collections.Generic.List<RegionWeatherControler>();

	public static System.Collections.Generic.Dictionary<string, DataStr.Base64SliceBase> Base64Slices = new System.Collections.Generic.Dictionary<string, DataStr.Base64SliceBase>();

	public static System.Collections.Generic.Dictionary<string, string> m_InterloperFilter = new System.Collections.Generic.Dictionary<string, string>();

	public static System.Collections.Generic.List<string> m_InterloperRare = new System.Collections.Generic.List<string>();

	public static System.Collections.Generic.List<float> HoursOffsetTable = new System.Collections.Generic.List<float> { 5f, 6f, 7f, 12f, 16.5f, 18f, 19.5f };

	public static DataStr.TimeOfDayStatus CurrentTimeOfDayStatus = DataStr.TimeOfDayStatus.NightEndToDawn;

	public static bool DSQuit = false;

	public static float LocalChatMaxDistance = 70f;

	public static int GameRegionNegativeOffset = 5;

	public static int GameRegionPositiveOffset = 18;

	public static readonly System.Collections.Generic.Dictionary<string, GameRegion> RegionSceneNames = new System.Collections.Generic.Dictionary<string, GameRegion>(StringComparer.OrdinalIgnoreCase)
	{
		{
			"LakeRegion",
			GameRegion.MysteryLake
		},
		{
			"CoastalRegion",
			GameRegion.CoastalHighWay
		},
		{
			"WhalingStationRegion",
			GameRegion.DesolationPoint
		},
		{
			"RuralRegion",
			GameRegion.PlesantValley
		},
		{
			"CrashMountainRegion",
			GameRegion.TimberwolfMountain
		},
		{
			"MarshRegion",
			GameRegion.ForlornMuskeg
		},
		{
			"MountainTownRegion",
			GameRegion.MountainTown
		},
		{
			"TracksRegion",
			GameRegion.BrokenRailroad
		},
		{
			"RiverValleyRegion",
			GameRegion.HushedRiverValley
		},
		{
			"CanneryRegion",
			GameRegion.BleakInlet
		},
		{
			"AshCanyonRegion",
			GameRegion.AshCanyon
		},
		{
			"BlackrockRegion",
			GameRegion.Blackrock
		},
		{
			"CanyonRoadTransitionZone",
			GameRegion.KeepersPassSouth
		},
		{
			"KeepersPassSouthRegion",
			GameRegion.KeepersPassSouth
		},
		{
			"BlackrockTransitionZone",
			GameRegion.KeepersPassNorth
		},
		{
			"KeepersPassNorthRegion",
			GameRegion.KeepersPassNorth
		},
		{
			"DamRiverTransitionZoneB",
			GameRegion.WindingRiver
		},
		{
			"WindingRiverRegion",
			GameRegion.WindingRiver
		},
		{
			"RavineTransitionZone",
			GameRegion.Ravine
		},
		{
			"RavineRegion",
			GameRegion.Ravine
		},
		{
			"HighwayTransitionZone",
			GameRegion.CrumblingHighWay
		},
		{
			"CrumblingHighwayRegion",
			GameRegion.CrumblingHighWay
		},
		{
			"DamTransitionZone",
			GameRegion.MysteryLake
		},
		{
			"AirfieldRegion",
			GameRegion.ForsakenAirfield
		},
		{
			"ForsakenAirfieldRegion",
			GameRegion.ForsakenAirfield
		},
		{
			"TransferPassRegion",
			GameRegion.TransferPass
		},
		{
			"ZoneOfContaminationRegion",
			GameRegion.ZoneOfContamination
		},
		{
			"ContaminationRegion",
			GameRegion.ZoneOfContamination
		},
		{
			"SundredPassRegion",
			GameRegion.SunderedPass
		},
		{
			"SunderedPassRegion",
			GameRegion.SunderedPass
		},
		{
			"FarRangeRegion",
			GameRegion.FarRangeBranchLine
		},
		{
			"FarRangeBranchLineRegion",
			GameRegion.FarRangeBranchLine
		}
	};

	public static readonly System.Collections.Generic.Dictionary<string, GameRegion> RegionAliases = new System.Collections.Generic.Dictionary<string, GameRegion>(StringComparer.OrdinalIgnoreCase)
	{
		{
			"MysteryLake",
			GameRegion.MysteryLake
		},
		{
			"CoastalHighway",
			GameRegion.CoastalHighWay
		},
		{
			"DesolationPoint",
			GameRegion.DesolationPoint
		},
		{
			"PleasantValley",
			GameRegion.PlesantValley
		},
		{
			"PlesantValley",
			GameRegion.PlesantValley
		},
		{
			"TimberwolfMountain",
			GameRegion.TimberwolfMountain
		},
		{
			"ForlornMuskeg",
			GameRegion.ForlornMuskeg
		},
		{
			"MountainTown",
			GameRegion.MountainTown
		},
		{
			"BrokenRailroad",
			GameRegion.BrokenRailroad
		},
		{
			"HushedRiverValley",
			GameRegion.HushedRiverValley
		},
		{
			"BleakInlet",
			GameRegion.BleakInlet
		},
		{
			"AshCanyon",
			GameRegion.AshCanyon
		},
		{
			"Blackrock",
			GameRegion.Blackrock
		},
		{
			"KeepersPassSouth",
			GameRegion.KeepersPassSouth
		},
		{
			"KeepersPassNorth",
			GameRegion.KeepersPassNorth
		},
		{
			"WindingRiver",
			GameRegion.WindingRiver
		},
		{
			"Ravine",
			GameRegion.Ravine
		},
		{
			"CrumblingHighway",
			GameRegion.CrumblingHighWay
		},
		{
			"ForsakenAirfield",
			GameRegion.ForsakenAirfield
		},
		{
			"Airfield",
			GameRegion.ForsakenAirfield
		},
		{
			"TransferPass",
			GameRegion.TransferPass
		},
		{
			"ZoneOfContamination",
			GameRegion.ZoneOfContamination
		},
		{
			"SunderedPass",
			GameRegion.SunderedPass
		},
		{
			"SundredPass",
			GameRegion.SunderedPass
		},
		{
			"FarRangeBranchLine",
			GameRegion.FarRangeBranchLine
		},
		{
			"FarRange",
			GameRegion.FarRangeBranchLine
		}
	};

	public static string ContainerDecompressedDataBackup = "";

	public static string ContainerGUIDDataBackup = "";

	public static bool CloseContainerOnCancle = false;

	public static bool IsMainMenuScene(string scene)
	{
		if (!(scene == "MainMenu"))
		{
			return scene?.StartsWith("MainMenu_", StringComparison.Ordinal) ?? false;
		}
		return true;
	}

	public static string GetSceneNameForRegion(GameRegion region)
	{
		foreach (System.Collections.Generic.KeyValuePair<string, GameRegion> regionSceneName in RegionSceneNames)
		{
			if (regionSceneName.Value == region && regionSceneName.Key.EndsWith("Region", StringComparison.OrdinalIgnoreCase))
			{
				return regionSceneName.Key;
			}
		}
		return "";
	}

	public static GameRegion GetRegionForSceneName(string sceneName)
	{
		if (string.IsNullOrEmpty(sceneName))
		{
			return GameRegion.RandomRegion;
		}
		if (RegionSceneNames.TryGetValue(sceneName, out var value))
		{
			return value;
		}
		if (!sceneName.EndsWith("Region", StringComparison.OrdinalIgnoreCase) && RegionSceneNames.TryGetValue(sceneName + "Region", out value))
		{
			return value;
		}
		if (RegionAliases.TryGetValue(sceneName, out value))
		{
			return value;
		}
		System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, GameRegion>> list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, GameRegion>>(RegionSceneNames);
		list.AddRange(RegionAliases);
		list.Sort((System.Collections.Generic.KeyValuePair<string, GameRegion> a, System.Collections.Generic.KeyValuePair<string, GameRegion> b) => b.Key.Length.CompareTo(a.Key.Length));
		foreach (System.Collections.Generic.KeyValuePair<string, GameRegion> item in list)
		{
			if (sceneName.IndexOf(item.Key, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return item.Value;
			}
		}
		return GameRegion.RandomRegion;
	}

	public static void NewDayBegins()
	{
	}

	public static void NewTimeOfDayState()
	{
		Log("New Time Of Day Status: " + CurrentTimeOfDayStatus, LoggerColor.Blue);
	}

	public static void UpdateTimeOfDayState()
	{
		float num = 1f;
		float num2 = (float)MyMod.OverridedHourse - num;
		if ((double)num2 < (double)HoursOffsetTable[0])
		{
			if (CurrentTimeOfDayStatus != DataStr.TimeOfDayStatus.NightStartToNightEnd)
			{
				CurrentTimeOfDayStatus = DataStr.TimeOfDayStatus.NightStartToNightEnd;
				NewTimeOfDayState();
			}
			return;
		}
		if ((double)num2 < (double)HoursOffsetTable[1])
		{
			if (CurrentTimeOfDayStatus != DataStr.TimeOfDayStatus.NightEndToDawn)
			{
				CurrentTimeOfDayStatus = DataStr.TimeOfDayStatus.NightEndToDawn;
				NewTimeOfDayState();
			}
			return;
		}
		if ((double)num2 < (double)HoursOffsetTable[2])
		{
			if (CurrentTimeOfDayStatus != DataStr.TimeOfDayStatus.DawnToMorning)
			{
				CurrentTimeOfDayStatus = DataStr.TimeOfDayStatus.DawnToMorning;
				NewTimeOfDayState();
			}
			return;
		}
		if ((double)num2 < (double)HoursOffsetTable[3])
		{
			if (CurrentTimeOfDayStatus != DataStr.TimeOfDayStatus.MorningToMidday)
			{
				CurrentTimeOfDayStatus = DataStr.TimeOfDayStatus.MorningToMidday;
				NewTimeOfDayState();
			}
			return;
		}
		if ((double)num2 < (double)HoursOffsetTable[4])
		{
			if (CurrentTimeOfDayStatus != DataStr.TimeOfDayStatus.MiddayToAfternoon)
			{
				CurrentTimeOfDayStatus = DataStr.TimeOfDayStatus.MiddayToAfternoon;
				NewTimeOfDayState();
			}
			return;
		}
		if ((double)num2 < (double)HoursOffsetTable[5])
		{
			if (CurrentTimeOfDayStatus != DataStr.TimeOfDayStatus.AfternoonToDusk)
			{
				CurrentTimeOfDayStatus = DataStr.TimeOfDayStatus.AfternoonToDusk;
				NewTimeOfDayState();
			}
			return;
		}
		DataStr.TimeOfDayStatus timeOfDayStatus = (((double)num2 < (double)HoursOffsetTable[6]) ? DataStr.TimeOfDayStatus.DuskToNightStart : DataStr.TimeOfDayStatus.NightStartToNightEnd);
		if (timeOfDayStatus != CurrentTimeOfDayStatus)
		{
			CurrentTimeOfDayStatus = timeOfDayStatus;
			NewTimeOfDayState();
		}
	}

	public static void ForceNextWeatherSet()
	{
		foreach (RegionWeatherControler regionWeather in RegionWeathers)
		{
			if (!regionWeather.m_WaitsForUpdate)
			{
				regionWeather.m_WaitsForUpdate = true;
				regionWeather.m_SearchingVolunteer = true;
			}
		}
	}

	public static void ForceNextWeather()
	{
		foreach (RegionWeatherControler regionWeather in RegionWeathers)
		{
			if (regionWeather.m_WaitsForUpdate)
			{
				continue;
			}
			if (regionWeather.m_StageDuration.Count == 1)
			{
				regionWeather.m_WaitsForUpdate = true;
				regionWeather.m_SearchingVolunteer = true;
				continue;
			}
			float num = 0f;
			for (int i = 0; i < regionWeather.m_StageDuration.Count; i++)
			{
				num += regionWeather.m_StageDuration[i];
				if (num > regionWeather.m_Time)
				{
					regionWeather.m_Time = regionWeather.m_StageDuration[i];
					break;
				}
			}
		}
	}

	public static void RegisterWeatherSetForRegion(int ClientID, DataStr.WeatherVolunteerData Data)
	{
		foreach (RegionWeatherControler regionWeather in RegionWeathers)
		{
			if (regionWeather.m_Region == Data.CurrentRegion)
			{
				if (regionWeather.m_WaitsForUpdate)
				{
					regionWeather.m_SearchingVolunteer = false;
					regionWeather.SetNewSet(Data);
					Log("New WeatherSet for Region " + Data.CurrentRegion + " provided by Client " + ClientID, LoggerColor.Blue);
				}
				return;
			}
		}
		Log("New WeatherSet for Region " + Data.CurrentRegion + " provided by Client " + ClientID, LoggerColor.Blue);
		RegionWeathers.Add(new RegionWeatherControler(Data));
	}

	public static void WeatherUpdate(int Minutes = 1)
	{
		float num = 0.016f;
		foreach (RegionWeatherControler regionWeather in RegionWeathers)
		{
			if (!regionWeather.m_WaitsForUpdate)
			{
				regionWeather.AddTime(num * (float)Minutes);
				regionWeather.MayRecalculate();
			}
		}
	}

	public static void WeatherUpdateSecond()
	{
		float num = 0.016f;
		foreach (RegionWeatherControler regionWeather in RegionWeathers)
		{
			if (!regionWeather.m_WaitsForUpdate)
			{
				regionWeather.AddTime(num / 60f);
				regionWeather.AddTOD(90, CurrentTimeOfDayStatus);
				if (regionWeather.m_Region == MyMod.GetCurrentRegionIfPossible())
				{
					ClientHandle.DoWeatherSync(regionWeather.m_Progress, regionWeather.m_WeatherSeed, regionWeather.m_Duration, (WeatherStage)regionWeather.m_WeatherType, regionWeather.m_WeatherSetIndex, regionWeather.m_StageDuration, regionWeather.m_TransitionDuration, regionWeather.m_WeatherTimeOfDayCount, regionWeather.m_HighTemp, regionWeather.m_LowTemp, regionWeather.m_PreviousStage);
				}
				ServerSend.DEDICATEDWEATHER(regionWeather.m_Region, regionWeather.m_WeatherType, regionWeather.m_WeatherSetIndex, regionWeather.m_Progress, regionWeather.m_WeatherSeed, regionWeather.m_Duration, regionWeather.m_StageDuration, regionWeather.m_TransitionDuration, regionWeather.m_WeatherTimeOfDayCount, regionWeather.m_HighTemp, regionWeather.m_LowTemp, regionWeather.m_PreviousStage);
			}
		}
	}

	public static void OnUpdate()
	{
	}

	public static void EverySecond()
	{
		if (!MyMod.iAmHost)
		{
			return;
		}
		MPStats.EverySecond();
		ExpeditionManager.UpdateExpeditions();
		SecondsBeforeUnload++;
		if (SecondsBeforeUnload > MPSaveManager.SaveRecentTimer)
		{
			SecondsBeforeUnload = 0;
			MPSaveManager.SaveRecentStuff();
		}
		MPSaveManager.UpdateKnockDoorRequests();
		ServerSend.KEEPITALIVE(0, _msg: true);
		UpdateStunnedRabbits();
		MyMod.UpdatePlayerStatusMenu(SleepTrackerAndTimeOutAndAnimalControllers());
		WeatherUpdateSecond();
		SafeZoneManager.UpdatePlayersSafeZoneStatus();
		for (int i = 0; i < MyMod.RecentlyPickedGears.Count; i++)
		{
			DataStr.PickedGearSync pickedGearSync = MyMod.RecentlyPickedGears[i];
			if (pickedGearSync != null)
			{
				if (pickedGearSync.m_Recently > 0)
				{
					pickedGearSync.m_Recently--;
				}
				if (pickedGearSync.m_Recently <= 0)
				{
					MyMod.RecentlyPickedGears.RemoveAt(i);
				}
			}
		}
	}

	public static void EveryInGameMinute(int MinutesSkipped = 1)
	{
		MyMod.OverridedMinutes++;
		if (MyMod.OverridedMinutes > 59)
		{
			MyMod.OverridedMinutes = 0;
			MyMod.OverridedHourse++;
			MyMod.PlayedHoursInOnline++;
			UpdateTimeOfDayState();
		}
		if (MyMod.OverridedHourse > 23)
		{
			MyMod.OverridedHourse = 0;
			UpdateTimeOfDayState();
		}
		MyMod.OveridedTime = MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes;
		if (MyMod.iAmHost)
		{
			MyMod.MinutesFromStartServer++;
			ServerSend.GAMETIME(MyMod.OveridedTime);
			UpdateTicksOnScenes();
			WeatherUpdate(MinutesSkipped);
		}
	}

	public static void Log(string TXT, LoggerColor Color = LoggerColor.White)
	{
		MelonLogger.Msg(MyMod.ConvertLoggerColor(Color), TXT);
	}

	public static bool HasNonASCIIChars(string str)
	{
		return Encoding.UTF8.GetByteCount(str) != str.Length;
	}

	// string.GetHashCode() is randomized per process on .NET 6, so it must never feed shared seeds or keys:
	// players rolled different scattered loot and dropped-gear keys changed between launches.
	public static int StableHash(string text)
	{
		uint num = 2166136261u;
		if (text != null)
		{
			foreach (char c in text)
			{
				num = (num ^ c) * 16777619;
			}
		}
		return (int)num;
	}

	public static int GetVectorHash(Vector3 v3)
	{
		return (v3.x + v3.y + v3.z).GetHashCode();
	}

	public static int GetQuaternionHash(Quaternion Quat)
	{
		return (Quat.x + Quat.y + Quat.z + Quat.w).GetHashCode();
	}

	public static long GetVectorHashV2(Vector3 v3)
	{
		return GetDeterministicId(v3.x.ToString() + v3.y + v3.z);
	}

	public static bool IsZero(float a, float epsilon = 0.0001f)
	{
		return (double)Math.Abs(a) <= (double)epsilon;
	}

	public static bool IsZero(double a, double epsilon = 9.999999747378752E-05)
	{
		return Math.Abs(a) <= epsilon;
	}

	public static bool Approximately(double a, double b, double epsilon = 0.0001)
	{
		return IsZero(a - b, epsilon);
	}

	public static bool RollChance(float percent)
	{
		if (!IsZero(percent))
		{
			if (!Approximately(percent, 100.0, 9.999999747378752E-05))
			{
				return (double)NextFloat(0f, 100f) < (double)percent;
			}
			return true;
		}
		return false;
	}

	public static void ModifyDynamicGears(string Scene)
	{
		System.Collections.Generic.Dictionary<int, DataStr.DroppedGearItemDataPacket> dictionary = MPSaveManager.LoadDropVisual(Scene);
		System.Collections.Generic.Dictionary<int, DataStr.SlicedJsonDroppedGear> dictionary2 = MPSaveManager.LoadDropData(Scene);
		if (dictionary == null || dictionary2 == null)
		{
			return;
		}
		System.Collections.Generic.List<DataStr.DictionaryElementToReNew> list = new System.Collections.Generic.List<DataStr.DictionaryElementToReNew>();
		System.Collections.Generic.List<Vector3> list2 = new System.Collections.Generic.List<Vector3>();
		foreach (System.Collections.Generic.KeyValuePair<int, DataStr.SlicedJsonDroppedGear> item in dictionary2)
		{
			int key = item.Key;
			DataStr.SlicedJsonDroppedGear value = item.Value;
			if (!value.m_GearName.ToLower().Contains("gear_snare") || (value.m_Extra.m_Variant != 1 && value.m_Extra.m_Variant != 4))
			{
				continue;
			}
			int num = MyMod.MinutesFromStartServer - value.m_Extra.m_DroppedTime;
			int num2 = 720;
			int num3 = num2 - num + 1;
			float percent = 50f;
			float percent2 = 15f;
			if (num3 <= 0)
			{
				dictionary.TryGetValue(key, out var value2);
				if (value.m_Extra.m_Variant == 4 || Utils.RollChance(percent))
				{
					value.m_Extra.m_Variant = 3;
					value.m_Extra.m_GoalTime = -1;
					GearItemSaveDataProxy gearItemSaveDataProxy = Utils.DeserializeObject<GearItemSaveDataProxy>(value.m_Json);
					SnareItem snareItem = Utils.DeserializeObject<SnareItem>(gearItemSaveDataProxy.m_SnareItemSerialized);
					snareItem.m_State = (SnareState)value.m_Extra.m_Variant;
					gearItemSaveDataProxy.m_SnareItemSerialized = snareItem.Serialize();
					value.m_Json = Utils.SerializeObject(gearItemSaveDataProxy);
					list2.Add(gearItemSaveDataProxy.m_Position);
				}
				else if (Utils.RollChance(percent2))
				{
					value.m_Extra.m_Variant = 2;
					value.m_Extra.m_GoalTime = -1;
					GearItemSaveDataProxy gearItemSaveDataProxy2 = Utils.DeserializeObject<GearItemSaveDataProxy>(value.m_Json);
					SnareItem snareItem2 = Utils.DeserializeObject<SnareItem>(gearItemSaveDataProxy2.m_SnareItemSerialized);
					snareItem2.m_State = (SnareState)value.m_Extra.m_Variant;
					gearItemSaveDataProxy2.m_SnareItemSerialized = snareItem2.Serialize();
					value.m_Json = Utils.SerializeObject(gearItemSaveDataProxy2);
				}
				else
				{
					value.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
					value.m_Extra.m_GoalTime = MyMod.MinutesFromStartServer + num2;
				}
				DataStr.DictionaryElementToReNew dictionaryElementToReNew = new DataStr.DictionaryElementToReNew();
				dictionaryElementToReNew.m_Key = key;
				dictionaryElementToReNew.m_Val = value;
				if (value2 != null)
				{
					value2.m_Extra = value.m_Extra;
					dictionaryElementToReNew.m_Val2 = value2;
				}
				list.Add(dictionaryElementToReNew);
			}
		}
		if (list.Count > 0)
		{
			for (int i = 0; i < list.Count; i++)
			{
				int key2 = list[i].m_Key;
				dictionary2.Remove(key2);
				dictionary2.Add(key2, list[i].m_Val);
				if (list[i].m_Val2 != null)
				{
					dictionary.Remove(key2);
					dictionary.Add(key2, list[i].m_Val2);
				}
			}
		}
		if (list2.Count > 0)
		{
			for (int j = 0; j < list2.Count; j++)
			{
				DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = new DataStr.SlicedJsonDroppedGear();
				slicedJsonDroppedGear.m_GearName = "gear_rabbitcarcass";
				slicedJsonDroppedGear.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
				slicedJsonDroppedGear.m_Extra.m_Dropper = "Snare";
				slicedJsonDroppedGear.m_Extra.m_GearName = "gear_rabbitcarcass";
				string json = "";
				int num4 = 0;
				Vector3 vector = list2[j];
				Quaternion quaternion = new Quaternion(0f, 0f, 0f, 0f);
				GameObject gearItemObject = MyMod.GetGearItemObject("gear_rabbitcarcass");
				if (gearItemObject != null)
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(gearItemObject, vector, quaternion);
					GearItem component = gameObject.GetComponent<GearItem>();
					component.SkipSpawnChanceRollInitialDecayAndAutoEvolve();
					BodyHarvest bodyHarvest = gameObject.GetComponent<BodyHarvest>();
					if (bodyHarvest == null)
					{
						bodyHarvest = gameObject.AddComponent<BodyHarvest>();
					}
					gameObject.name = "gear_rabbitcarcass";
					component.m_CurrentHP = bodyHarvest.GetCondition() / 100f * component.m_GearItemData.MaxHP;
					json = gameObject.GetComponent<GearItem>().SerializeToString();
					int vectorHash = GetVectorHash(vector);
					int quaternionHash = GetQuaternionHash(quaternion);
					int hashCode = StableHash(Scene);
					num4 = vectorHash + quaternionHash + hashCode;
					UnityEngine.Object.Destroy(gameObject);
				}
				DataStr.DroppedGearItemDataPacket droppedGearItemDataPacket = new DataStr.DroppedGearItemDataPacket();
				droppedGearItemDataPacket.m_Extra = slicedJsonDroppedGear.m_Extra;
				droppedGearItemDataPacket.m_GearID = -1;
				droppedGearItemDataPacket.m_Hash = num4;
				droppedGearItemDataPacket.m_LevelGUID = Scene;
				droppedGearItemDataPacket.m_Position = list2[j];
				droppedGearItemDataPacket.m_Rotation = new Quaternion(0f, 0f, 0f, 0f);
				slicedJsonDroppedGear.m_Json = json;
				dictionary2.Add(num4, slicedJsonDroppedGear);
				dictionary.Add(num4, droppedGearItemDataPacket);
			}
		}
		if (MPSaveManager.RecentData.ContainsKey(Scene))
		{
			MPSaveManager.RecentData.Remove(Scene);
		}
		MPSaveManager.RecentData.Add(Scene, dictionary2);
		if (MPSaveManager.RecentVisual.ContainsKey(Scene))
		{
			MPSaveManager.RecentVisual.Remove(Scene);
		}
		MPSaveManager.RecentVisual.Add(Scene, dictionary);
	}

	public static void UpdateTicksOnScenes()
	{
		for (int i = 0; i < MyMod.MaxPlayers; i++)
		{
			if (i == 0)
			{
				MyMod.MyTicksOnScene++;
			}
			else
			{
				if (MyMod.playersData[i] == null)
				{
					continue;
				}
				if (Server.clients[i].IsBusy() && MyMod.playersData[i].m_AnimState != "Knock")
				{
					if (MyMod.playersData[i].m_Levelid != MyMod.playersData[i].m_PreviousLevelId)
					{
						MyMod.playersData[i].m_PreviousLevelId = MyMod.playersData[i].m_Levelid;
						MyMod.playersData[i].m_TicksOnScene = 0;
					}
					else
					{
						MyMod.playersData[i].m_TicksOnScene = MyMod.playersData[i].m_TicksOnScene + 1;
					}
				}
				else
				{
					MyMod.playersData[i].m_TicksOnScene = 0;
				}
			}
		}
	}

	public static void SkipRTTime(int h)
	{
		int num = MyMod.OverridedHourse + h;
		MyMod.PlayedHoursInOnline += h;
		if (num > 24)
		{
			MyMod.OverridedHourse = num - 24;
		}
		else
		{
			MyMod.OverridedHourse += h;
		}
		int minutesFromStartServer = MyMod.MinutesFromStartServer;
		MyMod.MinutesFromStartServer += h * 60;
		Log("Skipping " + h + " hour(s) now should be " + MyMod.OverridedHourse);
		Log("MinutesFromStartServer " + minutesFromStartServer + " now it " + MyMod.MinutesFromStartServer + " because " + h * 60 + " been added");
		EveryInGameMinute(h * 60);
	}

	public static void ProcessSleep(int Sleepers, int SleepersNeed, int Deads, int FinalHours)
	{
		bool flag = Sleepers >= SleepersNeed && Deads < SleepersNeed;
		flag = SleepRendezvousPolicy.AllReady(Sleepers, SleepersNeed, Deads);
		SleepRendezvousFix.ProcessReadiness(Sleepers, SleepersNeed, flag, FinalHours);
		if (!MyMod.iAmHost)
		{
			return;
		}
		if (flag)
		{
			if ((!GameManager.GetPlayerManagerComponent().PlayerIsSleeping() && !MyMod.IsDead) || MyMod.IsCycleSkiping)
			{
				return;
			}
			Log("Everyone sleep going to skip " + FinalHours);
			MyMod.IsCycleSkiping = true;
			int num = FinalHours;
			for (int i = 0; i < MyMod.playersData.Count; i++)
			{
				if (MyMod.playersData[i] != null)
				{
					MyMod.playersData[i].m_SleepHours = 0;
				}
			}
			if (num > 0)
			{
				if (MyMod.IsDead)
				{
					MyMod.SimpleSleepWithNoSleep(num);
				}
				SkipRTTime(num);
			}
		}
		else
		{
			MyMod.IsCycleSkiping = false;
		}
	}

	private static readonly System.Collections.Generic.Dictionary<int, bool> LastAnimalRoles = new System.Collections.Generic.Dictionary<int, bool>();

	public static bool ShouldbeAnimalController(int _Ticks, int _Level, int _From)
	{
		if (_From <= 0 || _From >= MyMod.playersData.Count)
		{
			return false;
		}
		DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[_From];
		if (multiPlayerClientData == null || !Server.clients.ContainsKey(_From))
		{
			return false;
		}
		string levelGuid = multiPlayerClientData.m_LevelGuid;
		if (!WildlifeSpawnAuthorityPolicy.Ready(Server.clients[_From].IsBusy(), multiPlayerClientData.m_IsLoading, multiPlayerClientData.m_Dead, multiPlayerClientData.m_AnimState == "Knock", multiPlayerClientData.m_TicksOnScene, levelGuid))
		{
			return false;
		}
		if (!MyMod.DedicatedServerAppMode && MyMod.WildlifeSceneReady() && MyMod.level_guid == levelGuid)
		{
			return false;
		}
		for (int i = 1; i < _From; i++)
		{
			if (i < MyMod.playersData.Count && Server.clients.ContainsKey(i))
			{
				DataStr.MultiPlayerClientData multiPlayerClientData2 = MyMod.playersData[i];
				if (multiPlayerClientData2 != null && multiPlayerClientData2.m_LevelGuid == levelGuid && WildlifeSpawnAuthorityPolicy.Ready(Server.clients[i].IsBusy(), multiPlayerClientData2.m_IsLoading, multiPlayerClientData2.m_Dead, multiPlayerClientData2.m_AnimState == "Knock", multiPlayerClientData2.m_TicksOnScene, multiPlayerClientData2.m_LevelGuid))
				{
					return false;
				}
			}
		}
		return true;
	}

	public static System.Collections.Generic.List<DataStr.MultiPlayerClientStatus> SleepTrackerAndTimeOutAndAnimalControllers()
	{
		System.Collections.Generic.List<DataStr.MultiPlayerClientStatus> list = new System.Collections.Generic.List<DataStr.MultiPlayerClientStatus>();
		using Packet packet = new Packet(43);
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		System.Collections.Generic.List<int> list2 = new System.Collections.Generic.List<int>();
		list2.Add(MyMod.MyCycleSkip);
		if (!MyMod.DedicatedServerAppMode)
		{
			num++;
			DataStr.MultiPlayerClientStatus multiPlayerClientStatus = new DataStr.MultiPlayerClientStatus();
			multiPlayerClientStatus.m_ID = 0;
			multiPlayerClientStatus.m_Name = MyMod.MyChatName;
			multiPlayerClientStatus.m_Sleep = MyMod.IsSleeping;
			multiPlayerClientStatus.m_IsLoading = false;
			multiPlayerClientStatus.m_Dead = MyMod.IsDead;
			if (multiPlayerClientStatus.m_Sleep || multiPlayerClientStatus.m_Dead)
			{
				num2++;
			}
			if (multiPlayerClientStatus.m_Dead)
			{
				num3++;
			}
			list.Add(multiPlayerClientStatus);
		}
		for (int i = 1; i <= Server.MaxPlayers; i++)
		{
			if (Server.clients[i] == null || !Server.clients[i].IsBusy() || Server.clients[i].RCON)
			{
				continue;
			}
			num++;
			DataStr.MultiPlayerClientStatus multiPlayerClientStatus2 = new DataStr.MultiPlayerClientStatus();
			multiPlayerClientStatus2.m_ID = i;
			multiPlayerClientStatus2.m_Name = MyMod.playersData[i].m_Name;
			if (MyMod.playersData[i].m_SleepHours > 0)
			{
				multiPlayerClientStatus2.m_Sleep = true;
			}
			else
			{
				multiPlayerClientStatus2.m_Sleep = false;
			}
			if (MyMod.playersData[i].m_AnimState == "Knock")
			{
				multiPlayerClientStatus2.m_Dead = true;
			}
			else
			{
				multiPlayerClientStatus2.m_Dead = false;
			}
			multiPlayerClientStatus2.m_IsLoading = ClientIsLoading(i);
			if (multiPlayerClientStatus2.m_Sleep || multiPlayerClientStatus2.m_Dead)
			{
				num2++;
			}
			if (multiPlayerClientStatus2.m_Dead)
			{
				num3++;
			}
			if (MyMod.iAmHost)
			{
				list2.Add(MyMod.playersData[multiPlayerClientStatus2.m_ID].m_SleepHours);
			}
			list.Add(multiPlayerClientStatus2);
			int num4 = MyMod.TimeOutSeconds;
			if (ClientIsLoading(i))
			{
				num4 = MyMod.TimeOutSecondsForLoaders;
			}
			if (Server.clients[i].RCON)
			{
				num4 = 300;
			}
			Server.clients[i].TimeOutTime = Server.clients[i].TimeOutTime + 1;
			if (Server.clients[i].TimeOutTime > num4)
			{
				bool flag = false;
				string name = MyMod.playersData[i].m_Name;
				Server.clients[i].TimeOutTime = 0;
				if (!Server.clients[i].RCON)
				{
					DataStr.MultiplayerChatMessage multiplayerChatMessage = new DataStr.MultiplayerChatMessage();
					multiplayerChatMessage.m_Type = 0;
					multiplayerChatMessage.m_By = MyMod.playersData[i].m_Name;
					multiplayerChatMessage.m_Message = MyMod.playersData[i].m_Name + " disconnected!";
					ServerSend.KICKMESSAGE(i, "The host has disconnected you from the server due to a long period without receiving data from you.");
					if (!string.IsNullOrEmpty(name))
					{
						SendMessageToChat(multiplayerChatMessage);
						flag = true;
					}
				}
				else
				{
					ServerSend.KICKMESSAGE(i, "Your RCON session is over, please reconnect.");
				}
				Server.clients[i].RCON = false;
				ResetDataForSlot(i);
				Log("Client " + i + " processing disconnect");
				Server.clients[i].udp.Disconnect();
				if (flag)
				{
					WebhookPlayerLeave(name);
				}
			}
			if (MyMod.playersData[i] != null && Server.clients[i].IsBusy())
			{
				bool flag2 = false;
				bool controller = MyMod.playersData[i].m_AnimState != "Knock" && ShouldbeAnimalController(MyMod.playersData[i].m_TicksOnScene, MyMod.playersData[i].m_Levelid, i);
				if (!LastAnimalRoles.TryGetValue(i, out bool lastRole) || lastRole != controller)
				{
					LastAnimalRoles[i] = controller;
					Log("[Wildlife] Client " + i + " animal controller=" + controller + " scene=" + MyMod.playersData[i].m_LevelGuid + " hostScene=" + MyMod.level_guid + " hostReady=" + MyMod.WildlifeSceneReady());
				}
				ServerSend.ANIMALROLE(_msg: controller, _toClient: i);
			}
		}
		list2.Sort();
		int num5 = 0;
		if (list2.Count > 0)
		{
			num5 = SleepRendezvousPolicy.MinimumRequestedHours(list2);
		}
		packet.Write(num);
		for (int j = 0; j < list.Count; j++)
		{
			packet.Write(list[j]);
		}
		packet.Write(num5);
		ServerSend.SendUDPDataToAll(packet);
		MyMod.PlayersOnServer = num;
		ProcessSleep(num2, num, num3, num5);
		return list;
	}

	public static bool IsLocksmithItem(string name)
	{
		switch (name.ToLower())
		{
		case "gear_scrapmetal":
		case "gear_scmetalblanksmall":
		case "gear_scdoorkeytemp":
		case "gear_scmetalblank":
		case "gear_scdoorkeyleadtemp":
			return true;
		default:
			return false;
		}
	}

	public static string GetLockSmithProduct(string GearName, int Tool)
	{
		switch (GearName)
		{
		case "gear_scrapmetal":
			switch (Tool)
			{
			case 0:
				return "gear_sclockpick";
			case 1:
				return "broken";
			}
			break;
		case "gear_scmetalblank":
			switch (Tool)
			{
			case 0:
				return "gear_scmetalblanksmall";
			case 1:
				return "gear_scmetalblank";
			}
			break;
		case "gear_scmetalblanksmall":
			switch (Tool)
			{
			case 0:
				return "gear_scdoorkeytemp";
			case 1:
				return "gear_scmetalblanksmall";
			}
			break;
		case "gear_scdoorkeytemp":
			switch (Tool)
			{
			case 0:
				return "broke";
			case 1:
				return "gear_scdoorkey";
			}
			break;
		case "gear_scdoorkeyleadtemp":
			switch (Tool)
			{
			case 0:
				return "broke";
			case 1:
				return "gear_scdoorkeylead";
			}
			break;
		case "gear_scwoodform":
			if (Tool == 5)
			{
				return "gear_scwoodformb";
			}
			break;
		}
		return "broke";
	}

	public static void ChangeOpenableThingState(string Scene, string GUID, bool state)
	{
		MyMod.OpenableThings.Remove(GUID);
		MyMod.OpenableThings.Add(GUID, state);
		Log("Openable things " + GUID + " changed state to OpenIs=" + state);
		if (MyMod.iAmHost)
		{
			ServerSend.USEOPENABLE(0, GUID, state, toAll: true);
			MPSaveManager.ChangeOpenableThingState(Scene, GUID, state);
		}
	}

	public static void SendContainerData(string DataProxy, string LevelKey, string GUID, string DecompressedBackup, int SendTo = 0)
	{
		if (!string.IsNullOrEmpty(DecompressedBackup) && MyMod.sendMyPosition)
		{
			ContainerDecompressedDataBackup = DecompressedBackup;
		}
		ContainerGUIDDataBackup = GUID;
		if (MyMod.sendMyPosition)
		{
			ContainerLootSync.TrackUpload(LevelKey, GUID, DataProxy);
		}
		SendSlicedBase64Data(GetBase64Sliced(DataProxy, GUID + "|" + LevelKey, DataStr.SlicedBase64Purpose.Container), SendTo);
	}

	public static void SendDroppedItemToPicker(string DataProxy, int GiveItemTo, int SearchKey, int GearID, bool place, DataStr.ExtraDataForDroppedGear Extra)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(DataProxy);
		Log("Going to send gear to client " + GiveItemTo + " bytes: " + bytes.Length);
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
				slicedJsonData.m_GearName = MyMod.level_guid;
				slicedJsonData.m_SendTo = GearID;
				slicedJsonData.m_Hash = SearchKey;
				slicedJsonData.m_Str = str;
				if (list.Count != 0)
				{
					slicedJsonData.m_Last = false;
				}
				else
				{
					slicedJsonData.m_Last = true;
					slicedJsonData.m_Extra = Extra;
				}
				if (!place)
				{
					ServerSend.GETREQUESTEDITEMSLICE(GiveItemTo, slicedJsonData);
				}
				else
				{
					ServerSend.GETREQUESTEDFORPLACESLICE(GiveItemTo, slicedJsonData);
				}
			}
			if (list.Count < 500 && list.Count != 0)
			{
				byte[] bytes3 = list.GetRange(0, list.Count).ToArray();
				list.RemoveRange(0, list.Count);
				string str2 = Encoding.UTF8.GetString(bytes3);
				DataStr.SlicedJsonData slicedJsonData2 = new DataStr.SlicedJsonData();
				slicedJsonData2.m_GearName = MyMod.level_guid;
				slicedJsonData2.m_SendTo = GearID;
				slicedJsonData2.m_Hash = SearchKey;
				slicedJsonData2.m_Str = str2;
				slicedJsonData2.m_Last = true;
				slicedJsonData2.m_Extra = Extra;
				if (!place)
				{
					ServerSend.GETREQUESTEDITEMSLICE(GiveItemTo, slicedJsonData2);
				}
				else
				{
					ServerSend.GETREQUESTEDFORPLACESLICE(GiveItemTo, slicedJsonData2);
				}
			}
		}
		else
		{
			DataStr.SlicedJsonData slicedJsonData3 = new DataStr.SlicedJsonData();
			slicedJsonData3.m_GearName = MyMod.level_guid;
			slicedJsonData3.m_SendTo = GearID;
			slicedJsonData3.m_Hash = SearchKey;
			slicedJsonData3.m_Str = DataProxy;
			slicedJsonData3.m_Last = true;
			slicedJsonData3.m_Extra = Extra;
			if (!place)
			{
				ServerSend.GETREQUESTEDITEMSLICE(GiveItemTo, slicedJsonData3);
			}
			else
			{
				ServerSend.GETREQUESTEDFORPLACESLICE(GiveItemTo, slicedJsonData3);
			}
		}
	}

	public static void AnimalDestoryGear(int Hash, string Scene)
	{
		ServerSend.PICKDROPPEDGEAR(-1, Hash, toEveryOne: true);
		MyMod.DroppedGearsObjs.TryGetValue(Hash, out var value);
		if (value != null)
		{
			MyMod.DroppedGearsObjs.Remove(Hash);
			MyMod.TrackableDroppedGearsObjs.Remove(Hash);
			UnityEngine.Object.Destroy(value);
		}
		MPSaveManager.RequestSpecificGear(Hash, Scene);
	}

	public static void ClientTryPickupItem(int Hash, int sendTo, string Scene, bool place)
	{
		ServerSend.PICKDROPPEDGEAR(sendTo, Hash, toEveryOne: true);
		DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = MPSaveManager.RequestSpecificGear(Hash, Scene);
		if (!MyMod.DedicatedServerAppMode)
		{
			MyMod.DroppedGearsObjs.TryGetValue(Hash, out var value);
			if (value != null)
			{
				MyMod.DroppedGearsObjs.Remove(Hash);
				MyMod.TrackableDroppedGearsObjs.Remove(Hash);
				UnityEngine.Object.Destroy(value);
			}
		}
		if (slicedJsonDroppedGear != null)
		{
			Log("Found gear with hash " + Hash);
			SendDroppedItemToPicker(slicedJsonDroppedGear.m_Json, sendTo, Hash, -1, place, slicedJsonDroppedGear.m_Extra);
			if (!MyMod.DedicatedServerAppMode && MyMod.players[sendTo] != null && MyMod.players[sendTo].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
			{
				MyMod.players[sendTo].GetComponent<Comps.MultiplayerPlayerAnimator>().Pickup();
			}
		}
		else
		{
			Log("Client requested gear we have not data for, so gear most likely is missing. Gear hash " + Hash);
			ServerSend.GEARNOTEXIST(sendTo, _msg: true);
		}
	}

	public static void AddDroppedGear(int GearID, int Hash, string DataProxy, string Scene, DataStr.ExtraDataForDroppedGear extra)
	{
		DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = new DataStr.SlicedJsonDroppedGear();
		slicedJsonDroppedGear.m_GearName = extra.m_GearName;
		slicedJsonDroppedGear.m_Json = DataProxy;
		slicedJsonDroppedGear.m_Extra = extra;
		if (IsLocksmithItem(slicedJsonDroppedGear.m_GearName))
		{
			if (extra.m_Variant == 4)
			{
				MPSaveManager.AddBlank(Hash, slicedJsonDroppedGear.m_GearName, Scene, slicedJsonDroppedGear.m_Extra.m_Dropper);
			}
		}
		else if (extra.m_Variant == 5)
		{
			MPSaveManager.AddBlank(Hash, slicedJsonDroppedGear.m_GearName, Scene, slicedJsonDroppedGear.m_Extra.m_Dropper);
		}
		MPSaveManager.AddGearData(Scene, Hash, slicedJsonDroppedGear);
	}

	public static void AddSlicedJsonDataForDrop(DataStr.SlicedJsonData jData, int ClientID)
	{
		if (MyMod.SlicedJsonDataBuffer.ContainsKey(jData.m_Hash))
		{
			string value = "";
			if (MyMod.SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out value))
			{
				string value2 = value + jData.m_Str;
				MyMod.SlicedJsonDataBuffer.Remove(jData.m_Hash);
				MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, value2);
			}
			else
			{
				MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
			}
		}
		else
		{
			MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
		}
		if (jData.m_Last)
		{
			string value3 = "";
			if (MyMod.SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out value3))
			{
				MyMod.SlicedJsonDataBuffer.Remove(jData.m_Hash);
				AddDroppedGear(jData.m_SendTo, jData.m_Hash, value3, jData.m_GearName, jData.m_Extra);
			}
		}
		ServerSend.READYSENDNEXTSLICEGEAR(ClientID, _msg: true);
	}

	public static void AddLootedContainer(DataStr.ContainerOpenSync box, bool needSync, int Looter = 0, int State = 0)
	{
		if (MyMod.iAmHost)
		{
			MPSaveManager.AddLootedContainer(box, State, Looter);
		}
		if (needSync)
		{
			if (MyMod.iAmHost)
			{
				if (Looter == 0)
				{
					ServerSend.LOOTEDCONTAINER(0, box, State, toEveryOne: true);
				}
				else
				{
					ServerSend.LOOTEDCONTAINER(Looter, box, State, toEveryOne: false);
				}
			}
			else if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(155))
				{
					packet.Write(box.m_Guid);
					packet.Write(MyMod.level_guid);
					packet.Write(State);
					MyMod.SendUDPData(packet);
				}
			}
		}
		else if (box.m_LevelGUID == MyMod.level_guid)
		{
			MyMod.RemoveLootFromContainer(box.m_Guid, State);
		}
	}

	public static void AddLoadingClient(int clientID)
	{
		if (MyMod.playersData[clientID] != null)
		{
			MyMod.playersData[clientID].m_IsLoading = true;
			Log("Client " + clientID + " loading scene...");
		}
	}

	public static void RemoveLoadingClient(int clientID)
	{
		MyMod.playersData[clientID].m_IsLoading = false;
		Log("Client " + clientID + " finished loading scene");
	}

	public static bool ClientIsLoading(int clientID)
	{
		if (MyMod.playersData[clientID] != null && MyMod.playersData[clientID].m_IsLoading)
		{
			return true;
		}
		return false;
	}

	public static void SendChatCommandToHost(string Command)
	{
		if (MyMod.iAmHost)
		{
			ChatCommand(Command);
			return;
		}
		using Packet packet = new Packet(174);
		packet.Write(Command);
		MyMod.SendUDPData(packet);
	}

	public static bool ClientOnlyChatCommand(string Command)
	{
		Command = Command.ToLower();
		switch (Command)
		{
		case "!debug":
			if (Supporters.MyID == "76561198152259224" || Supporters.MyID == "76561198867520214")
			{
				if (!MyMod.DebugGUI)
				{
					MyMod.DebugGUI = true;
					MyMod.DebugBind = true;
				}
				else
				{
					MyMod.DebugGUI = false;
					MyMod.DebugBind = false;
				}
			}
			return true;
		case "!expedition":
			if (MyMod.ExpeditionEditorUI != null)
			{
				MyMod.ExpeditionEditorSelectUI.SetActive(!MyMod.ExpeditionEditorSelectUI.activeSelf);
				MyMod.ExpeditionEditorUI.SetActive(value: false);
				ExpeditionEditor.DisableRadiousSpheres();
				if (MyMod.ExpeditionEditorSelectUI.activeSelf)
				{
					ExpeditionEditor.RefreshExpeditionsList();
				}
			}
			return true;
		case "!cfg":
			MyMod.ShowCFGData();
			SendFeedBackMessage("Server configuration printer to your log.");
			return true;
		case "!stats":
		case "!today":
		case "!progress":
			SendChatCommandToHost(Command);
			return true;
		case "!refman":
			SanityManager.SpawnRefMan();
			return true;
		default:
			if (Command.Contains("refman") || Command.Contains("ref man") || Command.Contains("oscar") || Command.Contains("vendigo") || (Command.Contains("white") && Command.Contains("man")) || (Command.Contains("белый") && Command.Contains("мужик")) || Command.Contains("вендиго") || Command.Contains("вендизель") || (Command.Contains("сервак") && Command.Contains("говно")))
			{
				SanityManager.MaySpawnRefMan(0.35f);
				return false;
			}
			if (Command.StartsWith("!spawn "))
			{
				if (Supporters.MyID == "76561198152259224" || Supporters.MyID == "76561198867520214")
				{
					string text = Command.Replace("!spawn ", "");
					GameObject gameObject = MyMod.BundleAsset<GameObject>(text);
					if (gameObject == null)
					{
						gameObject = Resources.Load<GameObject>(text);
					}
					if (gameObject == null)
					{
						SendFeedBackMessage("Can't find, trying other way..");
						foreach (UnityEngine.Object item in Resources.LoadAll("", Il2CppType.Of<GameObject>()))
						{
							if (item.name.ToLower() == text)
							{
								SendFeedBackMessage("Found it!");
								gameObject = item.Cast<GameObject>();
								break;
							}
						}
					}
					if (gameObject == null)
					{
						SendFeedBackMessage("Prefab " + text + " not exist!");
					}
					else
					{
						GameObject objectToPlace = UnityEngine.Object.Instantiate(gameObject, GameManager.GetPlayerTransform().transform.position, GameManager.GetPlayerTransform().transform.rotation);
						GameManager.GetPlayerManagerComponent().StartPlaceMesh(objectToPlace, PlaceMeshFlags.None);
						SendFeedBackMessage("Created " + text);
					}
				}
				return true;
			}
			if (Command.StartsWith("!test"))
			{
				if (Supporters.MyID == "76561198152259224" || Supporters.MyID == "76561198867520214")
				{
					SendFeedBackMessage("Searcing OutdoorSceneRoot...");
					foreach (LoadScene item2 in UnityEngine.Object.FindObjectsOfType<LoadScene>())
					{
						if (item2.transform.root.GetComponent<OutdoorSceneRoot>() != null)
						{
							SendFeedBackMessage("Found instance");
							OutdoorSceneRoot component = item2.transform.root.GetComponent<OutdoorSceneRoot>();
							SendFeedBackMessage("Inst contains " + component.m_CachedPrefabs.Count + " cached prefabs");
							for (int i = 0; i < component.m_CachedPrefabs.Count; i++)
							{
								MelonLogger.Msg("[OutdoorSceneRoot] " + component.m_CachedPrefabs[i].m_Prefab.name);
							}
							SendFeedBackMessage("Inst master object " + (object)component.m_MasterObject);
						}
					}
					SendFeedBackMessage("Contains " + OutdoorSceneRoot.m_MasterObjectDict.Count);
					Il2CppSystem.Collections.Generic.Dictionary<string, GameObject>.Enumerator enumerator3 = OutdoorSceneRoot.m_MasterObjectDict.GetEnumerator();
					while (enumerator3.MoveNext())
					{
						Il2CppSystem.Collections.Generic.KeyValuePair<string, GameObject> current3 = enumerator3.Current;
						MelonLogger.Msg("[OutdoorSceneRoot] " + current3.value.name);
					}
				}
				return true;
			}
			return false;
		}
	}

	public static void ChatCommand(string Command, int From = 0)
	{
		Command = Command.ToLower();
		string text = "";
		switch (Command)
		{
		case "!stats":
			text = MPStats.GetPlayerGlobalStats(Server.GetMACByID(From)).GetString(HideMAC: true, HideRegionPlayTime: false);
			break;
		case "!today":
			text = MPStats.TodayStats.GetString();
			break;
		case "!progress":
		{
			if (MyMod.iAmHost && From != 0)
			{
				ServerHandle.REQUESTEXPEDITIONSPROGRESS(From);
				break;
			}
			MPStats.ExpeditionsProgressData expeditionsProgress = MPStats.GetExpeditionsProgress(Server.GetMACByID(From));
			MelonLogger.Msg(ConsoleColor.Green, "Total Progress: " + expeditionsProgress.TotalProgress);
			foreach (System.Collections.Generic.KeyValuePair<int, int> item in expeditionsProgress.ExpeditionsProgress)
			{
				MelonLogger.Msg(ConsoleColor.Green, ExpeditionBuilder.GetRegionString(item.Key) + ": " + item.Value);
			}
			break;
		}
		}
		if (!string.IsNullOrEmpty(text))
		{
			SendFeedBackMessage(text, From);
		}
	}

	public static void SendFeedBackMessage(string Message, int For = 0)
	{
		DataStr.MultiplayerChatMessage multiplayerChatMessage = new DataStr.MultiplayerChatMessage();
		multiplayerChatMessage.m_Message = Message;
		multiplayerChatMessage.m_Type = 0;
		if (MyMod.iAmHost && For != 0)
		{
			ServerSend.CHATPM(For, multiplayerChatMessage);
		}
		else
		{
			SendMessageToChat(multiplayerChatMessage, needSync: false);
		}
	}

	public static void SendMessageToChat(DataStr.MultiplayerChatMessage message, bool needSync = true)
	{
		if (ClientOnlyChatCommand(message.m_Message))
		{
			return;
		}
		if (!MyMod.DedicatedServerAppMode)
		{
			if (MyMod.ChatMessages.Count > MyMod.MaxChatMessages)
			{
				UnityEngine.Object.Destroy(MyMod.ChatMessages[0].m_TextObj.gameObject);
				MyMod.ChatMessages.RemoveAt(0);
			}
			Text text = (message.m_TextObj = MyMod.SpawnModAsset(MyMod.BundleAsset<GameObject>("MP_ChatText"), MyMod.chatPanel.transform).GetComponent<Text>());
			if (message.m_Type == 1)
			{
				string text2 = "[Global] ";
				if (!message.m_Global)
				{
					text2 = "[Area] ";
				}
				if (message.m_Private)
				{
					text2 = "[Private] ";
				}
				text.text = text2 + message.m_By + ": " + message.m_Message;
			}
			else
			{
				text.text = message.m_Message;
			}
			if (message.m_Type == 0)
			{
				text.color = Color.green;
			}
			MyMod.ChatMessages.Add(message);
			MyMod.HideChatTimer = 5;
			if (MyMod.ChatObject != null)
			{
				MyMod.ChatObject.SetActive(value: true);
			}
		}
		if (!needSync)
		{
			return;
		}
		if (MyMod.sendMyPosition)
		{
			using Packet packet = new Packet(42);
			packet.Write(message);
			MyMod.SendUDPData(packet);
		}
		if (MyMod.iAmHost)
		{
			ServerSend.CHAT(0, message, GameManager.GetPlayerTransform().position, MyMod.level_guid);
		}
	}

	public static void AddDeployedRopes(Vector3 position, bool deployed, bool snapped, int lvl, string lvlguid, bool needSync)
	{
		DataStr.ClimbingRopeSync climbingRopeSync = new DataStr.ClimbingRopeSync();
		climbingRopeSync.m_Position = position;
		climbingRopeSync.m_Deployed = deployed;
		climbingRopeSync.m_Snapped = snapped;
		climbingRopeSync.m_LevelID = lvl;
		climbingRopeSync.m_LevelGUID = lvlguid;
		if (!MyMod.DeployedRopes.Contains(climbingRopeSync))
		{
			MyMod.DeployedRopes.Add(climbingRopeSync);
		}
		else
		{
			for (int i = 0; i < MyMod.DeployedRopes.Count; i++)
			{
				DataStr.ClimbingRopeSync climbingRopeSync2 = MyMod.DeployedRopes[i];
				if (climbingRopeSync2.m_Position == position && climbingRopeSync2.m_LevelID == lvl && climbingRopeSync2.m_LevelGUID == lvlguid)
				{
					climbingRopeSync2.m_Deployed = deployed;
					climbingRopeSync2.m_Snapped = snapped;
					break;
				}
			}
		}
		if (needSync)
		{
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(55);
				packet.Write(climbingRopeSync);
				MyMod.SendUDPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.ROPE(0, climbingRopeSync, toEveryOne: true);
			}
		}
		else if (lvl == MyMod.levelid && lvlguid == MyMod.level_guid)
		{
			MyMod.UpdateDeployedRopes();
		}
	}

	public static void AddRecentlyPickedGear(DataStr.PickedGearSync data)
	{
		data.m_Recently = 3;
		MyMod.RecentlyPickedGears.Add(data);
	}

	public static void CanclePickOfOtherClient(DataStr.PickedGearSync data)
	{
		if (data.m_PickerID == 0)
		{
			Log("Other shokal has pickup item before me! I need to delete my picked gear with IID " + data.m_MyInstanceID);
			int myInstanceID = data.m_MyInstanceID;
			Il2CppSystem.Collections.Generic.List<GearItemObject> items = GameManager.GetInventoryComponent().m_Items;
			for (int i = 0; i < items.Count; i++)
			{
				GearItemObject gearItemObject = items[i];
				if (gearItemObject != null && gearItemObject.m_GearItem.m_InstanceID == myInstanceID)
				{
					HUDMessage.AddMessage("THIS ALREADY PICKED!");
					GameAudioManager.PlayGUIError();
					GameManager.GetInventoryComponent().DestroyGear(gearItemObject.m_GearItem.gameObject);
					break;
				}
			}
		}
		else
		{
			ServerSend.CANCLEPICKUP(0, data, toEveryOne: true);
		}
	}

	public static void AddPickedGear(Vector3 spawn, int lvl, string lvlguid, int pickerId, int isntID, string gearName, bool needSync)
	{
		if (needSync && PickupSync.TryGetWorldPosition(isntID, out var worldPosition))
		{
			spawn = worldPosition;
		}
		if (lvlguid == MyMod.level_guid)
		{
			RadialSpawnSync.RememberPickup(gearName, spawn);
		}
		if (needSync)
		{
			PickupSync.MarkSent(isntID);
		}
		DataStr.PickedGearSync pickedGearSync = new DataStr.PickedGearSync();
		pickedGearSync.m_Spawn = spawn;
		pickedGearSync.m_LevelID = lvl;
		pickedGearSync.m_LevelGUID = lvlguid;
		pickedGearSync.m_PickerID = pickerId;
		pickedGearSync.m_MyInstanceID = isntID;
		pickedGearSync.m_GearName = gearName;
		long pickedGearKey = MPSaveManager.GetPickedGearKey(pickedGearSync);
		if (MyMod.iAmHost)
		{
			MPSaveManager.AddPickedGear(pickedGearSync, pickerId);
			if (!MyMod.RecentlyPickedGears.Contains(pickedGearSync))
			{
				AddRecentlyPickedGear(pickedGearSync);
			}
			else
			{
				Log("Other shokal has pickup item later! Picker ID " + pickedGearSync.m_PickerID + " Should delete gear with IID " + pickedGearSync.m_MyInstanceID);
				CanclePickOfOtherClient(pickedGearSync);
			}
		}
		if (needSync)
		{
			if (MyMod.sendMyPosition)
			{
				Log("Sent pickup " + pickedGearKey);
				using Packet packet = new Packet(53);
				packet.Write(pickedGearSync);
				MyMod.SendUDPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.GEARPICKUP(0, pickedGearSync, toEveryOne: true);
			}
		}
		else if (lvlguid == MyMod.level_guid)
		{
			MyMod.RemovePickedGear(pickedGearKey, spawn, gearName);
		}
	}

	public static void ShelterCreated(Vector3 spawn, Quaternion rot, int lvl, string lvlguid, bool needSync)
	{
		DataStr.ShowShelterByOther showShelterByOther = new DataStr.ShowShelterByOther();
		showShelterByOther.m_Position = spawn;
		showShelterByOther.m_Rotation = rot;
		showShelterByOther.m_LevelID = lvl;
		showShelterByOther.m_LevelGUID = lvlguid;
		if (!MyMod.ShowSheltersBuilded.Contains(showShelterByOther))
		{
			MyMod.ShowSheltersBuilded.Add(showShelterByOther);
		}
		if (needSync)
		{
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(73);
				packet.Write(showShelterByOther);
				MyMod.SendUDPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.ADDSHELTER(0, showShelterByOther, toEveryOne: true);
			}
		}
		else if (lvl == MyMod.levelid && lvlguid == MyMod.level_guid)
		{
			MyMod.SpawnSnowShelterByOther(showShelterByOther);
		}
	}

	public static void ShelterRemoved(Vector3 spawn, int lvl, string lvlguid, bool needSync)
	{
		DataStr.ShowShelterByOther showShelterByOther = new DataStr.ShowShelterByOther();
		showShelterByOther.m_Position = spawn;
		showShelterByOther.m_LevelID = lvl;
		showShelterByOther.m_LevelGUID = lvlguid;
		if (MyMod.ShowSheltersBuilded.Contains(showShelterByOther))
		{
			MyMod.ShowSheltersBuilded.Remove(showShelterByOther);
		}
		if (!needSync)
		{
			return;
		}
		if (MyMod.sendMyPosition)
		{
			using Packet packet = new Packet(74);
			packet.Write(showShelterByOther);
			MyMod.SendUDPData(packet);
		}
		if (MyMod.iAmHost)
		{
			ServerSend.REMOVESHELTER(0, showShelterByOther, toEveryOne: true);
		}
	}

	public static float NextFloat(float min, float max)
	{
		return (float)(new System.Random().NextDouble() * (double)(max - min) + (double)min);
	}

	public static float NextFloat(System.Random random, float min, float max)
	{
		return (float)(random.NextDouble() * (double)(max - min) + (double)min);
	}

	public static void OnAnimalCorpseChanged(string GUID, float MeatTaken, int GutsTaken, int HideTaken, string Feeding = "")
	{
		if (!AnimalsKilled.TryGetValue(GUID, out var value))
		{
			return;
		}
		value.m_Meat -= MeatTaken;
		value.m_Guts -= GutsTaken;
		value.m_Hide -= HideTaken;
		if (!string.IsNullOrEmpty(Feeding) && value.m_Meat <= 0f)
		{
			value.m_Meat = 0f;
			value.m_Guts = 0;
			value.m_Hide = 0;
			ServerSend.ANIMALSTOPSFEEDING(Feeding);
			Log("Animal " + Feeding + " finished eating");
			GameObject gameObject = ObjectGuidManager.Lookup(Feeding);
			if (gameObject == null)
			{
				BaseAi component = gameObject.GetComponent<BaseAi>();
				if ((bool)component)
				{
					component.m_TargetBodyHarvest = null;
				}
			}
		}
		AnimalsKilled.Remove(GUID);
		AnimalsKilled.Add(GUID, value);
		MPSaveManager.AnimalsKilledChanged = true;
	}

	public static void OnAnimalQuarted(string GUID)
	{
		if (AnimalsKilled.TryGetValue(GUID, out var _))
		{
			AnimalsKilled.Remove(GUID);
			MPSaveManager.AnimalsKilledChanged = true;
		}
	}

	public static int PickUpRabbit(string GUID)
	{
		MPSaveManager.AnimalsKilledChanged = true;
		if (StunnedRabbits.ContainsKey(GUID))
		{
			StunnedRabbits.Remove(GUID);
			if (AnimalsKilled.ContainsKey(GUID))
			{
				AnimalsKilled.Remove(GUID);
			}
			MyMod.DeleteAnimal(GUID);
			ServerSend.ANIMALDELETE(0, GUID);
			return 2;
		}
		if (AnimalsKilled.ContainsKey(GUID))
		{
			AnimalsKilled.Remove(GUID);
			MyMod.DeleteAnimal(GUID);
			ServerSend.ANIMALDELETE(0, GUID);
			return 1;
		}
		return 0;
	}

	public static DataStr.BodyHarvestUnits GetBodyHarvestUnits(string name)
	{
		DataStr.BodyHarvestUnits bodyHarvestUnits = new DataStr.BodyHarvestUnits();
		switch (name)
		{
		case "WILDLIFE_Wolf":
			bodyHarvestUnits.m_Meat = NextFloat(3f, 6f);
			bodyHarvestUnits.m_Guts = 2;
			break;
		case "WILDLIFE_Wolf_grey":
			bodyHarvestUnits.m_Meat = NextFloat(4f, 7f);
			bodyHarvestUnits.m_Guts = 2;
			break;
		case "WILDLIFE_Bear":
			bodyHarvestUnits.m_Meat = NextFloat(25f, 40f);
			bodyHarvestUnits.m_Guts = 10;
			break;
		case "WILDLIFE_Stag":
			bodyHarvestUnits.m_Meat = NextFloat(8f, 10f);
			bodyHarvestUnits.m_Guts = 2;
			break;
		case "WILDLIFE_Rabbit":
			bodyHarvestUnits.m_Meat = NextFloat(0.75f, 1.5f);
			bodyHarvestUnits.m_Guts = 1;
			break;
		case "WILDLIFE_Moose":
			bodyHarvestUnits.m_Meat = NextFloat(30f, 45f);
			bodyHarvestUnits.m_Guts = 12;
			break;
		case "WILDLIFE_Cougar":
			bodyHarvestUnits.m_Meat = NextFloat(8f, 14f);
			bodyHarvestUnits.m_Guts = 3;
			break;
		case "WILDLIFE_Ptarmigan":
			bodyHarvestUnits.m_Meat = NextFloat(0.4f, 0.8f);
			bodyHarvestUnits.m_Guts = 0;
			break;
		}
		return bodyHarvestUnits;
	}

	public static void BanSpawnRegion(string GUID, int BanDuration = 1440)
	{
		int value = MyMod.MinutesFromStartServer + BanDuration;
		if (!MyMod.BannedSpawnRegions.ContainsKey(GUID))
		{
			MyMod.BannedSpawnRegions.Add(GUID, value);
			ServerSend.SPAWNREGIONBANCHECK(GUID);
		}
	}

	public static bool CheckSpawnRegionBanned(string GUID)
	{
		if (MyMod.BannedSpawnRegions.TryGetValue(GUID, out var value))
		{
			if (MyMod.MinutesFromStartServer > value)
			{
				MyMod.BannedSpawnRegions.Remove(GUID);
				return false;
			}
			if (new System.Random(default(Guid).GetHashCode()).Next(0, 100) <= 23)
			{
				BanSpawnRegion(GUID);
				return true;
			}
			return false;
		}
		return false;
	}

	public static void ReviveRabbit(string GUID)
	{
		MPSaveManager.AnimalsKilledChanged = true;
		ServerSend.ANIMALDELETE(0, GUID);
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		DataStr.AnimalKilled value2;
		if (MyMod.AnimalsController)
		{
			DataStr.AnimalKilled value;
			if ((bool)gameObject)
			{
				Vector3 position = gameObject.transform.position;
				UnityEngine.Object.Destroy(gameObject);
				AnimalsKilled.Remove(GUID);
				MyMod.OnRabbitRevived(position);
			}
			else if (AnimalsKilled.TryGetValue(GUID, out value))
			{
				Vector3 position = value.m_Position;
				string levelGUID = value.m_LevelGUID;
				AnimalsKilled.Remove(GUID);
				ServerSend.RABBITREVIVED(0, position, levelGUID);
			}
		}
		else if (AnimalsKilled.TryGetValue(GUID, out value2))
		{
			Vector3 position = value2.m_Position;
			string levelGUID = value2.m_LevelGUID;
			AnimalsKilled.Remove(GUID);
			ServerSend.RABBITREVIVED(0, position, levelGUID);
		}
	}

	public static void UpdateStunnedRabbits()
	{
		if (StunnedRabbits.Count <= 0)
		{
			return;
		}
		System.Collections.Generic.List<DataStr.ElementToModfy> list = new System.Collections.Generic.List<DataStr.ElementToModfy>();
		foreach (System.Collections.Generic.KeyValuePair<string, int> stunnedRabbit in StunnedRabbits)
		{
			DataStr.ElementToModfy elementToModfy = new DataStr.ElementToModfy();
			elementToModfy.m_GUID = stunnedRabbit.Key;
			elementToModfy.m_Time = stunnedRabbit.Value;
			list.Add(elementToModfy);
		}
		foreach (DataStr.ElementToModfy item in list)
		{
			int time = item.m_Time;
			time--;
			if (time == 0)
			{
				ReviveRabbit(item.m_GUID);
				StunnedRabbits.Remove(item.m_GUID);
			}
			else
			{
				StunnedRabbits.Remove(item.m_GUID);
				StunnedRabbits.Add(item.m_GUID, time);
			}
		}
	}

	public static void OnAnimalStunned(string GUID)
	{
		if (!StunnedRabbits.ContainsKey(GUID))
		{
			StunnedRabbits.Add(GUID, 5);
		}
	}

	public static void OnAnimalKilled(string prefab, Vector3 v3, Quaternion rot, string GUID, string LevelGUID, string RegionGUID, bool knocked = false)
	{
		if (!AnimalsKilled.ContainsKey(GUID))
		{
			DataStr.AnimalKilled animalKilled = new DataStr.AnimalKilled();
			animalKilled.m_Position = v3;
			animalKilled.m_Rotation = rot;
			animalKilled.m_PrefabName = prefab;
			animalKilled.m_GUID = GUID;
			animalKilled.m_LevelGUID = LevelGUID;
			animalKilled.m_CreatedTime = MyMod.MinutesFromStartServer;
			animalKilled.m_RegionGUID = RegionGUID;
			DataStr.BodyHarvestUnits bodyHarvestUnits = GetBodyHarvestUnits(prefab);
			animalKilled.m_Meat = bodyHarvestUnits.m_Meat;
			animalKilled.m_Guts = bodyHarvestUnits.m_Guts;
			animalKilled.m_Hide = 1;
			AnimalsKilled.Add(GUID, animalKilled);
			MPSaveManager.AnimalsKilledChanged = true;
			if (LevelGUID == MyMod.level_guid)
			{
				MyMod.ProcessAnimalCorpseSync(animalKilled);
			}
			if (knocked)
			{
				OnAnimalStunned(GUID);
			}
			else
			{
				BanSpawnRegion(RegionGUID);
			}
		}
	}

	public static void ResetDataForSlot(int _from)
	{
		if (_from != 0 && _from < MyMod.playersData.Count)
		{
			if (MyMod.playersData[_from] != null)
			{
				MyMod.playersData[_from] = new DataStr.MultiPlayerClientData();
			}
			ServerSend.EQUIPMENT(_from, new DataStr.PlayerEquipmentData(), toEveryOne: false);
			ServerSend.LIGHTSOURCE(_from, _msg: false, toEveryOne: false);
			ServerSend.LIGHTSOURCENAME(_from, "", toEveryOne: false);
			ServerSend.XYZ(_from, new Vector3(0f, 0f, 0f), toEveryOne: false);
			ServerSend.XYZW(_from, new Quaternion(0f, 0f, 0f, 0f), toEveryOne: false);
			ServerSend.ANIMSTATE(_from, "Idle", toEveryOne: false);
			ServerSend.LEVELID(_from, 0, toEveryOne: false);
		}
	}

	public static void SendSlotData(int _forClient)
	{
		Log("Sending savedata for " + _forClient);
		DataStr.SaveSlotSync saveSlotSync = new DataStr.SaveSlotSync();
		saveSlotSync.m_Episode = 0;
		saveSlotSync.m_SaveSlotType = 3;
		saveSlotSync.m_Seed = GameManager.m_SceneTransitionData.m_GameRandomSeed;
		saveSlotSync.m_ExperienceMode = (int)ExperienceModeManager.GetCurrentExperienceModeType();
		saveSlotSync.m_Location = (int)RegionCompat.GetCurrentRegion();
		saveSlotSync.m_FixedSpawnScene = MyMod.SavedSceneForSpawn;
		saveSlotSync.m_FixedSpawnPosition = MyMod.SavedPositionForSpawn;
		if (MyMod.ServerConfig.m_PlayersSpawnType == 0 && !MyMod.DedicatedServerAppMode && !string.IsNullOrEmpty(MyMod.level_name) && MyMod.level_name != "Empty" && MyMod.level_name != "Boot" && !IsMainMenuScene(MyMod.level_name) && GameManager.GetPlayerTransform() != null)
		{
			// "Same as host" used to give only the host's region, and the game then picked a random start point in it,
			// so players sometimes started far apart. A new character now starts next to the host.
			saveSlotSync.m_FixedSpawnScene = MyMod.level_name;
			saveSlotSync.m_FixedSpawnPosition = GameManager.GetPlayerTransform().position;
			Log("[Spawn] New guest character will start next to the host in " + MyMod.level_name);
		}
		if (ExperienceModeManager.GetCurrentExperienceModeType() == ExperienceModeType.Custom)
		{
			saveSlotSync.m_CustomExperienceStr = GameManager.GetExperienceModeManagerComponent().GetCurrentCustomModeString();
		}
		else
		{
			saveSlotSync.m_CustomExperienceStr = "";
		}
		ServerSend.SAVEDATA(_forClient, saveSlotSync);
	}

	public static void ClientTryingLockDoor(string DoorKey, string KeySeed, string Scene, int Client)
	{
		string text = DoorKey.Split('_')[1];
		switch (MPSaveManager.AddLockedDoor(Scene, DoorKey, KeySeed))
		{
		case MPSaveManager.UseKeyStatus.Done:
			ServerSend.ADDDOORLOCK(-1, text, Scene);
			if (!MyMod.DedicatedServerAppMode && Scene == MyMod.level_guid)
			{
				MyMod.AddLocksToDoorsByGUID(text);
			}
			ServerSend.DOORLOCKEDMSG(Client, "You locked this building!");
			break;
		case MPSaveManager.UseKeyStatus.KeyUsed:
			ServerSend.DOORLOCKEDMSG(Client, "This key already used for other door!");
			break;
		case MPSaveManager.UseKeyStatus.DoorAlreadyLocked:
			ServerSend.DOORLOCKEDMSG(Client, "This door is already locked!");
			break;
		}
	}

	public static void InitAllPlayers()
	{
		for (int i = 0; i < MyMod.MaxPlayers; i++)
		{
			if (MyMod.playersData.Count < MyMod.MaxPlayers)
			{
				MyMod.playersData.Add(null);
			}
			if (MyMod.playersData[i] == null)
			{
				MyMod.playersData[i] = new DataStr.MultiPlayerClientData();
			}
		}
		if (MyMod.DedicatedServerAppMode)
		{
			return;
		}
		if (MyMod.MyRadioAudio == null && MyMod.BundleAsset<GameObject>("MyRadio") != null)
		{
			MyMod.MyRadioAudio = MyMod.SpawnModAsset(MyMod.BundleAsset<GameObject>("MyRadio"));
			GameObject gameObject = MyMod.MyRadioAudio.transform.GetChild(1).gameObject;
			GameObject gameObject2 = MyMod.MyRadioAudio.transform.GetChild(2).gameObject;
			gameObject.AddComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSource = gameObject.GetComponent<AudioSource>();
			gameObject.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSourceBgNoise = gameObject2.GetComponent<AudioSource>();
			gameObject.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_ID = -1;
			gameObject.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_RadioFilter = true;
		}
		for (int j = 0; j < MyMod.MaxPlayers; j++)
		{
			if (MyMod.players.Count < MyMod.MaxPlayers)
			{
				MyMod.players.Add(null);
			}
			if (!(MyMod.players[j] == null))
			{
				continue;
			}
			GameObject gameObject3 = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
			if (!(gameObject3 == null))
			{
				GameObject gameObject4 = MyMod.SpawnModAsset(gameObject3);
				gameObject4.AddComponent<Comps.MultiplayerPlayerAnimator>().m_Animer = gameObject4.GetComponent<Animator>();
				gameObject4.AddComponent<Comps.MultiplayerPlayerClothingManager>().m_Player = gameObject4;
				Transform child = gameObject4.transform.GetChild(4);
				_ = child.GetChild(0).gameObject;
				GameObject gameObject5 = child.GetChild(1).gameObject;
				GameObject gameObject6 = child.GetChild(2).gameObject;
				GameObject gameObject7 = child.GetChild(3).gameObject;
				gameObject5.AddComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSource = gameObject5.GetComponent<AudioSource>();
				gameObject5.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_ID = j;
				gameObject6.AddComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSource = gameObject6.GetComponent<AudioSource>();
				gameObject6.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSourceBgNoise = gameObject7.GetComponent<AudioSource>();
				gameObject6.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_ID = j;
				gameObject6.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_RadioFilter = true;
				Comps.MultiplayerPlayer multiplayerPlayer = gameObject4.AddComponent<Comps.MultiplayerPlayer>();
				multiplayerPlayer.m_Player = gameObject4;
				multiplayerPlayer.m_ID = j;
				MyMod.players[j] = gameObject4;
				if (MyMod.playersData.Count > 0 && MyMod.playersData[j] != null)
				{
					gameObject4.transform.position = MyMod.playersData[j].m_Position;
					gameObject4.transform.rotation = MyMod.playersData[j].m_Rotation;
				}
				MyMod.ApplyDamageZones(gameObject4, multiplayerPlayer);
				gameObject4.SetActive(value: false);
				multiplayerPlayer.m_TorchIgniter = gameObject4.transform.GetChild(3).GetChild(8).GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.GetChild(1)
					.GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.GetChild(8)
					.GetChild(1)
					.gameObject;
				Supporters.ApplyFlairsForModel(j, MyMod.playersData[j].m_SupporterBenefits.m_Flairs);
			}
		}
		if (MyMod.MyPlayerDoll == null && MyMod.BundleAsset<GameObject>("multiplayerPlayer") != null)
		{
			GameObject gameObject8 = MyMod.SpawnModAsset(MyMod.BundleAsset<GameObject>("multiplayerPlayer"));
			gameObject8.name = "MyPlayerDoll";
			MyMod.MyPlayerDoll = gameObject8;
			MyMod.MyPlayerDoll.SetActive(value: false);
			Comps.MultiplayerPlayerAnimator multiplayerPlayerAnimator = gameObject8.AddComponent<Comps.MultiplayerPlayerAnimator>();
			multiplayerPlayerAnimator.m_MyDoll = true;
			multiplayerPlayerAnimator.m_Animer = gameObject8.GetComponent<Animator>();
			MyMod.DollCameraDummy = gameObject8.transform.GetChild(3).GetChild(8).GetChild(0)
				.GetChild(0)
				.GetChild(0)
				.GetChild(2)
				.GetChild(0)
				.GetChild(0)
				.gameObject;
			Supporters.ApplyFlairsForModel(gameObject8, Supporters.ConfiguratedBenefits.m_Flairs, "I am");
		}
	}

	public static void HostAServer(int port = 26950)
	{
		if (!MyMod.iAmHost)
		{
			MyMod.isRuning = true;
			MPSaveManager.LoadNonUnloadables();
			Server.Start(MyMod.MaxPlayers, port);
			MyMod.nextActionTime = Time.time;
			MyMod.nextActionTimeAniamls = Time.time;
			MyMod.iAmHost = true;
			MyMod.AnimalsController = true;
			MyMod.WildlifeAuthorityScene = "";
			InitAllPlayers();
			Log("Server has been runned with InGame time: " + GameManager.GetTimeOfDayComponent().GetHour() + ":" + GameManager.GetTimeOfDayComponent().GetMinutes() + " seed " + GameManager.m_SceneTransitionData.m_GameRandomSeed);
			MyMod.OverridedHourse = GameManager.GetTimeOfDayComponent().GetHour();
			MyMod.OverridedMinutes = GameManager.GetTimeOfDayComponent().GetMinutes();
			MyMod.OveridedTime = MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes;
			MyMod.NeedSyncTime = true;
			MyMod.DisableOriginalAnimalSpawns(host: true);
			MyMod.SetFixedSpawn();
			MyMod.KillConsole();
		}
	}

	public static DataStr.DedicatedServerData LoadDedicatedServerConfig()
	{
		string path = "Mods\\server.json";
		if (File.Exists(path))
		{
			Log("Reading server.json...", LoggerColor.Blue);
			DataStr.DedicatedServerData dedicatedServerData = JSON.Load(File.ReadAllText(path)).Make<DataStr.DedicatedServerData>();
			Log("Server settings: ", LoggerColor.Blue);
			Log("SaveSlot: " + dedicatedServerData.SaveSlot, LoggerColor.Blue);
			Log("ItemDupes: " + dedicatedServerData.ItemDupes, LoggerColor.Blue);
			Log("ContainersDupes: " + dedicatedServerData.ContainersDupes, LoggerColor.Blue);
			Log("SpawnStyle: " + dedicatedServerData.SpawnStyle, LoggerColor.Blue);
			Log("MaxPlayers: " + dedicatedServerData.MaxPlayers, LoggerColor.Blue);
			Log("UsingSteam: " + dedicatedServerData.UsingSteam, LoggerColor.Blue);
			Log("Ports: " + dedicatedServerData.Ports, LoggerColor.Blue);
			Log("Cheats: " + dedicatedServerData.Cheats, LoggerColor.Blue);
			Log("SteamServerAccessibility: " + dedicatedServerData.SteamServerAccessibility, LoggerColor.Blue);
			Log("RCON: (SECURED)", LoggerColor.Blue);
			Log("DropUnloadPeriod: " + dedicatedServerData.DropUnloadPeriod, LoggerColor.Blue);
			Log("SaveScamProtection: " + dedicatedServerData.SaveScamProtection, LoggerColor.Blue);
			Log("ModValidationCheck: " + dedicatedServerData.ModValidationCheck, LoggerColor.Blue);
			Log("ExperienceMode: " + dedicatedServerData.ExperienceMode, LoggerColor.Blue);
			Log("StartRegion: " + dedicatedServerData.StartRegion, LoggerColor.Blue);
			ExperienceForDS = dedicatedServerData.ExperienceMode;
			StartingRegionDS = dedicatedServerData.StartRegion;
			MyMod.CustomServerName = dedicatedServerData.ServerName;
			if (dedicatedServerData.Seed == 0)
			{
				Log("Seed: (Random)", LoggerColor.Blue);
				dedicatedServerData.Seed = Guid.NewGuid().GetHashCode();
			}
			else
			{
				Log("Seed: " + dedicatedServerData.Seed, LoggerColor.Blue);
			}
			Log("PVP: " + dedicatedServerData.PVP, LoggerColor.Blue);
			Log("SavingPeriod: " + dedicatedServerData.SavingPeriod, LoggerColor.Blue);
			Log("RestartPerioud: " + dedicatedServerData.RestartPerioud, LoggerColor.Blue);
			Log("No problems with server.json found!", LoggerColor.Green);
			if (dedicatedServerData.RCON != null)
			{
				MyMod.RCON = dedicatedServerData.RCON;
			}
			if (dedicatedServerData.DropUnloadPeriod > 5)
			{
				MPSaveManager.SaveRecentTimer = dedicatedServerData.DropUnloadPeriod;
			}
			MyMod.ServerConfig.m_CheckModsValidation = dedicatedServerData.ModValidationCheck;
			MyMod.ServerConfig.m_SaveScamProtection = dedicatedServerData.SaveScamProtection;
			MyMod.ServerConfig.m_DuppedContainers = dedicatedServerData.ContainersDupes;
			MyMod.ServerConfig.m_DuppedSpawns = dedicatedServerData.ItemDupes;
			MyMod.ServerConfig.m_PlayersSpawnType = dedicatedServerData.SpawnStyle;
			MyMod.ServerConfig.m_CheatsMode = dedicatedServerData.Cheats;
			MyMod.ServerConfig.m_PVP = dedicatedServerData.PVP;
			MyMod.DsSavePerioud = dedicatedServerData.SavingPeriod;
			MyMod.MaxPlayers = dedicatedServerData.MaxPlayers;
			return dedicatedServerData;
		}
		return new DataStr.DedicatedServerData
		{
			Seed = Guid.NewGuid().GetHashCode()
		};
	}

	public static void FakeDropItem(DataStr.DroppedGearItemDataPacket GearData, bool JustLoad = false)
	{
		DroppedGearDiagnostics.ObserveVisual(GearData, JustLoad);
		if (MyMod.iAmHost && !JustLoad)
		{
			MPSaveManager.AddGearVisual(GearData.m_LevelGUID, GearData);
		}
		if (GearData.m_LevelGUID == MyMod.level_guid)
		{
			try
			{
				MyMod.FakeDropItem(GearData.m_GearID, GearData.m_Position, GearData.m_Rotation, GearData.m_Hash, GearData.m_Extra);
			}
			catch (Exception exception)
			{
				DroppedGearDiagnostics.CreationFailed(GearData, exception);
				throw;
			}
			DroppedGearDiagnostics.ObserveResult(GearData);
		}
	}

	public static string ExecuteCommand(string CMD, int _fromClient = -1)
	{
		string text = CMD.ToLower();
		if (_fromClient != -1)
		{
			Log("[RCON] Operator Execute: " + CMD, LoggerColor.Magenta);
		}
		switch (text)
		{
		case "trafficdebug":
		case "traffic":
		case "trafictrace":
		case "trafficcheck":
			MyMod.DebugTrafficCheck = !MyMod.DebugTrafficCheck;
			return "DebugTrafficCheck = " + MyMod.DebugTrafficCheck;
		case "savediag":
		case "unloaddiag":
			MPSaveManager.Diagnostic = !MPSaveManager.Diagnostic;
			return "MPSaveManager.Diagnostic = " + MPSaveManager.Diagnostic;
		case "reset":
		case "restart":
			MyMod.RestartPerioud = 0;
			MyMod.SecondsWithoutSaving = MyMod.DsSavePerioud;
			return "Server going to restart in 30 seconds!";
		case "shutdown":
			Application.Quit();
			return "Server will shutdown";
		case "players":
		case "playerslist":
		case "clients":
		{
			string text16 = "Players:";
			{
				foreach (System.Collections.Generic.KeyValuePair<int, Client> client in Server.clients)
				{
					if (client.Value.IsBusy())
					{
						text16 = (client.Value.RCON ? (text16 + " " + client.Key + ". RCON") : (text16 + " " + client.Key + ". " + MyMod.playersData[client.Key].m_Name));
					}
				}
				return text16;
			}
		}
		default:
			if (text.StartsWith("say "))
			{
				string text2 = CMD.Remove(0, 4);
				SendMessageToChat(new DataStr.MultiplayerChatMessage
				{
					m_Type = 0,
					m_By = "",
					m_Message = "[SERVER] " + text2
				});
				return "Message sent to chat";
			}
			if (text.StartsWith("skip "))
			{
				int h = int.Parse(CMD.Split(' ')[1]);
				SkipRTTime(h);
				return "Skipped " + h + " hour(s)";
			}
			if (text == "skip")
			{
				SkipRTTime(1);
				return "Skipped 1 hour";
			}
			if (text.StartsWith("rpc "))
			{
				string[] array = CMD.Split(' ');
				if (array.Length < 2)
				{
					return "Invalid syntax!";
				}
				int num = int.Parse(array[1]);
				if (num < 0)
				{
					return "Client ID can't be negative";
				}
				string text3 = "";
				for (int i = 2; i < array.Length; i++)
				{
					text3 = ((!(text3 == "")) ? (text3 + " " + array[i]) : array[i]);
				}
				if (num < MyMod.playersData.Count && MyMod.playersData[num] != null && Server.clients[num].IsBusy())
				{
					ServerSend.RPC(num, text3);
					return "Sent " + text3 + " to client " + num;
				}
				return "There no Client " + num;
			}
			switch (text)
			{
			case "save":
				MPSaveManager.SaveGlobalData();
				MPSaveManager.SaveRecentStuff();
				MPStats.SaveRecentStuff();
				return "Manual saving done!";
			case "today":
			case "showstats":
			case "stats":
			case "statistics":
			case "statistic":
				return "\n" + MPStats.TodayStats.GetString(IncludePlayersStats: true, HideMAC: false, HideRegionPlayTime: false);
			case "globalstats":
			case "global stats":
			case "stats global":
			case "statsglobal":
			case "alltime":
				return "\n" + MPStats.AllTimeStats.GetString(IncludePlayersStats: true, HideMAC: false, HideRegionPlayTime: false);
			default:
				if (text.StartsWith("kick ") || text.StartsWith("ban "))
				{
					bool flag = text.StartsWith("ban ");
					string[] array2 = CMD.Split(' ');
					int num2 = int.Parse(array2[1]);
					string text4 = "";
					if (array2.Length > 2)
					{
						for (int j = 2; j < array2.Length; j++)
						{
							text4 = text4 + array2[j] + " ";
						}
					}
					if (num2 > 0 && Server.clients.ContainsKey(num2))
					{
						string text5 = "You has been kicked from the server.";
						string text6 = "";
						if (flag)
						{
							text6 = Server.clients[num2].SubNetworkGUID;
							text5 = "You has been banned from the server.";
						}
						if (string.IsNullOrEmpty(text4))
						{
							ServerSend.KICKMESSAGE(num2, text5);
						}
						else
						{
							ServerSend.KICKMESSAGE(num2, text5 + "\nReason: " + text4);
						}
						if (!string.IsNullOrEmpty(text6))
						{
							if (!MPSaveManager.BannedUsers.ContainsKey(text6))
							{
								MPSaveManager.BannedUsers.Add(text6, text4);
								MPSaveManager.SaveBanned();
							}
							Server.clients[num2].udp.Disconnect();
							return "User " + text6 + " banned. Reason " + text4;
						}
						Server.clients[num2].udp.Disconnect();
						return "User with ID " + num2 + " kicked. Reason " + text4;
					}
					return "Incorrect syntax";
				}
				if (text.StartsWith("banmac "))
				{
					string[] array3 = CMD.Split(' ');
					string text7 = array3[1];
					string text8 = "";
					if (array3.Length > 2)
					{
						for (int k = 2; k < array3.Length; k++)
						{
							text8 = text8 + array3[k] + " ";
						}
					}
					if (string.IsNullOrEmpty(text7))
					{
						if (!MPSaveManager.BannedUsers.ContainsKey(text7))
						{
							MPSaveManager.BannedUsers.Add(text7, text8);
							MPSaveManager.SaveBanned();
							return "User " + text7 + " banned. Reason " + text8;
						}
						return "This user already banned!";
					}
					return "Incorrect syntax!";
				}
				if (text.StartsWith("unbanmac ") || text.StartsWith("unban "))
				{
					string text9 = CMD.Split(' ')[1];
					if (!string.IsNullOrEmpty(text9))
					{
						if (MPSaveManager.BannedUsers.ContainsKey(text9))
						{
							MPSaveManager.BannedUsers.Remove(text9);
							MPSaveManager.SaveBanned();
							return "User " + text9 + " unbanned!";
						}
						return "This user isn't banned!";
					}
					return "Incorrect syntax";
				}
				if (text.StartsWith("addloottoscene "))
				{
					string[] array4 = CMD.Split(' ');
					if (array4.Length == 2)
					{
						string text10 = array4[1];
						return "Added loot to " + MPSaveManager.AddLootToScene(text10) + " containers on scene " + text10;
					}
					return "Incorrect syntax";
				}
				if (text.StartsWith("addloot "))
				{
					string[] array5 = CMD.Split(' ');
					if (array5.Length == 3)
					{
						string text11 = array5[1];
						string text12 = array5[2];
						if (MPSaveManager.AddLootToContainerOnScene(text12, text11))
						{
							return "Added loot to container with GUID " + text12 + " on scene " + text11;
						}
						return "Scene " + text11 + " does not contains contaienr with GUID " + text12 + " or this container already has loot!";
					}
					return "Incorrect syntax";
				}
				if (text.StartsWith("exp "))
				{
					string[] array6 = CMD.Split(' ');
					if (array6.Length == 3)
					{
						string text13 = array6[1];
						int iD = int.Parse(array6[2]);
						string mACByID = Server.GetMACByID(iD);
						if (string.IsNullOrEmpty(mACByID))
						{
							return "There no player with ID " + iD;
						}
						for (int num3 = ExpeditionManager.m_ActiveExpeditions.Count - 1; num3 >= 0; num3--)
						{
							if (ExpeditionManager.m_ActiveExpeditions[num3].m_Players.Contains(mACByID))
							{
								ExpeditionManager.CompleteExpedition(ExpeditionManager.m_ActiveExpeditions[num3].m_GUID, 0);
							}
						}
						if (ExpeditionManager.StartNewExpedition(mACByID, 0, text13, NoMessage: true))
						{
							return "Expedition " + text13 + " started for player ID " + iD;
						}
						return "Wasn't able to start expedition " + text13;
					}
					return "Incorrect syntax.";
				}
				if (text == "exps")
				{
					ExpeditionBuilder.RefrashTemplatesList();
					string text14 = "";
					{
						foreach (System.Collections.Generic.KeyValuePair<int, System.Collections.Generic.List<ExpeditionBuilder.ExpeditionTaskTemplate>> expeditionTask in ExpeditionBuilder.m_ExpeditionTasks)
						{
							_ = expeditionTask.Key;
							foreach (ExpeditionBuilder.ExpeditionTaskTemplate item in expeditionTask.Value)
							{
								if (item.m_CanBeTaken && item.m_TaskType != ExpeditionManager.ExpeditionTaskType.CRASHSITE)
								{
									string alias = item.m_Alias;
									text14 = ((!string.IsNullOrEmpty(text14)) ? (text14 + "\n" + alias) : alias);
								}
							}
						}
						return text14;
					}
				}
				if (text == "expsfull")
				{
					ExpeditionBuilder.RefrashTemplatesList();
					string text15 = "";
					{
						foreach (System.Collections.Generic.KeyValuePair<int, System.Collections.Generic.List<ExpeditionBuilder.ExpeditionTaskTemplate>> expeditionTask2 in ExpeditionBuilder.m_ExpeditionTasks)
						{
							_ = expeditionTask2.Key;
							foreach (ExpeditionBuilder.ExpeditionTaskTemplate item2 in expeditionTask2.Value)
							{
								string alias2 = item2.m_Alias;
								text15 = ((!string.IsNullOrEmpty(text15)) ? (text15 + "\n" + alias2) : alias2);
							}
						}
						return text15;
					}
				}
				uConsole.RunCommand(CMD);
				return uConsoleLog.m_Log[uConsoleLog.m_Log.Count - 1];
			}
		}
	}

	public static string CompressString(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		MemoryStream memoryStream = new MemoryStream();
		using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
		{
			gZipStream.Write(bytes, 0, bytes.Length);
		}
		memoryStream.Position = 0L;
		byte[] array = new byte[memoryStream.Length];
		memoryStream.Read(array, 0, array.Length);
		byte[] array2 = new byte[array.Length + 4];
		Buffer.BlockCopy(array, 0, array2, 4, array.Length);
		Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length), 0, array2, 0, 4);
		return Convert.ToBase64String(array2);
	}

	public static string DecompressString(string compressedText)
	{
		byte[] array = Convert.FromBase64String(compressedText);
		using MemoryStream memoryStream = new MemoryStream();
		int num = BitConverter.ToInt32(array, 0);
		memoryStream.Write(array, 4, array.Length - 4);
		byte[] array2 = new byte[num];
		memoryStream.Position = 0L;
		using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
		{
			gZipStream.Read(array2, 0, array2.Length);
		}
		return Encoding.UTF8.GetString(array2);
	}

	public static bool IsBase64String(string s)
	{
		s = s.Trim();
		if (s.Length % 4 == 0)
		{
			return Regex.IsMatch(s, "^[a-zA-Z0-9\\+/]*={0,3}$", RegexOptions.None);
		}
		return false;
	}

	public static long GetSaveFingerprint(string saveData)
	{
		if (saveData == null)
		{
			throw new ArgumentNullException("saveData");
		}
		if (saveData.Length == 0)
		{
			throw new ArgumentException("Save data must not be empty.", "saveData");
		}
		using SHA256 sHA = SHA256.Create();
		long num = BinaryPrimitives.ReadInt64LittleEndian(sHA.ComputeHash(Encoding.UTF8.GetBytes(saveData)));
		return (num == 0L) ? 1 : num;
	}

	public static long GetDeterministicId(string m)
	{
		return (long)m.ToCharArray().Select((char c, int i) => Math.Pow(i, c % 5) * Math.Max(Math.Sqrt((int)c), i)).Sum();
	}

	public static string GetMacAddress()
	{
		string result = string.Empty;
		NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
		foreach (NetworkInterface networkInterface in allNetworkInterfaces)
		{
			if (networkInterface.OperationalStatus == OperationalStatus.Up && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback && networkInterface.GetPhysicalAddress().ToString().Length > 4)
			{
				result = networkInterface.GetPhysicalAddress().ToString();
				break;
			}
		}
		return result;
	}

	public static void RequestPhoto(string PhotoGUID, int Client = 0)
	{
		if (MyMod.sendMyPosition)
		{
			using Packet packet = new Packet(160);
			packet.Write(PhotoGUID);
			MyMod.SendUDPData(packet);
		}
		if (MyMod.iAmHost && Client != 0)
		{
			ServerSend.REQUESTSPHOTOAGAIN(Client, PhotoGUID);
		}
	}

	public static string SHA256CheckSum(string base64)
	{
		if (string.IsNullOrEmpty(base64))
		{
			return "null";
		}
		byte[] array;
		using (SHA256 sHA = SHA256.Create())
		{
			array = sHA.ComputeHash(Encoding.UTF8.GetBytes(base64));
		}
		string text = string.Empty;
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			text += b.ToString("x2");
		}
		return text;
	}

	public static void Base64DataSelfTest(System.Collections.Generic.List<DataStr.SlicedBase64Data> Slices)
	{
		foreach (DataStr.SlicedBase64Data Slice in Slices)
		{
			if (!Base64Slices.ContainsKey(Slice.m_GUID))
			{
				Base64Slices.Add(Slice.m_GUID, new DataStr.Base64SliceBase(Slice.m_LastSliceIndex + 1));
			}
			Base64Slices[Slice.m_GUID].SetSlice(Slice.m_SliceIndex, Slice.m_Slice);
			if (!Base64Slices[Slice.m_GUID].IsReady())
			{
				continue;
			}
			string text = "";
			string[] slices = Base64Slices[Slice.m_GUID].m_Slices;
			Base64Slices.Remove(Slice.m_GUID);
			for (int i = 0; i < slices.Length; i++)
			{
				if (!string.IsNullOrEmpty(slices[i]))
				{
					text += slices[i];
					continue;
				}
				Log("[Base64DataSelfTest] Some slices are missing! Doing another request", LoggerColor.Red);
				Log("[Base64DataSelfTest] Problem accured on " + i + "/" + Slice.m_LastSliceIndex + " slice", LoggerColor.Red);
				return;
			}
			bool flag = IsBase64String(text);
			string text2 = SHA256CheckSum(text);
			Log("[Base64DataSelfTest] Final string is base64? " + flag);
			Log("[Base64DataSelfTest] Final checksum " + text2 + " expected " + Slice.m_CheckSum);
			if (flag && text2 == Slice.m_CheckSum)
			{
				Log("[Base64DataSelfTest] Everything correct");
			}
			else if (!flag)
			{
				Log("[Base64DataSelfTest] Base64 string corrupted!.", LoggerColor.Red);
			}
			else if (text2 == Slice.m_CheckSum)
			{
				Log("[Base64DataSelfTest] Checksum is incorrect!", LoggerColor.Red);
			}
		}
	}

	public static void SendSlicedBase64Data(System.Collections.Generic.List<DataStr.SlicedBase64Data> Slices, int SendTo = 0)
	{
		if (MyMod.sendMyPosition)
		{
			foreach (DataStr.SlicedBase64Data Slice in Slices)
			{
				using Packet packet = new Packet(161);
				packet.Write(Slice);
				MyMod.SendUDPData(packet);
			}
		}
		if (!MyMod.iAmHost)
		{
			return;
		}
		foreach (DataStr.SlicedBase64Data Slice2 in Slices)
		{
			ServerSend.BASE64SLICE(SendTo, Slice2);
		}
	}

	public static void SendSlicedBase64Data(System.Collections.Generic.List<DataStr.SlicedBase64Data> Slices, string LevelGUID)
	{
		if (!MyMod.iAmHost)
		{
			return;
		}
		foreach (DataStr.SlicedBase64Data Slice in Slices)
		{
			ServerSend.BASE64SLICE(Slice, LevelGUID);
		}
	}

	public static System.Collections.Generic.List<DataStr.SlicedBase64Data> GetBase64Sliced(string FullString, string GUID, DataStr.SlicedBase64Purpose Purpose)
	{
		string checkSum = SHA256CheckSum(FullString);
		int num = 175;
		int num2 = (int)Math.Ceiling((double)FullString.Length / (double)num);
		System.Collections.Generic.List<DataStr.SlicedBase64Data> list = new System.Collections.Generic.List<DataStr.SlicedBase64Data>();
		for (int i = 0; i < num2; i++)
		{
			int num3 = i * num;
			int length = Math.Min(num, FullString.Length - num3);
			string slice = FullString.Substring(num3, length);
			DataStr.SlicedBase64Data slicedBase64Data = new DataStr.SlicedBase64Data();
			slicedBase64Data.m_Slice = slice;
			slicedBase64Data.m_LastSliceIndex = num2 - 1;
			slicedBase64Data.m_SliceIndex = i;
			slicedBase64Data.m_CheckSum = checkSum;
			slicedBase64Data.m_GUID = GUID;
			slicedBase64Data.m_Purpose = (int)Purpose;
			list.Add(slicedBase64Data);
		}
		return list;
	}

	public static void AddBase64Slice(DataStr.SlicedBase64Data Data, int FromClient = 0)
	{
		DataStr.SlicedBase64Purpose purpose = (DataStr.SlicedBase64Purpose)Data.m_Purpose;
		if (!Base64Slices.ContainsKey(Data.m_GUID))
		{
			Base64Slices.Add(Data.m_GUID, new DataStr.Base64SliceBase(Data.m_LastSliceIndex + 1));
		}
		Base64Slices[Data.m_GUID].SetSlice(Data.m_SliceIndex, Data.m_Slice);
		if (!Base64Slices[Data.m_GUID].IsReady())
		{
			return;
		}
		string text = Data.m_GUID;
		string text2 = "";
		if (purpose == DataStr.SlicedBase64Purpose.Container)
		{
			text = Data.m_GUID.Split('|')[0];
			text2 = Data.m_GUID.Split('|')[1];
		}
		Log("Finishing getting base64 slices for " + text, LoggerColor.Green);
		string text3 = "";
		string[] slices = Base64Slices[Data.m_GUID].m_Slices;
		Base64Slices.Remove(Data.m_GUID);
		for (int i = 0; i < slices.Length; i++)
		{
			if (!string.IsNullOrEmpty(slices[i]))
			{
				text3 += slices[i];
				continue;
			}
			Log("Some slices are missing! Doing another request", LoggerColor.Red);
			Log("Problem accured on " + i + "/" + Data.m_LastSliceIndex + " slice", LoggerColor.Red);
			if (purpose == DataStr.SlicedBase64Purpose.Photo)
			{
				RequestPhoto(text, FromClient);
			}
			if (purpose == DataStr.SlicedBase64Purpose.Container)
			{
				if (MyMod.iAmHost)
				{
					ServerSend.FINISHEDSENDINGCONTAINER(FromClient, Result: true, text2, text, Data.m_CheckSum);
				}
				if (MyMod.sendMyPosition)
				{
					CoopContainerFlow.RetryCorrupted(text2, text);
				}
			}
			return;
		}
		bool flag = IsBase64String(text3);
		string text4 = SHA256CheckSum(text3);
		Log("Final string is base64? " + flag);
		Log("Final checksum " + text4 + " expected " + Data.m_CheckSum);
		if (flag && text4 == Data.m_CheckSum)
		{
			Log("Everything correct");
			if (purpose == DataStr.SlicedBase64Purpose.Photo)
			{
				if (MyMod.sendMyPosition)
				{
					MPSaveManager.AddPhoto(text3, Cached: true, text);
				}
				else
				{
					MPSaveManager.AddPhoto(text3, Cached: true, text);
					MPSaveManager.AddPhoto(text3, Cached: false, text);
				}
				foreach (System.Collections.Generic.KeyValuePair<int, GameObject> droppedGearsObj in MyMod.DroppedGearsObjs)
				{
					if (droppedGearsObj.Value != null && (bool)droppedGearsObj.Value.GetComponent<Comps.DroppedGearDummy>() && droppedGearsObj.Value.GetComponent<Comps.DroppedGearDummy>().m_Extra.m_PhotoGUID == text)
					{
						Texture2D photoTexture = MPSaveManager.GetPhotoTexture(text, droppedGearsObj.Value.GetComponent<Comps.DroppedGearDummy>().m_Extra.m_GearName);
						if ((bool)photoTexture)
						{
							droppedGearsObj.Value.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = photoTexture;
						}
						MelonLogger.Msg("Found and applied on " + droppedGearsObj.Key + " dropped gear");
					}
				}
				if (MyMod.iAmHost)
				{
					SendSlicedBase64Data(GetBase64Sliced(text3, text, DataStr.SlicedBase64Purpose.Photo), MyMod.playersData[FromClient].m_LevelGuid);
				}
			}
			if (purpose != DataStr.SlicedBase64Purpose.Container)
			{
				return;
			}
			if (MyMod.iAmHost)
			{
				MPSaveManager.SaveContainer(text2, text, text3);
				ServerSend.FINISHEDSENDINGCONTAINER(FromClient, Result: false, text2, text, Data.m_CheckSum);
			}
			if (MyMod.sendMyPosition && ContainerLootSync.MatchesPending(text2, text))
			{
				if (MyMod.LastPacketForRepeat == MyMod.ResendPacketType.Container)
				{
					MyMod.DiscardRepeatPacket();
				}
				MyMod.FinishOpeningFakeContainer(text3);
			}
			return;
		}
		if (!flag)
		{
			Log("Base64 string corrupted, doing another request...", LoggerColor.Red);
		}
		else if (text4 == Data.m_CheckSum)
		{
			Log("Checksum is incorrect, doing another request...", LoggerColor.Red);
		}
		if (purpose == DataStr.SlicedBase64Purpose.Photo)
		{
			RequestPhoto(text, FromClient);
		}
		if (purpose == DataStr.SlicedBase64Purpose.Container)
		{
			if (MyMod.iAmHost)
			{
				ServerSend.FINISHEDSENDINGCONTAINER(FromClient, Result: true, text2, text, Data.m_CheckSum);
			}
			else if (MyMod.sendMyPosition)
			{
				CoopContainerFlow.RetryCorrupted(text2, text);
			}
		}
	}

	public static int GetPlayersOnServer()
	{
		int num = 0;
		for (int i = 1; i <= Server.MaxPlayers; i++)
		{
			if (Server.clients[i] != null && Server.clients[i].IsBusy() && !Server.clients[i].RCON)
			{
				num++;
			}
		}
		return num;
	}

	public static void WebhookPlayerJoin(string PlayerName)
	{
	}

	public static void WebhookPlayerLeave(string PlayerName)
	{
		string.IsNullOrEmpty(PlayerName);
	}

	public static void WebhookCrashSiteSpawn(string Text)
	{
	}

	public static void WebhookCrashSiteFound()
	{
	}

	public static void WebhookCrashSiteTimeOver()
	{
	}

	public static void AddInterloperReplace(string Gear, string Replace, bool Rare = false)
	{
		if (!m_InterloperFilter.ContainsKey(Gear.ToLower()))
		{
			m_InterloperFilter.Add(Gear.ToLower(), Replace);
			if (Rare)
			{
				m_InterloperRare.Add(Gear.ToLower());
			}
		}
	}

	public static bool IsInterloperExpereinceMode()
	{
		return ExperienceModeManager.GetCurrentExperienceModeType() == ExperienceModeType.Interloper;
	}

	public static bool IsStalkerExpereinceMode()
	{
		return ExperienceModeManager.GetCurrentExperienceModeType() == ExperienceModeType.Stalker;
	}

	public static bool IsVoyageurExpereinceMode()
	{
		return ExperienceModeManager.GetCurrentExperienceModeType() != ExperienceModeType.Voyageur;
	}

	public static bool IsPilgrimExpereinceMode()
	{
		return ExperienceModeManager.GetCurrentExperienceModeType() != ExperienceModeType.Pilgrim;
	}

	public static string GetInterloperReplace(string Gear)
	{
		if (ExperienceModeManager.GetCurrentExperienceModeType() != ExperienceModeType.Interloper)
		{
			return Gear;
		}
		if (m_InterloperFilter.TryGetValue(Gear.ToLower(), out var value))
		{
			if (m_InterloperRare.Contains(Gear.ToLower()))
			{
				if (new System.Random(Guid.NewGuid().GetHashCode()).NextDouble() < 0.33000001311302185)
				{
					return value;
				}
				return "";
			}
			return value;
		}
		return Gear;
	}

	public static void LoadInterloperReplace()
	{
		AddInterloperReplace("GEAR_AirlineFoodChick", "");
		AddInterloperReplace("GEAR_AirlineFoodVeg", "");
		AddInterloperReplace("GEAR_CondensedMilk", "GEAR_Peaches");
		AddInterloperReplace("GEAR_PeanutButter", "GEAR_MapleSyrup", Rare: true);
		AddInterloperReplace("GEAR_Arrow", "");
		AddInterloperReplace("GEAR_Bow", "");
		AddInterloperReplace("GEAR_BrokenArrow", "");
		AddInterloperReplace("GEAR_Revolver", "");
		AddInterloperReplace("GEAR_RevolverAmmoBox", "");
		AddInterloperReplace("GEAR_RevolverAmmoCasing", "");
		AddInterloperReplace("GEAR_RevolverAmmoSingle", "");
		AddInterloperReplace("GEAR_Rifle", "");
		AddInterloperReplace("GEAR_RifleAmmoBox", "");
		AddInterloperReplace("GEAR_RifleAmmoCasing", "");
		AddInterloperReplace("GEAR_RifleAmmoSingle", "");
		AddInterloperReplace("GEAR_Flashlight", "");
		AddInterloperReplace("GEAR_Hatchet", "GEAR_HatchetImprovised", Rare: true);
		AddInterloperReplace("GEAR_HookAndLine", "GEAR_Hook");
		AddInterloperReplace("GEAR_Knife", "GEAR_KnifeImprovised", Rare: true);
		AddInterloperReplace("GEAR_NewsprintRoll", "GEAR_Tinder");
		AddInterloperReplace("GEAR_WoodMatches", "GEAR_PackMatches");
		AddInterloperReplace("GEAR_RifleCleaningKit", "GEAR_SewingKit", Rare: true);
		AddInterloperReplace("GEAR_Balaclava", "GEAR_Toque");
		AddInterloperReplace("GEAR_ClimbingSocks", "GEAR_WoolSocks");
		AddInterloperReplace("GEAR_CowichanSweater", "GEAR_FleeceSweater");
		AddInterloperReplace("GEAR_DownParka", "GEAR_LightParka");
		AddInterloperReplace("GEAR_FishermanSweater", "GEAR_PlaidShirt");
		AddInterloperReplace("GEAR_Gauntlets", "GEAR_WorkGloves");
		AddInterloperReplace("GEAR_GreyMotherBoots", "");
		AddInterloperReplace("GEAR_HeavyParka", "GEAR_BasicWinterCoat");
		AddInterloperReplace("GEAR_HeavyWoolSweater", "GEAR_FleeceSweater");
		AddInterloperReplace("GEAR_InsulatedBoots", "GEAR_LeatherShoes");
		AddInterloperReplace("GEAR_InsulatedPants", "GEAR_CombatPants");
		AddInterloperReplace("GEAR_LongUnderwearWool", "GEAR_LongUnderwear");
		AddInterloperReplace("GEAR_MilitaryParka", "GEAR_DownSkiJacket");
		AddInterloperReplace("GEAR_Mittens", "GEAR_FleeceMittens");
		AddInterloperReplace("GEAR_MuklukBoots", "GEAR_WorkBoots");
		AddInterloperReplace("GEAR_PremiumWinterCoat", "GEAR_DownSkiJacket");
		AddInterloperReplace("GEAR_QualityWinterCoat", "GEAR_SkiJacket");
		AddInterloperReplace("GEAR_SkiBoots", "");
		AddInterloperReplace("GEAR_SkiGloves", "GEAR_BasicGloves");
		AddInterloperReplace("GEAR_WoolShirt", "GEAR_TeeShirt");
		AddInterloperReplace("GEAR_WoolWrap", "GEAR_CottonScarf");
		AddInterloperReplace("GEAR_WoolWrapCap", "GEAR_BasicWoolScarf");
	}
}
