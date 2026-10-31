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
			if (!IsStrippedCorpse(__instance, out var carcass))
			{
				return true;
			}
			try
			{
				__instance.HoverText = Localization.Get((carcass != null) ? "GAMEPLAY_Harvest" : "GAMEPLAY_PickUp");
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
			if (!IsStrippedCorpse(__instance, out var carcass))
			{
				return true;
			}
			__result = true;
			try
			{
				if (carcass != null)
				{
					// Same path as the mod's BodyHarvestInteraction patch: in co-op the host hands out the carcass.
					if (MyMod.InOnline())
					{
						MyMod.RequestAnimalCorpseInteration(carcass);
					}
					else
					{
						carcass.InteractWithBodyHarvest(false);
					}
				}
				else
				{
					// Rabbits: the mod removes their BodyHarvest; the dead rabbit is picked up whole through the host.
					ObjectGuid guid = __instance.GetComponent<ObjectGuid>();
					if (guid != null)
					{
						MyMod.AttemptToPickupRabbit(guid.Get());
					}
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Warning("[Corpse] Interaction: " + ex.Message);
				__result = false;
			}
			return false;
		}
	}

	// A corpse made by the mod: no BaseAi any more, but still marked as a co-op corpse.
	private static bool IsStrippedCorpse(WildlifeItemInteraction interaction, out BodyHarvest carcass)
	{
		carcass = null;
		try
		{
			if (interaction == null || interaction.TryCast<WildlifeItemInteraction>() == null || interaction.m_BaseAi != null)
			{
				return false;
			}
			var root = interaction.GetComponentInParent<Comps.AnimalCorpseObject>();
			if (root == null && interaction.GetComponent<BodyHarvest>() == null)
			{
				return false;
			}
			// Rabbit bodies are picked up whole, even if an old component wrapper remains.
			if (root != null && root.gameObject.name.Contains("Rabbit")) return true;
			carcass = interaction.m_BodyHarvest;
			if (carcass == null)
			{
				carcass = interaction.GetComponent<BodyHarvest>();
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
