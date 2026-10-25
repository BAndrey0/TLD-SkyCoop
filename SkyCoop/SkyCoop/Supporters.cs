using System;
using System.Collections.Generic;
using System.Net;
using MelonLoader;
using TinyJSON;
using UnityEngine;

namespace SkyCoop;

public class Supporters
{
	public class SupporterBenefits
	{
		public bool m_Knife;

		public bool m_BrightNick;

		public bool m_Canadium;

		public List<int> m_Flairs = new List<int>();
	}

	public class Supporter
	{
		public string E = "";

		public string S = "";

		public string N = "";

		public List<string> P = new List<string>();
	}

	public class JsonStruct
	{
		public List<Supporter> Supps = new List<Supporter>();

		public List<string> Every = new List<string>();
	}

	public static string LoadedJSON = "";

	public static Dictionary<string, List<string>> SupportersData = new Dictionary<string, List<string>>();

	public static List<string> EveryOneAvailableFlairs = new List<string>();

	public static bool DeBug = false;

	public static bool FlairsIDsReady = false;

	public static List<string> FlairsIDs = new List<string>();

	public static string MyID = "";

	public static SupporterBenefits AvailableBenefits = new SupporterBenefits();

	public static SupporterBenefits ConfiguratedBenefits = new SupporterBenefits();

	public static int FlairSpots = 4;

	public static WebClient web;

	public static void FlairsIDsInit()
	{
		FlairsIDs.Add("Supp");
		FlairsIDs.Add("Gift");
		FlairsIDs.Add("DrkW");
		FlairsIDs.Add("Crtg");
		FlairsIDs.Add("Bugy");
		FlairsIDs.Add("Ban");
		FlairsIDs.Add("Bee");
		FlairsIDs.Add("Pin");
		FlairsIDs.Add("Crab");
		FlairsIDs.Add("Ghst");
		FlairsIDs.Add("Mlon");
		FlairsIDs.Add("Lkot");
		FlairsIDs.Add("WiRo");
		FlairsIDs.Add("Bear");
		FlairsIDs.Add("Kai");
		FlairsIDs.Add("Pie");
		FlairsIDs.Add("Snw");
		FlairsIDs.Add("Lammy");
		FlairsIDs.Add("Dedi");
		FlairsIDs.Add("Ogur");
		FlairsIDsReady = true;
	}

	public static void ApplyFlairsForModel(int PlayerIndex, List<int> FlairsConfig)
	{
		if (PlayerIndex >= 0 && PlayerIndex < MyMod.players.Count && (bool)MyMod.players[PlayerIndex])
		{
			ApplyFlairsForModel(MyMod.players[PlayerIndex], FlairsConfig, "Client " + PlayerIndex);
		}
		else
		{
			DebugLog("Can't apply flairs, there no player object with index " + PlayerIndex);
		}
	}

	public static void ApplyFlairsForModel(GameObject obj, List<int> FlairsConfig, string DebugName = "unknown")
	{
		if (obj == null)
		{
			DebugLog("Can't apply flairs, object " + DebugName + " is null");
			return;
		}
		Transform child = obj.transform.GetChild(3).GetChild(8).GetChild(0)
			.GetChild(7);
		for (int i = 0; i < FlairSpots; i++)
		{
			Transform child2 = child.GetChild(i);
			if (!child2)
			{
				continue;
			}
			for (int j = 0; j < child2.childCount; j++)
			{
				GameObject gameObject = child2.GetChild(j).gameObject;
				if (i > FlairsConfig.Count - 1 || FlairsConfig[i] != j)
				{
					gameObject.SetActive(value: false);
					continue;
				}
				gameObject.SetActive(value: true);
				int num = i + 1;
				DebugLog(DebugName + " equipped " + GetFlairNameByID(j) + " on spot " + num);
			}
		}
	}

	public static int GetFlairIDByName(string name)
	{
		for (int i = 0; i < FlairsIDs.Count; i++)
		{
			if (FlairsIDs[i] == name)
			{
				return i;
			}
		}
		return -1;
	}

	public static string GetFlairNameByID(int ID)
	{
		if (FlairsIDs[ID] != null)
		{
			return FlairsIDs[ID];
		}
		return "";
	}

	public static bool IsFlairForEveryone(int ID)
	{
		return IsFlairForEveryone(GetFlairNameByID(ID));
	}

	public static bool IsFlairForEveryone(string Name)
	{
		return EveryOneAvailableFlairs.Contains(Name);
	}

