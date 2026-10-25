using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.Scenes;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCoop;

public static class RegionCompat
{
	private static readonly Dictionary<Shared.GameRegion, RegionSpecification> s_SpecCache = new Dictionary<Shared.GameRegion, RegionSpecification>();

	public static Shared.GameRegion FromSceneSet(SceneSet set)
	{
		if (set == null)
		{
			return Shared.GameRegion.RandomRegion;
		}
		Shared.GameRegion regionForSceneName = Shared.GetRegionForSceneName(set.name);
		if (regionForSceneName != Shared.GameRegion.RandomRegion)
		{
			return regionForSceneName;
		}
		try
		{
			return Shared.GetRegionForSceneName(set.RegionGroupName);
		}
		catch
		{
			return Shared.GameRegion.RandomRegion;
		}
	}

	public static Shared.GameRegion FromSceneName(string sceneName)
	{
		return Shared.GetRegionForSceneName(sceneName);
	}

	public static Shared.GameRegion FromRegionSpecification(RegionSpecification spec)
	{
		if (spec == null)
		{
			return Shared.GameRegion.RandomRegion;
		}
		Shared.GameRegion regionForSceneName = Shared.GetRegionForSceneName(spec.name);
		if (regionForSceneName != Shared.GameRegion.RandomRegion)
		{
			return regionForSceneName;
		}
		try
		{
			if (spec.m_RegionDescription != null)
			{
				return Shared.GetRegionForSceneName(spec.m_RegionDescription.m_LocalizationID);
			}
		}
		catch
		{
		}
		return Shared.GameRegion.RandomRegion;
	}

	public static RegionSpecification FindRegionSpecification(Shared.GameRegion region)
	{
		if (s_SpecCache.TryGetValue(region, out var value) && value != null)
		{
			return value;
		}
		try
		{
			foreach (RegionSpecification item in Resources.FindObjectsOfTypeAll<RegionSpecification>())
			{
				Shared.GameRegion gameRegion = FromRegionSpecification(item);
				if (gameRegion != Shared.GameRegion.RandomRegion)
				{
					s_SpecCache[gameRegion] = item;
				}
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[SkyCoop] Could not enumerate region specifications: " + ex.Message);
		}
		s_SpecCache.TryGetValue(region, out value);
		return value;
	}

	public static string GetLocalizedName(Shared.GameRegion region)
	{
		RegionSpecification regionSpecification = FindRegionSpecification(region);
		if (regionSpecification != null)
		{
			string localizedRegionDescription = regionSpecification.LocalizedRegionDescription;
			if (!string.IsNullOrEmpty(localizedRegionDescription))
			{
				return localizedRegionDescription;
			}
		}
		return ExpeditionBuilder.GetRegionString((int)region);
	}

	public static Shared.GameRegion GetStartRegion()
	{
		return FromRegionSpecification(GameManager.m_StartRegion);
	}

	public static void SetStartRegion(Shared.GameRegion region)
	{
		RegionSpecification regionSpecification = FindRegionSpecification(region);
		if (regionSpecification != null)
		{
			GameManager.m_StartRegion = regionSpecification;
		}
		else
		{
			MelonLogger.Warning("[SkyCoop] No region asset found for " + region.ToString() + ", leaving the start region alone");
		}
	}

	public static Shared.GameRegion GetCurrentRegion()
	{
		try
		{
			UniStormWeatherSystem uniStorm = GameManager.GetUniStorm();
			if (uniStorm != null)
			{
				Shared.GameRegion gameRegion = FromSceneSet(uniStorm.m_CurrentRegion);
				if (gameRegion != Shared.GameRegion.RandomRegion)
				{
					return gameRegion;
				}
			}
		}
		catch
		{
		}
		string text = MyMod.level_name;
		if (string.IsNullOrEmpty(text))
		{
			text = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		}
		// Interior names only match regions by substring (HouseBasementPV -> Pleasant Valley); indoors use the
		// outdoor scene the player came from.
		if (!string.IsNullOrEmpty(text) && !GameManager.IsOutDoorsScene(text))
		{
			string lastOutdoorScene = GameManager.m_SceneTransitionData?.m_LastOutdoorScene;
			if (!string.IsNullOrEmpty(lastOutdoorScene))
			{
				text = lastOutdoorScene;
			}
		}
		return Shared.GetRegionForSceneName(text);
	}
}
