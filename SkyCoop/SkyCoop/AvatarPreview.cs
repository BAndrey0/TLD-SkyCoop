using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;
using UnityEngine;

namespace SkyCoop;

internal static class AvatarPreview
{
    private static bool enabled;
    private static bool firstPersonBody;
    private static bool front;
    private static bool cameraApplied;
    private static Vector3 lastEye;
    internal static bool CameraApplied => cameraApplied;
    internal static Vector3 LastEye => lastEye;
    internal static Vector3 LastViewport { get; private set; }
    internal static Vector3 LastRelativeEye { get; private set; }
    internal static GameObject Model => avatar;
    internal static bool GlovesVisible => wardrobe != null && wardrobe.GlovesVisible;
    internal static void InvalidateWardrobe() => choice = int.MinValue;
    private static Camera camera;
    private static Camera weaponCamera;
    private static bool originalWeaponVisibility;
    private static GameObject avatar;
    private static GameObject rightHand;
    private static GameObject leftHand;
    private static Comps.MultiplayerPlayerClothingManager wardrobe;
    private static string held;
    private static string animation;
    private static bool lit;
    private static int choice = int.MinValue;
    private static float nextClothingRefresh;
    private static Il2CppStructArray<RaycastHit> collisionHits;
    private static readonly Dictionary<int, (Renderer Renderer, bool Hidden)> hiddenViewmodels = new();
    private static float nextViewmodelScan;
    private static readonly Dictionary<int, (Renderer Renderer, bool Hidden)> bodyVisibility = new();
    private static bool bodyFilterDirty;

    internal static bool Active => enabled;
    internal static bool FirstPersonBody => firstPersonBody;
    internal static bool ShowsDoll => enabled || firstPersonBody;
    internal static string Status { get; private set; } = "";

    private static bool CanStart() => GearCatalogIndex.InWorld && !MyMod.IsDead && !BedRest.Active && !MyMod.IsFlyModeActive();
    internal static void Toggle()
    {
        if (!enabled && !CanStart())
        {
            Status = "Камера доступна в игре, когда персонаж на ногах";
            return;
        }
        if (!enabled)
        {
            if (firstPersonBody) Disable();
            Shared.EnsureLocalDoll();
            if (MyMod.MyPlayerDoll == null || GameManager.GetVpFPSCamera()?.GetCameraComponent() == null)
            {
                Status = "Модель или камера ещё загружается";
                return;
            }
        }
        enabled = !enabled;
        front = false;
        if (!enabled) Disable();
        Status = enabled ? "Камера от третьего лица" : "Камера от первого лица";
        HUDMessage.AddMessage(Status);
    }

    internal static void ToggleBody()
    {
        if (firstPersonBody)
        {
            Disable();
            Status = "Тело от первого лица скрыто";
            return;
        }
        if (!CanStart()) return;
        Disable();
        firstPersonBody = true;
        bodyFilterDirty = true;
        Status = "Тело от первого лица включено";
    }

    internal static void Turn() => SetSide(!front);
    internal static void Front() => SetSide(true);
    internal static void Back() => SetSide(false);

    private static void SetSide(bool showFront)
    {
        if (!enabled) Toggle();
        if (!enabled) return;
        front = showFront;
        cameraApplied = false;
        Status = front ? "Камера спереди" : "Камера сзади";
    }

    internal static void RestoreCamera()
    {
        if (camera != null) camera.ResetWorldToCameraMatrix();
        camera = null;
        if (weaponCamera != null) weaponCamera.enabled = originalWeaponVisibility;
        weaponCamera = null;
    }

    private static void Disable()
    {
        enabled = false;
        firstPersonBody = false;
        cameraApplied = false;
        RestoreCamera();
        RestoreViewmodels();
        RestoreBodyVisibility();
        if (avatar != null && !MyMod.IsFlyModeActive()) avatar.SetActive(false);
    }

    private static void RestoreViewmodels()
    {
        foreach (var saved in hiddenViewmodels.Values)
            if (saved.Renderer != null) saved.Renderer.forceRenderingOff = saved.Hidden;
        hiddenViewmodels.Clear();
        nextViewmodelScan = 0f;
    }

