using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GameServer;
using HarmonyLib;
using Il2Cpp;
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

    private sealed record Move(string Id, string Parent, Pose Pose, string Operation, long Revision);
    private sealed record Pot(string Gear, Pose Pose, Pose Slot);
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
        gear != null && gear.GetGearName().EndsWith("Dummy", StringComparison.Ordinal) && IsPotName(gear.GetGearName());

    internal static bool KeepNativeCookingObject(GearItem gear) =>
        gear != null && (IsCookingDummy(gear) || gear.m_CookingPotItem?.GetGearPlacePoint() != null);

    private static bool InWorld => MyMod.InOnline() && !MyMod.LoadingScreenIsOn &&
        WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_guid);

    private static string Id(GameObject obj)
    {
        if (obj == null) return "";
        var placeable = obj.GetComponent<Placeable>();
        if (placeable != null && !string.IsNullOrEmpty(placeable.m_Guid)) return placeable.m_Guid;
        var guid = obj.GetComponent<ObjectGuid>();
        return guid != null ? guid.Get() ?? "" : "";
    }

    private static GameObject Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var obj = ObjectGuidManager.Lookup(id);
        if (obj != null) return obj;
        var placeables = UnityEngine.Object.FindObjectsOfType<Placeable>();
        for (int i = 0; i < placeables.Length; i++)
        {
            var placeable = placeables[i];
            if (placeable != null && !placeable.m_Invalidated && placeable.m_Guid == id)
                return placeable.gameObject;
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
        if (!InWorld || obj == null || IsFixedBed(obj)) return;
        var decoration = obj.GetComponent<DecorationItem>();
        if (decoration == null && obj.GetComponent<Placeable>() == null) return;
        string id = Id(obj);
        if (id.Length == 0) return;
        string parent = decoration != null && decoration.DecorationParent != null ? Id(decoration.DecorationParent.gameObject) : "";
        var move = new Move(id, parent, Pose.From(obj.transform), Guid.NewGuid().ToString("N"), 0);
        if (MyMod.iAmHost) AcceptMove(MyMod.level_guid, move);
        else
        {
            Pending[id] = move;
            SendToHost(1, MyMod.level_guid, JsonSerializer.Serialize(move));
        }
    }

    [HarmonyPatch(typeof(DecorationItem), nameof(DecorationItem.FinalizePlacement))]
    private static class DecorationPlaced
    {
        private static void Postfix(DecorationItem __instance, bool __0)
        {
            if (__0) PublishFurniture(__instance.gameObject);
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
        foreach (var move in moves.Values) revision = Math.Max(revision, move.Revision);
        Saved.Add(area, moves);
        return moves;
    }

    private static void AcceptMove(string area, Move move, int sender = 0)
    {
        if (move?.Pose?.Valid != true || string.IsNullOrEmpty(move.Id) || move.Id.Length > 100 ||
            move.Parent == null || move.Parent.Length > 100 || move.Id == move.Parent ||
            !Guid.TryParseExact(move.Operation, "N", out _)) return;
        var moves = GetScene(area);
        if (!AcceptedOperations.Contains(move.Operation))
        {
            if (moves.Count >= 2048 && !moves.ContainsKey(move.Id)) return;
            if (OperationOrder.Count >= 8192) AcceptedOperations.Remove(OperationOrder.Dequeue());
            AcceptedOperations.Add(move.Operation);
            OperationOrder.Enqueue(move.Operation);
            move = move with { Revision = ++revision };
            moves[move.Id] = move;
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
        var obj = Find(move.Id);
        if (obj == null) return false;
        if (IsFixedBed(obj)) return true;
        var player = GameManager.GetPlayerManagerComponent();
        if (player != null && player.m_ObjectToPlace == obj) return false;
        var decoration = obj.GetComponent<DecorationItem>();
        if (decoration == null && obj.GetComponent<Placeable>() == null) return true;
        if (decoration != null)
        {
            var parent = Find(move.Parent);
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
            AccessTools.Method(typeof(CookingPotItem), nameof(CookingPotItem.StartCooking))
        };
        private static void Prefix() => cookingDepth++;
        private static void Finalizer() => cookingDepth--;
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
            if (name.EndsWith("Dummy", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 5);
            if (!IsPotName(name)) continue;
            PublishedPots.Add(pair.Key);
            PublishPot(pair.Key, new Pot(name, Pose.From(pot.transform), Pose.From(point.transform)));
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

    internal static bool RemotePotAt(GearPlacePoint point) => point != null &&
        RemotePots.Values.Any(p => (p.State.Slot.Position - point.transform.position).sqrMagnitude < .04f);

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
        if (!IsPotName(pot.Gear) || pot.Pose?.Valid != true || pot.Slot?.Valid != true) return;
        if (previous == null && RemotePots.Count >= 128) return;
        var visual = previous?.Visual;
        if (previous != null && previous.State.Gear != pot.Gear && visual != null)
        {
            UnityEngine.Object.Destroy(visual);
            visual = null;
        }
        if (visual == null) visual = CreatePotVisual(pot.Gear);
        if (visual != null) visual.transform.SetPositionAndRotation(pot.Pose.Position, pot.Pose.Rotation);
        RemotePots[identity] = new RemotePot(pot, visual, Time.realtimeSinceStartup + 7f);
    }

    private static GameObject CreatePotVisual(string gear)
    {
        var prefab = MyMod.GetGearItemObject(gear);
        if (prefab == null || prefab.GetComponent<CookingPotItem>() == null) return null;
        var root = new GameObject("SkyCoop shared cookware");
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(false))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !renderer.enabled) continue;
            var mesh = new GameObject(renderer.name);
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = prefab.transform.InverseTransformPoint(renderer.transform.position);
            mesh.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * renderer.transform.rotation;
            mesh.transform.localScale = renderer.transform.lossyScale;
            mesh.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
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
                if (pot != null && (!IsPotName(pot.Gear) || pot.Pose?.Valid != true || pot.Slot?.Valid != true)) return;
                ReceivePot(area, from, key, pot);
                SendToPeers(2, area, data, from, key);
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
