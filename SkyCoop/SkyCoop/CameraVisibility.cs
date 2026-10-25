using HarmonyLib;
using Il2Cpp;

namespace SkyCoop;

public static class CameraVisibility
{
	[HarmonyPatch(typeof(FlyMode), "Enter")]
	private static class FlyModeEntered
	{
		private static void Postfix(FlyMode __instance)
		{
			enteredFlyMode = __instance;
		}
	}

	[HarmonyPatch(typeof(FlyMode), "Exit")]
	private static class FlyModeExited
	{
		private static void Postfix(FlyMode __instance)
		{
			if (enteredFlyMode == __instance)
			{
				enteredFlyMode = null;
			}
		}
	}

	[HarmonyPatch(typeof(FlyMode), "TeleportPlayerAndExit")]
	private static class FlyModeTeleportedAndExited
	{
		private static void Postfix(FlyMode __instance)
		{
			if (enteredFlyMode == __instance)
			{
				enteredFlyMode = null;
			}
		}
	}

	private static FlyMode enteredFlyMode;

	public static bool IsFlyModeEntered
	{
		get
		{
			if (enteredFlyMode != null)
			{
				return enteredFlyMode.gameObject.activeInHierarchy;
			}
			return false;
		}
	}
}
