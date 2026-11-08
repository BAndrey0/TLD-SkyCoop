using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameServer;
using Il2Cpp;
using UnityEngine;
namespace SkyCoop;

// One sequenced, disposable snapshot instead of a reliable backlog of poses.
public static class MotionSync
{
 public const int ClientPacket=196,ServerPacket=194;
 private sealed class State
 {
  public int sequence,level; public string scene,animation;
  public Vector3 position,previous; public Quaternion rotation;
  public double time,previousTime;
 }
 private static readonly Stopwatch Clock=Stopwatch.StartNew();
 private static readonly Dictionary<int,State> states=new();
 private static double nextSend; private static int sequence;
 public static void Tick()
 {
  if(!MyMod.InOnline()){states.Clear();nextSend=0;return;}
  double now=Clock.Elapsed.TotalSeconds;
  // Dead players still need their final Knock pose sent. Wildlife readiness
  // excludes death and therefore is not a suitable movement gate.
  if(now<nextSend||MyMod.LoadingScreenIsOn||string.IsNullOrEmpty(MyMod.level_guid)||Shared.IsMainMenuScene(MyMod.level_name)||MyMod.level_name=="Boot"||MyMod.level_name=="Empty"||GameManager.m_PlayerObject==null)return;
  nextSend=now+.05;
  var transform=GameManager.GetPlayerTransform();if(transform==null)return;
  Vector3 position=transform.position;Quaternion rotation=transform.rotation;
  var rest=GameManager.GetRestComponent();
  if(rest!=null&&rest.IsSleeping()&&rest.m_Bed!=null)
  {if(rest.m_Bed.m_BodyPlacementTransform!=null)position=rest.m_Bed.m_BodyPlacementTransform.position;rotation=MyMod.GetBedRotation(rest.m_Bed);}
  if (BedRest.Active) { position = BedRest.Position; rotation = BedRest.Rotation; }
  var state=new State{sequence=unchecked(++sequence),level=MyMod.levelid,scene=MyMod.level_guid,animation=BedRest.Active ? BedRest.Animation : MyMod.MyAnimState,position=position,rotation=rotation};
  if(MyMod.iAmHost)Broadcast(0,state);else{using var p=new Packet(ClientPacket);Write(p,state);MyMod.SendUDPData(p);}
 }
 private static void Write(Packet p,State s){p.Write(s.sequence);p.Write(s.level);p.Write(s.scene);p.Write(s.animation);p.Write(s.position);p.Write(s.rotation);}
 private static State Read(Packet p)=>new(){sequence=p.ReadInt(),level=p.ReadInt(),scene=p.ReadString(),animation=p.ReadString(),position=p.ReadVector3(),rotation=p.ReadQuaternion()};
 private static bool Accept(int id,State state)
 {
  if(id<0||id>=MyMod.playersData.Count||MyMod.playersData[id]==null)return false;
  if(!float.IsFinite(state.position.x)||!float.IsFinite(state.position.y)||!float.IsFinite(state.position.z)||!float.IsFinite(state.rotation.x)||!float.IsFinite(state.rotation.y)||!float.IsFinite(state.rotation.z)||!float.IsFinite(state.rotation.w))return false;
  double now=Clock.Elapsed.TotalSeconds;
  if(states.TryGetValue(id,out var old))
  {
   // A reused slot may restart its sequence only after a disconnect/load gap.
   if(now-old.time<5&&!MotionTiming.Newer(state.sequence,old.sequence))return false;
   if(old.scene==state.scene&&Vector3.Distance(old.position,state.position)<6)
   {state.previous=old.position;state.previousTime=old.time;}
  }
  state.time=now;if(state.previousTime==0){state.previous=state.position;state.previousTime=now;}
  states[id]=state;
  var data=MyMod.playersData[id];bool moved=(data.m_Position-state.position).sqrMagnitude>.0001f;
  data.m_Position=state.position;data.m_Rotation=state.rotation;data.m_Levelid=state.level;data.m_LevelGuid=state.scene;data.m_AnimState=state.animation;
  if(moved&&id<MyMod.players.Count&&MyMod.players[id]!=null)
  {var actor=MyMod.players[id].GetComponent<Comps.MultiplayerPlayer>();if(actor!=null)MyMod.LongActionCancleCauseMoved(actor);}
  return true;
 }
 public static void ReceiveClient(int from,Packet p){if(from<=0)return;var s=Read(p);if(Accept(from,s))Broadcast(from,s);}
 public static void Receive(Packet p){int id=p.ReadInt();var s=Read(p);if(id!=ClientUser.myId)Accept(id,s);}
 private static void Broadcast(int id,State s){using var p=new Packet(ServerPacket);p.Write(id);Write(p,s);ServerSend.SendUDPDataToAllButNotSender(p,id);}
 public static Vector3 Position(int id,Vector3 fallback)
 {
  if(!states.TryGetValue(id,out var s)||s.scene!=MyMod.level_guid)return fallback;
  return Vector3.LerpUnclamped(s.previous,s.position,MotionTiming.Factor(Clock.Elapsed.TotalSeconds,s.previousTime,s.time));
 }
 public static double Age(int id)=>states.TryGetValue(id,out var s)?Math.Max(0,Clock.Elapsed.TotalSeconds-s.time):-1;
}
