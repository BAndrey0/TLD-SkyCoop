using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

internal static class BedRest
{
    private static Bed bed;
    private static PlayerManager player;
    private static Transform camera;
    private static Vector3 cameraPosition;
    private static Quaternion cameraRotation;
    private static string scene;

    internal static bool Active => player != null && bed != null;
    internal static Transform BodyPose => Active ? bed.m_BodyPlacementTransform : null;

    internal static bool IsOccupied(Bed target)
    {
        if (target == null) return false;
        if (target == bed || target.GetOccupant() != null) return true;
        var rest = GameManager.m_Rest;
        if (rest != null && rest.IsSleeping() && rest.m_Bed == target) return true;
        if (target.m_BodyPlacementTransform == null) return false;
        Vector3 position = target.m_BodyPlacementTransform.position;
        foreach (var remote in MyMod.playersData)
            if (remote != null && remote.m_LevelGuid == MyMod.level_guid && remote.m_AnimState == "Sleep" &&
                (remote.m_Position - position).sqrMagnitude < 2.25f) return true;
        return false;
    }

    private static Panel_Rest AvailablePanel()
    {
        if (!CoopFatigue.ShowsStrengthCost || MyMod.LoadingScreenIsOn || MyMod.IsDead ||
            MyMod.SaveExitPending || SleepRendezvousFix.Waiting || CheatConsole.IsOpen || ItemSpawner.IsOpen) return null;
        var panel = GameCompat.Panel<Panel_Rest>();
        return panel != null && panel.isActiveAndEnabled && panel.m_Bed != null &&
            panel.m_Bed.m_BodyPlacementTransform != null ? panel : null;
    }

    internal static void Tick()
    {
        if (player != null)
        {
            if (!Active || !MyMod.InOnline() || MyMod.LoadingScreenIsOn || MyMod.IsDead ||
                MyMod.SaveExitPending || scene != MyMod.level_guid || player.GetControlMode() != PlayerControlMode.Locked ||
                (GameManager.GetPlayerStruggleComponent()?.InStruggle() ?? false) ||
                KeyboardUtilities.InputManager.GetKeyDown(KeyCode.Escape) ||
                KeyboardUtilities.InputManager.GetKeyDown(KeyCode.Space)) End();
            return;
        }
        if (!KeyboardUtilities.InputManager.GetKeyDown(KeyCode.R)) return;
        var panel = AvailablePanel();
        if (panel == null) return;
        var target = panel.m_Bed;
        if (IsOccupied(target))
        {
            HUDMessage.AddMessage("Кровать занята");
            return;
        }
        var manager = GameManager.GetPlayerManagerComponent();
        var view = GameManager.GetVpFPSCamera()?.transform;
        if (manager == null || view == null) return;
        panel.Enable(false);
        camera = view;
        cameraPosition = view.localPosition;
        cameraRotation = view.localRotation;
        bed = target;
        player = manager;
        scene = MyMod.level_guid;
        player.SetControlMode(PlayerControlMode.Locked);
    }

    private static void End()
    {
        if (camera != null)
        {
            camera.localPosition = cameraPosition;
            camera.localRotation = cameraRotation;
        }
        if (player != null && player.GetControlMode() == PlayerControlMode.Locked &&
            !(GameManager.GetPlayerStruggleComponent()?.InStruggle() ?? false))
            player.ReturnToNormalControlMode(PlayerControlMode.Locked);
        player = null;
        camera = null;
        bed = null;
        scene = null;
    }

    internal static void LateUpdate()
    {
        var pose = BodyPose;
        if (camera == null || pose == null) return;
        var forward = Vector3.ProjectOnPlane(pose.forward, Vector3.up);
        if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
        camera.SetPositionAndRotation(pose.position + Vector3.up * .32f, Quaternion.LookRotation(Vector3.up, forward));
    }

    internal static void Draw()
    {
        if (!Active && AvailablePanel() == null) return;
        var box = new Rect((Screen.width - 360f) * .5f, Screen.height - 84f, 360f, 33f);
        GUI.Box(box, Active ? "Восстанавливается Сила · Esc / пробел — встать" : "R — лечь и восстановить Силу");
    }
}
