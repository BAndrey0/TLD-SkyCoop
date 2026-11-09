using System;
using System.IO;
using Il2CppTLD.AddressableAssets;
using UnityEngine;

namespace SkyCoop;

internal static class WardrobeProbe
{
    internal static void Run(string output)
    {
        using var report = new StreamWriter(output);
        void Dump(GameObject root)
        {
            if (root == null) { report.WriteLine("missing"); return; }
            report.WriteLine(root.name + " root=" + root.transform.localScale);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var skin = renderer.TryCast<SkinnedMeshRenderer>();
                var mesh = skin?.sharedMesh ?? renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var t = renderer.transform;
                report.WriteLine($"{renderer.name}: {mesh.name} {mesh.vertexCount} bones={skin?.bones.Length} size={mesh.bounds.size:F3} center={mesh.bounds.center:F3} scale={t.lossyScale:F3} pos={t.position:F3} rot={t.eulerAngles:F0} read={mesh.isReadable}");
                foreach (var m in renderer.sharedMaterials) report.WriteLine(" mat " + m?.name);
                if (skin != null && root.name.Contains("SkiGloves"))
                    foreach (var bone in skin.bones) report.WriteLine(" bone " + bone?.name + " " + bone?.position);
            }
        }
        foreach (var name in new[] { "GEAR_DownParka", "GEAR_MilitaryParka", "GEAR_BasicWinterCoat", "GEAR_BearSkinCoat",
            "GEAR_Jeans", "GEAR_InsulatedPants", "GEAR_SkiGloves", "GEAR_BasicWoolHat", "GEAR_InsulatedBoots",
            "GEAR_RunningShoes", "GEAR_WoolScarf" })
        {
            report.WriteLine(name);
            var gear = MyMod.GetGearItemPrefab(name);
            report.WriteLine(gear?.m_ClothingItem?.m_PaperDollTextureName);
            Dump(gear?.gameObject);
            var gloves = gear?.m_ClothingItem?.m_FirstPersonPrefabMale;
            if (gloves != null && gloves.RuntimeKeyIsValid())
                Dump(AssetHelper.SafeLoadAssetAsync<GameObject>(gloves.RuntimeKey.ToString(), true).WaitForCompletion());
        }
        foreach (string asset in new[] { "CLTH_HMM_TOR_Down_Parka_INSP", "CLTH_HMM_TOR_Military_Parka_INSP",
            "CLTH_HMM_LEG_Jeans_INSP", "CLTH_HMF_LEG_Insulated_pants_INSP", "CLTH_HMM_SHO_Insulated_Boots_INSP",
            "CLTH_HMM_HAR_Wool_toque_INSP", "CLTH_HMM_HED_Wool_scarf_INSP" })
        {
            report.WriteLine(asset);
            Dump(AssetHelper.SafeLoadAssetAsync<GameObject>("Assets/ArtAssets/Characters/Clothes/" + asset + ".fbx", true).WaitForCompletion());
        }
        Dump(NativeCharacterLibrary.Load(0));
        Dump(NativeCharacterLibrary.Load(1));
    }
}
