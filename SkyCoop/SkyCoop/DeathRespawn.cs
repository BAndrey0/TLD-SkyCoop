using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Every sandbox difficulty is permadeath, and in co-op a dead player was left on the death journal with no working
// button (the journal's back button no longer reaches the patched OnQuitToMainMenu in 2.55). In co-op death always
// ends in a respawn: the journal's back actions respawn, and an overlay offers "respawn" and "respawn and go to menu".
public static class DeathRespawn
{
	[HarmonyPatch(typeof(Panel_Log), "OnBack")]
	private static class JournalBack
	{
		private static bool Prefix()
		{
			return !TryRespawnFromJournal("OnBack");
		}
	}

	[HarmonyPatch(typeof(Panel_Log), "ReallyQuitToMainMenu")]
	private static class JournalReallyQuit
	{
		private static bool Prefix()
		{
			return !TryRespawnFromJournal("ReallyQuitToMainMenu");
		}
	}

	private static double menuAfter = -1.0;

	private static float lastRespawn = -100f;

	private static bool PlayerDead
	{
		get
		{
			PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
			if (playerManager != null)
			{
				return playerManager.GetControlMode() == PlayerControlMode.Dead;
			}
			return false;
		}
	}

	private static bool InWorld
	{
		get
		{
			if (!string.IsNullOrEmpty(MyMod.level_name) && MyMod.level_name != "Empty" && MyMod.level_name != "Boot")
			{
				return !Shared.IsMainMenuScene(MyMod.level_name);
			}
			return false;
		}
	}

	private static bool TryRespawnFromJournal(string from)
	{
		if (!MyMod.InOnline() || !PlayerDead)
		{
			return false;
		}
		MelonLogger.Msg("[Death] Journal " + from + " -> respawn");
		Respawn();
		return true;
	}

	private static void Respawn()
	{
		// One respawn at a time: the journal button and the overlay can both fire in the same moment.
		if (Time.realtimeSinceStartup - lastRespawn < 3f)
		{
			return;
		}
		lastRespawn = Time.realtimeSinceStartup;
		MyMod.BeginRespawn();
	}

	public static void Tick()
	{
		if (menuAfter < 0.0 || Time.realtimeSinceStartup < menuAfter)
		{
			return;
		}
		if (!InWorld || PlayerDead)
		{
			// Still loading the respawn scene; try again shortly.
			menuAfter = Time.realtimeSinceStartup + 1f;
			return;
		}
		menuAfter = -1.0;
		if (CoopReturnToMenu.CanRequest)
		{
			CoopReturnToMenu.Request();
		}
	}

	public static void Draw()
	{
		if (!MyMod.InOnline() || !InWorld || !PlayerDead)
		{
			return;
		}
		int depth = GUI.depth;
		GUI.depth = -15000;
		float width = 520f;
		float x = ((float)Screen.width - width) / 2f;
		float y = Screen.height - 150;
		GUI.Box(new Rect(x, y, width, 100f), "Вы погибли. В кооперативе можно продолжить новым персонажем на этом же сервере.");
		if (GUI.Button(new Rect(x + 20f, y + 45f, 230f, 40f), "Возродиться"))
		{
			MelonLogger.Msg("[Death] Respawn from overlay");
			Respawn();
		}
		if (GUI.Button(new Rect(x + width - 250f, y + 45f, 230f, 40f), "Возродиться и выйти в меню"))
		{
			MelonLogger.Msg("[Death] Respawn and return to menu from overlay");
			Respawn();
			menuAfter = Time.realtimeSinceStartup + 4f;
		}
		GUI.depth = depth;
	}
}
