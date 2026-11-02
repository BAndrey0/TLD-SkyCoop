using System;
namespace SkyCoop;

// Math.Clamp/Mathf.Clamp do not turn NaN into a valid stat.
public static class VitalNumbers
{
 public static float Clamp(float value,float fallback,float maximum=100)
 {
  if(!float.IsFinite(maximum)||maximum<=0)maximum=100;
  if(!float.IsFinite(value))value=float.IsFinite(fallback)?fallback:maximum*.6f;
  return Math.Clamp(value,0,maximum);
 }
 public static float Rate(float value,float fallback)=>float.IsFinite(value)&&value>0?value:fallback;
 public static float Fraction(float value,float maximum,float fallback)=>Clamp(value,fallback,maximum)/(float.IsFinite(maximum)&&maximum>0?maximum:100);
}
