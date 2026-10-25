using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameServer;
using Il2Cpp;
using MelonLoader;
using TinyJSON;
using UnityEngine;

namespace SkyCoop;

public class ExpeditionManager
{
	public class SaveData
	{
		public string m_ActiveExpeditions = "";

		public string m_ActiveCrashSiteGUID = "";

		public string m_UnavailableGearSpawners = "";

		public string m_GearSpawnerGears = "";

		public int NextCrashSiteIn = 7200;

		public Dictionary<string, List<string>> m_SpecialItemsOwners = new Dictionary<string, List<string>>();
	}

	public enum ExpeditionTaskType
	{
		ENTERSCENE,
		ENTERZONE,
		COLLECT,
		FLAREGUNSHOT,
		CHARCOAL,
		STAYINZONE,
		CRASHSITE,
		AUTOCOMPLETE,
		INTERACT
	}

	public enum ExpeditionInteractiveImpact
	{
		EVERY,
		ONEOF,
		NONE
	}

	public enum ExpeditionCompleteOrder
	{
		LINEAL,
		LINEALHIDDEN,
		LINEALLAST,
		ANYORDER
	}

	public class ExpeditionInvite
	{
		public string m_InviterMAC = "";

		public string m_PersonToInviteMAC = "";

		public string m_InviterName = "";

		public int m_Timeout = 60;
	}

	public class SpecialExpeditionItem
	{
		public string m_GearReferenceName = "";

		public string m_GearName = "";

		public string m_GearDescription = "";

		public string m_ExpeditionAlias = "";

		public string m_ModelPrefab = "";

		public string m_GearIcon = "";

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Quaternion m_Rotation = new Quaternion(0f, 0f, 0f, 0f);

		public string m_Scene = "";
	}

	public class Expedition
	{
		public string m_Name = "";

		public string m_Alias = "";

		public List<ExpeditionTask> m_Tasks = new List<ExpeditionTask>();

		public int m_RegionBelong;

		public List<string> m_Players = new List<string>();

		public string m_GUID = "";

		public bool m_Completed;

		public int m_TasksCompleted;

		public int m_TimeLeft = 7200;

		public List<DataStr.MultiPlayerClientData> GetExpeditionPlayersData(bool Everyone = false)
		{
			List<DataStr.MultiPlayerClientData> list = new List<DataStr.MultiPlayerClientData>();
			if (Everyone)
			{
				foreach (int expeditionPlayersID in GetExpeditionPlayersIDs(Everyone))
				{
					if (expeditionPlayersID != -1 && expeditionPlayersID != 0)
					{
						if (MyMod.playersData[expeditionPlayersID] != null)
						{
							list.Add(MyMod.playersData[expeditionPlayersID]);
						}
					}
					else if (expeditionPlayersID == 0)
					{
						DataStr.MultiPlayerClientData multiPlayerClientData = new DataStr.MultiPlayerClientData();
						multiPlayerClientData.m_LevelGuid = MyMod.level_guid;
						multiPlayerClientData.m_Position = GameManager.GetPlayerTransform().position;
						list.Add(multiPlayerClientData);
					}
				}
				return list;
			}
			foreach (string player in m_Players)
			{
				int iDByMAC = Server.GetIDByMAC(player);
				if (iDByMAC != -1 && iDByMAC != 0)
				{
					if (MyMod.playersData[iDByMAC] != null)
					{
						list.Add(MyMod.playersData[iDByMAC]);
					}
				}
				else if (iDByMAC == 0)
				{
					DataStr.MultiPlayerClientData multiPlayerClientData2 = new DataStr.MultiPlayerClientData();
					multiPlayerClientData2.m_LevelGuid = MyMod.level_guid;
					multiPlayerClientData2.m_Position = GameManager.GetPlayerTransform().position;
					list.Add(multiPlayerClientData2);
				}
			}
			return list;
		}

		public List<int> GetExpeditionPlayersIDs(bool Everyone = false)
		{
			List<int> list = new List<int>();
			if (!IsCrashSite())
			{
				foreach (string player in m_Players)
				{
					int iDByMAC = Server.GetIDByMAC(player);
					if (iDByMAC != -1 && MyMod.playersData[iDByMAC] != null)
					{
						list.Add(iDByMAC);
					}
				}
			}
			else if (Everyone)
			{
				for (int i = 0; i < MyMod.playersData.Count; i++)
				{
					if (MyMod.playersData[i] != null)
					{
						list.Add(i);
					}
				}
			}
			else
			{
				for (int j = 0; j < MyMod.playersData.Count; j++)
				{
					if (MyMod.playersData[j] != null && !PlayerInExpedition(j))
					{
						list.Add(j);
					}
				}
			}
			return list;
		}

		public void OnTaskCompletedNotification()
		{
			foreach (int expeditionPlayersID in GetExpeditionPlayersIDs())
			{
				if (expeditionPlayersID == 0)
				{
					MyMod.DoExpeditionState(3);
				}
				else
				{
					ServerSend.EXPEDITIONRESULT(expeditionPlayersID, 3);
				}
			}
		}

		public void OnTaskCompleted(ExpeditionTask TaskCompleted)
		{
			if (TaskCompleted.m_TimeAdd)
			{
				m_TimeLeft += TaskCompleted.m_Time;
			}
			else
			{
				m_TimeLeft = TaskCompleted.m_Time;
			}
		}

		public bool IsCrashSite()
		{
			if (m_TimeLeft <= 0 && m_Tasks.Count > 0)
			{
				if (m_Tasks[m_Tasks.Count - 1].m_Type == ExpeditionTaskType.CRASHSITE)
				{
					return true;
				}
				return false;
			}
			if (!string.IsNullOrEmpty(m_ActiveCrashSiteGUID) && m_ActiveCrashSiteGUID == m_GUID)
			{
				return true;
			}
			return false;
		}

