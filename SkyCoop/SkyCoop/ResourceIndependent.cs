using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GameServer;
using TinyJSON;
using UnityEngine;

namespace SkyCoop;

public class ResourceIndependent
{
	public static Dictionary<string, string> Templates = new Dictionary<string, string>();

	public static bool IsInit = false;

	public static DataStr.BufferedGearSpawners GearSpawners = new DataStr.BufferedGearSpawners();

	public static bool GearSpawnersReady = false;

	public static void Log(string TXT, Shared.LoggerColor Color = Shared.LoggerColor.White)
	{
	}

	public static void LoadGearSpawners()
	{
		if (GearSpawnersReady)
		{
			return;
		}
		string text = "";
		if (Directory.Exists(MPSaveManager.GetBaseDirectory() + "Mods") && File.Exists(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "IndependantGearSpawners.json"))
		{
			byte[] bytes = File.ReadAllBytes(MPSaveManager.GetBaseDirectory() + "Mods" + MPSaveManager.GetSeparator() + "IndependantGearSpawners.json");
			text = Encoding.UTF8.GetString(bytes);
			if (!string.IsNullOrEmpty(text))
			{
				text = MPSaveManager.VectorsFixUp(text);
			}
		}
		else if (File.Exists(MPSaveManager.GetBaseDirectory() + "IndependantGearSpawners.json"))
		{
			byte[] bytes2 = File.ReadAllBytes(MPSaveManager.GetBaseDirectory() + "IndependantGearSpawners.json");
			text = Encoding.UTF8.GetString(bytes2);
			if (!string.IsNullOrEmpty(text))
			{
				text = MPSaveManager.VectorsFixUp(text);
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			GearSpawners = JSON.Load(text).Make<DataStr.BufferedGearSpawners>();
			Log("IndependantGearSpawners loaded!", Shared.LoggerColor.Green);
			GearSpawnersReady = true;
		}
	}

	public static void Init()
	{
		if (!IsInit)
		{
			string jsonTemplate = "{\"m_Position\":[],\"m_Rotation\":[],\"m_InstanceIDProxy\":-1,\"m_CurrentHPProxy\":100,\"m_NormalizedCondition\":1,\"m_RolledSpawnChanceProxy\":true,\"m_WeightKG\":0.1,\"m_GearItemSaveVersion\":4,\"m_InspectSerialized\":\"{}\"}";
			string jsonTemplate2 = "{\"m_Position\":[],\"m_Rotation\":[],\"m_InstanceIDProxy\":-1,\"m_CurrentHPProxy\":100,\"m_NormalizedCondition\":1,\"m_RolledSpawnChanceProxy\":true,\"m_ObjectGuidSerialized\":\"#KEYNAME_#KEYSEED\",\"m_WeightKG\":0.1,\"m_GearItemSaveVersion\":4,\"m_InspectSerialized\":\"{}\"}";
			AddTemplate("gear_sclockpick", jsonTemplate);
			AddTemplate("gear_scmetalblanksmall", jsonTemplate);
			AddTemplate("gear_scdoorkeytemp", jsonTemplate);
			AddTemplate("gear_scdoorkeyleadtemp", jsonTemplate);
			AddTemplate("gear_scdoorkey", jsonTemplate2);
			AddTemplate("gear_scdoorkeylead", jsonTemplate2);
			AddTemplate("gear_rabbitcarcass", "{\"m_Position\":[],\"m_Rotation\":[],\"m_InstanceIDProxy\":-1,\"m_CurrentHPProxy\":9.693987,\"m_NormalizedCondition\":0.9693987,\"m_RolledSpawnChanceProxy\":true,\"m_WeightKG\":3,\"m_BodyHarvestSerialized\":\"{\\\"m_MeatAvailableKG\\\":#KGVAL,\\\"m_HideAvailableUnits\\\":1,\\\"m_GutAvailableUnits\\\":1,\\\"m_Condition\\\":96.93987,\\\"m_HoursPlayed\\\":5.815399,\\\"m_QuarterBagWasteMultiplier\\\":1,\\\"m_DamageSide\\\":\\\"DamageSideLeft\\\"}\",\"m_GearItemSaveVersion\":4,\"m_InspectSerialized\":\"{}\"}");
			AddTemplate("gear_snare_3", "{\"m_HoursPlayed\":0,\"m_Position\":[],\"m_Rotation\":[],\"m_InstanceIDProxy\":-1,\"m_CurrentHPProxy\":100,\"m_NormalizedCondition\":1,\"m_BeenInPlayerInventoryProxy\":true,\"m_BeenInspectedProxy\":true,\"m_HasBeenOwnedByPlayer\":true,\"m_SnareItemSerialized\":\"{\\\"m_HoursPlayed\\\":0,\\\"m_HoursAtLastRoll\\\":0,\\\"m_State\\\":\\\"WithRabbit\\\"}\",\"m_WeightKG\":0.3,\"m_GearItemSaveVersion\":4,\"m_InspectSerialized\":\"{}\"}");
			AddTemplate("gear_snare_2", "{\"m_HoursPlayed\":0,\"m_Position\":[],\"m_Rotation\":[],\"m_InstanceIDProxy\":-1,\"m_CurrentHPProxy\":100,\"m_NormalizedCondition\":1,\"m_BeenInPlayerInventoryProxy\":true,\"m_BeenInspectedProxy\":true,\"m_HasBeenOwnedByPlayer\":true,\"m_SnareItemSerialized\":\"{\\\"m_HoursPlayed\\\":0,\\\"m_HoursAtLastRoll\\\":0,\\\"m_State\\\":\\\"Broken\\\"}\",\"m_WeightKG\":0.3,\"m_GearItemSaveVersion\":4,\"m_InspectSerialized\":\"{}\"}");
			AddTemplate("gear_scwoodform", jsonTemplate);
			AddTemplate("gear_scwoodformb", jsonTemplate);
			LoadGearSpawners();
			IsInit = true;
		}
	}

	public static void AddTemplate(string GearName, string JsonTemplate)
	{
		if (!Templates.ContainsKey(GearName))
		{
			Templates.Add(GearName, JsonTemplate);
		}
	}

	public static string OverrideTransform(string JSON, Vector3 pos, Quaternion rot)
	{
		string text = "[" + pos.x.ToString(CultureInfo.InvariantCulture) + "," + pos.y.ToString(CultureInfo.InvariantCulture) + "," + pos.z.ToString(CultureInfo.InvariantCulture) + "]";
		string text2 = "[" + rot.x.ToString(CultureInfo.InvariantCulture) + "," + rot.y.ToString(CultureInfo.InvariantCulture) + "," + rot.z.ToString(CultureInfo.InvariantCulture) + "," + rot.w.ToString(CultureInfo.InvariantCulture) + "]";
		JSON = JSON.Replace("\"m_Position\":[]", "\"m_Position\":" + text);
		JSON = JSON.Replace("\"m_Rotation\":[]", "\"m_Rotation\":" + text2);
		return JSON;
	}

	public static string OverrideKey(string JSON, string KeyName, string KeySeed)
	{
		string newValue = KeyName + "_" + KeySeed;
		JSON = JSON.Replace("#KEYNAME_#KEYSEED", newValue);
		return JSON;
	}

	public static string GetRabbit(Vector3 pos, Quaternion rot)
	{
		if (Templates.TryGetValue("gear_rabbitcarcass", out var value))
		{
			value = OverrideTransform(value, pos, rot);
			value = value.Replace("#KGVAL", Shared.GetBodyHarvestUnits("WILDLIFE_Rabbit").m_Meat.ToString(CultureInfo.InvariantCulture));
			Log("gear_rabbitcarcass JSON:");
			Log(value);
			return value;
		}
		return "";
	}

	public static string GetSnare(Vector3 pos, Quaternion rot, int State)
	{
		if (Templates.TryGetValue("gear_snare_" + State, out var value))
		{
			value = OverrideTransform(value, pos, rot);
			Log("gear_snare JSON:");
			Log(value);
			return value;
		}
		return "";
	}

	public static string GetLocksmithGear(string GearName, Vector3 pos, Quaternion rot, string KeyName = "", string KeySeed = "")
	{
		GearName = GearName.ToLower();
		if (Templates.TryGetValue(GearName, out var value))
		{
			value = OverrideTransform(value, pos, rot);
			if (!string.IsNullOrEmpty(KeyName) && !string.IsNullOrEmpty(KeySeed))
			{
				value = OverrideKey(value, KeyName, KeySeed);
			}
			Log(GearName + " JSON:");
			Log(value);
			return value;
		}
		return "";
	}

	public static void SpawnAllGearsOnScene(string Scene, string LayerAffix = "", string PrefabOverride = "")
	{
		LoadGearSpawners();
		if (!GearSpawnersReady)
		{
			return;
		}
		string key = Scene;
		if (!string.IsNullOrEmpty(LayerAffix))
		{
			key = Scene + "_" + LayerAffix;
		}
		if (!GearSpawners.GearSpawners.TryGetValue(key, out var value))
		{
			return;
		}
		foreach (DataStr.PrefabSpawnDescriptor prefabSpawn in value.PrefabSpawns)
		{
			foreach (DataStr.GearSpawnerElement gear in prefabSpawn.Gears)
			{
				string text = PrefabOverride;
				if (string.IsNullOrEmpty(text))
				{
					text = gear.GearName;
				}
				SpawnGear(text, Scene, gear.Position, gear.Rotation, 0, "PrefabSpawnDescriptor");
			}
		}
		foreach (DataStr.RandomSpawnObjectDescriptor randomSpawn in value.RandomSpawns)
		{
			foreach (DataStr.GearSpawnerElement gear2 in randomSpawn.Gears)
			{
				string text2 = PrefabOverride;
				if (string.IsNullOrEmpty(text2))
				{
					text2 = gear2.GearName;
				}
				SpawnGear(text2, Scene, gear2.Position, gear2.Rotation, 0, "RandomSpawnObjectDescriptor");
			}
		}
	}

	public static List<int> CalculateSetWeights(List<DataStr.GearSpawnerElement> PrefabList, out int sumOfAllWeights)
	{
		sumOfAllWeights = 0;
		List<int> list = new List<int>();
		for (int i = 0; i < PrefabList.Count; i++)
		{
			DataStr.GearSpawnerElement gearSpawnerElement = PrefabList[i];
			list.Add(gearSpawnerElement.Weight);
			sumOfAllWeights += gearSpawnerElement.Weight;
		}
		return list;
	}

	public static void SpawnSomeGearsOnScene(string Scene, string LayerAffix = "", string PrefabOverride = "")
	{
		LoadGearSpawners();
		if (!GearSpawnersReady)
		{
			return;
		}
		string key = Scene;
		if (!string.IsNullOrEmpty(LayerAffix))
		{
			key = Scene + "_" + LayerAffix;
		}
		System.Random random = new System.Random();
		if (!GearSpawners.GearSpawners.TryGetValue(key, out var value))
		{
			return;
		}
		foreach (DataStr.PrefabSpawnDescriptor prefabSpawn in value.PrefabSpawns)
		{
			int num = random.Next(0, 100);
			if (prefabSpawn.ChanceOfNoSpawn < num)
			{
				continue;
			}
			int num2 = random.Next(prefabSpawn.Min, prefabSpawn.Max);
			if (prefabSpawn.Min == 0 || prefabSpawn.Max == 0 || num2 == 0)
			{
				continue;
			}
			List<int> list = new List<int>();
			int num3 = 20;
			while (num2 != list.Count && num3 != 0)
			{
				int item = random.Next(0, prefabSpawn.Gears.Count);
				if (!list.Contains(item))
				{
					list.Add(item);
				}
				else
				{
					num3--;
				}
			}
			foreach (int item3 in list)
			{
				DataStr.GearSpawnerElement gearSpawnerElement = prefabSpawn.Gears[item3];
				string text = PrefabOverride;
				if (string.IsNullOrEmpty(text))
				{
					text = gearSpawnerElement.GearName;
				}
				SpawnGear(text, Scene, gearSpawnerElement.Position, gearSpawnerElement.Rotation);
			}
		}
		foreach (DataStr.RandomSpawnObjectDescriptor randomSpawn in value.RandomSpawns)
		{
			int num4 = 0;
			if (Shared.IsPilgrimExpereinceMode())
			{
				num4 = randomSpawn.SpawnOnPiligrim;
			}
			else if (Shared.IsVoyageurExpereinceMode())
			{
				num4 = randomSpawn.SpawnOnVoyageur;
			}
			else if (Shared.IsStalkerExpereinceMode())
			{
				num4 = randomSpawn.SpawnOnStalker;
			}
			else if (Shared.IsInterloperExpereinceMode())
			{
				num4 = randomSpawn.SpawnOnInterloper;
			}
			List<int> list2 = new List<int>();
			int num5 = 20;
			while (num4 != list2.Count && num5 != 0)
			{
				int item2 = random.Next(0, randomSpawn.Gears.Count);
				if (!list2.Contains(item2))
				{
					list2.Add(item2);
				}
				else
				{
					num5--;
				}
			}
			foreach (int item4 in list2)
			{
				DataStr.GearSpawnerElement gearSpawnerElement2 = randomSpawn.Gears[item4];
				if ((float)random.Next(0, 100) < gearSpawnerElement2.Chance)
				{
					string text2 = PrefabOverride;
					if (string.IsNullOrEmpty(text2))
					{
						text2 = gearSpawnerElement2.GearName;
					}
					SpawnGear(text2, Scene, gearSpawnerElement2.Position, gearSpawnerElement2.Rotation);
				}
			}
		}
	}

	public static void SpawnGear(string Prefab, string Scene, Vector3 PlaceV3, Quaternion Rotation, int Variant = 0, string DroppedName = "", string PhotoGUID = "", string ExpeditionNote = "")
	{
		if (!string.IsNullOrEmpty(Prefab))
		{
			DataStr.SlicedJsonDroppedGear slicedJsonDroppedGear = new DataStr.SlicedJsonDroppedGear();
			slicedJsonDroppedGear.m_GearName = Prefab.ToLower();
			slicedJsonDroppedGear.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
			slicedJsonDroppedGear.m_Extra.m_Dropper = DroppedName;
			slicedJsonDroppedGear.m_Extra.m_GearName = slicedJsonDroppedGear.m_GearName;
			slicedJsonDroppedGear.m_Extra.m_Variant = Variant;
			slicedJsonDroppedGear.m_Extra.m_PhotoGUID = PhotoGUID;
			slicedJsonDroppedGear.m_Extra.m_ExpeditionNote = ExpeditionNote;
			int vectorHash = Shared.GetVectorHash(PlaceV3);
			int quaternionHash = Shared.GetQuaternionHash(Rotation);
			int hashCode = Shared.StableHash(Scene);
			int num = vectorHash + quaternionHash + hashCode;
			DataStr.DroppedGearItemDataPacket droppedGearItemDataPacket = new DataStr.DroppedGearItemDataPacket();
			droppedGearItemDataPacket.m_Extra = slicedJsonDroppedGear.m_Extra;
			droppedGearItemDataPacket.m_GearID = -1;
			droppedGearItemDataPacket.m_Hash = num;
			droppedGearItemDataPacket.m_LevelGUID = Scene;
			droppedGearItemDataPacket.m_Position = PlaceV3;
			droppedGearItemDataPacket.m_Rotation = Rotation;
			slicedJsonDroppedGear.m_Json = "";
			Dictionary<int, DataStr.DroppedGearItemDataPacket> dictionary = MPSaveManager.LoadDropVisual(Scene);
			if (dictionary == null || !dictionary.ContainsKey(num))
			{
				MPSaveManager.AddGearData(Scene, num, slicedJsonDroppedGear);
				MPSaveManager.AddGearVisual(Scene, droppedGearItemDataPacket);
				Shared.FakeDropItem(droppedGearItemDataPacket, JustLoad: true);
				ServerSend.DROPITEM(0, droppedGearItemDataPacket, toEveryOne: true);
			}
		}
	}
}
