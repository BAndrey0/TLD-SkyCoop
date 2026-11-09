using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

internal static class BedRest
{
    private static Bed bed;
    private static PlayerManager player;
    private static Transform camera;
    private static Camera weaponCamera;
    private static bool weaponCameraEnabled;
    private static Vector3 surfacePosition;
    private static Quaternion surfaceRotation;
    private static Vector3 cameraPosition;
    private static Quaternion cameraRotation;
    private static string scene;
    private static GameObject body;
    private static RestRig localRig;
    private static readonly Dictionary<int, RestRig> RemoteRigs = new();
    private static bool restClicked;

    internal static bool Active => player != null && bed != null;
    internal const string Animation = "BedRest";
    internal const string Status = "Отдых · Сила восстанавливается\nEsc / пробел — встать";
    internal static void Request() => restClicked = true;
    internal static Vector3 Position => surfacePosition;
    internal static Quaternion Rotation => surfaceRotation;

    private static (Vector3 Position, Quaternion Rotation) Surface(Bed target)
    {
        var anchor = target.m_BodyPlacementTransform;
        if (anchor == null) anchor = target.transform;
        bool portable = target.m_Bedroll != null || target.GetComponent<Comps.FakeBedDummy>() != null;
        var meshRoot = target.m_BedRollPlacedMesh;
        return SurfaceFromMeshes(anchor, meshRoot != null ? meshRoot : target.gameObject,
            portable, portable || target.m_BodyPlacementTransform == null);
    }

