using System;
using System.Collections.Generic;
using System.Diagnostics;
using GameServer;
using Il2Cpp;
using UnityEngine;
namespace SkyCoop;

// Host-clock RTT; no synchronized machine clocks or assumed heartbeat latency.
public static class SessionPing
{
 private static readonly Stopwatch Clock=Stopwatch.StartNew();
 private static readonly Dictionary<int,(int nonce,double time)> pending=new();
 private static readonly Dictionary<int,(int ms,double time)> values=new();
 private static double nextProbe;private static int nonce;
 public static void Tick()
 {
  if(!MyMod.InOnline()){pending.Clear();values.Clear();nextProbe=0;return;}
  if(!MyMod.iAmHost||Clock.Elapsed.TotalSeconds<nextProbe)return;
  double now=Clock.Elapsed.TotalSeconds;nextProbe=now+2;
  for(int id=1;id<=Server.MaxPlayers;id++)
   if(Server.clients.TryGetValue(id,out var client)&&client.IsBusy()&&!client.RCON)
   {int token=unchecked(++nonce);pending[id]=(token,now);using var p=new Packet(195);p.Write(0);p.Write(token);ServerSend.SendUDPData(id,p);}
  using var table=new Packet(195);table.Write(2);table.Write(values.Count);
  foreach(var pair in values){table.Write(pair.Key);table.Write(now-pair.Value.time<8?pair.Value.ms:-1);}
  ServerSend.SendUDPDataToAll(table);
 }
 public static void Receive(Packet p)
 {
  int kind=p.ReadInt();
  if(kind==0){int token=p.ReadInt();using var reply=new Packet(197);reply.Write(token);MyMod.SendUDPData(reply);}
  else if(kind==2){int count=p.ReadInt();if(count<0||count>128)return;values.Clear();for(int i=0;i<count;i++){int id=p.ReadInt(),ms=p.ReadInt();values[id]=(ms,Clock.Elapsed.TotalSeconds);}}
 }
 public static void ReceiveClient(int from,Packet p)
 {
  int token=p.ReadInt();double now=Clock.Elapsed.TotalSeconds;
  if(pending.TryGetValue(from,out var sent)&&sent.nonce==token)
  {values[from]=((int)Math.Clamp((now-sent.time)*1000,0,60000),now);pending.Remove(from);}
 }
 public static void Draw()
 {
  if(!MyMod.InOnline()||GameCompat.Panel<Panel_PauseMenu>()?.isActiveAndEnabled!=true)return;
  float width=350,x=Screen.width-width-24,y=90;
  GUI.Box(new Rect(x,y,width,80+MyMod.playersData.Count*27),"Игроки · RTT до хоста");int row=0;
  for(int id=0;id<MyMod.playersData.Count;id++)
  {
   var player=MyMod.playersData[id];if(player==null||(!player.m_Used&&id!=0&&id!=ClientUser.myId))continue;
   string ping=id==0?"хост":values.TryGetValue(id,out var v)&&v.ms>=0&&Clock.Elapsed.TotalSeconds-v.time<8?v.ms+" мс":"нет ответа";
   double age=MotionSync.Age(id);string stale=age>=1?" · данные "+age.ToString("F1")+" с назад":"";
   GUI.Label(new Rect(x+14,y+35+row++*27,width-25,26),player.m_Name+" — "+ping+stale);
  }
 }
}
