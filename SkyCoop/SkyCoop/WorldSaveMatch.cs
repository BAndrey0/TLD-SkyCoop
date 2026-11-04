using System;

namespace SkyCoop;

internal static class WorldSaveMatch
{
	internal const int Protocol = 1;

	internal static bool CanResume(string hostId, int seed, string slotId, string displayName)
	{
		if (string.IsNullOrEmpty(hostId))
		{
			return string.IsNullOrEmpty(slotId) && seed != 0 && displayName == seed.ToString();
		}
		return Guid.TryParseExact(hostId, "N", out Guid hostWorld) &&
			Guid.TryParseExact(slotId, "N", out Guid slotWorld) && hostWorld == slotWorld;
	}
}