		public void UpdateTasks()
		{
			int count = m_Tasks.Count;
			int num = 0;
			m_TimeLeft--;
			if (m_TimeLeft <= 0)
			{
				if (m_Tasks.Count > 0)
				{
					_ = m_Tasks[m_Tasks.Count - 1];
					if (IsCrashSite())
					{
						CompleteCrashsite(-2, m_GUID);
					}
					else
					{
						CompleteExpedition(m_GUID, 0);
					}
				}
				else
				{
					CompleteExpedition(m_GUID, 0);
				}
				return;
			}
			List<DataStr.MultiPlayerClientData> expeditionPlayersData = GetExpeditionPlayersData(IsCrashSite());
			ExpeditionCompleteOrder expeditionCompleteOrder = ExpeditionCompleteOrder.LINEAL;
			bool flag = true;
			bool flag2 = false;
			string text = "";
			bool flag3 = false;
			bool flag4 = true;
			for (int i = 0; i < m_Tasks.Count; i++)
			{
				ExpeditionTask expeditionTask = m_Tasks[i];
				ExpeditionTask expeditionTask2 = null;
				bool flag5 = false;
				if (i + 1 < m_Tasks.Count)
				{
					expeditionTask2 = m_Tasks[i + 1];
				}
				if (expeditionTask.m_CompleteOrder == ExpeditionCompleteOrder.ANYORDER && expeditionTask2 != null && expeditionTask2.m_CompleteOrder != ExpeditionCompleteOrder.ANYORDER)
				{
					flag5 = true;
				}
				if (flag4 && !expeditionTask.m_IsComplete)
				{
					flag4 = false;
				}
				if (!flag2)
				{
					if (expeditionTask.m_IsComplete)
					{
						num++;
					}
					else
					{
						expeditionTask.Update(expeditionPlayersData);
						if (flag)
						{
							expeditionCompleteOrder = expeditionTask.m_CompleteOrder;
							flag = false;
						}
					}
					if (((expeditionCompleteOrder == ExpeditionCompleteOrder.LINEAL || expeditionCompleteOrder == ExpeditionCompleteOrder.LINEALHIDDEN || expeditionCompleteOrder == ExpeditionCompleteOrder.LINEALLAST) && !expeditionTask.m_IsComplete) || (flag5 && !flag4))
					{
						flag2 = true;
					}
				}
				string text2 = "[FFFFFF]";
				string text3 = "[707070]";
				string text4 = "[-]";
				string text5 = "";
				if (expeditionTask.m_Type == ExpeditionTaskType.STAYINZONE && !expeditionTask.m_IsComplete && expeditionTask.m_SecondsInZone > 0)
				{
					int completeProcent = expeditionTask.GetCompleteProcent();
					text5 = "Progress: [";
					int num2 = 18;
					int num3 = num2 * completeProcent / 100;
					if (num3 > 0)
					{
						text5 += "[00FF00]";
					}
					for (int j = 1; j <= num2; j++)
					{
						text5 += "■";
						if (num3 == j)
						{
							text5 += text4;
						}
					}
					string text6 = text5;
					int num4 = completeProcent;
					text5 = text6 + "] " + num4 + "%";
					text5 = "\n" + text5;
				}
				switch (expeditionCompleteOrder)
				{
				case ExpeditionCompleteOrder.LINEALLAST:
					if (!expeditionTask.m_IsComplete && !flag3)
					{
						text = text2 + expeditionTask.m_Text + text4 + text5;
						flag3 = true;
					}
					break;
				case ExpeditionCompleteOrder.LINEAL:
				case ExpeditionCompleteOrder.LINEALHIDDEN:
				case ExpeditionCompleteOrder.ANYORDER:
					if (m_Tasks.Count > 1)
					{
						if (!flag3)
						{
							string text7 = text2 + "- ";
							if (expeditionTask.m_IsComplete)
							{
								text7 = text3 + "- ";
							}
							text7 = ((!string.IsNullOrEmpty(text)) ? ("\n" + text7 + expeditionTask.m_Text + text4 + text5) : (text7 + expeditionTask.m_Text + text4 + text5));
							text += text7;
						}
					}
					else
					{
						text = text2 + "- " + expeditionTask.m_Text + text4 + text5;
					}
					break;
				}
				if (!expeditionTask.m_IsComplete && expeditionCompleteOrder == ExpeditionCompleteOrder.LINEALHIDDEN)
				{
					flag3 = true;
				}
				if (flag5 && !flag4 && expeditionTask2 != null && (expeditionTask2.m_CompleteOrder == ExpeditionCompleteOrder.LINEALHIDDEN || expeditionTask2.m_CompleteOrder == ExpeditionCompleteOrder.LINEALLAST))
				{
					flag3 = true;
				}
			}
			if (m_TasksCompleted < num)
			{
				m_TasksCompleted = num;
				if (count > num)
				{
					OnTaskCompletedNotification();
				}
			}
			foreach (int expeditionPlayersID in GetExpeditionPlayersIDs())
			{
				if (expeditionPlayersID == 0)
				{
					MyMod.OnExpedition = true;
					MyMod.ExpeditionLastName = m_Name;
					MyMod.ExpeditionLastTaskText = text;
					MyMod.ExpeditionLastTime = m_TimeLeft;
					MyMod.LastExpeditionAlias = m_Alias;
				}
				else
				{
					ServerSend.EXPEDITIONSYNC(expeditionPlayersID, m_Name, text, m_TimeLeft, m_Alias);
				}
			}
			if (count == num)
			{
				m_Completed = true;
			}
		}
	}

	public class ExpeditionTask
	{
		public ExpeditionTaskType m_Type;

		public ExpeditionCompleteOrder m_CompleteOrder;

		public string m_Text = "";

		public string m_Scene = "";

		public string m_Alias = "";

		public Vector3 m_ZoneCenter = new Vector3(0f, 0f, 0f);

		public float m_ZoneRadius;

		public List<string> m_SpecificContrainers = new List<string>();

		public List<ExpeditionBuilder.ExpeditionGearSpawner> m_GearSpawners = new List<ExpeditionBuilder.ExpeditionGearSpawner>();

		public List<string> m_SpecificPlants = new List<string>();

		public List<string> m_SpecificBreakdowns = new List<string>();

		public List<DataStr.UniversalSyncableObjectSpawner> m_ObjectSpawners = new List<DataStr.UniversalSyncableObjectSpawner>();

		public bool m_ObjectsSpawned;

		public bool m_RestockSceneContainers;

		public bool m_IsComplete;

		public string m_ObjectiveGearSpawnerGUID = "";

		public bool m_ObjectiveGearSpawned;

		public bool m_Debug;

		public bool m_RewardAlreadySpawned;

		public string m_ExpeditionGUID;

		public int m_LastPlayersAmout = 1;

		public bool m_CanCheckFlaregun;

		public bool m_DidFlareShots;

		public bool m_CanCheckCharcoal;

		public int m_Time = 3600;

		public bool m_TimeAdd = true;

		public int m_SecondsInZone;

		public int m_StayInZoneSeconds = 300;

		public Dictionary<string, bool> m_RequiredInteractionsEvery = new Dictionary<string, bool>();

		public List<string> m_RequiredInteractionsAny = new List<string>();

		public bool m_RequiredInteractionsAnyDone = true;

		public bool m_RequiredInteractionsEveryDone = true;

		public void StartDespawnOfAllObjects()
		{
			if (!m_ObjectsSpawned)
			{
				return;
			}
			foreach (DataStr.UniversalSyncableObjectSpawner objectSpawner in m_ObjectSpawners)
			{
				MPSaveManager.AddUniversalSyncableObject(new DataStr.UniversalSyncableObject
				{
					m_Prefab = objectSpawner.m_Prefab,
					m_GUID = objectSpawner.m_GUID,
					m_Position = objectSpawner.m_Position,
					m_Rotation = objectSpawner.m_Rotation,
					m_ExpeditionBelong = m_ExpeditionGUID,
					m_Scene = m_Scene,
					m_CreationTime = MyMod.MinutesFromStartServer,
					m_RemoveTime = MyMod.MinutesFromStartServer + 1440,
					m_ObjectGroup = objectSpawner.m_ObjectGroup
				});
			}
		}

		public void RemoveAllObjects()
		{
			if (!m_ObjectsSpawned)
			{
				return;
			}
			foreach (DataStr.UniversalSyncableObjectSpawner objectSpawner in m_ObjectSpawners)
			{
				MPSaveManager.RemoveUniversalSyncableObject(m_Scene, objectSpawner.m_GUID);
				if (MyMod.level_guid == m_Scene)
				{
					MyMod.RemoveObjectByGUID(objectSpawner.m_GUID);
				}
			}
		}

		public void SpawnObjects()
		{
			if (!m_ObjectsSpawned)
			{
				DebugLog("Task " + m_Alias + " SpawnObjects");
				SpawnObjectsFromSpawners(m_ObjectSpawners, m_ExpeditionGUID, m_Scene, AddContainersLoot: true, this);
			}
			m_ObjectsSpawned = true;
		}

