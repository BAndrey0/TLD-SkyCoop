using System;
using System.Collections.Generic;
using UnityEngine;
namespace SkyCoop;
// Transfer the animated orientation in the avatar's space. Calibrate the
// difference between both bind poses once, including the roll of each palm.
internal sealed class NativeCostumeRig
{
 private sealed record Link(Transform Source, Transform Target, Quaternion Correction, bool Root);
 private sealed record RestPose(Vector3 Position, Quaternion Rotation);
 private sealed record Arm(Transform Upper, Transform Elbow, Transform Wrist, Transform Grip);
 private readonly List<Link> links = new();
 private readonly List<Arm> arms = new();
 private readonly Transform playerRoot;
 private readonly Vector3 hipOffset;
 private readonly Transform sourceHead, targetHead;
 public float FitScale { get; private set; } = 1f;
 public int Count => links.Count;

 public NativeCostumeRig(GameObject player, GameObject costume, bool headOnly = false)
 {
  playerRoot = player.transform;
  var source = new Dictionary<string, Transform>(StringComparer.Ordinal);
  var rest = new Dictionary<string, RestPose>(StringComparer.Ordinal);
  foreach (var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
  {
   var mesh = skin.sharedMesh;
   if (mesh == null) continue;
   var bones = skin.bones;
   var binds = mesh.bindposes;
   var skinToAvatar = playerRoot.worldToLocalMatrix * skin.transform.localToWorldMatrix;
   for (int i = 0; i < bones.Length; i++)
   {
    var bone = bones[i];
    if (bone == null || bone.IsChildOf(costume.transform) || !source.TryAdd(bone.name, bone)) continue;
    var transform = i < binds.Length ? skinToAvatar * binds[i].inverse : playerRoot.worldToLocalMatrix * bone.localToWorldMatrix;
    rest.Add(bone.name, new RestPose(transform.MultiplyPoint3x4(Vector3.zero), transform.rotation));
   }
  }
  var targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
  var ordered = costume.GetComponentsInChildren<Transform>(true);
  foreach (var bone in ordered) targets.TryAdd(bone.name, bone);
  if (headOnly)
  {
   sourceHead = source["Head"];
   targetHead = targets["Head_jnt"];
  }
  FitScale = ModelProportions.Scale(BodyLength(source, false), BodyLength(targets, true));
  costume.transform.localScale *= FitScale;
  var inverseRoot = Quaternion.Inverse(playerRoot.rotation);
  foreach (var target in ordered)
  {
   string mapped = Map(target.name);
   if (mapped == null || !source.TryGetValue(mapped, out var from)) continue;
   var bind = rest[mapped];
   Quaternion targetRotation = inverseRoot * target.rotation;
   string end = End(target.name);
   if (end != null && targets.TryGetValue(end, out var targetEnd) && rest.TryGetValue(Map(end) ?? "", out var sourceEnd))
   {
    Vector3 expected = sourceEnd.Position - bind.Position;
    Vector3 actual = inverseRoot * (targetEnd.position - target.position);
    if (expected.sqrMagnitude > .000001f && actual.sqrMagnitude > .000001f)
    {
     Quaternion align = Quaternion.FromToRotation(actual, expected);
     if (target.name is "LeftHand_jnt" or "RightHand_jnt")
     {
      string side = target.name.StartsWith("Left", StringComparison.Ordinal) ? "Left" : "Right";
      if (targets.TryGetValue(side + "Index1_jnt", out var index) && targets.TryGetValue(side + "Pinky1_jnt", out var pinky) &&
          rest.TryGetValue(Map(index.name), out var sourceIndex) && rest.TryGetValue(Map(pinky.name), out var sourcePinky))
      {
       Vector3 a = sourceIndex.Position - sourcePinky.Position;
       Vector3 b = inverseRoot * (index.position - pinky.position);
       if (Vector3.Cross(expected, a).sqrMagnitude > .0000001f && Vector3.Cross(actual, b).sqrMagnitude > .0000001f)
        align = Quaternion.LookRotation(expected, a) * Quaternion.Inverse(Quaternion.LookRotation(actual, b));
      }
     }
     targetRotation = align * targetRotation;
    }
   }
   links.Add(new Link(from, target, Quaternion.Inverse(bind.Rotation) * targetRotation, target.name == "Hips_jnt"));
  }
  if (links.Count < 15) throw new InvalidOperationException("Incomplete costume rig: " + links.Count);
  if (!headOnly) foreach (string side in new[] { "Left", "Right" })
   if (targets.TryGetValue(side + "UpArm_jnt", out var upper) &&
       targets.TryGetValue(side + "Elbow_jnt", out var elbow) &&
       targets.TryGetValue(side + "Hand_jnt", out var wrist) &&
       source.TryGetValue(Map(wrist.name), out var grip))
    arms.Add(new Arm(upper, elbow, wrist, grip));
  hipOffset = StandingOffset(targets, rest);
  Update();
 }

 private Vector3 StandingOffset(Dictionary<string, Transform> target, Dictionary<string, RestPose> source)
 {
  if (!target.TryGetValue("Hips_jnt", out var hip) || !source.TryGetValue("root", out var sourceHip)) return Vector3.zero;
  float difference = 0f;
  int count = 0;
  foreach (string side in new[] { "Left", "Right" })
  {
   if (!target.TryGetValue(side + "Toe_jnt", out var toe) || !source.TryGetValue(Map(toe.name), out var sourceToe)) continue;
   float sourceLength = sourceHip.Position.y - sourceToe.Position.y;
   float targetLength = playerRoot.InverseTransformPoint(hip.position).y - playerRoot.InverseTransformPoint(toe.position).y;
   difference += targetLength - sourceLength;
   count++;
  }
  return Vector3.up * (count == 0 ? 0f : Mathf.Clamp(difference / count, -.25f, .25f));
 }

 public void Update()
 {
  // Parents are visited before children. No foot-based pelvis correction runs
  // during a step, so switching the supporting leg cannot shift the whole body.
  foreach (var link in links)
  {
   if (link.Source == null || link.Target == null) continue;
   if (link.Root) link.Target.position = link.Source.position + playerRoot.TransformVector(hipOffset);
   link.Target.rotation = link.Source.rotation * link.Correction;
  }
  foreach (var arm in arms) AlignGrip(arm);
  if (sourceHead != null && targetHead != null) targetHead.position = sourceHead.position;
 }

 private static void AlignGrip(Arm arm)
 {
  var shoulder = arm.Upper.position;
  var elbow = arm.Elbow.position;
  var wrist = arm.Wrist.position;
  var requested = arm.Grip.position - shoulder;
  float upper = Vector3.Distance(shoulder, elbow), lower = Vector3.Distance(elbow, wrist);
  float reach = requested.magnitude;
  if (reach < .02f || upper < .02f || lower < .02f) return;
  var direction = requested / reach;
  var bend = Vector3.ProjectOnPlane(elbow - shoulder, direction);
  if (bend.sqrMagnitude < .000001f) return;
  reach = Mathf.Clamp(reach, Mathf.Abs(upper - lower) + .002f, upper + lower - .002f);
  float along = (upper * upper - lower * lower + reach * reach) / (2f * reach);
  float across = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
  var newElbow = shoulder + direction * along + bend.normalized * across;
  var newWrist = shoulder + direction * reach;
  var wristRotation = arm.Wrist.rotation;
  arm.Upper.rotation = Quaternion.FromToRotation(elbow - shoulder, newElbow - shoulder) * arm.Upper.rotation;
  arm.Elbow.rotation = Quaternion.FromToRotation(arm.Wrist.position - arm.Elbow.position, newWrist - arm.Elbow.position) * arm.Elbow.rotation;
  arm.Wrist.rotation = wristRotation;
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