	public static SupporterBenefits VerifyBenefitsWithConfig(string Owner, SupporterBenefits Desired)
	{
		if (DeBug)
		{
			Log("Player " + Owner + " Desired following flairs:");
			for (int i = 0; i < Desired.m_Flairs.Count; i++)
			{
				Log("Slot" + i + " FlairID " + Desired.m_Flairs[i]);
			}
		}
		SupporterBenefits playerBenefits = GetPlayerBenefits(Owner);
		SupporterBenefits supporterBenefits = new SupporterBenefits();
		for (int j = 0; j < FlairSpots; j++)
		{
			supporterBenefits.m_Flairs.Add(-1);
		}
		if (playerBenefits.m_Knife && Desired.m_Knife)
		{
			supporterBenefits.m_Knife = true;
		}
		if (playerBenefits.m_BrightNick && Desired.m_BrightNick)
		{
			supporterBenefits.m_BrightNick = true;
		}
		if (playerBenefits.m_Canadium && Desired.m_Canadium)
		{
			supporterBenefits.m_Canadium = true;
		}
		DebugLog("Configurated flairs for this player:");
		for (int k = 0; k < Desired.m_Flairs.Count && k < FlairSpots; k++)
		{
			int num = Desired.m_Flairs[k];
			if (num != -1 && (IsFlairForEveryone(num) || playerBenefits.m_Flairs.Contains(num)))
			{
				supporterBenefits.m_Flairs[k] = num;
			}
			DebugLog("Slot" + k + " FlairID " + supporterBenefits.m_Flairs[k]);
		}
		return supporterBenefits;
	}

	public static void Log(string LOG)
	{
		MelonLogger.Msg(ConsoleColor.Blue, "[Supporters] " + LOG);
	}

	public static void DebugLog(string LOG)
	{
		if (DeBug)
		{
			MelonLogger.Msg(ConsoleColor.Blue, "[Supporters] " + LOG);
		}
	}

	public static void Error(string LOG)
	{
		MelonLogger.Msg(ConsoleColor.Red, "[Supporters] " + LOG);
	}

	public static bool IsLoaded()
	{
		return LoadedJSON != "";
	}

	public static int GetFlairsUpdateData()
	{
		if (!MPSaveManager.IsFileExist("FlairsUpdateData"))
		{
			return -1;
		}
		return int.Parse(MPSaveManager.LoadData("FlairsUpdateData"));
	}

	public static void SetFlairsUpdateData()
	{
		if (IsLoaded())
		{
			MPSaveManager.SaveData("FlairsUpdateData", AvailableBenefits.m_Flairs.Count.ToString());
		}
	}

	public static bool AnythingNew()
	{
		if (IsLoaded())
		{
			int flairsUpdateData = GetFlairsUpdateData();
			if (flairsUpdateData != -1 && flairsUpdateData < AvailableBenefits.m_Flairs.Count)
			{
				return true;
			}
			return false;
		}
		return false;
	}

	public static List<string> GetPlayerPerksList(string ID)
	{
		List<string> value = new List<string>();
		if (IsLoaded() && SupportersData.ContainsKey(ID))
		{
			SupportersData.TryGetValue(ID, out value);
		}
		return value;
	}

	public static SupporterBenefits GetPlayerBenefits(string ID)
	{
		return ConvertToBenefits(GetPlayerPerksList(ID));
	}

	public static SupporterBenefits ConvertToBenefits(List<string> Perks)
	{
		SupporterBenefits supporterBenefits = new SupporterBenefits();
		if (Perks.Contains("K"))
		{
			supporterBenefits.m_Knife = true;
		}
		if (Perks.Contains("B"))
		{
			supporterBenefits.m_BrightNick = true;
		}
		if (Perks.Contains("C"))
		{
			supporterBenefits.m_Canadium = true;
		}
		for (int i = 0; i < Perks.Count; i++)
		{
			int flairIDByName = GetFlairIDByName(Perks[i]);
			if (flairIDByName != -1)
			{
				supporterBenefits.m_Flairs.Add(flairIDByName);
			}
		}
		return supporterBenefits;
	}

	public static void PrintAll()
	{
		Log("Going to print everything...");
		foreach (KeyValuePair<string, List<string>> supportersDatum in SupportersData)
		{
			string text = "";
			for (int i = 0; i < supportersDatum.Value.Count; i++)
			{
				text = ((!(text == "")) ? (text + " " + supportersDatum.Value[i]) : supportersDatum.Value[i]);
			}
			Log("Key: " + supportersDatum.Key + " Perks: " + text);
		}
		foreach (string everyOneAvailableFlair in EveryOneAvailableFlairs)
		{
			Log("Everyone Available: " + everyOneAvailableFlair);
		}
	}

