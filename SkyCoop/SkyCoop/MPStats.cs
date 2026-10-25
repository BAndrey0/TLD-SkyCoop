using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameServer;
using MelonLoader;
using TinyJSON;

namespace SkyCoop;

public class MPStats
{
	public class PlayTime
	{
		public int Seconds;

		public int Minutes;

		public int Hours;

		public int Days;

		public void AddSecond()
		{
			Seconds++;
			if (Seconds >= 60)
			{
				Seconds = 0;
				AddMinute();
			}
		}

		public void AddMinute()
		{
			Minutes++;
			if (Minutes >= 60)
			{
				Minutes = 0;
				AddHour();
			}
		}

		public void AddHour()
		{
			Hours++;
			if (Hours >= 24)
			{
				Hours = 0;
				AddDay();
			}
		}

		public void AddDay()
		{
			Days++;
		}

		public string GetString()
		{
			string text = "";
			text = ((Seconds <= 1) ? "1 Second" : (Seconds + " Seconds"));
			if (Minutes > 0)
			{
				text = ((Minutes != 1) ? (Minutes + " Minutes " + text) : ("1 Minute " + text));
			}
			if (Hours > 0)
			{
				text = ((Hours != 1) ? (Hours + " Hours " + text) : ("1 Hour " + text));
			}
			if (Days > 0)
			{
				text = ((Days != 1) ? (Days + " Days " + text) : ("1 Day " + text));
			}
			return text;
		}
	}

	public class ResourcesStatistic
	{
		public int GearsPicked;

		public int ContainersLooted;

		public int AnimalsKilled;

		public int PlantsHarvested;
	}

	public class PlayerStatistic
	{
		public string Name = "";

		public string MAC = "";

		public int Visits = 1;

		public int Deaths;

		public int ExpeditionsCompleted;

		public Dictionary<string, bool> CompletedExpeditions = new Dictionary<string, bool>();

		public int CrashSitesFound;

		public PlayTime TotalPlayTime = new PlayTime();

		public Dictionary<int, PlayTime> RegionsHistory = new Dictionary<int, PlayTime>();

		public ResourcesStatistic Looted = new ResourcesStatistic();

		public PlayerStatistic(string mac = "", string name = "")
		{
			MAC = mac;
			Name = name;
		}

		public PlayerStatistic()
		{
		}

		public string GetString(bool HideMAC = true, bool HideRegionPlayTime = true)
		{
			string text = "Name " + Name;
			if (!HideMAC)
			{
				text = text + "\nMAC " + MAC;
			}
			text = text + "\nTotal Play Time " + TotalPlayTime.GetString() + "\nServer Visits " + Visits + "\nDeaths " + Deaths + "\nPicked Gears " + Looted.GearsPicked + "\nLooted Containers " + Looted.ContainersLooted + "\nPlants Harvested " + Looted.PlantsHarvested + "\nAnimals Killed " + Looted.AnimalsKilled + "\nExpeditions Completed " + ExpeditionsCompleted + "\nCrash Sites Found " + CrashSitesFound;
			if (!HideRegionPlayTime && RegionsHistory.Count > 0)
			{
				text += "\nRegions Play Time:";
				foreach (KeyValuePair<int, PlayTime> item in RegionsHistory)
				{
					text = text + "\n" + GetRegionName(item.Key) + ": " + item.Value.GetString();
				}
			}
			return text;
		}
	}

	public class ExpeditionsProgressData
	{
		public Dictionary<int, int> ExpeditionsProgress = new Dictionary<int, int>();

		public int TotalProgress;
	}

	public class Day
	{
		public int Visits;

		public int UniqueVisits;

		public int ExpeditionsCompleted;

		public int CrashSitesFound;

		public Dictionary<string, int> VisitsHistory = new Dictionary<string, int>();

		public PlayTime OnlineTime = new PlayTime();

		public PlayTime EmptyTime = new PlayTime();

		public Dictionary<string, Dictionary<string, int>> ActivitySnapshots = new Dictionary<string, Dictionary<string, int>>();

		public int Deaths;

		public ResourcesStatistic Looted = new ResourcesStatistic();

		public Dictionary<string, PlayerStatistic> Players = new Dictionary<string, PlayerStatistic>();

		public Dictionary<int, PlayTime> RegionsHistory = new Dictionary<int, PlayTime>();

