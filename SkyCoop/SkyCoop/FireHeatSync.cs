using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace SkyCoop;

public static class FireHeatSync
{
	private static float FiniteNonnegative(float value)
	{
		if (!float.IsNaN(value) && !float.IsInfinity(value))
		{
			return Mathf.Max(0f, value);
		}
		return 0f;
	}

	public static void Capture(Fire fire, DataStr.FireSourcesSync data)
	{
		data.m_Fuel = FiniteNonnegative(fire.GetRemainingLifeTimeSeconds());
		HeatSource heatSource = fire.m_HeatSource;
		if (!(heatSource == null))
		{
			data.m_CurrentHeat = FiniteNonnegative(heatSource.GetCurrentTempIncrease());
			data.m_MaxHeat = FiniteNonnegative(heatSource.m_MaxTempIncrease);
			data.m_HeatInnerRadius = FiniteNonnegative(heatSource.m_MaxTempIncreaseInnerRadius);
			data.m_HeatOuterRadius = FiniteNonnegative(heatSource.m_MaxTempIncreaseOuterRadius);
			data.m_HeatMasked = heatSource.m_MaskTempIncrease;
		}
	}

	public static void Expire(DataStr.FireSourcesSync data)
	{
		if (data == null || data.m_LevelId != MyMod.levelid || data.m_LevelGUID != MyMod.level_guid)
		{
			return;
		}
		List<Fire>.Enumerator enumerator = FireManager.m_Fires.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Fire current = enumerator.Current;
			if (current == null || current.m_StartedByPlayer)
			{
				continue;
			}
			ObjectGuid component = current.GetComponent<ObjectGuid>();
			if (MyMod.IsSameFire(new DataStr.FireSourcesSync
			{
				m_Position = current.transform.position,
				m_Guid = ((component != null) ? component.Get() : "")
			}, data))
			{
				current.TurnOffImmediate();
				if (current.m_HeatSource != null)
				{
					current.m_HeatSource.TurnOffImmediate();
				}
				break;
			}
		}
	}

	public static void Apply(Fire fire, DataStr.FireSourcesSync data)
	{
		if (!(fire == null) && data != null && !fire.m_StartedByPlayer)
		{
			HeatSource heatSource = fire.m_HeatSource;
			if (!(heatSource == null))
			{
				heatSource.enabled = true;
				heatSource.TurnOn();
				heatSource.m_MaskTempIncrease = data.m_HeatMasked;
				heatSource.m_MaxTempIncrease = FiniteNonnegative(data.m_MaxHeat);
				heatSource.m_MaxTempIncreaseInnerRadius = FiniteNonnegative(data.m_HeatInnerRadius);
				heatSource.m_MaxTempIncreaseOuterRadius = Mathf.Max(heatSource.m_MaxTempIncreaseInnerRadius, FiniteNonnegative(data.m_HeatOuterRadius));
				heatSource.m_TempIncrease = Mathf.Min(FiniteNonnegative(data.m_CurrentHeat), heatSource.m_MaxTempIncrease);
				fire.m_FuelHeatIncrease = heatSource.m_MaxTempIncrease;
				fire.m_MaxOnTODSeconds = FiniteNonnegative(data.m_Fuel);
				fire.m_ElapsedOnTODSeconds = 0f;
				fire.m_ElapsedOnTODSecondsUnmodified = 0f;
			}
		}
	}
}