    private static void HideViewmodels(vp_FPSCamera fps)
    {
        if (Time.realtimeSinceStartup < nextViewmodelScan) return;
        nextViewmodelScan = Time.realtimeSinceStartup + .5f;
        var root = GameObject.Find("/CHARACTER_FPSPlayer")?.transform;
        HideRenderers(root?.Find("NEW_FPHand_Rig"));
        HideRenderers(root?.Find("WeaponView"));
        HideRenderers(fps.m_WeaponParent);
    }

    private static void HideRenderers(Transform root)
    {
        if (root == null) return;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            int id = renderer.GetInstanceID();
            if (!hiddenViewmodels.ContainsKey(id)) hiddenViewmodels[id] = (renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
    }

    internal static void LateUpdate()
    {
        if (!ShowsDoll) return;
        if (MyMod.LoadingScreenIsOn || MyMod.IsDead || BedRest.Active || MyMod.IsFlyModeActive() || Shared.IsMainMenuScene(MyMod.level_name))
        {
            Disable();
            return;
        }
        var player = GameManager.GetPlayerManagerComponent();
        Shared.EnsureLocalDoll();
        var doll = MyMod.MyPlayerDoll;
        if (doll == null || player == null || GameManager.m_PlayerObject == null) return;
        if (player.GetControlMode() == PlayerControlMode.Dead || player.IsInspectModeActive() ||
            (firstPersonBody && (player.PlayerIsClimbing() || GameManager.GetPlayerInVehicle()?.m_InVehicle == true)))
        {
            doll.SetActive(false);
            RestoreViewmodels();
            return;
        }
        if (avatar != doll)
        {
            avatar = doll;
            wardrobe = doll.AddComponent<Comps.MultiplayerPlayerClothingManager>();
            wardrobe.enabled = false;
            (rightHand, leftHand) = Comps.MultiplayerPlayer.HandSockets(doll);
            foreach (var collider in doll.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            held = null;
            choice = int.MinValue;
            bodyFilterDirty = true;
            var animator = doll.GetComponent<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }
        doll.SetActive(true);
        var body = GameManager.GetPlayerTransform();
        PlaceAvatar(body);
        if (choice != NativeAppearance.Choice || Time.realtimeSinceStartup >= nextClothingRefresh)
        {
            MyMod.UpdateMyClothing(publish: false);
            choice = NativeAppearance.Choice;
            nextClothingRefresh = Time.realtimeSinceStartup + 1f;
            if (wardrobe.ApplyLocalAppearance(doll, choice, (int)PlayerManager.m_VoicePersona))
            {
                RestoreBodyVisibility();
                bodyFilterDirty = true;
            }
        }
        if (firstPersonBody && bodyFilterDirty)
        {
            FilterFirstPersonBody();
            bodyFilterDirty = false;
        }
        string item = player.m_ItemInHands != null ? player.m_ItemInHands.GetGearName() : MyMod.MyLightSourceName ?? "";
        if (item != held || MyMod.MyLightSource != lit || MyMod.MyAnimState != animation)
        {
            held = item;
            lit = MyMod.MyLightSource;
            animation = MyMod.MyAnimState;
            string food = animation == "Drinking" ? "Water" : animation == "Eating" ? "Food" : "";
            Comps.MultiplayerPlayer.ShowHeldItem(rightHand, leftHand, firstPersonBody ? "" : held,
                firstPersonBody ? "" : food, !firstPersonBody && lit, animation);
            leftHand.transform.GetChild(6).gameObject.SetActive(!firstPersonBody && held == "Book" && animation == "Idle");
            leftHand.transform.GetChild(7).gameObject.SetActive(!firstPersonBody && held == "Book" && animation != "Idle");
        }
    }

    private static void PlaceAvatar(Transform body)
    {
        var forward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
        if (forward.sqrMagnitude <= .001f) return;
        var position = body.position;
        // The native camera sits near the front of the head. Leave enough room
        // to see the outside of the clothes when looking down.
        if (firstPersonBody) position -= forward.normalized * .24f;
        avatar.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
    }

    private static void FilterFirstPersonBody()
    {
        var wardrobeRoot = avatar.transform.GetChild(0);
        var trousers = wardrobeRoot.GetChild(2);
        var boots = wardrobeRoot.GetChild(3);
        foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(trousers) || renderer.transform.IsChildOf(boots)) continue;
            bodyVisibility[renderer.GetInstanceID()] = (renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
    }

    private static void RestoreBodyVisibility()
    {
        foreach (var entry in bodyVisibility.Values)
            if (entry.Renderer != null) entry.Renderer.forceRenderingOff = entry.Hidden;
        bodyVisibility.Clear();
    }

    [HarmonyPatch(typeof(vp_FPSCamera), nameof(vp_FPSCamera.LateUpdate))]
    private static class ApplyAfterNativeCamera
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(vp_FPSCamera __instance)
        {
            if (enabled && __instance == GameManager.GetVpFPSCamera()) ApplyCamera(__instance);
        }
    }

    internal static void ApplyCamera(vp_FPSCamera fps)
    {
        if (avatar == null || !avatar.activeInHierarchy || !enabled || BedRest.Active || MyMod.LoadingScreenIsOn) return;
        var rendered = fps?.GetCameraComponent();
        var body = GameManager.GetPlayerTransform();
        if (rendered == null || body == null) return;
        HideViewmodels(fps);
        PlaceAvatar(body);
        if (camera != null) RestoreCamera();
        camera = rendered;
        weaponCamera = fps.GetWeaponCamera();
        originalWeaponVisibility = weaponCamera != null && weaponCamera.enabled;
        if (weaponCamera != null) weaponCamera.enabled = false;

        var motor = GameManager.GetVpFPSPlayer()?.Controller;
        float stanceHeight = motor?.m_Controller != null && motor.NormalHeight > .1f
            ? Mathf.Clamp(motor.m_Controller.height / motor.NormalHeight, .45f, 1.1f) * 1.55f
            : 1.55f;
        Vector3 pivot = body.position + Vector3.up * stanceHeight;
        Vector3 facing = avatar.transform.forward;
        Vector3 offset = front ? facing * 2.65f + Vector3.up * .15f :
            -facing * 2.8f + avatar.transform.right * .48f + Vector3.up * .22f;
        var clipped = ClipOffset(body, pivot, offset);
        if (front && clipped.sqrMagnitude < .64f)
        {
            var left = ClipOffset(body, pivot, offset - avatar.transform.right * 1.2f);
            var right = ClipOffset(body, pivot, offset + avatar.transform.right * 1.2f);
            if (left.sqrMagnitude > clipped.sqrMagnitude) clipped = left;
            if (right.sqrMagnitude > clipped.sqrMagnitude) clipped = right;
        }
        var eye = pivot + clipped;
        float pitch = float.IsFinite(fps.m_TargetPitch) ? Mathf.Clamp(fps.m_TargetPitch, -55f, 55f) : 0f;
        float lookHeight = -Mathf.Tan(pitch * Mathf.Deg2Rad) * 1.15f;
        var target = front ? pivot - Vector3.up * .50f : pivot + facing * 2.2f + Vector3.up * (lookHeight - .55f);
        var rotation = Quaternion.LookRotation(target - eye, Vector3.up);
        rendered.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(eye, rotation, Vector3.one).inverse;
        lastEye = eye;
        LastViewport = rendered.WorldToViewportPoint(target);
        LastRelativeEye = Quaternion.Inverse(avatar.transform.rotation) * (eye - body.position);
        cameraApplied = true;
    }

    private static Vector3 ClipOffset(Transform player, Vector3 origin, Vector3 offset)
    {
        var direction = offset.normalized;
        float distance = offset.magnitude;
        collisionHits ??= new Il2CppStructArray<RaycastHit>(16);
        int count = Physics.SphereCastNonAlloc(origin, .14f, direction, collisionHits, distance,
            Utils.m_PhysicalCollisionLayerMask, QueryTriggerInteraction.Ignore);
        var controller = GameManager.GetVpFPSPlayer()?.transform;
        var playerObject = GameManager.m_PlayerObject?.transform;
        for (int i = 0; i < count; i++)
        {
            var hit = collisionHits[i];
            var part = hit.collider != null ? hit.collider.transform : null;
            if (part == null || part.IsChildOf(player) || player.IsChildOf(part) || (controller != null && part.IsChildOf(controller)) ||
                (playerObject != null && part.IsChildOf(playerObject)) ||
                (avatar != null && part.IsChildOf(avatar.transform))) continue;
            distance = Mathf.Min(distance, Mathf.Max(.08f, hit.distance - .09f));
        }
        return direction * distance;
    }
}