    internal static (Vector3 Position, Quaternion Rotation) SurfaceFromMeshes(Transform anchor, GameObject meshRoot, bool portable, bool useMeshHeight)
    {
        Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(anchor.up, Vector3.up).normalized;
        if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
        Vector3 center = anchor.position;
        Vector3 longest = portable ? Vector3.Cross(Vector3.up, forward) : forward;
        float bestArea = 0f;
        foreach (var filter in meshRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            var mesh = filter.sharedMesh.bounds;
            var x = filter.transform.TransformVector(Vector3.right * mesh.size.x);
            var y = filter.transform.TransformVector(Vector3.up * mesh.size.y);
            var z = filter.transform.TransformVector(Vector3.forward * mesh.size.z);
            var a = Vector3.ProjectOnPlane(x, Vector3.up);
            var b = Vector3.ProjectOnPlane(y, Vector3.up);
            var c = Vector3.ProjectOnPlane(z, Vector3.up);
            float area = (Vector3.Cross(a, b).magnitude + Vector3.Cross(a, c).magnitude + Vector3.Cross(b, c).magnitude);
            if (area <= bestArea) continue;
            bestArea = area;
            longest = a.sqrMagnitude >= b.sqrMagnitude && a.sqrMagnitude >= c.sqrMagnitude ? a : b.sqrMagnitude >= c.sqrMagnitude ? b : c;
            var meshCenter = filter.transform.TransformPoint(mesh.center);
            center = new Vector3(meshCenter.x, anchor.position.y, meshCenter.z);
            if (useMeshHeight)
                center.y = meshCenter.y + (Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y)) * .5f;
        }
        var axis = longest.normalized;
        var preferred = Mathf.Abs(Vector3.Dot(axis, forward)) > .25f ? forward : Vector3.ProjectOnPlane(anchor.right, Vector3.up);
        if (Vector3.Dot(axis, preferred) < 0f) axis = -axis;
        return (center, Quaternion.LookRotation(axis.sqrMagnitude > .01f ? axis : forward, Vector3.up));
    }

    internal static bool IsOccupied(Bed target)
    {
        if (target == null) return false;
        if (target == bed || target.GetOccupant() != null) return true;
        var rest = GameManager.m_Rest;
        if (rest != null && rest.IsSleeping() && rest.m_Bed == target) return true;
        var anchor = target.m_BodyPlacementTransform;
        Vector3 position = anchor != null ? anchor.position : target.transform.position;
        foreach (var remote in MyMod.playersData)
            if (remote != null && remote.m_LevelGuid == MyMod.level_guid && (remote.m_AnimState == "Sleep" || remote.m_AnimState == Animation) &&
                (remote.m_Position - position).sqrMagnitude < 2.25f) return true;
        return false;
    }

    internal static bool CanBegin(Panel_Rest panel) => panel != null && panel.isActiveAndEnabled &&
        panel.m_Bed != null && CoopFatigue.ShowsStrengthCost && !MyMod.LoadingScreenIsOn && !MyMod.IsDead &&
        !MyMod.SaveExitPending && !SleepRendezvousFix.Waiting && !CheatConsole.IsOpen && !ItemSpawner.IsOpen;

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
        var panel = GameCompat.Panel<Panel_Rest>();
        if (!CanBegin(panel)) return;
        var target = panel.m_Bed;
        if (IsOccupied(target))
        {
            HUDMessage.AddMessage("Кровать занята");
            return;
        }
        var manager = GameManager.GetPlayerManagerComponent();
        var fps = GameManager.GetVpFPSCamera();
        var view = fps?.GetCameraComponent()?.transform;
        if (manager == null || view == null) return;
        (surfacePosition, surfaceRotation) = Surface(target);
        weaponCamera = fps.GetWeaponCamera();
        weaponCameraEnabled = weaponCamera != null && weaponCamera.enabled;
        if (weaponCamera != null) weaponCamera.enabled = false;
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
        body = UnityEngine.Object.Instantiate(prefab, surfacePosition, surfaceRotation);
        foreach (var collider in body.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var source in body.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
        body.SetActive(true);
        FreezeRestAnimation(body);
        var clothing = body.AddComponent<Comps.MultiplayerPlayerClothingManager>();
        clothing.ApplyLocalRest(body);
        clothing.enabled = false;
        localRig = new RestRig(body.transform);
    }

    internal static void FreezeRestAnimation(GameObject avatar)
    {
        var animator = avatar.GetComponent<Animator>();
        if (animator == null) return;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Play("Idle", 0, .2f);
        animator.Update(0f);
        animator.enabled = false;
    }

    private static void End()
    {
        if (weaponCamera != null) weaponCamera.enabled = weaponCameraEnabled;
        weaponCamera = null;
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
        localRig = null;
        if (bed != null && bed.GetComponent<Comps.FakeBedDummy>() != null) UnityEngine.Object.Destroy(bed.gameObject);
        player = null;
        camera = null;
        bed = null;
        scene = null;
    }

    [HarmonyPatch(typeof(vp_FPSCamera), nameof(vp_FPSCamera.LateUpdate))]
    private static class RestCamera
    {
        private static void Postfix(vp_FPSCamera __instance)
        {
            if (camera != null && __instance.GetCameraComponent()?.transform == camera) UpdateCamera();
        }
    }

    private static void UpdateCamera()
    {
        if (camera == null || !Active) return;
        if (weaponCamera != null && weaponCamera.enabled) weaponCamera.enabled = false;
        localRig?.Align(surfacePosition, surfaceRotation);
        var view = FirstPerson(localRig, surfacePosition, surfaceRotation);
        camera.SetPositionAndRotation(view.Position, view.Rotation);
    }

    internal static (Vector3 Position, Quaternion Rotation) FirstPerson(RestRig rig, Vector3 position, Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        var eyes = position + Vector3.up * .57f - forward * 1.02f;
        if (rig?.Head != null)
        {
            var headOffset = rig.Head.position - position;
            float along = Vector3.Dot(headOffset, forward);
            if (along >= -1f && along <= -.5f && Mathf.Abs(Vector3.Dot(headOffset, rotation * Vector3.right)) < .2f)
                eyes = rig.Head.position + Vector3.up * .30f - forward * .03f;
        }
        return (eyes, Quaternion.LookRotation(forward - Vector3.up * .12f, Vector3.up));
    }

    internal static void LateUpdate()
    {
        UpdateCamera();
        if (!MyMod.InOnline() || MyMod.LoadingScreenIsOn) { RemoteRigs.Clear(); return; }
        for (int i = 0; i < MyMod.playersData.Count && i < MyMod.players.Count; i++)
        {
            var state = MyMod.playersData[i];
            var actor = MyMod.players[i];
            if (state == null || state.m_LevelGuid != MyMod.level_guid || state.m_AnimState != Animation ||
                actor == null || !actor.activeInHierarchy || (!MyMod.iAmHost && i == GameServer.ClientUser.myId))
            { RemoteRigs.Remove(i); continue; }
            if (!RemoteRigs.TryGetValue(i, out var rig) || rig.Root != actor.transform)
                RemoteRigs[i] = rig = new RestRig(actor.transform);
            rig.Align(state.m_Position, state.m_Rotation);
        }
    }

    internal sealed class RestRig
    {
        internal readonly Transform Root;
        internal readonly Transform Head;
        private readonly Transform hips;
        private readonly Transform[] upperArms = new Transform[2];
        private readonly Transform[] forearms = new Transform[2];
        private readonly Transform[] hands = new Transform[2];
        private readonly Transform[] thighs = new Transform[2];
        private readonly Transform[] shins = new Transform[2];
        private readonly Transform[] feetBones = new Transform[2];

        internal RestRig(Transform root)
        {
            Root = root;
            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var bone in skin.bones)
                    if (bone != null) bones.TryAdd(bone.name, bone);
            Transform Bone(string name) => bones.TryGetValue(NativeCostumeRig.Map(name) ?? name, out var bone) ? bone : null;
            Head = Bone("Head_jnt");
            hips = Bone("Hips_jnt");
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "Left" : "Right";
                upperArms[i] = Bone(side + "UpArm_jnt");
                forearms[i] = Bone(side + "Elbow_jnt");
                hands[i] = Bone(side + "Hand_jnt");
                thighs[i] = Bone(side + "Hip_jnt");
                shins[i] = Bone(side + "Knee_jnt");
                feetBones[i] = Bone(side + "Foot_jnt");
            }
        }

        internal void Align(Vector3 position, Quaternion rotation)
        {
            if (Root == null) return;
            var feet = rotation * Vector3.forward;
            Root.SetPositionAndRotation(position + feet * .83f + Vector3.up * .22f,
                Quaternion.LookRotation(Vector3.up, -feet));
            if (hips == null) return;
            var across = rotation * Vector3.right;
            for (int side = 0; side < 2; side++)
            {
                Vector3 outward = (side == 0 ? -1f : 1f) * across;
                if (upperArms[side] != null && forearms[side] != null && hands[side] != null)
                {
                    Aim(upperArms[side], forearms[side], hips.position - feet * .10f + outward * .27f);
                    Aim(forearms[side], hands[side], hips.position - feet * .12f + outward * .08f + Vector3.up * .17f);
                }
                if (thighs[side] != null && shins[side] != null && feetBones[side] != null)
                {
                    Aim(thighs[side], shins[side], hips.position + feet * .42f + outward * .12f + Vector3.up * .05f);
                    Aim(shins[side], feetBones[side], hips.position + feet * .85f + outward * .13f);
                }
            }
        }

        private static void Aim(Transform joint, Transform next, Vector3 target)
        {
            Vector3 from = next.position - joint.position;
            Vector3 to = target - joint.position;
            if (from.sqrMagnitude > .001f && to.sqrMagnitude > .001f)
                joint.rotation = Quaternion.FromToRotation(from, to) * joint.rotation;
        }
    }
}
