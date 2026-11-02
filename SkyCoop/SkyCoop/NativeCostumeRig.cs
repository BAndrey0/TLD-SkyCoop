using System;
using System.Collections.Generic;
using UnityEngine;
namespace SkyCoop;
// Solve anatomical directions rather than transferring unrelated FBX bone axes.
internal sealed class NativeCostumeRig
{
 private sealed class Link { public Transform source,target,sourceEnd,targetEnd,sa,sb,ta,tb;public Quaternion rest;public int depth;public bool root; }
 private readonly List<Link> links=new();
 private readonly List<(Transform source,Transform target)> feet=new();
 private readonly Transform playerRoot;
 private Transform pelvis;
 public float FitScale{get;private set;}=1;
 public int Count=>links.Count;
 public NativeCostumeRig(GameObject player,GameObject costume)
 {
  playerRoot=player.transform;
  var source=new Dictionary<string,Transform>(StringComparer.Ordinal);
  foreach(var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))foreach(var bone in skin.bones)
   if(bone!=null&&!bone.IsChildOf(costume.transform)&&!source.ContainsKey(bone.name))source.Add(bone.name,bone);
  var targets=new Dictionary<string,Transform>(StringComparer.Ordinal);
  foreach(var bone in costume.GetComponentsInChildren<Transform>(true))targets[bone.name]=bone;
  float reference=BodyLength(source,false),native=BodyLength(targets,true);
  FitScale=ModelProportions.Scale(reference,native);costume.transform.localScale*=FitScale;
  targets.TryGetValue("Hips_jnt",out pelvis);
  foreach(string side in new[]{"Left","Right"})
   if(targets.TryGetValue(side+"Toe_jnt",out var targetFoot)&&source.TryGetValue(side=="Left"?"Toe.L":"Toe.R",out var sourceFoot))feet.Add((sourceFoot,targetFoot));
  foreach(var skin in costume.GetComponentsInChildren<SkinnedMeshRenderer>(true)){skin.updateWhenOffscreen=false;skin.localBounds=new Bounds(new Vector3(0,1.2f,0),new Vector3(4,5,4));}
  foreach(var pair in targets)
  {
   string mapped=Map(pair.Key);if(mapped==null||!source.TryGetValue(mapped,out var from))continue;
   var link=new Link{source=from,target=pair.Value,rest=pair.Value.localRotation,root=pair.Key=="Hips_jnt"};
   for(var p=pair.Value.parent;p!=null;p=p.parent)link.depth++;
   string end=End(pair.Key);
   if(end!=null&&targets.TryGetValue(end,out var te)&&source.TryGetValue(Map(end)??"",out var se)){link.targetEnd=te;link.sourceEnd=se;}
   if(pair.Key=="LeftHand_jnt"||pair.Key=="RightHand_jnt")
   {
    string side=pair.Key.StartsWith("Left")?"Left":"Right";
    if(targets.TryGetValue(side+"Index1_jnt",out var a)&&targets.TryGetValue(side+"Pinky1_jnt",out var b)&&source.TryGetValue(Map(a.name),out var sa)&&source.TryGetValue(Map(b.name),out var sb)){link.ta=a;link.tb=b;link.sa=sa;link.sb=sb;}
   }
   links.Add(link);
  }
  links.Sort((a,b)=>a.depth.CompareTo(b.depth));if(links.Count<15)throw new InvalidOperationException("Incomplete costume rig: "+links.Count);Update();
 }
 public void Update()
 {
  foreach(var l in links)if(l.target!=null)l.target.localRotation=l.rest;
  foreach(var l in links)
  {
   if(l.source==null||l.target==null)continue;
   if(l.root)l.target.position=l.source.position;
   if(l.targetEnd==null||l.sourceEnd==null)continue;
   var wanted=l.sourceEnd.position-l.source.position;var current=l.targetEnd.position-l.target.position;
   if(wanted.sqrMagnitude<.000001f||current.sqrMagnitude<.000001f)continue;
   if(l.ta!=null&&l.sa!=null)
   {
    var a=l.sa.position-l.sb.position;var b=l.ta.position-l.tb.position;
    if(Vector3.Cross(wanted,a).sqrMagnitude>.0000001f&&Vector3.Cross(current,b).sqrMagnitude>.0000001f){l.target.rotation=Quaternion.LookRotation(wanted,a)*Quaternion.Inverse(Quaternion.LookRotation(current,b))*l.target.rotation;continue;}
   }
   l.target.rotation=Quaternion.FromToRotation(current,wanted)*l.target.rotation;
  }
  if(pelvis!=null&&feet.Count>0)
  {
   Vector3 up=playerRoot.up;float a=float.PositiveInfinity,b=float.PositiveInfinity;
   foreach(var foot in feet)if(foot.source!=null&&foot.target!=null){a=Mathf.Min(a,Vector3.Dot(foot.source.position,up));b=Mathf.Min(b,Vector3.Dot(foot.target.position,up));}
   if(float.IsFinite(a)&&float.IsFinite(b))pelvis.position+=up*(a-b);
  }
 }
 private static float BodyLength(Dictionary<string,Transform> bones,bool native)
 {
  float Chain(params string[] names){float total=0;for(int i=1;i<names.Length;i++)if(bones.TryGetValue(names[i-1],out var a)&&bones.TryGetValue(names[i],out var b))total+=Vector3.Distance(a.position,b.position);return total;}
  float torso=native?Chain("Hips_jnt","Spine1_jnt","Spine2_jnt","Spine3_jnt","Neck_jnt","Head_jnt"):Chain("root","Spine1","Spine2","Chest","Neck","Head");
  float legs=native?(Chain("LeftHip_jnt","LeftKnee_jnt","LeftFoot_jnt")+Chain("RightHip_jnt","RightKnee_jnt","RightFoot_jnt"))*.5f:(Chain("Thigh.L","Shin.L","Foot.L")+Chain("Thigh.R","Shin.R","Foot.R"))*.5f;
  return torso+legs;
 }
 private static string End(string name)
 {
  switch(name){case "Hips_jnt":return "Spine1_jnt";case "Spine1_jnt":return "Spine2_jnt";case "Spine2_jnt":return "Spine3_jnt";case "Spine3_jnt":return "Neck_jnt";case "Neck_jnt":return "Head_jnt";}
  string[] from={"Shoulder","UpArm","Elbow","Hand","Hip","Knee","Foot"},to={"UpArm","Elbow","Hand","Middle1","Knee","Foot","Toe"};
  foreach(string side in new[]{"Left","Right"})for(int i=0;i<from.Length;i++)if(name==side+from[i]+"_jnt")return side+to[i]+"_jnt";
  if(name.EndsWith("1_jnt",StringComparison.Ordinal))return name.Substring(0,name.Length-5)+"2_jnt";
  if(name.EndsWith("2_jnt",StringComparison.Ordinal))return name.Substring(0,name.Length-5)+"3_jnt";
  return null;
 }
 internal static string Map(string name)
 {
  switch(name){case "Hips_jnt":return "root";case "Spine1_jnt":return "Spine1";case "Spine2_jnt":return "Spine2";case "Spine3_jnt":return "Chest";case "Neck_jnt":return "Neck";case "Head_jnt":return "Head";}
  string side=name.StartsWith("Left",StringComparison.Ordinal)?"l":name.StartsWith("Right",StringComparison.Ordinal)?"r":null;if(side==null)return null;
  string part=name.Substring(side=="l"?4:5).Replace("_jnt","");
  switch(part){case "Shoulder":return "chest_"+side;case "UpArm":return "arms_"+side+"_01";case "Elbow":return "arms_"+side+"_02";case "Hand":return "hand_"+side;case "Hip":return "Thigh."+side.ToUpperInvariant();case "Knee":return "Shin."+side.ToUpperInvariant();case "Foot":return "Foot."+side.ToUpperInvariant();case "Toe":return "Toe."+side.ToUpperInvariant();}
  foreach(string f in new[]{"Index","Middle","Ring","Pinky","Thumb"})if(part.StartsWith(f,StringComparison.Ordinal)&&part.Length==f.Length+1&&part[part.Length-1]>='1'&&part[part.Length-1]<='3')return (f=="Pinky"?"little":f.ToLowerInvariant())+"_"+side+"_"+part[part.Length-1];
  return null;
 }
}
