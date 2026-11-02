using System;
using System.Diagnostics;
using MelonLoader;
using UnityEngine;
namespace SkyCoop;
public static class ResourceHealth
{
 private static long baseline=-1,peak;
 private static float next;private static int frames,slow;private static float elapsed;
 public static void Tick()
 {
  if(!MyMod.InOnline()){baseline=-1;peak=0;frames=slow=0;elapsed=0;next=Time.realtimeSinceStartup+60;return;}
  float dt=Time.unscaledDeltaTime;frames++;elapsed+=dt;if(dt>.1f)slow++;
  if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+60;
  try{using var p=Process.GetCurrentProcess();long bytes=p.PrivateMemorySize64;if(baseline<0)baseline=bytes;peak=Math.Max(peak,bytes);MelonLogger.Msg($"[Health] fps={(elapsed>0?frames/elapsed:0):F0} stalls100ms={slow} privateMiB={p.PrivateMemorySize64/1048576} managedMiB={GC.GetTotalMemory(false)/1048576} frameCap={Application.targetFrameRate} deltaMiB={(bytes-baseline)/1048576} peakMiB={peak/1048576} prefabRefs={GameCompat.AssetCacheCount} textureRefs={GameCompat.TextureCacheCount} avatars={SteamConnect.Main.AvatarTextureCount}");}
  catch(Exception){ }
  frames=slow=0;elapsed=0;
 }
}
