using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
namespace SkyCoop;

// Native local searches and remote replays must share a per-lid state machine.
public static class ContainerLidState
{
 private sealed class State
 {
  public ObjectAnim anim;public Transform target;public readonly LidTransition gate=new();
  public Vector3 closedPosition,openPosition;public Quaternion closedRotation,openRotation;public bool openKnown;
  public float started;
 }
 private static readonly Dictionary<IntPtr,State> states=new();
 private static readonly HashSet<IntPtr> active=new();
 private static float nextTick;
 private static State Get(ObjectAnim anim)
 {
  if(states.TryGetValue(anim.Pointer,out var state)&&state.anim!=null)return state;
  var box=anim.GetComponentInParent<Container>()??anim.GetComponentInChildren<Container>();
  if(box==null&&anim.m_Target!=null)box=anim.m_Target.GetComponentInParent<Container>();
  if(box==null||anim.m_Target==null)return null;
  var target=anim.m_Target.transform;
  state=new State{anim=anim,target=target,closedPosition=target.localPosition,closedRotation=target.localRotation};states[anim.Pointer]=state;return state;
 }
 [HarmonyPatch(typeof(ObjectAnim),"Initialize")]
 private static class RememberClosed
 {
  private static void Postfix(ObjectAnim __instance,bool __runOriginal){if(__runOriginal&&MyMod.InOnline())Get(__instance);}
 }
 [HarmonyPatch(typeof(ObjectAnim),"Play")]
 private static class PlayOnce
 {
  [HarmonyPriority(Priority.First)]
  private static bool Prefix(ObjectAnim __instance,string name)
  {
   if(!MyMod.InOnline()||(name!="open"&&name!="close")||ContainerContentRestore.IsRestoring(__instance))return true;
   var s=Get(__instance);if(s==null)return true;
   if(!s.gate.Start(name,__instance.IsAnimating()||(active.Contains(__instance.Pointer)&&Time.realtimeSinceStartup-s.started<.25f))){if(s.gate.Queued!=null)active.Add(__instance.Pointer);return false;}
   if(name=="open"){s.target.localPosition=s.closedPosition;s.target.localRotation=s.closedRotation;}
   else if(s.openKnown){s.target.localPosition=s.openPosition;s.target.localRotation=s.openRotation;}
   s.started=Time.realtimeSinceStartup;active.Add(__instance.Pointer);return true;
  }
 }
 public static void Tick()
 {
  if(!MyMod.InOnline()){states.Clear();active.Clear();return;}
  if(Time.realtimeSinceStartup<nextTick||active.Count==0)return;nextTick=Time.realtimeSinceStartup+.1f;
  foreach(var ptr in new List<IntPtr>(active))
  {
   var s=states[ptr];if(s.anim==null||s.target==null){active.Remove(ptr);states.Remove(ptr);continue;}
   if(Time.realtimeSinceStartup-s.started<.25f||s.anim.IsAnimating())continue;
   if(s.gate.Current=="close"){s.target.localPosition=s.closedPosition;s.target.localRotation=s.closedRotation;}
   else if(!s.openKnown){s.openPosition=s.target.localPosition;s.openRotation=s.target.localRotation;s.openKnown=true;}
   else{s.target.localPosition=s.openPosition;s.target.localRotation=s.openRotation;}
   active.Remove(ptr);string queued=s.gate.Complete();if(queued!=null)s.anim.Play(queued);
  }
  if(states.Count>256)foreach(var ptr in new List<IntPtr>(states.Keys))if(states[ptr].anim==null)states.Remove(ptr);
 }
}
