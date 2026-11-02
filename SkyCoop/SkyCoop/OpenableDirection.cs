using HarmonyLib;
using Il2Cpp;

namespace SkyCoop;

// A single authored direction is shared by both peers. ReverseAnimLocator is
// viewer-dependent and was reevaluated from the receiving player's position.
internal static class OpenableDirection
{
	[HarmonyPatch(typeof(OpenClose), "PlayAnimInReverse")]
	private static class Reverse
	{
		private static bool Prefix(ref bool __result)
		{
			if (!MyMod.InOnline()) return true;
			__result = false; return false;
		}
	}
	private static bool Prepare(OpenClose door, bool immediate, bool open)
	{
		if (!MyMod.InOnline()) return true;
		if (!immediate && (door.m_Animating || door.IsOpen() == open)) return false;
		door.m_ReverseAnimLocator = null;
		door.m_AnimsReversed = false;
		return true;
	}
	[HarmonyPatch(typeof(OpenClose), "Open", new[] { typeof(bool), typeof(bool) })]
	private static class Opening
	{
		private static bool Prefix(OpenClose __instance, bool isImmediate) => Prepare(__instance,isImmediate,true);
	}
	[HarmonyPatch(typeof(OpenClose), "Close", new[] { typeof(bool), typeof(bool) })]
	private static class Closing
	{
		private static bool Prefix(OpenClose __instance, bool isImmediate) => Prepare(__instance,isImmediate,false);
	}
}
