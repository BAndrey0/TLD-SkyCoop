using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Co-op work and fatigue rules (see kit/README-RU.md). In co-op the world clock belongs to the host, so actions
// (crafting, reading, harvesting, breaking down, repairs, fishing) no longer move time for everyone; what they cost
// instead is Strength, a 0-100 pool for work, and extra fatigue for heavy work. Sleep needs real tiredness.
//
// Every timed action goes through TimeOfDay.Accelerate / AccelerateTime with its game-time length, which gives one
// place to price it. Strength drains while the action runs; at zero the action is cancelled. Crafting, reading and
// fishing keep their progress (the game resumes crafting from an in-progress item), so a recipe that costs more
// than one full pool is built over several sittings. Other actions need their whole cost up front.
public static class CoopFatigue
{
	private enum Weight
	{
		Light,
		Medium,
		Heavy
	}

	[HarmonyPatch(typeof(TimeOfDay), "Accelerate")]
	private static class OnAccelerate
	{
		private static void Prefix(float realTimeSeconds, float gameTimeHours)
		{
			ActionStarted(gameTimeHours, realTimeSeconds);
		}
	}

	[HarmonyPatch(typeof(TimeOfDay), "AccelerateTime")]
	private static class OnAccelerateTime
	{
		private static void Prefix(float minutes, float realtimeDurationSeconds)
		{
			ActionStarted(minutes / 60f, realtimeDurationSeconds);
		}
	}

	public const float MaxStrength = 100f;

	private const float RegenFullSeconds = 1200f;

	private const float RestRegenMultiplier = 3f;

	private const float RegenDelaySeconds = 2f;

	public const float SleepThreshold = 0.6f;

	private static MelonPreferences_Entry<bool> enabledPref;

	private static float strength = MaxStrength;

	private static bool actionActive;

	private static float actionStarted;

	private static float actionEnds;

	private static float strengthPerSecond;

	private static float fatiguePerSecond;

	private static MelonPreferences_Entry<bool> actionFatiguePref;

	private static float fatigueAtActionStart;

	private static bool ActionFatigue => actionFatiguePref != null && actionFatiguePref.Value;

	private static float savedWalking = -1f;

	private static float savedSprintMin;

	private static float savedSprintMax;

	private static bool actionAllowsPartial;

	private static string actionName = "";

	private static float lastActionEnd = -100f;

	private static bool cancelPending;

	private static string cancelMessage = "";

	private static Vector3 lastPosition;

	private static float lastTick;

	private static int fatigueWarning;

	public static float Strength => strength;

	public static bool Active
	{
		get
		{
			if (enabledPref == null)
			{
				MelonPreferences_Category category = MelonPreferences.CreateCategory("SkyCoop");
				enabledPref = category.CreateEntry("CoopFatigue", default_value: true, "Co-op work and fatigue rules (Strength, sleep threshold)");
				actionFatiguePref = category.CreateEntry("CoopActionFatigue", default_value: false, "Work also tires (fatigue for the action's game hours, heavier work more). Off: fatigue only grows with time awake");
			}
			if (!enabledPref.Value || !MyMod.InOnline() || MyMod.IsDead)
			{
				return false;
			}
			if (string.IsNullOrEmpty(MyMod.level_name) || MyMod.level_name == "Empty" || MyMod.level_name == "Boot")
			{
				return false;
			}
			return !Shared.IsMainMenuScene(MyMod.level_name);
		}
	}

	private static float TiredFraction
	{
		get
		{
			Fatigue fatigue = GameManager.GetFatigueComponent();
			if (fatigue == null || fatigue.m_MaxFatigue <= 0f)
			{
				return 0f;
			}
			return Mathf.Clamp01(fatigue.m_CurrentFatigue / fatigue.m_MaxFatigue);
		}
	}

	private static bool Sleeping
	{
		get
		{
			if (GameManager.m_Rest != null && GameManager.m_Rest.IsSleeping())
			{
				return true;
			}
			Panel_Rest panel = GameCompat.Panel<Panel_Rest>();
			return panel != null && panel.isActiveAndEnabled;
		}
	}

