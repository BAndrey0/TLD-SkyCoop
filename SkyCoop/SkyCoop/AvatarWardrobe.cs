using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Fit the game's unfolded item meshes to the existing animated wardrobe.
// The item keeps its original UVs, geometry and materials.
internal sealed class AvatarWardrobe
{
    private enum Part { Hat, Coat, Trousers, Shoes, Scarf }
    private sealed record Garment(Mesh Mesh, Material[] Materials);
    private sealed record Binding(SkinnedMeshRenderer Renderer, Garment Original);
    private static readonly Dictionary<(int, string, Part, bool), Garment> Cache = new();
    private static readonly Dictionary<int, Calibration> Rest = new();
    private readonly List<Binding> bindings = new();

    internal bool Set(GameObject avatar, string hat, string coat, string trousers, string shoes, string scarf)
    {
        foreach (var binding in bindings)
            if (binding.Renderer != null) Assign(binding.Renderer, binding.Original);
        bindings.Clear();

        var clothing = avatar.transform.GetChild(0);
        var headwear = clothing.GetChild(0);
        var scarfRoot = headwear.GetChild(string.IsNullOrEmpty(coat) ? 2 : 3);
        Fit(avatar.transform, headwear, hat, Part.Hat, scarfRoot);
        Fit(avatar.transform, scarfRoot, scarf, Part.Scarf);
        bool fittedCoat = Fit(avatar.transform, clothing.GetChild(1), coat, Part.Coat);
        Fit(avatar.transform, clothing.GetChild(2), trousers, Part.Trousers, underCoat: fittedCoat);
        Fit(avatar.transform, clothing.GetChild(3), shoes, Part.Shoes);
        return fittedCoat;
    }

    private bool Fit(Transform avatar, Transform group, string gear, Part part, Transform skip = null, bool underCoat = false)
    {
        if (string.IsNullOrEmpty(gear) || gear.Length > 96 || !gear.StartsWith("GEAR_", StringComparison.Ordinal)) return false;
        bool fitted = false;
        foreach (var reference in group.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!VisibleBelow(reference.transform, group) || (skip != null && reference.transform.IsChildOf(skip))) continue;
            var source = reference.sharedMesh;
            if (source == null || !source.isReadable) continue;
            // The first torso slot is the bare body. Keep it for the existing sleeve/vest masks.
            if (part == Part.Coat && reference.transform.IsChildOf(group.GetChild(0))) continue;
            var key = (source.GetInstanceID(), gear, part, underCoat);
            if (!Cache.TryGetValue(key, out var garment))
            {
                if (Cache.Count >= 128) continue;
                try { garment = Build(avatar, reference, gear, part, underCoat); }
                catch (Exception ex) { MelonLogger.Warning("[Wardrobe] " + gear + ": " + ex); }
                Cache.Add(key, garment);
                if (garment != null && (Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER") != null ||
                    Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_WORLD_TEST") != null))
                    UnityEngine.Debug.Log("[Wardrobe] " + gear + " " + part + " mesh=" + garment.Mesh.vertexCount + " submeshes=" + garment.Mesh.subMeshCount + " triangles=" + (garment.Mesh.triangles.Length / 3));
            }
            if (garment == null) continue;
            var materials = reference.sharedMaterials;
            var original = new Material[materials.Length];
            for (int i = 0; i < original.Length; i++) original[i] = materials[i];
            bindings.Add(new Binding(reference, new Garment(source, original)));
            Assign(reference, garment);
            fitted = true;
        }
        return fitted;
    }

    private static bool VisibleBelow(Transform node, Transform root)
    {
        for (var current = node; current != root; current = current.parent)
            if (current == null || !current.gameObject.activeSelf) return false;
        return root.gameObject.activeSelf;
    }

    private static void Assign(SkinnedMeshRenderer renderer, Garment garment)
    {
        renderer.sharedMesh = garment.Mesh;
        renderer.sharedMaterials = garment.Materials;
    }

    private sealed record Calibration(Matrix4x4[] Bones, string[] Names, Vector3[] Joints, Vector3[] Points, Bounds Bounds);

    private static Calibration Calibrate(Transform avatar, SkinnedMeshRenderer reference)
    {
        var mesh = reference.sharedMesh;
        int id = mesh.GetInstanceID();
        if (Rest.TryGetValue(id, out var found)) return found;
        var path = new Stack<int>();
        for (var node = reference.transform; node != avatar; node = node.parent)
            path.Push(node.GetSiblingIndex());
        var prefab = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
        if (prefab == null) return null;
        var nodeInPrefab = prefab.transform;
        foreach (int index in path) nodeInPrefab = nodeInPrefab.GetChild(index);
        var skin = nodeInPrefab.GetComponent<SkinnedMeshRenderer>();
        if (skin == null || skin.sharedMesh != mesh) return null;
        var bones = skin.bones;
        var binds = mesh.bindposes;
        if (bones.Length != binds.Length) return null;
        var matrices = new Matrix4x4[binds.Length];
        var names = new string[binds.Length];
        var joints = new Vector3[binds.Length];
        var toBody = prefab.transform.worldToLocalMatrix;
        for (int i = 0; i < matrices.Length; i++)
        {
            if (bones[i] == null) return null;
            matrices[i] = toBody * bones[i].localToWorldMatrix * binds[i];
            names[i] = bones[i].name;
            joints[i] = toBody.MultiplyPoint3x4(bones[i].position);
        }
        var vertices = mesh.vertices;
        var weights = mesh.boneWeights;
        if (vertices.Length == 0 || vertices.Length != weights.Length) return null;
        var points = new Vector3[vertices.Length];
        var bounds = new Bounds(SkinMatrix(matrices, weights[0]).MultiplyPoint3x4(vertices[0]), Vector3.zero);
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = SkinMatrix(matrices, weights[i]).MultiplyPoint3x4(vertices[i]);
            bounds.Encapsulate(points[i]);
        }
        return Rest[id] = new Calibration(matrices, names, joints, points, bounds);
    }

