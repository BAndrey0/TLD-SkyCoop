using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MelonLoader;

namespace GameServer;

internal class ThreadManager
{
	public static readonly List<Action> executeOnMainThread = new List<Action>();

	public static readonly List<Action> executeCopiedOnMainThread = new List<Action>();

	public static bool actionToExecuteOnMainThread = false;

	public static void Log(string TXT)
	{
		try
		{
			WriteModLog(TXT);
		}
		catch
		{
			Console.WriteLine(TXT);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void WriteModLog(string value)
	{
		MelonLogger.Error(value);
	}

	public static void ExecuteOnMainThread(Action _action)
	{
		if (_action == null)
		{
			Log("No action to execute on main thread!");
			return;
		}
		lock (executeOnMainThread)
		{
			executeOnMainThread.Add(_action);
			actionToExecuteOnMainThread = true;
		}
	}

	public static void UpdateMain()
	{
		if (!actionToExecuteOnMainThread)
		{
			return;
		}
		executeCopiedOnMainThread.Clear();
		lock (executeOnMainThread)
		{
			executeCopiedOnMainThread.AddRange(executeOnMainThread);
			executeOnMainThread.Clear();
			actionToExecuteOnMainThread = false;
		}
		for (int i = 0; i < executeCopiedOnMainThread.Count; i++)
		{
			try
			{
				executeCopiedOnMainThread[i]();
			}
			catch (Exception ex)
			{
				Log("[SkyCoop] Main-thread action failed: " + ex);
			}
		}
	}
}
