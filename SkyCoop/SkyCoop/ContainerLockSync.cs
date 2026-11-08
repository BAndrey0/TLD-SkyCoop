using System;
using System.Collections.Generic;
using System.Text.Json;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

internal static class ContainerLockSync
{
    internal sealed record State(string Id, LockState Value, bool WasLocked, bool Opened);
    private static readonly Dictionary<string, Lock> Local = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, State> Confirmed = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, State> Pending = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Dictionary<string, State>> Saved = new(StringComparer.Ordinal);
    private static int worldSeed;
    private static string scene;
    private static float nextCheck;

    private static string Id(Container container) => container.GetComponent<ObjectGuid>()?.Get() ?? "";
    private static State Snapshot(string id, Lock item, bool opened = false) =>
        new(id, item.m_LockState, item.m_WasEverLocked, opened);

    private static Dictionary<string, State> ForScene(string area)
    {
        int seed = MPSaveManager.GetSeed();
        if (seed != worldSeed)
        {
            worldSeed = seed;
            Saved.Clear();
        }
        if (Saved.TryGetValue(area, out var cached)) return cached;
        try { cached = JsonSerializer.Deserialize<Dictionary<string, State>>(MPSaveManager.LoadData(area + "_ContainerLocks", seed) ?? "{}"); }
        catch (JsonException) { cached = null; }
        return Saved[area] = cached ?? new(StringComparer.Ordinal);
    }

    internal static void Prepare(string area)
    {
        scene = area;
        nextCheck = 0f;
        Local.Clear();
        Pending.Clear();
        Confirmed.Clear();
        if (area == null) return;
        foreach (var container in UnityEngine.Object.FindObjectsOfType<Container>())
        {
            var item = container.m_Lock;
            if (item == null) continue;
            string id = Id(container);
            if (id.Length == 0) continue;
            Local[id] = item;
            if (!item.m_LockStateRolled) item.RollLockedState();
            if (MyMod.iAmHost) Receive(Accept(area, Snapshot(id, item), fromHost: true, save: false));
        }
        if (MyMod.iAmHost && Local.Count > 0) Save(area, ForScene(area));
    }

    internal static void CheckForUnlocks()
    {
        if (scene == null || Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = Time.realtimeSinceStartup + 1f;
        foreach (var entry in Local)
            if (entry.Value != null && Confirmed.TryGetValue(entry.Key, out var old) &&
                old.Value == LockState.Locked && entry.Value.m_LockState != LockState.Locked && !Pending.ContainsKey(entry.Key))
                PublishUnlock(entry.Key, entry.Value);
    }

    internal static IEnumerable<State> Requests()
    {
        foreach (var entry in Local)
        {
            if (Pending.TryGetValue(entry.Key, out var change)) yield return change;
            else if (entry.Value != null && !Confirmed.ContainsKey(entry.Key)) yield return Snapshot(entry.Key, entry.Value);
        }
    }

    private static void Save(string area, Dictionary<string, State> locks) =>
        MPSaveManager.SaveData(area + "_ContainerLocks", JsonSerializer.Serialize(locks), MPSaveManager.GetSeed());

    internal static State Accept(string area, State proposal, bool fromHost = false, bool save = true)
    {
        if (proposal == null || string.IsNullOrEmpty(proposal.Id) || proposal.Id.Length > 120 ||
            !Enum.IsDefined(typeof(LockState), proposal.Value)) return null;
        var all = ForScene(area);
        all.TryGetValue(proposal.Id, out var state);
        if (state != null && (!proposal.Opened || state.Value != LockState.Locked || proposal.Value == LockState.Locked)) return state;
        if (state == null && !fromHost && area == scene && Local.TryGetValue(proposal.Id, out var item) && item != null)
            proposal = Snapshot(proposal.Id, item);
        all[proposal.Id] = proposal;
        if (save) Save(area, all);
        if (area == scene) Receive(proposal);
        return proposal;
    }

    internal static void Receive(State state)
    {
        if (state == null || !Local.TryGetValue(state.Id, out var item) || item == null) return;
        Confirmed[state.Id] = state;
        Pending.Remove(state.Id);
        item.m_LockState = state.Value;
        item.m_WasEverLocked = state.WasLocked;
        item.m_LockStateRolled = true;
    }

    internal static bool CanInteract(Container container)
    {
        if (!MyMod.InOnline() || MyMod.iAmHost || container == null || container.m_Lock == null) return true;
        string id = Id(container);
        if (id.Length == 0 || !Local.ContainsKey(id) || Confirmed.ContainsKey(id)) return true;
        ScenePlacementSync.PublishLock(Snapshot(id, container.m_Lock));
        HUDMessage.AddMessage("Проверяем состояние замка…");
        return false;
    }

    private static void PublishUnlock(string id, Lock item)
    {
        var state = Snapshot(id, item, opened: true);
        if (!MyMod.iAmHost) Pending[id] = state;
        ScenePlacementSync.PublishLock(state);
    }

    [HarmonyPatch(typeof(Lock), nameof(Lock.SetLockState))]
    private static class ObserveUnlock
    {
        private static void Prefix(Lock __instance, out LockState __state) => __state = __instance.m_LockState;
        private static void Postfix(Lock __instance, LockState __state)
        {
            if (!MyMod.InOnline() || scene == null || __state != LockState.Locked ||
                __instance.m_LockState == LockState.Locked) return;
            foreach (var entry in Local)
            {
                if (entry.Value != __instance) continue;
                PublishUnlock(entry.Key, __instance);
                break;
            }
        }
    }
}
