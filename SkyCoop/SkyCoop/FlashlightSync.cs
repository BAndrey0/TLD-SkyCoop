using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

public static class FlashlightSync
{
	private sealed class View
	{
		public GameObject Owner;

		public GameObject Root;

		public Light Beam;

		public int MeshCount;
	}

	private static readonly Dictionary<int, View> Views = new Dictionary<int, View>();

	private static bool wasHoldingFlashlight;

	public static bool IsFlashlightName(string name)
	{
		return GameCompat.StripCloneSuffix(name).StartsWith("GEAR_Flashlight", StringComparison.Ordinal);
	}

	public static void PollHeld()
	{
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		if (playerManagerComponent == null)
		{
			return;
		}
		GearItem itemInHands = playerManagerComponent.m_ItemInHands;
		int num;
		if (itemInHands != null)
		{
			num = ((itemInHands.m_FlashlightItem != null) ? 1 : 0);
			if (num != 0)
			{
				MyMod.MyLightSource = itemInHands.m_FlashlightItem.IsLit();
				goto IL_0062;
			}
		}
		else
		{
			num = 0;
		}
		if (wasHoldingFlashlight)
		{
			MyMod.MyLightSource = itemInHands != null && itemInHands.IsLitLightsource();
		}
		goto IL_0062;
		IL_0062:
		wasHoldingFlashlight = (byte)num != 0;
	}

	private static View Create(Comps.MultiplayerPlayer remote)
	{
		View view = new View
		{
			Owner = remote.m_Player,
			Root = new GameObject("SkyCoop_RemoteFlashlight")
		};
		view.Root.transform.SetParent(remote.hand_r.transform, worldPositionStays: false);
		view.Root.transform.localPosition = Vector3.zero;
		view.Root.transform.localRotation = Quaternion.identity;
		GearItem gearItem = MyMod.GetGearItemObject("GEAR_Flashlight")?.GetComponent<GearItem>();
		if (gearItem != null)
		{
			foreach (MeshFilter componentsInChild in gearItem.GetComponentsInChildren<MeshFilter>(includeInactive: true))
			{
				MeshRenderer component = componentsInChild.GetComponent<MeshRenderer>();
				if (!(componentsInChild.sharedMesh == null) && !(component == null))
				{
					GameObject gameObject = new GameObject("Mesh_" + componentsInChild.name);
					gameObject.transform.SetParent(view.Root.transform, worldPositionStays: false);
					gameObject.transform.localPosition = gearItem.transform.InverseTransformPoint(componentsInChild.transform.position);
					gameObject.transform.localRotation = Quaternion.Inverse(gearItem.transform.rotation) * componentsInChild.transform.rotation;
					gameObject.transform.localScale = componentsInChild.transform.lossyScale;
					gameObject.AddComponent<MeshFilter>().sharedMesh = componentsInChild.sharedMesh;
					gameObject.AddComponent<MeshRenderer>().sharedMaterials = component.sharedMaterials;
					view.MeshCount++;
				}
			}
		}
		GameObject gameObject2 = new GameObject("SharedFlashlightBeam");
		gameObject2.transform.SetParent(view.Root.transform, worldPositionStays: false);
		view.Beam = gameObject2.AddComponent<Light>();
		view.Beam.type = LightType.Spot;
		view.Beam.color = new Color(0.93f, 0.96f, 1f);
		view.Beam.range = 25f;
		view.Beam.spotAngle = 48f;
		view.Beam.innerSpotAngle = 28f;
		view.Beam.intensity = 2f;
		view.Beam.shadows = LightShadows.Soft;
		view.Beam.enabled = false;
		return view;
	}

	public static void UpdateRemote(Comps.MultiplayerPlayer remote)
	{
		if (remote == null || remote.m_Player == null || remote.hand_r == null)
		{
			return;
		}
		int instanceID = remote.m_Player.GetInstanceID();
		bool flag = IsFlashlightName(remote.m_HoldingItem) && string.IsNullOrEmpty(remote.m_HoldingFood);
		if (!Views.TryGetValue(instanceID, out var value) || value.Root == null)
		{
			if (!flag)
			{
				return;
			}
			value = Create(remote);
			Views[instanceID] = value;
		}
		value.Root.SetActive(flag);
		value.Beam.enabled = flag && remote.m_LightSourceOn;
		if (flag)
		{
			Vector3 forward = remote.m_Player.transform.forward;
			value.Beam.transform.position = remote.hand_r.transform.position + forward * 0.18f;
			value.Beam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
		}
	}

	public static object Snapshot(Comps.MultiplayerPlayer remote)
	{
		View value = null;
		if (remote != null && remote.m_Player != null)
		{
			Views.TryGetValue(remote.m_Player.GetInstanceID(), out value);
		}
		Light light = value?.Beam;
		return new
		{
			holding = ((remote != null) ? remote.m_HoldingItem : ""),
			receivedOn = (remote != null && remote.m_LightSourceOn),
			visualExists = (value != null && value.Root != null),
			meshCount = (value?.MeshCount ?? 0),
			modelActive = (value != null && value.Root != null && value.Root.activeInHierarchy),
			beamExists = (light != null),
			beamEnabled = (light != null && light.enabled),
			beamActive = (light != null && light.gameObject.activeInHierarchy),
			intensity = ((light != null) ? light.intensity : 0f),
			range = ((light != null) ? light.range : 0f),
			position = ((!(light != null)) ? null : new float[3]
			{
				light.transform.position.x,
				light.transform.position.y,
				light.transform.position.z
			}),
			forward = ((!(light != null)) ? null : new float[3]
			{
				light.transform.forward.x,
				light.transform.forward.y,
				light.transform.forward.z
			}),
			nativeGearComponents = ((value != null && value.Root != null) ? value.Root.GetComponentsInChildren<GearItem>(includeInactive: true).Length : 0)
		};
	}
}
