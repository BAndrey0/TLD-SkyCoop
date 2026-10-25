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

		private static void LegacyFailed(Exception ex)
		{
			legacyFailed = true;
			MelonLogger.Warning("[Input] UnityEngine.Input unavailable, using the game's input manager: " + ex.Message);
		}
	}
}
