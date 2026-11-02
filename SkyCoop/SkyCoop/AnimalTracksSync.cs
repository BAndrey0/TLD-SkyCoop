using System;
using System.Collections.Generic;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
namespace SkyCoop;

// Replicate actual snow imprints. Remote actors have no BaseAi, so relying on
// its native animation-event callbacks does not produce tracks on spectators.
public static class AnimalTracksSync
{
 private struct Step {public int trail,type,variant;public Vector3 owner,position,normal;public float yaw,depth;public bool flip;}
 private static readonly Queue<Step> queue=new();
 private static readonly Dictionary<(int,int),FootstepTrail> trails=new();
 private static bool applying;private static string scene;private static float nextSend;
 [HarmonyPatch(typeof(FootstepTrail),"AddFootstep")]
 private static class Capture
 {
  private static bool Prefix(FootstepTrail __instance)=>applying||!MyMod.InOnline()||__instance.m_TrailType==FootstepTrailType.Player||MyMod.CanSpawnWildlifeInCurrentScene();
  private static void Postfix(FootstepTrail __instance,Vector3 ownerPos,Vector3 pos,Vector3 groundNormal,float yRotation,bool flip,int variantIndex,float forceDeepFrac,bool __runOriginal)
  {
   if(!__runOriginal||applying||!MyMod.InOnline()||!MyMod.CanSpawnWildlifeInCurrentScene()||__instance.m_TrailType==FootstepTrailType.Player||__instance.m_ImprintType==SnowImprintType.HumanFootprint)return;
   if(queue.Count>=128)queue.Dequeue();
   queue.Enqueue(new Step{trail=__instance.Pointer.GetHashCode(),type=(int)__instance.m_ImprintType,variant=variantIndex,owner=ownerPos,position=pos,normal=groundNormal,yaw=yRotation,flip=flip,depth=__instance.m_LastFootprintDepthFrac});
  }
 }
 public static void Tick()
 {
  if(scene!=MyMod.level_guid||!MyMod.InOnline()){scene=MyMod.level_guid;queue.Clear();trails.Clear();}
  if(queue.Count==0||Time.realtimeSinceStartup<nextSend)return;nextSend=Time.realtimeSinceStartup+.2f;
  var steps=new List<Step>();while(queue.Count>0&&steps.Count<16)steps.Add(queue.Dequeue());
  if(MyMod.iAmHost)Broadcast(0,scene,steps);
  else{using var p=new Packet(198);Write(p,scene,steps);MyMod.SendUDPData(p);}
 }
 private static void Write(Packet p,string area,List<Step> steps)
 {
  p.Write(area);p.Write(steps.Count);
  foreach(var s in steps){p.Write(s.trail);p.Write(s.type);p.Write(s.variant);p.Write(s.owner);p.Write(s.position);p.Write(s.normal);p.Write(s.yaw);p.Write(s.flip);p.Write(s.depth);}
 }
 private static List<Step> Read(Packet p,out string area)
 {
  area=p.ReadString();int count=p.ReadInt();if(count<0||count>16)throw new ArgumentException("Invalid track batch");var steps=new List<Step>();
  for(int i=0;i<count;i++)steps.Add(new Step{trail=p.ReadInt(),type=p.ReadInt(),variant=p.ReadInt(),owner=p.ReadVector3(),position=p.ReadVector3(),normal=p.ReadVector3(),yaw=p.ReadFloat(),flip=p.ReadBool(),depth=p.ReadFloat()});return steps;
 }
 public static void ReceiveClient(int from,Packet p)
 {
  var steps=Read(p,out var area);
  if(from<=0||from>=MyMod.playersData.Count||MyMod.playersData[from]?.m_LevelGuid!=area)return;
  Apply(from,area,steps);Broadcast(from,area,steps);
 }
 private static void Broadcast(int from,string area,List<Step> steps)
 {using var p=new Packet(196);p.Write(from);Write(p,area,steps);ServerSend.SendUDPDataToAllButNotSender(p,from,area);}
 public static void Receive(Packet p){int from=p.ReadInt();var steps=Read(p,out var area);Apply(from,area,steps);}
 private static void Apply(int from,string area,List<Step> steps)
 {
  if(area!=MyMod.level_guid||MyMod.LoadingScreenIsOn)return;
  var manager=GameManager.GetFootstepTrailManager();if(manager==null)return;
  applying=true;
  try
  {
   foreach(var s in steps)
   {
    if(s.type<1||s.type>9||s.variant< -1||s.variant>32||!Finite(s.position)||!Finite(s.normal)||!Finite(s.owner)||s.normal.sqrMagnitude<.01f||!float.IsFinite(s.yaw))continue;
    var key=(from,s.trail);
    if(!trails.TryGetValue(key,out var trail))
    {if(trails.Count>=128)continue;trail=new FootstepTrail(FootstepTrailType.AiPersistent,(SnowImprintType)s.type);manager.AddFootstepTrail(trail);trails[key]=trail;}
    trail.AddFootstep(s.owner,s.position,s.normal,s.yaw,s.flip,s.variant,VitalNumbers.Clamp(s.depth,0,1));
   }
  }
  finally{applying=false;}
 }
 private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
}
