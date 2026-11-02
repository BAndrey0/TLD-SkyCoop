using System;
namespace SkyCoop;
public static class MotionWire
{
 public static bool IsMotion(byte[] wire,bool fromHost)
 {
  if(wire==null||wire.Length<8)return false;
  int offset=4;
  if(!fromHost)
  {
   int guidLength=BitConverter.ToInt32(wire,4);
   if(guidLength<0||guidLength>wire.Length-16)return false;
   offset=12+guidLength;
  }
  if(offset>wire.Length-4)return false;
  return BitConverter.ToInt32(wire,offset)==(fromHost?194:196);
 }
}
