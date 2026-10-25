using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2Cpp;
using TinyJSON;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoop;

public class ExpeditionEditor
{
	public static List<ExpeditionBuilder.ExpeditionGearSpawner> m_Spawns = new List<ExpeditionBuilder.ExpeditionGearSpawner>();

	public static Vector3 m_Center = new Vector3(0f, 0f, 0f);

	public static List<string> m_Containers = new List<string>();

	public static List<string> m_Plants = new List<string>();

	public static List<string> m_Breakdowns = new List<string>();

	public static List<DataStr.UniversalSyncableObjectSpawner> m_Objects = new List<DataStr.UniversalSyncableObjectSpawner>();

	public static List<GameObject> m_VisualizeObjects = new List<GameObject>();

	public static string m_LastSpawnerGUID = "";

	public static string m_LastObjectGUID = "";

	public static float m_LastChance = 0f;

	public static int m_LastType = 0;

	public static float m_LastRadious = 0f;

	public static GameObject m_InnerSphere = null;

	public static GameObject m_OutterSphere = null;

	public static int LastSelectedRegion = 6;

	public static string Seperator = "\\";

	public static bool LastOnlyShowFirstTasksState = false;

	public static void DisableRadiousSpheres()
	{
		if (m_InnerSphere != null)
		{
			m_InnerSphere.SetActive(value: false);
		}
		if (m_OutterSphere != null)
		{
			m_OutterSphere.SetActive(value: false);
		}
	}

	public static void SetRadiousSpheres()
	{
		if (m_LastType == 0)
		{
			if (m_InnerSphere != null)
			{
				m_InnerSphere.SetActive(value: false);
			}
			if (m_OutterSphere != null)
			{
				m_OutterSphere.SetActive(value: false);
			}
			return;
		}
		float value = MyMod.ExpeditionEditorUI.transform.GetChild(6).gameObject.GetComponent<Slider>().m_Value;
		if (m_InnerSphere == null)
		{
			m_InnerSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			m_OutterSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			UnityEngine.Object.Destroy(m_InnerSphere.GetComponent<SphereCollider>());
			UnityEngine.Object.Destroy(m_OutterSphere.GetComponent<SphereCollider>());
		}
		m_InnerSphere.transform.position = m_Center;
		m_InnerSphere.transform.localScale = new Vector3(value * 2f, value * 2f, value * 2f);
		m_InnerSphere.SetActive(value: true);
		m_OutterSphere.transform.position = m_Center;
		m_OutterSphere.transform.localScale = new Vector3((0f - value) * 2f, (0f - value) * 2f, (0f - value) * 2f);
		m_OutterSphere.SetActive(value: true);
	}

