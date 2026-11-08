using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class ThrownFlareSync
{
	private sealed class Visual
	{
		public GameObject Owner;

		public GameObject Effects;

		public Light Light;

		public int Deadline;
	}

	[HarmonyPatch(typeof(MyMod), "OnUpdate")]
	private static class Update
	{
		private static void Postfix()
		{
			Tick();
		}
	}

	private static readonly List<Visual> visuals = new List<Visual>();


	public static int VisualCount => visuals.Count;

	public static bool IsFlare(GearItem gear)
	{
		if (gear != null)
		{
			return gear.m_FlareItem != null;
		}
		return false;
	}

	private static void Tick()
	{
		for (int num = visuals.Count - 1; num >= 0; num--)
		{
			Visual visual = visuals[num];
			if (visual.Owner == null)
			{
				visuals.RemoveAt(num);
			}
			else
			{
				bool flag = MyMod.MinutesFromStartServer < visual.Deadline;
				if (visual.Effects != null && visual.Effects.activeSelf != flag)
				{
					visual.Effects.SetActive(flag);
				}
				if (visual.Light != null)
				{
					visual.Light.enabled = flag;
				}
				if (!flag)
				{
					visuals.RemoveAt(num);
				}
			}
		}
	}

	public static void FillDropExtra(GearItem gear, DataStr.ExtraDataForDroppedGear extra)
	{
		if (IsFlare(gear) && extra != null)
		{
			FlareItem flareItem = gear.m_FlareItem;
			bool flag = flareItem.IsBurning();
			extra.m_Variant = (flag ? 1 : 0);
			float num = Math.Max(0f, flareItem.GetModifiedBurnLifetimeMinutes() - flareItem.m_ElapsedBurnMinutes);
			extra.m_GoalTime = (flag ? (MyMod.MinutesFromStartServer + (int)Math.Ceiling(num)) : 0);
		}
	}

	public static void PrepareReplica(GameObject obj, DataStr.ExtraDataForDroppedGear extra)
	{
		if (obj == null || extra == null || extra.m_Variant != 1 || extra.m_GoalTime <= MyMod.MinutesFromStartServer)
		{
			return;
		}
		FlareItem component = obj.GetComponent<FlareItem>();
		if (!(component == null))
		{
			component.OnIgniteComplete();
			Visual visual = new Visual
			{
				Owner = obj,
				Deadline = extra.m_GoalTime
			};
			if (component.m_FXGameObject != null && component.m_FXGameObject != obj)
			{
				visual.Effects = UnityEngine.Object.Instantiate(component.m_FXGameObject, obj.transform, worldPositionStays: false);
				visual.Effects.name = "SkyCoopFlareEffects";
				visual.Effects.SetActive(value: true);
			}
			Light light = (GameManager.GetWeatherComponent().UseOutdoorLightingForLightSources() ? component.m_LightOutdoor : component.m_LightIndoor);
			if (light != null)
			{
				GameObject gameObject = new GameObject("SkyCoopFlareLight");
				gameObject.transform.SetParent(obj.transform, worldPositionStays: false);
				gameObject.transform.position = light.transform.position;
				visual.Light = gameObject.AddComponent<Light>();
				visual.Light.type = light.type;
				visual.Light.color = light.color;
				visual.Light.range = light.range;
				visual.Light.intensity = light.intensity;
				visual.Light.shadows = light.shadows;
				visual.Light.enabled = true;
			}
			component.HideEffects(hide: true);
			if (component.m_FlareCapMeshObject != null)
			{
				component.m_FlareCapMeshObject.SetActive(value: false);
			}
			visuals.Add(visual);
			MelonLogger.Msg("[Flare sync] Burning pickup replica effects=" + (visual.Effects != null) + " light=" + (visual.Light != null));
		}
	}

	public static void ApplyPickupBurn(GearItem gear, DataStr.ExtraDataForDroppedGear extra)
	{
		if (IsFlare(gear) && extra != null && extra.m_Variant == 1)
		{
			FlareItem flareItem = gear.m_FlareItem;
			float num = Math.Max(0, extra.m_GoalTime - MyMod.MinutesFromStartServer);
			flareItem.m_ElapsedBurnMinutes = Math.Max(flareItem.m_ElapsedBurnMinutes, Math.Max(0f, flareItem.GetModifiedBurnLifetimeMinutes() - num));
			if (num <= 0f)
			{
				flareItem.ForceBurnout();
			}
		}
	}
}
