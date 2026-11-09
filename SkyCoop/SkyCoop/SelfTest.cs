using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Developer self-test, off unless SKYCOOP_SELFTEST_SLOT is set (slot name like sandbox16 or its display name):
// loads that save from the main menu and starts a host (plain UDP server from server.json defaults), so host-side
// start-up can be reproduced without a second player or clicking through the menus.
public static class SelfTest
{
	private static string slot;

	private static bool started;

	private static float menuSince = -1f;
	private static bool dumpedAvatar;
	private static bool previewCreated;
	private static bool nativePreviewCreated;
	private static bool nativeAssetsProbed;
	private static float balanceSampleAt=-1, balanceFatigue;
	private static bool ModelOnly => Environment.GetEnvironmentVariable("SKYCOOP_MODEL_ONLY_TEST")=="1";
	private static Vector3 previewTarget;
	private static bool previewTargetSet;
	private static float previewSince;
	private static bool rendered;
	private static Camera probeCamera;
	private static string AvatarWorldOutput => Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_WORLD_TEST");
	private static int avatarStep;
	private static float avatarNext;
	private static int avatarMovement;
	private static bool walked, ran;
	private static float cameraOffsetVariation;
	private static Vector3 previousCameraOffset;
	private static bool sampledCameraOffset;
	private static float probeTopSpeed;
	private static Vector3 probeStartPosition;
	private static GameObject wardrobeTestAvatar;
	private static readonly string[][] WardrobeOutfits =
	{
		new[] { "GEAR_DownParka", "GEAR_InsulatedPants", "GEAR_InsulatedBoots", "GEAR_BasicWoolHat", "GEAR_SkiGloves" },
		new[] { "GEAR_MilitaryParka", "GEAR_Jeans", "GEAR_BasicShoes", "GEAR_BasicWoolScarf", "GEAR_WorkGloves" },
		new[] { "GEAR_CottonShirt", "GEAR_CargoPants", "GEAR_WorkBoots" }
	};
	private static string RestRender => Environment.GetEnvironmentVariable("SKYCOOP_REST_RENDER");

	private static void WearProbeOutfit(string[] outfit)
	{
		var manager = GameManager.GetPlayerManagerComponent();
		manager.UnequipImmediate(false);
		foreach (var region in new[] { ClothingRegion.Head, ClothingRegion.Chest, ClothingRegion.Legs, ClothingRegion.Feet, ClothingRegion.Hands })
			for (int layer = (int)ClothingLayer.Base; layer < (int)ClothingLayer.NumLayers; layer++)
				manager.GetClothingInSlot(region, (ClothingLayer)layer)?.m_ClothingItem?.TakeOff();
		foreach (string name in outfit)
		{
			var inventory = GameManager.GetInventoryComponent();
			if (inventory.GetBestGearItemWithName(name) == null) CheatConsole.GiveItem(name, 1);
			var item = inventory.GetBestGearItemWithName(name)?.m_ClothingItem;
			if (item == null) throw new InvalidOperationException("Missing fixture garment " + name);
			item.PutOn(item.m_MinLayer);
		}
		MyMod.UpdateMyClothing(publish: false);
		using var packet = new GameServer.Packet();
		packet.Write(MyMod.LocalClothing());
		using var input = new GameServer.Packet(packet.ToArray());
		var decoded = input.ReadClothingData();
		MelonLogger.Msg($"[Avatar world] decoded character={decoded.m_Appearance} coat={decoded.m_Top} " +
			$"pants={decoded.m_Bottom} boots={decoded.m_Boots} hat={decoded.m_Hat} scarf={decoded.m_Scarf} gloves={decoded.m_Gloves}");
	}

