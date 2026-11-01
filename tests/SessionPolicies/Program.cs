using System;
using SkyCoop;

static class Program
{
 static int checks;
 static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
 static void Main()
 {
  MyMod.sendMyPosition = true;
  Check(CoopBalancePolicy.Cost(30, 10) == 300, "Long recipe must cost three bars, never cap at 100");
  Check(!CoopBalancePolicy.CanStart(100, 160, false), "Non-resumable work cannot start unaffordable");
  Check(CoopBalancePolicy.CanStart(25, 300, true), "Long recipes must allow partial work");
  Check(CoopBalancePolicy.WorkCharge(10, 9, 12, 0, 10) == 10, "Final work frame cannot overcharge beyond completion");
  Check(CoopBalancePolicy.WorkCharge(10, -1, 1, 0, 10) == 10, "Work cannot charge before start");
  Check(CoopBalancePolicy.AdvanceClock(23, 42, 1) == 42, "Sleep must wrap midnight without adding a minute");
  Check(CoopBalancePolicy.AdvanceClock(16, 25, 8) == 25, "Exactly midnight must be 00 not 24");
  Check(CoopBalancePolicy.AdvanceClock(16, 25, 26) == 18*60+25, "Long skip must wrap multiple days");
  Check(CoopBalancePolicy.RecoveryRate(false, true, .9f) < CoopBalancePolicy.RecoveryRate(false, true, 0), "Severe tiredness must slow work recovery");
  GameServer.ClientUser.myId = 1;
  ConnectionWatchdog.Tick(0);
  ConnectionWatchdog.Tick(19);
  Check(CoopReturnToMenu.Losses == 0, "Short interruption must not disconnect");
  ConnectionWatchdog.ReceivedHeartbeat(19);
  ConnectionWatchdog.Tick(38);
  Check(CoopReturnToMenu.Losses == 0, "Heartbeat must refresh timeout");
  ConnectionWatchdog.Tick(39);
  Check(CoopReturnToMenu.Losses == 1, "Silent host must eject guest at 20 seconds");
  MyMod.sendMyPosition = false;
  ConnectionWatchdog.Tick(40);
  MyMod.sendMyPosition = true;
  ConnectionWatchdog.Tick(100);
  ConnectionWatchdog.Tick(119);
  Check(CoopReturnToMenu.Losses == 1, "New session must start with a fresh timeout");
  MyMod.LoadingScreenIsOn = true;
  ConnectionWatchdog.Tick(120);
  MyMod.LoadingScreenIsOn = false;
  ConnectionWatchdog.Tick(400);
  ConnectionWatchdog.Tick(419);
  Check(CoopReturnToMenu.Losses == 1, "Blocked scene load must not look like a lost host");
  ConnectionWatchdog.Tick(420);
  Check(CoopReturnToMenu.Losses == 2, "Timeout must resume after scene load");
  MyMod.iAmHost = true;
  ConnectionWatchdog.Tick(500);
  ConnectionWatchdog.Tick(500.5);
  ConnectionWatchdog.Tick(501);
  Check(GameServer.ServerSend.Beats == 2, "Host heartbeat must follow real time even if world time is paused");
  Check(CoopReturnToMenu.Losses == 2, "Host must never disconnect itself");
  Check(WildlifeSpawnAuthorityPolicy.AcceptGrant(true, "LakeRegion", "LakeRegion"), "Guest can own its own scene");
  Check(WildlifeSpawnAuthorityPolicy.Ready(true,false,false,false,0,"RavineTransitionZone"), "Explicitly loaded guest must not wait for a host scene tick");
  Check(!WildlifeSpawnAuthorityPolicy.Ready(true,true,false,false,100,"RavineTransitionZone"), "Host tick history must never override guest loading");
  Check(!WildlifeSpawnAuthorityPolicy.AcceptGrant(true, "LakeRegion", "RavineTransitionZone"), "Stale authority grant must be rejected");
  Check(!WildlifeSpawnAuthorityPolicy.AcceptGrant(false, "LakeRegion", "LakeRegion"), "Loading guest must not accept authority");
  Check(!WildlifeSpawnAuthorityPolicy.WorldScene("MainMenu"), "Menu must never spawn wildlife");
  Check(WildlifeSpawnAuthorityPolicy.LocalReady(false, false, "LakeRegion"), "Guest readiness must not depend on the host-only tick counter");
  Check(!WildlifeOwnershipPolicy.Eligible(true, false, false, false, 1, "LakeRegion", 1, "RavineTransitionZone"), "Other scene must not steal animal ownership");
  Console.WriteLine($"PASS: {checks} session and wildlife checks");
 }
}
namespace SkyCoop
{
 static class MyMod
 {
  public static bool iAmHost, sendMyPosition, ConnectedSteamWorks, LoadingScreenIsOn;
  public static string SteamServerWorks = "host";
 }
 static class CoopReturnToMenu
 {
  public static bool InProgress;
  public static int Losses;
  public static void ConnectionLost(string reason) { Losses++; }
 }
}
namespace GameServer
{
 static class ClientUser { public static int myId; public static string LastConnectedIp = "host"; }
 static class ServerSend { public static int Beats; public static void KEEPITALIVE(int from, bool message) => Beats++; }
}
