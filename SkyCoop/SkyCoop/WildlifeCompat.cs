using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2Cpp;
using Il2CppTLD.AI;
using Il2CppTLD.AddressableAssets;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class WildlifeCompat
{
	private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

	public static readonly Dictionary<string, object> RegionSpawnDiagnostics = new Dictionary<string, object>();

	public static void RecordRegionSpawn(string regionGuid, SpawnRegion region, BaseAi animal, string stage)
	{
		GameObject gameObject = ((animal != null) ? animal.gameObject : null);
		Transform transform = ((animal != null && animal.m_MoveAgent != null) ? animal.m_MoveAgent.transform : null);
		Transform transform2 = ((transform != null) ? transform : ((gameObject != null) ? gameObject.transform : null));
		Transform transform3 = ((transform2 != null) ? transform2.parent : null);
		string key = regionGuid + ":" + stage;
		if (!RegionSpawnDiagnostics.ContainsKey(key) && RegionSpawnDiagnostics.Count >= 64)
		{
			RegionSpawnDiagnostics.Clear();
		}
		RegionSpawnDiagnostics[key] = new
		{
			regionGuid = regionGuid,
			stage = stage,
			returnedAnimal = (animal != null),
			name = ((gameObject != null) ? gameObject.name : ""),
			instanceId = ((gameObject != null) ? gameObject.GetInstanceID() : 0),
			activeSelf = (gameObject != null && gameObject.activeSelf),
			activeInHierarchy = (gameObject != null && gameObject.activeInHierarchy),
			moveAgentPresent = (transform != null),
			rootActive = (transform2 != null && transform2.gameObject.activeInHierarchy),
			parentName = ((transform3 != null) ? transform3.name : ""),
			parentActive = (transform3 != null && transform3.gameObject.activeInHierarchy),
			regionActive = (region != null && region.gameObject.activeInHierarchy),
			spawnListPresent = (region != null && region.m_SpawnList != null),
			spawnListCount = (region != null && region.m_SpawnList != null ? region.m_SpawnList.Count : 0),
			regionSpawnCount = ((region != null && region.m_Spawns != null) ? region.m_Spawns.Count : 0),
			registeredNativeCount = ((BaseAiManager.m_BaseAis != null) ? BaseAiManager.m_BaseAis.Count : 0)
		};
		if (stage == "detached-inactive-region")
		{
			MelonLogger.Msg("[SkyCoop] Detached wildlife agent from inactive region: " + ((gameObject != null) ? gameObject.name : "null"));
		}
	}

	public static string PrefabNameFromInstance(string instanceName)
	{
		string input = GameCompat.StripCloneSuffix(instanceName);
		input = Regex.Replace(input, "(?:_| )\\(?[0-9]+\\)?$", "");
		if (string.Equals(input, "WILDLIFE_Wolf_Aurora", StringComparison.OrdinalIgnoreCase))
		{
			return "WILDLIFE_Wolf_aurora";
		}
		if (string.Equals(input, "WILDLIFE_Bear_Aurora", StringComparison.OrdinalIgnoreCase))
		{
			return "WILDLIFE_Bear_aurora";
		}
		return input;
	}

	public static void StripAdditionalAi(GameObject animal)
	{
		if (!(animal == null))
		{
			AiDoe component = animal.GetComponent<AiDoe>();
			if (component != null)
			{
				UnityEngine.Object.Destroy(component);
			}
			AiTimberwolf component2 = animal.GetComponent<AiTimberwolf>();
			if (component2 != null)
			{
				UnityEngine.Object.Destroy(component2);
			}
		}
	}

	public static void SetAdditionalAiEnabled(GameObject animal, bool enabled)
	{
		if (!(animal == null))
		{
			AiDoe component = animal.GetComponent<AiDoe>();
			if (component != null)
			{
				component.enabled = enabled;
			}
			AiTimberwolf component2 = animal.GetComponent<AiTimberwolf>();
			if (component2 != null)
			{
				component2.enabled = enabled;
			}
		}
	}

	public static GameObject GetPrefab(string prefabName)
	{
		if (string.IsNullOrEmpty(prefabName))
		{
			return null;
		}
		if (string.Equals(prefabName, "WILDLIFE_Wolf_Aurora", StringComparison.OrdinalIgnoreCase))
		{
			prefabName = "WILDLIFE_Wolf_aurora";
		}
		else if (string.Equals(prefabName, "WILDLIFE_Bear_Aurora", StringComparison.OrdinalIgnoreCase))
		{
			prefabName = "WILDLIFE_Bear_aurora";
		}
		if (Prefabs.TryGetValue(prefabName, out var value) && value != null)
		{
			return value;
		}
		value = Resources.Load<GameObject>(prefabName);
		if (value == null)
		{
			GameCompat.TryLoadAsset(prefabName, out value);
		}
		if (value != null)
		{
			Prefabs[prefabName] = value;
			MelonLogger.Msg("[SkyCoop] Resolved wildlife prefab " + prefabName);
		}
		return value;
	}
}
