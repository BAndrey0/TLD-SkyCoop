using System;
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
	private static string RestRender => Environment.GetEnvironmentVariable("SKYCOOP_REST_RENDER");
	private static void BuildModelFixture()
	{
		bool alignment=Environment.GetEnvironmentVariable("SKYCOOP_MODEL_ALIGNMENT_TEST")=="1";
		var prefab=MyMod.BundleAsset<GameObject>("multiplayerPlayer");if(prefab==null||NativeCharacterLibrary.Load(0)==null)return;
		nativePreviewCreated=true;
		probeCamera=new GameObject("QA_ModelCamera").AddComponent<Camera>();probeCamera.enabled=false;probeCamera.transform.position=new Vector3(0,1.1f,0);probeCamera.fieldOfView=50;probeCamera.aspect=1.6f;probeCamera.nearClipPlane=.05f;probeCamera.farClipPlane=30;probeCamera.cullingMask=1<<30;probeCamera.clearFlags=CameraClearFlags.SolidColor;probeCamera.backgroundColor=new Color(.18f,.2f,.23f);
		var light=new GameObject("QA_ModelLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(35,-25,0);
		RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
		for(int choice=0;choice<(alignment?5:4);choice++)
		{
			var avatar=UnityEngine.Object.Instantiate(prefab,new Vector3((choice-(alignment?2:1.5f))*1.3f,0,alignment?5:4),Quaternion.Euler(0,180,0));
			foreach(var collider in avatar.GetComponentsInChildren<Collider>(true))collider.enabled=false;
			NativeAppearance.ApplyChoice(avatar,choice<4?choice:-1);
			foreach(var t in avatar.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
			foreach(var animator in avatar.GetComponentsInChildren<Animator>(true))if(animator.enabled&&animator.runtimeAnimatorController!=null){animator.applyRootMotion=false;animator.Play(alignment?"Idle":new[]{"Idle","Walk","Run","Ctrl"}[choice],0,0);animator.Update(.3f);break;}
		}
		previewTarget=new Vector3(0,1.05f,alignment?5:4);previewTargetSet=true;previewSince=Time.realtimeSinceStartup;
	}
	public static void LateUpdate()
	{
		if(ModelOnly&&previewTargetSet&&probeCamera!=null)
		{
			var camera=probeCamera;
			camera.transform.rotation=Quaternion.LookRotation(previewTarget-camera.transform.position,Vector3.up);
			if(!rendered&&Time.realtimeSinceStartup-previewSince>3)
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
		if(Environment.GetEnvironmentVariable("SKYCOOP_CATALOG_PROBE")=="1")
		{
			Application.runInBackground=true;
			if(Time.realtimeSinceStartup>35&&!nativePreviewCreated){nativePreviewCreated=true;ItemSpawner.Open();}
			if(Time.realtimeSinceStartup>43&&nativePreviewCreated){ItemSpawner.Close();ItemSpawner.Open();var entries=ItemSpawner.AuditCatalog();System.IO.File.WriteAllLines(Environment.GetEnvironmentVariable("SKYCOOP_MODEL_RENDER"),entries);ItemSpawner.Close();Application.Quit();}
			return;
		}
		if(ModelOnly&&Time.realtimeSinceStartup>300){Application.Quit();return;}
		if(ModelOnly){Application.runInBackground=true;if(!nativePreviewCreated&&Time.realtimeSinceStartup>35)BuildModelFixture();return;}
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
		if (slot.Length == 0 || (!ModelOnly&&!Shared.IsMainMenuScene(MyMod.level_name)))
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
				MelonLogger.Msg("[SelfTest] Loading " + info.m_SaveSlotName + " and hosting after load");
			MyMod.AutoHostWhenLoaded = !ModelOnly;
				SaveGameSlots.SetBaseNameForSave(info.m_SaveSlotName, info.m_SaveSlotName);
				SaveGameSystem.SetCurrentSaveInfo(info.m_Episode, info.m_GameMode, info.m_GameId, info.m_SaveSlotName);
				GameManager.LoadSaveGameSlot(info.m_SaveSlotName, info.m_SaveChangelistVersion);
				return;
			}
		}
		MelonLogger.Warning("[SelfTest] Slot " + slot + " not found");
	}
}
