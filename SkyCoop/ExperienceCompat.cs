using System;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gameplay;
using MelonLoader;

namespace SkyCoop;

public static class ExperienceCompat
{
	private static int CountOf(IList<GameModeConfig> list)
	{
		if (list == null)
		{
			return 0;
		}
		return list.TryCast<ICollection<GameModeConfig>>()?.Count ?? 0;
	}

	public static GameModeConfig FindGameModeConfig(ExperienceModeType type)
	{
		try
		{
			ExperienceModeManager experienceModeManagerComponent = GameManager.GetExperienceModeManagerComponent();
			if (experienceModeManagerComponent == null)
			{
				return null;
			}
			IList<GameModeConfig> availableGameModes = experienceModeManagerComponent.GetAvailableGameModes();
			int num = CountOf(availableGameModes);
			for (int i = 0; i < num; i++)
			{
				GameModeConfig gameModeConfig = availableGameModes[i];
				if (gameModeConfig != null && gameModeConfig.m_XPMode != null && gameModeConfig.m_XPMode.m_ModeType == type)
				{
					return gameModeConfig;
				}
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[SkyCoop] Could not look up game mode " + type.ToString() + ": " + ex.Message);
		}
		return null;
	}

	public static void SetExperienceModeType(ExperienceModeType type)
	{
		ExperienceModeManager experienceModeManagerComponent = GameManager.GetExperienceModeManagerComponent();
		GameModeConfig gameModeConfig = FindGameModeConfig(type);
		if (experienceModeManagerComponent != null && gameModeConfig != null)
		{
			experienceModeManagerComponent.SetGameModeConfig(gameModeConfig);
		}
		else
		{
			MelonLogger.Warning("[SkyCoop] No game mode config for " + type.ToString() + ", experience mode unchanged");
		}
	}

	public static ExperienceModeType GetSelectedExperienceMode(Panel_SelectExperience panel)
	{
		if (panel == null)
		{
			return ExperienceModeType.Pilgrim;
		}
		try
		{
			IList<GameModeConfig> availableGameModes = GameManager.GetExperienceModeManagerComponent().GetAvailableGameModes();
			int selectedIndex = panel.GetSelectedIndex();
			if (selectedIndex >= 0 && selectedIndex < CountOf(availableGameModes) && availableGameModes[selectedIndex] != null && availableGameModes[selectedIndex].m_XPMode != null)
			{
				return availableGameModes[selectedIndex].m_XPMode.m_ModeType;
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[SkyCoop] Could not read the selected experience mode: " + ex.Message);
		}
		return ExperienceModeType.Pilgrim;
	}

	public static bool IsStoryMode()
	{
		ExperienceModeType currentExperienceModeType = ExperienceModeManager.GetCurrentExperienceModeType();
		if (currentExperienceModeType != ExperienceModeType.Story && currentExperienceModeType != ExperienceModeType.StoryFresh)
		{
			return currentExperienceModeType == ExperienceModeType.StoryHardened;
		}
		return true;
	}

	public static string GetLocalizedName(ExperienceModeType type)
	{
		GameModeConfig gameModeConfig = FindGameModeConfig(type);
		if (gameModeConfig != null && gameModeConfig.Name != null)
		{
			return gameModeConfig.Name.Text();
		}
		return type.ToString();
	}
}
