using UnityEngine;

namespace SkyCoop;

public static class KeyboardUtilities
{
	public static class InputManager
	{
		public static bool GetKey(KeyCode key)
		{
			try
			{
				return Input.GetKey(key);
			}
			catch
			{
				return false;
			}
		}

		public static bool GetKeyDown(KeyCode key)
		{
			try
			{
				return Input.GetKeyDown(key);
			}
			catch
			{
				return false;
			}
		}
	}
}
