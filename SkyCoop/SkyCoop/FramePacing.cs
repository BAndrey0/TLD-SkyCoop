using Il2Cpp;
using UnityEngine;
namespace SkyCoop;
public static class FramePacing
{
 private static int original=-2,last=-2;
 public static void Tick()
 {
  int current=Application.targetFrameRate;
  if(original==-2||current!=last)original=current;
  bool menu=string.IsNullOrEmpty(MyMod.level_name)||MyMod.level_name=="Empty"||MyMod.level_name=="Boot"||MyMod.LoadingScreenIsOn||Shared.IsMainMenuScene(MyMod.level_name)||CheatConsole.IsOpen||ItemSpawner.IsOpen||NativeAppearance.IsOpen||GameCompat.Panel<Panel_PauseMenu>()?.isActiveAndEnabled==true;
  int cap=!Application.isFocused?30:menu?60:120;
  int desired=original>0?System.Math.Min(original,cap):cap;
  if(current!=desired)Application.targetFrameRate=desired;
  last=desired;
 }
}