		public void SpawnReward()
		{
			if (m_RewardAlreadySpawned)
			{
				return;
			}
			DebugLog("Task " + m_Alias + " SpawnReward");
			m_RewardAlreadySpawned = true;
			if (m_RestockSceneContainers)
			{
				MPSaveManager.AddLootToScene(m_Scene);
			}
			if (m_SpecificContrainers.Count > 0)
			{
				foreach (string specificContrainer in m_SpecificContrainers)
				{
					MPSaveManager.AddLootToContainerOnScene(specificContrainer, m_Scene);
				}
			}
			if (m_GearSpawners.Count > 0)
			{
				foreach (ExpeditionBuilder.ExpeditionGearSpawner gearSpawner in m_GearSpawners)
				{
					CreateRewardGear(gearSpawner, m_Scene, m_LastPlayersAmout, m_Debug);
				}
			}
			if (!string.IsNullOrEmpty(m_ObjectiveGearSpawnerGUID) && m_UnavailableGearSpawners.ContainsKey(m_ObjectiveGearSpawnerGUID))
			{
				m_ObjectiveGearSpawned = true;
				DebugLog("Objective Gear Spawned!");
			}
			if (m_SpecificPlants.Count > 0)
			{
				MPSaveManager.ForceGrowPlants(m_Scene, m_SpecificPlants);
			}
			if (m_SpecificBreakdowns.Count > 0)
			{
				MPSaveManager.RepairBreakdowns(m_Scene, m_SpecificBreakdowns);
			}
		}

		public int GetCompleteProcent()
		{
			return 100 * m_SecondsInZone / m_StayInZoneSeconds;
		}

		public void OnCompleted()
		{
			if (m_IsComplete)
			{
				return;
			}
			DebugLog("Task " + m_Alias + " OnCompleted");
			m_IsComplete = true;
			if (m_Type != ExpeditionTaskType.CRASHSITE)
			{
				foreach (Expedition activeExpedition in m_ActiveExpeditions)
				{
					if (activeExpedition.m_GUID == m_ExpeditionGUID)
					{
						activeExpedition.OnTaskCompleted(this);
						break;
					}
				}
			}
			SpawnReward();
			StartDespawnOfAllObjects();
		}

		public void CheckInteractiveDone(string GUID)
		{
			if (m_IsComplete)
			{
				return;
			}
			bool flag = false;
			bool flag2 = false;
			if (!m_RequiredInteractionsAnyDone && m_RequiredInteractionsAny.Count > 0)
			{
				for (int i = 0; i < m_RequiredInteractionsAny.Count; i++)
				{
					if (m_RequiredInteractionsAny[i] == GUID)
					{
						m_RequiredInteractionsAnyDone = true;
						flag2 = true;
						break;
					}
				}
			}
			if (!m_RequiredInteractionsEveryDone && m_RequiredInteractionsEvery.Count > 0)
			{
				if (m_RequiredInteractionsEvery.ContainsKey(GUID))
				{
					m_RequiredInteractionsEvery.Remove(GUID);
					m_RequiredInteractionsEvery.Add(GUID, value: true);
					flag = true;
				}
				using Dictionary<string, bool>.Enumerator enumerator = m_RequiredInteractionsEvery.GetEnumerator();
				while (enumerator.MoveNext() && enumerator.Current.Value)
				{
					m_RequiredInteractionsEveryDone = true;
				}
			}
			if (flag2)
			{
				for (int j = 0; j < m_RequiredInteractionsAny.Count; j++)
				{
					MPSaveManager.RemoveUniversalSyncableObject(m_Scene, m_RequiredInteractionsAny[j]);
					if (MyMod.level_guid == m_Scene)
					{
						MyMod.RemoveObjectByGUID(m_RequiredInteractionsAny[j]);
					}
				}
			}
			if (flag)
			{
				MPSaveManager.RemoveUniversalSyncableObject(m_Scene, GUID);
				if (MyMod.level_guid == m_Scene)
				{
					MyMod.RemoveObjectByGUID(GUID);
				}
			}
		}

		public void CheckFlaregunShot(string Scene, Vector3 Position)
		{
			if (!m_IsComplete && m_CanCheckFlaregun && m_Type == ExpeditionTaskType.FLAREGUNSHOT && Scene == m_Scene && Vector3.Distance(Position, m_ZoneCenter) <= m_ZoneRadius)
			{
				OnCompleted();
			}
		}

		public void CheckCharcoalSpot(string Scene, Vector3 Position)
		{
			if (!m_IsComplete && m_CanCheckCharcoal && m_Type == ExpeditionTaskType.CHARCOAL && Scene == m_Scene && Vector3.Distance(Position, m_ZoneCenter) <= m_ZoneRadius)
			{
				OnCompleted();
			}
		}

		public List<int> GetCrashSiteNearPlayers()
		{
			List<int> list = new List<int>();
			if (MyMod.level_guid == m_Scene && Vector3.Distance(GameManager.GetPlayerTransform().position, m_ZoneCenter) <= m_ZoneRadius * 2f)
			{
				list.Add(0);
			}
			for (int i = 0; i < MyMod.playersData.Count; i++)
			{
				if (MyMod.playersData[i] != null)
				{
					DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[i];
					if (multiPlayerClientData.m_LevelGuid == m_Scene && Vector3.Distance(multiPlayerClientData.m_Position, m_ZoneCenter) <= m_ZoneRadius * 2f)
					{
						list.Add(i);
					}
				}
			}
			return list;
		}

		public void Update(List<DataStr.MultiPlayerClientData> PlayersData)
		{
			m_LastPlayersAmout = PlayersData.Count;
			if (!m_ObjectsSpawned)
			{
				SpawnObjects();
			}
			if (!m_DidFlareShots)
			{
				DoFlareShots(m_GearSpawners, m_Scene);
				m_DidFlareShots = true;
			}
			if (m_Type == ExpeditionTaskType.FLAREGUNSHOT)
			{
				m_CanCheckFlaregun = true;
			}
			if (m_Type == ExpeditionTaskType.CHARCOAL)
			{
				m_CanCheckCharcoal = true;
			}
			if (m_Type == ExpeditionTaskType.COLLECT)
			{
				if (m_ObjectiveGearSpawned)
				{
					if (!m_UnavailableGearSpawners.ContainsKey(m_ObjectiveGearSpawnerGUID))
					{
						OnCompleted();
					}
					return;
				}
				{
					foreach (DataStr.MultiPlayerClientData PlayersDatum in PlayersData)
					{
						if (PlayersDatum.m_LevelGuid == m_Scene && Vector3.Distance(PlayersDatum.m_Position, m_ZoneCenter) <= m_ZoneRadius)
						{
							SpawnReward();
							break;
						}
					}
					return;
				}
			}
			if (m_Type == ExpeditionTaskType.STAYINZONE)
			{
				foreach (DataStr.MultiPlayerClientData PlayersDatum2 in PlayersData)
				{
					if (PlayersDatum2.m_LevelGuid == m_Scene && Vector3.Distance(PlayersDatum2.m_Position, m_ZoneCenter) <= m_ZoneRadius)
					{
						m_SecondsInZone++;
					}
				}
				if (m_SecondsInZone >= m_StayInZoneSeconds)
				{
					OnCompleted();
				}
				return;
			}
			if (m_Type == ExpeditionTaskType.ENTERSCENE)
			{
				foreach (DataStr.MultiPlayerClientData PlayersDatum3 in PlayersData)
				{
					if (PlayersDatum3.m_LevelGuid == m_Scene)
					{
						OnCompleted();
						break;
					}
				}
				return;
			}
			if (m_Type == ExpeditionTaskType.ENTERZONE)
			{
				foreach (DataStr.MultiPlayerClientData PlayersDatum4 in PlayersData)
				{
					if (PlayersDatum4.m_LevelGuid == m_Scene && Vector3.Distance(PlayersDatum4.m_Position, m_ZoneCenter) <= m_ZoneRadius)
					{
						OnCompleted();
						break;
					}
				}
				return;
			}
			if (m_Type == ExpeditionTaskType.CRASHSITE)
			{
				foreach (DataStr.MultiPlayerClientData PlayersDatum5 in PlayersData)
				{
					if (PlayersDatum5.m_LevelGuid == m_Scene && Vector3.Distance(PlayersDatum5.m_Position, m_ZoneCenter) <= m_ZoneRadius)
					{
						OnCompleted();
						break;
					}
				}
				return;
			}
			if (m_Type == ExpeditionTaskType.AUTOCOMPLETE)
			{
				m_SecondsInZone++;
				if (m_SecondsInZone >= m_StayInZoneSeconds)
				{
					OnCompleted();
				}
			}
			else if (m_Type == ExpeditionTaskType.INTERACT && !m_IsComplete && m_ObjectsSpawned && m_RequiredInteractionsEveryDone && m_RequiredInteractionsAnyDone)
			{
				OnCompleted();
			}
		}
	}

