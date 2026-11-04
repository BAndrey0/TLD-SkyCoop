using System;

namespace SkyCoop;

public static class WildlifeSpawnAuthorityPolicy
{
	public static bool LocalReady(bool loading, bool dead, string scene) => !loading && !dead && WorldScene(scene);
	public static bool WorldScene(string scene)
	{
		if (!string.IsNullOrEmpty(scene) && scene != "null" && scene != "Boot" && scene != "Empty" && scene != "MainMenu")
		{
			return !scene.StartsWith("MainMenu_", StringComparison.Ordinal);
		}
		return false;
	}

	public static bool Ready(bool connected, bool loading, bool dead, bool knocked, int ticks, string scene)
	{
		// Loading now reflects the explicit scene-ready handshake. A host-side
		// tick count is neither necessary nor proof that the remote scene loaded.
		if (connected && !loading && !dead && !knocked)
		{
			return WorldScene(scene);
		}
		return false;
	}

	public static bool AcceptGrant(bool ready, string currentScene, string grantScene)
	{
		return ready && SameScene(currentScene, grantScene);
	}

	public static bool SameScene(string first, string second) =>
		WorldScene(first) && string.Equals(first, second, StringComparison.Ordinal);
}
