using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TinyJSON;
using UnityEngine;

namespace SkyCoop;

public class ExpeditionBuilder
{
	public class ExpeditionGearSpawner
	{
		public string m_GUID = "";

		public List<string> m_GearsVariant = new List<string>();

		public float m_Chance;

		public Vector3 m_Possition = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public DataStr.ExtraDataForDroppedGear m_Extra = new DataStr.ExtraDataForDroppedGear();

		public bool RollChance(int PlayersInExpedition = 1)
		{
			System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
			float num = m_Chance;
			if (PlayersInExpedition > 1)
			{
				int num2 = PlayersInExpedition - 1;
				num += (float)(num2 * 5);
			}
			return random.NextDouble() < (double)num;
		}

		public string PickGear()
		{
			if (m_GearsVariant.Count > 2)
			{
				int index = new System.Random(Guid.NewGuid().GetHashCode()).Next(0, m_GearsVariant.Count - 1);
				return Shared.GetInterloperReplace(m_GearsVariant[index]);
			}
			if (m_GearsVariant.Count == 1)
			{
				return Shared.GetInterloperReplace(m_GearsVariant[0]);
			}
			return "GEAR_Tinder";
		}
	}

	public class ExpeditionTaskTemplate
	{
		public ExpeditionManager.ExpeditionTaskType m_TaskType;

		public string m_Alias = "";

		public string m_TaskText = "";

		public int m_RegionBelong;

		public string m_SceneName = "";

		public List<ExpeditionGearSpawner> m_GearSpawners = new List<ExpeditionGearSpawner>();

		public List<string> m_Containers = new List<string>();

		public List<string> m_Plants = new List<string>();

		public List<string> m_Breakdowns = new List<string>();

		public bool m_RestockSceneContainers;

		public Vector3 m_ZoneCenter = new Vector3(0f, 0f, 0f);

		public float m_ZoneRadius = 1f;

		public string m_NextTaskAlias = "";

		public bool m_CanBeTaken = true;

		public string m_ObjectiveGearGUID = "";

		public ExpeditionManager.ExpeditionCompleteOrder m_CompleatOrder;

		public int m_RandomTasksAmout;

		public int m_Time = 3600;

		public bool m_TimeAdd = true;

		public int m_StaySeconds = 300;

		public List<DataStr.UniversalSyncableObjectSpawner> m_Objects = new List<DataStr.UniversalSyncableObjectSpawner>();
	}

	public static Dictionary<int, List<ExpeditionTaskTemplate>> m_ExpeditionTasks = new Dictionary<int, List<ExpeditionTaskTemplate>>();

	public static bool Initilized = false;

	public static void RefrashTemplatesList()
	{
		m_ExpeditionTasks.Clear();
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetBaseDirectory() + "Mods");
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates");
		FileInfo[] files = new DirectoryInfo(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates").GetFiles("*.json");
		for (int i = 0; i < files.Length; i++)
		{
			byte[] bytes = File.ReadAllBytes(files[i].FullName);
			string text = Encoding.UTF8.GetString(bytes);
			if (!string.IsNullOrEmpty(text))
			{
				text = MPSaveManager.VectorsFixUp(text);
				ExpeditionTaskTemplate expeditionTaskTemplate = JSON.Load(text).Make<ExpeditionTaskTemplate>();
				if (!m_ExpeditionTasks.ContainsKey(expeditionTaskTemplate.m_RegionBelong))
				{
					m_ExpeditionTasks.Add(expeditionTaskTemplate.m_RegionBelong, new List<ExpeditionTaskTemplate>());
				}
				m_ExpeditionTasks[expeditionTaskTemplate.m_RegionBelong].Add(expeditionTaskTemplate);
			}
		}
	}

	public static void Init(bool Force = false)
	{
		if (!Initilized || Force)
		{
			int num = 3600;
			ExpeditionManager.NextCrashSiteIn = new System.Random().Next(num * 3, num * 6);
			RefrashTemplatesList();
			Initilized = true;
		}
	}

