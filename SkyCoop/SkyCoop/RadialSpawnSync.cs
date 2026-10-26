using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SkyCoop;

// Sticks, stones, limbs and similar loose items come from the game's radial spawners: each spawner places its items
// at random points around itself when a player comes near, with checks against what the camera sees. Every player
// therefore had a different set. In co-op the spawners use one random sequence per spawner (scene seed, spawner
// position and how many items it already holds), so both players place the same items in
// the same spots; saved spawner state is ignored so both start from the same point. Pickups of these items are
// matched by name and distance (see MyMod.RemovePickedGear) and kept removed for the rest of the scene visit.
public static class RadialSpawnSync
{
	private struct Picked
	{
		public string Scene;

		public string Gear;

		public Vector3 Position;
	}

	[HarmonyPatch(typeof(RadialObjectSpawner), "Deserialize")]
	private static class SkipSavedState
	{
		private static bool Prefix(ref bool __result)
		{
			if (!Active)
			{
				return true;
			}
			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(RadialObjectSpawner), "RollRandomNumToSpawn")]
	private static class SeedNumToSpawn
	{
		private static void Prefix(RadialObjectSpawner __instance, ref Random.State? __state)
		{
			__state = Seed(__instance, 0x5bd1e995);
		}

		private static void Postfix(Random.State? __state)
		{
			Restore(__state);
		}
	}

	[HarmonyPatch(typeof(RadialObjectSpawner), "SpawnAttemptAllNoVisChecks")]
	private static class SeedSpawnAll
	{
		private static void Prefix(RadialObjectSpawner __instance, ref Random.State? __state)
		{
			int held = (__instance.m_Spawns != null) ? __instance.m_Spawns.Count : 0;
			int pending = (__instance.m_PendingSpawns != null) ? __instance.m_PendingSpawns.Count : 0;
			__state = Seed(__instance, (held + pending) * 7919 + 17);
		}

		private static void Postfix(Random.State? __state)
		{
			Restore(__state);
		}
	}

	// Single attempts are seeded by how many items the spawner already holds, so a retry after a failed camera check
	// tries the same spot again and every player ends up with the item in the same place, whenever it appears.
	[HarmonyPatch(typeof(RadialObjectSpawner), "SpawnObjectAttempt")]
	private static class SeedSingleAttempt
	{
		private static void Prefix(RadialObjectSpawner __instance, ref Random.State? __state)
		{
			int held = (__instance.m_Spawns != null) ? __instance.m_Spawns.Count : 0;
			int pending = (__instance.m_PendingSpawns != null) ? __instance.m_PendingSpawns.Count : 0;
			__state = Seed(__instance, (held + pending) * 7919 + 17);
			if (__state.HasValue)
			{
				SeededAttempts++;
				SeededSpawners.Add(__instance.GetInstanceID());
			}
		}

		private static void Postfix(Random.State? __state)
		{
			Restore(__state);
		}
	}

	private static int SeededAttempts;

	private static readonly HashSet<int> SeededSpawners = new HashSet<int>();

	private static float nextReport;

	private static readonly List<Picked> PickedThisVisit = new List<Picked>();

	private static float nextSweep;

	public static bool Active
	{
		get
		{
			if (!MyMod.InOnline())
			{
				return !string.IsNullOrEmpty(MyMod.MyLobby);
			}
			return true;
		}
	}

	private static Random.State? Seed(RadialObjectSpawner spawner, int salt)
	{
		if (!Active || spawner == null)
		{
			return null;
		}
		Vector3 p = spawner.transform.position;
		string key = p.x.ToString("F1", CultureInfo.InvariantCulture) + "," + p.y.ToString("F1", CultureInfo.InvariantCulture) + "," + p.z.ToString("F1", CultureInfo.InvariantCulture);
		Random.State state = Random.state;
		Random.InitState(GameManager.GetRandomSeed(Shared.StableHash(key) ^ salt));
		return state;
	}

	private static void Restore(Random.State? state)
	{
		if (state.HasValue)
		{
			Random.state = state.Value;
		}
	}

	// A live pickup by any player; radial items reappear from the seeded sequence, so they are removed again here.
	public static void RememberPickup(string gearName, Vector3 position)
	{
		if (!Active || string.IsNullOrEmpty(gearName) || position == Vector3.zero)
		{
			return;
		}
		PickedThisVisit.Add(new Picked
		{
			Scene = MyMod.level_guid,
			Gear = gearName,
			Position = position
		});
	}

