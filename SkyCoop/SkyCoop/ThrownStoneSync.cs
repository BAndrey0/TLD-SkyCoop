using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

internal static class ThrownStoneSync
{
    private sealed record Flight(float X, float Y, float Z, float Qx, float Qy, float Qz, float Qw, int Sequence, bool End)
    {
        internal Vector3 Position => new(X, Y, Z);
        internal Quaternion Rotation => new(Qx, Qy, Qz, Qw);
        internal bool Valid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) &&
            float.IsFinite(Qx) && float.IsFinite(Qy) && float.IsFinite(Qz) && float.IsFinite(Qw) &&
            Math.Abs(X) < 100000 && Math.Abs(Y) < 100000 && Math.Abs(Z) < 100000 &&
            Qx * Qx + Qy * Qy + Qz * Qz + Qw * Qw > .5f &&
            Qx * Qx + Qy * Qy + Qz * Qz + Qw * Qw < 1.5f && Sequence > 0;
    }

    private sealed class RemoteFlight
    {
        internal Flight State;
        internal GameObject Visual;
        internal float Expires;
    }

    private static readonly Dictionary<(int Owner, int Key), RemoteFlight> Flights = new();
    private static string scene;

    internal static void Publish(GearItem gear, int sequence, bool end)
    {
        if (gear == null || !MyMod.InOnline() || MyMod.LoadingScreenIsOn) return;
        var p = gear.transform.position;
        var q = gear.transform.rotation;
        var state = new Flight(p.x, p.y, p.z, q.x, q.y, q.z, q.w, sequence, end);
        ScenePlacementSync.PublishStoneFlight(gear.GetInstanceID(), JsonSerializer.Serialize(state));
    }

    internal static bool Receive(string area, int owner, int key, string json)
    {
        if (area != MyMod.level_guid || MyMod.LoadingScreenIsOn) return false;
        var state = JsonSerializer.Deserialize<Flight>(json);
        if (state?.Valid != true) return false;
        if (scene != area) Reset(area);
        var id = (owner, key);
        if (Flights.TryGetValue(id, out var current))
        {
            if (state.Sequence <= current.State.Sequence || current.State.End) return false;
        }
        else
        {
            if (Flights.Count >= 128) return false;
            current = new RemoteFlight();
            Flights.Add(id, current);
        }
        current.State = state;
        current.Expires = Time.realtimeSinceStartup + (state.End ? 10f : 2f);
        if (state.End)
        {
            if (current.Visual != null) UnityEngine.Object.Destroy(current.Visual);
            current.Visual = null;
            return true;
        }
        if (current.Visual == null)
        {
            current.Visual = CreateVisual();
            if (current.Visual != null) current.Visual.transform.SetPositionAndRotation(state.Position, state.Rotation);
        }
        return true;
    }

    private static GameObject CreateVisual()
    {
        var prefab = MyMod.GetGearItemObject("GEAR_Stone");
        if (prefab == null) return null;
        var root = new GameObject("SkyCoop stone in flight");
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeSelf) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
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
        string area = MyMod.InOnline() && !MyMod.LoadingScreenIsOn ? MyMod.level_guid : null;
        if (scene != area) Reset(area);
        float now = Time.realtimeSinceStartup;
        float blend = 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime);
        foreach (var pair in Flights.ToArray())
        {
            var flight = pair.Value;
            if (flight.Expires <= now)
            {
                if (flight.Visual != null) UnityEngine.Object.Destroy(flight.Visual);
                Flights.Remove(pair.Key);
            }
            else if (flight.Visual != null)
            {
                var transform = flight.Visual.transform;
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, flight.State.Position, blend),
                    Quaternion.Slerp(transform.rotation, flight.State.Rotation, blend));
            }
        }
    }

    private static void Reset(string area)
    {
        foreach (var flight in Flights.Values)
            if (flight.Visual != null) UnityEngine.Object.Destroy(flight.Visual);
        Flights.Clear();
        scene = area;
    }
}
