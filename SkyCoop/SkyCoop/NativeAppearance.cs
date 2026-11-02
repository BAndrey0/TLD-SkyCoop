using System;
using System.Collections.Generic;
using GameServer;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HarmonyLib;

namespace SkyCoop;

// Keep the co-op animation controller and equipment sockets, and drive the
// complete story costume from its bone poses. Inventory clothes are hidden.
public static class NativeAppearance
{
	private sealed class Visual
	{
		public GameObject root;
		public readonly List<Renderer> originals = new();
		public readonly List<Mesh> meshes = new();
		public readonly Dictionary<Animator,AnimatorCullingMode> animationModes = new();
		public int choice;
		public NativeCostumeRig rig;
	}
	private static readonly Dictionary<int,Visual> Visuals = new();
	private static readonly Dictionary<int,int> Choices = new();
	private static MelonPreferences_Entry<int> preference;
	private static bool open;
	private static float nextSend;
	private static int openedFrame;
	private static Panel_SelectSurvivor selectionPanel;
	private static bool selectionFemale, confirmingSelection;
	[HarmonyPatch(typeof(Panel_SelectSurvivor), "OnSelectSurvivorMale")]
	private static class SelectMale
	{
		[HarmonyPriority(Priority.First)]
		private static bool Prefix(Panel_SelectSurvivor __instance) => SelectAtStart(__instance,false);
	}
	[HarmonyPatch(typeof(Panel_SelectSurvivor), "OnSelectSurvivorFemale")]
	private static class SelectFemale
	{
		[HarmonyPriority(Priority.First)]
		private static bool Prefix(Panel_SelectSurvivor __instance) => SelectAtStart(__instance,true);
	}
	private static bool SelectAtStart(Panel_SelectSurvivor panel, bool female)
	{
		if (confirmingSelection || (MyMod.PendingSave == null && MenuChange.MenuMode != "NewGameSelect")) return true;
		selectionPanel=panel;selectionFemale=female;panel.Enable(false);Open();return false;
	}
	public static int Choice => preference?.Value ?? -1;
	public static void Open()
	{
		if (open) return;
		open=true; openedFrame=Time.frameCount; OverlayInput.Acquire();
	}
	private static void Close()
	{
		if(!open)return;open=false;OverlayInput.Release();
		var panel=selectionPanel;selectionPanel=null;
		if(panel!=null&&Shared.IsMainMenuScene(MyMod.level_name))panel.Enable(true);
	}
	public static void Tick()
	{
		preference ??= MelonPreferences.CreateCategory("SkyCoop").CreateEntry("NativeCharacter", -1,
			"Original story costume: -1 classic, 0 Mackenzie, 1 Astrid, 2 Jeremiah, 3 Molly");
		if (MyMod.DedicatedServerAppMode) return;
		if (MyMod.LoadingScreenIsOn) Close();
		if (KeyboardUtilities.InputManager.GetTogglePressed(KeyCode.F4)) { if(open)Close();else if(!CheatConsole.IsOpen&&!ItemSpawner.IsOpen)Open(); }
		if(open){if(Time.frameCount!=openedFrame&&KeyboardUtilities.InputManager.GetTogglePressed(KeyCode.Escape))Close();else OverlayInput.Keep();}
		foreach(int id in new List<int>(Visuals.Keys))
			if(Visuals[id].root==null){foreach(var pair in Visuals[id].animationModes)if(pair.Key!=null)pair.Key.cullingMode=pair.Value;foreach(var mesh in Visuals[id].meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);Visuals.Remove(id);}
		if(!MyMod.InOnline()){Choices.Clear(); nextSend=0; return;}
		if(Time.realtimeSinceStartup<nextSend)return;
		nextSend=Time.realtimeSinceStartup+3;
		if(MyMod.iAmHost)
		{
			Choices[0]=Choice;
			foreach(var pair in Choices)Broadcast(pair.Key,pair.Value);
		}
		else if(MyMod.sendMyPosition){using var packet=new Packet(195);packet.Write(Choice);MyMod.SendUDPData(packet);}
	}
	public static void ReceiveClient(int from, Packet packet)
	{
		int choice=packet.ReadInt();
		if(choice < -1 || choice >= NativeCharacterLibrary.Names.Length || from<=0 || from>=MyMod.playersData.Count)return;
		if(!Choices.TryGetValue(from,out int old)||old!=choice){Choices[from]=choice;Refresh(from);}
		Broadcast(from,choice);
	}
	private static void Broadcast(int id,int choice){using var packet=new Packet(193);packet.Write(id);packet.Write(choice);ServerSend.SendUDPDataToAll(packet);}
	public static void Receive(Packet packet)
	{
		int id=packet.ReadInt(),choice=packet.ReadInt();
		if(id<0||id>=MyMod.playersData.Count||choice < -1||choice>=NativeCharacterLibrary.Names.Length)return;
		if(!Choices.TryGetValue(id,out int old)||old!=choice){Choices[id]=choice;Refresh(id);}
	}
	private static void Refresh(int id)
	{
		if(id>=0&&id<MyMod.players.Count&&id<MyMod.playersData.Count&&MyMod.playersData[id]!=null&&MyMod.players[id]!=null)MyMod.players[id].GetComponent<Comps.MultiplayerPlayerClothingManager>()?.UpdateClothing();
	}
	public static void LateUpdate()
	{
		foreach(var visual in Visuals.Values)if(visual.root!=null)visual.rig?.Update();
	}
	public static void Draw()
	{
		if(!open)return;
		float height=selectionPanel!=null?410:350;
		float x=(Screen.width-520)/2f,y=(Screen.height-height)/2f;
		GUI.Box(new Rect(x,y,520,height),"Персонаж · F4 / Esc — закрыть");
		GUI.Label(new Rect(x+20,y+35,480,45),"Полный сюжетный костюм. Надетая одежда скрыта.\nНужны локальные ресурсы WINTERMUTE.");
		for(int i=-1;i<NativeCharacterLibrary.Names.Length;i++)
		{
			string label=i<0?"Классическая модель коопа":NativeCharacterLibrary.Names[i];
			if(GUI.Button(new Rect(x+20,y+90+(i+1)*40,480,34),(Choice==i?"✓ ":"")+label))
			{
				if(i>=0&&NativeCharacterLibrary.Load(i)==null)continue;
				preference.Value=i;MelonPreferences.Save();nextSend=0;
			}
		}
		GUI.Label(new Rect(x+20,y+300,480,35),NativeCharacterLibrary.Status);
		if(selectionPanel!=null&&GUI.Button(new Rect(x+20,y+345,480,42),"Продолжить"))
		{
			if(Choice>=0&&NativeCharacterLibrary.Load(Choice)==null)return;
			var panel=selectionPanel;bool female=Choice>=0?(Choice==1||Choice==3):selectionFemale;
			Close();confirmingSelection=true;
			try{if(female)panel.OnSelectSurvivorFemale();else panel.OnSelectSurvivorMale();}
			finally{confirmingSelection=false;}
		}
	}
	public static void Apply(GameObject player)
	{
		if(player==null)return;
		var actor=player.GetComponent<Comps.MultiplayerPlayer>();
		if(actor==null)return;
		int choice=Choices.TryGetValue(actor.m_ID,out int chosen)?chosen:-1;
		ApplyChoice(player,choice);
	}
	public static bool ApplyChoice(GameObject player,int choice)
	{
		int id=player.GetInstanceID();
		if(Visuals.TryGetValue(id,out var old))
		{
			if(old.root!=null&&old.choice==choice){foreach(var r in old.originals)if(r!=null)r.enabled=false;return true;}
			foreach(var r in old.originals)if(r!=null)r.enabled=true;
			foreach(var pair in old.animationModes)if(pair.Key!=null)pair.Key.cullingMode=pair.Value;
			if(old.root!=null)UnityEngine.Object.Destroy(old.root);
			foreach(var mesh in old.meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
			Visuals.Remove(id);
		}
		if(choice<0)return true;
		var prefab=NativeCharacterLibrary.Load(choice);if(prefab==null)return false;
		var visual=new Visual{choice=choice};
		try
		{
			foreach(var animator in player.GetComponentsInChildren<Animator>(true))
				if(animator.enabled&&animator.runtimeAnimatorController!=null)
				{ visual.animationModes.Add(animator,animator.cullingMode);animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
			visual.root=UnityEngine.Object.Instantiate(prefab);
			visual.root.name="SkyCoop_NativeCostume";
			visual.root.SetActive(false);
			visual.root.transform.SetParent(player.transform,false);
			foreach(var animator in visual.root.GetComponentsInChildren<Animator>(true))animator.enabled=false;
			visual.rig=new NativeCostumeRig(player,visual.root);
			for(int group=0;group<2&&group<player.transform.childCount;group++)
			foreach(var renderer in player.transform.GetChild(group).GetComponentsInChildren<Renderer>(true))
			{
				visual.originals.Add(renderer);renderer.enabled=false;
			}
			visual.root.SetActive(true);Visuals[id]=visual;
			MelonLogger.Msg("[Native appearance] "+NativeCharacterLibrary.Names[choice]+" full costume, bones="+visual.rig.Count);
			return true;
		}
		catch(Exception ex)
		{
			foreach(var pair in visual.animationModes)if(pair.Key!=null)pair.Key.cullingMode=pair.Value;
			if(visual.root!=null)UnityEngine.Object.Destroy(visual.root);
			foreach(var mesh in visual.meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
			foreach(var r in visual.originals)if(r!=null)r.enabled=true;
			MelonLogger.Warning("[Native appearance] Keeping classic model: "+ex.Message);return false;
		}
	}
}
