using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MelonLoader;
using TinyJSON;

namespace SkyCoop;

public class ModsValidation
{
	public class ModHashPair
	{
		public string m_Name = "";

		public string m_Hash = "";

		public ModHashPair(string n, string h)
		{
			m_Name = n;
			m_Hash = h;
		}
	}

	public class ModValidationData
	{
		public long m_Hash;

		public List<ModHashPair> m_Files = new List<ModHashPair>();

		public string m_FullString = "";

		public string m_FullStringBase64 = "";

		public List<string> m_WhiteList = new List<string>();
	}

	public static ModValidationData LastRequested = null;

	public static List<string> ServerSideOnlyFiles = new List<string>();

	public static List<string> WhitelistFiles = new List<string>();

	public static string SHA256CheckSum(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return "null";
		}
		byte[] array;
		using (SHA256 sHA = SHA256.Create())
		{
			using FileStream inputStream = File.OpenRead(filePath);
			array = sHA.ComputeHash(inputStream);
		}
		string text = string.Empty;
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			text += b.ToString("x2");
		}
		return text;
	}

	public static bool ServerSideOnly(string Name)
	{
		if (!ServerSideOnlyFiles.Contains(Name))
		{
			return WhitelistFiles.Contains(Name);
		}
		return true;
	}

	public static ModValidationData GetModsHash(bool Force = false, List<string> WhitelistedHashes = null)
	{
		ModValidationData modValidationData = new ModValidationData();
		if (WhitelistedHashes == null)
		{
			WhitelistedHashes = new List<string>();
		}
		if (!Force && LastRequested != null)
		{
			return LastRequested;
		}
		if (MyMod.DedicatedServerAppMode)
		{
			if (File.Exists("Mods\\serversideonly.json"))
			{
				MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Found Server Side Files List!");
				ServerSideOnlyFiles = JSON.Load(File.ReadAllText("Mods\\serversideonly.json")).Make<List<string>>();
			}
			if (File.Exists("modswhitelist.json"))
			{
				MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Found Mods White List!");
				WhitelistFiles = JSON.Load(File.ReadAllText("modswhitelist.json")).Make<List<string>>();
			}
		}
		foreach (MelonMod registeredMelon in MelonTypeBase<MelonMod>.RegisteredMelons)
		{
			string hash = registeredMelon.MelonAssembly.Hash;
			string text = registeredMelon.MelonAssembly.Assembly.GetName().Name + ".dll";
			if (!ServerSideOnly(text) || WhitelistedHashes.Contains(hash))
			{
				modValidationData.m_Files.Add(new ModHashPair("Mods\\" + text, hash));
			}
			else
			{
				MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Ignore " + text);
			}
			if (WhitelistFiles.Contains(text))
			{
				modValidationData.m_WhiteList.Add(hash);
			}
		}
		FileInfo[] files = new DirectoryInfo("Mods").GetFiles("*.modcomponent");
		foreach (FileInfo fileInfo in files)
		{
			string text2 = SHA256CheckSum("Mods\\" + fileInfo.Name);
			string name = fileInfo.Name;
			if (!ServerSideOnly(name) && !WhitelistedHashes.Contains(text2))
			{
				modValidationData.m_Files.Add(new ModHashPair("Mods\\" + name, text2));
			}
			else
			{
				MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Ignore " + name);
			}
			if (WhitelistFiles.Contains(name))
			{
				modValidationData.m_WhiteList.Add(text2);
			}
		}
		foreach (MelonPlugin registeredMelon2 in MelonTypeBase<MelonPlugin>.RegisteredMelons)
		{
			string hash2 = registeredMelon2.MelonAssembly.Hash;
			string text3 = registeredMelon2.MelonAssembly.Assembly.GetName().Name + ".dll";
			if (!ServerSideOnly(text3) && !WhitelistedHashes.Contains(hash2))
			{
				modValidationData.m_Files.Add(new ModHashPair("Plugins\\" + text3, hash2));
			}
			else
			{
				MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Ignore " + text3);
			}
			if (WhitelistFiles.Contains(text3))
			{
				modValidationData.m_WhiteList.Add(hash2);
			}
		}
		string text4 = "";
		string text5 = "";
		modValidationData.m_Files.Sort((ModHashPair x, ModHashPair y) => x.m_Name.CompareTo(y.m_Name));
		foreach (ModHashPair file in modValidationData.m_Files)
		{
			if (string.IsNullOrEmpty(text4))
			{
				text4 = file.m_Hash;
				text5 = file.m_Name;
			}
			else
			{
				text4 += file.m_Hash;
				text5 = text5 + "\n" + file.m_Name;
			}
			MelonLogger.Msg(ConsoleColor.Green, "[ModsValidation][Info] " + file.m_Name + " Hash: " + file.m_Hash);
		}
		modValidationData.m_Hash = Shared.GetDeterministicId(text4);
		modValidationData.m_FullString = text5;
		modValidationData.m_FullStringBase64 = Shared.CompressString(text5);
		MelonLogger.Msg(ConsoleColor.Blue, "[ModsValidation][Info] Main Hash: " + modValidationData.m_Hash);
		MelonLogger.Msg(ConsoleColor.Magenta, "[ModsValidation][Info] Stock: " + Encoding.UTF8.GetBytes(modValidationData.m_FullString).Length);
		MelonLogger.Msg(ConsoleColor.Magenta, "[ModsValidation][Info] Compressed: " + Shared.CompressString(modValidationData.m_FullStringBase64).Length);
		LastRequested = modValidationData;
		return modValidationData;
	}
}
