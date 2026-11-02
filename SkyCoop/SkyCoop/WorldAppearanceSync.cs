using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

// Do not use Unity's process-wide RNG or a local saved palette index. Cosmetics
// can initialize before the network/world seed arrives, so use scene + position.
internal static class WorldAppearanceSync
{
	private static int Index(Component component, int count)
	{
		Vector3 p=component.transform.position;
		string key=component.gameObject.scene.name+"|"+p.x.ToString("F2",CultureInfo.InvariantCulture)+","+p.y.ToString("F2",CultureInfo.InvariantCulture)+","+p.z.ToString("F2",CultureInfo.InvariantCulture)+"|"+component.gameObject.name;
		return (int)((uint)Shared.StableHash(key) % (uint)count);
	}
	[HarmonyPatch(typeof(TintMaterials),"UpdateTint")]
	private static class Tint
	{
		private static void Prefix(TintMaterials __instance)
		{
			if(__instance.m_Tints!=null&&__instance.m_Tints.Length>0)__instance.m_IndexToUse=Index(__instance,__instance.m_Tints.Length);
		}
	}
	[HarmonyPatch(typeof(TintMaterials),"Deserialize")]
	private static class RestoreTint
	{
		private static void Postfix(TintMaterials __instance)
		{ if(__instance.m_Tints!=null&&__instance.m_Tints.Length>0)__instance.UpdateTint(); }
	}
	[HarmonyPatch(typeof(ReplaceMaterial),"ChooseRandomMaterial")]
	private static class MaterialChoice
	{
		private static void Prefix(ReplaceMaterial __instance, ref bool useRandom)
		{
			if(__instance.newMaterial==null||__instance.newMaterial.Count==0)return;
			__instance.newMatIndex=Index(__instance,__instance.newMaterial.Count);useRandom=false;
		}
	}
	[HarmonyPatch(typeof(ReplaceMaterial),"Deserialize")]
	private static class RestoreMaterial
	{
		private static void Postfix(ReplaceMaterial __instance)
		{
			if(__instance.newMaterial==null||__instance.newMaterial.Count==0||__instance.sourceMaterial==null)return;
			__instance.newMatIndex=Index(__instance,__instance.newMaterial.Count);
			__instance.ScanForRenderers();
			__instance.ReplaceMaterials(__instance.sourceMaterial,__instance.newMatIndex);
		}
	}
}