    private static Matrix4x4 SkinMatrix(Matrix4x4[] bones, BoneWeight weight)
    {
        var a = bones[weight.boneIndex0];
        var b = bones[weight.boneIndex1];
        var c = bones[weight.boneIndex2];
        var d = bones[weight.boneIndex3];
        float Blend(float p, float q, float r, float s) =>
            p * weight.weight0 + q * weight.weight1 + r * weight.weight2 + s * weight.weight3;
        return new Matrix4x4
        {
            m00 = Blend(a.m00, b.m00, c.m00, d.m00), m01 = Blend(a.m01, b.m01, c.m01, d.m01),
            m02 = Blend(a.m02, b.m02, c.m02, d.m02), m03 = Blend(a.m03, b.m03, c.m03, d.m03),
            m10 = Blend(a.m10, b.m10, c.m10, d.m10), m11 = Blend(a.m11, b.m11, c.m11, d.m11),
            m12 = Blend(a.m12, b.m12, c.m12, d.m12), m13 = Blend(a.m13, b.m13, c.m13, d.m13),
            m20 = Blend(a.m20, b.m20, c.m20, d.m20), m21 = Blend(a.m21, b.m21, c.m21, d.m21),
            m22 = Blend(a.m22, b.m22, c.m22, d.m22), m23 = Blend(a.m23, b.m23, c.m23, d.m23),
            m33 = 1f
        };
    }

