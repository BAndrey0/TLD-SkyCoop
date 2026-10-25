using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class SpawnSafety
{
	private const float BootstrapDelaySeconds = 2f;

	private const float PublishedGraceSeconds = 2f;

	private const float MaximumBootstrapProtectionSeconds = 120f;

	private static bool armed;

	private static bool expired;

	private static bool failed;

	private static string scene = "";

	private static float armedAt = -1f;

	private static float loadedAt = -1f;

	private static float publishedAt = -1f;

	private static int deferredAttacks;

	private static int bootstrapCalls;

	private static bool PendingHost
	{
		get
		{
			if (!MyMod.HostFromLobbyAfterLoader)
			{
				return MyMod.StartServerAfterSelectSave;
			}
			return true;
		}
	}

	private static bool WorldScene
	{
		get
		{
			if (MyMod.level_name != "Empty" && MyMod.level_name != "Boot" && !string.IsNullOrEmpty(MyMod.level_name))
			{
				return !Shared.IsMainMenuScene(MyMod.level_name);
			}
			return false;
		}
	}

	public static void Reset()
	{
		armed = (expired = (failed = false));
		scene = "";
		armedAt = (loadedAt = (publishedAt = -1f));
		deferredAttacks = (bootstrapCalls = 0);
	}

	public static void LoadingChanged(bool loading)
	{
		if (loading)
		{
			Reset();
		}
		else if (PendingHost && WorldScene)
		{
			Arm();
			loadedAt = Time.realtimeSinceStartup;
		}
	}

	private static void Arm()
	{
		if (!armed)
		{
			armed = true;
			scene = MyMod.level_name;
			armedAt = Time.realtimeSinceStartup;
			MelonLogger.Msg("[Spawn safety] Initial host bootstrap armed; new local predator attacks temporarily deferred.");
		}
	}

	private static bool NativeWorldReady()
	{
		if (!WorldScene || MyMod.LoadingScreenIsOn || GameManager.GetPlayerTransform() == null || GameManager.GetPlayerManagerComponent() == null || GameManager.GetTimeOfDayComponent() == null)
		{
			return false;
		}
		Panel_Loading panel_Loading = GameCompat.Panel<Panel_Loading>();
		if (!(panel_Loading == null))
		{
			return panel_Loading.HasFinishedLoading();
		}
		return true;
	}

	public static void Tick()
	{
		if (!WorldScene || MyMod.IReallyWantToQuit || MyMod.SaveExitPending)
		{
			if (armed)
			{
				Reset();
			}
			return;
		}
		if (armed && scene != MyMod.level_name)
		{
			Reset();
		}
		if (PendingHost && !failed)
		{
			Arm();
			if (!NativeWorldReady())
			{
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (loadedAt < 0f)
			{
				loadedAt = realtimeSinceStartup;
			}
			if (realtimeSinceStartup - loadedAt < 2f)
			{
				return;
			}
			MyMod.SendAfterLoadingFinished = 0;
			bootstrapCalls++;
			try
			{
				MyMod.FinishedLoading();
			}
			catch (Exception ex)
			{
				failed = true;
				MyMod.HostFromLobbyAfterLoader = false;
				MyMod.StartServerAfterSelectSave = false;
				MelonLogger.Error("[Spawn safety] Host bootstrap failed; temporary protection ended: " + ex);
				return;
			}
			if (!MyMod.iAmHost)
			{
				failed = true;
				MyMod.HostFromLobbyAfterLoader = false;
				MyMod.StartServerAfterSelectSave = false;
				MelonLogger.Error("[Spawn safety] Host bootstrap returned without a running host; protection ended.");
				return;
			}
			MelonLogger.Msg("[Spawn safety] Initial host bootstrap completed on unscaled time.");
		}
		if (!armed)
		{
			return;
		}
		if (!PendingHost && MyMod.iAmHost)
		{
			if (publishedAt < 0f)
			{
				publishedAt = Time.realtimeSinceStartup;
			}
			if (Time.realtimeSinceStartup - publishedAt >= 2f)
			{
				Reset();
			}
		}
		else if (!PendingHost)
		{
			Reset();
		}
	}

	private static bool ProtectionActive()
	{
		if (!WorldScene || MyMod.IsDead || MyMod.IReallyWantToQuit || MyMod.SaveExitPending || failed)
		{
			return false;
		}
		if (PendingHost)
		{
			Arm();
		}
		if (!armed || scene != MyMod.level_name)
		{
			return false;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup - armedAt >= 120f)
		{
			if (!expired)
			{
				expired = true;
				MelonLogger.Warning("[Spawn safety] Initial host startup exceeded the bounded protection window; attacks resume.");
			}
			return false;
		}
		if (PendingHost)
		{
			return true;
		}
		if (!MyMod.iAmHost)
		{
			return false;
		}
		if (publishedAt < 0f)
		{
			publishedAt = realtimeSinceStartup;
		}
		return realtimeSinceStartup - publishedAt < 2f;
	}

	public static bool DeferLocalAttack(BaseAi animal)
	{
		if (!ProtectionActive() || animal == null || animal.m_CurrentTarget == null || !animal.m_CurrentTarget.IsPlayer())
		{
			return false;
		}
		Transform playerTransform = GameManager.GetPlayerTransform();
		Transform transform = animal.m_CurrentTarget.transform;
		if (playerTransform == null || transform == null || (transform != playerTransform && !transform.IsChildOf(playerTransform)))
		{
			return false;
		}
		PlayerStruggle playerStruggleComponent = GameManager.GetPlayerStruggleComponent();
		if (playerStruggleComponent != null && playerStruggleComponent.InStruggle())
		{
			return false;
		}
		deferredAttacks++;
		return true;
	}

	public static object Snapshot()
	{
		return new
		{
			armed = armed,
			expired = expired,
			failed = failed,
			scene = scene,
			pendingHost = PendingHost,
			armedAt = armedAt,
			loadedAt = loadedAt,
			publishedAt = publishedAt,
			deferredAttacks = deferredAttacks,
			bootstrapCalls = bootstrapCalls,
			maximumSeconds = 120f
		};
	}
}
