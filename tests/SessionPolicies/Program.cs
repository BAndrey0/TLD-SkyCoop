using System;
using SkyCoop;

static class Program
{
 static int checks;
 static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
 static void Main()
 {
  MyMod.sendMyPosition = true;
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