	public static List<string> GetWorldEdits()
	{
		List<string> list = new List<string>();
		if (Directory.Exists(MPSaveManager.GetBaseDirectory() + "Mods"))
		{
			string path = MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates";
			if (Directory.Exists(path))
			{
				FileInfo[] files = new DirectoryInfo(path).GetFiles("*.json");
				foreach (FileInfo fileInfo in files)
				{
					if (fileInfo.Name.StartsWith("WorldEdit"))
					{
						byte[] bytes = File.ReadAllBytes(fileInfo.FullName);
						if (!string.IsNullOrEmpty(Encoding.UTF8.GetString(bytes)))
						{
							list.Add(fileInfo.Name.Replace(".json", ""));
						}
					}
				}
			}
		}
		return list;
	}

	public static string GetRandomCrashSiteName(int Index = -1)
	{
		List<string> list = new List<string>();
		if (Directory.Exists(MPSaveManager.GetBaseDirectory() + "Mods"))
		{
			string path = MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates";
			if (Directory.Exists(path))
			{
				FileInfo[] files = new DirectoryInfo(path).GetFiles("*.json");
				foreach (FileInfo fileInfo in files)
				{
					if (fileInfo.Name.StartsWith("Crashsite"))
					{
						byte[] bytes = File.ReadAllBytes(fileInfo.FullName);
						if (!string.IsNullOrEmpty(Encoding.UTF8.GetString(bytes)))
						{
							list.Add(fileInfo.Name.Replace(".json", ""));
						}
					}
				}
			}
			if (list.Count > 0)
			{
				if (Index == -1)
				{
					if (list.Count > 1)
					{
						return list[new System.Random().Next(0, list.Count - 1)];
					}
					return list[0];
				}
				if (list.Count - 1 >= Index)
				{
					return list[Index];
				}
				return "";
			}
			return "";
		}
		return "";
	}

