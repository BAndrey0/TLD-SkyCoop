using System.Collections.Generic;
using GameServer;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

public class SafeZoneManager
{
	public class SafeZoneSpace
	{
		public Vector3 m_Center = new Vector3(0f, 0f, 0f);

		public float m_Radius = 1f;

		public SafeZoneSpace(Vector3 center, float radius)
		{
			m_Center = center;
			m_Radius = radius;
		}
	}

	public static List<string> SafeScenes = new List<string>();

	public static Dictionary<string, List<SafeZoneSpace>> SafeZones = new Dictionary<string, List<SafeZoneSpace>>();

	public static bool isReady = false;

	public static void DebugRenderZones()
	{
		if (!SafeZones.TryGetValue(MyMod.level_guid, out var value))
		{
			return;
		}
		foreach (SafeZoneSpace item in value)
		{
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			Object.Destroy(gameObject.GetComponent<SphereCollider>());
			gameObject.transform.position = item.m_Center;
			gameObject.transform.localScale = new Vector3(item.m_Radius * 2f, item.m_Radius * 2f, item.m_Radius * 2f);
		}
	}

	public static void InitZones()
	{
		AddSafeZone("TracksRegion", new Vector3(582.3058f, 198.95f, 560.3252f), 130f);
		AddSafeScene("MaintenanceShedA");
		AddSafeZone("MountainTownRegion", new Vector3(1104.269f, 260.8032f, 1716.49f), 137f);
		AddSafeScene("GreyMothersHouseA");
		AddSafeZone("CanneryRegion", new Vector3(-389.7225f, 31.96249f, -597.4935f), 50f);
		AddSafeScene("MaintenanceShedB");
		AddSafeZone("CanneryRegion", new Vector3(-71.42158f, 138.4941f, 23.34852f), 30f);
		AddSafeScene("RadioControlHutB");
		AddSafeZone("LakeRegion", new Vector3(1671.796f, 35.21933f, 1341.215f), 70f);
		AddSafeScene("Dam");
		AddSafeScene("DamTransitionZone");
		AddSafeZone("LakeRegion", new Vector3(1019.397f, 26.49471f, 444.3498f), 70f);
		AddSafeScene("CampOffice");
		AddSafeZone("CoastalRegion", new Vector3(766.1833f, 24.32027f, 643.6183f), 50f);
		AddSafeScene("QuonsetGasStation");
		AddSafeZone("WhalingStationRegion", new Vector3(695.0294f, 38.12838f, 769.0448f), 100f);
		AddSafeScene("LighthouseA");
		AddSafeZone("WhalingStationRegion", new Vector3(1032.46f, 23.80079f, 998.7527f), 70f);
		AddSafeScene("WhalingShipA");
		AddSafeZone("RuralRegion", new Vector3(2317.808f, 53.10027f, 2243.66f), 230f);
		AddSafeScene("CommunityHallA");
		AddSafeZone("RuralRegion", new Vector3(1453.856f, 48.80129f, 1028.714f), 73f);
		AddSafeScene("FarmHouseA");
		AddSafeScene("FarmHouseABasement");
		AddSafeZone("BlackrockRegion", new Vector3(-185.7764f, 225.7132f, 6.317502f), 50f);
		AddSafeScene("BlackrockPrisonSurvivalZone");
		AddSafeScene("BlackrockInteriorASurvival");
		AddSafeZone("CrashMountainRegion", new Vector3(888.7276f, 160.72f, 342.1175f), 50f);
	}

	public static bool SceneIsSafe(string Scene)
	{
		return SafeScenes.Contains(Scene);
	}

	public static bool InsideSafeZone(string Scene, Vector3 Position)
	{
		if (SafeZones.TryGetValue(Scene, out var value))
		{
			foreach (SafeZoneSpace item in value)
			{
				if (Vector3.Distance(Position, item.m_Center) <= item.m_Radius)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static void UpdatePlayersSafeZoneStatus()
	{
		if (!isReady)
		{
			InitZones();
			isReady = true;
		}
		int num = 0;
		if ((bool)GameManager.m_PlayerObject)
		{
			bool flag = SceneIsSafe(MyMod.level_guid) || InsideSafeZone(MyMod.level_guid, GameManager.GetPlayerTransform().position);
			if (flag != MyMod.LastSafeStatus)
			{
				MyMod.LastSafeStatus = flag;
				if (flag)
				{
					HUDMessage.AddMessage("You are entering safe zone!");
				}
				else
				{
					HUDMessage.AddMessage("You are leaving safe zone!");
				}
			}
		}
		foreach (DataStr.MultiPlayerClientData playersDatum in MyMod.playersData)
		{
			bool flag2 = false;
			string message = "You are leaving safe zone!";
			if (SceneIsSafe(playersDatum.m_LevelGuid) || InsideSafeZone(playersDatum.m_LevelGuid, playersDatum.m_Position))
			{
				flag2 = true;
				message = "You are entering safe zone!";
			}
			if (playersDatum.m_IsSafe != flag2)
			{
				playersDatum.m_IsSafe = flag2;
				ServerSend.ADDHUDMESSAGE(num, message);
			}
			num++;
		}
	}

	public static void AddSafeZone(string Scene, Vector3 Center, float Radius)
	{
		SafeZoneSpace item = new SafeZoneSpace(Center, Radius);
		List<SafeZoneSpace> list;
		if (!SafeZones.ContainsKey(Scene))
		{
			list = new List<SafeZoneSpace>();
			list.Add(item);
			SafeZones.Add(Scene, list);
		}
		else if (SafeZones.TryGetValue(Scene, out list))
		{
			list.Add(item);
			SafeZones.Remove(Scene);
			SafeZones.Add(Scene, list);
		}
	}

	public static void AddSafeScene(string Scene)
	{
		if (!SceneIsSafe(Scene))
		{
			SafeScenes.Add(Scene);
		}
	}
}
