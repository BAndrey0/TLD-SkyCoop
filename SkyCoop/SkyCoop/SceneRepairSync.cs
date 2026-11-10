using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using Il2CppInteractiveObjects;
using UnityEngine;

namespace SkyCoop;

internal static class SceneRepairSync
{
    private const int MaxRepairsPerScene = 1024;
    private static readonly Dictionary<string, RepairableInteractiveItem> Local = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, string> LocalIds = new();
    private static readonly Dictionary<string, HashSet<string>> Saved = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HashSet<string>> Pending = new(StringComparer.Ordinal);
    private static int seed;
    private static string world;
    private static string scene;

    private static void UpdateWorld()
    {
        int currentSeed = MPSaveManager.GetSeed();
        string currentWorld = WorldSaveIdentity.Active;
        if (currentSeed == seed && currentWorld == world) return;
        seed = currentSeed;
        world = currentWorld;
        Saved.Clear();
        Pending.Clear();
    }

    internal static void Prepare(string area)
    {
        scene = area;
        Local.Clear();
        LocalIds.Clear();
        if (area == null) return;
        UpdateWorld();

        var repaired = MyMod.iAmHost ? GetSaved(area) : null;
        bool changed = false;
        foreach (var item in UnityEngine.Object.FindObjectsOfType<RepairableInteractiveItem>(true))
        {
            if (item == null || !item.gameObject.scene.IsValid()) continue;
            string id = Id(item);
            if (!Local.TryAdd(id, item)) continue;
            LocalIds[item.GetInstanceID()] = id;
            if (repaired == null) continue;
            if (repaired.Contains(id)) Receive(area, id);
            else if (item.IsRepaired) changed |= repaired.Add(id);
        }
        if (changed) Save(area, repaired);
    }

    private static string Id(RepairableInteractiveItem item)
    {
        string guid = item.GetComponent<ObjectGuid>()?.Get();
        if (Valid(guid)) return guid;
        // The repair component can be a child of a cabinet with no ObjectGuid of its own.
        string path = "";
        for (var part = item.transform; part != null; part = part.parent)
            path = part.name + "/" + path;
        var p = item.transform.position;
        string identity = FormattableString.Invariant($"{path}|{p.x:F2}|{p.y:F2}|{p.z:F2}");
        return "repair-" + Shared.StableHash(identity).ToString("X8", CultureInfo.InvariantCulture);
    }

    private static bool Valid(string id) => !string.IsNullOrEmpty(id) && id.Length <= 120;

    private static HashSet<string> GetSaved(string area)
    {
        UpdateWorld();
        if (Saved.TryGetValue(area, out var repairs)) return repairs;
        try { repairs = JsonSerializer.Deserialize<HashSet<string>>(MPSaveManager.LoadData(area + "_Repairs", seed) ?? "[]"); }
        catch (JsonException) { repairs = null; }
        return Saved[area] = repairs ?? new(StringComparer.Ordinal);
    }

    private static void Save(string area, HashSet<string> repairs) =>
        MPSaveManager.SaveData(area + "_Repairs", JsonSerializer.Serialize(repairs), seed);

    internal static IEnumerable<string> Snapshot(string area) => GetSaved(area);

    internal static IEnumerable<string> Requests(string area) =>
        Pending.TryGetValue(area, out var repairs) ? new List<string>(repairs) : Array.Empty<string>();

    internal static bool Accept(string area, string id)
    {
        if (!Valid(id)) return false;
        var repaired = GetSaved(area);
        if (repaired.Contains(id)) return true;
        if (repaired.Count >= MaxRepairsPerScene || (area == scene && !Local.ContainsKey(id))) return false;
        repaired.Add(id);
        Save(area, repaired);
        Receive(area, id);
        return true;
    }

    internal static void Receive(string area, string id)
    {
        if (Pending.TryGetValue(area, out var repairs)) repairs.Remove(id);
        if (area != scene || !Local.TryGetValue(id, out var item) || item == null || item.IsRepaired) return;
        item.Deserialize(new RepairableItemSaveState { m_Guid = item.GetComponent<ObjectGuid>()?.Get() ?? id, m_IsRepaired = true });
        if (item.m_FixedGameobject != null) item.m_FixedGameobject.SetActive(true);
        if (item.m_BrokenGameobject != null) item.m_BrokenGameobject.SetActive(false);
    }

    [HarmonyPatch(typeof(RepairableInteractiveItem), nameof(RepairableInteractiveItem.OnAnimatedInteractionEnd))]
    private static class ObserveRepair
    {
        private static void Prefix(RepairableInteractiveItem __instance, out bool __state) => __state = __instance.IsRepaired;

        private static void Postfix(RepairableInteractiveItem __instance, bool __state)
        {
            if (__state || !__instance.IsRepaired || scene == null || !MyMod.InOnline() ||
                !LocalIds.TryGetValue(__instance.GetInstanceID(), out var id)) return;
            if (!MyMod.iAmHost)
            {
                if (!Pending.TryGetValue(scene, out var repairs)) Pending[scene] = repairs = new(StringComparer.Ordinal);
                repairs.Add(id);
            }
            ScenePlacementSync.PublishRepair(scene, id);
        }
    }
}