	public static ExpeditionManager.Expedition BuildBasicExpedition(int Region, string Alias = "", bool DebugFlag = false, bool NoMulty = false)
	{
		bool flag = false;
		int region = 0;
		System.Random random = new System.Random();
		List<ExpeditionTaskTemplate> list = new List<ExpeditionTaskTemplate>();
		if (string.IsNullOrEmpty(Alias))
		{
			ExpeditionManager.DebugLog("Searching neighbors for Region " + Region);
			bool flag2 = false;
			List<int> neighborRegions = GetNeighborRegions(Region);
			ExpeditionManager.DebugLog("Found " + neighborRegions.Count + " neighbors");
			int num = 0;
			if (neighborRegions.Count > 2)
			{
				num = random.Next(0, neighborRegions.Count - 1);
				region = neighborRegions[num];
				flag2 = true;
				ExpeditionManager.DebugLog("Randomly select Region " + region + " with index " + num);
			}
			else if (neighborRegions.Count == 1)
			{
				region = neighborRegions[0];
				ExpeditionManager.DebugLog("Selecting Region " + region);
			}
			list = GetTasksForRegion(region);
			ExpeditionManager.DebugLog("Region " + region + " has " + list.Count + " Avaliable Tasks");
			if (list.Count > 0)
			{
				flag = true;
			}
			if (!flag && flag2)
			{
				int num2 = num;
				ExpeditionManager.DebugLog("This region invalid for expedition, trying next one, but stop if we once again meet index " + num2);
				int num3 = num + 1;
				do
				{
					region = neighborRegions[num3];
					ExpeditionManager.DebugLog("Next region is Region " + region + " with index " + num3);
					list = GetTasksForRegion(region);
					ExpeditionManager.DebugLog("Region " + region + " has " + list.Count + " Avaliable Tasks");
					if (list.Count == 0)
					{
						num3++;
						if (num3 > neighborRegions.Count - 1)
						{
							num3 = 0;
						}
						continue;
					}
					flag = true;
					ExpeditionManager.DebugLog("This region is valid for expedition");
					break;
				}
				while (num3 != num2);
			}
		}
		ExpeditionTaskTemplate expeditionTaskTemplate = null;
		if (!string.IsNullOrEmpty(Alias))
		{
			if (NoMulty)
			{
				foreach (ExpeditionManager.Expedition activeExpedition in ExpeditionManager.m_ActiveExpeditions)
				{
					if (activeExpedition.m_Alias == Alias)
					{
						return null;
					}
				}
			}
			expeditionTaskTemplate = JSON.Load(GetExpeditionJsonByAlias(Alias)).Make<ExpeditionTaskTemplate>();
			flag = true;
		}
		if (flag)
		{
			if (expeditionTaskTemplate == null)
			{
				expeditionTaskTemplate = ((list.Count <= 1) ? list[0] : list[random.Next(0, list.Count - 1)]);
			}
			ExpeditionManager.Expedition expedition = new ExpeditionManager.Expedition();
			expedition.m_Alias = expeditionTaskTemplate.m_Alias;
			expedition.m_RegionBelong = expeditionTaskTemplate.m_RegionBelong;
			expedition.m_Name = GetRegionString(expeditionTaskTemplate.m_RegionBelong) + " Expedition";
			List<ExpeditionTaskTemplate> list2 = new List<ExpeditionTaskTemplate> { expeditionTaskTemplate };
			while (!(expeditionTaskTemplate.m_NextTaskAlias == ""))
			{
				string nextTaskAlias;
				if (expeditionTaskTemplate.m_NextTaskAlias.Contains("#"))
				{
					string text = expeditionTaskTemplate.m_NextTaskAlias.Split('#')[0];
					string text2 = expeditionTaskTemplate.m_NextTaskAlias.Split('#')[1];
					if (text2.Contains("-"))
					{
						List<string> list3 = new List<string>();
						int num4 = int.Parse(text2.Split('-')[0]);
						int num5 = int.Parse(text2.Split('-')[1]);
						int randomTasksAmout = expeditionTaskTemplate.m_RandomTasksAmout;
						for (int i = num4; i <= num5; i++)
						{
							string text3 = text + i;
							bool flag3 = false;
							foreach (ExpeditionManager.Expedition activeExpedition2 in ExpeditionManager.m_ActiveExpeditions)
							{
								foreach (ExpeditionManager.ExpeditionTask task in activeExpedition2.m_Tasks)
								{
									if (task.m_Alias == text3)
									{
										flag3 = true;
										break;
									}
								}
							}
							if (!flag3)
							{
								list3.Add(text3);
							}
						}
						for (int j = 1; j <= randomTasksAmout; j++)
						{
							int index = random.Next(0, list3.Count);
							string text4 = list3[index];
							list3.RemoveAt(index);
							string expeditionJsonByAlias = GetExpeditionJsonByAlias(text4);
							if (string.IsNullOrEmpty(expeditionJsonByAlias))
							{
								ExpeditionManager.DebugLog("CAN'T FIND EXPEDITION VARIANT TASK " + text4 + "!!!!!!!!!!!");
								break;
							}
							expeditionTaskTemplate = JSON.Load(expeditionJsonByAlias).Make<ExpeditionTaskTemplate>();
							list2.Add(expeditionTaskTemplate);
						}
						continue;
					}
					nextTaskAlias = expeditionTaskTemplate.m_NextTaskAlias;
				}
				else
				{
					nextTaskAlias = expeditionTaskTemplate.m_NextTaskAlias;
				}
				string expeditionJsonByAlias2 = GetExpeditionJsonByAlias(nextTaskAlias);
				if (string.IsNullOrEmpty(expeditionJsonByAlias2))
				{
					ExpeditionManager.DebugLog("CAN'T FIND EXPEDITION TASK " + nextTaskAlias + "!!!!!!!!!!!");
					break;
				}
				expeditionTaskTemplate = JSON.Load(expeditionJsonByAlias2).Make<ExpeditionTaskTemplate>();
				list2.Add(expeditionTaskTemplate);
			}
			if (list2.Count > 0)
			{
				expedition.m_TimeLeft = list2[0].m_Time;
			}
			expedition.m_GUID = MPSaveManager.GetNewUGUID();
			{
				foreach (ExpeditionTaskTemplate item in list2)
				{
					ExpeditionManager.ExpeditionTask expeditionTask = new ExpeditionManager.ExpeditionTask();
					expeditionTask.m_Debug = DebugFlag;
					expeditionTask.m_Alias = item.m_Alias;
					expeditionTask.m_Type = item.m_TaskType;
					expeditionTask.m_Scene = item.m_SceneName;
					expeditionTask.m_ZoneCenter = item.m_ZoneCenter;
					expeditionTask.m_ZoneRadius = item.m_ZoneRadius;
					expeditionTask.m_Text = item.m_TaskText;
					expeditionTask.m_SpecificContrainers = item.m_Containers;
					expeditionTask.m_GearSpawners = item.m_GearSpawners;
					expeditionTask.m_RestockSceneContainers = item.m_RestockSceneContainers;
					expeditionTask.m_ObjectiveGearSpawnerGUID = item.m_ObjectiveGearGUID;
					expeditionTask.m_CompleteOrder = item.m_CompleatOrder;
					expeditionTask.m_Time = item.m_Time;
					expeditionTask.m_TimeAdd = item.m_TimeAdd;
					expeditionTask.m_SpecificPlants = item.m_Plants;
					expeditionTask.m_SpecificBreakdowns = item.m_Breakdowns;
					expeditionTask.m_StayInZoneSeconds = item.m_StaySeconds;
					expeditionTask.m_ObjectSpawners = item.m_Objects;
					expeditionTask.m_ExpeditionGUID = expedition.m_GUID;
					expedition.m_Tasks.Add(expeditionTask);
				}
				return expedition;
			}
		}
		return null;
	}

