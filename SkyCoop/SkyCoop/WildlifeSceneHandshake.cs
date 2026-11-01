using GameServer;
using UnityEngine;

namespace SkyCoop;

// Scene readiness is reported by the machine which actually loaded it. The host
// need not load that scene or look up any of its SpawnRegions.
public static class WildlifeSceneHandshake
{
	private static float nextSend;
	private static string lastScene = "";
	private static bool lastReady;
	public static void Tick()
	{
		if (!MyMod.sendMyPosition || MyMod.iAmHost) { nextSend = 0; return; }
		string scene = MyMod.level_guid ?? "";
		bool ready = MyMod.WildlifeSceneReady();
		if (Time.realtimeSinceStartup < nextSend && lastScene == scene && lastReady == ready) return;
		nextSend = Time.realtimeSinceStartup + 3f;
		lastScene = scene; lastReady = ready;
		using var packet = new Packet((int)ClientPackets.WILDLIFESCENEREADY);
		packet.Write(scene); packet.Write(MyMod.levelid); packet.Write(ready);
		MyMod.SendUDPData(packet);
	}
}
