using System;

namespace SkyCoop;

// Rules expressed independently of Unity so long recipes, cancellation and clock
// boundaries can be checked without loading a save.
public static class CoopBalancePolicy
{
	public const float BaseAwakeHours = 16f;
	public const float RecoverySeconds = 600f;
	public const float BedRecoverySeconds = 330f;
	public static float BedRecoveryRate(float tired) => 100f / BedRecoverySeconds * FatigueRecoveryMultiplier(tired);
	private static float FatigueRecoveryMultiplier(float tired) => 1f - .6f * Math.Clamp((tired - .65f) / .35f, 0f, 1f);
	public static float Cost(float hours, float rate) => Math.Max(0, hours) * Math.Max(0, rate);
	public static bool CanStart(float strength, float cost, bool resumable) => resumable ? strength > .5f : strength + .001f >= cost;
	public static float WorkCharge(float rate, float since, float until, float start, float end) => Math.Max(0, Math.Min(until, end) - Math.Max(since, start)) * Math.Max(0, rate);
	public static float RecoveryRate(bool moving, bool resting, float tired)
	{
		float recovery = 100f / RecoverySeconds * (resting ? 1.65f : moving ? 1f : 1.5f);
		return recovery * FatigueRecoveryMultiplier(tired);
	}
	public static int AdvanceClock(int hour, int minute, int hours) => ((hour * 60 + minute + Math.Max(0, hours) * 60) % 1440 + 1440) % 1440;
}
