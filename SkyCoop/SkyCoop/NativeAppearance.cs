using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using HarmonyLib;

namespace SkyCoop;

// Drive the complete WINTERMUTE story costume from the co-op animation rig.
// The costume keeps its own proportions; inventory clothes are layered on top.
public static class NativeAppearance
{
	private sealed class Visual
	{
		public GameObject root;
		public readonly List<(Renderer Renderer, bool Enabled)> originals = new();
		public readonly Dictionary<Animator, AnimatorCullingMode> animationModes = new();
		public int choice;
		public NativeCostumeRig rig;
		public CostumeWardrobe wardrobe;
	}
	private static readonly Dictionary<int, Visual> Visuals = new();
	private static MelonPreferences_Entry<int> preference;
	private static bool open;
	private static float nextPrune;
	private static int openedFrame;
	private static Panel_SelectSurvivor selectionPanel;
	private static bool selectionFemale, confirmingSelection;

	internal const int MalePreset = 4, FemalePreset = 5, StoryWill = 6, StoryAstrid = 7;
	internal static bool ExperimentsEnabled { get; } =
		Environment.GetEnvironmentVariable("SKYCOOP_MODEL_SWITCHING_TEST") == "1" ||
		!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_WORLD_TEST"));

	internal static int CharacterFor(int choice, int original) => choice switch
	{
		MalePreset or StoryWill => 0,
		FemalePreset or StoryAstrid => 1,
		_ => original
	};
	internal static int Normalize(int choice) => choice is 0 or 2 ? MalePreset : choice is 1 or 3 ? FemalePreset :
		choice == StoryWill ? MalePreset : choice == StoryAstrid ? FemalePreset :
		choice is MalePreset or FemalePreset ? choice : -1;

	public static int Choice => ExperimentsEnabled ? Normalize(preference?.Value ?? -1) : -1;
	public static bool IsOpen => open;
	public static bool HasCostume(GameObject player) => player != null && Visuals.ContainsKey(player.GetInstanceID());

	internal static void Select(int choice, bool persist = true)
	{
		if (preference == null) return;
		preference.Value = Normalize(choice);
		AvatarPreview.InvalidateWardrobe();
		if (MyMod.InOnline() && GearCatalogIndex.InWorld) MyMod.UpdateMyClothing();
		if (persist) MelonPreferences.Save();
	}

	public static void Open()
	{
		if (open || !ExperimentsEnabled) return;
		open = true; openedFrame = Time.frameCount; OverlayInput.Acquire();
	}
	private static void Close()
	{
		if (!open) return; open = false; OverlayInput.Release();
		var panel = selectionPanel; selectionPanel = null;
		if (panel != null && Shared.IsMainMenuScene(MyMod.level_name)) panel.Enable(true);
	}

	public static void Tick()
	{
		if (!ExperimentsEnabled) return;
		preference ??= MelonPreferences.CreateCategory("SkyCoop").CreateEntry("NativeCharacter", -1,
			"Survivor skin: -1 classic, 4 Mackenzie, 5 Astrid");
		if (MyMod.DedicatedServerAppMode) return;
		if (MyMod.LoadingScreenIsOn) Close();
		if (KeyboardUtilities.InputManager.GetTogglePressed(KeyCode.F4)) { if (open) Close(); else if (!CheatConsole.IsOpen && !ItemSpawner.IsOpen) Open(); }
		if (open) { if (Time.frameCount != openedFrame && KeyboardUtilities.InputManager.GetTogglePressed(KeyCode.Escape)) Close(); else OverlayInput.Keep(); }
		if (Time.realtimeSinceStartup >= nextPrune)
		{
			nextPrune = Time.realtimeSinceStartup + 1;
			foreach (int id in new List<int>(Visuals.Keys))
				if (Visuals[id].root == null) { Dispose(Visuals[id]); Visuals.Remove(id); }
		}
	}

	public static void LateUpdate()
	{
		foreach (var visual in Visuals.Values)
			if (visual.root != null && visual.root.activeInHierarchy)
				visual.rig?.Update();
	}

