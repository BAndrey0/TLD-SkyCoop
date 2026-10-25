using System;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class KeyboardUtilities
{
	public static class InputManager
	{
		private static bool legacyFailed;

		public static bool GetKey(KeyCode key)
		{
			if (!legacyFailed)
			{
				try
				{
					return Input.GetKey(key);
				}
				catch (Exception ex)
				{
					LegacyFailed(ex);
				}
			}
			return false;
		}

		public static bool GetKeyDown(KeyCode key)
		{
			if (!legacyFailed)
			{
				try
				{
					if (Input.GetKeyDown(key))
					{
						return true;
					}
				}
				catch (Exception ex)
				{
					LegacyFailed(ex);
				}
			}
			// The game's own input wrapper (Rewired) still sees keys when legacy Input does not.
			try
			{
				return Il2Cpp.InputManager.GetKeyDown(Il2Cpp.InputManager.m_CurrentContext, key);
			}
			catch
			{
				return false;
			}
		}

		private static readonly System.Collections.Generic.Dictionary<KeyCode, float> LastToggle = new System.Collections.Generic.Dictionary<KeyCode, float>();

		// Legacy Input reports a press on its frame and the game's input wrapper reports the same press a frame later,
		// so a window closed by one source was reopened by the other. One press counts once within a short window.
		public static bool GetTogglePressed(KeyCode key)
		{
			if (!GetKeyDown(key))
			{
				return false;
			}
			float now = Time.realtimeSinceStartup;
			if (LastToggle.TryGetValue(key, out var last) && now - last < 0.35f)
			{
				return false;
			}
			LastToggle[key] = now;
			return true;
		}

		public static void MarkToggled(KeyCode key)
		{
			LastToggle[key] = Time.realtimeSinceStartup;
		}

		private static void LegacyFailed(Exception ex)
		{
			legacyFailed = true;
			MelonLogger.Warning("[Input] UnityEngine.Input unavailable, using the game's input manager: " + ex.Message);
		}
	}
}