		public string GetString(bool IncludePlayersStats = false, bool HideMAC = true, bool HideRegionPlayTime = true)
		{
			string text = "Visits " + Visits + "\nUnique Visits " + UniqueVisits + "\nServer Works " + OnlineTime.GetString() + "\nEmpty Time " + EmptyTime.GetString() + "\nPlayers Died " + Deaths + "\nPicked Gears " + Looted.GearsPicked + "\nLooted Containers " + Looted.ContainersLooted + "\nPlants Harvested " + Looted.PlantsHarvested + "\nAnimals Killed " + Looted.AnimalsKilled + "\nExpeditions Completed " + ExpeditionsCompleted + "\nCrash Sites Found " + CrashSitesFound;
			if (!HideRegionPlayTime && RegionsHistory.Count > 0)
			{
				text += "\nRegions Play Time:";
				foreach (KeyValuePair<int, PlayTime> item in RegionsHistory)
				{
					text = text + "\n" + GetRegionName(item.Key) + ": " + item.Value.GetString();
				}
			}
			if (IncludePlayersStats && Players.Count > 0)
			{
				text += "\nToday Statistic Of Players:";
				foreach (KeyValuePair<string, PlayerStatistic> player in Players)
				{
					text = text + "\n" + player.Value.GetString(HideMAC, HideRegionPlayTime);
				}
			}
			return text;
		}
	}

	public static bool Started = false;

	private static bool WarnedMissingPickupIdentity;

	private static bool WarnedMissingPlayerIdentity;

	public static Day TodayStats = new Day();

	public static Day AllTimeStats = new Day();

	public static int DataDay = 0;

	public static Dictionary<string, PlayerStatistic> RecentPlayersGlobalStatistic = new Dictionary<string, PlayerStatistic>();

	public static int SaveRecentTimer = 0;

	public static int SaveRecentPerioud = 300;

	public static void Log(string TXT, Shared.LoggerColor Color = Shared.LoggerColor.White)
	{
		MelonLogger.Msg(MyMod.ConvertLoggerColor(Color), TXT);
	}

	public static void Start()
	{
		WarnedMissingPickupIdentity = false;
		WarnedMissingPlayerIdentity = false;
		Started = true;
		AllTimeStats = LoadDayStats("AllTime");
		StartDay();
	}

	public static void StartDay(bool SendWebhook = false)
	{
		TodayStats.GetString();
		DateTime now = DateTime.Now;
		DataDay = now.Day;
		TodayStats = LoadDayStats(now.Day + "_" + now.Month + "_" + now.Year);
		Log("[MPStats] Starting statistic for " + now.Day + "." + now.Month + "." + now.Year, Shared.LoggerColor.Blue);
	}

