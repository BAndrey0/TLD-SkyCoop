using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace SkyCoop;

// In 2.55 a carcass is used through WildlifeItemInteraction, which reads the animal's BaseAi. The mod turns killed
// animals into corpses by removing BaseAi (MyMod.RemoveAnimalComponents), so the interaction threw a
// NullReferenceException when hovered, showed no prompt and could not be used. For such corpses hovering and using
// go straight to the carcass (BodyHarvest).
//
// Small methods in this build can share code with others (see CoopFatigue), so each hook first checks that it was
// really called on a WildlifeItemInteraction.
public static class CorpseInteractionFix
{
	[HarmonyPatch(typeof(WildlifeItemInteraction), "InitializeInteraction")]
	private static class Hover
	{
		private static bool Prefix(WildlifeItemInteraction __instance)
		{
			BodyHarvest carcass = StrippedCarcass(__instance);
			if (carcass == null)
			{
				return true;
			}
			try
			{
				__instance.HoverText = Localization.Get("GAMEPLAY_Harvest");
			}
			catch
			{
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(WildlifeItemInteraction), "PerformInteraction")]
	private static class Use
	{
		private static bool Prefix(WildlifeItemInteraction __instance, ref bool __result)
		{
			BodyHarvest carcass = StrippedCarcass(__instance);
			if (carcass == null)
			{
				return true;
			}
			try
			{
				carcass.InteractWithBodyHarvest(false);
				__result = true;
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[Corpse] Harvest interaction: " + ex.Message);
				__result = false;
			}
			return false;
		}
	}

	private static BodyHarvest StrippedCarcass(WildlifeItemInteraction interaction)
	{
		try
		{
			if (interaction == null || interaction.TryCast<WildlifeItemInteraction>() == null)
			{
				return null;
			}
			if (interaction.m_BaseAi != null)
			{
				return null;
			}
			BodyHarvest carcass = interaction.m_BodyHarvest;
			if (carcass == null)
			{
				carcass = interaction.GetComponent<BodyHarvest>();
			}
			return carcass;
		}
		catch
		{
			return null;
		}
	}
}
