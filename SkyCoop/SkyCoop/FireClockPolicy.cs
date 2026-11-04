using System;

namespace SkyCoop;

internal static class FireClockPolicy
{
	internal static double SecondsAt(int worldMinute) => (double)worldMinute * 60;

	internal static float Remaining(double deadline, int worldMinute)
		=> double.IsFinite(deadline) ? (float)Math.Max(0, deadline - SecondsAt(worldMinute)) : 0;

	internal static double ChangeDeadline(double deadline, int worldMinute, float delta, float capacity)
	{
		double now = SecondsAt(worldMinute);
		if (!float.IsFinite(delta))
			delta = 0;
		if (!float.IsFinite(capacity) || capacity < 0)
			capacity = 0;
		return now + Math.Clamp(Remaining(deadline, worldMinute) + delta, 0, capacity);
	}
}
