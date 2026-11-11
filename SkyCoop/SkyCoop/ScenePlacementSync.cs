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

    private sealed record Move(string Id, string Parent, Pose Pose, string Operation, long Revision, int Version = 0, string Anchor = "", string ParentAnchor = "", string Asset = "", string AssetName = "", float Sx = 1f, float Sy = 1f, float Sz = 1f, bool RegisterOnly = false);
    private sealed class FurniturePlacement
    {
        internal GameObject Object;
        internal Move[] Moves;
        internal bool Confirmed;
    }
    private static FurniturePlacement placingFurniture;
    private static bool creatingReplica;
    private static float nextFurnitureApply;
    private static readonly HashSet<string> FurnitureApplyWarnings = new();
    private sealed record Pot(string Gear, Pose Pose, Pose Slot, string Meshes, string Food, string Mode, int Minutes, bool CanTake, int Liquid, string NativeHover, float Liters = 0f);
    private sealed record GearTransfer(Guid Id, int Requester, string Gear, string Json, float Heat, int TimesHeated);
    private sealed record WaterTransfer(Guid Id, int Requester, float Liters, bool Potable);
    private static readonly Dictionary<Guid, float> ReceivedOutputs = new();
    private sealed record RemotePot(Pot State, GameObject Visual, BoxCollider Hitbox, float Expires);
    private static readonly Dictionary<string, Dictionary<string, Move>> Saved = new(StringComparer.Ordinal);
    // LoadData migrates old Unity vectors by lowercasing X, Y and Z before returning JSON.
    private static readonly JsonSerializerOptions FurnitureSaveOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, Move> Pending = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Move> Unapplied = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> LatestRevision = new(StringComparer.Ordinal);
    private static readonly HashSet<string> AcceptedOperations = new(StringComparer.Ordinal);
    private static readonly Queue<string> OperationOrder = new();
    private static readonly Dictionary<int, CookingPotItem> LocalPots = new();
    private static readonly Dictionary<int, GearPlacePoint> CookingPoints = new();
    private static readonly Dictionary<int, (GearPlacePoint Point, GearItem Gear, CookingPotItem Pot)> LocalCooking = new();
    private static float nextCookingScan;
    private static float nextCookingPublish;
    private sealed record SlotClaim(Pot State, float Expires);
    private static readonly Dictionary<(string Scene, int Owner, int Key), SlotClaim> CookingClaims = new();
    private static readonly HashSet<int> RejectedCooking = new();
    private static Client cookingSession;
    private const float SlotMatchSquared = .0064f;
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
        var placeable = obj.GetComponent<Placeable>();
        if (placeable != null && !string.IsNullOrEmpty(placeable.m_Guid) && placeable.m_Addressable != null) return placeable.m_Guid;
        string persistent = obj.GetComponent<ObjectGuid>()?.Get() ?? "";
        if (persistent.Length > 0) return persistent;
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

    // Build once per apply batch. Avoid IL2CPP Dictionary.TryGetValue with the
    // game's boxed PlaceableInfo struct as an out argument.
    private sealed class PlacementLookup
    {
        private Dictionary<string, Placeable> byGuid;

        internal Placeable Find(string id)
        {
            if (byGuid == null)
            {
                byGuid = new Dictionary<string, Placeable>(StringComparer.Ordinal);
                foreach (var placeable in UnityEngine.Object.FindObjectsOfType<Placeable>(true))
                    Register(placeable);
            }
            return byGuid.TryGetValue(id, out var match) && match != null && !match.m_Invalidated ? match : null;
        }

        internal void Register(Placeable placeable)
        {
            if (byGuid == null || placeable == null || placeable.m_Invalidated) return;
            string guid = placeable.m_Guid;
            if (!string.IsNullOrEmpty(guid)) byGuid[guid] = placeable;
        }
    }

    private static GameObject Find(string id, string anchor = "", PlacementLookup lookup = null)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var obj = ObjectGuidManager.Lookup(id);
        if (obj != null) return obj;
        if (DecorationItem.TryGetDecorationFromId(id, out var decoration) && decoration != null) return decoration.gameObject;
        var placeable = (lookup ?? new PlacementLookup()).Find(id);
        if (placeable != null) return placeable.gameObject;
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

    private static bool IsFixedBed(GameObject obj) => obj != null &&
        (obj.GetComponent<DecorationItem>() != null || obj.GetComponent<Placeable>() != null) &&
        obj.GetComponentInParent<SnowShelter>() == null && obj.GetComponentInChildren<SnowShelter>(true) == null &&
        obj.GetComponentInChildren<Bed>(true) is { } bed && bed.m_Bedroll == null;

    internal static bool CanPlace(GameObject obj)
    {
        if (!MyMod.InOnline() || !IsFixedBed(obj)) return true;
        if (!BedRest.IsOccupied(obj.GetComponentInChildren<Bed>(true)) && Id(obj).Length > 0) return true;
        HUDMessage.AddMessage("Освободите кровать перед перестановкой");
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
        if (!InWorld || obj == null) return null;
        var decoration = obj.GetComponent<DecorationItem>();
        if (decoration == null && obj.GetComponent<Placeable>() == null) return null;
        string id = Id(obj);
        if (id.Length == 0) return null;
        string parent = decoration != null && decoration.DecorationParent != null ? Id(decoration.DecorationParent.gameObject) : "";
        var move = new Move(id, parent, Pose.From(obj.transform), Guid.NewGuid().ToString("N"), 0, 2,
            Anchor(obj), Anchor(decoration?.DecorationParent?.gameObject));
        var placeable = obj.GetComponent<Placeable>();
        if (placeable == null || IsFixedBed(obj) || !Guid.TryParseExact(placeable.m_Addressable?.AssetGUID, "N", out _)) return move;
        var scale = obj.transform.localScale;
        return move with { Anchor = "", Asset = placeable.m_Addressable.AssetGUID, AssetName = obj.name, Sx = scale.x, Sy = scale.y, Sz = scale.z };
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
            if (!InWorld || obj == null || !CanPlace(obj) ||
                (obj.GetComponent<DecorationItem>() == null && obj.GetComponent<Placeable>() == null)) return;
            if (IsFixedBed(obj))
            {
                var bedMove = CaptureFurniture(obj);
                if (bedMove == null) return;
                __state = new FurniturePlacement { Object = obj, Moves = new[] { bedMove } };
                placingFurniture = __state;
                return;
            }
            var moves = obj.GetComponentsInChildren<DecorationItem>(true)
                .Select(item => CaptureFurniture(item.gameObject)).Where(move => move != null).ToList();
            if (__instance.m_DecorationItemChain != null)
                foreach (var item in __instance.m_DecorationItemChain)
                {
                    var move = item.Item != null ? CaptureFurniture(item.Item.gameObject) : null;
                    if (move != null && !moves.Any(existing => existing.Id == move.Id)) moves.Add(move);
                }
            var rootMove = CaptureFurniture(obj);
            if (rootMove != null && !moves.Any(existing => existing.Id == rootMove.Id)) moves.Insert(0, rootMove);
            __state = new FurniturePlacement { Object = obj, Moves = moves.ToArray() };
            placingFurniture = __state;
        }

        private static void Postfix(PlayerManager __instance, FurniturePlacement __state)
        {
            if (__state == null || (!__state.Confirmed && __instance.m_ObjectToPlace == __state.Object)) return;
            if (__state.Moves.Length == 0)
            {
                PublishMove(CaptureFurniture(__state.Object));
                return;
            }
            foreach (var captured in __state.Moves)
            {
                var obj = Find(captured.Id, captured.Anchor);
                var decoration = obj != null ? obj.GetComponent<DecorationItem>() : null;
                string parent = decoration?.DecorationParent != null ? Id(decoration.DecorationParent.gameObject) : "";
                var parentAnchor = Anchor(decoration?.DecorationParent?.gameObject);
                var pose = obj != null && obj.transform.position != Vector3.zero ? Pose.From(obj.transform) : captured.Pose;
                var current = obj != null ? CaptureFurniture(obj) : null;
                PublishMove((current ?? captured) with { Parent = parent, ParentAnchor = parentAnchor, Pose = pose });
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

    [HarmonyPatch(typeof(PlaceableManager), nameof(PlaceableManager.ReturnFromPlayer), new[] { typeof(Placeable) })]
    private static class ReturnCraftedFurniture
    {
        private static void Postfix(Placeable __0)
        {
            if (!InWorld || creatingReplica || PlaceableManager.s_IsDeserializing || __0 == null) return;
            var move = CaptureFurniture(__0.gameObject);
            if (move != null && !string.IsNullOrEmpty(move.Asset)) PublishMove(move);
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
        try { moves = data == null ? null : JsonSerializer.Deserialize<Dictionary<string, Move>>(data, FurnitureSaveOptions); }
        catch (JsonException) { moves = null; }
        moves ??= new Dictionary<string, Move>(StringComparer.Ordinal);
        int invalid = 0, registrations = 0;
        foreach (var entry in moves.ToArray())
        {
            var move = entry.Value;
            if (move?.RegisterOnly == true)
            {
                moves.Remove(entry.Key);
                registrations++;
            }
            else if (!HasFurniturePose(move))
            {
                moves.Remove(entry.Key);
                invalid++;
            }
        }
        if (invalid > 0) MelonLoader.MelonLogger.Warning("[Furniture] Ignored " + invalid + " invalid legacy placements in " + area);
        if (registrations > 0) MelonLoader.MelonLogger.Msg("[Furniture] Ignored " + registrations + " automatic registrations in " + area);
        foreach (var move in moves.Values) revision = Math.Max(revision, move.Revision);
        Saved.Add(area, moves);
        return moves;
    }

    private static void AcceptMove(string area, Move move, int sender = 0)
    {
        if (!HasFurniturePose(move) || string.IsNullOrEmpty(move.Id) || move.Id.Length > 100 ||
            (move.Anchor?.Length ?? 0) > 8 || (move.ParentAnchor?.Length ?? 0) > 8 ||
            move.Parent == null || move.Parent.Length > 100 || move.Id == move.Parent || move.Version != 2 ||
            !Guid.TryParseExact(move.Operation, "N", out _) || !ValidFurnitureAsset(move)) return;
        var moves = GetScene(area);
        string ancestor = move.Parent;
        for (int depth = 0; ancestor.Length > 0; depth++)
        {
            if (ancestor == move.Id || depth > moves.Count) return;
            if (!moves.TryGetValue(ancestor, out var parent)) break;
            ancestor = parent.Parent ?? "";
        }
        if (!move.RegisterOnly && !AcceptedOperations.Contains(move.Operation))
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
        if (area != MyMod.level_guid || !HasFurniturePose(move) || move.RegisterOnly || string.IsNullOrEmpty(move.Id)) return;
        if (LatestRevision.TryGetValue(move.Id, out long latest) && latest >= move.Revision) return;
        LatestRevision[move.Id] = move.Revision;
        Unapplied[move.Id] = move;
    }

    private static bool HasFurniturePose(Move move) => move?.Pose is { Valid: true } pose &&
        (pose.X != 0f || pose.Y != 0f || pose.Z != 0f) &&
        (pose.Qx != 0f || pose.Qy != 0f || pose.Qz != 0f || pose.Qw != 0f);

    private static bool ValidFurnitureAsset(Move move) => string.IsNullOrEmpty(move.Asset) ||
        (Guid.TryParseExact(move.Asset, "N", out _) && move.AssetName != null && move.AssetName.Length <= 128 &&
         float.IsFinite(move.Sx) && float.IsFinite(move.Sy) && float.IsFinite(move.Sz) &&
         Mathf.Abs(move.Sx) <= 20f && Mathf.Abs(move.Sy) <= 20f && Mathf.Abs(move.Sz) <= 20f);

    private static bool Apply(Move move, PlacementLookup lookup)
    {
        if (PlaceableManager.s_IsDeserializing) return false;
        if (!string.IsNullOrEmpty(move.Asset) && !ValidFurnitureAsset(move)) return true;
        var obj = Find(move.Id, move.Anchor, lookup);
        if (obj == null && !string.IsNullOrEmpty(move.Asset) && ValidFurnitureAsset(move))
        {
            if (move.Parent.Length > 0 && Find(move.Parent, move.ParentAnchor, lookup) == null) return false;
            var saved = new PlaceableSaveData
            {
                m_Addressable = new AssetReferencePlaceable(move.Asset), m_Name = move.AssetName,
                m_Position = move.Pose.Position, m_Rotation = move.Pose.Rotation, m_ActiveSelf = true,
                m_Scale = new Vector3(move.Sx, move.Sy, move.Sz)
            };
            creatingReplica = true;
            try
            {
                var replica = Placeable.FindOrCreateAndDeserialize(move.Id, saved);
                if (replica != null)
                {
                    lookup.Register(replica);
                    obj = replica.gameObject;
                }
            }
            catch (Exception ex)
            {
                if (FurnitureApplyWarnings.Add(move.Id))
                    MelonLoader.MelonLogger.Warning("[Furniture] Could not create " + move.AssetName + ": " + ex.Message);
                return true;
            }
            finally { creatingReplica = false; }
        }
        if (obj == null) return false;
        var placeable = obj.GetComponent<Placeable>();
        if (placeable != null && (placeable.m_Invalidated || PlaceableManager.GetPlacementState(placeable) == PlacementState.Removed)) return true;
        bool fixedBed = IsFixedBed(obj);
        if (fixedBed && BedRest.IsOccupied(obj.GetComponentInChildren<Bed>(true))) return false;
        var player = GameManager.GetPlayerManagerComponent();
        if (player != null && player.m_ObjectToPlace == obj) return false;
        if (!fixedBed && move.Parent.Length > 0 && Unapplied.ContainsKey(move.Parent)) return false;
        var decoration = obj.GetComponent<DecorationItem>();
        if (decoration == null && placeable == null) return true;
        if (decoration != null && !fixedBed)
        {
            var parent = Find(move.Parent, move.ParentAnchor, lookup);
            if (move.Parent.Length > 0 && parent == null) return false;
            var parentDecoration = parent != null ? parent.GetComponent<DecorationItem>() : null;
            if (parentDecoration != decoration.DecorationParent &&
                (parent == null || !parent.transform.IsChildOf(obj.transform)))
                decoration.SetParent(parentDecoration);
        }
        obj.transform.SetPositionAndRotation(move.Pose.Position, move.Pose.Rotation);
        return true;
    }

    internal static bool ProbeFurniture()
    {
        var lookup = new PlacementLookup();
        foreach (var placeable in UnityEngine.Object.FindObjectsOfType<Placeable>())
        {
            if (placeable == null || placeable.m_Invalidated || string.IsNullOrEmpty(placeable.m_Guid)) continue;
            var obj = placeable.gameObject;
            var move = CaptureFurniture(obj);
            if (move == null || !string.IsNullOrEmpty(move.Parent) || IsFixedBed(obj)) continue;
            return lookup.Find(placeable.m_Guid) == placeable &&
                lookup.Find(Guid.NewGuid().ToString("N")) == null &&
                Find(move.Id, move.Anchor, lookup) == obj && Apply(move, lookup);
        }
        return false;
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

        private static void Postfix(GearPlacePoint __instance)
        {
            if (InWorld && __instance.m_FireToAttach != null)
                CookingPoints[__instance.GetInstanceID()] = __instance;
        }
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
        float now = Time.realtimeSinceStartup;
        if (now < nextCookingPublish) return;
        nextCookingPublish = now + .45f;
        if (now >= nextCookingScan)
        {
            nextCookingScan = now + 3f;
            foreach (var slot in UnityEngine.Object.FindObjectsOfType<CookingSlot>())
                if (slot.m_GearPlacePoint != null)
                    CookingPoints[slot.m_GearPlacePoint.GetInstanceID()] = slot.m_GearPlacePoint;
            foreach (var pot in UnityEngine.Object.FindObjectsOfType<CookingPotItem>())
                if (pot.GetGearPlacePoint() != null) LocalPots[pot.GetInstanceID()] = pot;
        }

        var potsByPoint = new Dictionary<int, CookingPotItem>();
        foreach (var entry in LocalPots.ToArray())
        {
            var pot = entry.Value;
            var point = pot != null && pot.isActiveAndEnabled ? pot.GetGearPlacePoint() : null;
            if (point == null || pot.m_GearItem == null)
            {
                LocalPots.Remove(entry.Key);
                continue;
            }
            int id = point.GetInstanceID();
            CookingPoints[id] = point;
            if (!potsByPoint.TryGetValue(id, out var previous) || (!previous.IsCookingSomething() && pot.IsCookingSomething()))
                potsByPoint[id] = pot;
        }

        LocalCooking.Clear();
        foreach (var entry in CookingPoints.ToArray())
        {
            var point = entry.Value;
            if (point == null)
            {
                CookingPoints.Remove(entry.Key);
                continue;
            }
            var gear = point.GetPlacedGear();
            potsByPoint.TryGetValue(entry.Key, out var attachedPot);
            var pot = gear?.m_CookingPotItem;
            if (attachedPot != null && (pot == null || (!pot.IsCookingSomething() && attachedPot.IsCookingSomething()))) pot = attachedPot;
            if (pot != null && pot.m_GearItem != null) gear = pot.m_GearItem;
            if (gear == null || gear.m_InPlayerInventory) continue;
            string name = gear.GetGearName();
            if (!IsCookingGearName(name)) continue;
            int key = gear.GetInstanceID();
            LocalCooking[key] = (point, gear, pot);
            int liquid = pot == null ? 0 : pot.m_LitersSnowBeingMelted.ToLiters() > 0 ? 2 :
                pot.m_LitersWaterBeingBoiled.ToLiters() > 0 ? 1 : 0;
            var visible = new System.Text.StringBuilder();
            foreach (var renderer in gear.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (visible.Length >= 256) break;
                if (liquid != 0 && renderer == pot.m_GrubMeshRenderer) continue;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    visible.Append(PotMeshKey(renderer, gear.transform));
            }
            var food = pot?.m_GearItemBeingCooked;
            bool canTake = pot == null || CanTakeFromPot(pot);
            PublishedPots.Add(key);
            var slotPose = Pose.From(point.transform);
            var gearPose = (gear.transform.position - point.transform.position).sqrMagnitude < .5625f ? Pose.From(gear.transform) : slotPose;
            PublishPot(key, new Pot(name, gearPose, slotPose, visible.ToString(),
                food != null ? food.GetGearName() : "", pot?.GetCookingState().ToString() ?? "",
                pot != null && float.IsFinite(pot.m_MinutesUntilCooked) ? Mathf.CeilToInt(pot.m_MinutesUntilCooked) : 0, canTake, liquid, pot?.GetHoverText() ?? "", pot?.m_LitersWaterBeingBoiled.ToLiters() ?? 0f));
        }
        foreach (int key in PublishedPots.ToArray())
            if (!LocalCooking.ContainsKey(key) && PublishedPots.Remove(key)) PublishPot(key, null);
        foreach (int key in RejectedCooking.ToArray())
        {
            if (LocalCooking.ContainsKey(key)) ReleaseCookware(key);
            else RejectedCooking.Remove(key);
        }
    }

    private static bool IsCookingGearName(string name) => !string.IsNullOrEmpty(name) && name.Length <= 64 &&
        name.StartsWith("GEAR_", StringComparison.OrdinalIgnoreCase);

    private static bool CanTakeFromPot(CookingPotItem pot)
    {
        return !pot.m_IsBeingInspected && (!pot.IsDummyPot() || pot.m_GearItemBeingCooked != null);
    }

    private static bool IsPotName(string name) => name != null && name.StartsWith("GEAR_", StringComparison.Ordinal) &&
        name.Length <= 64 && (name.Contains("CookingPot", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Skillet", StringComparison.OrdinalIgnoreCase) || name.Contains("RecycledCan", StringComparison.OrdinalIgnoreCase));

    private static void PublishPot(int key, Pot pot)
    {
        string data = JsonSerializer.Serialize(pot);
        if (MyMod.iAmHost) AcceptCookingState(MyMod.level_guid, 0, key, pot, data);
        else SendToHost(2, MyMod.level_guid, data, key);
    }

    private static void AcceptCookingState(string area, int owner, int key, Pot pot, string data)
    {
        var id = (area, owner, key);
        if (pot == null) CookingClaims.Remove(id);
        else
        {
            if (!ValidPot(pot)) return;
            foreach (var occupied in CookingClaims)
            {
                if (occupied.Key.Scene != area || occupied.Key.Owner == owner ||
                    occupied.Value.Expires <= Time.realtimeSinceStartup ||
                    (occupied.Value.State.Slot.Position - pot.Slot.Position).sqrMagnitude >= SlotMatchSquared) continue;
                if (owner == 0) RejectCooking(key);
                else
                {
                    using var rejection = MakeServerPacket(6, area, "", 0, key);
                    ServerSend.SendUDPData(owner, rejection);
                }
                return;
            }
            if (!CookingClaims.ContainsKey(id) && CookingClaims.Count >= 512) return;
            CookingClaims[id] = new SlotClaim(pot, Time.realtimeSinceStartup + 7f);
        }
        ReceivePot(area, owner, key, pot);
        SendToPeers(2, area, data, owner, key);
    }

    private static void RejectCooking(int key)
    {
        if (RejectedCooking.Add(key)) HUDMessage.AddMessage("Этот слот уже занял другой игрок");
    }

    internal static bool RemotePotAt(GearPlacePoint point) => FindRemotePot(point).HasValue;

    private static KeyValuePair<(int Owner, int Key), RemotePot>? FindRemotePot(GearPlacePoint point)
    {
        if (point == null || !MyMod.InOnline()) return null;
        Vector3 position = point.transform.position;
        foreach (var pot in RemotePots)
            if ((pot.Value.State.Slot.Position - position).sqrMagnitude < SlotMatchSquared) return pot;
        return null;
    }

    private static KeyValuePair<(int Owner, int Key), RemotePot>? FindRemoteCookware(GameObject target)
    {
        if (target == null || !InWorld) return null;
        foreach (var pair in RemotePots)
            if (pair.Value.Visual == target) return pair;
        var slot = target.GetComponentInParent<CookingSlot>();
        return FindRemotePot(slot?.m_GearPlacePoint ?? target.GetComponentInParent<GearPlacePoint>());
    }

    internal static bool IsCookwareReplica(GameObject target)
    {
        if (target == null) return false;
        foreach (var pot in RemotePots.Values)
            if (pot.Visual == target) return true;
        return false;
    }

    internal static GameObject AimCookware(float range, RaycastHit worldHit = default)
    {
        if (RemotePots.Count == 0) return null;
        var camera = GameManager.GetVpFPSCamera()?.GetCameraComponent();
        if (camera == null) return null;
        var ray = new Ray(camera.transform.position, camera.transform.forward);
        if (worldHit.collider == null)
            Physics.Raycast(ray, out worldHit, range, Utils.m_PhysicalCollisionLayerMask, QueryTriggerInteraction.Ignore);
        float nearest = worldHit.collider != null
            ? Mathf.Min(range, Vector3.Dot(worldHit.point - ray.origin, ray.direction) + .025f) : range;
        GameObject selected = null;
        foreach (var remote in RemotePots.Values)
        {
            var box = remote.Hitbox;
            if (box == null) continue;
            var localRay = new Ray(box.transform.InverseTransformPoint(ray.origin), box.transform.InverseTransformDirection(ray.direction));
            if (!new Bounds(box.center, box.size).IntersectRay(localRay, out float distance) || distance > nearest) continue;
            selected = remote.Visual;
            nearest = distance;
        }
        return selected;
    }

    internal static string CookingHover(GameObject target) => FindRemoteCookware(target) is { } entry ? CookingLabel(entry) : null;

    private static string CookingLabel(KeyValuePair<(int Owner, int Key), RemotePot> entry)
    {
        var state = entry.Value.State;
        string title = GameCompat.GetGearDisplayName(state.Food.Length > 0 ? state.Food : state.Gear);
        string description = state.Liquid == 2 ? "Тает снег" : state.Liquid == 1 ? "Кипятится вода" : state.Food.Length > 0 ? "Готовится" : "Пустая посуда";
        if (state.Mode == "Ready") description = state.Liquid == 1 ? "Питьевая вода" : "Готово";
        else if (state.Mode == "Ruined") description = state.Liquid == 1 ? "Вода выкипела" : "Испорчено";
        else if (state.Minutes > 0 && (state.Liquid != 0 || state.Food.Length > 0)) description += $" - До готовности: {state.Minutes} мин.";
        string text = !string.IsNullOrWhiteSpace(state.NativeHover) && (state.Food.Length > 0 || state.Liquid != 0)
            ? state.NativeHover.Trim() : title + "\n" + description;
        if (!text.Contains("\n")) text += "\n" + description;
        int owner = entry.Key.Owner;
        if (owner >= 0 && owner < MyMod.playersData.Count && !string.IsNullOrEmpty(MyMod.playersData[owner]?.m_Name))
            text += " · " + MyMod.playersData[owner].m_Name;
        return text;
    }

    [HarmonyPatch(typeof(CookingSlot), nameof(CookingSlot.UpdateInteraction), new[] { typeof(float) })]
    private static class RefreshRemoteCookingHover
    {
        private static void Postfix(CookingSlot __instance)
        {
            if (__instance.m_GearPlacePoint?.GetPlacedGear() != null) return;
            string label = CookingHover(__instance.gameObject);
            if (label == null) return;
            __instance.HoverText = label;
            __instance.m_CanInteract = true;
        }
    }

    [HarmonyPatch(typeof(CookingSlot), nameof(CookingSlot.PerformInteraction))]
    private static class InteractWithRemoteCookware
    {
        private static bool Prefix(CookingSlot __instance, ref bool __result)
        {
            if (__instance.m_GearPlacePoint?.GetPlacedGear() != null ||
                FindRemotePot(__instance.m_GearPlacePoint) is not { } entry) return true;
            __result = OpenCookingActions(__instance.gameObject);
            return false;
        }
    }

    private sealed record CookingInspection(string Scene, int Owner, int Key, int Action, GameObject Root, GearItem Food);
    private static CookingInspection inspection;

    internal static bool OpenCookingActions(GameObject target)
    {
        if (FindRemoteCookware(target) is not { } entry) return false;
        if (inspection != null) return true;
        var state = entry.Value.State;
        var player = GameManager.GetPlayerManagerComponent();
        var prefab = MyMod.GetGearItemObject(state.Gear);
        if (player == null || prefab == null || !state.CanTake) return true;
        var root = UnityEngine.Object.Instantiate(prefab);
        root.transform.position = state.Pose.Position;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        var pot = root.GetComponent<CookingPotItem>();
        if (pot == null)
        {
            UnityEngine.Object.Destroy(root);
            return true;
        }
        pot.enabled = false;
        pot.m_MinutesUntilCooked = state.Minutes;
        if (Enum.TryParse<CookingPotItem.CookingState>(state.Mode, out var mode)) pot.m_CookingState = mode;
        pot.m_LitersWaterBeingBoiled = GameCompat.Liters(state.Liters);
        var foodPrefab = state.Food.Length == 0 ? null : MyMod.GetGearItemObject(state.Food);
        GearItem food = null;
        if (foodPrefab != null)
        {
            var foodObject = UnityEngine.Object.Instantiate(foodPrefab, root.transform);
            foreach (var collider in foodObject.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            food = foodObject.GetComponent<GearItem>();
            pot.m_GearItemBeingCooked = food;
        }
        inspection = new CookingInspection(MyMod.level_guid, entry.Key.Owner, entry.Key.Key, TakeAction(state), root, food);
        try
        {
            if (inspection.Action == 1) player.EnterInspectGearModeFromCookingPot(root.GetComponent<GearItem>(), pot);
            else pot.StartInspectMode();
            if (!player.IsInspectModeActive()) EndCookingInspection();
        }
        catch (Exception ex)
        {
            EndCookingInspection();
            MelonLoader.MelonLogger.Warning("[Cooking] Inspect: " + ex.Message);
        }
        return true;
    }

    private static void EndCookingInspection()
    {
        var current = inspection;
        inspection = null;
        if (current?.Food != null) UnityEngine.Object.Destroy(current.Food.gameObject);
        if (current?.Root != null) UnityEngine.Object.Destroy(current.Root);
    }

    [HarmonyPatch]
    private static class TakeFromSharedInspection
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods() => new[]
        {
            AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPickupFromCookingPotInspection)),
            AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnEquipFromCookingPotInspection))
        };

        private static bool Prefix(PlayerManager __instance)
        {
            var current = inspection;
            if (current == null) return true;
            __instance.ExitInspectGearMode(true);
            if (current.Scene == MyMod.level_guid)
            {
                if (MyMod.iAmHost) RequestCookwareRelease(0, current.Owner, current.Key, current.Scene, current.Action);
                else SendToHost(4, current.Scene, $"{current.Owner}:{current.Action}", current.Key);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.DoSpecialActionFromInspectMode))]
    private static class DisableRemoteCookSkip
    {
        private static bool Prefix(CookingPotItem __instance, ref bool __result)
        {
            if (inspection?.Root != __instance.gameObject) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ExitInspectGearMode))]
    private static class CloseSharedInspection
    {
        private static void Postfix() => EndCookingInspection();
    }

    private static int TakeAction(Pot state) => state.Liquid == 2 ? 4 : state.Liquid == 1 ? 3 : state.Food.Length > 0 ? 2 : 1;

    private static void RequestCookwareRelease(int requester, int owner, int key, string area, int action)
    {
        if (owner == requester || owner < 0 ||
            (owner == 0 ? MyMod.level_guid != area :
             owner >= MyMod.playersData.Count || MyMod.playersData[owner]?.m_LevelGuid != area)) return;
        if (!CookingClaims.TryGetValue((area, owner, key), out var claim) || !claim.State.CanTake || TakeAction(claim.State) != action) return;
        if (owner == 0) ReleaseCookware(key, action, requester);
        else
        {
            using var packet = MakeServerPacket(4, area, action.ToString(), requester, key);
            ServerSend.SendUDPData(owner, packet);
        }
    }

    private static void ReleaseCookware(int key, int expectedAction = 0, int requester = -1)
    {
        if (!LocalCooking.TryGetValue(key, out var entry) || entry.Point == null || entry.Gear == null) return;
        var point = entry.Point;
        var pot = entry.Pot;
        var gear = entry.Gear;
        if (pot != null && (pot.GetGearPlacePoint() != point || !CanTakeFromPot(pot))) return;
        if (pot == null && point.GetPlacedGear() != gear) return;

        float water = pot == null ? 0 : pot.m_LitersWaterBeingBoiled.ToLiters();
        bool snow = pot != null && pot.m_LitersSnowBeingMelted.ToLiters() > 0f;
        int currentAction = snow ? 4 : water > 0f ? 3 : pot?.m_GearItemBeingCooked != null ? 2 : 1;
        if (expectedAction != 0 && currentAction != expectedAction) return;
        if (snow || water > 0f)
        {
            var state = pot.GetCookingState();
            pot.m_LitersSnowBeingMelted = GameCompat.Liters(0);
            pot.m_LitersWaterBeingBoiled = GameCompat.Liters(0);
            pot.UpdateVisuals();
            if (!snow && state != CookingPotItem.CookingState.Ruined)
            {
                int recipient = requester >= 0 ? requester : MyMod.iAmHost ? 0 : ClientUser.myId;
                var transfer = new WaterTransfer(Guid.NewGuid(), recipient, water, state == CookingPotItem.CookingState.Ready);
                if (MyMod.iAmHost) DeliverWater(MyMod.level_guid, transfer);
                else SendToHost(8, MyMod.level_guid, JsonSerializer.Serialize(transfer));
            }
        }
        else if (pot != null && pot.m_GearItemBeingCooked != null)
        {
            var raw = pot.m_GearItemBeingCooked;
            var cookingState = pot.GetCookingState();
            bool cancelRaw = cookingState == CookingPotItem.CookingState.Cooking;
            bool hot = cookingState == CookingPotItem.CookingState.Ready && pot.IsItemBeingCookedHot();
            if (!cancelRaw) pot.MaybeSpawnGearItemForInspectMode();
            var source = cancelRaw ? raw : pot.GetGearItemForInspectMode() ?? raw;
            if (source == null || source == pot.m_GearItem) return;
            var output = UnityEngine.Object.Instantiate(source.gameObject, point.transform.position, source.transform.rotation).GetComponent<GearItem>();
            if (cookingState == CookingPotItem.CookingState.Ready && output.m_FoodItem != null &&
                (hot || output.m_FoodItem.m_HeatedWhenCooked) && output.m_FoodItem.m_HeatPercent <= 0f)
                pot.SetHeatPercentWhenCooked(output.m_FoodItem);
            if (cancelRaw && !pot.IsDummyPot()) pot.DestroyGearItemBeingCooked();
            else pot.PickUpCookedGearItem(addToInventory: false);
            if ((pot != null && pot.m_GearItemBeingCooked == raw) || (source != null && source.m_InPlayerInventory))
            {
                UnityEngine.Object.Destroy(output.gameObject);
                return;
            }
            if (source != null)
            {
                if (point.GetPlacedGear() == source) point.ResetPlacedGear();
                UnityEngine.Object.Destroy(source.gameObject);
            }
            TransferCookingOutput(output, point, requester);
        }
        else
        {
            if (pot != null)
            {
                LocalPots.Remove(pot.GetInstanceID());
                pot.DetachFromFire();
            }
            if (point.GetPlacedGear() == gear) point.ResetPlacedGear();
            LocalCooking.Remove(key);
            PublishedPots.Remove(key);
            PublishPot(key, null);
            TransferCookingOutput(gear, point, requester);
        }
    }

    private static void TransferCookingOutput(GearItem gear, GearPlacePoint point, int requester)
    {
        if (requester >= 0)
        {
            string json = gear.SerializeToString();
            var food = gear.m_FoodItem;
            var transfer = new GearTransfer(Guid.NewGuid(), requester, gear.GetGearName(), json,
                food != null && float.IsFinite(food.m_HeatPercent) ? food.m_HeatPercent : -1f, food?.m_NumTimesHeatedUp ?? 0);
            string data = JsonSerializer.Serialize(transfer);
            if (!string.IsNullOrEmpty(json) && data.Length <= 10000)
            {
                if (MyMod.iAmHost) DeliverGear(MyMod.level_guid, transfer, data);
                else SendToHost(9, MyMod.level_guid, data);
                UnityEngine.Object.Destroy(gear.gameObject);
                return;
            }
        }
        DropCookingOutput(gear, point, requester);
    }

    private static bool AcceptOutput(Guid id)
    {
        if (id == Guid.Empty) return false;
        float now = Time.realtimeSinceStartup;
        foreach (var entry in ReceivedOutputs.ToArray())
            if (entry.Value <= now) ReceivedOutputs.Remove(entry.Key);
        return ReceivedOutputs.TryAdd(id, now + 30f);
    }

    private static void DeliverGear(string area, GearTransfer transfer, string data)
    {
        if (transfer == null || !IsCookingGearName(transfer.Gear) || IsCookingDummyName(transfer.Gear) || string.IsNullOrEmpty(transfer.Json)) return;
        if (transfer.Requester == (MyMod.iAmHost ? 0 : ClientUser.myId))
        {
            var player = GameManager.GetPlayerManagerComponent();
            var prefab = MyMod.GetGearItemObject(transfer.Gear);
            if (player == null || prefab == null || MyMod.level_guid != area || !AcceptOutput(transfer.Id)) return;
            var gear = UnityEngine.Object.Instantiate(prefab, player.transform.position, Quaternion.identity).GetComponent<GearItem>();
            gear.DeserializeFromJson(transfer.Json);
            gear.m_LastUpdatedTODHours = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
            gear.ManualStart();
            RestoreFoodHeat(gear, transfer);
            if (!player.ProcessPickupItemInteractionCompat(gear)) MyMod.SendDropItem(gear, cookingOutput: true);
            else RestoreFoodHeat(gear, transfer);
        }
        else if (MyMod.iAmHost && transfer.Requester > 0 && transfer.Requester < MyMod.playersData.Count &&
            MyMod.playersData[transfer.Requester]?.m_LevelGuid == area)
        {
            using var packet = MakeServerPacket(9, area, data, 0, 0);
            ServerSend.SendUDPData(transfer.Requester, packet);
        }
    }

    private static void RestoreFoodHeat(GearItem gear, GearTransfer transfer)
    {
        if (gear == null || gear.m_FoodItem == null || !float.IsFinite(transfer.Heat) || transfer.Heat < 0f) return;
        gear.m_FoodItem.m_HeatPercent = transfer.Heat;
        gear.m_FoodItem.m_NumTimesHeatedUp = transfer.TimesHeated;
    }

    private static void DropCookingOutput(GearItem gear, GearPlacePoint point, int requester)
    {
        gear.gameObject.SetActive(true);
        var slot = point.transform.position;
        var origin = slot + point.transform.forward * .85f;
        var recipient = requester >= 0 && requester < MyMod.playersData.Count ? MyMod.playersData[requester] : null;
        if (recipient != null && recipient.m_LevelGuid == MyMod.level_guid && (recipient.m_Position - slot).sqrMagnitude < 16f)
        {
            var offset = Vector3.ProjectOnPlane(slot - recipient.m_Position, Vector3.up);
            origin = recipient.m_Position + Vector3.ClampMagnitude(offset, .45f);
        }
        if (Physics.Raycast(origin + Vector3.up * .35f, Vector3.down, out var floor, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            origin = floor.point + Vector3.up * .06f;
        gear.transform.position = origin;
        MyMod.SendDropItem(gear, samepose: true, cookingOutput: true);
    }

    private static void DeliverWater(string area, WaterTransfer transfer)
    {
        if (transfer == null || !float.IsFinite(transfer.Liters) || transfer.Liters <= 0f || transfer.Liters > 10f) return;
        if (transfer.Requester == (MyMod.iAmHost ? 0 : ClientUser.myId))
        {
            var inventory = GameManager.GetInventoryComponent();
            if (MyMod.level_guid != area || inventory == null || !AcceptOutput(transfer.Id)) return;
            inventory.AddToWaterSupply(GameCompat.Liters(transfer.Liters),
                transfer.Potable ? LiquidQuality.Potable : LiquidQuality.NonPotable);
            HUDMessage.AddMessage(transfer.Potable ? "Питьевая вода добавлена в инвентарь" : "Непитьевая вода добавлена в инвентарь");
        }
        else if (MyMod.iAmHost && transfer.Requester > 0 && transfer.Requester < MyMod.playersData.Count &&
            MyMod.playersData[transfer.Requester]?.m_LevelGuid == area)
        {
            using var packet = MakeServerPacket(8, area, JsonSerializer.Serialize(transfer), 0, 0);
            ServerSend.SendUDPData(transfer.Requester, packet);
        }
    }

    private static void ReceivePot(string area, int owner, int key, Pot pot)
    {
        if (area != MyMod.level_guid || owner == (MyMod.iAmHost ? 0 : ClientUser.myId)) return;
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
        var hitbox = previous?.Hitbox;
        if (previous != null && (previous.State.Gear != pot.Gear || previous.State.Food != pot.Food || previous.State.Meshes != pot.Meshes || previous.State.Liquid != pot.Liquid) && visual != null)
        {
            UnityEngine.Object.Destroy(visual);
            visual = null;
        }
        if (visual == null)
        {
            visual = CreatePotVisual(pot);
            hitbox = visual != null ? visual.GetComponent<BoxCollider>() : null;
        }
        if (visual != null) visual.transform.SetPositionAndRotation(pot.Pose.Position, pot.Pose.Rotation);
        RemotePots[identity] = new RemotePot(pot, visual, hitbox, Time.realtimeSinceStartup + 7f);
    }

    private static bool ValidPot(Pot pot) => float.IsFinite(pot.Liters) && pot.Liters >= 0f && pot.Liters <= 20f && IsCookingGearName(pot.Gear) && pot.Pose?.Valid == true && pot.Slot?.Valid == true &&
        pot.Meshes != null && pot.Meshes.Length <= 256 && pot.Meshes.Length % 8 == 0 && pot.Liquid >= 0 && pot.Liquid <= 2 && pot.Food != null && pot.Food.Length <= 80 && pot.Mode != null && pot.Mode.Length <= 32 && pot.NativeHover != null && pot.NativeHover.Length <= 300;

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
        bool useFood = IsCookingDummyName(pot.Gear) && IsCookingGearName(pot.Food);
        var prefab = MyMod.GetGearItemObject(useFood ? pot.Food : pot.Gear);
        if (prefab == null) return null;
        var root = new GameObject("TLD Coop cooking slot");
        var originalCollider = prefab.GetComponent<Collider>() ?? prefab.GetComponentInChildren<Collider>(true);
        root.layer = originalCollider != null ? originalCollider.gameObject.layer : prefab.layer;
        Bounds hitBounds = default;
        bool hasHitBounds = false;
        var cooking = prefab.GetComponent<CookingPotItem>();
        var visible = new HashSet<string>();
        for (int i = 0; i < pot.Meshes.Length; i += 8) visible.Add(pot.Meshes.Substring(i, 8));
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null) continue;
            Mesh sourceMesh = filter.sharedMesh;
            var materials = renderer.sharedMaterials;
            if (pot.Liquid != 0 && cooking != null && renderer == cooking.m_GrubMeshRenderer)
            {
                sourceMesh = pot.Liquid == 2 ? cooking.m_SnowMesh : cooking.m_WaterMesh;
                var liquidMaterials = pot.Liquid == 2 ? cooking.m_MeltSnowMaterialsList : cooking.m_BoilWaterPotMaterialsList;
                if (liquidMaterials != null && liquidMaterials.Length != 0)
                    materials = new[] { liquidMaterials[0] };
                else continue;
            }
            else if (useFood || visible.Count == 0)
            {
                if (!renderer.enabled || !renderer.gameObject.activeSelf || (cooking != null && renderer == cooking.m_GrubMeshRenderer)) continue;
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
            var bounds = sourceMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                var point = mesh.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                if (hasHitBounds) hitBounds.Encapsulate(point);
                else { hitBounds = new Bounds(point, Vector3.zero); hasHitBounds = true; }
            }
        }
        var hitbox = root.AddComponent<BoxCollider>();
        hitbox.center = hasHitBounds ? hitBounds.center : Vector3.up * .07f;
        hitbox.size = hasHitBounds ? Vector3.Max(hitBounds.size + Vector3.one * .02f, Vector3.one * .055f) : new Vector3(.20f, .15f, .20f);
        return root;
    }

    internal static void Tick()
    {
        if (MyMod.iAmHost)
        {
            Server.clients.TryGetValue(1, out var session);
            if (!ReferenceEquals(session, cookingSession))
            {
                cookingSession = session;
                CookingClaims.Clear();
            }
            float current = Time.realtimeSinceStartup;
            foreach (var claim in CookingClaims.ToArray())
                if (claim.Value.Expires <= current) CookingClaims.Remove(claim.Key);
        }
        string area = InWorld ? MyMod.level_guid : null;
        if (area != scene)
        {
            foreach (var pot in RemotePots.Values)
                if (pot.Visual != null) UnityEngine.Object.Destroy(pot.Visual);
            RemotePots.Clear();
            LocalPots.Clear();
            LocalCooking.Clear();
            CookingPoints.Clear();
            RejectedCooking.Clear();
            nextCookingScan = 0f;
            nextCookingPublish = 0f;
            PublishedPots.Clear();
            Pending.Clear();
            Unapplied.Clear();
            LatestRevision.Clear();
            FurnitureApplyWarnings.Clear();
            nextFurnitureApply = 0f;
            scene = area;
            ContainerLockSync.Prepare(area);
            SceneRepairSync.Prepare(area);
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
        if (Unapplied.Count > 0 && now >= nextFurnitureApply)
        {
            nextFurnitureApply = now + .5f;
            var lookup = new PlacementLookup();
            foreach (var pair in Unapplied.ToArray())
                if (Apply(pair.Value, lookup)) Unapplied.Remove(pair.Key);
        }
        TickPots();
        if (MyMod.iAmHost || now < nextRequest) return;
        nextRequest = now + 8f;
        foreach (var move in Pending.Values) SendToHost(1, area, JsonSerializer.Serialize(move));
        SendToHost(0, area, "");
        foreach (var state in ContainerLockSync.Requests()) PublishLock(state);
        foreach (var id in SceneRepairSync.Requests(area)) PublishRepair(area, id);
    }

    internal static void ReceiveClient(int from, Packet packet)
    {
        byte kind = packet.ReadByte();
        string area = packet.ReadString();
        int key = packet.ReadInt();
        string data = packet.ReadString();
        if (from <= 0 || from >= MyMod.playersData.Count || MyMod.playersData[from]?.m_LevelGuid != area ||
            !WildlifeSpawnAuthorityPolicy.WorldScene(area) || area.Length > 160 ||
            area.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || data.Length > (kind == 9 ? 10000 : 1100)) return;
        try
        {
            if (kind == 0)
            {
                foreach (var move in GetScene(area).Values)
                {
                    using var reply = MakeServerPacket(1, area, JsonSerializer.Serialize(move), 0, 0);
                    ServerSend.SendUDPData(from, reply);
                }
                foreach (var id in SceneRepairSync.Snapshot(area))
                {
                    using var reply = MakeServerPacket(11, area, id, 0, 0);
                    ServerSend.SendUDPData(from, reply);
                }
            }
            else if (kind == 11 && SceneRepairSync.Accept(area, data)) SendToPeers(11, area, data);
            else if (kind == 10) DeathRespawn.ReceiveClient(from, area, key, data);
            else if (kind == 1) AcceptMove(area, JsonSerializer.Deserialize<Move>(data), from);
            else if (kind == 2)
            {
                var pot = JsonSerializer.Deserialize<Pot>(data);
                AcceptCookingState(area, from, key, pot, data);
            }
            else if (kind == 4)
            {
                var request = data.Split(':');
                if (request.Length == 2 && int.TryParse(request[0], out int owner) && int.TryParse(request[1], out int action))
                    RequestCookwareRelease(from, owner, key, area, action);
            }
            else if (kind == 8) DeliverWater(area, JsonSerializer.Deserialize<WaterTransfer>(data));
            else if (kind == 9) DeliverGear(area, JsonSerializer.Deserialize<GearTransfer>(data), data);
            else if (kind == 7)
            {
                var state = ContainerLockSync.Accept(area, JsonSerializer.Deserialize<ContainerLockSync.State>(data));
                if (state != null) SendToPeers(7, area, JsonSerializer.Serialize(state));
            }
            else if (kind == 5 && MyMod.playersData[from].m_LevelGuid == area)
            {
                if (area != MyMod.level_guid || ThrownGearSync.Receive(area, from, key, data))
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
        if (!InWorld || area != MyMod.level_guid || data.Length > (kind == 9 ? 10000 : 1100)) return;
        try
        {
            if (kind == 10) DeathRespawn.Receive(area, key, data);
            else if (kind == 11) SceneRepairSync.Receive(area, data);
            else if (kind == 1) ReceiveMove(area, JsonSerializer.Deserialize<Move>(data));
            else if (kind == 2) ReceivePot(area, owner, key, JsonSerializer.Deserialize<Pot>(data));
            else if (kind == 4 && int.TryParse(data, out int action) && action >= 1 && action <= 4) ReleaseCookware(key, action, owner);
            else if (kind == 8) DeliverWater(area, JsonSerializer.Deserialize<WaterTransfer>(data));
            else if (kind == 9) DeliverGear(area, JsonSerializer.Deserialize<GearTransfer>(data), data);
            else if (kind == 6) RejectCooking(key);
            else if (kind == 7) ContainerLockSync.Receive(JsonSerializer.Deserialize<ContainerLockSync.State>(data));
            else if (kind == 5 && owner != ClientUser.myId) ThrownGearSync.Receive(area, owner, key, data);
            else if (kind == 3)
            {
                foreach (var move in Pending.Values.ToArray())
                    if (move.Operation == data) Pending.Remove(move.Id);
            }
        }
        catch (JsonException) { }
    }

    internal static void SendRescueToHost(int subject, string data) => SendToHost(10, MyMod.level_guid, data, subject);

    internal static void BroadcastDowned(string area, int subject, string data) => SendToPeers(10, area, data, key: subject);

    internal static void SendRescueToGuest(int guest, string area, int subject, string data)
    {
        using var packet = MakeServerPacket(10, area, data, 0, subject);
        ServerSend.SendUDPData(guest, packet);
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

    internal static void PublishLock(ContainerLockSync.State state)
    {
        string area = MyMod.level_guid;
        if (MyMod.iAmHost)
        {
            var accepted = ContainerLockSync.Accept(area, state, fromHost: true);
            if (accepted != null) SendToPeers(7, area, JsonSerializer.Serialize(accepted));
        }
        else SendToHost(7, area, JsonSerializer.Serialize(state));
    }

    internal static void PublishRepair(string area, string id)
    {
        if (!MyMod.iAmHost) SendToHost(11, area, id);
        else if (SceneRepairSync.Accept(area, id)) SendToPeers(11, area, id);
    }

    internal static void PublishThrownFlight(int key, string data)
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
