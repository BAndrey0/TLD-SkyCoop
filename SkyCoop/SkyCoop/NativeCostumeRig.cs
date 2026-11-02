using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkyCoop;

// Retarget the co-op animator's bones to the story rig. Bind poses supply the
// axis correction; copying raw Euler angles between these rigs is incorrect.
internal sealed class NativeCostumeRig
{
	private sealed class Link
	{
		public Transform source, target;
		public Quaternion axis;
		public int depth;
		public bool root;
	}
	private readonly List<Link> links = new();
	public int Count => links.Count;
	public NativeCostumeRig(GameObject player, GameObject costume)
	{
		var sources = new Dictionary<string,Transform>(StringComparer.Ordinal);
		var sourceRest = new Dictionary<string,Quaternion>(StringComparer.Ordinal);
		foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			var bones=skin.bones;var bind=skin.sharedMesh?.bindposes;
			if(bind==null)continue;
			for(int i=0;i<bones.Length&&i<bind.Length;i++)
				if(bones[i]!=null&&!sources.ContainsKey(bones[i].name))
				{
					sources.Add(bones[i].name,bones[i]);
					sourceRest.Add(bones[i].name,(skin.transform.localToWorldMatrix*bind[i].inverse).rotation);
				}
		}
		var nativeRest=new Dictionary<string,Quaternion>(StringComparer.Ordinal);
		foreach(var skin in costume.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			var bones=skin.bones;var bind=skin.sharedMesh.bindposes;
			for(int i=0;i<bones.Length&&i<bind.Length;i++)
				if(bones[i]!=null&&!nativeRest.ContainsKey(bones[i].name))nativeRest.Add(bones[i].name,(skin.transform.localToWorldMatrix*bind[i].inverse).rotation);
			skin.updateWhenOffscreen=true;
			skin.localBounds=new Bounds(new Vector3(0,1.2f,0),new Vector3(4,5,4));
		}
		foreach(var bone in costume.GetComponentsInChildren<Transform>(true))
		{
			string sourceName=Map(bone.name);
			if(sourceName==null||!sources.TryGetValue(sourceName,out var source)||!nativeRest.TryGetValue(bone.name,out var rest))continue;
			int depth=0;for(var parent=bone.parent;parent!=null;parent=parent.parent)depth++;
			links.Add(new Link{source=source,target=bone,axis=Quaternion.Inverse(sourceRest[sourceName])*rest,depth=depth,root=bone.name=="Hips_jnt"});
		}
		links.Sort((a,b)=>a.depth.CompareTo(b.depth));
		if(links.Count<15)throw new InvalidOperationException("Incomplete costume rig: "+links.Count+" mapped bones");
		Update();
	}
	public void Update()
	{
		foreach(var link in links)
			if(link.source!=null&&link.target!=null)
			{
				link.target.rotation=link.source.rotation*link.axis;
				// Keep authored segment lengths. World-position copying every joint
				// stretches the story mesh onto the unrelated co-op proportions.
				if(link.root)link.target.position=link.source.position;
			}
	}
	internal static string Map(string name)
	{
		switch(name)
		{
			case "Hips_jnt": return "root";
			case "Spine1_jnt": return "Spine1";
			case "Spine2_jnt": return "Spine2";
			case "Spine3_jnt": return "Chest";
			case "Neck_jnt": return "Neck";
			case "Head_jnt": return "Head";
		}
		string side=name.StartsWith("Left",StringComparison.Ordinal)?"l":name.StartsWith("Right",StringComparison.Ordinal)?"r":null;
		if(side==null)return null;
		string part=name.Substring(side=="l"?4:5).Replace("_jnt","");
		switch(part)
		{
			case "Shoulder": return "chest_"+side;
			case "UpArm": return "arms_"+side+"_01";
			case "Elbow": return "arms_"+side+"_02";
			case "Hand": return "hand_"+side;
			case "Hip": return "Thigh."+side.ToUpperInvariant();
			case "Knee": return "Shin."+side.ToUpperInvariant();
			case "Foot": return "Foot."+side.ToUpperInvariant();
			case "Toe": return "Toe."+side.ToUpperInvariant();
		}
		foreach(string finger in new[]{"Index","Middle","Ring","Pinky","Thumb"})
			if(part.StartsWith(finger,StringComparison.Ordinal)&&part.Length==finger.Length+1&&part[part.Length-1]>='1'&&part[part.Length-1]<='3')
				return (finger=="Pinky"?"little":finger.ToLowerInvariant())+"_"+side+"_"+part[part.Length-1];
		return null;
	}
}