	private static void CheckAvatarInWorld()
	{
		string output = AvatarWorldOutput;
		if (string.IsNullOrEmpty(output) || !GearCatalogIndex.InWorld || GameManager.m_PlayerObject == null) return;
		if (avatarMovement > 0 && AvatarPreview.CameraApplied)
		{
			walked |= MyMod.MyAnimState == "Walk";
			ran |= MyMod.MyAnimState == "Run";
			var capsule = GameManager.GetVpFPSPlayer()?.Controller?.m_Controller;
			if (capsule != null)
			{
				var heading = Vector3.ProjectOnPlane(GameManager.GetPlayerTransform().forward, Vector3.up).normalized;
				GameManager.GetPlayerManagerComponent().m_InRunMode = avatarMovement == 2;
				capsule.Move(heading * (avatarMovement == 2 ? 3.1f : 1.35f) * Time.deltaTime);
				probeTopSpeed = Mathf.Max(probeTopSpeed, capsule.velocity.magnitude);
			}
			var offset = AvatarPreview.LastRelativeEye;
			if (sampledCameraOffset) cameraOffsetVariation = Mathf.Max(cameraOffsetVariation, Vector3.Distance(previousCameraOffset, offset));
			previousCameraOffset = offset;
			sampledCameraOffset = true;
		}
		if (Time.realtimeSinceStartup < avatarNext) return;
		if (avatarStep < 12)
		{
			int outfit = avatarStep / 2 % 3;
			bool female = avatarStep >= 6;
			if (avatarStep % 2 == 0)
			{
				NativeAppearance.Select(female ? NativeAppearance.FemalePreset : NativeAppearance.MalePreset, persist: false);
				WearProbeOutfit(WardrobeOutfits[outfit]);
				AvatarPreview.Front();
				avatarNext = Time.realtimeSinceStartup + 1.4f;
			}
			else
			{
				var doll = AvatarPreview.Model;
				wardrobeTestAvatar ??= doll;
				float facing = Vector3.Dot(AvatarPreview.LastEye - doll.transform.position, doll.transform.forward);
				MelonLogger.Msg($"[Avatar world] front={facing > .4f && AvatarPreview.CameraApplied} face={NativeAppearance.HasCostume(doll)} sameAvatar={doll == wardrobeTestAvatar}");
				CaptureAvatarCamera(output + (female ? "-astrid-" : "-will-") + (outfit + 1) + ".png");
			}
			avatarStep++;
			return;
		}
		if (avatarStep == 12)
		{
			AvatarPreview.Back();
			probeStartPosition = GameManager.GetPlayerTransform().position;
			avatarMovement = 1;
			avatarNext = Time.realtimeSinceStartup + 1.4f;
			avatarStep++;
			return;
		}
		if (avatarStep == 13)
		{
			CaptureAvatarCamera(output + "-walk.png");
			avatarMovement = 2;
			avatarNext = Time.realtimeSinceStartup + 1.8f;
			avatarStep++;
			return;
		}
		if (avatarStep == 14)
		{
			CaptureAvatarCamera(output + "-run.png");
			avatarMovement = 0;
			MelonLogger.Msg("[Avatar world] Walk=" + walked + " Run=" + ran +
				" distance=" + Vector3.Distance(probeStartPosition, GameManager.GetPlayerTransform().position).ToString("F2") +
				" m speed=" + probeTopSpeed.ToString("F2") +
				" m/s maximum camera offset change=" + cameraOffsetVariation.ToString("F4") + " m");
			AvatarPreview.ToggleBody();
			avatarNext = Time.realtimeSinceStartup + 1f;
			avatarStep++;
			return;
		}
		if (avatarStep == 15)
		{
			var doll = AvatarPreview.Model;
			int legs = 0;
			foreach (var renderer in doll.transform.GetChild(0).GetChild(2).GetComponentsInChildren<Renderer>())
				if (!renderer.forceRenderingOff && renderer.enabled) legs++;
			MelonLogger.Msg("[Avatar world] first-person trouser renderers=" + legs);
			CaptureAvatarCamera(output + "-first-person.png", lookDown: true);
			avatarStep++;
			return;
		}
		Application.Quit();
	}


	private static void CaptureAvatarCamera(string path, bool lookDown = false)
	{
		var controller = GameManager.GetVpFPSCamera();
		AvatarPreview.ApplyCamera(controller);
		var view = controller.GetCameraComponent();
		var active = RenderTexture.active;
		var original = view.targetTexture;
		var target = RenderTexture.GetTemporary(1600, 1000, 24, RenderTextureFormat.ARGB32);
		var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
		try
		{
			view.targetTexture = target;
			if (lookDown)
			{
				var eye = view.transform.position;
				var rotation = Quaternion.LookRotation((Vector3.down + AvatarPreview.Model.transform.forward * .36f).normalized, Vector3.up);
				view.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(eye, rotation, Vector3.one).inverse;
			}
			view.Render();
			RenderTexture.active = target;
			image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
			image.Apply();
			System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
		}
		finally
		{
			view.targetTexture = original;
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(target);
			UnityEngine.Object.Destroy(image);
			AvatarPreview.RestoreCamera();
			if (lookDown) view.ResetWorldToCameraMatrix();
		}
	}

