using System;

namespace SkyCoop;

public static class WildlifeSpawnAuthorityPolicy
{
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
		if (connected && !loading && !dead && !knocked && ticks > 0)
		{
			return WorldScene(scene);
		}
		return false;
	}

	public static bool AcceptGrant(bool ready, string currentScene, string grantScene)
	{
		if (ready && WorldScene(grantScene))
		{
			return string.Equals(currentScene, grantScene, StringComparison.Ordinal);
		}
		return false;
	}
}
