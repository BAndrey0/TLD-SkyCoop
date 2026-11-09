using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Layer Survival clothing items on top of a full WINTERMUTE story costume.
// The costume's own clothing submeshes are hidden when a matching item is worn.
internal sealed class CostumeWardrobe : IDisposable
{
    private readonly GameObject costume;
    private readonly bool female;
    private readonly SkinnedMeshRenderer bodyRenderer;
    private readonly Mesh bodyMesh;
    private readonly Dictionary<string, int[]> originalTriangles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> materialSlots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Material> originalMaterials = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> attachments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Mesh> attachmentMeshes = new(StringComparer.Ordinal);

    private DataStr.PlayerClothingData current;

    internal CostumeWardrobe(GameObject costume, bool female)
    {
        this.costume = costume;
        this.female = female;
        bodyRenderer = FindBodyRenderer(costume);
        if (bodyRenderer != null && bodyRenderer.sharedMesh != null)
        {
            bodyMesh = UnityEngine.Object.Instantiate(bodyRenderer.sharedMesh);
            bodyMesh.name = bodyRenderer.sharedMesh.name + " · co-op clone";
            bodyRenderer.sharedMesh = bodyMesh;
            CacheSubmeshes();
        }
    }

    internal string BodyName => bodyRenderer != null && bodyRenderer.sharedMesh != null ? bodyRenderer.sharedMesh.name : null;

