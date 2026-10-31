using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

public static class DroppedInspectSync
{
	[HarmonyPatch(typeof(PlayerManager), "ProcessPickupItemInteraction")]
	private static class AcceptPreview
	{
		private static void Prefix(GearItem __0, out GearItem __state)
		{
			__state = ((__0 != null && pending.ContainsKey(__0.GetInstanceID())) ? __0 : null);
			if (__state != null)
			{
				taking.Add(__state.GetInstanceID());
			}
		}

		private static void Postfix(PlayerManager __instance, bool __result, GearItem __state)
		{
			if (!(__state == null))
			{
				int instanceID = __state.GetInstanceID();
				taking.Remove(instanceID);
				if (__result || __state.m_InPlayerInventory)
				{
					pending.Remove(instanceID);
				}
				else if (!__instance.IsInspectModeActive())
				{
					ReturnOnce(__state);
				}
			}
		}

		private static Exception Finalizer(Exception __exception, GearItem __state)
		{
			if (__exception != null && __state != null)
			{
				taking.Remove(__state.GetInstanceID());
			}
			return __exception;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "OnEquipFromFoodItemInspection")]
	private static class TransferFoodPreview
	{
		private static void Prefix(PlayerManager __instance, out GearItem __state)
		{
			__state = null;
			if (pending.Count != 0)
			{
				GearItem gearItem = __instance.GearItemBeingInspected();
				if (!(gearItem == null) && pending.ContainsKey(gearItem.GetInstanceID()))
				{
					__state = gearItem;
					taking.Add(gearItem.GetInstanceID());
				}
			}
		}

		private static Exception Finalizer(Exception __exception, GearItem __state)
		{
			if (__state != null)
			{
				taking.Remove(__state.GetInstanceID());
			}
			return __exception;
		}
	}

	[HarmonyPatch(typeof(Inventory), "AddGear")]
	private static class AcceptInventoryPreview
	{
		private static void Postfix(GearItem __0)
		{
			if (pending.Count != 0 && !(__0 == null) && __0.m_InPlayerInventory)
			{
				pending.Remove(__0.GetInstanceID());
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "ExitInspectGearMode")]
	private static class ReturnClosedPreview
	{
		private static void Prefix(PlayerManager __instance, out GearItem __state)
		{
			GearItem gearItem = __instance.GearItemBeingInspected();
			__state = ((gearItem != null && pending.ContainsKey(gearItem.GetInstanceID())) ? gearItem : null);
		}

		private static void Postfix(GearItem __state)
		{
			if (!(__state == null) && !taking.Contains(__state.GetInstanceID()))
			{
				if (__state.m_InPlayerInventory)
				{
					pending.Remove(__state.GetInstanceID());
				}
				else
				{
					ReturnOnce(__state);
				}
			}
		}
	}

	private static readonly Dictionary<int, GearItem> pending = new Dictionary<int, GearItem>();

	private static readonly HashSet<int> taking = new HashSet<int>();
	private static readonly Dictionary<int, (GearItem item, int variant, int frame)> returns = new();

	public static void Tick()
	{
		if (returns.Count == 0) return;
		var player = GameManager.GetPlayerManagerComponent();
		var harvest = GameCompat.Panel<Panel_BodyHarvest>();
		foreach (int id in new List<int>(returns.Keys))
		{
			var entry = returns[id];
			if (entry.item == null || entry.item.m_InPlayerInventory)
			{
				returns.Remove(id);
				continue;
			}
			if (Time.frameCount <= entry.frame + 2 || taking.Contains(id)) continue;
			if (player != null && player.IsInspectModeActive() && player.GearItemBeingInspected() == entry.item) continue;
			if (harvest != null && harvest.isActiveAndEnabled && harvest.m_BodyHarvest != null &&
				(harvest.m_BodyHarvest.gameObject == entry.item.gameObject || harvest.m_BodyHarvest.transform.IsChildOf(entry.item.transform))) continue;
			returns.Remove(id);
			PublishReturn(entry.item, entry.variant);
		}
	}

	public static bool Begin(PlayerManager player, GearItem item)
	{
		if (player == null || item == null)
		{
			return false;
		}
		item.m_BeenInPlayerInventory = true;
		int instanceID = item.GetInstanceID();
		pending[instanceID] = item;
		try
		{
			player.EnterInspectGearMode(item, null, null, null, null);
			if (player.IsInspectModeActive() && player.GearItemBeingInspected() == item)
			{
				return true;
			}
			ReturnOnce(item);
			return false;
		}
		catch
		{
			ReturnOnce(item);
			throw;
		}
	}

	public static bool ReturnOnce(GearItem item, int variant = -1)
	{
		if (item == null || item.m_InPlayerInventory) return false;
		var marker = item.GetComponent<Comps.DropFakeOnLeave>();
		if (marker == null || marker.m_ReturnedToWorld) return false;
		int id = item.GetInstanceID();
		if (!returns.ContainsKey(id)) returns[id] = (item, variant, Time.frameCount);
		return true;
	}

	private static bool PublishReturn(GearItem item, int variant)
	{
		if (item == null)
		{
			return false;
		}
		Comps.DropFakeOnLeave component = item.GetComponent<Comps.DropFakeOnLeave>();
		if (component == null || component.m_ReturnedToWorld)
		{
			return false;
		}
		int instanceID = item.GetInstanceID();
		if (taking.Contains(instanceID) || item.m_InPlayerInventory)
		{
			return false;
		}
		if (variant < 0)
		{
			variant = (((item.m_FoodItem != null && item.m_FoodItem.m_Opened) || (item.GetComponent<SmashableItem>() != null && item.GetComponent<SmashableItem>().m_HasBeenSmashed)) ? 1 : 0);
		}
		component.m_ReturnedToWorld = true;
		try
		{
			MyMod.SendDropItem(item, 0, 0, samepose: true, variant);
			pending.Remove(instanceID);
			return true;
		}
		catch
		{
			component.m_ReturnedToWorld = false;
			throw;
		}
	}
}