	public static void MayAddTaskToList(List<ExpeditionTaskTemplate> List, ExpeditionTaskTemplate Task)
	{
		if (!Task.m_CanBeTaken)
		{
			return;
		}
		foreach (ExpeditionManager.Expedition activeExpedition in ExpeditionManager.m_ActiveExpeditions)
		{
			if (activeExpedition.m_Alias == Task.m_Alias)
			{
				return;
			}
		}
		List.Add(Task);
	}

	public static List<ExpeditionTaskTemplate> GetTasksForRegion(int Region)
	{
		List<ExpeditionTaskTemplate> list = new List<ExpeditionTaskTemplate>();
		if (m_ExpeditionTasks.TryGetValue(Region, out var value))
		{
			foreach (ExpeditionTaskTemplate item in value)
			{
				MayAddTaskToList(list, item);
			}
			return list;
		}
		return list;
	}

	public static List<int> GetNeighborRegions(int Region)
	{
		List<int> list = new List<int>();
		switch ((Shared.GameRegion)Region)
		{
		case Shared.GameRegion.MysteryLake:
			list.Add(5);
			list.Add(8);
			list.Add(3);
			list.Add(1);
			list.Add(-2);
			list.Add(-3);
			list.Add(11);
			break;
		case Shared.GameRegion.CoastalHighWay:
			list.Add(0);
			list.Add(3);
			list.Add(2);
			list.Add(11);
			list.Add(-1);
			list.Add(-2);
			break;
		case Shared.GameRegion.DesolationPoint:
			list.Add(1);
			list.Add(-1);
			list.Add(-2);
			break;
		case Shared.GameRegion.PlesantValley:
			list.Add(0);
			list.Add(1);
			list.Add(4);
			list.Add(13);
			list.Add(-4);
			list.Add(-5);
			list.Add(-3);
			break;
		case Shared.GameRegion.TimberwolfMountain:
			list.Add(3);
			list.Add(12);
			list.Add(13);
			break;
		case Shared.GameRegion.ForlornMuskeg:
			list.Add(8);
			list.Add(9);
			list.Add(11);
			list.Add(0);
			break;
		case Shared.GameRegion.MountainTown:
			list.Add(5);
			list.Add(0);
			list.Add(10);
			list.Add(9);
			break;
		case Shared.GameRegion.BrokenRailroad:
			list.Add(5);
			list.Add(0);
			list.Add(8);
			list.Add(-2);
			break;
		case Shared.GameRegion.HushedRiverValley:
			list.Add(8);
			list.Add(0);
			list.Add(5);
			break;
		case Shared.GameRegion.BleakInlet:
			list.Add(5);
			list.Add(0);
			list.Add(-3);
			list.Add(1);
			list.Add(-2);
			break;
		case Shared.GameRegion.AshCanyon:
			list.Add(4);
			list.Add(13);
			list.Add(3);
			break;
		case Shared.GameRegion.Blackrock:
			list.Add(4);
			list.Add(3);
			list.Add(-5);
			list.Add(-4);
			break;
		case Shared.GameRegion.CrumblingHighWay:
			list.Add(1);
			list.Add(2);
			list.Add(-2);
			break;
		case Shared.GameRegion.WindingRiver:
			list.Add(0);
			list.Add(3);
			list.Add(-2);
			list.Add(5);
			break;
		case Shared.GameRegion.KeepersPassNorth:
			list.Add(13);
			list.Add(3);
			list.Add(-3);
			list.Add(4);
			break;
		case Shared.GameRegion.KeepersPassSouth:
			list.Add(13);
			list.Add(3);
			list.Add(-3);
			list.Add(4);
			break;
		case Shared.GameRegion.Ravine:
			list.Add(11);
			list.Add(0);
			list.Add(1);
			list.Add(-1);
			list.Add(-3);
			list.Add(2);
			break;
		}
		return list;
	}