    private static Garment Build(Transform avatar, SkinnedMeshRenderer reference, string gear, Part part, bool underCoat)
    {
        var item = MyMod.GetGearItemPrefab(gear);
        if (item == null || item.m_ClothingItem == null) return null;
        MeshFilter donor = null;
        foreach (var filter in item.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.sharedMesh.isReadable || filter.GetComponent<MeshRenderer>() == null) continue;
            if (filter.name == "MeshInspectMode") { donor = filter; break; }
            if (donor == null) donor = filter;
        }
        if (donor == null || (part is Part.Coat or Part.Trousers && donor.name != "MeshInspectMode")) return null;
        var source = donor.sharedMesh;
        var referenceMesh = reference.sharedMesh;
        var paperDoll = item.m_ClothingItem?.m_PaperDollTextureName;
        var clothing = item.m_ClothingItem;
        if (Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER") != null)
        {
            string display = item.DisplayName;
            UnityEngine.Debug.Log($"[Wardrobe item] {gear} display={display} region={clothing?.m_Region} warmth={clothing?.m_Warmth} waterproof={clothing?.m_Waterproofness}");
            UnityEngine.Debug.Log($"[Wardrobe] {gear} {part} donor mesh={source.vertexCount} tris={source.triangles.Length / 3} reference mesh={referenceMesh.vertexCount} tris={referenceMesh.triangles.Length / 3} paperDoll={paperDoll}");
        }
        // Retain the original skinned version when the bundle already includes this garment.
        if (gear == "GEAR_BearSkinCoat") return null;
        var weights = referenceMesh.boneWeights;
        var calibration = Calibrate(avatar, reference);
        if (calibration == null) return null;
        var referencePoints = calibration.Points;
        var targetBounds = calibration.Bounds;
        var fromItem = item.transform.worldToLocalMatrix * donor.transform.localToWorldMatrix;
        // The dropped wool toque lies flat, with the head opening facing forward.
        if (gear == "GEAR_BasicWoolHat") fromItem = Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f)) * fromItem;
        var input = source.vertices;
        var inputBounds = new Bounds(fromItem.MultiplyVector(input[0]), Vector3.zero);
        var points = new Vector3[input.Length];
        for (int i = 0; i < points.Length; i++)
        {
            // Ignore translations baked into scene pickups, for example the wool toque.
            points[i] = fromItem.MultiplyVector(input[i]);
            inputBounds.Encapsulate(points[i]);
        }
        var size = inputBounds.size;
        if (Mathf.Min(size.x, size.y, size.z) < .008f) return null;
        if (part is Part.Coat or Part.Trousers && size.y < .45f) return null;
        if (part == Part.Shoes && size.y < .035f) return null;

        // Map the item bounds into the bind pose. Use the reference depth to
        // locate the front and back, and keep the thickness of padded items.
        var targetSize = targetBounds.size;
        targetSize.z = Mathf.Clamp(size.z, targetSize.z * .96f, targetSize.z * 1.24f);
        if (part == Part.Coat)
        {
            targetSize.y = Mathf.Clamp(size.y, targetSize.y * .96f, targetSize.y * 1.25f);
            targetSize.z = Mathf.Max(targetSize.z, targetBounds.size.z * 1.12f);
        }
        var center = targetBounds.center;
        if (part == Part.Coat) center.y = targetBounds.max.y - targetSize.y * .5f;
        var scale = new Vector3(targetSize.x / size.x, targetSize.y / size.y, targetSize.z / size.z);
        var outputWeights = new BoneWeight[points.Length];
        var normals = source.normals;
        var outputNormals = new Vector3[normals.Length];
        var sourceToBody = Matrix4x4.Scale(scale) * fromItem;
        var shoeSide = part == Part.Shoes ? ShoeSides(source, points, inputBounds.center.x) : null;
        var sleeveSide = part == Part.Coat ? Sleeves(source, points, inputBounds) : null;
        int leftFoot = Array.IndexOf(calibration.Names, "Foot.L"), rightFoot = Array.IndexOf(calibration.Names, "Foot.R");
        int leftHand = Array.IndexOf(calibration.Names, "hand_l"), rightHand = Array.IndexOf(calibration.Names, "hand_r");
        int head = Array.IndexOf(calibration.Names, "Head"), neck = Array.IndexOf(calibration.Names, "Neck");
        int hips = Array.IndexOf(calibration.Names, "root");
        var cuff = new[] { new Vector3(0, float.PositiveInfinity, 0), new Vector3(0, float.PositiveInfinity, 0) };
        if (part == Part.Coat)
        {
            for (int i = 0; i < points.Length; i++)
            {
                int side = sleeveSide[i];
                if (side >= 0 && points[i].y < cuff[side].y) cuff[side] = points[i];
            }
            var sums = new Vector3[2];
            var counts = new int[2];
            for (int i = 0; i < points.Length; i++)
            {
                int side = sleeveSide[i];
                if (side < 0 || points[i].y - cuff[side].y > size.y * .04f) continue;
                sums[side] += points[i];
                counts[side]++;
            }
            for (int side = 0; side < 2; side++)
                if (counts[side] > 0) cuff[side] = sums[side] / counts[side];
        }
        var referenceIsArm = new bool[weights.Length];
        if (part == Part.Coat)
        {
            bool IsArm(int bone) => calibration.Names[bone].StartsWith("arms_", StringComparison.Ordinal) ||
                calibration.Names[bone].StartsWith("hand_", StringComparison.Ordinal);
            for (int i = 0; i < weights.Length; i++)
            {
                var weight = weights[i];
                referenceIsArm[i] = (IsArm(weight.boneIndex0) ? weight.weight0 : 0f) +
                    (IsArm(weight.boneIndex1) ? weight.weight1 : 0f) +
                    (IsArm(weight.boneIndex2) ? weight.weight2 : 0f) +
                    (IsArm(weight.boneIndex3) ? weight.weight3 : 0f) > .25f;
            }
        }
        // Transform each vertex back to bind space using the same weights
        // Unity will use to bring it forward into the fitting pose.
        for (int i = 0; i < points.Length; i++)
        {
            var fitted = center + Vector3.Scale(points[i] - inputBounds.center, scale);
            float height = (points[i].y - inputBounds.min.y) / size.y;
            bool sleeve = sleeveSide != null && sleeveSide[i] >= 0;
            if (underCoat)
            {
                float taper = Mathf.SmoothStep(0f, 1f, (height - .60f) / .29f);
                fitted.x = center.x + (fitted.x - center.x) * (1f - .20f * taper);
                fitted.z = center.z + (fitted.z - center.z) * (1f - .28f * taper);
            }
            int rigidBone = part switch
            {
                Part.Hat => head,
                Part.Scarf => neck,
                Part.Shoes when leftFoot >= 0 && rightFoot >= 0 =>
                    shoeSide[i] == (calibration.Joints[leftFoot].x < calibration.Joints[rightFoot].x) ? leftFoot : rightFoot,
                Part.Coat when !sleeve && height < .30f => hips,
                _ => -1
            };
            if (sleeve && leftHand >= 0 && rightHand >= 0)
            {
                int side = sleeveSide[i];
                int hand = (side == 0) == (calibration.Joints[leftHand].x < calibration.Joints[rightHand].x) ? leftHand : rightHand;
                if (float.IsFinite(cuff[side].y))
                {
                    var cuffPosition = center + Vector3.Scale(cuff[side] - inputBounds.center, scale);
                    var adjustment = calibration.Joints[hand] - cuffPosition;
                    float taper = Mathf.Clamp01(1f - (points[i].y - cuff[side].y) / (size.y * .43f));
                    fitted.x += Mathf.Clamp(adjustment.x, -.12f, .12f) * taper;
                    fitted.y += Mathf.Clamp(adjustment.y + .035f, -.22f, .10f) * taper;
                    fitted.z += Mathf.Clamp(adjustment.z, -.09f, .09f) * taper;
                }
            }
            if (rigidBone >= 0) outputWeights[i] = new BoneWeight { boneIndex0 = rigidBone, weight0 = 1f };
            else
            {
                int nearest = 0;
                float distance = float.PositiveInfinity;
                for (int j = 0; j < referencePoints.Length; j++)
                {
                    var point = referencePoints[j];
                    float x = point.x - fitted.x, y = point.y - fitted.y, z = point.z - fitted.z;
                    float candidate = x * x + y * y + z * z;
                    // Keep torso vertices from following a nearby sleeve as the
                    // arms swing. Near the shoulders both groups may contribute.
                    if (part == Part.Coat && height < .82f && sleeve != referenceIsArm[j]) candidate += .045f;
                    if (candidate >= distance) continue;
                    distance = candidate;
                    nearest = j;
                }
                outputWeights[i] = weights[nearest];
            }
            var skinMatrix = SkinMatrix(calibration.Bones, outputWeights[i]);
            float determinant = skinMatrix.m00 * (skinMatrix.m11 * skinMatrix.m22 - skinMatrix.m12 * skinMatrix.m21)
                - skinMatrix.m01 * (skinMatrix.m10 * skinMatrix.m22 - skinMatrix.m12 * skinMatrix.m20)
                + skinMatrix.m02 * (skinMatrix.m10 * skinMatrix.m21 - skinMatrix.m11 * skinMatrix.m20);
            if (Mathf.Abs(determinant) < .000000001f) return null;
            var toBind = skinMatrix.inverse;
            points[i] = toBind.MultiplyPoint3x4(fitted);
            if (normals.Length == points.Length)
                outputNormals[i] = (toBind * sourceToBody).inverse.transpose.MultiplyVector(normals[i]).normalized;
        }
        var donorMaterials = donor.GetComponent<MeshRenderer>().sharedMaterials;
        if (Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER") != null)
        {
            AuditTextures(gear + " donor", donorMaterials);
            AuditTextures(gear + " reference", reference.sharedMaterials);
        }
        if (donorMaterials.Length != source.subMeshCount) return null;
        var materials = new Material[donorMaterials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            var donorMat = donorMaterials[i];
            // Prototype: GEAR_DownParka looks flat because the donor uses the custom
            // "Shader Forge/Damage_Blend" shader. Borrow the Standard setup from the
            // reference (classic avatar) material and swap in the item's diffuse map.
            if (gear == "GEAR_DownParka" && donorMat != null && donorMat.shader?.name == "Shader Forge/Damage_Blend" &&
                reference.sharedMaterials != null && i < reference.sharedMaterials.Length && reference.sharedMaterials[i] != null)
            {
                var standard = new Material(reference.sharedMaterials[i]);
                standard.name = donorMat.name + " · Standard";
                if (donorMat.HasProperty("_MainTex")) standard.mainTexture = donorMat.GetTexture("_MainTex");
                if (donorMat.HasProperty("_Color")) standard.color = donorMat.GetColor("_Color");
                materials[i] = standard;
            }
            else materials[i] = donorMat;
        }
        var mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = gear + " · co-op skin";
        mesh.vertices = points;
        mesh.bindposes = referenceMesh.bindposes;
        mesh.boneWeights = outputWeights;
        // Source normals follow its authored hard edges and UV seams.
        if (normals.Length == points.Length) mesh.normals = outputNormals;
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return new Garment(mesh, materials);
    }

    private static void AuditTextures(string label, Material[] materials)
    {
        if (materials == null) return;
        foreach (var mat in materials)
        {
            if (mat == null) continue;
            var sb = new System.Text.StringBuilder();
            sb.Append($"[Wardrobe audit] {label} material={mat.name} shader={mat.shader?.name}");
            string[] props = { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_SpecGlossMap" };
            foreach (var p in props)
            {
                if (!mat.HasProperty(p)) continue;
                var tex = mat.GetTexture(p);
                if (tex != null) sb.Append($" {p}={tex.name}x{tex.width}");
            }
            UnityEngine.Debug.Log(sb.ToString());
        }
    }

    // Join UV seam vertices and assign each connected boot piece to one foot.
    // An inward-facing toe can cross the midpoint between the shoes.
    private static bool[] ShoeSides(Mesh mesh, Vector3[] points, float center)
    {
        var parent = new int[points.Length];
        var weld = new Dictionary<(int, int, int), int>();
        int Root(int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }
        void Join(int a, int b) => parent[Root(a)] = Root(b);
        for (int i = 0; i < points.Length; i++)
        {
            parent[i] = i;
            var p = points[i];
            var key = (Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
            if (weld.TryGetValue(key, out int seam)) Join(i, seam);
            else weld.Add(key, i);
        }
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            var indices = mesh.GetTriangles(sub);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Join(indices[i], indices[i + 1]);
                Join(indices[i], indices[i + 2]);
            }
        }
        var sums = new float[points.Length];
        for (int i = 0; i < points.Length; i++) sums[Root(i)] += points[i].x - center;
        var left = new bool[points.Length];
        for (int i = 0; i < points.Length; i++) left[i] = sums[Root(i)] < 0f;
        return left;
    }

    private static int[] Sleeves(Mesh mesh, Vector3[] points, Bounds bounds)
    {
        const int slices = 36;
        var triangles = new List<int>();
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
            foreach (int index in mesh.GetTriangles(sub)) triangles.Add(index);
        var thresholds = new float[slices, 2];
        var intervals = new List<(float Min, float Max)>();
        float width = bounds.size.x;
        for (int slice = 0; slice < slices; slice++)
        {
            float height = (slice + .5f) / slices;
            float y = bounds.min.y + bounds.size.y * height;
            for (int side = 0; side < 2; side++)
            {
                intervals.Clear();
                float sign = side == 0 ? -1f : 1f;
                for (int i = 0; i + 2 < triangles.Count; i += 3)
                {
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    for (int edge = 0; edge < 3; edge++)
                    {
                        var a = points[triangles[i + edge]];
                        var b = points[triangles[i + (edge + 1) % 3]];
                        if ((a.y > y) == (b.y > y)) continue;
                        float x = (a.x + (b.x - a.x) * ((y - a.y) / (b.y - a.y)) - bounds.center.x) * sign;
                        min = Math.Min(min, x);
                        max = Math.Max(max, x);
                    }
                    if (max > 0f) intervals.Add((Math.Max(0f, min), max));
                }
                intervals.Sort((a, b) => a.Min.CompareTo(b.Min));
                float end = 0f;
                thresholds[slice, side] = height > .64f ? width * .28f : float.PositiveInfinity;
                foreach (var interval in intervals)
                {
                    if (end > width * .12f && interval.Min > end + width * .018f && interval.Min > width * .20f)
                    {
                        thresholds[slice, side] = (end + interval.Min) * .5f;
                        break;
                    }
                    end = Math.Max(end, interval.Max);
                }
            }
        }
        var result = new int[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            float x = points[i].x - bounds.center.x;
            int side = x < 0f ? 0 : 1;
            int slice = Mathf.Clamp((int)((points[i].y - bounds.min.y) / bounds.size.y * slices), 0, slices - 1);
            result[i] = Math.Abs(x) >= thresholds[slice, side] ? side : -1;
        }
        return result;
    }
}
