using System;
using System.Collections.Generic;
using GameServer;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SkyCoop;

// Keep the co-op animation rig and wardrobe. Story outfits are fixed meshes;
// only the original head is transplanted, so equipped clothing remains visible.
public static class NativeAppearance
{
	private sealed class Visual
	{
		public GameObject root;
		public readonly List<Renderer> originals = new();
		public readonly List<Mesh> meshes = new();
		public int choice;
	}
	private static readonly Dictionary<int,Visual> Visuals = new();
	private static readonly Dictionary<int,int> Choices = new();
	private static MelonPreferences_Entry<int> preference;
	private static bool open;
	private static float nextSend;
	private static int openedFrame;
	public static int Choice => preference?.Value ?? -1;
	public static void Open()
	{
		if (open) return;
		open=true; openedFrame=Time.frameCount; OverlayInput.Acquire();
	}
	private static void Close() { if(!open)return;open=false;OverlayInput.Release(); }
	public static void Tick()
	{
		preference ??= MelonPreferences.CreateCategory("SkyCoop").CreateEntry("NativeCharacter", -1,
			"Original story head: -1 classic, 0 Mackenzie, 1 Astrid, 2 Jeremiah, 3 Molly");
		if (MyMod.DedicatedServerAppMode) return;
		if (MyMod.LoadingScreenIsOn) Close();
		if (KeyboardUtilities.InputManager.GetTogglePressed(KeyCode.F4)) { if(open)Close();else if(!CheatConsole.IsOpen&&!ItemSpawner.IsOpen)Open(); }
		if(open){if(Time.frameCount!=openedFrame&&KeyboardUtilities.InputManager.GetTogglePressed(KeyCode.Escape))Close();else OverlayInput.Keep();}
		foreach(int id in new List<int>(Visuals.Keys))
			if(Visuals[id].root==null){foreach(var mesh in Visuals[id].meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);Visuals.Remove(id);}
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
	public static void Draw()
	{
		if(!open)return;
		float x=(Screen.width-520)/2f,y=(Screen.height-350)/2f;
		GUI.Box(new Rect(x,y,520,350),"Персонаж · F4 / Esc — закрыть");
		GUI.Label(new Rect(x+20,y+35,480,45),"Игровая голова + синхронизируемая одежда коопа.\nНужны локальные ресурсы WINTERMUTE.");
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
	}
	public static void Apply(GameObject player)
	{
		if(player==null)return;
		var actor=player.GetComponent<Comps.MultiplayerPlayer>();
		if(actor==null)return;
		int choice=Choices.TryGetValue(actor.m_ID,out int chosen)?chosen:-1;
		if(actor.m_ID>=0 && actor.m_ID<MyMod.playersData.Count && !string.IsNullOrEmpty(MyMod.playersData[actor.m_ID]?.m_PlayerClothingData?.m_Balaclava))choice=-1;
		ApplyChoice(player,choice);
	}
	public static bool ApplyChoice(GameObject player,int choice)
	{
		int id=player.GetInstanceID();
		if(Visuals.TryGetValue(id,out var old))
		{
			if(old.root!=null&&old.choice==choice){foreach(var r in old.originals)if(r!=null)r.enabled=false;return true;}
			foreach(var r in old.originals)if(r!=null)r.enabled=true;
			if(old.root!=null)UnityEngine.Object.Destroy(old.root);
			foreach(var mesh in old.meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
			Visuals.Remove(id);
		}
		if(choice<0)return true;
		var prefab=NativeCharacterLibrary.Load(choice);if(prefab==null)return false;
		var visual=new Visual{choice=choice};
		try
		{
			Transform target=null;
			foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				foreach(var bone in skin.bones)if(bone!=null&&bone.name=="Head"){target=bone;break;}
				if(target!=null)break;
			}
			if(target==null)throw new InvalidOperationException("Co-op head bone not found");
			visual.root=UnityEngine.Object.Instantiate(prefab);
			visual.root.name="SkyCoop_NativeHead";
			visual.root.SetActive(false);
			foreach(var animator in visual.root.GetComponentsInChildren<Animator>(true))animator.enabled=false;
			Transform nativeHead=null;
			foreach(var bone in visual.root.GetComponentsInChildren<Transform>(true))if(bone.name=="Head_jnt"){nativeHead=bone;break;}
			if(nativeHead==null)throw new InvalidOperationException("Story head bone not found");
			int faces=0;
			foreach(var renderer in visual.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				var source=renderer.sharedMesh;var weights=source.boneWeights;var bones=renderer.bones;
				bool HeadBone(int index){for(var t=bones[index];t!=null;t=t.parent)if(t==nativeHead)return true;return false;}
				bool Vertex(int index){var w=weights[index];float total=0;if(w.weight0>0&&HeadBone(w.boneIndex0))total+=w.weight0;if(w.weight1>0&&HeadBone(w.boneIndex1))total+=w.weight1;if(w.weight2>0&&HeadBone(w.boneIndex2))total+=w.weight2;if(w.weight3>0&&HeadBone(w.boneIndex3))total+=w.weight3;return total>=.5f;}
				var copy=UnityEngine.Object.Instantiate(source);visual.meshes.Add(copy);
				for(int sub=0;sub<source.subMeshCount;sub++)
				{
					var triangles=source.GetTriangles(sub);var keep=new List<int>();
					for(int t=0;t+2<triangles.Length;t+=3)if(Vertex(triangles[t])&&Vertex(triangles[t+1])&&Vertex(triangles[t+2])){keep.Add(triangles[t]);keep.Add(triangles[t+1]);keep.Add(triangles[t+2]);}
					copy.SetTriangles(new Il2CppStructArray<int>(keep.ToArray()),sub);faces+=keep.Count/3;
				}
				renderer.sharedMesh=copy;renderer.updateWhenOffscreen=true;
			}
			if(faces==0)throw new InvalidOperationException("Story head has no readable triangles");
			visual.root.transform.rotation=player.transform.rotation;
			visual.root.transform.position+=target.position-nativeHead.position;
			visual.root.transform.SetParent(target,true);
			foreach(var r in player.transform.GetChild(1).GetComponentsInChildren<Renderer>(true))
			{
				string name=r.name.ToLowerInvariant();
				if(name.Contains("head")||name.Contains("hair")||name.Contains("beard")||name=="brows"){visual.originals.Add(r);r.enabled=false;}
			}
			visual.root.SetActive(true);Visuals[id]=visual;
			MelonLogger.Msg("[Native appearance] "+NativeCharacterLibrary.Names[choice]+" head triangles="+faces);
			return true;
		}
		catch(Exception ex)
		{
			if(visual.root!=null)UnityEngine.Object.Destroy(visual.root);
			foreach(var mesh in visual.meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
			foreach(var r in visual.originals)if(r!=null)r.enabled=true;
			MelonLogger.Warning("[Native appearance] Keeping classic model: "+ex.Message);return false;
		}
	}
}
