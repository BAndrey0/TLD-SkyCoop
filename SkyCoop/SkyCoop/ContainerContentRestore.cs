using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

// A network contents snapshot must not rebase relative lid tweens from the
// pose of an already open container. Scene loading still initializes normally.
internal sealed class ContainerContentRestore : IDisposable
{
 private static readonly HashSet<IntPtr> restoring = new();
 internal static bool IsRestoring(ObjectAnim anim)=>anim!=null&&restoring.Contains(anim.Pointer);
 private readonly List<(Transform target,Vector3 position,Quaternion rotation,Vector3 scale)> poses=new();
 private readonly List<IntPtr> owned=new();
 public ContainerContentRestore(Container box)
 {
  Capture(box.transform);
  if(box.m_ObjectAnims!=null)foreach(var anim in box.m_ObjectAnims)Protect(anim);
  foreach(var door in box.GetComponentsInChildren<OpenClose>(true))Protect(door.m_ObjectAnim);
 }
 private void Capture(Transform target)
 {
  if(target==null)return;
  foreach(var p in poses)if(p.target==target)return;
  poses.Add((target,target.localPosition,target.localRotation,target.localScale));
 }
 private void Protect(ObjectAnim anim)
 {
  if(anim==null)return;
  if(restoring.Add(anim.Pointer))owned.Add(anim.Pointer);
  if(anim.m_Target!=null)Capture(anim.m_Target.transform);
 }
 public void Dispose()
 {
  foreach(var p in poses)if(p.target!=null){p.target.localPosition=p.position;p.target.localRotation=p.rotation;p.target.localScale=p.scale;}
  foreach(var ptr in owned)restoring.Remove(ptr);
 }
 [HarmonyPatch(typeof(ObjectAnim),"Initialize")]
 private static class KeepTweenOrigin
 {
  private static bool Prefix(ObjectAnim __instance)=>!restoring.Contains(__instance.Pointer);
 }
 [HarmonyPatch(typeof(ObjectAnim),"Play")]
 private static class KeepPose
 {
  private static bool Prefix(ObjectAnim __instance)=>!restoring.Contains(__instance.Pointer);
 }
 [HarmonyPatch(typeof(ObjectAnim),"TryPlay")]
 private static class KeepOptionalPose
 {
  private static bool Prefix(ObjectAnim __instance,ref bool __result)
  {if(!restoring.Contains(__instance.Pointer))return true;__result=false;return false;}
 }
}
