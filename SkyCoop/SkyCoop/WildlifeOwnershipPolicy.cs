namespace SkyCoop;

public static class WildlifeOwnershipPolicy
{
	public static bool Eligible(bool connected, bool loading, bool dead, bool knocked, string candidateGuid, string currentGuid)
	{
		return connected && !loading && !dead && !knocked &&
			WildlifeSpawnAuthorityPolicy.SameScene(candidateGuid, currentGuid);
	}

	public static bool Closer(float candidateDistance, float ownerDistance) =>
		candidateDistance + 5f < ownerDistance;

	public static bool AcceptSnapshot(int owner, int sender, bool ownerActive) =>
		owner < 0 || owner == sender || !ownerActive;
}
