using System;
using System.Collections.Generic;
using Il2CppTLD.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SkyCoop;

internal sealed class AvatarGloveRig : IDisposable
{
    private sealed record Asset(AsyncOperationHandle<GameObject> Handle, SkinnedMeshRenderer Skin);
    private static readonly Dictionary<(string, bool), Asset> Assets = new();
    private readonly List<GameObject> objects = new();
    private AvatarGloveRig() { }

    public void Dispose()
    {
        foreach (var obj in objects)
            if (obj != null) { obj.SetActive(false); UnityEngine.Object.Destroy(obj); }
        objects.Clear();
    }

    internal static AvatarGloveRig Attach(Transform avatar, SkinnedMeshRenderer hands, string gear, bool female)
    {
        if (gear.Length > 96 || !gear.StartsWith("GEAR_", StringComparison.Ordinal)) return null;
        var item = MyMod.GetGearItemPrefab(gear)?.m_ClothingItem;
        if (item == null) return null;
        var reference = female ? item.m_FirstPersonPrefabFemale : item.m_FirstPersonPrefabMale;
        if (reference == null || !reference.RuntimeKeyIsValid()) return null;
        var key = (gear, female);
        if (!Assets.TryGetValue(key, out var asset))
        {
            if (Assets.Count >= 32) return null;
            var handle = AssetHelper.SafeLoadAssetAsync<GameObject>(reference.RuntimeKey.ToString(), true);
            var prefab = handle.WaitForCompletion();
            var skin = prefab == null ? null : prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null || skin.sharedMesh == null)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }
            Assets[key] = asset = new Asset(handle, skin);
        }

        var target = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var bone in hands.bones)
            if (bone != null) target.TryAdd(bone.name, bone);
        var template = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
        if (template == null) return null;
        var rest = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var bone in template.GetComponentsInChildren<Transform>(true))
            if (target.ContainsKey(bone.name)) rest.TryAdd(bone.name, bone);
        var source = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var bone in asset.Skin.bones)
            if (bone != null) source.TryAdd(bone.name, bone);
        var maps = new Dictionary<string, Matrix4x4>();
        foreach (string side in new[] { "Left", "Right" })
        {
            string suffix = side == "Left" ? "_l" : "_r";
            if (!source.TryGetValue(side + "WristJoint", out var wrist) ||
                !source.TryGetValue(side + "_HandMiddle1", out var middle) ||
                !source.TryGetValue(side + "_HandIndex1", out var index) ||
                !source.TryGetValue(side + "_HandPinky1", out var pinky) ||
                !rest.TryGetValue("hand" + suffix, out var dstWrist) ||
                !rest.TryGetValue("middle" + suffix + "_1", out var dstMiddle) ||
                !rest.TryGetValue("index" + suffix + "_1", out var dstIndex) ||
                !rest.TryGetValue("little" + suffix + "_1", out var dstPinky))
                return null;
            var inputDirection = middle.position - wrist.position;
            var outputDirection = dstMiddle.position - dstWrist.position;
            if (inputDirection.sqrMagnitude < .00001f || outputDirection.sqrMagnitude < .00001f) return null;
            var rotation = Quaternion.LookRotation(outputDirection, dstIndex.position - dstPinky.position) *
                Quaternion.Inverse(Quaternion.LookRotation(inputDirection, index.position - pinky.position));
            float scale = Mathf.Clamp(outputDirection.magnitude / inputDirection.magnitude, .7f, 1.3f);
            maps[side] = Matrix4x4.TRS(dstWrist.position, rotation, Vector3.one * scale) *
                Matrix4x4.Translate(-wrist.position);
        }

        var rig = new AvatarGloveRig();
        try
        {
            var model = new GameObject("TLDCoop_" + gear);
            rig.objects.Add(model);
            model.transform.SetParent(avatar, false);
            model.layer = hands.gameObject.layer;
            var renderer = model.AddComponent<SkinnedMeshRenderer>();
            var sourceBones = asset.Skin.bones;
            var fitted = new Transform[sourceBones.Length];
            for (int i = 0; i < fitted.Length; i++)
            {
                var bone = sourceBones[i];
                string name = bone?.name ?? "";
                string side = name.Contains("Left", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Lef_", StringComparison.Ordinal) ? "Left" :
                    name.Contains("Right", StringComparison.OrdinalIgnoreCase) ? "Right" : null;
                if (side == null) { fitted[i] = model.transform; continue; }
                string mapped = FingerBone(name, side);
                if (!target.TryGetValue(mapped, out var joint) || !rest.TryGetValue(mapped, out var jointRest))
                {
                    rig.Dispose();
                    return null;
                }
                var frame = jointRest.worldToLocalMatrix * maps[side] * bone.localToWorldMatrix;
                var proxy = new GameObject("TLDCoop_GloveBone");
                rig.objects.Add(proxy);
                proxy.transform.SetParent(joint, false);
                proxy.transform.localPosition = frame.MultiplyPoint3x4(Vector3.zero);
                proxy.transform.localRotation = frame.rotation;
                proxy.transform.localScale = frame.lossyScale;
                fitted[i] = proxy.transform;
            }
            renderer.bones = fitted;
            renderer.rootBone = hands.rootBone;
            renderer.sharedMesh = asset.Skin.sharedMesh;
            renderer.sharedMaterials = asset.Skin.sharedMaterials;
            renderer.localBounds = hands.localBounds;
            renderer.updateWhenOffscreen = true;
            if (Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_WORLD_TEST") != null)
                MelonLoader.MelonLogger.Msg("[Wardrobe] " + gear + " Gloves mesh=" + renderer.sharedMesh.vertexCount);
            return rig;
        }
        catch
        {
            rig.Dispose();
            throw;
        }
    }

    private static string FingerBone(string name, string side)
    {
        string suffix = side == "Left" ? "_l" : "_r";
        foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
        {
            if (!name.Contains("Hand" + finger, StringComparison.OrdinalIgnoreCase) || name[^1] is < '1' or > '4') continue;
            int segment = name[^1] - '0';
            if (finger == "Thumb") segment--;
            return (finger == "Pinky" ? "little" : finger.ToLowerInvariant()) + suffix + "_" + Math.Clamp(segment, 1, 3);
        }
        if (name.Contains("ForeArm", StringComparison.Ordinal) || name.EndsWith("CuffAdjust", StringComparison.Ordinal))
            return "arms" + suffix + "_02";
        return "hand" + suffix;
    }
}