	public static List<Expedition> m_ActiveExpeditions = new List<Expedition>();

	public static string m_ActiveCrashSiteGUID = "";

	public static Dictionary<string, int> m_UnavailableGearSpawners = new Dictionary<string, int>();

	public static Dictionary<int, string> m_GearSpawnerGears = new Dictionary<int, string>();

	public static List<ExpeditionInvite> m_Invites = new List<ExpeditionInvite>();

	public static int NextCrashSiteIn = 7200;

	public static bool Debug = true;

	public static List<SpecialExpeditionItem> m_SpecialItems = new List<SpecialExpeditionItem>();

	public static Dictionary<string, List<string>> m_SpecialItemsOwners = new Dictionary<string, List<string>>();

	public static SpecialExpeditionItem GetSpecialItem(string ReferenceName)
	{
		for (int i = 0; i < m_SpecialItems.Count; i++)
		{
			SpecialExpeditionItem specialExpeditionItem = m_SpecialItems[i];
			if (specialExpeditionItem.m_GearReferenceName == ReferenceName)
			{
				return specialExpeditionItem;
			}
		}
		return null;
	}

	public static void AddSpecialItem(string JSONString)
	{
		JSONString = MPSaveManager.VectorsFixUp(JSONString);
		AddSpecialItem(JSON.Load(JSONString).Make<SpecialExpeditionItem>());
	}

	public static void AddSpecialItem(SpecialExpeditionItem NewItem)
	{
		if (GetSpecialItem(NewItem.m_GearReferenceName) == null)
		{
			m_SpecialItems.Add(NewItem);
		}
		Log("Registered Special Expedition Item " + NewItem.m_GearReferenceName);
	}

