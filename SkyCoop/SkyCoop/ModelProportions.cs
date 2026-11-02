using System;
namespace SkyCoop;
public static class ModelProportions
{
 public static float Scale(float reference,float costume)
 {
  if(!float.IsFinite(reference)||!float.IsFinite(costume)||reference<=.1f||costume<=.1f)return 1;
  return Math.Clamp(reference/costume,.8f,1.4f);
 }
}