	private static string reportScene = "";

	private static float looseReportAt = -1f;

	private static int looseReports;

	// Diagnostics for comparing two players' logs: where loose sticks and stones come from and a fingerprint of
	// their positions. Equal fingerprints mean both players have the same set.
	private static void ReportLooseGear()
	{
		if (MyMod.level_guid != reportScene)
		{
			reportScene = MyMod.level_guid;
			looseReports = 0;
			looseReportAt = Time.realtimeSinceStartup + 45f;
		}
		if (looseReportAt < 0f || Time.realtimeSinceStartup < looseReportAt || GearManager.m_Gear == null || Shared.IsMainMenuScene(MyMod.level_name))
		{
			return;
		}
		looseReports++;
		looseReportAt = (looseReports < 3) ? (Time.realtimeSinceStartup + 135f) : (-1f);
		int total = 0;
		int radial = 0;
		List<string> keys = new List<string>();
		for (int i = 0; i < GearManager.m_Gear.Count; i++)
		{
			GearItem gearItem = GearManager.m_Gear[i];
			if (gearItem == null || gearItem.m_BeenInPlayerInventory || gearItem.m_InsideContainer || !gearItem.gameObject.activeInHierarchy)
			{
				continue;
			}
			string name = gearItem.GetGearName();
			if (name != "GEAR_Stick" && name != "GEAR_Stone" && name != "GEAR_Limb" && name != "GEAR_Softwood" && name != "GEAR_Hardwood" && name != "GEAR_ReclaimedWoodB")
			{
				continue;
			}
			total++;
			if (gearItem.transform.parent != null && gearItem.transform.parent.GetComponent<RadialObjectSpawner>() != null)
			{
				radial++;
			}
			Vector3 p = gearItem.transform.position;
			keys.Add(name + Mathf.RoundToInt(p.x * 2f) + "," + Mathf.RoundToInt(p.z * 2f));
		}
		keys.Sort(System.StringComparer.Ordinal);
		MelonLogger.Msg("[Loose gear] scene=" + MyMod.level_name + " items=" + total + " radial=" + radial + " fingerprint=" + Shared.StableHash(string.Join(";", keys)) + (Active ? "" : " (sync off)"));
	}

	public static void Tick()
	{
		ReportLooseGear();
		if (SeededAttempts > 0 && Time.realtimeSinceStartup >= nextReport)
		{
			nextReport = Time.realtimeSinceStartup + 120f;
			MelonLogger.Msg("[Radial sync] seeded attempts=" + SeededAttempts + " spawners=" + SeededSpawners.Count + " scene=" + MyMod.level_name);
		}
		if (PickedThisVisit.Count == 0 || Time.realtimeSinceStartup < nextSweep)
		{
			return;
		}
		nextSweep = Time.realtimeSinceStartup + 1f;
		PickedThisVisit.RemoveAll((Picked p) => p.Scene != MyMod.level_guid);
		if (!Active || PickedThisVisit.Count == 0 || GearManager.m_Gear == null)
		{
			return;
		}
		for (int i = 0; i < GearManager.m_Gear.Count; i++)
		{
			GearItem gearItem = GearManager.m_Gear[i];
			if (gearItem == null || gearItem.m_BeenInPlayerInventory || gearItem.m_InsideContainer || !gearItem.gameObject.activeInHierarchy || gearItem.transform.parent == null)
			{
				continue;
			}
			RadialObjectSpawner spawner = gearItem.transform.parent.GetComponent<RadialObjectSpawner>();
			if (spawner == null)
			{
				continue;
			}
			string gearName = gearItem.GetGearName();
			Vector3 position = gearItem.transform.position;
			foreach (Picked picked in PickedThisVisit)
			{
				if (string.Equals(picked.Gear, gearName, System.StringComparison.OrdinalIgnoreCase) && Vector3.Distance(picked.Position, position) < 0.75f)
				{
					spawner.RemoveFromSpawns(gearItem.gameObject);
					gearItem.gameObject.SetActive(value: false);
					MelonLogger.Msg("[Radial sync] Removed respawned picked " + gearName);
					break;
				}
			}
		}
	}
}
