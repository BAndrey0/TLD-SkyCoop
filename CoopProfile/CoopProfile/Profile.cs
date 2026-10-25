using System;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace CoopProfile;

public class Profile : MelonMod
{
	private static string root;

	public override void OnInitializeMelon()
	{
		root = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "saves"));
		string[] array = new string[2] { "Init", "CreatePersistentDataFolders" };
		foreach (string name in array)
		{
			base.HarmonyInstance.Patch(AccessTools.Method(typeof(PersistentDataPath), name), new HarmonyMethod(typeof(Profile), "InitPaths"));
		}
		array = new string[3] { "GetPersistentDataPath", "GetPersistentDataPathForHardDrive", "GetBaseFolderPath" };
		foreach (string name2 in array)
		{
			base.HarmonyInstance.Patch(AccessTools.Method(typeof(PersistentDataPath), name2), new HarmonyMethod(typeof(Profile), "GetPath"));
		}
		InitPaths();
		MelonLogger.Msg("COOP_PROFILE_SAVE_ROOT=" + root);
	}

	private static bool InitPaths()
	{
		PersistentDataPath.m_PathSeparator = Path.DirectorySeparatorChar.ToString();
		PersistentDataPath.m_Path = root;
		PersistentDataPath.m_PathNoSync = Path.Combine(root, "NoSync");
		PersistentDataPath.m_PathSurvival = Path.Combine(root, "Survival");
		PersistentDataPath.m_PathNoSyncSurvival = Path.Combine(root, "SurvivalNoSync");
		string[] array = new string[4]
		{
			root,
			PersistentDataPath.m_PathNoSync,
			PersistentDataPath.m_PathSurvival,
			PersistentDataPath.m_PathNoSyncSurvival
		};
		for (int i = 0; i < array.Length; i++)
		{
			Directory.CreateDirectory(array[i]);
		}
		return false;
	}

	private static bool GetPath(ref string __result)
	{
		__result = root;
		return false;
	}
}
