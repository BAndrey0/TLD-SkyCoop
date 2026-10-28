using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Game-time effects that did not end in co-op: an emergency stim (adrenaline) stayed active forever, even through
// death, and a fired flare-gun round kept burning on the ground. In co-op the world clock is pinned to the host's
// minute every frame (MyMod.SetTime), which these timers do not survive reliably. Both are ended here by real time,
// a little after they would have ended at normal game speed (a game hour is five real minutes).
public static class TimerWatchdog
{
	private const float RealSecondsPerGameHour = 300f;

	private const float FlareMaxBurnSeconds = 150f;

	private static float stimStarted = -1f;

	private static float nextFlareScan;

	private static readonly Dictionary<IntPtr, float> landedFlares = new Dictionary<IntPtr, float>();

	public static void Tick()
	{
		if (!MyMod.InOnline())
		{
			stimStarted = -1f;
			landedFlares.Clear();
			return;
		}
		WatchStim();
		WatchFlares();
	}

	private static void WatchStim()
	{
		try
		{
			EmergencyStim stim = GameManager.GetEmergencyStimComponent();
			if (stim == null || !stim.GetEmergencyStimActive())
			{
				stimStarted = -1f;
				return;
			}
			float now = Time.realtimeSinceStartup;
			if (stimStarted < 0f)
			{
				stimStarted = now;
				return;
			}
			float hours = 1f;
			if (stim.m_EmergencyStimParams != null && stim.m_EmergencyStimParams.m_HoursStimulatedWhenInjected > 0f)
			{
				hours = stim.m_EmergencyStimParams.m_HoursStimulatedWhenInjected;
			}
			if (now - stimStarted > hours * RealSecondsPerGameHour + 30f)
			{
				stim.ResetEmergencyStim();
				stimStarted = -1f;
				MelonLogger.Msg("[Timers] Emergency stim ended by real time");
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Timers] Stim: " + ex.Message);
			stimStarted = -1f;
		}
	}

	private static void WatchFlares()
	{
		float now = Time.realtimeSinceStartup;
		if (now < nextFlareScan)
		{
			return;
		}
		nextFlareScan = now + 2f;
		try
		{
			Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<FlareGunRoundItem> rounds = UnityEngine.Object.FindObjectsOfType<FlareGunRoundItem>();
			HashSet<IntPtr> seen = new HashSet<IntPtr>();
			for (int i = 0; i < rounds.Length; i++)
			{
				FlareGunRoundItem round = rounds[i];
				if (round == null || !round.IsBurning() || round.InFlight(false))
				{
					continue;
				}
				seen.Add(round.Pointer);
				if (!landedFlares.TryGetValue(round.Pointer, out var landed))
				{
					landedFlares[round.Pointer] = now;
				}
				else if (now - landed > FlareMaxBurnSeconds)
				{
					round.BurnOut();
					MelonLogger.Msg("[Timers] Flare-gun round burnt out by real time");
				}
			}
			List<IntPtr> gone = new List<IntPtr>();
			foreach (IntPtr key in landedFlares.Keys)
			{
				if (!seen.Contains(key))
				{
					gone.Add(key);
				}
			}
			foreach (IntPtr key in gone)
			{
				landedFlares.Remove(key);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Timers] Flares: " + ex.Message);
		}
	}

	public static void ResetForRespawn()
	{
		try
		{
			EmergencyStim stim = GameManager.GetEmergencyStimComponent();
			if (stim != null && stim.GetEmergencyStimActive())
			{
				stim.ResetEmergencyStim();
			}
		}
		catch
		{
		}
		stimStarted = -1f;
	}
}
