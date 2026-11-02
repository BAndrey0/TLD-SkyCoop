using System.Collections.Generic;
using UnityEngine;
namespace SkyCoop;
public static class PendingPickupVisual
{
 private static readonly Dictionary<int,(GameObject obj,string scene,float until)> pending=new();
 public static void Hide(int key,GameObject obj)
 {if(obj==null||pending.ContainsKey(key))return;pending[key]=(obj,MyMod.level_guid,Time.realtimeSinceStartup+15);obj.SetActive(false);}
 public static void Confirm(int key)=>pending.Remove(key);
 public static void Tick()
 {
  if(pending.Count==0)return;
  foreach(int key in new List<int>(pending.Keys))
  {
   var p=pending[key];
   if(p.obj==null||p.scene!=MyMod.level_guid){pending.Remove(key);continue;}
   if(!MyMod.InOnline()||Time.realtimeSinceStartup>p.until){pending.Remove(key);p.obj.SetActive(true);}
  }
 }
}
