using System;
using SkyCoop;

static class Program
{
 static int checks;
 static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
 static void Main()
 {
  MyMod.sendMyPosition = true;
  const string previousWorld = "a1111111111111111111111111111111";
  const string newWorld = "b2222222222222222222222222222222";
  Check(WorldSaveMatch.CanResume(previousWorld, -42, previousWorld, "Renamed"), "The same world must resume after the guest renames its slot");
  Check(!WorldSaveMatch.CanResume(newWorld, -42, previousWorld, "-42"), "Different worlds with the same seed must use separate guest slots");
  Check(!WorldSaveMatch.CanResume(newWorld, -42, "", "-42"), "An identified world must reject an untagged save with the same seed");
  Check(!WorldSaveMatch.CanResume("", -42, previousWorld, "-42"), "A legacy host must not claim a slot bound to an identified world");
  Check(WorldSaveMatch.CanResume("", -42, "", "-42"), "Legacy saves must remain resumable on a legacy world");
  Check(!WorldSaveMatch.CanResume("", 0, "", "0"), "An unspecified legacy seed must start a fresh guest slot");
  double fireDeadline=FireClockPolicy.SecondsAt(100)+3600;
  Check(FireClockPolicy.Remaining(fireDeadline,105)==3300,"One hour of book work must consume only five shared minutes of fire");
  Check(FireClockPolicy.Remaining(fireDeadline,105)==3300,"Reapplying an unchanged fire deadline must not refill it");
  fireDeadline=FireClockPolicy.ChangeDeadline(fireDeadline,105,600,43200);
  fireDeadline=FireClockPolicy.ChangeDeadline(fireDeadline,105,1200,43200);
  Check(FireClockPolicy.Remaining(fireDeadline,105)==5100,"Fuel contributions from both players must accumulate exactly once");
  fireDeadline=FireClockPolicy.ChangeDeadline(fireDeadline,105,-600,43200);
  Check(FireClockPolicy.Remaining(fireDeadline,105)==4500,"Taking a torch must reduce the shared remaining fuel");
  Check(FireClockPolicy.Remaining(fireDeadline,225)==0,"A shared two-hour sleep must expire a shorter fire");
  Check(FireClockPolicy.Remaining(FireClockPolicy.SecondsAt(1430)+3600,1450)==2400,"Burning across midnight must use elapsed server time");
  Check(FireClockPolicy.Remaining(FireClockPolicy.ChangeDeadline(fireDeadline,105,999999,43200),105)==43200,"Large fuel additions must respect the fire capacity");
  Check(FireClockPolicy.Remaining(double.NaN,105)==0,"Invalid fire deadlines must not produce NaN UI or immortal fire");
  Check(FireClockPolicy.Remaining(FireClockPolicy.ChangeDeadline(fireDeadline,105,float.NaN,43200),105)==4500,"Invalid fuel delta must not corrupt an existing fire");
  using(var bytes=new System.IO.MemoryStream())
  {
   using(var writer=new System.IO.BinaryWriter(bytes,System.Text.Encoding.UTF8,true))
   {writer.Write(3);foreach(string key in new[]{"GEAR_Rifle","Assets/Gear/GEAR_Water.prefab","Assets/Gear/GEAR_Rifle.fbx"}){var encoded=System.Text.Encoding.ASCII.GetBytes(key);writer.Write((byte)0);writer.Write(encoded.Length);writer.Write(encoded);}}
   var names=new System.Collections.Generic.HashSet<string>(GearCatalogPolicy.ReadKeyNames(bytes.ToArray()));
   Check(names.SetEquals(new[]{"GEAR_Rifle","GEAR_Water"}),"Serialized aliases must retain gear and reject meshes");
   Check(new System.Collections.Generic.List<string>(GearCatalogPolicy.ReadKeyNames(new byte[]{0,0,0,0,0,255,255,255,127})).Count==0,"Truncated key records must not read outside the buffer");
  }
  string catalog=Environment.GetEnvironmentVariable("SKYCOOP_CATALOG_FILE");
  if(!string.IsNullOrEmpty(catalog)) {var names=new System.Collections.Generic.HashSet<string>(GearCatalogPolicy.ReadCatalog(catalog));Check(names.Count>=441&&names.Contains("GEAR_Rifle"),"Installed catalog must retain prefab inventory and key aliases");Console.WriteLine("Catalog metadata: "+names.Count+" gear IDs");}
  Check(GearCatalogPolicy.GearName("Assets/Gear/GEAR_Rifle.prefab")=="GEAR_Rifle","Catalog prefab paths must resolve to item IDs");
  Check(GearCatalogPolicy.GearName("GEAR_Rifle")=="GEAR_Rifle","Direct catalog item address must be retained");
  Check(GearCatalogPolicy.GearName("Assets/GEAR_Rifle.fbx")==null,"Meshes must not be listed as inventory items");
  Check(GearCatalogPolicy.GearName("OBJ_Chair.prefab")==null,"World-only scenery is not an inventory item");
  Check(GearCatalogPolicy.Section("GEAR_RifleAmmo")=="AmmoItem","Ammunition must not disappear into the weapon group");
  Check(GearCatalogPolicy.Section("GEAR_DecorationShelf")=="Decorations","Safehouse decorations have a visible category");
  Check(GearCatalogPolicy.Section("GEAR_NewDlcUnknown")=="Other","Unknown DLC items must still be listed");
  Check(GearCatalogPolicy.Sections.Length==15,"Fixed sections must fit the window, unlike all technical components");
  Check(Math.Abs(ModelProportions.Scale(1.8f,1.5f)-1.2f)<.001,"A shorter costume must be enlarged against the original rig");
  Check(ModelProportions.Scale(1.8f,1.8f)==1,"Matching proportions must not be changed");
  Check(ModelProportions.Scale(float.NaN,1.8f)==1,"Invalid scale measurements must not poison model transforms");
  Check(ModelProportions.Scale(100,.2f)==1.4f,"Bad assets must not create giant players");
  Check(VitalNumbers.Clamp(float.NaN,42)==42,"NaN must recover the last known fatigue, not fill or empty the bar");
  Check(VitalNumbers.Clamp(float.PositiveInfinity,37)==37,"Infinity must not enter gameplay stats");
  Check(VitalNumbers.Clamp(23,60)==23,"Healthy fatigue must be unchanged");
  Check(VitalNumbers.Fraction(float.NaN,100,60)==.6f,"Invalid sleep percentage must remain finite");
  Check(VitalNumbers.Rate(float.NaN,5)==5,"Invalid native burn rate must not poison AddFatigue");
  Check(Math.Abs(VitalNumbers.Clamp(float.NaN,float.NaN,float.NaN)-60)<.001f,"All-invalid saved stats need a finite recovery point");
  var budget=new AttemptBudget();
  Check(budget.Allow(1,0,0),"First spawn attempt allowed");
  Check(!budget.Allow(1,0,.01),"Failed radial spot must not be retried on every frame");
  Check(budget.Allow(1,0,.5),"Radial spawning retries after a bounded cooldown");
  Check(budget.Allow(1,1,.51),"Progress immediately unlocks the next deterministic spawn");
  Check(budget.Allow(2,0,.51),"Independent spawners must not block each other");
  var lid=new LidTransition();
  Check(lid.Start("open",false),"First opening plays");
  Check(!lid.Start("open",false),"Second player opening an open lid must not replay its relative tween");
  Check(!lid.Start("close",true)&&lid.Queued=="close","Close during opening waits for the current tween");
  Check(lid.Complete()=="close"&&lid.Start("close",false),"Deferred close plays exactly once");
  Check(!lid.Start("close",false),"Repeated close must not rotate through the closed pose");
  for(int i=0;i<50;i++){if(!lid.Start("open",false)||lid.Start("open",false)||!lid.Start("close",false)||lid.Start("close",false))throw new Exception("Lid cycle drift");}
  Check(MotionTiming.Newer(11,10),"Fresh motion snapshot accepted");
  Check(!MotionTiming.Newer(9,10),"Reordered snapshot must not rewind a player");
  Check(!MotionTiming.Newer(10,10),"Duplicate snapshot rejected");
  Check(MotionTiming.Newer(int.MinValue,int.MaxValue),"Sequence wrap must not freeze movement");
  Check(Math.Abs(MotionTiming.Factor(1.1,1,1.05)-.5f)<.001,"75ms interpolation buffer");
  Check(MotionTiming.Factor(20,1,1.05)<=3.001f,"Prediction must stop after 100ms, not run on through a disconnect");
  Check(MotionTiming.Factor(2,1,2)==1,"Long reception gap must snap without extreme extrapolation");
  var hostWire=new byte[12];BitConverter.GetBytes(194).CopyTo(hostWire,4);
  Check(MotionWire.IsMotion(hostWire,true),"Host motion uses the independent unreliable channel");
  BitConverter.GetBytes(193).CopyTo(hostWire,4);
  Check(!MotionWire.IsMotion(hostWire,true),"Character choice remains reliable");
  var guestWire=new byte[24];BitConverter.GetBytes(4).CopyTo(guestWire,4);BitConverter.GetBytes(196).CopyTo(guestWire,16);
  Check(MotionWire.IsMotion(guestWire,false),"Guest motion header includes client and GUID");
  BitConverter.GetBytes(int.MaxValue).CopyTo(guestWire,4);
  Check(!MotionWire.IsMotion(guestWire,false),"Malformed header cannot select a traffic channel");
	var travel=new FatigueTravelLedger();
	Check(travel.Enter("Coast",1)==null,"First load must use the saved character fatigue");
	travel.Remember(42);
	travel.Remember(float.NaN);travel.Remember(float.PositiveInfinity);
	Check(travel.Last==42,"Invalid travel snapshots must not replace the last finite fatigue");
	travel.Loading();
	Check(travel.Enter("House",2)==42,"Interior load must keep 42, not the native default zero/max");
	travel.Remember(43);
	travel.Loading();
	Check(travel.Enter("Coast",3)==43,"Returning outside must preserve accumulated fatigue");
	Check(travel.Enter("Coast",3)==null,"Normal update must not undo sleep or other fatigue changes");
	travel.Remember(5);travel.Loading();
	Check(travel.Enter("Coast",3)==5,"A reload with the same scene and reused component must keep the latest value");
	travel.Remember(6);
	Check(travel.Enter("House",3)==6,"Scene change must be detected even when Unity reuses the native pointer");
	travel.Reset();
	Check(travel.Enter("House",3)==null,"Respawn or a new session must not inherit a previous character fatigue");
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
  Check(!WildlifeOwnershipPolicy.Eligible(true, false, false, false, "LakeRegion", "RavineTransitionZone"), "Other scene must not steal animal ownership");
  Check(WildlifeOwnershipPolicy.Eligible(true, false, false, false, "LakeRegion", "LakeRegion"), "Loaded peers share wildlife by save-scene key");
  Check(WildlifeOwnershipPolicy.Closer(8, 14) && !WildlifeOwnershipPolicy.Closer(10, 14), "Handoff uses distance from the animal with hysteresis");
  Check(!WildlifeOwnershipPolicy.AcceptSnapshot(1, 0, true), "The previous owner cannot undo an active handoff");
  Check(WildlifeOwnershipPolicy.AcceptSnapshot(1, 1, true) && WildlifeOwnershipPolicy.AcceptSnapshot(1, 0, false), "Current owner and recovery after silence can update replicas");
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
