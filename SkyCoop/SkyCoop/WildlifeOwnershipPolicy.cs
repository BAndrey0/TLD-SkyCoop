namespace SkyCoop;

public static class WildlifeOwnershipPolicy
{
	public static bool Eligible(bool connected, bool loading, bool dead, bool knocked, int candidateScene, string candidateGuid, int currentScene, string currentGuid)
	{
		if (connected && !loading && !dead && !knocked && candidateScene == currentScene)
		{
			return candidateGuid == currentGuid;
		}
		return false;
	}
}
