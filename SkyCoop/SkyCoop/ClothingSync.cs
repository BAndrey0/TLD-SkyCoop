using HarmonyLib;
using Il2Cpp;

namespace SkyCoop;

public static class ClothingSync
{
	[HarmonyPatch(typeof(PlayerManager), "PutOnClothingItem")]
	private static class Worn
	{
		private static void Postfix() { if (MyMod.InOnline()) MyMod.UpdateMyClothing(); }
	}

	[HarmonyPatch(typeof(PlayerManager), "TakeOffClothingItem")]
	private static class Removed
	{
		private static void Postfix() { if (MyMod.InOnline()) MyMod.UpdateMyClothing(); }
	}
}
