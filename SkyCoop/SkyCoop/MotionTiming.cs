using System;
namespace SkyCoop;
public static class MotionTiming
{
 public static bool Newer(int value,int previous)=>unchecked(value-previous)>0;
 // Render shortly behind reception; permit only a short, bounded prediction.
 public static float Factor(double now,double previous,double latest)
 {
  double interval=latest-previous;
  if(interval<=0 || interval>.5)return 1;
  return (float)Math.Clamp((now-.075-previous)/interval,0,1+Math.Min(.1/interval,2));
 }
}
