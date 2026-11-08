using HarmonyLib;
using Il2Cpp;

namespace SkyCoop;

internal static class StrengthCostLabels
{
    [HarmonyPatch(typeof(Panel_BreakDown), nameof(Panel_BreakDown.UpdateDurationLabel))]
    private static class Breakdown
    {
        private static void Postfix(Panel_BreakDown __instance)
        {
            if (!CoopFatigue.ShowsStrengthCost) return;
            var label = __instance.GetPanelInfo()?.m_DurationLabel;
            if (label != null) label.text = CoopFatigue.CostLabel(__instance.m_DurationHours, heavy: true);
        }
    }

    [HarmonyPatch(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.RefreshEstimatedTimeLabel))]
    private static class Carcass
    {
        private static void Postfix(Panel_BodyHarvest __instance)
        {
            if (!CoopFatigue.ShowsStrengthCost || __instance.m_Label_EstimatedTime == null) return;
            bool quarter = __instance.IsTabQuarterSelected();
            float minutes = quarter ? __instance.GetQuarterDurationMinutes() : __instance.GetHarvestDurationMinutes();
            __instance.m_Label_EstimatedTime.text = CoopFatigue.CostLabel(minutes / 60f, heavy: quarter);
        }
    }

    [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.RefreshHoursToRead))]
    private static class Reading
    {
        private static void Postfix(Panel_Inventory_Examine __instance)
        {
            if (CoopFatigue.ShowsStrengthCost && __instance.m_TimeToReadLabel != null)
                __instance.m_TimeToReadLabel.text = CoopFatigue.CostLabelForReading(__instance.m_HoursToRead);
        }
    }

    [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.RefreshHarvest))]
    private static class Harvest
    {
        private static void Postfix(Panel_Inventory_Examine __instance)
        {
            if (CoopFatigue.ShowsStrengthCost && __instance.m_HarvestTime_Label != null)
                __instance.m_HarvestTime_Label.text = CoopFatigue.CostLabel(__instance.m_HarvestTimeSeconds / 3600f);
        }
    }

    [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.UpdateRepairLabels))]
    private static class Repair
    {
        private static void Postfix(Panel_Inventory_Examine __instance)
        {
            if (!CoopFatigue.ShowsStrengthCost) return;
            var label = __instance.GetRepairTimeLabel();
            if (label != null)
                label.text = CoopFatigue.CostLabelForRepair(__instance.m_RepairTimeSeconds / 3600f, __instance.m_GearItem);
        }
    }

    [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.RefreshSelectedBlueprint))]
    private static class Crafting
    {
        private static void Postfix(Panel_Crafting __instance)
        {
            if (!CoopFatigue.ShowsStrengthCost || __instance.SelectedBPI == null || __instance.m_SelectedDescription == null) return;
            int minutes = __instance.GetFinalCraftingTimeWithAllModifiers();
            var label = __instance.m_SelectedDescription;
            string description = label.text ?? "";
            if (description.StartsWith("Сила −", System.StringComparison.Ordinal))
                description = description.Substring(description.IndexOf('\n') + 1);
            label.text = CoopFatigue.CostLabel(minutes / 60f) + "\n" + description;
        }
    }

    [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.UpdateCleanLabels))]
    private static class Cleaning
    {
        private static void Postfix(Panel_Inventory_Examine __instance)
        {
            if (!CoopFatigue.ShowsStrengthCost || __instance.m_Clean_TimeLabel == null || __instance.m_GearItem == null) return;
            __instance.GetCleanConditionIncreaseAndDuration(out _, out int minutes);
            __instance.m_Clean_TimeLabel.text = CoopFatigue.CostLabel(minutes / 60f);
        }
    }

    [HarmonyPatch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.UpdateSharpenLabels))]
    private static class Sharpening
    {
        private static void Postfix(Panel_Inventory_Examine __instance)
        {
            if (!CoopFatigue.ShowsStrengthCost || __instance.m_Sharpen_TimeLabel == null || __instance.m_GearItem == null) return;
            __instance.GetSharpenConditionIncreaseAndDuration(out _, out int minutes);
            __instance.m_Sharpen_TimeLabel.text = CoopFatigue.CostLabel(minutes / 60f);
        }
    }
}
