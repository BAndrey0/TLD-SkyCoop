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
		private static void Prefix(TimeOfDay __instance,float realTimeSeconds, float gameTimeHours,ref bool doFadeToBlack)
		{
			WorkLighting.Begin(__instance,realTimeSeconds);
			if(Active&&!Sleeping)doFadeToBlack=false;
			ActionStarted(gameTimeHours, realTimeSeconds);
		}
	}

	[HarmonyPatch(typeof(TimeOfDay), "AccelerateTime")]
	private static class OnAccelerateTime
	{
		private static void Prefix(TimeOfDay __instance,float minutes, float realtimeDurationSeconds)
		{
			WorkLighting.Begin(__instance,realtimeDurationSeconds);
			ActionStarted(minutes / 60f, realtimeDurationSeconds);
		}
	}

	public const float MaxStrength = 100f;

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
	private static float savedStanding;
	private static IntPtr fatiguePointer;
	private static float lastWorkTime;
	private static float previousTired;
	private static bool wasSleeping;
	private static float expectedFatigue = float.NaN;
	private static IntPtr clockOwner;
	private static readonly FatigueTravelLedger Travel = new();
	private static float lastGoodStrength=MaxStrength;
	private static float lastRepairWarning=-100;
	private static float lastSprintMaximum=1;
	internal static void ApplyPassTime(int hours)
	{
		if (hours < 1 || hours > 24 || MyMod.IsDead || MyMod.DedicatedServerAppMode) return;
		var hunger = GameManager.GetHungerComponent();
		var thirst = GameManager.GetThirstComponent();
		var freezing = GameManager.GetFreezingComponent();
		var fatigue = GameManager.GetFatigueComponent();
		if (hunger != null)
			hunger.RemoveReserveCalories(VitalNumbers.Rate(hunger.GetCurrentCalorieBurnPerHour(), hunger.m_CalorieBurnPerHourStanding) * hours);
		if (thirst != null)
			thirst.AddThirst(VitalNumbers.Rate(thirst.m_ThirstIncreasePerDay, 0f) * hours / 24f);
		if (freezing != null)
		{
			float degreesBelowThreshold = freezing.m_TemperatureFreezingStarts - freezing.CalculateBodyTemperature();
			float rate = degreesBelowThreshold > 0 ? freezing.m_FreezingIncreasePerHourPerDegreeCelsius :
				freezing.m_WarmingIncreasePerHourPerDegreeCelsius;
			if (float.IsFinite(degreesBelowThreshold) && float.IsFinite(rate))
				freezing.AddFreezing(degreesBelowThreshold * Mathf.Abs(rate) * hours);
		}
		if (fatigue != null)
		{
			RepairFatigue(fatigue);
			fatigue.CalculateTotalFatigueBurnPerHour();
			float rate = VitalNumbers.Rate(fatigue.m_TotalFatigueBurnPerHour, fatigue.m_MaxFatigue / CoopBalancePolicy.BaseAwakeHours);
			fatigue.AddFatigue(rate * hours);
			expectedFatigue = fatigue.m_CurrentFatigue;
			Travel.Remember(expectedFatigue);
		}
	}

	private static void RepairFatigue(Fatigue fatigue)
	{
		if(fatigue==null||!fatigue.m_StartHasBeenCalled)return;
		if(!float.IsFinite(fatigue.m_MaxFatigue)||fatigue.m_MaxFatigue<=0)fatigue.m_MaxFatigue=100;
		if(!float.IsFinite(fatigue.m_CurrentFatigue))
		{
			fatigue.m_CurrentFatigue=VitalNumbers.Clamp(fatigue.m_CurrentFatigue,Travel.Last??60,fatigue.m_MaxFatigue);
			if(Time.realtimeSinceStartup-lastRepairWarning>10){lastRepairWarning=Time.realtimeSinceStartup;MelonLogger.Warning("[Fatigue] Recovered invalid native value from last finite session value");}
		}
		fatigue.m_FatigueValueLastFrame=VitalNumbers.Clamp(fatigue.m_FatigueValueLastFrame,fatigue.m_CurrentFatigue,fatigue.m_MaxFatigue);
		fatigue.m_TotalFatigueBurnPerHour=VitalNumbers.Rate(fatigue.m_TotalFatigueBurnPerHour,fatigue.m_MaxFatigue/CoopBalancePolicy.BaseAwakeHours);
		fatigue.m_CurrentFatigueBurnPerHour=VitalNumbers.Rate(fatigue.m_CurrentFatigueBurnPerHour,fatigue.m_TotalFatigueBurnPerHour);
		var movement=GameManager.GetPlayerMovementComponent();
		if(movement!=null)
		{
			if(float.IsFinite(movement.m_MaxSprintStamina)&&movement.m_MaxSprintStamina>0)lastSprintMaximum=movement.m_MaxSprintStamina;
			else if(!float.IsFinite(movement.m_MaxSprintStamina))movement.m_MaxSprintStamina=lastSprintMaximum;
			movement.m_SprintStamina=movement.m_MaxSprintStamina<=0?0:VitalNumbers.Clamp(movement.m_SprintStamina,0,movement.m_MaxSprintStamina);
		}
	}
	[HarmonyPatch(typeof(Fatigue),"GetNormalizedFatigue")]
	private static class FiniteHud
	{
		private static void Postfix(Fatigue __instance,ref float __result)
		{if(Active&&!float.IsFinite(__result))__result=VitalNumbers.Fraction(__instance.m_CurrentFatigue,__instance.m_MaxFatigue,Travel.Last??60);}
	}
	[HarmonyPatch(typeof(Fatigue), nameof(Fatigue.AddFatigue), new[] { typeof(float) })]
	private static class FiniteAddition
	{
		private static bool Prefix(Fatigue __instance,ref float __0)
		{
			if(!Active)return true;
			RepairFatigue(__instance);
			return float.IsFinite(__0);
		}
	}
	// Native Update in 2.59 also increments the field directly, bypassing
	// CalculateFatigueIncrease. Leave its HUD/voice/breathing work intact.
	[HarmonyPatch(typeof(Fatigue), "Update")]
	private static class NativeFrameClock
	{
		private static void Prefix(Fatigue __instance, out float __state)
		{
			if(Active)RepairFatigue(__instance);
			__state = Active && !ActionFatigue && !(GameManager.m_Rest?.IsSleeping() ?? false)
				? __instance.m_CurrentFatigue : float.NaN;
		}
		private static void Postfix(Fatigue __instance, float __state)
		{
			if(Active)RepairFatigue(__instance);
			if (!float.IsNaN(__state) && __instance.m_CurrentFatigue > __state)
				__instance.m_CurrentFatigue = __state;
		}
	}

	[HarmonyPatch(typeof(Fatigue), "CalculateFatigueIncrease")]
	private static class NativeAwakeClock
	{
		private static bool Prefix(ref float __result)
		{
			if (!Active || ActionFatigue) return true;
			// Timed actions pass virtual hours through this method. Charge real
			// awake time once in Tick instead, using the game's buff/difficulty rate.
			__result = 0; return false;
		}
	}
	[HarmonyPatch(typeof(PlayerClimbRope), "UpdateFatigue")]
	private static class RopeAwakeClock
	{
		private static bool Prefix() => !Active || ActionFatigue;
	}
	[HarmonyPatch(typeof(TimeOfDay), "TimeLapseEnd")]
	private static class FinishWorkCharge
	{
		private static void Prefix() { if (actionActive) ChargeWork(Time.realtimeSinceStartup); }
		private static void Postfix(TimeOfDay __instance) { if(MyMod.InOnline()&&__instance.m_IsTimeLapseActiveCounter<0)__instance.m_IsTimeLapseActiveCounter=0;WorkLighting.End(__instance); }
	}
	private static void ChargeWork(float now)
	{
		strength = Mathf.Max(0, strength - CoopBalancePolicy.WorkCharge(strengthPerSecond,lastWorkTime,now,actionStarted,actionEnds));
		lastWorkTime = now;
	}

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
			return VitalNumbers.Fraction(fatigue.m_CurrentFatigue,fatigue.m_MaxFatigue,Travel.Last??60);
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
			if (!Active || !float.IsFinite(hours) || !float.IsFinite(realSeconds) || hours <= 0f || Sleeping)
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
			float rate = WorkRate(weight);
			float fatigueMultiplier = (weight == Weight.Heavy) ? 1.5f : ((weight == Weight.Medium) ? 1.25f : 1f);
			float cost = CoopBalancePolicy.Cost(hours, rate);
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
			if (!CoopBalancePolicy.CanStart(strength, cost, partial))
			{
				Block("Не хватает сил: нужно " + Mathf.CeilToInt(cost) + ", есть " + Mathf.FloorToInt(strength) + (cost > MaxStrength ? ". Уменьшите объём работы или смените инструмент" : ""));
				return;
			}
			Fatigue fatigue = GameManager.GetFatigueComponent();
			float standingPerHour = (fatigue != null) ? fatigue.m_FatigueIncreasePerHourStanding : 0f;
			realSeconds = Mathf.Max(realSeconds, 0.5f);
			actionActive = true;
			actionStarted = now;
			lastWorkTime = now;
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

	private static float WorkRate(Weight weight) => weight == Weight.Heavy ? 16f : weight == Weight.Medium ? 10f : 6f;

	internal static bool ShowsStrengthCost => Active;

	internal static string CostLabel(float hours, bool heavy = false)
	{
		float rate = heavy ? WorkRate(Weight.Heavy) : WorkRate(DetectWeight(out _, out _));
		return $"Сила −{Mathf.CeilToInt(CoopBalancePolicy.Cost(hours, rate))}";
	}

	internal static string CraftingCostLabel(int minutes) =>
		$"Сила −{Mathf.CeilToInt(CoopBalancePolicy.Cost(minutes / 60f, WorkRate(Weight.Medium)))}";

	internal static string CostLabelForReading(float hours) =>
		$"Сила −{Mathf.CeilToInt(CoopBalancePolicy.Cost(hours, WorkRate(Weight.Light)))}";

	internal static string CostLabelForRepair(float hours, GearItem item) =>
		$"Сила −{Mathf.CeilToInt(CoopBalancePolicy.Cost(hours, WorkRate(item?.m_ClothingItem != null ? Weight.Light : Weight.Medium)))}";

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
			if (harvest.IsQuartering() || harvest.IsTabQuarterSelected())
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
			expectedFatigue = float.NaN;
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
		var travelComponent = GameManager.GetFatigueComponent();
		if(Active)RepairFatigue(travelComponent);
		strength=VitalNumbers.Clamp(strength,lastGoodStrength);
		lastGoodStrength=strength;
		if (!MyMod.InOnline() || MyMod.IsDead)
		{
			Travel.Reset();
		}
		else if (!MyMod.WildlifeSceneReady() || travelComponent == null || !travelComponent.m_StartHasBeenCalled || travelComponent.m_MaxFatigue <= 0)
		{
			Travel.Loading(); expectedFatigue = float.NaN;
			return;
		}
		else
		{
			float? restore = Travel.Enter(MyMod.level_guid, travelComponent.Pointer.ToInt64());
			if (restore.HasValue)
			{
				travelComponent.m_CurrentFatigue = VitalNumbers.Clamp(restore.Value,Travel.Last??60,travelComponent.m_MaxFatigue);
				travelComponent.m_FatigueValueLastFrame = travelComponent.m_CurrentFatigue;
				expectedFatigue = travelComponent.m_CurrentFatigue;
				clockOwner = travelComponent.Pointer;
				previousTired=VitalNumbers.Fraction(travelComponent.m_CurrentFatigue,travelComponent.m_MaxFatigue,60);
				wasSleeping=false;actionActive=false;cancelPending=false;lastActionEnd=now;
				lastPosition=GameManager.GetPlayerTransform().position;
				MelonLogger.Msg("[Fatigue travel] Restored " + travelComponent.m_CurrentFatigue + " scene=" + MyMod.level_guid);
			}
		}
		if (!Active)
		{
			expectedFatigue = float.NaN;
			actionActive = false;
			cancelPending = false;
			wasSleeping = false;
			RestoreMovementFatigue();
			return;
		}
		FlattenMovementFatigue();
		bool asleep = GameManager.m_Rest != null && GameManager.m_Rest.IsSleeping();
		var clockFatigue = GameManager.GetFatigueComponent();
		if (!ActionFatigue && clockFatigue != null && !MyMod.LoadingScreenIsOn && !asleep)
		{
			// Some Unity native update paths bypass Harmony trampolines. Reconcile
			// their small continuous increase at our frame boundary as well. Keep
			// discrete penalties (parasites etc.) and every fatigue reduction.
			if (clockOwner == clockFatigue.Pointer && !float.IsNaN(expectedFatigue))
			{
				float extra = clockFatigue.m_CurrentFatigue - expectedFatigue;
				if (extra > 0 && extra < clockFatigue.m_MaxFatigue * .01f)
				{
					clockFatigue.m_CurrentFatigue = expectedFatigue;
					clockFatigue.m_FatigueValueLastFrame = expectedFatigue;
				}
			}
			clockOwner = clockFatigue.Pointer;
			clockFatigue.CalculateTotalFatigueBurnPerHour();
			float rate = VitalNumbers.Rate(clockFatigue.m_TotalFatigueBurnPerHour,clockFatigue.m_MaxFatigue/CoopBalancePolicy.BaseAwakeHours);
			clockFatigue.AddFatigue(rate * dt / (VitalNumbers.Rate(MyMod.period,5) * 60f));
			expectedFatigue = clockFatigue.m_CurrentFatigue;
		}
		else expectedFatigue = float.NaN;
		if (MyMod.InOnline() && clockFatigue != null) Travel.Remember(clockFatigue.m_CurrentFatigue);
		if ((asleep || wasSleeping) && TiredFraction < previousTired)
			strength = Mathf.Min(MaxStrength, strength + (previousTired - TiredFraction) * 200f);
		previousTired = TiredFraction; wasSleeping = asleep;
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
				ChargeWork(now);
				Fatigue workFatigue = GameManager.GetFatigueComponent();
				if (workFatigue != null)
				{
					if (fatiguePerSecond > 0f)
					{
						workFatigue.AddFatigue(fatiguePerSecond * dt);
					}
				}
				if (strength <= 0f && now < actionEnds - .05f)
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
			PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
			bool resting = playerManager != null && playerManager.PlayerIsCrouched() && speed < .3f;
			if (!asleep && !MyMod.LoadingScreenIsOn && (playerManager == null || !playerManager.PlayerIsClimbing()))
			{
				float rate = BedRest.Active ? CoopBalancePolicy.BedRecoveryRate(TiredFraction) :
					CoopBalancePolicy.RecoveryRate(speed >= .3f, resting, TiredFraction);
				strength = Mathf.Min(MaxStrength, strength + rate * dt);
			}
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
		if (savedWalking < 0f || fatiguePointer != fatigue.Pointer)
		{
			fatiguePointer = fatigue.Pointer;
			savedStanding = fatigue.m_FatigueIncreasePerHourStanding;
			savedWalking = fatigue.m_FatigueIncreasePerHourWalking;
			savedSprintMin = fatigue.m_FatigueIncreasePerHourSprintingMin;
			savedSprintMax = fatigue.m_FatigueIncreasePerHourSprintingMax;
			MelonLogger.Msg("[Balance] Original standing="+savedStanding+", awake clock="+(fatigue.m_MaxFatigue/CoopBalancePolicy.BaseAwakeHours)+" per hour before native modifiers; rope extra fatigue="+ActionFatigue);
		}
		float standing = fatigue.m_MaxFatigue / CoopBalancePolicy.BaseAwakeHours;
		fatigue.m_FatigueIncreasePerHourStanding = standing;
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
		if (fatigue != null && fatigue.Pointer == fatiguePointer)
		{
			fatigue.m_FatigueIncreasePerHourStanding = savedStanding;
			fatigue.m_FatigueIncreasePerHourWalking = savedWalking;
			fatigue.m_FatigueIncreasePerHourSprintingMin = savedSprintMin;
			fatigue.m_FatigueIncreasePerHourSprintingMax = savedSprintMax;
		}
		savedWalking = -1f;
	}

	public static void ResetForRespawn()
	{
		Travel.Reset(); expectedFatigue = float.NaN;
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
		// Shown like the game's time and scent indicators: while the HUD key (Tab) is held, and while working.
		bool wanted = Active && (actionActive || KeyboardUtilities.InputManager.GetKey(KeyCode.Tab));
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