	public static void DisableAllPanels()
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(13).gameObject.SetActive(value: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(14).gameObject.SetActive(value: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).gameObject.SetActive(value: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(27).gameObject.SetActive(value: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(28).gameObject.SetActive(value: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(31).gameObject.SetActive(value: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).gameObject.SetActive(value: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(9).gameObject.SetActive(value: false);
	}

	public static void BackToSelect(int Region)
	{
		RefreshExpeditionsList(Region);
		DisableAllPanels();
		MyMod.ExpeditionEditorUI.SetActive(value: false);
		MyMod.ExpeditionEditorSelectUI.SetActive(value: true);
		DisableRadiousSpheres();
		DisableVisualizeObjects();
	}

	public static void ExpeditionTypeChanged()
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(17).GetComponent<Toggle>().Set(m_LastType == 0);
		SetRadiousSpheres();
	}

	public static void ExpeditionRadiousChanged()
	{
		SetRadiousSpheres();
	}

	public static void SetObjectiveGear()
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(20).GetComponent<InputField>().SetText(m_LastSpawnerGUID);
	}

	public static void AutoSelectRegion()
	{
		if (GameManager.GetUniStorm() != null)
		{
			int currentRegion = (int)RegionCompat.GetCurrentRegion();
			currentRegion += Shared.GameRegionNegativeOffset;
			MyMod.ExpeditionEditorUI.transform.GetChild(1).gameObject.GetComponent<Dropdown>().Set(currentRegion);
		}
	}

	public static void AutoSelectScene()
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(4).gameObject.GetComponent<InputField>().SetText(MyMod.level_guid);
	}

	public static void ToggleContainersList()
	{
		bool activeSelf = MyMod.ExpeditionEditorUI.transform.GetChild(13).gameObject.activeSelf;
		DisableAllPanels();
		MyMod.ExpeditionEditorUI.transform.GetChild(13).gameObject.SetActive(!activeSelf);
	}

	public static void ToggleGearsList()
	{
		bool activeSelf = MyMod.ExpeditionEditorUI.transform.GetChild(14).gameObject.activeSelf;
		DisableAllPanels();
		MyMod.ExpeditionEditorUI.transform.GetChild(14).gameObject.SetActive(!activeSelf);
	}

	public static void ToggleHarvestablesList()
	{
		bool activeSelf = MyMod.ExpeditionEditorUI.transform.GetChild(27).gameObject.activeSelf;
		DisableAllPanels();
		MyMod.ExpeditionEditorUI.transform.GetChild(27).gameObject.SetActive(!activeSelf);
	}

	public static void ToggleBreakdownsList()
	{
		bool activeSelf = MyMod.ExpeditionEditorUI.transform.GetChild(28).gameObject.activeSelf;
		DisableAllPanels();
		MyMod.ExpeditionEditorUI.transform.GetChild(28).gameObject.SetActive(!activeSelf);
	}

	public static void ToggleExtraPanel()
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).gameObject.SetActive(!MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).gameObject.activeSelf);
	}

	public static void ToggleObjectSettingsPanel()
	{
		bool flag = false;
		for (int i = 0; i < m_Objects.Count; i++)
		{
			if (m_Objects[i].m_GUID == m_LastObjectGUID)
			{
				if (m_Objects[i].m_Prefab == "ExpeditionInteractive")
				{
					flag = true;
				}
				break;
			}
		}
		if (!flag)
		{
			MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(9).gameObject.SetActive(value: false);
			MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).gameObject.SetActive(!MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).gameObject.activeSelf);
		}
		else
		{
			MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).gameObject.SetActive(value: false);
			MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(9).gameObject.SetActive(!MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(9).gameObject.activeSelf);
		}
	}

	public static void ToggleObjectsList()
	{
		bool activeSelf = MyMod.ExpeditionEditorUI.transform.GetChild(31).gameObject.activeSelf;
		DisableAllPanels();
		MyMod.ExpeditionEditorUI.transform.GetChild(31).gameObject.SetActive(!activeSelf);
	}

	public static void AutoPositionSelect()
	{
		m_Center = GameManager.GetPlayerTransform().position;
		SetRadiousSpheres();
	}

	public static void SetContainerContent()
	{
		if (!string.IsNullOrEmpty(m_LastObjectGUID))
		{
			string value = MPSaveManager.LoadContainer(MyMod.level_guid, m_LastObjectGUID);
			if (!string.IsNullOrEmpty(value))
			{
				MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).GetChild(1)
					.gameObject.GetComponent<InputField>().SetText(value);
				UpdateObjectSettings();
			}
		}
	}

	public static void SaveExpeditionTemplate()
	{
		int value = MyMod.ExpeditionEditorUI.transform.GetChild(3).GetComponent<Dropdown>().m_Value;
		MPSaveManager.CreateFolderIfNotExist("Mods");
		MPSaveManager.CreateFolderIfNotExist("Mods\\ExpeditionTemplates");
		string text = MyMod.ExpeditionEditorUI.transform.GetChild(9).GetComponent<InputField>().text;
		ExpeditionBuilder.ExpeditionTaskTemplate expeditionTaskTemplate = new ExpeditionBuilder.ExpeditionTaskTemplate();
		expeditionTaskTemplate.m_TaskType = (ExpeditionManager.ExpeditionTaskType)value;
		expeditionTaskTemplate.m_Alias = text;
		expeditionTaskTemplate.m_RegionBelong = MyMod.ExpeditionEditorUI.transform.GetChild(1).GetComponent<Dropdown>().m_Value - Shared.GameRegionNegativeOffset;
		expeditionTaskTemplate.m_SceneName = MyMod.ExpeditionEditorUI.transform.GetChild(4).GetComponent<InputField>().text;
		expeditionTaskTemplate.m_TaskText = MyMod.ExpeditionEditorUI.transform.GetChild(10).GetComponent<InputField>().text;
		expeditionTaskTemplate.m_GearSpawners = m_Spawns;
		expeditionTaskTemplate.m_Containers = m_Containers;
		expeditionTaskTemplate.m_ZoneCenter = m_Center;
		expeditionTaskTemplate.m_ZoneRadius = MyMod.ExpeditionEditorUI.transform.GetChild(6).gameObject.GetComponent<Slider>().m_Value;
		expeditionTaskTemplate.m_RestockSceneContainers = MyMod.ExpeditionEditorUI.transform.GetChild(17).GetComponent<Toggle>().isOn;
		expeditionTaskTemplate.m_CanBeTaken = MyMod.ExpeditionEditorUI.transform.GetChild(18).GetComponent<Toggle>().isOn;
		expeditionTaskTemplate.m_NextTaskAlias = MyMod.ExpeditionEditorUI.transform.GetChild(19).GetComponent<InputField>().text;
		expeditionTaskTemplate.m_ObjectiveGearGUID = MyMod.ExpeditionEditorUI.transform.GetChild(20).GetComponent<InputField>().text;
		expeditionTaskTemplate.m_CompleatOrder = (ExpeditionManager.ExpeditionCompleteOrder)MyMod.ExpeditionEditorUI.transform.GetChild(21).GetComponent<Dropdown>().m_Value;
		expeditionTaskTemplate.m_RandomTasksAmout = int.Parse(MyMod.ExpeditionEditorUI.transform.GetChild(22).GetComponent<InputField>().text);
		expeditionTaskTemplate.m_Time = int.Parse(MyMod.ExpeditionEditorUI.transform.GetChild(23).GetComponent<InputField>().text);
		expeditionTaskTemplate.m_TimeAdd = MyMod.ExpeditionEditorUI.transform.GetChild(24).GetComponent<Dropdown>().m_Value == 0;
		expeditionTaskTemplate.m_Plants = m_Plants;
		expeditionTaskTemplate.m_Breakdowns = m_Breakdowns;
		expeditionTaskTemplate.m_StaySeconds = int.Parse(MyMod.ExpeditionEditorUI.transform.GetChild(29).GetComponent<InputField>().text);
		expeditionTaskTemplate.m_Objects = m_Objects;
		MPSaveManager.SaveData(text + ".json", JSON.Dump(expeditionTaskTemplate), 0, "Mods\\ExpeditionTemplates\\" + text + ".json");
	}

	public static void AddContainer(string GUID)
	{
		if (!m_Containers.Contains(GUID))
		{
			m_Containers.Add(GUID);
			RecontructContainersList();
			HUDMessage.AddMessage("Added Container " + GUID);
		}
		else
		{
			HUDMessage.AddMessage("Container " + GUID + " is already added!");
		}
	}

	public static void AddPlant(string GUID)
	{
		if (!m_Plants.Contains(GUID))
		{
			m_Plants.Add(GUID);
			RecontructHarvestablesList();
			HUDMessage.AddMessage("Added Harvestable " + GUID);
		}
		else
		{
			HUDMessage.AddMessage("Harvestable " + GUID + " is already added!");
		}
	}

	public static void AddBreakdown(string GUID)
	{
		if (!m_Breakdowns.Contains(GUID))
		{
			m_Breakdowns.Add(GUID);
			RecontructBreakdownsList();
			HUDMessage.AddMessage("Added Breakdown " + GUID);
		}
		else
		{
			HUDMessage.AddMessage("Breakdown " + GUID + " is already added!");
		}
	}

	public static void RemoveContainerFromList(GameObject Element)
	{
		m_Containers.Remove(Element.transform.GetChild(0).gameObject.GetComponent<Text>().text);
		RecontructContainersList();
	}

	public static void RemoveHarvestableFromList(GameObject Element)
	{
		m_Plants.Remove(Element.transform.GetChild(0).gameObject.GetComponent<Text>().text);
		RecontructHarvestablesList();
	}

	public static void RemoveBreakdownFromList(GameObject Element)
	{
		m_Breakdowns.Remove(Element.transform.GetChild(0).gameObject.GetComponent<Text>().text);
		RecontructBreakdownsList();
	}

	public static void ClearSelectedGear()
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(2).gameObject.GetComponent<Text>().text = "GUID:";
		MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(3).gameObject.GetComponent<Slider>().Set(0f, sendCallback: false);
		MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(4).gameObject.GetComponent<Dropdown>().ClearOptions();
	}

	public static void ClearSelectedObject()
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(2).gameObject.GetComponent<Text>().text = "GUID:";
	}

	public static void UpdateExtra()
	{
		for (int num = m_Spawns.Count - 1; num >= 0; num--)
		{
			if (m_Spawns[num].m_GUID == m_LastSpawnerGUID)
			{
				m_Spawns[num].m_Extra.m_Dropper = MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).GetChild(1)
					.GetComponent<InputField>()
					.text;
				m_Spawns[num].m_Extra.m_PhotoGUID = MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).GetChild(2)
					.GetComponent<InputField>()
					.text;
				m_Spawns[num].m_Extra.m_ExpeditionNote = MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).GetChild(4)
					.GetComponent<InputField>()
					.text;
				break;
			}
		}
	}

	public static DataStr.ExpeditionInteractiveData GetInteractiveData(DataStr.UniversalSyncableObjectSpawner Object)
	{
		DataStr.ExpeditionInteractiveData expeditionInteractiveData = new DataStr.ExpeditionInteractiveData();
		Transform child = MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(9);
		expeditionInteractiveData.m_GUID = Object.m_GUID;
		expeditionInteractiveData.m_Position.x = float.Parse(child.GetChild(0).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Position.y = float.Parse(child.GetChild(1).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Position.z = float.Parse(child.GetChild(2).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Rotation.x = float.Parse(child.GetChild(3).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Rotation.y = float.Parse(child.GetChild(4).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Rotation.z = float.Parse(child.GetChild(5).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Scale.x = float.Parse(child.GetChild(6).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Scale.y = float.Parse(child.GetChild(7).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Scale.z = float.Parse(child.GetChild(8).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_ObjectText = child.GetChild(9).gameObject.GetComponent<InputField>().text;
		expeditionInteractiveData.m_InteractTime = float.Parse(child.GetChild(10).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_InteractText = child.GetChild(11).gameObject.GetComponent<InputField>().text;
		expeditionInteractiveData.m_Tool = child.GetChild(12).gameObject.GetComponent<InputField>().text;
		expeditionInteractiveData.m_Material = child.GetChild(13).gameObject.GetComponent<InputField>().text;
		expeditionInteractiveData.m_MaterialCount = int.Parse(child.GetChild(14).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Audio = child.GetChild(16).gameObject.GetComponent<InputField>().text;
		expeditionInteractiveData.m_Yield = child.GetChild(17).gameObject.GetComponent<InputField>().text;
		expeditionInteractiveData.m_YieldCount = int.Parse(child.GetChild(18).gameObject.GetComponent<InputField>().text);
		expeditionInteractiveData.m_Impact = (ExpeditionManager.ExpeditionInteractiveImpact)child.GetChild(19).gameObject.GetComponent<Dropdown>().m_Value;
		expeditionInteractiveData.m_ObjectGroupToRemove = child.GetChild(20).gameObject.GetComponent<InputField>().text;
		return expeditionInteractiveData;
	}

	public static void SetnteractiveData(DataStr.UniversalSyncableObjectSpawner Object)
	{
		DataStr.ExpeditionInteractiveData interactiveData = Object.m_InteractiveData;
		Transform child = MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(9);
		child.GetChild(0).gameObject.GetComponent<InputField>().text = interactiveData.m_Position.x.ToString(CultureInfo.InvariantCulture);
		child.GetChild(1).gameObject.GetComponent<InputField>().text = interactiveData.m_Position.y.ToString(CultureInfo.InvariantCulture);
		child.GetChild(2).gameObject.GetComponent<InputField>().text = interactiveData.m_Position.z.ToString(CultureInfo.InvariantCulture);
		child.GetChild(3).gameObject.GetComponent<InputField>().text = interactiveData.m_Rotation.x.ToString(CultureInfo.InvariantCulture);
		child.GetChild(4).gameObject.GetComponent<InputField>().text = interactiveData.m_Rotation.y.ToString(CultureInfo.InvariantCulture);
		child.GetChild(5).gameObject.GetComponent<InputField>().text = interactiveData.m_Rotation.z.ToString(CultureInfo.InvariantCulture);
		child.GetChild(6).gameObject.GetComponent<InputField>().text = interactiveData.m_Scale.x.ToString(CultureInfo.InvariantCulture);
		child.GetChild(7).gameObject.GetComponent<InputField>().text = interactiveData.m_Scale.y.ToString(CultureInfo.InvariantCulture);
		child.GetChild(8).gameObject.GetComponent<InputField>().text = interactiveData.m_Scale.z.ToString(CultureInfo.InvariantCulture);
		child.GetChild(9).gameObject.GetComponent<InputField>().text = interactiveData.m_ObjectText;
		child.GetChild(10).gameObject.GetComponent<InputField>().text = interactiveData.m_InteractTime.ToString(CultureInfo.InvariantCulture);
		child.GetChild(11).gameObject.GetComponent<InputField>().text = interactiveData.m_InteractText;
		child.GetChild(12).gameObject.GetComponent<InputField>().text = interactiveData.m_Tool;
		child.GetChild(13).gameObject.GetComponent<InputField>().text = interactiveData.m_Material;
		child.GetChild(14).gameObject.GetComponent<InputField>().text = interactiveData.m_MaterialCount.ToString(CultureInfo.InvariantCulture);
		child.GetChild(16).gameObject.GetComponent<InputField>().text = interactiveData.m_Audio.ToString(CultureInfo.InvariantCulture);
		child.GetChild(17).gameObject.GetComponent<InputField>().text = interactiveData.m_Yield.ToString(CultureInfo.InvariantCulture);
		child.GetChild(18).gameObject.GetComponent<InputField>().text = interactiveData.m_YieldCount.ToString(CultureInfo.InvariantCulture);
		child.GetChild(19).gameObject.GetComponent<Dropdown>().Set((int)interactiveData.m_Impact);
		child.GetChild(20).gameObject.GetComponent<InputField>().SetText(interactiveData.m_ObjectGroupToRemove);
	}

	public static void UpdateObjectSettings()
	{
		for (int num = m_Objects.Count - 1; num >= 0; num--)
		{
			if (m_Objects[num].m_GUID == m_LastObjectGUID)
			{
				m_Objects[num].m_Content = MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).GetChild(1)
					.gameObject.GetComponent<InputField>().text;
				m_Objects[num].m_InteractiveData = GetInteractiveData(m_Objects[num]);
				m_Objects[num].m_ObjectGroup = MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).GetChild(3)
					.gameObject.GetComponent<InputField>().text;
				break;
			}
		}
	}

	public static void UpdateGear()
	{
		ClearSelectedGear();
		if (string.IsNullOrEmpty(m_LastSpawnerGUID))
		{
			return;
		}
		for (int num = m_Spawns.Count - 1; num >= 0; num--)
		{
			if (m_Spawns[num].m_GUID == m_LastSpawnerGUID)
			{
				MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(2).gameObject.GetComponent<Text>().text = "GUID: " + m_Spawns[num].m_GUID;
				MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(3).gameObject.GetComponent<Slider>().Set(m_Spawns[num].m_Chance, sendCallback: false);
				m_LastChance = m_Spawns[num].m_Chance;
				Dropdown component = MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(4).gameObject.GetComponent<Dropdown>();
				for (int i = 0; i < m_Spawns[num].m_GearsVariant.Count; i++)
				{
					component.options.Add(new Dropdown.OptionData(m_Spawns[num].m_GearsVariant[i]));
				}
				if (m_Spawns[num].m_GearsVariant.Count == 1)
				{
					component.Set(1);
				}
				else
				{
					component.Set(m_Spawns[num].m_GearsVariant.Count - 1);
				}
				MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).GetChild(1)
					.GetComponent<InputField>()
					.SetText(m_Spawns[num].m_Extra.m_Dropper);
				MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).GetChild(2)
					.GetComponent<InputField>()
					.SetText(m_Spawns[num].m_Extra.m_PhotoGUID);
				MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(7).GetChild(4)
					.GetComponent<InputField>()
					.SetText(m_Spawns[num].m_Extra.m_ExpeditionNote);
				break;
			}
		}
	}

	public static void UpdateObject()
	{
		ClearSelectedObject();
		if (string.IsNullOrEmpty(m_LastObjectGUID))
		{
			return;
		}
		for (int num = m_Objects.Count - 1; num >= 0; num--)
		{
			if (m_Objects[num].m_GUID == m_LastObjectGUID)
			{
				MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(2).gameObject.GetComponent<Text>().text = "GUID: " + m_LastObjectGUID;
				MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).GetChild(1)
					.gameObject.GetComponent<InputField>().SetText(m_Objects[num].m_Content);
				SetnteractiveData(m_Objects[num]);
				MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(4).GetChild(3)
					.gameObject.GetComponent<InputField>().SetText(m_Objects[num].m_ObjectGroup);
				break;
			}
		}
	}

	public static void RemoveObjectFromList(GameObject Element)
	{
		string text = Element.GetComponent<Comps.LocalVariablesKit>().m_String;
		for (int num = m_Objects.Count - 1; num >= 0; num--)
		{
			if (m_Objects[num].m_GUID == text)
			{
				if (m_LastObjectGUID == text)
				{
					ClearSelectedObject();
				}
				m_Objects.RemoveAt(num);
				RecontructObjectsList();
				VisualizeObjects();
				break;
			}
		}
	}

	public static void SelectObjectFromList(GameObject Element)
	{
		string text = Element.GetComponent<Comps.LocalVariablesKit>().m_String;
		for (int num = m_Objects.Count - 1; num >= 0; num--)
		{
			if (m_Objects[num].m_GUID == text)
			{
				UpdateObjectSettings();
				m_LastObjectGUID = text;
				UpdateObject();
				break;
			}
		}
	}

	public static void RemoveGearFromList(GameObject Element)
	{
		string text = Element.GetComponent<Comps.LocalVariablesKit>().m_String;
		ExpeditionManager.DebugLog("RemoveGearFromList " + text);
		for (int num = m_Spawns.Count - 1; num >= 0; num--)
		{
			if (m_Spawns[num].m_GUID == text)
			{
				if (m_LastSpawnerGUID == text)
				{
					ClearSelectedGear();
				}
				m_Spawns.RemoveAt(num);
				RecontructGearsList();
				break;
			}
		}
	}

	public static void SelectGearFromList(GameObject Element)
	{
		string text = Element.GetComponent<Comps.LocalVariablesKit>().m_String;
		ExpeditionManager.DebugLog("SelectGearFromList " + text);
		for (int num = m_Spawns.Count - 1; num >= 0; num--)
		{
			if (m_Spawns[num].m_GUID == text)
			{
				UpdateExtra();
				m_LastSpawnerGUID = text;
				UpdateGear();
				break;
			}
		}
	}

	public static void RecontructContainersList()
	{
		GameObject gameObject = MyMod.ExpeditionEditorUI.transform.GetChild(13).GetChild(1).GetChild(0)
			.GetChild(0)
			.gameObject;
		for (int num = gameObject.transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(gameObject.transform.GetChild(num).gameObject);
		}
		GameObject prefab = MyMod.BundleAsset<GameObject>("MP_ContainerElement");
		foreach (string container in m_Containers)
		{
			GameObject Element = MyMod.SpawnModAsset(prefab, gameObject.transform);
			Element.transform.GetChild(0).gameObject.GetComponent<Text>().text = container;
			Action action = delegate
			{
				RemoveContainerFromList(Element);
			};
			Element.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(action);
		}
	}

	public static void RecontructHarvestablesList()
	{
		GameObject gameObject = MyMod.ExpeditionEditorUI.transform.GetChild(27).GetChild(1).GetChild(0)
			.GetChild(0)
			.gameObject;
		for (int num = gameObject.transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(gameObject.transform.GetChild(num).gameObject);
		}
		GameObject prefab = MyMod.BundleAsset<GameObject>("MP_ContainerElement");
		foreach (string plant in m_Plants)
		{
			GameObject Element = MyMod.SpawnModAsset(prefab, gameObject.transform);
			Element.transform.GetChild(0).gameObject.GetComponent<Text>().text = plant;
			Action action = delegate
			{
				RemoveHarvestableFromList(Element);
			};
			Element.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(action);
		}
	}

	public static void RecontructBreakdownsList()
	{
		GameObject gameObject = MyMod.ExpeditionEditorUI.transform.GetChild(28).GetChild(1).GetChild(0)
			.GetChild(0)
			.gameObject;
		for (int num = gameObject.transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(gameObject.transform.GetChild(num).gameObject);
		}
		GameObject prefab = MyMod.BundleAsset<GameObject>("MP_ContainerElement");
		foreach (string breakdown in m_Breakdowns)
		{
			GameObject Element = MyMod.SpawnModAsset(prefab, gameObject.transform);
			Element.transform.GetChild(0).gameObject.GetComponent<Text>().text = breakdown;
			Action action = delegate
			{
				RemoveBreakdownFromList(Element);
			};
			Element.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(action);
		}
	}

	public static void RecontructObjectsList()
	{
		GameObject gameObject = MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(1).GetChild(0)
			.GetChild(0)
			.gameObject;
		for (int num = gameObject.transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(gameObject.transform.GetChild(num).gameObject);
		}
		GameObject prefab = MyMod.BundleAsset<GameObject>("MP_GearElement");
		foreach (DataStr.UniversalSyncableObjectSpawner @object in m_Objects)
		{
			GameObject Element = MyMod.SpawnModAsset(prefab, gameObject.transform);
			Element.AddComponent<Comps.LocalVariablesKit>().m_String = @object.m_GUID;
			Element.transform.GetChild(0).GetChild(0).gameObject.GetComponent<Text>().text = @object.m_Prefab;
			Action action = delegate
			{
				RemoveObjectFromList(Element);
			};
			Element.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(action);
			Action action2 = delegate
			{
				SelectObjectFromList(Element);
			};
			Element.transform.GetChild(0).gameObject.GetComponent<Button>().onClick.AddListener(action2);
		}
	}

	public static void AddGear(string GearPrefab, Vector3 Pos, Quaternion Rot, string PHOTOGUID)
	{
		ExpeditionBuilder.ExpeditionGearSpawner expeditionGearSpawner = new ExpeditionBuilder.ExpeditionGearSpawner();
		expeditionGearSpawner.m_GUID = Guid.NewGuid().ToString();
		expeditionGearSpawner.m_Chance = 0.33f;
		expeditionGearSpawner.m_GearsVariant.Add(GearPrefab);
		expeditionGearSpawner.m_Possition = Pos;
		expeditionGearSpawner.m_Rotation = Rot;
		expeditionGearSpawner.m_Extra.m_PhotoGUID = PHOTOGUID;
		m_Spawns.Add(expeditionGearSpawner);
		if (!string.IsNullOrEmpty(PHOTOGUID))
		{
			MPSaveManager.CopyPhotoToExpeditionCache(PHOTOGUID);
		}
		HUDMessage.AddMessage("Added Gear " + expeditionGearSpawner.m_GUID);
		RecontructGearsList();
		UpdateExtra();
		m_LastSpawnerGUID = expeditionGearSpawner.m_GUID;
		UpdateGear();
	}

	public static void AddGearVariant(string GearPrefab)
	{
		if (string.IsNullOrEmpty(m_LastSpawnerGUID))
		{
			return;
		}
		for (int num = m_Spawns.Count - 1; num >= 0; num--)
		{
			if (m_Spawns[num].m_GUID == m_LastSpawnerGUID)
			{
				if (!m_Spawns[num].m_GearsVariant.Contains(GearPrefab))
				{
					m_Spawns[num].m_GearsVariant.Add(GearPrefab);
					HUDMessage.AddMessage("Added" + GearPrefab + " Variant");
				}
				else
				{
					HUDMessage.AddMessage(GearPrefab + " Already Added!");
				}
				UpdateGear();
				break;
			}
		}
	}

	public static void RemoveGearVariant()
	{
		if (string.IsNullOrEmpty(m_LastSpawnerGUID))
		{
			return;
		}
		Dropdown component = MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(4).gameObject.GetComponent<Dropdown>();
		for (int num = m_Spawns.Count - 1; num >= 0; num--)
		{
			if (m_Spawns[num].m_GUID == m_LastSpawnerGUID)
			{
				if (m_Spawns[num].m_GearsVariant.Count > 1)
				{
					m_Spawns[num].m_GearsVariant.RemoveAt(component.m_Value);
				}
				UpdateGear();
				break;
			}
		}
	}

	public static void ChangeChance()
	{
		if (string.IsNullOrEmpty(m_LastSpawnerGUID))
		{
			return;
		}
		for (int num = m_Spawns.Count - 1; num >= 0; num--)
		{
			if (m_Spawns[num].m_GUID == m_LastSpawnerGUID)
			{
				m_Spawns[num].m_Chance = MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(3).gameObject.GetComponent<Slider>().value;
				break;
			}
		}
	}

	public static void RecontructGearsList()
	{
		GameObject gameObject = MyMod.ExpeditionEditorUI.transform.GetChild(14).GetChild(1).GetChild(0)
			.GetChild(0)
			.gameObject;
		for (int num = gameObject.transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(gameObject.transform.GetChild(num).gameObject);
		}
		GameObject prefab = MyMod.BundleAsset<GameObject>("MP_GearElement");
		foreach (ExpeditionBuilder.ExpeditionGearSpawner spawn in m_Spawns)
		{
			GameObject Element = MyMod.SpawnModAsset(prefab, gameObject.transform);
			Element.AddComponent<Comps.LocalVariablesKit>().m_String = spawn.m_GUID;
			Element.transform.GetChild(0).GetChild(0).gameObject.GetComponent<Text>().text = spawn.m_GearsVariant[0];
			Action action = delegate
			{
				RemoveGearFromList(Element);
			};
			Element.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(action);
			Action action2 = delegate
			{
				SelectGearFromList(Element);
			};
			Element.transform.GetChild(0).gameObject.GetComponent<Button>().onClick.AddListener(action2);
		}
	}

	public static void LoadExpedition(ExpeditionBuilder.ExpeditionTaskTemplate Data)
	{
		MyMod.ExpeditionEditorUI.transform.GetChild(1).GetComponent<Dropdown>().Set(Data.m_RegionBelong + Shared.GameRegionNegativeOffset);
		MyMod.ExpeditionEditorUI.transform.GetChild(3).GetComponent<Dropdown>().Set((int)Data.m_TaskType);
		MyMod.ExpeditionEditorUI.transform.GetChild(4).GetComponent<InputField>().SetText(Data.m_SceneName);
		MyMod.ExpeditionEditorUI.transform.GetChild(6).GetComponent<Slider>().Set(Data.m_ZoneRadius);
		m_Center = Data.m_ZoneCenter;
		MyMod.ExpeditionEditorUI.transform.GetChild(9).GetComponent<InputField>().SetText(Data.m_Alias);
		MyMod.ExpeditionEditorUI.transform.GetChild(10).GetComponent<InputField>().SetText(Data.m_TaskText);
		MyMod.ExpeditionEditorUI.transform.GetChild(18).GetComponent<Toggle>().Set(Data.m_CanBeTaken);
		m_Spawns = Data.m_GearSpawners;
		m_Containers = Data.m_Containers;
		MyMod.ExpeditionEditorUI.transform.GetChild(20).GetComponent<InputField>().SetText(Data.m_ObjectiveGearGUID);
		MyMod.ExpeditionEditorUI.transform.GetChild(19).GetComponent<InputField>().SetText(Data.m_NextTaskAlias);
		MyMod.ExpeditionEditorUI.transform.GetChild(21).GetComponent<Dropdown>().Set((int)Data.m_CompleatOrder);
		MyMod.ExpeditionEditorUI.transform.GetChild(22).GetComponent<InputField>().SetText(Data.m_RandomTasksAmout.ToString());
		MyMod.ExpeditionEditorUI.transform.GetChild(23).GetComponent<InputField>().SetText(Data.m_Time.ToString());
		MyMod.ExpeditionEditorUI.transform.GetChild(29).GetComponent<InputField>().SetText(Data.m_StaySeconds.ToString());
		int value = 0;
		if (!Data.m_TimeAdd)
		{
			value = 1;
		}
		MyMod.ExpeditionEditorUI.transform.GetChild(24).GetComponent<Dropdown>().Set(value);
		m_Plants = Data.m_Plants;
		m_Breakdowns = Data.m_Breakdowns;
		m_Objects = Data.m_Objects;
		RecontructContainersList();
		RecontructGearsList();
		RecontructHarvestablesList();
		RecontructBreakdownsList();
		RecontructObjectsList();
		UpdateGear();
		UpdateObject();
		SetRadiousSpheres();
		MyMod.ExpeditionEditorSelectUI.SetActive(value: false);
		MyMod.ExpeditionEditorUI.SetActive(value: true);
	}

	public static void SelectExpedition(GameObject Element)
	{
		LoadExpedition(JSON.Load(ExpeditionBuilder.GetExpeditionJsonByAlias(Element.GetComponent<Comps.LocalVariablesKit>().m_String)).Make<ExpeditionBuilder.ExpeditionTaskTemplate>());
	}

	public static void SelectRegion(GameObject Element)
	{
		RefreshExpeditionsList(LastSelectedRegion = Element.GetComponent<Comps.LocalVariablesKit>().m_Int);
	}

	public static void TestExpedition(GameObject Element)
	{
		string alias = Element.transform.GetChild(0).gameObject.GetComponent<Text>().text.Replace(".json", "");
		string subNetworkGUID = MPSaveManager.GetSubNetworkGUID();
		for (int num = ExpeditionManager.m_ActiveExpeditions.Count - 1; num >= 0; num--)
		{
			if (ExpeditionManager.m_ActiveExpeditions[num].m_Players.Contains(subNetworkGUID))
			{
				ExpeditionManager.CompleteExpedition(ExpeditionManager.m_ActiveExpeditions[num].m_GUID, 0);
			}
		}
		ExpeditionManager.StartNewExpedition(subNetworkGUID, 0, alias);
		DisableRadiousSpheres();
	}

	public static void DisableVisualizeObjects()
	{
		for (int num = m_VisualizeObjects.Count - 1; num >= 0; num--)
		{
			if (m_VisualizeObjects[num] != null && (bool)m_VisualizeObjects[num].GetComponent<ObjectGuid>())
			{
				bool flag = false;
				for (int i = 0; i < m_Objects.Count; i++)
				{
					if (m_Objects[i].m_GUID == m_VisualizeObjects[num].GetComponent<ObjectGuid>().Get())
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					UnityEngine.Object.Destroy(m_VisualizeObjects[num]);
					m_VisualizeObjects.RemoveAt(num);
				}
			}
		}
	}

	public static void ApplyObjectSettings()
	{
		UpdateObjectSettings();
		VisualizeObjects();
	}

	public static void VisualizeObjects()
	{
		DisableVisualizeObjects();
		foreach (DataStr.UniversalSyncableObjectSpawner @object in m_Objects)
		{
			if (@object.m_Prefab == "RockCache")
			{
				DataStr.FakeRockCacheVisualData obj = new DataStr.FakeRockCacheVisualData
				{
					m_GUID = @object.m_GUID,
					m_Position = @object.m_Position,
					m_Rotation = @object.m_Rotation,
					m_Owner = "Unknown"
				};
				MyMod.AddRockCache(obj);
				MPSaveManager.AddRockCach(obj, 0);
				MPSaveManager.SaveContainer(MyMod.level_guid, @object.m_GUID, @object.m_Content);
				continue;
			}
			DataStr.UniversalSyncableObject universalSyncableObject = new DataStr.UniversalSyncableObject();
			universalSyncableObject.m_Prefab = @object.m_Prefab;
			universalSyncableObject.m_GUID = @object.m_GUID;
			universalSyncableObject.m_Position = @object.m_Position;
			universalSyncableObject.m_Rotation = @object.m_Rotation;
			universalSyncableObject.m_InteractiveData = @object.m_InteractiveData;
			MPSaveManager.RemoveContainer(MyMod.level_guid, @object.m_GUID);
			for (int i = 1; i < 10; i++)
			{
				MPSaveManager.RemoveContainer(MyMod.level_guid, @object.m_GUID + i);
			}
			if (!string.IsNullOrEmpty(@object.m_Content))
			{
				MPSaveManager.SaveContainer(MyMod.level_guid, @object.m_GUID, @object.m_Content);
				MPSaveManager.SetConstainerState(MyMod.level_guid, @object.m_GUID, 1);
			}
			else
			{
				MPSaveManager.SetConstainerState(MyMod.level_guid, @object.m_GUID, 2);
			}
			universalSyncableObject.m_ExpeditionBelong = "";
			universalSyncableObject.m_Scene = MyMod.level_guid;
			universalSyncableObject.m_CreationTime = MyMod.MinutesFromStartServer;
			GameObject gameObject = MyMod.SpawnUniversalSyncableObject(universalSyncableObject, DebugPurpose: true);
			if ((bool)gameObject)
			{
				m_VisualizeObjects.Add(gameObject);
			}
		}
	}

	public static void ToggleColisionOfObject(GameObject obj, bool Status)
	{
		List<GameObject> list = new List<GameObject>();
		list.Add(obj);
		foreach (Transform componentsInChild in obj.transform.GetComponentsInChildren<Transform>())
		{
			list.Add(componentsInChild.gameObject);
		}
		foreach (GameObject item in list)
		{
			foreach (BoxCollider component in item.GetComponents<BoxCollider>())
			{
				component.enabled = Status;
			}
			foreach (SphereCollider component2 in item.GetComponents<SphereCollider>())
			{
				component2.enabled = Status;
			}
			foreach (MeshCollider component3 in item.GetComponents<MeshCollider>())
			{
				component3.enabled = Status;
			}
			foreach (Rigidbody component4 in item.GetComponents<Rigidbody>())
			{
				component4.isKinematic = true;
			}
		}
	}

	public static void PlaceVisualizedObject()
	{
		GameObject gameObject = ObjectGuidManager.Lookup(m_LastObjectGUID);
		if ((bool)gameObject)
		{
			GameManager.GetPlayerManagerComponent().StartPlaceMesh(gameObject, PlaceMeshFlags.None);
			ToggleColisionOfObject(gameObject, Status: false);
		}
	}

	public static void AddObject()
	{
		string text = MyMod.ExpeditionEditorUI.transform.GetChild(31).GetChild(5).GetComponent<InputField>()
			.text;
		DataStr.UniversalSyncableObjectSpawner universalSyncableObjectSpawner = new DataStr.UniversalSyncableObjectSpawner();
		universalSyncableObjectSpawner.m_Prefab = text;
		universalSyncableObjectSpawner.m_Position = GameManager.GetPlayerTransform().transform.position;
		universalSyncableObjectSpawner.m_Rotation = GameManager.GetPlayerTransform().transform.rotation;
		universalSyncableObjectSpawner.m_GUID = ObjectGuidManager.GenerateNewGuidString();
		universalSyncableObjectSpawner.m_InteractiveData.m_Position = universalSyncableObjectSpawner.m_Position;
		universalSyncableObjectSpawner.m_InteractiveData.m_Rotation = universalSyncableObjectSpawner.m_Rotation.eulerAngles;
		universalSyncableObjectSpawner.m_InteractiveData.m_GUID = universalSyncableObjectSpawner.m_GUID;
		m_Objects.Add(universalSyncableObjectSpawner);
		m_LastObjectGUID = universalSyncableObjectSpawner.m_GUID;
		VisualizeObjects();
		RecontructObjectsList();
		UpdateObject();
		UpdateObjectSettings();
	}

	public static void RefreshExpeditionsList(int RegionMode = 6)
	{
		MPSaveManager.CreateFolderIfNotExist("Mods");
		MPSaveManager.CreateFolderIfNotExist("Mods" + Seperator + "ExpeditionTemplates");
		new DirectoryInfo("Mods" + Seperator + "ExpeditionTemplates");
		ExpeditionBuilder.RefrashTemplatesList();
		List<string> list = new List<string>();
		List<int> list2 = new List<int>();
		foreach (KeyValuePair<int, List<ExpeditionBuilder.ExpeditionTaskTemplate>> expeditionTask in ExpeditionBuilder.m_ExpeditionTasks)
		{
			int key = expeditionTask.Key;
			if (RegionMode == 6)
			{
				list.Add(ExpeditionBuilder.GetRegionString(key));
				list2.Add(key);
			}
			else
			{
				if (key != RegionMode)
				{
					continue;
				}
				foreach (ExpeditionBuilder.ExpeditionTaskTemplate item in expeditionTask.Value)
				{
					if (!LastOnlyShowFirstTasksState || item.m_CanBeTaken)
					{
						list.Add(item.m_Alias);
						list2.Add(item.m_RegionBelong);
					}
				}
			}
		}
		MyMod.ExpeditionEditorSelectUI.transform.GetChild(3).gameObject.SetActive(RegionMode == 6);
		MyMod.ExpeditionEditorSelectUI.transform.GetChild(4).gameObject.SetActive(RegionMode != 6);
		MyMod.ExpeditionEditorSelectUI.transform.GetChild(5).gameObject.SetActive(RegionMode != 6);
		GameObject gameObject = MyMod.ExpeditionEditorSelectUI.transform.GetChild(1).GetChild(0).GetChild(0)
			.gameObject;
		for (int num = gameObject.transform.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(gameObject.transform.GetChild(num).gameObject);
		}
		GameObject prefab = MyMod.BundleAsset<GameObject>("MP_ExpeditionElement");
		for (int i = 0; i < list.Count; i++)
		{
			string text = list[i];
			int num2 = list2[i];
			GameObject Element = MyMod.SpawnModAsset(prefab, gameObject.transform);
			Comps.LocalVariablesKit localVariablesKit = Element.AddComponent<Comps.LocalVariablesKit>();
			localVariablesKit.m_Int = num2;
			localVariablesKit.m_String = text;
			Element.transform.GetChild(0).gameObject.GetComponent<Text>().text = text;
			if (RegionMode != 6)
			{
				Action action = delegate
				{
					SelectExpedition(Element);
				};
				Element.transform.GetChild(1).gameObject.GetComponent<Button>().onClick.AddListener(action);
				Action action2 = delegate
				{
					TestExpedition(Element);
				};
				Element.transform.GetChild(2).gameObject.GetComponent<Button>().onClick.AddListener(action2);
			}
			else
			{
				Action action3 = delegate
				{
					SelectRegion(Element);
				};
				Element.transform.GetChild(2).gameObject.GetComponent<Button>().onClick.AddListener(action3);
				Element.transform.GetChild(1).gameObject.SetActive(value: false);
				Element.transform.GetChild(2).GetChild(0).gameObject.GetComponent<Text>().text = "Open";
			}
		}
	}
}