	public static void Draw()
	{
		if (!open) return;
		float height = selectionPanel != null ? 315 : 270;
		float x = (Screen.width - 520) / 2f, y = (Screen.height - height) / 2f;
		GUI.Box(new Rect(x, y, 520, height), "Персонаж · F4 / Esc — закрыть");
		GUI.Label(new Rect(x + 20, y + 35, 480, 45), "Полный костюм WINTERMUTE с динамической экипировкой.");
		int[] options = { -1, MalePreset, FemalePreset };
		string[] labels = { "Классическая модель", "Маккензи", "Астрид" };
		for (int i = 0; i < options.Length; i++)
		{
			int choice = options[i];
			if (!GUI.Button(new Rect(x + 20, y + 90 + i * 40, 480, 34), (Choice == choice ? "✓ " : "") + labels[i])) continue;
			Select(choice);
		}
		if (selectionPanel != null && GUI.Button(new Rect(x + 20, y + 245, 480, 42), "Продолжить"))
		{
			var panel = selectionPanel; bool female = CharacterFor(Choice, selectionFemale ? 1 : 0) == 1;
			Close(); confirmingSelection = true;
			try { if (female) panel.OnSelectSurvivorFemale(); else panel.OnSelectSurvivorMale(); }
			finally { confirmingSelection = false; }
		}
	}

	public static bool ApplyChoice(GameObject player, int choice)
	{
		int id = player.GetInstanceID();
		if (Visuals.TryGetValue(id, out var old))
		{
			if (old.root != null && old.choice == choice)
				return true;
			Dispose(old);
			Visuals.Remove(id);
		}
		int costume = choice switch
		{
			MalePreset or StoryWill => 0,
			FemalePreset or StoryAstrid => 1,
			0 => 0,
			1 => 1,
			_ => -1
		};
		if (costume < 0) return true;
		var prefab = NativeCharacterLibrary.Load(costume);
		UnityEngine.Debug.Log($"[Native appearance] ApplyChoice choice={choice} costume={costume} prefab={prefab?.name}");
		if (prefab == null) return false;
		var visual = new Visual { choice = choice };
		try
		{
			player.transform.localScale = Vector3.one;
			foreach (var animator in player.GetComponentsInChildren<Animator>(true))
				if (animator.enabled && animator.runtimeAnimatorController != null)
				{ visual.animationModes.Add(animator, animator.cullingMode); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
			visual.root = UnityEngine.Object.Instantiate(prefab);
			visual.root.name = "TLDCoop_NativeCostume";
			visual.root.SetActive(false);
			visual.root.transform.SetParent(player.transform, false);
			foreach (var animator in visual.root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
			visual.rig = new NativeCostumeRig(player, visual.root);
			foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
			{
				if (renderer.transform.IsChildOf(visual.root.transform)) continue;
				visual.originals.Add((renderer, renderer.enabled));
				renderer.enabled = false;
			}
			visual.wardrobe = new CostumeWardrobe(visual.root, costume == 1);
			visual.root.SetActive(true);
			Visuals[id] = visual;
			var body = visual.wardrobe.BodyName ?? "none";
			UnityEngine.Debug.Log($"[Native appearance] {NativeCharacterLibrary.Names[costume]} full costume, bones={visual.rig.Count} fit={visual.rig.FitScale:F3} body={body} female={costume == 1}");
			return true;
		}
		catch (Exception ex)
		{
			Dispose(visual);
			MelonLogger.Warning("[Native appearance] Keeping classic model: " + ex.Message);
			return false;
		}
	}

	internal static void ApplyClothing(GameObject player, DataStr.PlayerClothingData gear)
	{
		if (player == null || gear == null) return;
		if (Visuals.TryGetValue(player.GetInstanceID(), out var visual))
			visual.wardrobe?.Apply(gear);
	}

	private static void Dispose(Visual visual)
	{
		if (visual.root != null) visual.root.SetActive(false);
		foreach (var (renderer, enabled) in visual.originals)
			if (renderer != null) renderer.enabled = enabled;
		foreach (var pair in visual.animationModes)
			if (pair.Key != null) pair.Key.cullingMode = pair.Value;
		if (visual.root != null) UnityEngine.Object.Destroy(visual.root);
		visual.wardrobe?.Dispose();
	}
}
