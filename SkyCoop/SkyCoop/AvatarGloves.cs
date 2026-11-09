using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

internal static class AvatarGloves
{
    private sealed record Geometry(Mesh Source, Mesh Gloves, Mesh WithoutHands, int OriginalSlots);
    private static readonly Dictionary<int, Geometry> meshes = new();
    private static readonly Dictionary<(int, string), Material> materials = new();

    internal sealed class Fitting
    {
        private sealed record Attached(SkinnedMeshRenderer Renderer, Geometry Geometry, Material[] OriginalMaterials);
        private readonly List<Attached> attached = new();
        private AvatarGloveRig native;
        private string signature;

        internal void Set(Transform avatar, Transform arms, string gear, bool female)
        {
            int variant = -1;
            for (int i = 0; i < arms.childCount; i++)
                if (arms.GetChild(i).gameObject.activeSelf) { variant = i; break; }
            string key = variant + "|" + gear + "|" + female;
            if (signature == key) return;
            signature = key;
            native?.Dispose();
            native = null;
            foreach (var entry in attached)
            {
                if (entry.Renderer == null) continue;
                entry.Renderer.sharedMesh = entry.Geometry.Source;
                entry.Renderer.sharedMaterials = entry.OriginalMaterials;
            }
            attached.Clear();
            if (variant < 0 || string.IsNullOrEmpty(gear)) return;
            foreach (var renderer in arms.GetChild(variant).GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                var geometry = GetGeometry(renderer);
                var original = renderer.sharedMaterials;
                if (geometry == null || original.Length != geometry.OriginalSlots || original.Length == 0) continue;
                var baseMaterials = new Material[original.Length];
                for (int i = 0; i < original.Length; i++) baseMaterials[i] = original[i];
                if (native == null)
                {
                    try { native = AvatarGloveRig.Attach(avatar, renderer, gear, female); }
                    catch (Exception ex) { MelonLogger.Warning("[Wardrobe] Gloves " + gear + ": " + ex.Message); }
                    if (native != null)
                    {
                        renderer.sharedMesh = geometry.WithoutHands;
                        attached.Add(new Attached(renderer, geometry, baseMaterials));
                        continue;
                    }
                }
                var withGloves = new Material[original.Length + 1];
                for (int i = 0; i < original.Length; i++) withGloves[i] = baseMaterials[i] = original[i];
                withGloves[^1] = GloveMaterial(baseMaterials[0], gear);
                if (withGloves[^1] == null) continue;
                renderer.sharedMesh = geometry.Gloves;
                renderer.sharedMaterials = withGloves;
                attached.Add(new Attached(renderer, geometry, baseMaterials));
            }
        }

        internal bool Visible => attached.Count > 0;
    }

    private static bool HandBone(string name) =>
        name.StartsWith("hand_", StringComparison.Ordinal) || name.StartsWith("thumb_", StringComparison.Ordinal) ||
        name.StartsWith("index_", StringComparison.Ordinal) || name.StartsWith("middle_", StringComparison.Ordinal) ||
        name.StartsWith("ring_", StringComparison.Ordinal) || name.StartsWith("little_", StringComparison.Ordinal);

    private static Geometry GetGeometry(SkinnedMeshRenderer renderer)
    {
        var source = renderer.sharedMesh;
        if (source == null || !source.isReadable) return null;
        int id = source.GetInstanceID();
        if (meshes.TryGetValue(id, out var cached)) return cached;
        var bones = renderer.bones;
        var isHand = new bool[bones.Length];
        for (int i = 0; i < isHand.Length; i++) isHand[i] = bones[i] != null && HandBone(bones[i].name);
        var weights = source.boneWeights;
        float[] coverage = new float[weights.Length];
        float Weight(int bone, float value) => bone >= 0 && bone < isHand.Length && isHand[bone] ? value : 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            var w = weights[i];
            coverage[i] = Weight(w.boneIndex0, w.weight0) + Weight(w.boneIndex1, w.weight1) +
                Weight(w.boneIndex2, w.weight2) + Weight(w.boneIndex3, w.weight3);
        }
        if (coverage.Length != source.vertexCount) return meshes[id] = null;
        var hands = new List<int>();
        var skin = new List<int>[source.subMeshCount];
        for (int sub = 0; sub < skin.Length; sub++)
        {
            skin[sub] = new List<int>();
            var triangles = source.GetTriangles(sub);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var list = coverage[a] + coverage[b] + coverage[c] > 1.55f ? hands : skin[sub];
                list.Add(a); list.Add(b); list.Add(c);
            }
        }
        if (hands.Count == 0) return meshes[id] = null;
        var mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = source.name + " · fitted winter gloves";
        mesh.subMeshCount = skin.Length + 1;
        for (int sub = 0; sub < skin.Length; sub++) mesh.SetTriangles(skin[sub].ToArray(), sub, false);
        mesh.SetTriangles(hands.ToArray(), skin.Length, true);
        var withoutHands = UnityEngine.Object.Instantiate(mesh);
        withoutHands.subMeshCount = skin.Length;
        return meshes[id] = new Geometry(source, mesh, withoutHands, skin.Length);
    }

    private static Material GloveMaterial(Material basis, string gear)
    {
        if (basis == null) return null;
        string family = gear.Contains("Rabbit", StringComparison.OrdinalIgnoreCase) ? "fur" :
            gear.Contains("Ski", StringComparison.OrdinalIgnoreCase) ? "winter" :
            gear.Contains("Driving", StringComparison.OrdinalIgnoreCase) || gear.Contains("Work", StringComparison.OrdinalIgnoreCase) ? "leather" :
            gear.Contains("Gauntlet", StringComparison.OrdinalIgnoreCase) ? "gauntlet" : "wool";
        var key = (basis.GetInstanceID(), family);
        if (materials.TryGetValue(key, out var material)) return material;
        Color color = family switch
        {
            "fur" => new Color(.48f, .36f, .24f),
            "winter" => new Color(.20f, .31f, .40f),
            "leather" => new Color(.39f, .25f, .14f),
            "gauntlet" => new Color(.48f, .32f, .18f),
            _ => new Color(.24f, .23f, .20f)
        };
        material = new Material(basis) { name = "Co-op " + family + " glove" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .24f);
        return materials[key] = material;
    }
}
