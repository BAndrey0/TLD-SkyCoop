using Il2Cpp;
using UnityEngine;
namespace SkyCoop;
public static class WorkLighting
{
 private static TimeOfDay owner;private static float scale,deadline;
 public static void Begin(TimeOfDay tod,float duration)
 {
  if(!MyMod.InOnline()||tod==null)return;
  if(owner==null){owner=tod;scale=VitalNumbers.Rate(tod.GetDayLengthScale(),1);}
  deadline=Time.realtimeSinceStartup+VitalNumbers.Rate(duration,30)+10;
 }
 public static void End(TimeOfDay tod)
 {
  if(tod==null||owner==null||tod.Pointer!=owner.Pointer||tod.IsTimeLapseActive())return;
  Restore(tod);
 }
 private static void Restore(TimeOfDay tod)
 {
  owner=null;tod.SetDayLengthScale(scale);
  if(Utils.TryParseTOD(MyMod.OveridedTime,out var time))tod.SetNormalizedTime(time,true);
  tod.ForceUpdateTodObjects();tod.SetCameraVignettingIndoors();
 }
 public static void Tick()
 {
  if(owner==null)return;
  if(!MyMod.InOnline()||MyMod.LoadingScreenIsOn){owner=null;return;}
  if(!owner.IsTimeLapseActive()){Restore(owner);return;}
  if(Time.realtimeSinceStartup>deadline&&!(GameManager.m_Rest?.IsSleeping()??false))
  {var tod=owner;tod.m_IsTimeLapseActiveCounter=0;Restore(tod);}
 }
}
