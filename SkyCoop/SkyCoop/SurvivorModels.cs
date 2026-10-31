using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using MelonLoader;

namespace SkyCoop;

// Authored faceted survivor meshes, skinned to the existing animation rig.
// No changes to hitboxes, animations, equipment sockets or network identities.
public static class SurvivorModels
{
	public static MelonPreferences_Entry<bool> Preference;
	private sealed class Visual
	{
		public GameObject root;
		public SkinnedMeshRenderer renderer;
		public Mesh mesh;
		public readonly List<Renderer> originals = new();
		public readonly List<bool> originalEnabled = new();
		public string signature;
	}
	private static readonly Dictionary<int, Visual> Visuals = new();
	private static readonly Dictionary<string, Material> Materials = new();
	public static void Cleanup()
	{
		foreach (int id in new List<int>(Visuals.Keys))
		{
			if (Visuals[id].root != null) continue;
			if (Visuals[id].mesh != null) UnityEngine.Object.Destroy(Visuals[id].mesh);
			Visuals.Remove(id);
		}
	}
	public static bool Enabled => Preference?.Value ?? false;
	public static void Toggle()
	{
		Preference.Value = !Enabled;
		foreach (var player in MyMod.players)
			if (player != null) player.GetComponent<Comps.MultiplayerPlayerClothingManager>()?.UpdateClothing();
		MelonPreferences.Save();
	}
	public static void Apply(GameObject player, string hat, string top, string bottom, string boots, string scarf, int character, bool force = false)
	{
		if (player == null) return;
		Cleanup();
		int id = player.GetInstanceID();
		Visuals.TryGetValue(id, out var visual);
		if (!Enabled && !force)
		{
			if (visual != null && visual.root != null)
			{
				visual.root.SetActive(false);
				for (int i = 0; i < visual.originals.Count; i++) if (visual.originals[i] != null) visual.originals[i].enabled = visual.originalEnabled[i];
			}
			return;
		}
		string signature = string.Join("|", hat, top, bottom, boots, scarf, character);
		if (visual != null && visual.root != null && visual.signature == signature)
		{
			visual.root.SetActive(true);
			foreach (var original in visual.originals) if (original != null) original.enabled = false;
			return;
		}
		try
		{
			if (visual == null || visual.root == null)
			{
				SkinnedMeshRenderer source = null;
				foreach (var candidate in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					if (candidate.sharedMesh != null && candidate.bones.Length > 30) { source = candidate; break; }
				}
				if (source == null) return;
				visual = new Visual();
				var builder = new Builder(player.transform, source);
				visual.mesh = builder.Build(character, top, bottom, boots, hat, scarf);
				visual.root = new GameObject("SkyCoop_SurvivorModel");
				visual.root.transform.SetParent(player.transform, false);
				visual.renderer = visual.root.AddComponent<SkinnedMeshRenderer>();
				visual.renderer.sharedMesh = visual.mesh;
				visual.renderer.bones = source.bones;
				visual.renderer.rootBone = source.rootBone;
				visual.renderer.sharedMaterials = new Il2CppReferenceArray<Material>(builder.materials.ToArray());
				visual.renderer.updateWhenOffscreen = true;
				visual.renderer.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(3, 4, 3));
				for (int group = 0; group < Math.Min(2, player.transform.childCount - 1); group++)
				foreach (var original in player.transform.GetChild(group).GetComponentsInChildren<Renderer>(true))
				{
					visual.originals.Add(original);
					visual.originalEnabled.Add(original.enabled);
				}
				Visuals[id] = visual;
				visual.signature = signature;
				MelonLogger.Msg("[Survivor model] Built skinned model for " + player.name + ", vertices=" + visual.mesh.vertexCount);
			}
			else
			{
				// Rebuild only when the visible equipment changes.
				var source = visual.originals.Find(r => r != null && r.TryCast<SkinnedMeshRenderer>() != null)?.TryCast<SkinnedMeshRenderer>();
				if (source == null) return;
				var builder = new Builder(player.transform, source);
				var replacement = builder.Build(character, top, bottom, boots, hat, scarf);
				visual.renderer.sharedMesh = replacement;
				visual.renderer.sharedMaterials = new Il2CppReferenceArray<Material>(builder.materials.ToArray());
				UnityEngine.Object.Destroy(visual.mesh);
				visual.mesh = replacement;
				visual.signature = signature;
			}
			visual.root.SetActive(true);
			foreach (var original in visual.originals) if (original != null) original.enabled = false;
		}
		catch (Exception ex) { MelonLogger.Warning("[Survivor model] Keeping original: " + ex.Message); }
	}

	private sealed class Builder
	{
		private readonly Dictionary<string, int> boneIds = new(StringComparer.Ordinal);
		private readonly Matrix4x4[] binds;
		private readonly Matrix4x4 convert;
		private readonly List<Vector3> vertices = new();
		private readonly List<BoneWeight> weights = new();
		private readonly List<List<int>> triangles = new();
		private readonly List<Color> colors = new();
		public readonly List<Material> materials = new();
		private readonly Material template;
		private Vector3 right, up, forward;
		public Builder(Transform player, SkinnedMeshRenderer source)
		{
			convert = player.worldToLocalMatrix * source.transform.localToWorldMatrix;
			binds = new Matrix4x4[source.bones.Length];
			var original = source.sharedMesh.bindposes;
			for (int i = 0; i < binds.Length; i++)
			{
				boneIds[source.bones[i].name] = i;
				binds[i] = original[i] * convert.inverse;
			}
			template = source.sharedMaterial;
			right = (P("arms_r_01") - P("arms_l_01")).normalized;
			up = (P("Neck") - P("root")).normalized;
			forward = Vector3.Cross(right, up).normalized;
		}
		private int Bone(string name) => boneIds.TryGetValue(name, out int id) ? id : throw new Exception("Missing rig bone " + name);
		private Vector3 P(string name) => binds[Bone(name)].inverse.MultiplyPoint3x4(Vector3.zero);
		private int Mat(string hex)
		{
			if (!Materials.TryGetValue(hex, out var material) || material == null)
			{
				ColorUtility.TryParseHtmlString(hex, out var color);
				var shader = Shader.Find("Standard") ?? template.shader;
				material = new Material(shader) { name = "Survivor_" + hex, color = color };
				if (material.HasProperty("_MainTex")) material.mainTexture = Texture2D.whiteTexture;
				if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .06f);
				Materials[hex] = material;
			}
			int index = materials.IndexOf(material);
			if (index < 0) { index = materials.Count; materials.Add(material); colors.Add(material.color); triangles.Add(new List<int>()); }
			return index;
		}
		private void Tri(Vector3 a, Vector3 b, Vector3 c, BoneWeight wa, BoneWeight wb, BoneWeight wc, int material)
		{
			// Broad painted light planes also work with the game's ambient-only outdoor lighting.
			float light = Vector3.Dot(Vector3.Cross(b-a,c-a).normalized,new Vector3(-.4f,.8f,.5f).normalized);
			float shade = light > .65f ? 1.07f : light > .1f ? 1f : light > -.45f ? .86f : .73f;
			var color = colors[material];
			color.r = Mathf.Clamp01(color.r * shade); color.g = Mathf.Clamp01(color.g * shade); color.b = Mathf.Clamp01(color.b * shade);
			material = Mat("#" + ColorUtility.ToHtmlStringRGB(color));
			int start = vertices.Count;
			vertices.Add(a); vertices.Add(b); vertices.Add(c);
			weights.Add(wa); weights.Add(wb); weights.Add(wc);
			triangles[material].Add(start); triangles[material].Add(start + 1); triangles[material].Add(start + 2);
		}
		private BoneWeight W(string bone) => new() { boneIndex0 = Bone(bone), weight0 = 1 };
		private void Panel(Vector3 center, float width, float height, string color, string bone)
		{
			var x = right * width * .5f; var y = up * height * .5f;
			int mat = Mat(color); var w = W(bone);
			Tri(center-x-y,center+x-y,center+x+y,w,w,w,mat);
			Tri(center-x-y,center+x+y,center-x+y,w,w,w,mat);
		}
		private void Rings(Vector3[] centers, float[] widths, float[] depths, string[] bones, string color, Vector3 x, Vector3 z, int sides = 12)
		{
			int mat = Mat(color);
			var rings = new Vector3[centers.Length, sides];
			for (int i = 0; i < centers.Length; i++) for (int j = 0; j < sides; j++)
			{
				float angle = j * Mathf.PI * 2 / sides;
				rings[i,j] = centers[i] + x * (Mathf.Cos(angle) * widths[i]) + z * (Mathf.Sin(angle) * depths[i]);
			}
			for (int i = 0; i < centers.Length - 1; i++) for (int j = 0; j < sides; j++)
			{
				int k = (j + 1) % sides;
				Tri(rings[i,j], rings[i+1,j], rings[i,k], W(bones[i]), W(bones[i+1]), W(bones[i]), mat);
				Tri(rings[i,k], rings[i+1,j], rings[i+1,k], W(bones[i]), W(bones[i+1]), W(bones[i+1]), mat);
			}
			for (int j=0;j<sides;j++)
			{
				int k=(j+1)%sides, end=centers.Length-1;
				Tri(centers[0],rings[0,j],rings[0,k],W(bones[0]),W(bones[0]),W(bones[0]),mat);
				Tri(centers[end],rings[end,k],rings[end,j],W(bones[end]),W(bones[end]),W(bones[end]),mat);
			}
		}
		private void Limb(string a, string b, float ra, float rb, string color)
		{
			var axis = (P(b)-P(a)).normalized;
			var x = Vector3.Cross(axis,forward).normalized;
			var z = Vector3.Cross(x,axis).normalized;
			Rings(new[]{P(a),Vector3.Lerp(P(a),P(b),.55f),P(b)},new[]{ra,(ra+rb)*.52f,rb},new[]{ra*.9f,(ra+rb)*.46f,rb*.9f},new[]{a,a,b},color,x,z);
		}
		public Mesh Build(int character, string coat, string bottom, string boots, string hat, string scarf)
		{
			bool female = character == 1 || character == 3;
			string fabric = coat.Contains("Bear") ? "#594B3C" : coat.Contains("Wolf") ? "#62615B" : coat.Contains("Down") || coat.Contains("Parka") ? "#A05D3F" : coat.Contains("Shirt") ? "#5A6F68" : "#69775F";
			string trouser = bottom.Contains("Deer") ? "#807053" : bottom.Contains("Combat") || bottom.Contains("Cargo") ? "#59634B" : "#424F55";
			string leather = boots.Contains("Deer") || boots.Contains("Mukluk") ? "#827056" : "#4A4038";
			float waist = female ? .145f : .17f;
			var hip=P("root"); var chest=P("Chest"); var head=P("Head");
			Rings(new[]{hip-up*.18f,hip, P("Spine1"),P("Spine2"),chest,P("Neck")-up*.025f},new[]{.215f,.205f,waist,.205f,.23f,.08f},new[]{.13f,.135f,.115f,.125f,.14f,.065f},new[]{"root","root","Spine1","Spine2","Chest","Chest"},fabric,right,forward);
			// Stitched front placket and layered pocket flaps keep the coat readable at a distance.
			Panel(hip+up*.05f+forward*.14f,.025f,.24f,"#534B43","root");
			Panel(P("Spine1")+up*.07f+forward*.13f,.025f,.23f,"#534B43","Spine1");
			Panel(P("Spine2")+up*.06f+forward*.145f,.025f,.20f,"#534B43","Spine2");
			foreach(float sign in new[]{-1f,1f})
			{
				Panel(hip+up*.055f+right*(sign*.115f)+forward*.12f,.11f,.105f,"#76523E","root");
				Panel(hip+up*.105f+right*(sign*.115f)+forward*.125f,.12f,.025f,"#B08059","root");
				Panel(P("Spine2")+up*.04f+right*(sign*.11f)+forward*.12f,.09f,.075f,"#76523E","Spine2");
			}
			foreach (var side in new[]{"l","r"})
			{
				Limb("arms_"+side+"_01","arms_"+side+"_02",.09f,.07f,fabric);
				Limb("arms_"+side+"_02","hand_"+side,.072f,.048f,fabric);
				Limb("hand_"+side,"middle_"+side+"_1",.048f,.036f,"#4A4038");
			}
			foreach (var side in new[]{"L","R"})
			{
				Limb("Thigh."+side,"Shin."+side,.102f,.072f,trouser);
				Limb("Shin."+side,"Foot."+side,.073f,.052f,trouser);
				var ankle=P("Foot."+side);var toe=P("Toe."+side);
				Rings(new[]{toe+forward*.045f-up*.02f,ankle-up*.035f,ankle+up*.13f},new[]{.073f,.075f,.069f},new[]{.065f,.095f,.068f},new[]{"Foot."+side,"Foot."+side,"Foot."+side},leather,right,forward);
			}
			Rings(new[]{P("Neck")-up*.025f,head+up*.025f},new[]{.049f,.05f},new[]{.045f,.046f},new[]{"Neck","Head"},"#C39D7B",right,forward);
			Rings(new[]{head-up*.035f,head+up*.005f,head+up*.065f,head+up*.125f,head+up*.18f,head+up*.21f},new[]{.048f,.065f,.08f,.083f,.068f,.03f},new[]{.045f,.066f,.074f,.079f,.07f,.03f},new[]{"Head","Head","Head","Head","Head","Head"},"#C39D7B",right,forward);
			// Faceted nose, brow and eyes; no external texture dependency.
			int skin=Mat("#B48969"), dark=Mat("#3A302A"); var w=W("Head");
			var nose=head+up*.083f+forward*.103f;
			Tri(head+up*.12f+forward*.076f-right*.015f,head+up*.06f+forward*.075f-right*.018f,nose,w,w,w,skin);
			Tri(head+up*.12f+forward*.076f+right*.015f,nose,head+up*.06f+forward*.075f+right*.018f,w,w,w,skin);
			Panel(head+up*.035f+forward*.069f,.043f,.007f,"#78594B","Head");
			foreach(float sign in new[]{-1f,1f})
			{
				var eye=head+up*.115f+forward*.074f+right*(sign*.035f);
				Tri(eye-right*.019f,eye+right*.019f,eye+up*.008f,w,w,w,dark);
				Panel(eye+up*.017f,.041f,.009f,"#4B392E","Head");
			}
			Rings(new[]{head+up*.157f,head+up*.205f,head+up*.235f},new[]{.083f,.071f,.027f},new[]{.079f,.075f,.03f},new[]{"Head","Head","Head"},hat.Length>0?"#8C674A":"#4B392E",right,forward);
			if(scarf.Length>0) Rings(new[]{P("Neck")-up*.035f,P("Neck")+up*.04f},new[]{.083f,.08f},new[]{.075f,.075f},new[]{"Neck","Neck"},"#9F6D4D",right,forward);
			var mesh=new Mesh {name="SkyCoop_Survivor_v1"};
			mesh.vertices=new Il2CppStructArray<Vector3>(vertices.ToArray());
			mesh.boneWeights=new Il2CppStructArray<BoneWeight>(weights.ToArray());
			mesh.bindposes=new Il2CppStructArray<Matrix4x4>(binds);
			mesh.subMeshCount=triangles.Count;
			for(int i=0;i<triangles.Count;i++)mesh.SetTriangles(new Il2CppStructArray<int>(triangles[i].ToArray()),i);
			mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
		}
	}
}