	private static void BuildModelFixture()
	{
		if (Environment.GetEnvironmentVariable("TLD_WARDROBE_PROBE") == "1")
		{
			nativePreviewCreated = true;
			WardrobeProbe.Run(Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER"));
			Application.Quit();
			return;
		}
		bool alignment=Environment.GetEnvironmentVariable("SKYCOOP_MODEL_ALIGNMENT_TEST")=="1";
		var prefab=MyMod.BundleAsset<GameObject>("multiplayerPlayer");if(prefab==null)return;
		nativePreviewCreated=true;
		probeCamera=new GameObject("QA_ModelCamera").AddComponent<Camera>();probeCamera.enabled=false;probeCamera.transform.position=new Vector3(0,1.1f,0);probeCamera.fieldOfView=50;probeCamera.aspect=1.6f;probeCamera.nearClipPlane=.05f;probeCamera.farClipPlane=30;probeCamera.cullingMask=1<<30;probeCamera.clearFlags=CameraClearFlags.SolidColor;probeCamera.backgroundColor=new Color(.18f,.2f,.23f);
		var light=new GameObject("QA_ModelLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,-25,0);
		RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
		if (Environment.GetEnvironmentVariable("SKYCOOP_MODEL_SWITCHING_TEST") == "1")
		{
			MelonCoroutines.Start(CheckModelSwitching(prefab, probeCamera));
			return;
		}
		var avatars = new List<GameObject>();
		for(int choice=0;choice<6;choice++)
		{
			var avatar=UnityEngine.Object.Instantiate(prefab,new Vector3((choice-2.5f)*1.22f,0,5.8f),Quaternion.Euler(0,choice >= 4 ? 140f : 180f,0));
			avatars.Add(avatar);
			foreach(var collider in avatar.GetComponentsInChildren<Collider>(true))collider.enabled=false;
			var wardrobe=avatar.AddComponent<Comps.MultiplayerPlayerClothingManager>();
			wardrobe.enabled=false;
			int appearance = choice < 4 ? -1 : (choice % 2 == 0 ? NativeAppearance.MalePreset : NativeAppearance.FemalePreset);
				wardrobe.ApplyLocalAppearance(avatar, appearance, choice % 2,
				new DataStr.PlayerClothingData
				{
					m_Top = choice == 4 ? "GEAR_DownParka" : choice == 5 ? "GEAR_MilitaryParka" : new[] { "GEAR_DownParka", "GEAR_MilitaryParka", "" }[choice / 2],
					m_Hat = choice < 2 ? "GEAR_BasicWoolHat" : "",
					m_Boots = choice < 2 ? "GEAR_InsulatedBoots" : "GEAR_BasicShoes",
					m_Bottom = choice < 2 ? "GEAR_InsulatedPants" : "GEAR_Jeans",
					m_Scarf = choice >= 2 && choice < 4 ? "GEAR_BasicWoolScarf" : "",
					m_Gloves = choice < 4 ? "GEAR_SkiGloves" : ""
				});
			foreach(var t in avatar.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
			var animator = avatar.GetComponent<Animator>();
			animator.applyRootMotion = false;
			string baseAnim = alignment ? "Idle" : choice >= 4 ? "Ctrl" : new[]{"Idle","Walk","Run","Ctrl"}[choice];
				animator.Play(baseAnim, 0, 0);
			if (choice >= 4)
			{
				bool rifle = choice == 4;
				var (right, left) = Comps.MultiplayerPlayer.HandSockets(avatar);
				Comps.MultiplayerPlayer.ShowHeldItem(right, left, rifle ? "GEAR_Rifle" : "GEAR_Revolver", "", false, "Idle");
				animator.Play(rifle ? "RifleAim" : "HoldGun", 1, 0);
				if (!rifle) animator.Play("HoldRevolver", 2, 0);
			}
			animator.Update(.3f);
		}
		for (int i = 0; i < avatars.Count; i++) DumpActiveRenderers(avatars[i], "choice=" + i);
		previewTarget=new Vector3(0,1.05f,5.8f);previewTargetSet=true;previewSince=Time.realtimeSinceStartup;
	}

	private static System.Collections.IEnumerator CheckModelSwitching(GameObject prefab, Camera camera)
	{
		string output = Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER");
		string prefix = System.IO.Path.ChangeExtension(output, null);
		var report = new System.Text.StringBuilder();
		int failures = 0;
		void Check(bool pass, string message)
		{
			report.AppendLine((pass ? "PASS " : "FAIL ") + message);
			if (!pass) failures++;
		}
		for (int character = 0; character < 2; character++)
		{
			string sex = character == 0 ? "classic-male" : "classic-female";
			int story = character == 0 ? NativeAppearance.MalePreset : NativeAppearance.FemalePreset;
			string coat = character == 0 ? "GEAR_DownParka" : "GEAR_MilitaryParka";
			var outfit = new DataStr.PlayerClothingData { m_Top = coat, m_Bottom = "GEAR_Jeans", m_Boots = "GEAR_InsulatedBoots" };
			var avatar = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
			var wardrobe = avatar.AddComponent<Comps.MultiplayerPlayerClothingManager>();
			wardrobe.enabled = false;
			foreach (var collider in avatar.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
			var animator = avatar.GetComponent<Animator>();
			animator.applyRootMotion = false;
			animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			var clothing = avatar.transform.GetChild(0);
			var preDisabled = clothing.GetChild(1).GetChild(0).GetComponentInChildren<SkinnedMeshRenderer>(true);
			if (preDisabled != null) preDisabled.enabled = false;

			var classicIds = new HashSet<int>();
			Transform previousStory = null;
			for (int stage = 0; stage < 5; stage++)
			{
				bool storyMode = stage % 2 == 1;
				bool changed = wardrobe.ApplyLocalAppearance(avatar, storyMode ? story : -1, character, outfit);
				Transform activeStory = null;
				int storyRoots = 0;
				foreach (var t in avatar.GetComponentsInChildren<Transform>(true))
				{
					t.gameObject.layer = 30;
					if (t.name != "TLDCoop_NativeCostume" || !t.gameObject.activeInHierarchy) continue;
					activeStory = t;
					storyRoots++;
				}
				Check(changed, $"{sex} stage {stage}: same outfit, switch applied");
				Check(NativeAppearance.HasCostume(avatar) == storyMode && storyRoots == (storyMode ? 1 : 0),
					$"{sex} stage {stage}: active WINTERMUTE roots={storyRoots}");
				if (!storyMode && stage > 0)
					Check(previousStory == null || !previousStory.gameObject.activeInHierarchy, $"{sex} stage {stage}: previous story root inactive");

				report.AppendLine($"[Active renderers] {sex} stage {stage} {(storyMode ? "WINTERMUTE" : "classic")}");
				var activeIds = new HashSet<int>();
				var head = avatar.transform.GetChild(1).GetChild(1).GetChild(character);
				int topCount = 0, detailedTops = 0, bottomCount = 0, detailedBottoms = 0, bootCount = 0, detailedBoots = 0, heads = 0;
				foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
				{
					if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
					activeIds.Add(renderer.GetInstanceID());
					var mesh = renderer.TryCast<SkinnedMeshRenderer>()?.sharedMesh ?? renderer.GetComponent<MeshFilter>()?.sharedMesh;
					string path = renderer.name;
					for (var parent = renderer.transform.parent; parent != null && parent != avatar.transform; parent = parent.parent)
						path = parent.name + "/" + path;
					report.AppendLine($"  {path} | {mesh?.name ?? "(no mesh)"} | {mesh?.vertexCount ?? 0} vertices");
					if (renderer.transform.IsChildOf(clothing.GetChild(1)))
					{
						topCount++;
						if (mesh?.name == coat + " · co-op skin") detailedTops++;
					}
					if (renderer.transform.IsChildOf(clothing.GetChild(2)))
					{
						bottomCount++;
						if (mesh?.name == "GEAR_Jeans · co-op skin") detailedBottoms++;
					}
					if (renderer.transform.IsChildOf(clothing.GetChild(3)))
					{
						bootCount++;
						if (mesh?.name == "GEAR_InsulatedBoots · co-op skin") detailedBoots++;
					}
					if (renderer.transform.IsChildOf(head)) heads++;
				}
				if (storyMode)
				{
					Check(!activeIds.Overlaps(classicIds) && activeIds.Count > 0, $"{sex} stage {stage}: classic renderers hidden");
					previousStory = activeStory;
				}
				else
				{
					Check(topCount == 1 && detailedTops == 1 && bottomCount == 1 && detailedBottoms == 1 &&
						bootCount == 1 && detailedBoots == 1, $"{sex} stage {stage}: one detailed renderer per tested clothing slot");
					Check(heads > 0, $"{sex} stage {stage}: classic head={head.name}, visible renderers={heads}");
					if (stage == 0) classicIds = activeIds;
					else Check(classicIds.SetEquals(activeIds) && preDisabled != null && !preDisabled.enabled,
						$"{sex} stage {stage}: exact active set restored; disabled renderer stayed disabled");
				}
			}
			animator.Play("Idle", 0, .22f);
			animator.Update(0f);
			NativeAppearance.LateUpdate();
			CaptureModelSwitch(camera, prefix + "-" + sex + "-front.jpg", new Vector3(0, 1.05f, 2.85f), .97f);
			CaptureModelSwitch(camera, prefix + "-" + sex + "-side.jpg", new Vector3(2.85f, 1.05f, 0), .97f);
			Transform headBone = null;
			var coatSkin = clothing.GetChild(1).GetComponentInChildren<SkinnedMeshRenderer>(false);
			foreach (var bone in coatSkin.bones)
				if (bone.name == "Head") { headBone = bone; break; }
			float standingHeight = headBone?.position.y ?? 0f;
			animator.Play("Ctrl", 0, 0f);
			float headDrop = 0f;
			float deadline = Time.realtimeSinceStartup + 2f;
			while (Time.realtimeSinceStartup < deadline && headDrop < .28f)
			{
				yield return null;
				headDrop = standingHeight - (headBone?.position.y ?? standingHeight);
			}
			Check(headDrop >= .28f, $"{sex}: crouch head lowered by {headDrop:F2} m");
			CaptureModelSwitch(camera, prefix + "-" + sex + "-crouch.jpg", new Vector3(1.25f, .90f, 2.6f), .74f);
			avatar.SetActive(false);
			UnityEngine.Object.Destroy(avatar);
		}
		report.AppendLine($"Switching result: {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
		System.IO.File.WriteAllText(output, report.ToString());
		Application.Quit();
	}

	private static void CaptureModelSwitch(Camera camera, string output, Vector3 eye, float center)
	{
		camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(Vector3.up * center - eye));
		camera.fieldOfView = 46f;
		var previous = RenderTexture.active;
		var target = RenderTexture.GetTemporary(960, 1200, 24, RenderTextureFormat.ARGB32);
		var image = new Texture2D(960, 1200, TextureFormat.RGB24, false);
		try
		{
			camera.aspect = .8f;
			camera.targetTexture = target;
			camera.Render();
			RenderTexture.active = target;
			image.ReadPixels(new Rect(0, 0, 960, 1200), 0, 0, false);
			image.Apply();
			System.IO.File.WriteAllBytes(output, image.EncodeToJPG(92));
		}
		finally
		{
			camera.targetTexture = null;
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(target);
			UnityEngine.Object.Destroy(image);
		}
	}

	private static void DumpActiveRenderers(GameObject avatar, string label)
	{
		var sb = new System.Text.StringBuilder();
		sb.AppendLine("[Active meshes] " + label);
		foreach (var r in avatar.GetComponentsInChildren<Renderer>(true))
		{
			if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
			if (r is SkinnedMeshRenderer smr)
				sb.AppendLine($"  SMR {smr.name} mesh={(smr.sharedMesh != null ? smr.sharedMesh.name : "null")} verts={(smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0)}");
			else if (r is MeshRenderer mr && mr.TryGetComponent<MeshFilter>(out var mf))
				sb.AppendLine($"  MR {mr.name} mesh={(mf.sharedMesh != null ? mf.sharedMesh.name : "null")}");
		}
		UnityEngine.Debug.Log(sb.ToString());
	}
	public static void LateUpdate()
	{
		if(ModelOnly&&previewTargetSet&&probeCamera!=null)
		{
			var camera=probeCamera;
			camera.transform.rotation=Quaternion.LookRotation(previewTarget-camera.transform.position,Vector3.up);
			if(!rendered&&Time.realtimeSinceStartup-previewSince>7)
			{
				rendered=true;
				var saved=camera.targetTexture;var active=RenderTexture.active;
				var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32);
				var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
				try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,900),0,0,false);texture.Apply();System.IO.File.WriteAllBytes(Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER")??"model-probe.jpg",texture.EncodeToJPG(85));MelonLogger.Msg("[Model probe] Render saved; automatic exit");}
				finally{camera.targetTexture=saved;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(texture);Application.Quit();}
			}
		}
	}

	private static void RenderRestPose(string path)
	{
		var prefab = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
		var center = new Vector3(0f, 1f, 4f);
		var avatar = UnityEngine.Object.Instantiate(prefab, center, Quaternion.identity);
		avatar.SetActive(true);
		foreach (var bone in avatar.GetComponentsInChildren<Transform>(true)) bone.gameObject.layer = 30;
		BedRest.FreezeRestAnimation(avatar);
		var rig = new BedRest.RestRig(avatar.transform);
		rig.Align(center, Quaternion.identity);
		var camera = new GameObject("Rest probe camera").AddComponent<Camera>();
		camera.enabled = false;
		camera.fieldOfView = 60f;
		camera.aspect = 16f / 9f;
		camera.nearClipPlane = .05f;
		camera.cullingMask = 1 << 30;
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = new Color(.22f, .23f, .27f);
		var light = new GameObject("Rest probe light").AddComponent<Light>();
		light.type = LightType.Directional;
		light.intensity = 2f;
		light.cullingMask = 1 << 30;
		light.transform.rotation = Quaternion.Euler(45, 30, 0);
		var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
		bed.layer = 30;
		bed.transform.position = center + new Vector3(0f, -.09f, .10f);
		bed.transform.localScale = new Vector3(.8f, .12f, 2.1f);
		var rt = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
		var saved = RenderTexture.active;
		var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
		void Capture(string suffix, Vector3 position, Quaternion rotation)
		{
			camera.transform.SetPositionAndRotation(position, rotation);
			camera.targetTexture = rt;
			camera.Render();
			RenderTexture.active = rt;
			pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
			pixels.Apply();
			System.IO.File.WriteAllBytes(path + suffix + ".jpg", pixels.EncodeToJPG(88));
		}
		try
		{
			var view = BedRest.FirstPerson(rig, center, Quaternion.identity);
			Capture("-first", view.Position, view.Rotation);
			var side = center + new Vector3(2.3f, 1.1f, .25f);
			Capture("-side", side, Quaternion.LookRotation(center - side));
			MelonLogger.Msg("[Rest probe] PASS first-person and observer renders, head=" + (rig.Head != null) + " root=" + rig.Root.position + " headAt=" + rig.Head?.position + " eye=" + view.Position);
		}
		finally
		{
			camera.targetTexture = null;
			RenderTexture.active = saved;
			RenderTexture.ReleaseTemporary(rt);
			UnityEngine.Object.Destroy(pixels);
		}
	}

	private static void ProbeFlareAndBed()
	{
		int previousMinute = MyMod.MinutesFromStartServer;
		try
		{
			MyMod.MinutesFromStartServer = 103;
			ThrownFlareSync.ClockSync();
			var flare = new DataStr.ExtraDataForDroppedGear { m_GearName = "GEAR_BlueFlare", m_Variant = 1,
				m_DroppedTime = 100, m_FlareRemainingMinutes = 12.5f, m_FlareDropPhase = .25f };
			var pickup = ThrownFlareSync.SnapshotPickup(flare);
			if (Mathf.Abs(pickup.m_FlareRemainingMinutes - 9.75f) > .05f || flare.m_FlarePickupSnapshot)
				throw new InvalidOperationException("Flare ground aging or pickup snapshot failed");
			MyMod.MinutesFromStartServer = 400;
			ThrownFlareSync.ClockSync();
			if (ThrownFlareSync.Remaining(pickup) <= 0f || ThrownFlareSync.SnapshotPickup(flare).m_Variant != 2)
				throw new InvalidOperationException("Flare pickup depends on client clock, or ground expiry failed");
		}
		finally { MyMod.MinutesFromStartServer = previousMinute; ThrownFlareSync.ClockSync(); }
		var mattress = GameObject.CreatePrimitive(PrimitiveType.Cube);
		try
		{
			mattress.transform.localScale = new Vector3(2f, .15f, .65f);
			mattress.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
			var surface = BedRest.SurfaceFromMeshes(mattress.transform, mattress, portable: true, useMeshHeight: true);
			if (Mathf.Abs(Vector3.Dot(surface.Rotation * Vector3.forward, mattress.transform.right)) < .99f ||
				Mathf.Abs(surface.Position.y - .075f) > .01f)
				throw new InvalidOperationException("Rest pose failed to align along rotated mattress");
		}
		finally { UnityEngine.Object.Destroy(mattress); }
	}

	internal static void ProbeInteractions()
	{
		if (Environment.GetEnvironmentVariable("SKYCOOP_INTERACTION_PROBE") == "1")
		{
			Application.runInBackground = true;
			if (!started)
			{
				started = true;
				bool previousHost = MyMod.iAmHost;
				bool previousLoading = MyMod.LoadingScreenIsOn;
				MyMod.iAmHost = true;
				MyMod.LoadingScreenIsOn = false;
				var obj = new GameObject("Shared drop probe");
				try
				{
					var dummy = obj.AddComponent<Comps.DroppedGearDummy>();
					dummy.m_LocalizedDisplayName = "Stone probe";
					var interaction = dummy.Cast<Il2CppTLD.Interactions.IInteraction>();
					interaction.InitializeInteraction();
					interaction.UpdateInteraction();
					if (!interaction.HoverText.Contains("Stone probe") || interaction.GetInteractiveObject() != obj || !interaction.CanInteract)
						throw new InvalidOperationException("Shared drop native interaction failed: text=" + interaction.HoverText +
							", object=" + interaction.GetInteractiveObject()?.name + ", canInteract=" + interaction.CanInteract + ", enabled=" + interaction.IsEnabled);
					SleepRendezvousFix.RecordVote(2, -5);
					SleepRendezvousFix.RecordVote(1, -2);
					if (SleepRendezvousFix.SelectOffer(id => id == 2 ? -5 : -2) != -5)
						throw new InvalidOperationException("First group wait duration was replaced");
					SleepRendezvousFix.RecordVote(2, 0);
					if (SleepRendezvousFix.SelectOffer(id => id == 1 ? -2 : 0) != -2)
						throw new InvalidOperationException("Group wait cancellation failed");
					ProbeFlareAndBed();
					MelonLogger.Msg("[Interaction probe] PASS native shared-drop interface, wait vote, flare lifetime and rotated bed surface");
					if (!string.IsNullOrEmpty(RestRender)) RenderRestPose(RestRender);
				}
				catch (Exception ex) { MelonLogger.Error("[Interaction probe] FAIL " + ex); }
				finally
				{
					SleepRendezvousFix.ClearVotes();
					MyMod.iAmHost = previousHost;
					MyMod.LoadingScreenIsOn = previousLoading;
					UnityEngine.Object.Destroy(obj);
					Application.Quit();
				}
			}
			return;
		}
	}

	public static void Tick()
	{
		if (Environment.GetEnvironmentVariable("SKYCOOP_INTERACTION_PROBE") == "1") return;
		if ((ModelOnly || !string.IsNullOrEmpty(AvatarWorldOutput)) && !started &&
			GameCompat.Panel<Panel_Boot>() is { } boot)
		{
			boot.m_HoldLoadingUntilInputGiven = false;
			if (boot.m_SceneLoaded && boot.isActiveAndEnabled) boot._ReadyToActivateScene_k__BackingField = true;
		}
		if(Environment.GetEnvironmentVariable("SKYCOOP_CATALOG_PROBE")=="1")
		{
			Application.runInBackground=true;
			if(Time.realtimeSinceStartup>35&&!nativePreviewCreated){nativePreviewCreated=true;ItemSpawner.Open();}
			if(Time.realtimeSinceStartup>43&&nativePreviewCreated){ItemSpawner.Close();ItemSpawner.Open();var entries=ItemSpawner.AuditCatalog();System.IO.File.WriteAllLines(Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER"),entries);ItemSpawner.Close();Application.Quit();}
			return;
		}
		if(ModelOnly&&Time.realtimeSinceStartup>300){Application.Quit();return;}
		if(ModelOnly){Application.runInBackground=true;if(!nativePreviewCreated&&Shared.IsMainMenuScene(MyMod.level_name)&&GameCompat.Panel<Panel_MainMenu>()!=null&&Time.realtimeSinceStartup>16)BuildModelFixture();return;}
		if (Environment.GetEnvironmentVariable("SKYCOOP_NATIVE_MOTION_TEST") == "1") Application.runInBackground = true;
		if(!nativeAssetsProbed && Environment.GetEnvironmentVariable("SKYCOOP_NATIVE_AVATAR_TEST")=="1" && Time.realtimeSinceStartup>60 && MyMod.BundleAsset<GameObject>("multiplayerPlayer")!=null)
		{
			nativeAssetsProbed=true;NativeCharacterLibrary.Load(0);
		}
		if(Environment.GetEnvironmentVariable("SKYCOOP_BALANCE_TEST")=="1"&&(MyMod.iAmHost||ModelOnly)&&MyMod.WildlifeSceneReady())
		{
			var fatigue=GameManager.GetFatigueComponent();
			if(fatigue!=null&&balanceSampleAt==-1){
				GameManager.GetConditionComponent().m_CurrentHP=GameManager.GetConditionComponent().GetAdjustedMaxHP();
				GameManager.GetHungerComponent().m_CurrentReserveCalories=GameManager.GetHungerComponent().m_MaxReserveCalories;
				GameManager.GetThirstComponent().m_CurrentThirst=0;
				GameManager.GetFreezingComponent().m_CurrentFreezing=0;
				fatigue.m_CurrentFatigue=0;
				MyMod.OverridedHourse=12;MyMod.OverridedMinutes=0;MyMod.OveridedTime="12:00";Shared.UpdateTimeOfDayState();
				if(ModelOnly)GameManager.GetTimeOfDayComponent().SetNormalizedTime(.5f,true);
			balanceSampleAt=Time.realtimeSinceStartup;balanceFatigue=fatigue.m_CurrentFatigue;}
			else if(fatigue!=null&&balanceSampleAt>=0&&Time.realtimeSinceStartup-balanceSampleAt>=10)
			{
				MelonLogger.Msg("[Balance test] fatigueDelta="+(fatigue.m_CurrentFatigue-balanceFatigue)+" elapsed="+(Time.realtimeSinceStartup-balanceSampleAt)+" nativeRate="+fatigue.GetCurrentTotalFatigueBurnPerHour()+" configuredRate="+fatigue.m_TotalFatigueBurnPerHour+" strength="+CoopFatigue.Strength);
				balanceSampleAt=-2;
			}
		}
		if (!nativePreviewCreated && Environment.GetEnvironmentVariable("SKYCOOP_NATIVE_AVATAR_TEST") == "1" && (MyMod.iAmHost||ModelOnly) && MyMod.WildlifeSceneReady())
		{
			nativePreviewCreated=true;
			var prefab=NativeCharacterLibrary.Load(0);
			if(prefab!=null)
			{
				var camera=GameManager.GetMainCamera();var forward=camera.transform.forward;forward.y=0;forward.Normalize();
				previewTarget=GameManager.GetPlayerTransform().position+forward*3+Vector3.up;previewTargetSet=true;previewSince=Time.realtimeSinceStartup;
				for(int choice=0;choice<4;choice++)
				{
				var avatar=UnityEngine.Object.Instantiate(MyMod.BundleAsset<GameObject>("multiplayerPlayer"),GameManager.GetPlayerTransform().position+forward*3+camera.transform.right*((choice-1.5f)*1.2f),Quaternion.Euler(0,camera.transform.eulerAngles.y+180,0));
				foreach(var collider in avatar.GetComponentsInChildren<Collider>(true))collider.enabled=false;
				NativeAppearance.ApplyChoice(avatar,choice);
				if(Environment.GetEnvironmentVariable("SKYCOOP_NATIVE_MOTION_TEST")=="1")
				foreach(var animator in avatar.GetComponentsInChildren<Animator>(true))
					if(animator.enabled&&animator.runtimeAnimatorController!=null)
					{ animator.Play(new[]{"Idle","Walk","Run","Ctrl"}[choice],0,0); break; }
				var nativeDump=new System.Text.StringBuilder();
				foreach(var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					nativeDump.AppendLine("MESH "+renderer.name+" root="+renderer.rootBone?.name+" vertices="+renderer.sharedMesh?.vertexCount);
					foreach(var bone in renderer.bones)if(bone!=null)nativeDump.AppendLine(bone.name+";"+bone.parent?.name+";"+bone.position.ToString("F3"));
				}
				System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("SKYCOOP_NATIVE_AVATAR_DUMP") ?? System.IO.Path.Combine(Environment.CurrentDirectory,"..","native-model-test.txt"),nativeDump.ToString());
				}
			}
		}
		if (!previewCreated && Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_PREVIEW") == "1" && MyMod.WildlifeSceneReady() && !Shared.IsMainMenuScene(MyMod.level_name))
		{
			var prefab = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
			var camera = GameManager.GetMainCamera();
			if (prefab != null && camera != null)
			{
				var direction = camera.transform.forward; direction.y = 0; direction.Normalize();
				var position = GameManager.GetPlayerTransform().position + direction * 2.5f;
				var avatar = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(0,camera.transform.eulerAngles.y+180,0));
				avatar.name = "QA_NEW_SURVIVOR";
				foreach(var collider in avatar.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
				SurvivorModels.Apply(avatar,"GEAR_BasicWoolHat","GEAR_DownSkiJacket","GEAR_CombatPants","GEAR_WorkBoots","GEAR_Scarf",0,true);
				previewCreated = true;
			}
		}
		var dump = Environment.GetEnvironmentVariable("SKYCOOP_AVATAR_DUMP");
		if (!dumpedAvatar && !string.IsNullOrEmpty(dump))
		{
			var prefab = MyMod.BundleAsset<GameObject>("multiplayerPlayer");
			if (prefab != null)
			{
				var output = new System.Text.StringBuilder();
				foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
				{
					output.AppendLine("MESH " + renderer.name + " root=" + renderer.rootBone?.name);
					foreach (var bone in renderer.bones) if (bone != null) output.AppendLine(bone.name + ";" + bone.parent?.name + ";" + bone.position.ToString("F4"));
				}
				System.IO.File.WriteAllText(dump, output.ToString());
				dumpedAvatar = true;
			}
		}
		CheckAvatarInWorld();
		if (!string.IsNullOrEmpty(AvatarWorldOutput) && !started)
		{
			MoviePlayer.m_HasIntroPlayedForMainMenu = true;
			if (GameCompat.Panel<Panel_MainMenu>() is { } menu) menu.m_PlayedIntroMovie = true;
		}
		if (started)
		{
			return;
		}
		if (slot == null)
		{
			slot = Environment.GetEnvironmentVariable("SKYCOOP_SELFTEST_SLOT") ?? "";
			if (slot.Length > 0)
			{
				MelonLogger.Msg("[SelfTest] Will host slot " + slot);
			}
		}
		if (slot.Length == 0 || (!ModelOnly && !Shared.IsMainMenuScene(MyMod.level_name)))
		{
			return;
		}
		if (menuSince < 0f)
		{
			menuSince = Time.realtimeSinceStartup;
		}
		// The menu scene exists while startup movies are still showing; loading a save
		// during them leaves their overlay/camera above the world. Give the UI test time
		// to finish the startup screens before automatically hosting.
		float delay = float.TryParse(Environment.GetEnvironmentVariable("SKYCOOP_SELFTEST_DELAY"), out float requestedDelay) ? Math.Max(5, requestedDelay) : 60f;
		if (Time.realtimeSinceStartup - menuSince < delay)
		{
			return;
		}
		started = true;
		Il2CppSystem.Collections.Generic.List<SaveSlotInfo> slots = SaveGameSystem.GetSortedSaveSlots(Episode.One, SaveSlotType.SANDBOX);
		for (int i = 0; i < slots.Count; i++)
		{
			SaveSlotInfo info = slots[i];
			if (info.m_SaveSlotName == slot || info.m_UserDefinedName == slot)
			{
				MyMod.AutoHostWhenLoaded = !ModelOnly && string.IsNullOrEmpty(AvatarWorldOutput);
				MelonLogger.Msg("[SelfTest] Loading " + info.m_SaveSlotName + (MyMod.AutoHostWhenLoaded ? " and hosting after load" : " for an offline check"));
				SaveGameSlots.SetBaseNameForSave(info.m_SaveSlotName, info.m_SaveSlotName);
				SaveGameSystem.SetCurrentSaveInfo(info.m_Episode, info.m_GameMode, info.m_GameId, info.m_SaveSlotName);
				GameManager.LoadSaveGameSlot(info.m_SaveSlotName, info.m_SaveChangelistVersion);
				return;
			}
		}
		MelonLogger.Warning("[SelfTest] Slot " + slot + " not found");
	}
}