	public static void GotJsonCallback(object sender, DownloadStringCompletedEventArgs e)
	{
		if (e.Error != null)
		{
			return;
		}
		if (e.Result != "")
		{
			Log("Json data has been successfully received!");
			LoadedJSON = e.Result;
			DebugLog(LoadedJSON);
			JsonStruct jsonStruct = JSON.Load(LoadedJSON).Make<JsonStruct>();
			SupportersData = new Dictionary<string, List<string>>();
			EveryOneAvailableFlairs = new List<string>();
			if (jsonStruct != null)
			{
				foreach (Supporter supp in jsonStruct.Supps)
				{
					if (!string.IsNullOrEmpty(supp.S) && !SupportersData.ContainsKey(supp.S))
					{
						SupportersData.Add(supp.S, supp.P);
					}
					if (!string.IsNullOrEmpty(supp.E) && !SupportersData.ContainsKey(supp.E))
					{
						SupportersData.Add(supp.E, supp.P);
					}
				}
				EveryOneAvailableFlairs = jsonStruct.Every;
			}
			Log("Parsing to dictionary complete!");
			if (DeBug)
			{
				PrintAll();
			}
		}
		web.Dispose();
		web = null;
	}

	public static void GetSupportersList(bool Force = false)
	{
		if (!FlairsIDsReady)
		{
			FlairsIDsInit();
		}
		if (!Force && !IsLoaded())
		{
			Log("Trying to get supporters list...");
			if (web != null)
			{
				web.Dispose();
				web = null;
			}
			web = new WebClient();
			Uri address = new Uri("https://raw.githubusercontent.com/Filigrani/SkyCoop/main/Supporters.json");
			web.DownloadStringCompleted += GotJsonCallback;
			web.DownloadStringAsync(address);
		}
	}

	public static void LoadConfiguredBenfits()
	{
		string text = MPSaveManager.LoadData("ConfiguratedBenefits");
		if (text == "")
		{
			ConfiguratedBenefits = VerifyBenefitsWithConfig(MyID, AvailableBenefits);
		}
		else
		{
			SupporterBenefits desired = JSON.Load(text).Make<SupporterBenefits>();
			ConfiguratedBenefits = VerifyBenefitsWithConfig(MyID, desired);
		}
		if (DeBug)
		{
			Log("ConfiguratedBenefits.m_Knife " + ConfiguratedBenefits.m_Knife);
			Log("ConfiguratedBenefits.m_BrightNick " + ConfiguratedBenefits.m_BrightNick);
			Log("ConfiguratedBenefits.m_Flairs:");
			for (int i = 0; i < ConfiguratedBenefits.m_Flairs.Count; i++)
			{
				Log("[" + i + "] " + ConfiguratedBenefits.m_Flairs[i]);
			}
			Log("AvailableBenefits.m_Knife " + AvailableBenefits.m_Knife);
			Log("AvailableBenefits.m_BrightNick " + AvailableBenefits.m_BrightNick);
			Log("AvailableBenefits.m_Flairs:");
			for (int j = 0; j < AvailableBenefits.m_Flairs.Count; j++)
			{
				Log("[" + j + "] " + AvailableBenefits.m_Flairs[j]);
			}
		}
	}

	public static void EquipFlair(int Slot, int FlairID)
	{
		Log("Equip Flair " + FlairID + " To Slot " + Slot);
		Log("Flaist config " + ConfiguratedBenefits.m_Flairs.Count);
		ConfiguratedBenefits.m_Flairs[Slot - 1] = FlairID;
		MPSaveManager.SaveData("ConfiguratedBenefits", JSON.Dump(ConfiguratedBenefits));
		ApplyFlairsForModel(MyMod.MyPlayerDoll, ConfiguratedBenefits.m_Flairs, "I am");
	}

	public static void CheckForNewFlairs()
	{
		int flairsUpdateData = GetFlairsUpdateData();
		if (flairsUpdateData == AvailableBenefits.m_Flairs.Count)
		{
			Log("No any new flairs from previous visit");
		}
		else if (flairsUpdateData == -1)
		{
			Log("First visit, saving flairs count");
			SetFlairsUpdateData();
		}
		else if (flairsUpdateData < AvailableBenefits.m_Flairs.Count)
		{
			int num = AvailableBenefits.m_Flairs.Count - flairsUpdateData;
			Log("You got " + num + " new flairs!");
			MyMod.NotificationString = num.ToString();
		}
		else if (flairsUpdateData > AvailableBenefits.m_Flairs.Count)
		{
			SetFlairsUpdateData();
		}
	}

	public static void GetMyPerks()
	{
		if (IsLoaded() && MyID != "")
		{
			AvailableBenefits = GetPlayerBenefits(MyID);
			LoadConfiguredBenfits();
			if ((bool)MyMod.MyPlayerDoll)
			{
				ApplyFlairsForModel(MyMod.MyPlayerDoll, ConfiguratedBenefits.m_Flairs, "I am");
			}
			Log("My Supporter Bonuses loaded!");
			CheckForNewFlairs();
		}
		else
		{
			Error("Can't loade supporter bonuses because supporters list isn't loaded!");
		}
	}

	public static void SetID(string ID)
	{
		MyID = ID;
		GetMyPerks();
	}
}
