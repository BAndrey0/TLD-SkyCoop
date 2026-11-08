using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using Il2CppTLD.Placement;
using UnityEngine;

namespace SkyCoop;

internal static class ScenePlacementSync
{
    internal const int ClientPacket = 199;
    internal const int ServerPacket = 197;

    private sealed record Pose(float X, float Y, float Z, float Qx, float Qy, float Qz, float Qw)
    {
        internal static Pose From(Transform t) => new(t.position.x, t.position.y, t.position.z,
            t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w);
        internal Vector3 Position => new(X, Y, Z);
        internal Quaternion Rotation => new(Qx, Qy, Qz, Qw);
        internal bool Valid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) &&
            float.IsFinite(Qx) && float.IsFinite(Qy) && float.IsFinite(Qz) && float.IsFinite(Qw);
    }

    private sealed record Move(string Id, string Parent, Pose Pose, string Operation, long Revision, int Version = 0, string Anchor = "", string ParentAnchor = "");
    private sealed class FurniturePlacement
    {
        internal GameObject Object;
        internal Move[] Moves;
        internal bool Confirmed;
    }
    private static FurniturePlacement placingFurniture;
    private sealed record Pot(string Gear, Pose Pose, Pose Slot, string Meshes, string Food, string Mode, int Minutes, bool CanTake, int Liquid);
    private sealed record RemotePot(Pot State, GameObject Visual, float Expires);
    private static readonly Dictionary<string, Dictionary<string, Move>> Saved = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Move> Pending = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Move> Unapplied = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> LatestRevision = new(StringComparer.Ordinal);
    private static readonly HashSet<string> AcceptedOperations = new(StringComparer.Ordinal);
    private static readonly Queue<string> OperationOrder = new();
    private static readonly Dictionary<int, CookingPotItem> LocalPots = new();
    private static readonly Dictionary<(int Owner, int Key), RemotePot> RemotePots = new();
    private static readonly HashSet<int> PublishedPots = new();
    private static Client serverSession;
    private static string scene;
    private static int cookingDepth;
    private static float nextRequest;
    private static long revision;
    internal static bool NativeCooking => cookingDepth > 0;

    internal static bool IsCookingDummy(GearItem gear) =>
        gear != null && IsCookingDummyName(gear.GetGearName());

    internal static bool IsCookingDummyName(string name) => IsPotName(name) && name.EndsWith("Dummy", StringComparison.OrdinalIgnoreCase);

    internal static bool KeepNativeCookingObject(GearItem gear) =>
        gear != null && (IsCookingDummy(gear) || gear.m_CookingPotItem?.GetGearPlacePoint() != null);

    private static bool InWorld => MyMod.InOnline() && !MyMod.LoadingScreenIsOn &&
        WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_guid);

    private static string Id(GameObject obj)
    {
        if (obj == null) return "";
        string persistent = obj.GetComponent<ObjectGuid>()?.Get() ?? "";
        if (persistent.Length > 0) return persistent;
        var placeable = obj.GetComponent<Placeable>();
        if (placeable != null && !string.IsNullOrEmpty(placeable.m_Guid)) return placeable.m_Guid;
        string anchor = Anchor(obj);
        return anchor.Length == 0 ? "" : "anchor-" + anchor;
    }

    private static string Anchor(GameObject obj)
    {
        var decoration = obj != null ? obj.GetComponent<DecorationItem>() : null;
        return decoration != null && OriginalAnchors.TryGetValue(decoration.GetInstanceID(), out string key) ? key : "";
    }

    private static readonly Dictionary<int, string> OriginalAnchors = new();

    [HarmonyPatch(typeof(DecorationItem), nameof(DecorationItem.Awake))]
    private static class RememberOriginalFurniture
    {
        private static void Prefix(DecorationItem __instance)
        {
            var position = __instance.transform.position;
            if (!__instance.gameObject.scene.IsValid() || position == Vector3.zero) return;
            string identity = FormattableString.Invariant(
                $"{GameCompat.StripCloneSuffix(__instance.name)}|{position.x:F2}|{position.y:F2}|{position.z:F2}");
            OriginalAnchors[__instance.GetInstanceID()] = Shared.StableHash(identity).ToString("X8");
        }
    }

    [HarmonyPatch(typeof(DecorationItem), nameof(DecorationItem.OnDestroy))]
    private static class ForgetFurnitureAnchor
    {
        private static void Prefix(DecorationItem __instance) => OriginalAnchors.Remove(__instance.GetInstanceID());
    }

    private static GameObject Find(string id, string anchor = "")
    {
        if (string.IsNullOrEmpty(id)) return null;
        var obj = ObjectGuidManager.Lookup(id);
        if (obj != null) return obj;
        if (DecorationItem.TryGetDecorationFromId(id, out var decoration) && decoration != null) return decoration.gameObject;
        var placeables = UnityEngine.Object.FindObjectsOfType<Placeable>();
        for (int i = 0; i < placeables.Length; i++)
        {
            var placeable = placeables[i];
            if (placeable != null && !placeable.m_Invalidated && placeable.m_Guid == id)
                return placeable.gameObject;
        }
        if (!string.IsNullOrEmpty(anchor))
        {
            GameObject match = null;
            foreach (var item in UnityEngine.Object.FindObjectsOfType<DecorationItem>())
            {
                if (item == null || Anchor(item.gameObject) != anchor) continue;
                if (match != null) return null;
                match = item.gameObject;
            }
            return match;
        }
        return null;
    }

    private static bool IsFixedBed(GameObject obj) => obj != null && obj.GetComponentInChildren<Bed>(true) != null &&
        (obj.GetComponent<GearItem>() == null || obj.GetComponent<DecorationItem>() != null);

    internal static bool CanPlace(GameObject obj)
    {
        if (!MyMod.InOnline() || !IsFixedBed(obj)) return true;
        HUDMessage.AddMessage("Перенос стационарной кровати временно отключён в коопе");
        return false;
    }

    [HarmonyPatch(typeof(PlayerManager), "AttemptToPlaceMesh")]
    private static class BedPlacementGuard
    {
        private static bool Prefix(PlayerManager __instance) => CanPlace(__instance.m_ObjectToPlace);
    }

    internal static void PublishFurniture(GameObject obj)
    {
        if (placingFurniture != null) return;
        PublishMove(CaptureFurniture(obj));
    }

    private static Move CaptureFurniture(GameObject obj)
    {
        if (!InWorld || obj == null || IsFixedBed(obj)) return null;
        var decoration = obj.GetComponent<DecorationItem>();
        if (decoration == null && obj.GetComponent<Placeable>() == null) return null;
        string id = Id(obj);
        if (id.Length == 0) return null;
        string parent = decoration != null && decoration.DecorationParent != null ? Id(decoration.DecorationParent.gameObject) : "";
        return new Move(id, parent, Pose.From(obj.transform), Guid.NewGuid().ToString("N"), 0, 2,
            Anchor(obj), Anchor(decoration?.DecorationParent?.gameObject));
    }

    private static void PublishMove(Move move)
    {
        if (move?.Pose?.Valid != true || move.Pose.Position == Vector3.zero) return;
        if (MyMod.iAmHost) AcceptMove(MyMod.level_guid, move);
        else
        {
            Pending[move.Id] = move;
            SendToHost(1, MyMod.level_guid, JsonSerializer.Serialize(move));
        }
    }

    // Native finalization can reset the preview transform. Keep the confirmed preview's world pose.
    [HarmonyPatch(typeof(PlayerManager), "PlaceMeshInWorld")]
    private static class CapturePlacement
    {
        private static void Prefix(PlayerManager __instance, out FurniturePlacement __state)
        {
            __state = null;
            var obj = __instance.m_ObjectToPlaceDecorationItem?.gameObject ?? __instance.m_ObjectToPlace;
            if (!InWorld || obj == null || IsFixedBed(obj) ||
                (obj.GetComponent<DecorationItem>() == null && obj.GetComponent<Placeable>() == null)) return;
            var moves = obj.GetComponentsInChildren<DecorationItem>(true)
                .Select(item => CaptureFurniture(item.gameObject)).Where(move => move != null).ToList();
            if (__instance.m_DecorationItemChain != null)
                foreach (var item in __instance.m_DecorationItemChain)
                {
                    var move = item.Item != null ? CaptureFurniture(item.Item.gameObject) : null;
                    if (move != null && !moves.Any(existing => existing.Id == move.Id)) moves.Add(move);
                }
            if (moves.Count == 0)
            {
                var move = CaptureFurniture(obj);
                if (move != null) moves.Add(move);
            }
            __state = new FurniturePlacement { Object = obj, Moves = moves.ToArray() };
            placingFurniture = __state;
        }

        private static void Postfix(PlayerManager __instance, FurniturePlacement __state)
        {
            if (__state == null || (!__state.Confirmed && __instance.m_ObjectToPlace == __state.Object)) return;
            foreach (var captured in __state.Moves)
            {
                var obj = Find(captured.Id, captured.Anchor);
                var decoration = obj != null ? obj.GetComponent<DecorationItem>() : null;
                string parent = decoration?.DecorationParent != null ? Id(decoration.DecorationParent.gameObject) : "";
                var parentAnchor = Anchor(decoration?.DecorationParent?.gameObject);
                var pose = obj != null && obj.transform.position != Vector3.zero ? Pose.From(obj.transform) : captured.Pose;
                PublishMove(captured with { Parent = parent, ParentAnchor = parentAnchor, Pose = pose });
            }
        }

        private static void Finalizer(FurniturePlacement __state)
        {
            if (ReferenceEquals(placingFurniture, __state)) placingFurniture = null;
        }
    }

    [HarmonyPatch(typeof(DecorationItem), nameof(DecorationItem.FinalizePlacementRecursive))]
    private static class DecorationPlaced
    {
        private static void Prefix(DecorationItem __instance, bool __0, out Move __state)
        {
            __state = __0 && placingFurniture == null ? CaptureFurniture(__instance.gameObject) : null;
        }

        private static void Postfix(DecorationItem __instance, bool __0, Move __state)
        {
            if (placingFurniture != null)
            {
                if (__0 && placingFurniture.Object == __instance.gameObject) placingFurniture.Confirmed = true;
                return;
            }
            if (__0 && __state != null)
            {
                var current = CaptureFurniture(__instance.gameObject);
                PublishMove(current != null && current.Pose.Position != Vector3.zero ? current : __state);
            }
        }
    }

    private static Dictionary<string, Move> GetScene(string area)
    {
        Server.clients.TryGetValue(1, out var session);
        if (!ReferenceEquals(session, serverSession))
        {
            serverSession = session;
            Saved.Clear();
            AcceptedOperations.Clear();
            OperationOrder.Clear();
        }
        if (Saved.TryGetValue(area, out var moves)) return moves;
        string data = MPSaveManager.LoadData(area + "_FurniturePositions", MPSaveManager.GetSeed());
        try { moves = data == null ? null : JsonSerializer.Deserialize<Dictionary<string, Move>>(data); }
        catch (JsonException) { moves = null; }
        moves ??= new Dictionary<string, Move>(StringComparer.Ordinal);
        int invalid = 0;
        foreach (var entry in moves.ToArray())
        {
            var move = entry.Value;
            if (move?.Pose?.Valid != true || (move.Version < 2 && move.Pose.Position == Vector3.zero))
            {
                moves.Remove(entry.Key);
                invalid++;
            }
        }
        if (invalid > 0) MelonLoader.MelonLogger.Warning("[Furniture] Ignored " + invalid + " invalid legacy placements in " + area);
        foreach (var move in moves.Values) revision = Math.Max(revision, move.Revision);
        Saved.Add(area, moves);
        return moves;
    }

    private static void AcceptMove(string area, Move move, int sender = 0)
    {
        if (move?.Pose?.Valid != true || string.IsNullOrEmpty(move.Id) || move.Id.Length > 100 ||
            (move.Anchor?.Length ?? 0) > 8 || (move.ParentAnchor?.Length ?? 0) > 8 ||
            move.Pose.Position == Vector3.zero || move.Parent == null || move.Parent.Length > 100 || move.Id == move.Parent || move.Version != 2 ||
            !Guid.TryParseExact(move.Operation, "N", out _)) return;
        var moves = GetScene(area);
        string ancestor = move.Parent;
        for (int depth = 0; ancestor.Length > 0; depth++)
        {
            if (ancestor == move.Id || depth > moves.Count) return;
            if (!moves.TryGetValue(ancestor, out var parent)) break;
            ancestor = parent.Parent ?? "";
        }
        if (!AcceptedOperations.Contains(move.Operation))
        {
            if (moves.Count >= 2048 && !moves.ContainsKey(move.Id)) return;
            if (OperationOrder.Count >= 8192) AcceptedOperations.Remove(OperationOrder.Dequeue());
            AcceptedOperations.Add(move.Operation);
            OperationOrder.Enqueue(move.Operation);
            move = move with { Revision = ++revision };
            if (!string.IsNullOrEmpty(move.Anchor))
                foreach (var old in moves.Values.Where(existing => existing.Id != move.Id && existing.Anchor == move.Anchor).ToArray())
                    moves.Remove(old.Id);
            moves[move.Id] = move;
            MelonLoader.MelonLogger.Msg("[Furniture] Saved move " + move.Id + " in " + area + " revision=" + move.Revision);
            MPSaveManager.ValidateRootExits();
            MPSaveManager.SaveData(area + "_FurniturePositions", JsonSerializer.Serialize(moves), MPSaveManager.GetSeed());
            ReceiveMove(area, move);
            SendToPeers(1, area, JsonSerializer.Serialize(move));
        }
        if (sender > 0)
        {
            using var ack = MakeServerPacket(3, area, move.Operation, 0, 0);
            ServerSend.SendUDPData(sender, ack);
        }
    }

    private static void ReceiveMove(string area, Move move)
    {
        if (area != MyMod.level_guid || move?.Pose?.Valid != true || string.IsNullOrEmpty(move.Id)) return;
        if (LatestRevision.TryGetValue(move.Id, out long latest) && latest >= move.Revision) return;
        LatestRevision[move.Id] = move.Revision;
        Unapplied[move.Id] = move;
    }

    private static bool Apply(Move move)
    {
        if (PlaceableManager.s_IsDeserializing) return false;
        var obj = Find(move.Id, move.Anchor);
        if (obj == null) return false;
        if (IsFixedBed(obj)) return true;
        var player = GameManager.GetPlayerManagerComponent();
        if (player != null && player.m_ObjectToPlace == obj) return false;
        if (move.Parent.Length > 0 && Unapplied.ContainsKey(move.Parent)) return false;
        var decoration = obj.GetComponent<DecorationItem>();
        if (decoration == null && obj.GetComponent<Placeable>() == null) return true;
        if (decoration != null)
        {
            var parent = Find(move.Parent, move.ParentAnchor);
            if (move.Parent.Length > 0 && parent == null) return false;
            var parentDecoration = parent != null ? parent.GetComponent<DecorationItem>() : null;
            if (parentDecoration != decoration.DecorationParent &&
                (parent == null || !parent.transform.IsChildOf(obj.transform)))
                decoration.SetParent(parentDecoration);
        }
        obj.transform.SetPositionAndRotation(move.Pose.Position, move.Pose.Rotation);
        return true;
    }

    [HarmonyPatch]
    private static class CookingScope
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[]
        {
            AccessTools.Method(typeof(Panel_Cooking), nameof(Panel_Cooking.OnCook)),
            AccessTools.Method(typeof(Panel_Cooking), nameof(Panel_Cooking.OnCookNormalFood)),
            AccessTools.Method(typeof(Panel_Cooking), nameof(Panel_Cooking.OnCookRecipe)),
            AccessTools.Method(typeof(Panel_GearSelect), "DoFirePickerAction"),
            AccessTools.Method(typeof(Panel_CookWater), nameof(Panel_CookWater.OnBoil)),
            AccessTools.Method(typeof(Panel_CookWater), nameof(Panel_CookWater.OnMeltSnow)),
            AccessTools.Method(typeof(Panel_CookWater), nameof(Panel_CookWater.OnDoWater)),
            AccessTools.Method(typeof(CookingPotItem), nameof(CookingPotItem.StartCooking))
        };
        private static void Prefix() => cookingDepth++;
        private static void Finalizer() => cookingDepth--;
    }

    [HarmonyPatch(typeof(GearPlacePoint), nameof(GearPlacePoint.DropAndPlaceItem))]
    private static class PlaceCookingGear
    {
        private static bool Prefix(GearPlacePoint __instance, out bool __state)
        {
            __state = false;
            if (!MyMod.InOnline() || __instance.m_FireToAttach == null) return true;
            if (RemotePotAt(__instance))
            {
                HUDMessage.AddMessage("Конфорка занята");
                return false;
            }
            __state = true;
            cookingDepth++;
            return true;
        }

        private static void Finalizer(bool __state)
        {
            if (__state) cookingDepth--;
        }
    }

    [HarmonyPatch(typeof(GearPlacePoint), nameof(GearPlacePoint.IsAllowedGearItem))]
    private static class ValidateSlot
    {
        private static void Postfix(GearPlacePoint __instance, ref bool __result)
        {
            if (RemotePotAt(__instance)) __result = false;
        }
    }

    [HarmonyPatch(typeof(GearPlacePoint), nameof(GearPlacePoint.PlaceGear))]
    private static class ReserveSlot
    {
        private static bool Prefix(GearPlacePoint __instance) => !RemotePotAt(__instance);
    }

    [HarmonyPatch(typeof(GearPlacePoint), nameof(GearPlacePoint.FindFirstEmptyPlacePoint))]
    private static class FindFreeSlot
    {
        private static void Postfix(GameObject __0, ref GearPlacePoint __result)
        {
            if (__0 == null || !RemotePotAt(__result)) return;
            __result = null;
            foreach (var slot in __0.GetComponentsInChildren<CookingSlot>())
            {
                var point = slot.m_GearPlacePoint;
                if (point == null || point.GetPlacedGear() != null || RemotePotAt(point)) continue;
                __result = point;
                break;
            }
        }
    }

    [HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.AttachToFire))]
    private static class PotAttached
    {
        private static void Postfix(CookingPotItem __instance)
        {
            if (MyMod.InOnline()) LocalPots[__instance.GetInstanceID()] = __instance;
        }
    }

    private static void TickPots()
    {
        foreach (var pair in LocalPots.ToArray())
        {
            var pot = pair.Value;
            var point = pot != null && pot.isActiveAndEnabled ? pot.GetGearPlacePoint() : null;
            if (point == null || pot.m_GearItem == null)
            {
                LocalPots.Remove(pair.Key);
                if (PublishedPots.Remove(pair.Key)) PublishPot(pair.Key, null);
                continue;
            }
            string name = pot.m_GearItem.GetGearName();
            if (!IsPotName(name)) continue;
            int liquid = pot.m_LitersSnowBeingMelted.ToLiters() > 0 ? 2 :
                pot.m_LitersWaterBeingBoiled.ToLiters() > 0 ? 1 : 0;
            var visible = new System.Text.StringBuilder();
            foreach (var renderer in pot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (visible.Length >= 256) break;
                if (renderer == pot.m_GrubMeshRenderer && liquid != 0) continue;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    visible.Append(PotMeshKey(renderer, pot.transform));
            }
            string food = liquid == 0 && pot.m_GearItemBeingCooked != null ? pot.m_GearItemBeingCooked.GetGearName() : "";
            bool canTake = !IsCookingDummyName(name) && !pot.IsCookingSomething() && liquid == 0;
            PublishedPots.Add(pair.Key);
            PublishPot(pair.Key, new Pot(name, Pose.From(pot.transform), Pose.From(point.transform), visible.ToString(),
                food, pot.GetCookingState().ToString(), Mathf.CeilToInt(pot.m_MinutesUntilCooked), canTake, liquid));
        }
    }

    private static bool IsPotName(string name) => name != null && name.StartsWith("GEAR_", StringComparison.Ordinal) &&
        name.Length <= 64 && (name.Contains("CookingPot", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Skillet", StringComparison.OrdinalIgnoreCase) || name.Contains("RecycledCan", StringComparison.OrdinalIgnoreCase));

    private static void PublishPot(int key, Pot pot)
    {
        string data = JsonSerializer.Serialize(pot);
        if (MyMod.iAmHost) SendToPeers(2, MyMod.level_guid, data, 0, key);
        else SendToHost(2, MyMod.level_guid, data, key);
    }

    internal static bool RemotePotAt(GearPlacePoint point) => FindRemotePot(point).HasValue;

    private static KeyValuePair<(int Owner, int Key), RemotePot>? FindRemotePot(GearPlacePoint point)
    {
        if (point == null || !MyMod.InOnline()) return null;
        Vector3 position = point.transform.position;
        foreach (var pot in RemotePots)
            if ((pot.Value.State.Slot.Position - position).sqrMagnitude < .04f) return pot;
        return null;
    }

    internal static string CookingHover(GameObject target)
    {
        var slot = target != null ? target.GetComponentInParent<CookingSlot>() : null;
        if (slot == null || FindRemotePot(slot.m_GearPlacePoint) is not { } entry) return null;
        var state = entry.Value.State;
        string contents = state.Food.Length > 0 ? GameCompat.GetGearDisplayName(state.Food) :
            state.Mode.Contains("Snow", StringComparison.OrdinalIgnoreCase) ? "Снег" :
            state.Mode.Contains("Water", StringComparison.OrdinalIgnoreCase) ? "Вода" : "";
        string label = GameCompat.GetGearDisplayName(state.Gear);
        if (contents.Length > 0) label += "\n" + contents;
        if (state.Minutes > 0 && contents.Length > 0) label += " · " + state.Minutes + " мин";
        if (state.CanTake) label += "\nСнять посуду";
        return label;
    }

    [HarmonyPatch(typeof(CookingSlot), nameof(CookingSlot.PerformInteraction))]
    private static class InteractWithRemoteCookware
    {
        private static bool Prefix(CookingSlot __instance, ref bool __result)
        {
            if (FindRemotePot(__instance.m_GearPlacePoint) is not { } entry) return true;
            __result = true;
            if (!entry.Value.State.CanTake)
                HUDMessage.AddMessage("Сначала освободите посуду от еды или воды");
            else if (MyMod.iAmHost)
                RequestCookwareRelease(0, entry.Key.Owner, entry.Key.Key, MyMod.level_guid);
            else
                SendToHost(4, MyMod.level_guid, entry.Key.Owner.ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Key.Key);
            return false;
        }
    }

    private static void RequestCookwareRelease(int requester, int owner, int key, string area)
    {
        if (owner == requester || owner < 0 || Shared.ReadyWildlifeScene(owner) != area) return;
        if (owner == 0) ReleaseCookware(key);
        else
        {
            using var packet = MakeServerPacket(4, area, "", requester, key);
            ServerSend.SendUDPData(owner, packet);
        }
    }

    private static void ReleaseCookware(int key)
    {
        if (!LocalPots.TryGetValue(key, out var pot) || pot == null) return;
        var point = pot.GetGearPlacePoint();
        var gear = pot.m_GearItem;
        if (point == null || gear == null || !IsPotName(gear.GetGearName()) || IsCookingDummy(gear) ||
            pot.IsCookingSomething() || pot.m_LitersWaterBeingBoiled.ToLiters() > 0 || pot.m_LitersSnowBeingMelted.ToLiters() > 0) return;
        LocalPots.Remove(key);
        PublishedPots.Remove(key);
        pot.DetachFromFire();
        if (point.GetPlacedGear() == gear) point.ResetPlacedGear();
        PublishPot(key, null);
        MyMod.SendDropItem(gear, samepose: true);
    }

    private static void ReceivePot(string area, int owner, int key, Pot pot)
    {
        if (area != MyMod.level_guid || owner == ClientUser.myId) return;
        var identity = (owner, key);
        RemotePots.TryGetValue(identity, out var previous);
        if (pot == null)
        {
            if (previous?.Visual != null) UnityEngine.Object.Destroy(previous.Visual);
            RemotePots.Remove(identity);
            return;
        }
        if (!ValidPot(pot)) return;
        if (previous == null && RemotePots.Count >= 128) return;
        var visual = previous?.Visual;
        if (previous != null && (previous.State.Gear != pot.Gear || previous.State.Meshes != pot.Meshes || previous.State.Liquid != pot.Liquid) && visual != null)
        {
            UnityEngine.Object.Destroy(visual);
            visual = null;
        }
        if (visual == null) visual = CreatePotVisual(pot);
        if (visual != null) visual.transform.SetPositionAndRotation(pot.Pose.Position, pot.Pose.Rotation);
        RemotePots[identity] = new RemotePot(pot, visual, Time.realtimeSinceStartup + 7f);
    }

    private static bool ValidPot(Pot pot) => IsPotName(pot.Gear) && pot.Pose?.Valid == true && pot.Slot?.Valid == true &&
        pot.Meshes != null && pot.Meshes.Length <= 256 && pot.Meshes.Length % 8 == 0 && pot.Liquid >= 0 && pot.Liquid <= 2 && pot.Food != null && pot.Food.Length <= 80 && pot.Mode != null && pot.Mode.Length <= 32;

    private static string PotMeshKey(MeshRenderer renderer, Transform root)
    {
        var filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return "";
        string path = "";
        for (var part = renderer.transform; part != null && part != root; part = part.parent)
            path = part.name + "/" + path;
        return Shared.StableHash(path + "|" + filter.sharedMesh.name).ToString("X8");
    }

    private static GameObject CreatePotVisual(Pot pot)
    {
        var prefab = MyMod.GetGearItemObject(pot.Gear);
        if (prefab == null || prefab.GetComponent<CookingPotItem>() == null) return null;
        var root = new GameObject("SkyCoop shared cookware");
        var cooking = prefab.GetComponent<CookingPotItem>();
        var visible = new HashSet<string>();
        for (int i = 0; i < pot.Meshes.Length; i += 8) visible.Add(pot.Meshes.Substring(i, 8));
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null) continue;
            Mesh sourceMesh = filter.sharedMesh;
            var materials = renderer.sharedMaterials;
            if (pot.Liquid != 0 && renderer == cooking.m_GrubMeshRenderer)
            {
                sourceMesh = pot.Liquid == 2 ? cooking.m_SnowMesh : cooking.m_WaterMesh;
                var liquidMaterials = pot.Liquid == 2 ? cooking.m_MeltSnowMaterialsList : cooking.m_BoilWaterPotMaterialsList;
                if (liquidMaterials != null && liquidMaterials.Length != 0)
                    materials = new[] { liquidMaterials[0] };
                else continue;
            }
            else if (!visible.Contains(PotMeshKey(renderer, prefab.transform))) continue;
            if (sourceMesh == null) continue;
            var mesh = new GameObject(renderer.name);
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = prefab.transform.InverseTransformPoint(renderer.transform.position);
            mesh.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * renderer.transform.rotation;
            mesh.transform.localScale = renderer.transform.lossyScale;
            mesh.AddComponent<MeshFilter>().sharedMesh = sourceMesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }
        return root;
    }

    internal static void Tick()
    {
        string area = InWorld ? MyMod.level_guid : null;
        if (area != scene)
        {
            foreach (var pot in RemotePots.Values)
                if (pot.Visual != null) UnityEngine.Object.Destroy(pot.Visual);
            RemotePots.Clear();
            LocalPots.Clear();
            PublishedPots.Clear();
            Pending.Clear();
            Unapplied.Clear();
            LatestRevision.Clear();
            scene = area;
            nextRequest = 0;
            if (area != null)
            {
                foreach (var pot in UnityEngine.Object.FindObjectsOfType<CookingPotItem>())
                    LocalPots[pot.GetInstanceID()] = pot;
                if (MyMod.iAmHost)
                    foreach (var move in GetScene(area).Values) ReceiveMove(area, move);
            }
        }
        if (area == null) return;
        float now = Time.realtimeSinceStartup;
        foreach (var pair in RemotePots.ToArray())
            if (pair.Value.Expires <= now) ReceivePot(area, pair.Key.Owner, pair.Key.Key, null);
        foreach (var pair in Unapplied.ToArray())
            if (Apply(pair.Value)) Unapplied.Remove(pair.Key);
        TickPots();
        if (MyMod.iAmHost || now < nextRequest) return;
        nextRequest = now + 8f;
        foreach (var move in Pending.Values) SendToHost(1, area, JsonSerializer.Serialize(move));
        SendToHost(0, area, "");
    }

    internal static void ReceiveClient(int from, Packet packet)
    {
        byte kind = packet.ReadByte();
        string area = packet.ReadString();
        int key = packet.ReadInt();
        string data = packet.ReadString();
        if (from <= 0 || from >= MyMod.playersData.Count || MyMod.playersData[from]?.m_LevelGuid != area ||
            !WildlifeSpawnAuthorityPolicy.WorldScene(area) || area.Length > 160 ||
            area.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || data.Length > 1100) return;
        try
        {
            if (kind == 0)
            {
                foreach (var move in GetScene(area).Values)
                {
                    using var reply = MakeServerPacket(1, area, JsonSerializer.Serialize(move), 0, 0);
                    ServerSend.SendUDPData(from, reply);
                }
            }
            else if (kind == 1) AcceptMove(area, JsonSerializer.Deserialize<Move>(data), from);
            else if (kind == 2)
            {
                var pot = JsonSerializer.Deserialize<Pot>(data);
                if (pot != null && !ValidPot(pot)) return;
                ReceivePot(area, from, key, pot);
                SendToPeers(2, area, data, from, key);
            }
            else if (kind == 4 && int.TryParse(data, out int owner)) RequestCookwareRelease(from, owner, key, area);
            else if (kind == 5 && MyMod.playersData[from].m_LevelGuid == area)
            {
                if (area != MyMod.level_guid || ThrownStoneSync.Receive(area, from, key, data))
                    SendToPeers(5, area, data, from, key);
            }
        }
        catch (JsonException) { }
    }

    internal static void Receive(Packet packet)
    {
        byte kind = packet.ReadByte();
        string area = packet.ReadString();
        int owner = packet.ReadInt();
        int key = packet.ReadInt();
        string data = packet.ReadString();
        if (!InWorld || area != MyMod.level_guid || data.Length > 1100) return;
        try
        {
            if (kind == 1) ReceiveMove(area, JsonSerializer.Deserialize<Move>(data));
            else if (kind == 2) ReceivePot(area, owner, key, JsonSerializer.Deserialize<Pot>(data));
            else if (kind == 4) ReleaseCookware(key);
            else if (kind == 5 && owner != ClientUser.myId) ThrownStoneSync.Receive(area, owner, key, data);
            else if (kind == 3)
            {
                foreach (var move in Pending.Values.ToArray())
                    if (move.Operation == data) Pending.Remove(move.Id);
            }
        }
        catch (JsonException) { }
    }

    private static void SendToHost(byte kind, string area, string data, int key = 0)
    {
        using var packet = new Packet(ClientPacket);
        packet.Write(kind);
        packet.Write(area);
        packet.Write(key);
        packet.Write(data);
        MyMod.SendUDPData(packet);
    }

    internal static void PublishStoneFlight(int key, string data)
    {
        if (MyMod.iAmHost) SendToPeers(5, MyMod.level_guid, data, 0, key);
        else if (MyMod.sendMyPosition) SendToHost(5, MyMod.level_guid, data, key);
    }

    private static Packet MakeServerPacket(byte kind, string area, string data, int owner, int key)
    {
        var packet = new Packet(ServerPacket);
        packet.Write(kind);
        packet.Write(area);
        packet.Write(owner);
        packet.Write(key);
        packet.Write(data);
        return packet;
    }

    private static void SendToPeers(byte kind, string area, string data, int owner = 0, int key = 0)
    {
        using var packet = MakeServerPacket(kind, area, data, owner, key);
        ServerSend.SendUDPDataToAll(packet, area);
    }
}
