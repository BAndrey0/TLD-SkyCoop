using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SkyCoop;

public static class PatchLoader
{
	public static readonly List<string> FailedPatches = new List<string>();

	public static int AppliedPatches { get; private set; }

	public static void ApplyPatches(HarmonyLib.Harmony harmony)
	{
		if (harmony == null)
		{
			MelonLogger.Error("[SkyCoop] No Harmony instance to patch with.");
			return;
		}
		AppliedPatches = 0;
		FailedPatches.Clear();
		Type[] array;
		try
		{
			array = typeof(PatchLoader).Assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			array = Array.FindAll(ex.Types, (Type t) => t != null);
		}
		Type[] array2 = array;
		foreach (Type type in array2)
		{
			if (HasHarmonyPatchAttribute(type))
			{
				try
				{
					harmony.CreateClassProcessor(type).Patch();
					AppliedPatches++;
				}
				catch (Exception e)
				{
					string text = type.FullName + ": " + Describe(e);
					FailedPatches.Add(text);
					MelonLogger.Warning("[SkyCoop] Skipped patch " + text);
				}
			}
		}
		if (FailedPatches.Count == 0)
		{
			MelonLogger.Msg(ConsoleColor.Green, "[SkyCoop] Applied " + AppliedPatches + " patch classes.");
			return;
		}
		MelonLogger.Warning("[SkyCoop] Applied " + AppliedPatches + " patch classes, " + FailedPatches.Count + " did not fit this game version. The features behind them are disabled; the rest of the mod works.");
		MelonLogger.Warning("[SkyCoop] This usually means the game updated. Check for a newer SkyCoop build.");
	}

	private static bool HasHarmonyPatchAttribute(Type type)
	{
		try
		{
			return type.GetCustomAttributes(typeof(HarmonyPatch), inherit: true).Length != 0;
		}
		catch
		{
			return false;
		}
	}

	private static string Describe(Exception e)
	{
		while (e.InnerException != null)
		{
			e = e.InnerException;
		}
		return e.Message;
	}
}