	public static void SaveRecentStuff()
	{
		int seed = MPSaveManager.GetSeed();
		MPSaveManager.ValidateRootExits();
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetPathForName("Statistic", MPSaveManager.GetSeed()));
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetPathForName("Statistic" + MPSaveManager.GetSeparator() + "Global", MPSaveManager.GetSeed()));
		foreach (KeyValuePair<string, PlayerStatistic> item in RecentPlayersGlobalStatistic)
		{
			if (!string.IsNullOrEmpty(item.Value.MAC))
			{
				MPSaveManager.SaveData(item.Value.MAC, JSON.Dump(item.Value), seed, "", "Statistic" + MPSaveManager.GetSeparator() + "Global" + MPSaveManager.GetSeparator() + PlayerStatsFileKey(item.Value.MAC));
			}
		}
		RecentPlayersGlobalStatistic.Clear();
		MPSaveManager.SaveData("AllTime", JSON.Dump(AllTimeStats), seed, "", "Statistic" + MPSaveManager.GetSeparator() + "AllTime");
		DateTime now = DateTime.Now;
		if (now.Day == DataDay)
		{
			string text = now.Day + "_" + now.Month + "_" + now.Year;
			MPSaveManager.SaveData(text, JSON.Dump(TodayStats), seed, "", "Statistic" + MPSaveManager.GetSeparator() + text);
		}
	}

	public static string PlayerStatsFileKey(string identity)
	{
		if (string.IsNullOrEmpty(identity))
		{
			throw new ArgumentException("Player identity is empty", "identity");
		}
		return string.Concat(identity.Select(delegate(char c)
		{
			if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '-' && c != '_')
			{
				int num = c;
				return "%" + num.ToString("X4", CultureInfo.InvariantCulture);
			}
			return c.ToString();
		}));
	}

	public static PlayerStatistic LoadPlayerGlobalStats(string MAC, string Name)
	{
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetPathForName("Statistic", MPSaveManager.GetSeed()));
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetPathForName("Statistic" + MPSaveManager.GetSeparator() + "Global", MPSaveManager.GetSeed()));
		string text = MPSaveManager.LoadData("Statistic" + MPSaveManager.GetSeparator() + "Global" + MPSaveManager.GetSeparator() + PlayerStatsFileKey(MAC), MPSaveManager.GetSeed());
		if (string.IsNullOrEmpty(text))
		{
			return new PlayerStatistic(MAC, Name);
		}
		return JSON.Load(text).Make<PlayerStatistic>();
	}

	public static Day LoadDayStats(string TodayFileName)
	{
		MPSaveManager.CreateFolderIfNotExist(MPSaveManager.GetPathForName("Statistic", MPSaveManager.GetSeed()));
		string text = MPSaveManager.LoadData("Statistic" + MPSaveManager.GetSeparator() + TodayFileName, MPSaveManager.GetSeed());
		if (string.IsNullOrEmpty(text))
		{
			return new Day();
		}
		return JSON.Load(text).Make<Day>();
	}

	public static string GetRegionName(int Region)
	{
		return ExpeditionBuilder.GetRegionString(Region);
	}

	public static void AddPlayer(string mac, string name = "")
	{
		if (string.IsNullOrWhiteSpace(mac))
		{
			if (!WarnedMissingPlayerIdentity)
			{
				WarnedMissingPlayerIdentity = true;
				Log("[MPStats] Join statistics skipped: player identity unavailable; server initialization continues.", Shared.LoggerColor.Yellow);
			}
			return;
		}
		if (!TodayStats.Players.ContainsKey(mac))
		{
			TodayStats.Players.Add(mac, new PlayerStatistic(mac, name));
		}
		else
		{
			TodayStats.Players[mac].Visits++;
		}
		if (!TodayStats.VisitsHistory.ContainsKey(mac))
		{
			TodayStats.VisitsHistory.Add(mac, 1);
			TodayStats.UniqueVisits++;
		}
		else
		{
			TodayStats.VisitsHistory[mac] = TodayStats.VisitsHistory[mac]++;
		}
		if (!AllTimeStats.VisitsHistory.ContainsKey(mac))
		{
			AllTimeStats.VisitsHistory.Add(mac, 1);
			AllTimeStats.UniqueVisits++;
		}
		else
		{
			AllTimeStats.VisitsHistory[mac] = AllTimeStats.VisitsHistory[mac]++;
		}
		TodayStats.Visits++;
		AllTimeStats.Visits++;
		GetPlayerGlobalStats(mac, name);
		if (RecentPlayersGlobalStatistic.ContainsKey(mac))
		{
			RecentPlayersGlobalStatistic[mac].Visits++;
			RecentPlayersGlobalStatistic[mac].Name = name;
		}
	}

	public static PlayerStatistic GetPlayerGlobalStats(string MAC, string Name = "")
	{
		if (RecentPlayersGlobalStatistic.TryGetValue(MAC, out var value))
		{
			return value;
		}
		value = LoadPlayerGlobalStats(MAC, Name);
		RecentPlayersGlobalStatistic.Add(MAC, value);
		return value;
	}

	public static void AddPickedGear(string MAC)
	{
		if (string.IsNullOrWhiteSpace(MAC))
		{
			if (!WarnedMissingPickupIdentity)
			{
				WarnedMissingPickupIdentity = true;
				Log("[MPStats] Pickup statistics skipped: player identity unavailable; gameplay pickup synchronization continues.", Shared.LoggerColor.Yellow);
			}
			return;
		}
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			value.Looted.GearsPicked++;
			TodayStats.Looted.GearsPicked++;
			TodayStats.Players[MAC] = value;
		}
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			RecentPlayersGlobalStatistic[MAC].Looted.GearsPicked++;
		}
		AllTimeStats.Looted.GearsPicked++;
	}

	public static void AddLootedContainer(string MAC)
	{
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			value.Looted.ContainersLooted++;
			TodayStats.Looted.ContainersLooted++;
			TodayStats.Players[MAC] = value;
		}
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			RecentPlayersGlobalStatistic[MAC].Looted.ContainersLooted++;
		}
		AllTimeStats.Looted.ContainersLooted++;
	}

	public static void AddPlantHarvested(string MAC)
	{
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			value.Looted.PlantsHarvested++;
			TodayStats.Looted.PlantsHarvested++;
			TodayStats.Players[MAC] = value;
		}
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			RecentPlayersGlobalStatistic[MAC].Looted.PlantsHarvested++;
		}
		AllTimeStats.Looted.PlantsHarvested++;
	}

	public static void AddDeath(string MAC)
	{
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			value.Deaths++;
			TodayStats.Players[MAC] = value;
		}
		TodayStats.Deaths++;
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			RecentPlayersGlobalStatistic[MAC].Deaths++;
		}
		AllTimeStats.Deaths++;
	}

	public static ExpeditionsProgressData GetExpeditionsProgress(string MAC)
	{
		PlayerStatistic playerGlobalStats = GetPlayerGlobalStats(MAC);
		ExpeditionsProgressData expeditionsProgressData = new ExpeditionsProgressData();
		if (playerGlobalStats != null)
		{
			int num = 0;
			int num2 = 0;
			for (int i = -Shared.GameRegionNegativeOffset; i <= Shared.GameRegionPositiveOffset; i++)
			{
				ExpeditionBuilder.GetRegionString(i);
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				if (!ExpeditionBuilder.m_ExpeditionTasks.TryGetValue(i, out var value))
				{
					continue;
				}
				foreach (ExpeditionBuilder.ExpeditionTaskTemplate item in value)
				{
					if (item.m_CanBeTaken)
					{
						bool num6 = playerGlobalStats.CompletedExpeditions.ContainsKey(item.m_Alias);
						num++;
						num3++;
						if (num6)
						{
							num4++;
							num2++;
						}
					}
				}
				num5 = (int)((double)num4 / (double)num3 * 100.0);
				expeditionsProgressData.ExpeditionsProgress.Add(i, num5);
			}
			expeditionsProgressData.TotalProgress = (int)((double)num2 / (double)num * 100.0);
		}
		return expeditionsProgressData;
	}

	public static void AddExpedition(string MAC, string Alias)
	{
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			value.ExpeditionsCompleted++;
			if (!value.CompletedExpeditions.ContainsKey(Alias))
			{
				value.CompletedExpeditions.Add(Alias, value: true);
			}
			TodayStats.Players[MAC] = value;
		}
		TodayStats.ExpeditionsCompleted++;
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			RecentPlayersGlobalStatistic[MAC].ExpeditionsCompleted++;
			if (!RecentPlayersGlobalStatistic[MAC].CompletedExpeditions.ContainsKey(Alias))
			{
				RecentPlayersGlobalStatistic[MAC].CompletedExpeditions.Add(Alias, value: true);
			}
		}
		AllTimeStats.ExpeditionsCompleted++;
	}

	public static void AddCrashSite(string MAC)
	{
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			value.ExpeditionsCompleted++;
			TodayStats.Players[MAC] = value;
		}
		TodayStats.CrashSitesFound++;
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			RecentPlayersGlobalStatistic[MAC].CrashSitesFound++;
		}
		AllTimeStats.CrashSitesFound++;
	}

	public static void AddRegionTime(string MAC, int Region)
	{
		if (Region == 6)
		{
			return;
		}
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			if (value.RegionsHistory.TryGetValue(Region, out var value2))
			{
				value2.AddSecond();
				value.RegionsHistory[Region] = value2;
			}
			else
			{
				value.RegionsHistory.Add(Region, new PlayTime());
			}
			value.TotalPlayTime.AddSecond();
			TodayStats.Players[MAC] = value;
		}
		if (TodayStats.RegionsHistory.TryGetValue(Region, out var value3))
		{
			value3.AddSecond();
			TodayStats.RegionsHistory[Region] = value3;
		}
		else
		{
			TodayStats.RegionsHistory.Add(Region, new PlayTime());
		}
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			PlayerStatistic playerStatistic = RecentPlayersGlobalStatistic[MAC];
			if (playerStatistic.RegionsHistory.TryGetValue(Region, out var value4))
			{
				value4.AddSecond();
				playerStatistic.RegionsHistory[Region] = value4;
			}
			else
			{
				playerStatistic.RegionsHistory.Add(Region, new PlayTime());
			}
			playerStatistic.TotalPlayTime.AddSecond();
			RecentPlayersGlobalStatistic[MAC] = playerStatistic;
		}
		if (AllTimeStats.RegionsHistory.TryGetValue(Region, out var value5))
		{
			value5.AddSecond();
			AllTimeStats.RegionsHistory[Region] = value5;
		}
		else
		{
			AllTimeStats.RegionsHistory.Add(Region, new PlayTime());
		}
	}

	public static void SetName(string MAC, string Name)
	{
		if (TodayStats.Players.TryGetValue(MAC, out var value))
		{
			value.Name = Name;
			TodayStats.Players[MAC] = value;
		}
		GetPlayerGlobalStats(MAC);
		if (RecentPlayersGlobalStatistic.ContainsKey(MAC))
		{
			RecentPlayersGlobalStatistic[MAC].Name = Name;
		}
	}

	public static Dictionary<string, int> GetGraphDummy()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		int num = 11;
		int num2 = 0;
		string text = num + ":" + num2;
		Random random = new Random();
		while (num != 0)
		{
			if (num2 == 0)
			{
				num2 = 30;
			}
			else
			{
				num2 = 0;
				num++;
			}
			if (num > 24)
			{
				num = 0;
			}
			text = num + ":" + num2;
			int value = random.Next(0, Server.MaxPlayers);
			if (!dictionary.ContainsKey(text))
			{
				dictionary.Add(text, value);
			}
		}
		return dictionary;
	}

	public static void EverySecond()
	{
		if (!Started)
		{
			return;
		}
		if (DataDay != 0)
		{
			DateTime now = DateTime.Now;
			if (DataDay != now.Day)
			{
				SaveRecentStuff();
				StartDay(SendWebhook: true);
			}
			else if (now.Minute == 0 || now.Minute == 30)
			{
				string key = now.Hour + ":" + now.Minute;
				string key2 = now.Day + "." + now.Month + "." + now.Year;
				bool flag = false;
				if (TodayStats.ActivitySnapshots.ContainsKey(key2))
				{
					if (!TodayStats.ActivitySnapshots[key2].ContainsKey(key))
					{
						TodayStats.ActivitySnapshots[key2].Add(key, MyMod.PlayersOnServer);
						flag = true;
					}
				}
				else
				{
					TodayStats.ActivitySnapshots.Add(key2, new Dictionary<string, int>());
					TodayStats.ActivitySnapshots[key2].Add(key, MyMod.PlayersOnServer);
					flag = true;
				}
				if (AllTimeStats.ActivitySnapshots.ContainsKey(key2))
				{
					if (!AllTimeStats.ActivitySnapshots[key2].ContainsKey(key))
					{
						AllTimeStats.ActivitySnapshots[key2].Add(key, MyMod.PlayersOnServer);
						flag = true;
					}
				}
				else
				{
					AllTimeStats.ActivitySnapshots.Add(key2, new Dictionary<string, int>());
					AllTimeStats.ActivitySnapshots[key2].Add(key, MyMod.PlayersOnServer);
					flag = true;
				}
				if (flag)
				{
					SaveRecentStuff();
				}
			}
		}
		foreach (KeyValuePair<int, Client> client in Server.clients)
		{
			Client value = client.Value;
			if (value.IsBusy() && !value.RCON && !string.IsNullOrEmpty(value.SubNetworkGUID) && !Shared.ClientIsLoading(value.id) && MyMod.playersData[value.id] != null)
			{
				DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[value.id];
				AddRegionTime(value.SubNetworkGUID, multiPlayerClientData.m_LastRegion);
			}
		}
		AddRegionTime(Server.GetMACByID(0), (int)MyMod.GetCurrentRegion());
		if (MyMod.PlayersOnServer == 0)
		{
			TodayStats.EmptyTime.AddSecond();
			AllTimeStats.EmptyTime.AddSecond();
		}
		TodayStats.OnlineTime.AddSecond();
		AllTimeStats.OnlineTime.AddSecond();
		if (SaveRecentTimer < SaveRecentPerioud)
		{
			SaveRecentTimer++;
			if (SaveRecentTimer >= SaveRecentPerioud)
			{
				SaveRecentTimer = 0;
				SaveRecentStuff();
			}
		}
	}
}