	public static string GetRegionString(int Region)
	{
		return (Shared.GameRegion)Region switch
		{
			Shared.GameRegion.MysteryLake => "Mystery Lake", 
			Shared.GameRegion.CoastalHighWay => "Coastal Highway", 
			Shared.GameRegion.DesolationPoint => "Desolation Point", 
			Shared.GameRegion.PlesantValley => "Plesant Valley", 
			Shared.GameRegion.TimberwolfMountain => "Timberwolf Mountain", 
			Shared.GameRegion.ForlornMuskeg => "Forlorn Muskeg", 
			Shared.GameRegion.MountainTown => "Mountain Town", 
			Shared.GameRegion.BrokenRailroad => "Broken Railroad", 
			Shared.GameRegion.HushedRiverValley => "Hushed River Valley", 
			Shared.GameRegion.BleakInlet => "Bleak Inlet", 
			Shared.GameRegion.AshCanyon => "Ash Canyon", 
			Shared.GameRegion.Blackrock => "Blackrock", 
			Shared.GameRegion.CrumblingHighWay => "Crumbling Highway", 
			Shared.GameRegion.Ravine => "Ravine", 
			Shared.GameRegion.WindingRiver => "Winding River", 
			Shared.GameRegion.KeepersPassSouth => "Keepers Pass South", 
			Shared.GameRegion.KeepersPassNorth => "Keepers Pass North", 
			Shared.GameRegion.ForsakenAirfield => "Forsaken Airfield", 
			Shared.GameRegion.TransferPass => "Transfer Pass", 
			Shared.GameRegion.ZoneOfContamination => "Zone of Contamination", 
			Shared.GameRegion.SunderedPass => "Sundered Pass", 
			Shared.GameRegion.FarRangeBranchLine => "Far Range Branch Line", 
			_ => "Unknown " + Region, 
		};
	}

	public static string GetExpeditionJsonByAlias(string Alias)
	{
		if (Directory.Exists(MPSaveManager.GetBaseDirectory() + "Mods"))
		{
			if (Directory.Exists(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates"))
			{
				byte[] bytes = File.ReadAllBytes(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates" + MPSaveManager.GetSeparator() + Alias + ".json");
				string text = Encoding.UTF8.GetString(bytes);
				if (string.IsNullOrEmpty(text))
				{
					return "";
				}
				return MPSaveManager.VectorsFixUp(text);
			}
			Directory.CreateDirectory(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates");
		}
		else
		{
			Directory.CreateDirectory(MPSaveManager.GetBaseDirectory() + "Mods");
			Directory.CreateDirectory(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates");
		}
		return "";
	}
}
