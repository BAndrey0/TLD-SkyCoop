using System.Diagnostics;
using GameServer;

namespace SkyCoop;

// Real time, independent of pause, sleep and accelerated world time.
public static class ConnectionWatchdog
{
	private static readonly Stopwatch Clock = Stopwatch.StartNew();
	private static bool monitoring;
	private static double lastHeartbeat;
	private static string host = "";
	private static bool wasLoading;
	private static double nextHostHeartbeat;
	public static void ReceivedHeartbeat() => ReceivedHeartbeat(Clock.Elapsed.TotalSeconds);
	internal static void ReceivedHeartbeat(double now) => lastHeartbeat = now;

	public static void Tick() => Tick(Clock.Elapsed.TotalSeconds);
	internal static void Tick(double now)
	{
		if (MyMod.iAmHost && now >= nextHostHeartbeat)
		{
			nextHostHeartbeat = now + 1;
			ServerSend.KEEPITALIVE(0, true);
		}
		bool guest = !MyMod.iAmHost && MyMod.sendMyPosition && ClientUser.myId > 0;
		if (!guest || CoopReturnToMenu.InProgress)
		{
			monitoring = false;
			wasLoading = false;
			return;
		}
		string currentHost = MyMod.ConnectedSteamWorks ? MyMod.SteamServerWorks : ClientUser.LastConnectedIp;
		if (!monitoring || host != currentHost)
		{
			monitoring = true;
			host = currentHost;
			ReceivedHeartbeat(now);
		}
		// A synchronous scene load can stall callbacks. Resume with a fresh grace period.
		if (MyMod.LoadingScreenIsOn || wasLoading)
		{
			ReceivedHeartbeat(now);
			wasLoading = MyMod.LoadingScreenIsOn;
			return;
		}
		if (now - lastHeartbeat >= 20)
		{
			monitoring = false;
			CoopReturnToMenu.ConnectionLost("Потеряна связь с хостом (20 секунд без ответа).");
		}
	}
}