	private static void ActionStarted(float hours, float realSeconds)
	{
		try
		{
			if (!Active || hours <= 0f || Sleeping)
			{
				return;
			}
			float now = Time.realtimeSinceStartup;
			// AccelerateTime may call Accelerate for the same action; price it once.
			if (actionActive && now - actionStarted < 0.5f)
			{
				return;
			}
			Weight weight = DetectWeight(out var name, out var partial);
			float rate = (weight == Weight.Heavy) ? 16f : ((weight == Weight.Medium) ? 10f : 6f);
			float fatigueMultiplier = (weight == Weight.Heavy) ? 1.5f : ((weight == Weight.Medium) ? 1.25f : 1f);
			float cost = hours * rate;
			if (GameManager.GetFatigueComponent() != null && GameManager.GetFatigueComponent().IsExhausted())
			{
				Block("Вы слишком устали для работы — нужен сон");
				return;
			}
			if (strength <= 0.5f)
			{
				Block("Нет сил на работу — передохните (присядьте)");
				return;
			}
			if (!partial && strength < Mathf.Min(cost, MaxStrength))
			{
				Block("Не хватает сил: нужно " + Mathf.CeilToInt(Mathf.Min(cost, MaxStrength)) + ", есть " + Mathf.FloorToInt(strength));
				return;
			}
			Fatigue fatigue = GameManager.GetFatigueComponent();
			float standingPerHour = (fatigue != null) ? fatigue.m_FatigueIncreasePerHourStanding : 0f;
			realSeconds = Mathf.Max(realSeconds, 0.5f);
			actionActive = true;
			actionStarted = now;
			fatigueAtActionStart = (fatigue != null) ? fatigue.m_CurrentFatigue : 0f;
			actionEnds = now + realSeconds;
			actionAllowsPartial = partial;
			actionName = name;
			strengthPerSecond = cost / realSeconds;
			fatiguePerSecond = ActionFatigue ? ((fatigueMultiplier - 1f) * standingPerHour * hours / realSeconds) : 0f;
			MelonLogger.Msg("[Fatigue] " + name + " " + hours.ToString("0.##") + " h, cost " + cost.ToString("0") + ", strength " + strength.ToString("0") + (partial ? " (resumable)" : ""));
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Fatigue] " + ex.Message);
		}
	}

	private static Weight DetectWeight(out string name, out bool partial)
	{
		partial = false;
		Panel_Crafting crafting = GameCompat.Panel<Panel_Crafting>();
		if (crafting != null && crafting.isActiveAndEnabled)
		{
			name = "crafting";
			partial = true;
			return Weight.Medium;
		}
		Panel_BreakDown breakDown = GameCompat.Panel<Panel_BreakDown>();
		if (breakDown != null && breakDown.isActiveAndEnabled)
		{
			name = "breakdown";
			return Weight.Heavy;
		}
		Panel_SnowShelterBuild shelter = GameCompat.Panel<Panel_SnowShelterBuild>();
		if (shelter != null && shelter.isActiveAndEnabled)
		{
			name = "snow shelter";
			return Weight.Heavy;
		}
		Panel_BodyHarvest harvest = GameCompat.Panel<Panel_BodyHarvest>();
		if (harvest != null && harvest.isActiveAndEnabled)
		{
			if (harvest.IsQuartering())
			{
				name = "quartering";
				return Weight.Heavy;
			}
			name = "carcass harvest";
			return Weight.Medium;
		}
		Panel_IceFishing fishing = GameCompat.Panel<Panel_IceFishing>();
		if (fishing != null && fishing.isActiveAndEnabled)
		{
			name = "ice fishing";
			partial = true;
			return Weight.Light;
		}
		Panel_Inventory_Examine examine = GameCompat.Panel<Panel_Inventory_Examine>();
		if (examine != null && examine.isActiveAndEnabled)
		{
			if (examine.IsReading())
			{
				name = "reading";
				partial = true;
				return Weight.Light;
			}
			if (examine.IsRepairing())
			{
				GearItem item = examine.m_GearItem;
				bool clothing = item != null && item.m_ClothingItem != null;
				name = clothing ? "clothing repair" : "tool repair";
				return clothing ? Weight.Light : Weight.Medium;
			}
			name = "item work";
			return Weight.Medium;
		}
		name = "action";
		return Weight.Light;
	}

	private static void Block(string message)
	{
		cancelPending = true;
		cancelMessage = message;
		MelonLogger.Msg("[Fatigue] Blocked: " + message);
	}

	private static void CancelCurrentAction()
	{
		try
		{
			Panel_Crafting crafting = GameCompat.Panel<Panel_Crafting>();
			if (crafting != null && crafting.isActiveAndEnabled)
			{
				crafting.OnCancelCrafting();
				return;
			}
			Panel_BreakDown breakDown = GameCompat.Panel<Panel_BreakDown>();
			if (breakDown != null && breakDown.isActiveAndEnabled)
			{
				breakDown.OnCancel();
				return;
			}
			Panel_SnowShelterBuild shelter = GameCompat.Panel<Panel_SnowShelterBuild>();
			if (shelter != null && shelter.isActiveAndEnabled)
			{
				shelter.OnCancel();
				return;
			}
			Panel_BodyHarvest harvest = GameCompat.Panel<Panel_BodyHarvest>();
			if (harvest != null && harvest.isActiveAndEnabled)
			{
				harvest.OnCancel();
				return;
			}
			Panel_IceFishing fishing = GameCompat.Panel<Panel_IceFishing>();
			if (fishing != null && fishing.isActiveAndEnabled)
			{
				fishing.OnCancel();
				return;
			}
			Panel_Inventory_Examine examine = GameCompat.Panel<Panel_Inventory_Examine>();
			if (examine != null && examine.isActiveAndEnabled)
			{
				examine.OnProgressBarCancel();
				return;
			}
			Panel_GenericProgressBar progress = GameCompat.Panel<Panel_GenericProgressBar>();
			if (progress != null && progress.isActiveAndEnabled)
			{
				progress.Cancel();
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Fatigue] Cancelling action: " + ex.Message);
		}
	}

	// Sleep threshold, checked before a sleep starts (Panel_Rest.DoRest and the group sleep wait). Ending a sleep
	// that already began left the screen faded to black, and a patch on Panel_Rest.OnFullRest recursed (0.15.0/0.15.1).
	public static bool SleepAllowed(bool explain)
	{
		if (!Active)
		{
			return true;
		}
		float tired = TiredFraction;
		Fatigue fatigue = GameManager.GetFatigueComponent();
		if (tired >= SleepThreshold || (fatigue != null && fatigue.IsExhausted()))
		{
			return true;
		}
		if (explain)
		{
			Hud("Вы ещё не устали настолько, чтобы уснуть (" + Mathf.RoundToInt(tired * 100f) + "% из " + Mathf.RoundToInt(SleepThreshold * 100f) + "%)");
		}
		return false;
	}

	public static void Tick()
	{
		float now = Time.realtimeSinceStartup;
		float dt = Mathf.Clamp(now - lastTick, 0f, 0.5f);
		lastTick = now;
		if (!Active)
		{
			actionActive = false;
			cancelPending = false;
			RestoreMovementFatigue();
			return;
		}
		FlattenMovementFatigue();
		if (cancelPending)
		{
			cancelPending = false;
			actionActive = false;
			lastActionEnd = now;
			CancelCurrentAction();
			Hud(cancelMessage);
			return;
		}
		if (actionActive)
		{
			TimeOfDay timeOfDay = GameManager.GetTimeOfDayComponent();
			bool lapse = timeOfDay != null && timeOfDay.IsTimeLapseActive();
			if ((!lapse && now - actionStarted > 0.5f) || now > actionEnds + 2f)
			{
				actionActive = false;
				lastActionEnd = now;
			}
			else
			{
				strength = Mathf.Max(0f, strength - strengthPerSecond * dt);
				Fatigue workFatigue = GameManager.GetFatigueComponent();
				if (workFatigue != null)
				{
					if (fatiguePerSecond > 0f)
					{
						workFatigue.AddFatigue(fatiguePerSecond * dt);
					}
					else if (!ActionFatigue)
					{
						// The game charges fatigue for the action's skipped hours; here fatigue only follows real time awake.
						float allowed = fatigueAtActionStart + workFatigue.m_FatigueIncreasePerHourStanding * (now - actionStarted) / 300f;
						if (workFatigue.m_CurrentFatigue > allowed)
						{
							workFatigue.m_CurrentFatigue = allowed;
						}
					}
				}
				if (strength <= 0f)
				{
					actionActive = false;
					lastActionEnd = now;
					CancelCurrentAction();
					Hud(actionAllowsPartial ? "Силы кончились — работа прервана, прогресс сохранён" : "Силы кончились — работа прервана");
					MelonLogger.Msg("[Fatigue] Out of strength during " + actionName);
				}
				return;
			}
		}
		Transform player = GameManager.GetPlayerTransform();
		float speed = 0f;
		if (player != null)
		{
			speed = (dt > 0f) ? (Vector3.Distance(player.position, lastPosition) / dt) : 0f;
			lastPosition = player.position;
		}
		if (now - lastActionEnd >= RegenDelaySeconds && speed < 4.5f && strength < MaxStrength)
		{
			float multiplier = 1f;
			PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
			if (playerManager != null && playerManager.PlayerIsCrouched() && speed < 0.3f)
			{
				multiplier = RestRegenMultiplier;
			}
			strength = Mathf.Min(MaxStrength, strength + MaxStrength / RegenFullSeconds * multiplier * dt);
		}
		WarnFatigue();
	}

	private static void WarnFatigue()
	{
		float tired = TiredFraction;
		if (tired < 0.8f)
		{
			fatigueWarning = 0;
		}
		else if (tired >= 0.95f && fatigueWarning < 2)
		{
			fatigueWarning = 2;
			Hud("Вы на пределе — скоро изнеможение, пора спать");
		}
		else if (tired >= 0.85f && fatigueWarning < 1)
		{
			fatigueWarning = 1;
			Hud("Вы сильно устали — ищите место для сна");
		}
	}

	// Fatigue works as the time-awake clock in co-op: walking and sprinting tire no faster than standing.
	private static void FlattenMovementFatigue()
	{
		Fatigue fatigue = GameManager.GetFatigueComponent();
		if (fatigue == null)
		{
			return;
		}
		if (savedWalking < 0f)
		{
			savedWalking = fatigue.m_FatigueIncreasePerHourWalking;
			savedSprintMin = fatigue.m_FatigueIncreasePerHourSprintingMin;
			savedSprintMax = fatigue.m_FatigueIncreasePerHourSprintingMax;
		}
		float standing = fatigue.m_FatigueIncreasePerHourStanding;
		fatigue.m_FatigueIncreasePerHourWalking = standing;
		fatigue.m_FatigueIncreasePerHourSprintingMin = standing;
		fatigue.m_FatigueIncreasePerHourSprintingMax = standing;
	}

	private static void RestoreMovementFatigue()
	{
		if (savedWalking < 0f)
		{
			return;
		}
		Fatigue fatigue = GameManager.GetFatigueComponent();
		if (fatigue != null)
		{
			fatigue.m_FatigueIncreasePerHourWalking = savedWalking;
			fatigue.m_FatigueIncreasePerHourSprintingMin = savedSprintMin;
			fatigue.m_FatigueIncreasePerHourSprintingMax = savedSprintMax;
		}
		savedWalking = -1f;
	}

	public static void ResetForRespawn()
	{
		strength = MaxStrength;
		actionActive = false;
		cancelPending = false;
	}

	private static float barAlpha;

	private static GUIStyle labelStyle;

	// A thin bar in the style of the game's own meters (dark track, pale fill, small caps label), centred above the
	// sprint bar so it does not cover the health widget. It fades in while Strength is spent and out when full.
	public static void Draw()
	{
		bool wanted = Active && (strength < MaxStrength - 0.01f || actionActive);
		if (Event.current != null && Event.current.type == EventType.Repaint)
		{
			barAlpha = Mathf.MoveTowards(barAlpha, wanted ? 1f : 0f, Time.unscaledDeltaTime * 2f);
		}
		if (!Active)
		{
			StrengthBar.HideNow();
			return;
		}
		StrengthBar.Update(Mathf.Clamp01(strength / MaxStrength), barAlpha);
		if (barAlpha <= 0.01f || StrengthBar.Available)
		{
			return;
		}
		float scale = Screen.height / 1080f;
		float width = 300f * scale;
		float height = Mathf.Max(3f, 5f * scale);
		float x = (Screen.width - width) / 2f;
		float y = Screen.height * 0.865f;
		if (labelStyle == null)
		{
			labelStyle = new GUIStyle(GUI.skin.label);
			labelStyle.alignment = TextAnchor.LowerCenter;
			labelStyle.fontStyle = FontStyle.Bold;
		}
		labelStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(15f * scale));
		Color old = GUI.color;
		float fill = Mathf.Clamp01(strength / MaxStrength);
		Color fillColor = (fill > 0.25f) ? new Color(0.88f, 0.85f, 0.76f) : new Color(0.85f, 0.32f, 0.25f);
		GUI.color = new Color(0f, 0f, 0f, 0.55f * barAlpha);
		GUI.DrawTexture(new Rect(x - 1f, y - 1f, width + 2f, height + 2f), Texture2D.whiteTexture);
		GUI.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0.9f * barAlpha);
		GUI.DrawTexture(new Rect(x, y, width * fill, height), Texture2D.whiteTexture);
		Rect label = new Rect(x, y - 24f * scale, width, 22f * scale);
		GUI.color = new Color(0f, 0f, 0f, 0.6f * barAlpha);
		GUI.Label(new Rect(label.x + 1f, label.y + 1f, label.width, label.height), "СИЛА", labelStyle);
		GUI.color = new Color(0.88f, 0.85f, 0.76f, 0.95f * barAlpha);
		GUI.Label(label, "СИЛА", labelStyle);
		GUI.color = old;
	}

	private static string lastHud = "";

	private static float lastHudTime = -100f;

	private static void Hud(string text)
	{
		// The same message from repeated clicks or per-frame checks is shown once every few seconds.
		if (text == lastHud && Time.realtimeSinceStartup - lastHudTime < 4f)
		{
			return;
		}
		lastHud = text;
		lastHudTime = Time.realtimeSinceStartup;
		try
		{
			HUDMessage.AddMessage(text);
		}
		catch
		{
		}
		MelonLogger.Msg("[Fatigue] " + text);
	}
}