    private static SkinnedMeshRenderer FindBodyRenderer(GameObject root)
    {
        SkinnedMeshRenderer best = null;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) continue;
            if (best == null || renderer.sharedMesh.vertexCount > best.sharedMesh.vertexCount)
                best = renderer;
        }
        return best;
    }

    public void Dispose()
    {
        foreach (var obj in attachments.Values)
            if (obj != null) UnityEngine.Object.Destroy(obj);
        attachments.Clear();
        foreach (var mesh in attachmentMeshes.Values)
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
        attachmentMeshes.Clear();
        if (bodyMesh != null) UnityEngine.Object.Destroy(bodyMesh);
    }

    internal void Apply(DataStr.PlayerClothingData gear)
    {
        current = gear;
        if (bodyRenderer == null || bodyMesh == null) return;

        string jacketMaterial = female ? "mat_Astrid_Ep5_Jacket" : "mat_Mackenzie_Ep5_Jacket";
        if (!female && gear.m_Top == "GEAR_DownParka")
        {
            // Prototype: keep the original skinned WINTERMUTE jacket submesh and swap
            // its material to the Survival item's material. This preserves correct
            // deformation during walk/run/crouch, unlike the rigid inspect mesh.
            AttachSkinnedTop(gear.m_Top, jacketMaterial);
        }
        else
        {
            SetSlotVisible(jacketMaterial, gear.m_Top == "");
            RestoreJacket(jacketMaterial);
            AttachTop(gear.m_Top, female);
        }

        if (female)
        {
            // Astrid's story sweater/jeans/boots/gloves are the visible base layers;
            // hiding their submeshes removes limbs, so only the outer jacket is hidden.
            SetSlotVisible("mat_Astrid_Ep5_Toque", gear.m_Hat == "");
            SetSlotVisible("mat_Astrid_Ep5_Hair", gear.m_Hat == "");
        }
        else
        {
            SetSlotVisible("mat_Mackenzie_Ep5_Hair", gear.m_Hat == "");
        }

        AttachHat(gear.m_Hat, female);
        AttachScarf(gear.m_Scarf, female);
        // Boot submeshes include ankles; keep story boots. Glove inspect meshes are
        // authored as flat inspect plates and do not follow hand orientation cleanly;
        // keep the story gloves from the full WINTERMUTE costume.
        // AttachBoots(gear.m_Boots, female);
        // AttachGloves(gear.m_Gloves, female);
    }

    private void CacheSubmeshes()
    {
        var materials = bodyRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length && i < bodyMesh.subMeshCount; i++)
        {
            string name = materials[i]?.name ?? "";
            if (!string.IsNullOrEmpty(name) && !materialSlots.ContainsKey(name))
            {
                materialSlots[name] = i;
                originalTriangles[name] = bodyMesh.GetTriangles(i);
                originalMaterials[name] = materials[i];
            }
        }
    }

    private void SetSlotVisible(string materialName, bool visible)
    {
        if (!materialSlots.TryGetValue(materialName, out int slot)) return;
        if (visible)
            bodyMesh.SetTriangles(originalTriangles[materialName], slot, false);
        else
            bodyMesh.SetTriangles(Array.Empty<int>(), slot, false);
    }

    private void AttachHat(string gear, bool female)
    {
        ReplaceAttachment("hat", gear, FindBone("Head"), female, new Vector3(0, 0.02f, 0));
    }

    private void AttachScarf(string gear, bool female)
    {
        ReplaceAttachment("scarf", gear, FindBone("Neck"), female, Vector3.zero);
    }

    private void AttachTop(string gear, bool female)
    {
        // Attach the coat mesh to the mid chest; the inspect mesh is centred at the
        // torso origin, so pull it down to the hips and forward onto the chest.
        var chest = FindBone("Spine2") ?? FindBone("Spine2_jnt") ?? FindBone("Spine3") ?? FindBone("Spine3_jnt");
        var offset = female ? new Vector3(0, -0.22f, 0.14f) : new Vector3(0, -0.20f, 0.13f);
        ReplaceAttachment("top", gear, chest, female, offset, female ? 0.86f : 0.94f);
    }

    private void AttachSkinnedTop(string gear, string jacketMaterial)
    {
        if (!materialSlots.TryGetValue(jacketMaterial, out int slot)) return;
        var prefab = MyMod.GetGearItemPrefab(gear);
        if (prefab == null) return;
        MeshRenderer donorRenderer = null;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
            if (filter.name == "MeshInspectMode") { donorRenderer = filter.GetComponent<MeshRenderer>(); break; }
            if (donorRenderer == null) donorRenderer = filter.GetComponent<MeshRenderer>();
        }
        if (donorRenderer == null || donorRenderer.sharedMaterials.Length == 0) return;

        var materials = bodyRenderer.sharedMaterials;
        if (materials[slot] != donorRenderer.sharedMaterials[0])
        {
            materials[slot] = donorRenderer.sharedMaterials[0];
            bodyRenderer.sharedMaterials = materials;
        }
        SetSlotVisible(jacketMaterial, true);
        // Remove the rigid coat attachment so it does not overlap the skinned submesh.
        if (attachments.TryGetValue("top", out var old) && old != null)
        {
            UnityEngine.Object.Destroy(old);
            attachments.Remove("top");
        }
        if (Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER") != null)
            MelonLogger.Msg("[CostumeWardrobe] skinned top " + gear + " material=" + donorRenderer.sharedMaterials[0].name);
    }

    private void RestoreJacket(string jacketMaterial)
    {
        if (!materialSlots.TryGetValue(jacketMaterial, out int slot)) return;
        if (!originalMaterials.TryGetValue(jacketMaterial, out var original)) return;
        var materials = bodyRenderer.sharedMaterials;
        if (materials[slot] != original)
        {
            materials[slot] = original;
            bodyRenderer.sharedMaterials = materials;
        }
    }

    private void AttachBoots(string gear, bool female)
    {
        ReplaceAttachment("bootL", gear, FindBone("LeftFoot") ?? FindBone("Foot.L") ?? FindBone("LeftFoot_jnt"), female, Vector3.zero);
        ReplaceAttachment("bootR", gear, FindBone("RightFoot") ?? FindBone("Foot.R") ?? FindBone("RightFoot_jnt"), female, Vector3.zero);
    }

    private void AttachGloves(string gear, bool female)
    {
        // The inspect glove mesh is authored along X; rotate to follow the fingers (Z).
        ReplaceAttachment("gloveL", gear, FindBone("LeftHand") ?? FindBone("hand_l") ?? FindBone("LeftHand_jnt"), female, Vector3.zero, explicitRotation: Quaternion.Euler(0, 90, 0));
        ReplaceAttachment("gloveR", gear, FindBone("RightHand") ?? FindBone("hand_r") ?? FindBone("RightHand_jnt"), female, Vector3.zero, explicitRotation: Quaternion.Euler(0, 90, 0));
    }

    private Transform FindBone(string name)
    {
        Transform best = null;
        foreach (var transform in costume.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == name) return transform;
            if (best == null && (transform.name == name + "_jnt" || transform.name == name + "_joint")) best = transform;
        }
        return best;
    }

    private void ReplaceAttachment(string key, string gear, Transform bone, bool female, Vector3 offset, float? explicitScale = null, Quaternion? explicitRotation = null)
    {
        if (attachments.TryGetValue(key, out var old) && old != null)
        {
            UnityEngine.Object.Destroy(old);
            attachments.Remove(key);
        }
        if (attachmentMeshes.TryGetValue(key, out var oldMesh) && oldMesh != null)
        {
            UnityEngine.Object.Destroy(oldMesh);
            attachmentMeshes.Remove(key);
        }
        if (string.IsNullOrEmpty(gear) || bone == null) return;
        if (!gear.StartsWith("GEAR_", StringComparison.Ordinal)) return;
        var prefab = MyMod.GetGearItemPrefab(gear);
        if (prefab == null) return;

        MeshFilter donor = null;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.sharedMesh.isReadable || filter.GetComponent<MeshRenderer>() == null) continue;
            if (filter.name == "MeshInspectMode") { donor = filter; break; }
            if (donor == null) donor = filter;
        }
        if (donor == null) return;

        var donorTransform = donor.transform;
        var obj = new GameObject("TLDCoop_" + key + "_" + gear);
        obj.transform.SetParent(bone, false);
        obj.transform.localPosition = donorTransform.localPosition + offset;
        obj.transform.localRotation = explicitRotation ?? donorTransform.localRotation;
        var filterOut = obj.AddComponent<MeshFilter>();
        var rendererOut = obj.AddComponent<MeshRenderer>();
        var mesh = UnityEngine.Object.Instantiate(donor.sharedMesh);
        mesh.name = gear + " · costume attachment";
        filterOut.sharedMesh = mesh;
        rendererOut.sharedMaterials = donor.GetComponent<MeshRenderer>().sharedMaterials;
        attachmentMeshes[key] = mesh;

        float scale = explicitScale ?? EstimateScale(key, female);
        obj.transform.localScale = Vector3.one * scale;

        attachments[key] = obj;
        if (Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER") != null)
            MelonLogger.Msg("[CostumeWardrobe] " + key + " " + gear + " vertices=" + mesh.vertexCount + " scale=" + scale.ToString("F2"));
    }

    private float EstimateScale(string key, bool female)
    {
        // Gear item meshes are already authored at world scale; only minor gender tweaks.
        return female && key is "hat" or "scarf" ? 0.94f : 1f;
    }
}