	public static void InitClues()
	{
		m_SpecialItems.Clear();
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetBaseDirectory() + "Mods");
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates");
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates" + MPSaveManager.GetSeparator() + "SpecialGears");
		FileInfo[] files = new DirectoryInfo(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "ExpeditionTemplates" + MPSaveManager.GetSeparator() + "SpecialGears").GetFiles("*.json");
		for (int i = 0; i < files.Length; i++)
		{
			byte[] bytes = File.ReadAllBytes(files[i].FullName);
			string text = Encoding.UTF8.GetString(bytes);
			if (!string.IsNullOrEmpty(text))
			{
				AddSpecialItem(text);
			}
		}
	}

	public static bool IsClueGear(string GearName)
	{
		string text = GearName.ToLower();
		foreach (SpecialExpeditionItem specialItem in m_SpecialItems)
		{
			if (text == specialItem.m_GearReferenceName.ToLower())
			{
				return true;
			}
		}
		return false;
	}

	public static string Save()
	{
		SaveData saveData = new SaveData();
		saveData.m_ActiveExpeditions = JSON.Dump(m_ActiveExpeditions);
		if (!string.IsNullOrEmpty(m_ActiveCrashSiteGUID))
		{
			saveData.m_ActiveCrashSiteGUID = m_ActiveCrashSiteGUID;
		}
		saveData.m_UnavailableGearSpawners = JSON.Dump(m_UnavailableGearSpawners);
		saveData.m_GearSpawnerGears = JSON.Dump(m_GearSpawnerGears);
		saveData.NextCrashSiteIn = NextCrashSiteIn;
		saveData.m_SpecialItemsOwners = m_SpecialItemsOwners;
		return JSON.Dump(saveData);
	}

	public static void Load(string JSONString)
	{
		ApplyWorldEdits();
		if (string.IsNullOrEmpty(JSONString))
		{
			return;
		}
		DebugLog("[ExpeditionManager] Loading SaveData...");
		JSONString = MPSaveManager.VectorsFixUp(JSONString);
		SaveData saveData = JSON.Load(JSONString).Make<SaveData>();
		if (saveData != null && !string.IsNullOrEmpty(saveData.m_ActiveExpeditions))
		{
			m_ActiveExpeditions = JSON.Load(saveData.m_ActiveExpeditions).Make<List<Expedition>>();
			foreach (Expedition activeExpedition in m_ActiveExpeditions)
			{
				DebugLog("[ExpeditionManager] " + activeExpedition.m_Alias);
				DebugLog("[ExpeditionManager] GUID " + activeExpedition.m_GUID);
				DebugLog("[ExpeditionManager] m_TimeLeft " + activeExpedition.m_TimeLeft);
				DebugLog("[ExpeditionManager] m_Players: ");
				foreach (string player in activeExpedition.m_Players)
				{
					DebugLog("[ExpeditionManager] Player " + player);
				}
				DebugLog("[ExpeditionManager] m_Tasks:");
				foreach (ExpeditionTask task in activeExpedition.m_Tasks)
				{
					DebugLog("[ExpeditionManager] Task " + task.m_Alias);
					DebugLog("[ExpeditionManager] m_ExpeditionGUID " + task.m_ExpeditionGUID);
					DebugLog("[ExpeditionManager] m_IsComplete " + task.m_IsComplete);
				}
			}
		}
		if (!string.IsNullOrEmpty(saveData.m_ActiveCrashSiteGUID))
		{
			m_ActiveCrashSiteGUID = saveData.m_ActiveCrashSiteGUID;
			foreach (Expedition activeExpedition2 in m_ActiveExpeditions)
			{
				if (activeExpedition2.m_GUID == m_ActiveCrashSiteGUID)
				{
					DebugLog("[ExpeditionManager] m_ActiveCrashSite " + activeExpedition2.m_Alias);
				}
			}
		}
		if (!string.IsNullOrEmpty(saveData.m_UnavailableGearSpawners))
		{
			m_UnavailableGearSpawners = JSON.Load(saveData.m_UnavailableGearSpawners).Make<Dictionary<string, int>>();
		}
		if (!string.IsNullOrEmpty(saveData.m_GearSpawnerGears))
		{
			m_GearSpawnerGears = JSON.Load(saveData.m_GearSpawnerGears).Make<Dictionary<int, string>>();
		}
		if (saveData.m_SpecialItemsOwners != null)
		{
			m_SpecialItemsOwners = saveData.m_SpecialItemsOwners;
		}
		NextCrashSiteIn = saveData.NextCrashSiteIn;
	}

	public static void Log(string LOG)
	{
		MelonLogger.Msg(ConsoleColor.Blue, "[ExpeditionManager] " + LOG);
	}

	public static void DebugLog(string LOG)
	{
		if (Debug)
		{
			MelonLogger.Msg(ConsoleColor.Blue, "[ExpeditionManager] " + LOG);
		}
	}

	public static Expedition GetExpeditionByGUID(string GUID)
	{
		foreach (Expedition activeExpedition in m_ActiveExpeditions)
		{
			if (activeExpedition.m_GUID == GUID)
			{
				return activeExpedition;
			}
		}
		return null;
	}

	public static bool PlayerInExpedition(string MAC)
	{
		if (string.IsNullOrEmpty(MAC))
		{
			return false;
		}
		for (int i = 0; i < m_ActiveExpeditions.Count; i++)
		{
			if (m_ActiveExpeditions[i].m_Players.Contains(MAC))
			{
				return true;
			}
		}
		return false;
	}

	public static bool PlayerInExpedition(int PlayerID)
	{
		return PlayerInExpedition(Server.GetMACByID(PlayerID));
	}

	public static bool StartNewExpedition(string LeaderMAC, int Region, string Alias = "", bool NoMessage = false, bool Special = false)
	{
		DebugLog("Client with MAC " + LeaderMAC + " trying start expedition on region " + Region);
		int iDByMAC = Server.GetIDByMAC(LeaderMAC);
		for (int i = 0; i < m_ActiveExpeditions.Count; i++)
		{
			if (m_ActiveExpeditions[i].m_Players.Contains(LeaderMAC))
			{
				if (!NoMessage)
				{
					ServerSend.ADDHUDMESSAGE(iDByMAC, "You are already in expedition!");
				}
				return false;
			}
		}
		if (Region == 6)
		{
			if (!NoMessage)
			{
				ServerSend.ADDHUDMESSAGE(iDByMAC, "You can't start expedition here, try to move to another region.");
			}
			return false;
		}
		Expedition expedition = ExpeditionBuilder.BuildBasicExpedition(Region, Alias, DebugFlag: false, NoMulty: true);
		if (expedition == null)
		{
			if (!NoMessage)
			{
				if (!Special)
				{
					ServerSend.ADDHUDMESSAGE(iDByMAC, "No available expeditions, try to move to another region.");
				}
				else
				{
					ServerSend.ADDHUDMESSAGE(iDByMAC, "You can't start this expedition right now, please try later.");
				}
			}
			return false;
		}
		DebugLog("Expedition created");
		DebugLog("Expedition Tasks:");
		for (int j = 0; j < expedition.m_Tasks.Count; j++)
		{
			DebugLog("Expedition Task[" + j + "] " + expedition.m_Tasks[j].m_Alias + " Type " + expedition.m_Tasks[j].m_Type);
		}
		expedition.m_Players.Add(LeaderMAC);
		m_ActiveExpeditions.Add(expedition);
		if (iDByMAC != 0)
		{
			ServerSend.EXPEDITIONRESULT(iDByMAC, 2);
		}
		else
		{
			MyMod.DoExpeditionState(2);
		}
		return true;
	}

	public static Expedition GetActiveCrashSite()
	{
		if (string.IsNullOrEmpty(m_ActiveCrashSiteGUID))
		{
			return null;
		}
		foreach (Expedition activeExpedition in m_ActiveExpeditions)
		{
			if (activeExpedition.m_GUID == m_ActiveCrashSiteGUID)
			{
				return activeExpedition;
			}
		}
		return null;
	}

	public static void MayNotifyAboutCrashSite(int ClientID)
	{
		if (!string.IsNullOrEmpty(m_ActiveCrashSiteGUID))
		{
			ServerSend.EXPEDITIONRESULT(ClientID, 5);
		}
	}

	public static void StartCrashSite(int CrashSiteID = -1)
	{
		if (GetActiveCrashSite() != null)
		{
			return;
		}
		string randomCrashSiteName = ExpeditionBuilder.GetRandomCrashSiteName(CrashSiteID);
		if (string.IsNullOrEmpty(randomCrashSiteName))
		{
			if (CrashSiteID == -1)
			{
				DebugLog("Wasn't able to find any valid crashsites!");
			}
			else
			{
				DebugLog("Wasn't able to find crashsites with index " + CrashSiteID);
			}
			return;
		}
		DebugLog("CrashSite " + randomCrashSiteName + " going to be loaded!");
		Expedition expedition = ExpeditionBuilder.BuildBasicExpedition(0, randomCrashSiteName);
		if (expedition != null)
		{
			DebugLog("CrashSite created");
			DebugLog("CrashSite Tasks:");
			for (int i = 0; i < expedition.m_Tasks.Count; i++)
			{
				DebugLog("CrashSite Task[" + i + "] " + expedition.m_Tasks[i].m_Alias + " Type " + expedition.m_Tasks[i].m_Type);
			}
			for (int j = 0; j < MyMod.playersData.Count; j++)
			{
				ServerSend.EXPEDITIONRESULT(j, 5);
			}
			m_ActiveExpeditions.Add(expedition);
			m_ActiveCrashSiteGUID = expedition.m_GUID;
			DataStr.MultiplayerChatMessage obj = new DataStr.MultiplayerChatMessage
			{
				m_Message = "Plane with a valuable cargo crashed somewhere on " + ExpeditionBuilder.GetRegionString(expedition.m_RegionBelong) + ", find the crash site before other players do.",
				m_Type = 0,
				m_By = "[Server]"
			};
			Shared.SendMessageToChat(obj);
			Shared.WebhookCrashSiteSpawn(obj.m_Message);
		}
	}

	public static void AcceptInvite(string Accepter, string Inviter)
	{
		int iDByMAC = Server.GetIDByMAC(Accepter);
		string playerName = "";
		if (iDByMAC == 0)
		{
			playerName = MyMod.MyChatName;
		}
		if (iDByMAC != 0 && iDByMAC != -1 && MyMod.playersData[iDByMAC] != null)
		{
			playerName = MyMod.playersData[iDByMAC].m_Name;
		}
		for (int i = 0; i < m_ActiveExpeditions.Count; i++)
		{
			if (m_ActiveExpeditions[i].m_Players.Contains(Accepter))
			{
				ServerSend.ADDHUDMESSAGE(iDByMAC, "You already in expedition!");
				return;
			}
		}
		Expedition expedition = null;
		for (int j = 0; j < m_ActiveExpeditions.Count; j++)
		{
			if (m_ActiveExpeditions[j].m_Players.Contains(Inviter))
			{
				expedition = m_ActiveExpeditions[j];
				break;
			}
		}
		if (expedition == null)
		{
			ServerSend.ADDHUDMESSAGE(iDByMAC, "This invite expired!");
		}
		for (int num = m_Invites.Count - 1; num >= 0; num--)
		{
			ExpeditionInvite expeditionInvite = m_Invites[num];
			if (expeditionInvite.m_InviterMAC == Inviter && expeditionInvite.m_PersonToInviteMAC == Accepter)
			{
				if (expedition != null)
				{
					List<int> expeditionPlayersIDs = expedition.GetExpeditionPlayersIDs();
					for (int k = 0; k < expeditionPlayersIDs.Count; k++)
					{
						if (expeditionPlayersIDs[k] == 0)
						{
							MyMod.NewPlayerInExpedition(playerName);
						}
						else
						{
							ServerSend.NEWPLAYEREXPEDITION(expeditionPlayersIDs[k], playerName);
						}
					}
					expedition.m_Players.Add(Accepter);
					if (iDByMAC != 0)
					{
						ServerSend.EXPEDITIONRESULT(iDByMAC, 2);
					}
					else
					{
						MyMod.DoExpeditionState(2);
					}
				}
				m_Invites.RemoveAt(num);
				break;
			}
		}
	}

	public static List<ExpeditionInvite> GetInviteForClient(string MAC)
	{
		List<ExpeditionInvite> list = new List<ExpeditionInvite>();
		foreach (ExpeditionInvite invite in m_Invites)
		{
			if (invite.m_PersonToInviteMAC == MAC)
			{
				list.Add(invite);
			}
		}
		return list;
	}

	public static bool SendInvite(string Inviter, string ToInvite, int InviterID, int ToInviteID)
	{
		foreach (ExpeditionInvite invite in m_Invites)
		{
			if (invite.m_InviterMAC == Inviter && invite.m_PersonToInviteMAC == ToInvite)
			{
				return false;
			}
		}
		ExpeditionInvite expeditionInvite = new ExpeditionInvite();
		expeditionInvite.m_InviterMAC = Inviter;
		expeditionInvite.m_PersonToInviteMAC = ToInvite;
		if (InviterID == 0)
		{
			expeditionInvite.m_InviterName = MyMod.MyChatName;
		}
		if (InviterID != 0 && InviterID != -1 && MyMod.playersData[InviterID] != null)
		{
			expeditionInvite.m_InviterName = MyMod.playersData[InviterID].m_Name;
		}
		m_Invites.Add(expeditionInvite);
		if (ToInviteID == 0)
		{
			MyMod.NewExpeditionInvite(expeditionInvite.m_InviterName);
		}
		else
		{
			ServerSend.NEWEXPEDITIONINVITE(ToInviteID, expeditionInvite.m_InviterName);
		}
		return true;
	}

	public static void CreateInviteToExpedition(string LeaderMAC, string InviteMAC)
	{
		Expedition expedition = null;
		int iDByMAC = Server.GetIDByMAC(LeaderMAC);
		int iDByMAC2 = Server.GetIDByMAC(InviteMAC);
		for (int i = 0; i < m_ActiveExpeditions.Count; i++)
		{
			if (m_ActiveExpeditions[i].m_Players.Contains(LeaderMAC))
			{
				expedition = m_ActiveExpeditions[i];
			}
		}
		if (expedition != null)
		{
			bool flag = false;
			for (int j = 0; j < m_ActiveExpeditions.Count; j++)
			{
				if (m_ActiveExpeditions[j].m_Players.Contains(InviteMAC))
				{
					flag = false;
				}
			}
			if (!flag)
			{
				if (!expedition.m_Players.Contains(InviteMAC))
				{
					if (SendInvite(LeaderMAC, InviteMAC, iDByMAC, iDByMAC2))
					{
						ServerSend.ADDHUDMESSAGE(iDByMAC, "Invite sent!");
					}
					else
					{
						ServerSend.ADDHUDMESSAGE(iDByMAC, "You already invited this player to expedition");
					}
				}
				else
				{
					ServerSend.ADDHUDMESSAGE(iDByMAC, "This player already in your expedition!");
				}
			}
			else
			{
				ServerSend.ADDHUDMESSAGE(iDByMAC, "This player already in another expedition!");
			}
		}
		else
		{
			ServerSend.ADDHUDMESSAGE(iDByMAC, "You are not in expedition!");
		}
	}

	public static void CompleteCrashsite(int FinishState = -2, string GUID = "")
	{
		int num = -1;
		if (string.IsNullOrEmpty(GUID) && m_ActiveCrashSiteGUID != "")
		{
			GUID = m_ActiveCrashSiteGUID;
		}
		for (int i = 0; i < m_ActiveExpeditions.Count; i++)
		{
			if (m_ActiveExpeditions[i].m_GUID == GUID)
			{
				num = i;
				break;
			}
		}
		if (num != -1)
		{
			CompleteCrashSite(num, new List<int>(), FinishState);
		}
	}

	public static void CompleteExpedition(string GUID, int FinishState = 1)
	{
		int num = -1;
		for (int i = 0; i < m_ActiveExpeditions.Count; i++)
		{
			if (m_ActiveExpeditions[i].m_GUID == GUID)
			{
				num = i;
				break;
			}
		}
		if (num != -1)
		{
			CompleteExpedition(num, FinishState);
		}
	}

	public static void CompleteExpedition(int RemoveID, int FinishState = 1)
	{
		Expedition expedition = m_ActiveExpeditions[RemoveID];
		List<int> expeditionPlayersIDs = expedition.GetExpeditionPlayersIDs();
		if (RemoveID == -1)
		{
			return;
		}
		foreach (int item in expeditionPlayersIDs)
		{
			if (item == 0)
			{
				MyMod.DoExpeditionState(FinishState);
			}
			else
			{
				ServerSend.EXPEDITIONRESULT(item, FinishState);
			}
			if (FinishState == 1)
			{
				string mACByID = Server.GetMACByID(item);
				if (!string.IsNullOrEmpty(mACByID))
				{
					MPStats.AddExpedition(mACByID, expedition.m_Alias);
				}
			}
		}
		if (FinishState == 0)
		{
			foreach (ExpeditionTask task in m_ActiveExpeditions[RemoveID].m_Tasks)
			{
				task.RemoveAllObjects();
			}
		}
		m_ActiveExpeditions.RemoveAt(RemoveID);
	}

	public static void CompleteCrashSite(int RemoveID, List<int> ClosePlayers, int DefaultFinishState = -1)
	{
		List<int> expeditionPlayersIDs = m_ActiveExpeditions[RemoveID].GetExpeditionPlayersIDs(Everyone: true);
		if (RemoveID != -1)
		{
			foreach (int item in expeditionPlayersIDs)
			{
				int num = DefaultFinishState;
				if (ClosePlayers.Contains(item))
				{
					num = 4;
				}
				if (item == 0)
				{
					MyMod.DoExpeditionState(num);
				}
				else
				{
					ServerSend.EXPEDITIONRESULT(item, num);
				}
				if (num == 4)
				{
					string mACByID = Server.GetMACByID(item);
					if (!string.IsNullOrEmpty(mACByID))
					{
						MPStats.AddCrashSite(mACByID);
					}
				}
			}
			if (DefaultFinishState == -2)
			{
				foreach (ExpeditionTask task in m_ActiveExpeditions[RemoveID].m_Tasks)
				{
					task.RemoveAllObjects();
				}
			}
			m_ActiveExpeditions.RemoveAt(RemoveID);
			m_ActiveCrashSiteGUID = "";
		}
		DataStr.MultiplayerChatMessage multiplayerChatMessage = new DataStr.MultiplayerChatMessage();
		switch (DefaultFinishState)
		{
		case -1:
			multiplayerChatMessage.m_Message = "Crash site has been found!";
			Shared.WebhookCrashSiteFound();
			break;
		case -2:
			multiplayerChatMessage.m_Message = "Time is up, no one has found the crash site.";
			Shared.WebhookCrashSiteTimeOver();
			break;
		}
		multiplayerChatMessage.m_Type = 0;
		multiplayerChatMessage.m_By = "[Server]";
		Shared.SendMessageToChat(multiplayerChatMessage);
	}

	public static void UpdateExpeditions()
	{
		for (int num = m_ActiveExpeditions.Count - 1; num > -1; num--)
		{
			Expedition expedition = m_ActiveExpeditions[num];
			expedition.UpdateTasks();
			if (expedition.m_Completed)
			{
				if (expedition.m_Tasks.Count > 0)
				{
					ExpeditionTask expeditionTask = expedition.m_Tasks[expedition.m_Tasks.Count - 1];
					if (expeditionTask.m_Type == ExpeditionTaskType.CRASHSITE)
					{
						CompleteCrashSite(num, expeditionTask.GetCrashSiteNearPlayers());
					}
					else
					{
						CompleteExpedition(num);
					}
				}
				else
				{
					CompleteExpedition(num);
				}
			}
		}
		for (int num2 = m_Invites.Count - 1; num2 > -1; num2--)
		{
			m_Invites[num2].m_Timeout--;
			if (m_Invites[num2].m_Timeout <= 0)
			{
				m_Invites.RemoveAt(num2);
			}
		}
		if (string.IsNullOrEmpty(m_ActiveCrashSiteGUID))
		{
			NextCrashSiteIn--;
			if (NextCrashSiteIn <= 0)
			{
				int num3 = 3600;
				NextCrashSiteIn = new System.Random().Next(num3 * 3, num3 * 6);
				StartCrashSite();
			}
		}
	}

	public static void RegisterInteractionDone(string GUID)
	{
		DebugLog("[RegisterInteractionDone] " + GUID);
		for (int num = m_ActiveExpeditions.Count - 1; num > -1; num--)
		{
			for (int num2 = m_ActiveExpeditions[num].m_Tasks.Count - 1; num2 > -1; num2--)
			{
				if (m_ActiveExpeditions[num].m_Tasks[num2].m_Type == ExpeditionTaskType.INTERACT)
				{
					m_ActiveExpeditions[num].m_Tasks[num2].CheckInteractiveDone(GUID);
				}
			}
		}
	}

	public static void RegisterFlaregunShot(string Scene, Vector3 Position)
	{
		DebugLog("[RegisterFlaregunShot] " + Scene);
		for (int num = m_ActiveExpeditions.Count - 1; num > -1; num--)
		{
			for (int num2 = m_ActiveExpeditions[num].m_Tasks.Count - 1; num2 > -1; num2--)
			{
				if (m_ActiveExpeditions[num].m_Tasks[num2].m_Type == ExpeditionTaskType.FLAREGUNSHOT)
				{
					m_ActiveExpeditions[num].m_Tasks[num2].CheckFlaregunShot(Scene, Position);
				}
			}
		}
	}

	public static void RegisterCharcoalDrawing(string Scene, Vector3 Position)
	{
		DebugLog("[RegisterCharcoalDrawing] " + Scene);
		for (int num = m_ActiveExpeditions.Count - 1; num > -1; num--)
		{
			for (int num2 = m_ActiveExpeditions[num].m_Tasks.Count - 1; num2 > -1; num2--)
			{
				if (m_ActiveExpeditions[num].m_Tasks[num2].m_Type == ExpeditionTaskType.CHARCOAL)
				{
					m_ActiveExpeditions[num].m_Tasks[num2].CheckCharcoalSpot(Scene, Position);
				}
			}
		}
	}

	public static void SpawnObjectsFromSpawners(List<DataStr.UniversalSyncableObjectSpawner> m_ObjectSpawners, string m_ExpeditionGUID, string m_Scene, bool AddContainersLoot, ExpeditionTask Task = null)
	{
		if (m_ObjectSpawners.Count <= 0)
		{
			return;
		}
		foreach (DataStr.UniversalSyncableObjectSpawner m_ObjectSpawner in m_ObjectSpawners)
		{
			if (m_ObjectSpawner.m_Prefab == "Expedition3DAudioEvent" || m_ObjectSpawner.m_Prefab == "Expedition2DAudioEvent" || m_ObjectSpawner.m_Prefab == "ExpeditionAudioEvent")
			{
				if (m_ObjectSpawner.m_Prefab == "ExpeditionAudioEvent")
				{
					Expedition expeditionByGUID = GetExpeditionByGUID(m_ExpeditionGUID);
					if (expeditionByGUID == null)
					{
						continue;
					}
					foreach (int expeditionPlayersID in expeditionByGUID.GetExpeditionPlayersIDs(Everyone: true))
					{
						if (expeditionPlayersID == 0)
						{
							MyMod.PlayCustomSoundEvent(m_ObjectSpawner.m_Position, m_ObjectSpawner.m_Content, m_ObjectSpawner.m_Prefab);
						}
						else
						{
							ServerSend.CUSTOMSOUNDEVENT(m_ObjectSpawner.m_Position, m_ObjectSpawner.m_Content, m_ObjectSpawner.m_Prefab, expeditionPlayersID);
						}
					}
				}
				else
				{
					if (MyMod.level_guid == m_Scene)
					{
						MyMod.PlayCustomSoundEvent(m_ObjectSpawner.m_Position, m_ObjectSpawner.m_Content, m_ObjectSpawner.m_Prefab);
					}
					ServerSend.CUSTOMSOUNDEVENT(m_ObjectSpawner.m_Position, m_ObjectSpawner.m_Content, m_ObjectSpawner.m_Prefab, m_Scene);
				}
				continue;
			}
			if (m_ObjectSpawner.m_Prefab == "RockCache")
			{
				DataStr.FakeRockCacheVisualData fakeRockCacheVisualData = new DataStr.FakeRockCacheVisualData();
				fakeRockCacheVisualData.m_GUID = m_ObjectSpawner.m_GUID;
				fakeRockCacheVisualData.m_Position = m_ObjectSpawner.m_Position;
				fakeRockCacheVisualData.m_Rotation = m_ObjectSpawner.m_Rotation;
				fakeRockCacheVisualData.m_Owner = "Unknown";
				if (MyMod.level_guid == m_Scene)
				{
					MyMod.AddRockCache(fakeRockCacheVisualData);
				}
				MPSaveManager.AddRockCach(fakeRockCacheVisualData, 0);
				MPSaveManager.SaveContainer(m_Scene, m_ObjectSpawner.m_GUID, m_ObjectSpawner.m_Content);
				continue;
			}
			DataStr.UniversalSyncableObject universalSyncableObject = new DataStr.UniversalSyncableObject();
			universalSyncableObject.m_Prefab = m_ObjectSpawner.m_Prefab;
			universalSyncableObject.m_GUID = m_ObjectSpawner.m_GUID;
			universalSyncableObject.m_Position = m_ObjectSpawner.m_Position;
			universalSyncableObject.m_Rotation = m_ObjectSpawner.m_Rotation;
			universalSyncableObject.m_ExpeditionBelong = m_ExpeditionGUID;
			universalSyncableObject.m_Scene = m_Scene;
			universalSyncableObject.m_CreationTime = MyMod.MinutesFromStartServer;
			universalSyncableObject.m_RemoveTime = 0;
			universalSyncableObject.m_InteractiveData = m_ObjectSpawner.m_InteractiveData;
			universalSyncableObject.m_ObjectGroup = m_ObjectSpawner.m_ObjectGroup;
			if (universalSyncableObject.m_Prefab == "ExpeditionInteractive" && Task != null)
			{
				if (universalSyncableObject.m_InteractiveData.m_Impact == ExpeditionInteractiveImpact.EVERY)
				{
					Task.m_RequiredInteractionsEvery.Add(universalSyncableObject.m_GUID, value: false);
					Task.m_RequiredInteractionsEveryDone = false;
				}
				else if (universalSyncableObject.m_InteractiveData.m_Impact == ExpeditionInteractiveImpact.ONEOF)
				{
					Task.m_RequiredInteractionsAny.Add(universalSyncableObject.m_GUID);
					Task.m_RequiredInteractionsAnyDone = false;
				}
			}
			MPSaveManager.AddUniversalSyncableObject(universalSyncableObject);
			if (AddContainersLoot)
			{
				MPSaveManager.RemoveContainer(m_Scene, m_ObjectSpawner.m_GUID);
				for (int i = 1; i < 10; i++)
				{
					MPSaveManager.RemoveContainer(m_Scene, m_ObjectSpawner.m_GUID + i);
				}
				if (!string.IsNullOrEmpty(m_ObjectSpawner.m_Content))
				{
					MPSaveManager.SaveContainer(m_Scene, m_ObjectSpawner.m_GUID, m_ObjectSpawner.m_Content);
					MPSaveManager.SetConstainerState(m_Scene, m_ObjectSpawner.m_GUID, 1, DontNotify: true);
				}
				else
				{
					MPSaveManager.SetConstainerState(m_Scene, m_ObjectSpawner.m_GUID, 2, DontNotify: true);
				}
			}
			if (MyMod.level_guid == m_Scene)
			{
				MyMod.SpawnUniversalSyncableObject(universalSyncableObject);
			}
			ServerSend.ADDUNIVERSALSYNCABLE(universalSyncableObject);
		}
	}

	public static void RemoveGearSpawnerGear(int SearchKey)
	{
		if (m_GearSpawnerGears.ContainsKey(SearchKey))
		{
			string key = m_GearSpawnerGears[SearchKey];
			if (m_UnavailableGearSpawners.ContainsKey(key))
			{
				m_UnavailableGearSpawners.Remove(key);
			}
			m_GearSpawnerGears.Remove(SearchKey);
		}
	}

	public static void DoFlareShots(List<ExpeditionBuilder.ExpeditionGearSpawner> Spawners, string Scene)
	{
		foreach (ExpeditionBuilder.ExpeditionGearSpawner Spawner in Spawners)
		{
			string text = Spawner.PickGear();
			Vector3 possition = Spawner.m_Possition;
			Quaternion rotation = Spawner.m_Rotation;
			if (text == "GEAR_FlareGunShoot")
			{
				DataStr.ShootSync shootSync = new DataStr.ShootSync();
				shootSync.m_position = possition;
				shootSync.m_rotation = rotation;
				shootSync.m_camera_up = new Vector3(0f, 1f, 0f);
				shootSync.m_lookat = true;
				shootSync.m_projectilename = "GEAR_FlareGunAmmoSingle";
				shootSync.m_sceneguid = Scene;
				ServerSend.SHOOTSYNC(0, shootSync, toEveryOne: true);
				MyMod.DoShootSync(shootSync, 0);
			}
		}
	}

	public static void CreateRewardGear(ExpeditionBuilder.ExpeditionGearSpawner Spawner, string Scene, int PlayersInExpedition = 1, bool DebugFlag = false)
	{
		if (m_UnavailableGearSpawners.ContainsKey(Spawner.m_GUID) || !(Spawner.RollChance(PlayersInExpedition) || DebugFlag))
		{
			return;
		}
		string text = Spawner.PickGear();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		Vector3 possition = Spawner.m_Possition;
		Quaternion rotation = Spawner.m_Rotation;
		if (!(text == "GEAR_FlareGunShoot"))
		{
			DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = new DataStr.SlicedJsonDroppedGear();
			slicedJsonDroppedGear.m_GearName = text.ToLower();
			slicedJsonDroppedGear.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
			slicedJsonDroppedGear.m_Extra.m_Dropper = Spawner.m_Extra.m_Dropper;
			slicedJsonDroppedGear.m_Extra.m_GearName = slicedJsonDroppedGear.m_GearName;
			slicedJsonDroppedGear.m_Extra.m_Variant = 0;
			slicedJsonDroppedGear.m_Extra.m_PhotoGUID = Spawner.m_Extra.m_PhotoGUID;
			slicedJsonDroppedGear.m_Extra.m_ExpeditionNote = Spawner.m_Extra.m_ExpeditionNote;
			int vectorHash = Shared.GetVectorHash(possition);
			int quaternionHash = Shared.GetQuaternionHash(rotation);
			int hashCode = Scene.GetHashCode();
			int num = vectorHash + quaternionHash + hashCode;
			DataStr.DroppedGearItemDataPacket droppedGearItemDataPacket = new DataStr.DroppedGearItemDataPacket();
			droppedGearItemDataPacket.m_Extra = slicedJsonDroppedGear.m_Extra;
			droppedGearItemDataPacket.m_GearID = -1;
			droppedGearItemDataPacket.m_Hash = num;
			droppedGearItemDataPacket.m_LevelGUID = Scene;
			droppedGearItemDataPacket.m_Position = possition;
			droppedGearItemDataPacket.m_Rotation = rotation;
			slicedJsonDroppedGear.m_Json = "";
			MPSaveManager.AddGearData(Scene, num, slicedJsonDroppedGear);
			MPSaveManager.AddGearVisual(Scene, droppedGearItemDataPacket);
			DebugLog("[CreateRewardGear] NewGear.m_GearName " + slicedJsonDroppedGear.m_GearName);
			m_UnavailableGearSpawners.Add(Spawner.m_GUID, num);
			if (m_GearSpawnerGears.ContainsKey(num))
			{
				m_GearSpawnerGears.Remove(num);
			}
			m_GearSpawnerGears.Add(num, Spawner.m_GUID);
			Shared.FakeDropItem(droppedGearItemDataPacket, JustLoad: true);
			ServerSend.DROPITEM(0, droppedGearItemDataPacket, toEveryOne: true);
		}
	}

	public static void ApplyWorldEdits()
	{
		foreach (string worldEdit in ExpeditionBuilder.GetWorldEdits())
		{
			ExpeditionBuilder.ExpeditionTaskTemplate expeditionTaskTemplate = JSON.Load(ExpeditionBuilder.GetExpeditionJsonByAlias(worldEdit)).Make<ExpeditionBuilder.ExpeditionTaskTemplate>();
			SpawnObjectsFromSpawners(expeditionTaskTemplate.m_Objects, "WorldEdit", expeditionTaskTemplate.m_SceneName, AddContainersLoot: false);
		}
	}

	public static void RemoveObjectGroup(string group)
	{
		foreach (KeyValuePair<string, Dictionary<string, DataStr.UniversalSyncableObject>> item in MPSaveManager.UniversalSyncableObjects.ToList())
		{
			string key = item.Key;
			foreach (KeyValuePair<string, DataStr.UniversalSyncableObject> item2 in item.Value.ToList())
			{
				if (item2.Value.m_ObjectGroup == group)
				{
					MPSaveManager.RemoveUniversalSyncableObject(key, item2.Value.m_GUID);
					if (MyMod.level_guid == key)
					{
						MyMod.RemoveObjectByGUID(item2.Value.m_GUID);
					}
				}
			}
		}
	}

	public static List<string> GetAllSpeicalItemsOfPlayer(string MAC)
	{
		List<string> value = new List<string>();
		if (m_SpecialItemsOwners.TryGetValue(MAC, out value))
		{
			return value;
		}
		return new List<string>();
	}

	public static bool PlayerHasSpecialItem(string MAC, string Item)
	{
		foreach (string item in GetAllSpeicalItemsOfPlayer(MAC))
		{
			if (item == Item)
			{
				return true;
			}
		}
		return false;
	}

	public static bool PlayerCanRequestSepcialExpedition(string MAC, string ExpeditionName)
	{
		foreach (SpecialExpeditionItem specialItem in m_SpecialItems)
		{
			if (specialItem.m_ExpeditionAlias == ExpeditionName)
			{
				return PlayerHasSpecialItem(MAC, specialItem.m_GearReferenceName);
			}
		}
		return false;
	}

	public static void MaySpawnSpeicalExpeditionItem(int ClientID, string Scene)
	{
		foreach (SpecialExpeditionItem specialItem in m_SpecialItems)
		{
			if (specialItem.m_Scene == Scene && !PlayerHasSpecialItem(Server.GetMACByID(ClientID), specialItem.m_GearReferenceName))
			{
				DataStr.DroppedGearItemDataPacket droppedGearItemDataPacket = new DataStr.DroppedGearItemDataPacket();
				droppedGearItemDataPacket.m_Position = specialItem.m_Position;
				droppedGearItemDataPacket.m_Rotation = specialItem.m_Rotation;
				droppedGearItemDataPacket.m_LevelGUID = specialItem.m_Scene;
				droppedGearItemDataPacket.m_Extra.m_GearName = specialItem.m_ModelPrefab;
				droppedGearItemDataPacket.m_Extra.m_Variant = -2;
				droppedGearItemDataPacket.m_Extra.m_ExpeditionNote = specialItem.m_GearName;
				droppedGearItemDataPacket.m_Extra.m_PhotoGUID = specialItem.m_GearReferenceName;
				if (ClientID == 0)
				{
					MyMod.FakeDropItem(-1, droppedGearItemDataPacket.m_Position, droppedGearItemDataPacket.m_Rotation, 0, droppedGearItemDataPacket.m_Extra);
				}
				else
				{
					ServerSend.DROPITEM(0, droppedGearItemDataPacket, toEveryOne: false, ClientID);
				}
				Log("Created special item " + specialItem.m_GearName + " for client " + ClientID);
			}
		}
	}

	public static void GivePlayerSpeicalItem(int ClientID, string Item)
	{
		string mACByID = Server.GetMACByID(ClientID);
		if (m_SpecialItemsOwners.ContainsKey(mACByID))
		{
			m_SpecialItemsOwners[mACByID].Add(Item);
			return;
		}
		m_SpecialItemsOwners.Add(mACByID, new List<string> { Item });
	}
}
