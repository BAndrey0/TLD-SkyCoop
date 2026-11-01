using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Drive the replacement population controller independently of native re-roll frequency.
public static class WildlifeSpawnerAudit
{
	public static Comps.SpawnRegionSimple ResolveRegion(string guid)
	{
		var regions = GameManager.GetSpawnRegionManager()?.m_SpawnRegions;
		if (regions == null) return null;
		foreach (var region in regions)
			if (region != null && region.GetComponent<ObjectGuid>()?.Get() == guid)
				return region.GetComponent<Comps.SpawnRegionSimple>();
		return null;
	}
	private static float nextTick, nextReport;
	public static void Tick()
	{
		if (!MyMod.InOnline() || Time.realtimeSinceStartup < nextTick) return;
		nextTick = Time.realtimeSinceStartup + 2;
		bool report = Time.realtimeSinceStartup >= nextReport;
		if (!MyMod.CanSpawnWildlifeInCurrentScene())
		{
			if(report){nextReport=Time.realtimeSinceStartup+30;MelonLogger.Msg($"[Wildlife gate] scene={MyMod.level_guid} ready={MyMod.WildlifeSceneReady()} controller={MyMod.AnimalsController} grant={MyMod.WildlifeAuthorityScene}");}
			return;
		}
		var manager = GameManager.GetSpawnRegionManager();
		if (manager == null || manager.m_SpawnRegions == null) return;
		int active = 0, nearby = 0, population = 0, target = 0;
		foreach (var region in manager.m_SpawnRegions)
		{
			if (region == null || !region.isActiveAndEnabled) continue;
			active++;
			var guid = region.GetComponent<ObjectGuid>();
			if (guid == null || string.IsNullOrEmpty(guid.Get())) continue;
			var sync = region.GetComponent<Comps.SpawnRegionSimple>();
			if (sync == null)
			{
				sync = region.gameObject.AddComponent<Comps.SpawnRegionSimple>();
				sync.m_Region = region;
				sync.m_GUID = guid.Get();
			}
			try
			{
				sync.UpdateFromManager();
				if (report && !sync.m_RolledToBeDisabled && !region.SpawningSupppressedByExperienceMode() && MyMod.AnyOneClose(region.m_Radius + MyMod.MinimalDistanceForSpawn, region.m_Center) != -1)
				{
					nearby++;
					population += MyMod.CountKnownWildlifeInRegion(sync.m_GUID, sync.m_Animals);
					target += MyMod.CalculateTargetPopulation(region);
				}
			}
			catch (System.Exception ex)
			{
				if (report) MelonLogger.Warning("[Wildlife spawn] region=" + sync.m_GUID + " " + ex.Message);
			}
		}
		if (report)
		{
			nextReport = Time.realtimeSinceStartup + 30;
			MelonLogger.Msg($"[Wildlife spawn] scene={MyMod.level_guid} regions={manager.m_SpawnRegions.Count} active={active} nearby={nearby} live={population} target={target}");
		}
	}
}
