using System.Collections.Generic;

namespace SkyCoop;

public static class SleepRendezvousPolicy
{
	public static bool AllReady(int sleepers, int participants, int dead)
	{
		if (participants > 0 && sleepers >= participants)
		{
			return dead < participants;
		}
		return false;
	}

	public static int MinimumRequestedHours(IEnumerable<int> requested)
	{
		int num = int.MaxValue;
		if (requested != null)
		{
			foreach (int item in requested)
			{
				if (item > 0 && item < num)
				{
					num = item;
				}
			}
		}
		if (num != int.MaxValue)
		{
			return num;
		}
		return 0;
	}
}
