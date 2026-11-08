using Il2Cpp;
using HarmonyLib;
using UnityEngine;

namespace SkyCoop;

internal static class DroppedGearInteraction
{
    private static int actionFrame = -1;
    private static float pendingUntil;
    private static int pendingInstance;

    internal static Comps.DroppedGearDummy Find(GameObject obj) =>
        obj != null ? obj.GetComponentInParent<Comps.DroppedGearDummy>() : null;

    private static bool Begin(Comps.DroppedGearDummy dummy)
    {
        if (dummy == null || !MyMod.InOnline() || MyMod.LoadingScreenIsOn || MyMod.SaveExitPending ||
            actionFrame == Time.frameCount) return false;
        int instance = dummy.GetInstanceID();
        if (pendingInstance == instance && Time.realtimeSinceStartup < pendingUntil) return false;
        actionFrame = Time.frameCount;
        pendingInstance = instance;
        pendingUntil = Time.realtimeSinceStartup + 2f;
        return true;
    }

    internal static bool PickUp(Comps.DroppedGearDummy dummy)
    {
        if (!Begin(dummy)) return false;
        if (dummy.GetComponent<Comps.FakeBed>() != null) MyMod.UseFakeBed(dummy.gameObject);
        else MyMod.PickupDroppedGear(dummy.gameObject);
        return true;
    }

    internal static bool Place(Comps.DroppedGearDummy dummy)
    {
        if (!Begin(dummy)) return false;
        MyMod.PlaceDroppedGear(dummy.gameObject);
        return true;
    }

    internal static void Tick()
    {
        if (!MyMod.InOnline() || MyMod.LoadingScreenIsOn || MyMod.DedicatedServerAppMode ||
            InterfaceManager.IsOverlayActiveImmediate() || CheatConsole.IsOpen || ItemSpawner.IsOpen ||
            MyMod.ChatIsOpen() || !KeyboardUtilities.InputManager.GetKeyDown(KeyCode.Mouse1)) return;
        var player = GameManager.GetPlayerManagerComponent();
        if (player == null || player.GetControlMode() != PlayerControlMode.Normal || player.IsInPlacementMode()) return;
        var dummy = Find(player.GetInteractiveObjectUnderCrosshair());
        if (dummy != null) Place(dummy);
    }

    [HarmonyPatch(typeof(PlayerManager), "InteractiveObjectsProcessAltFire")]
    private static class PlaceReplica
    {
        private static bool Prefix(PlayerManager __instance, ref bool __result)
        {
            var dummy = Find(__instance.GetInteractiveObjectUnderCrosshair());
            if (dummy == null) return true;
            Place(dummy);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerManager), "GetInteractiveObjectDisplayTextState")]
    private static class ReplicaHoverState
    {
        private static void Postfix(PlayerManager __instance, ref HoverTextState __result)
        {
            var target = __instance.GetInteractiveObjectUnderCrosshair();
            if (Find(target) != null || ScenePlacementSync.CookingHover(target) != null)
                __result = HoverTextState.CanInteract;
        }
    }
}
