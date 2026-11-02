using System;
using System.Diagnostics;
using MelonLoader;
using UnityEngine;
namespace SkyCoop;
public static class ResourceHealth
{
 private static float next;private static int frames,slow;private static float elapsed;
 public static void Tick()
 {
  if(!MyMod.InOnline()){frames=slow=0;elapsed=0;next=Time.realtimeSinceStartup+60;return;}
  float dt=Time.unscaledDeltaTime;frames++;elapsed+=dt;if(dt>.1f)slow++;
  if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+60;
  try{using var p=Process.GetCurrentProcess();MelonLogger.Msg($"[Health] fps={(elapsed>0?frames/elapsed:0):F0} stalls100ms={slow} privateMiB={p.PrivateMemorySize64/1048576} managedMiB={GC.GetTotalMemory(false)/1048576} frameCap={Application.targetFrameRate}");}
  catch(Exception){ }
  frames=slow=0;elapsed=0;
 }
}
