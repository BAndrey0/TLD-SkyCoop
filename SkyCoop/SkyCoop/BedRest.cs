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
    private static GameObject body;
    private static Transform head;
    private static Transform hips;
    private static bool restClicked;

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
        bool clicked = restClicked;
        restClicked = false;
        if (!clicked && !KeyboardUtilities.InputManager.GetKeyDown(KeyCode.R)) return;
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
        var prefab = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
        if (prefab == null) return;
        var pose = target.m_BodyPlacementTransform;
        body = UnityEngine.Object.Instantiate(prefab, pose.position, pose.rotation);
        foreach (var bone in body.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "Head_jnt" || bone.name == "Head") head = bone;
            else if (bone.name == "Hips_jnt" || bone.name == "Hips") hips = bone;
        }
        foreach (var collider in body.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var source in body.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
        var animator = body.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Play("Sleep", 0);
        }
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
        if (body != null) UnityEngine.Object.Destroy(body);
        body = null;
        head = null;
        hips = null;
        if (bed != null && bed.GetComponent<Comps.FakeBedDummy>() != null) UnityEngine.Object.Destroy(bed.gameObject);
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
        var eyes = pose.position + Vector3.up * .34f - forward * .22f;
        if (head != null && hips != null)
        {
            var towardFeet = Vector3.ProjectOnPlane(hips.position - head.position, Vector3.up);
            if (towardFeet.sqrMagnitude > .04f)
            {
                forward = towardFeet.normalized;
                eyes = head.position + Vector3.up * .17f + forward * .06f;
            }
        }
        camera.SetPositionAndRotation(eyes, Quaternion.LookRotation(forward - Vector3.up * .14f, Vector3.up));
    }

    internal static void Draw()
    {
        var box = new Rect((Screen.width - 320f) * .5f, Screen.height * .72f, 320f, 48f);
        if (Active)
        {
            GUI.Box(box, "Личный отдых · Сила восстанавливается\nEsc / пробел — встать");
        }
        else if (AvailablePanel() != null && GUI.Button(box, "Лечь отдохнуть [R]\nВосстановление Силы в реальном времени"))
        {
            restClicked = true;
        }
    }
}
