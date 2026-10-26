using System;
using System.Collections.Generic;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// In 2.55 the pickup patches on PlayerManager (ProcessPickupItemInteraction and friends) are not reached: logs of a
// whole session had no "Pickedup" line, so nothing a player picked up was removed for the others. This path goes
// through calls the game cannot bypass: the interaction on the world item (an interface call) remembers where the
// item lay, and the item arriving in the inventory sends the pickup.
public static class PickupSync
{
	private struct Candidate
	{
		public Vector3 Position;

		public string Gear;

		public int InstanceId;

		public float Time;
	}

	[HarmonyPatch(typeof(GearItem), "PerformInteraction")]
	private static class RememberWorldItem
	{
		private static void Prefix(GearItem __instance)
		{
			Remember(__instance);
		}
	}

	[HarmonyPatch(typeof(GearItem), "PerformAlternativeInteraction")]
	private static class RememberWorldItemAlt
	{
		private static void Prefix(GearItem __instance)
		{
			Remember(__instance);
		}
	}

	[HarmonyPatch(typeof(Inventory), "AddGear")]
	private static class ItemEntersInventory
	{
		private static void Prefix(GearItem gi)
		{
			Picked(gi);
		}
	}

	private static readonly Dictionary<IntPtr, Candidate> Candidates = new Dictionary<IntPtr, Candidate>();

	private static readonly Dictionary<int, float> Sent = new Dictionary<int, float>();

	public static void MarkSent(int instanceId)
	{
		Sent[instanceId] = Time.realtimeSinceStartup;
	}

	private static bool SentRecently(int instanceId)
	{
		if (Sent.TryGetValue(instanceId, out var time))
		{
			return Time.realtimeSinceStartup - time < 60f;
		}
		return false;
	}

	private static void Remember(GearItem item)
	{
		try
		{
			if (item == null || !MyMod.InOnline() || item.m_BeenInPlayerInventory || item.m_InsideContainer || item.GetComponent<Comps.DroppedGearDummy>() != null)
			{
				return;
			}
			float now = Time.realtimeSinceStartup;
			if (Candidates.Count > 64)
			{
				List<IntPtr> stale = new List<IntPtr>();
				foreach (KeyValuePair<IntPtr, Candidate> candidate in Candidates)
				{
					if (now - candidate.Value.Time > 60f)
					{
						stale.Add(candidate.Key);
					}
				}
				foreach (IntPtr key in stale)
				{
					Candidates.Remove(key);
				}
			}
			Candidates[item.Pointer] = new Candidate
			{
				Position = item.transform.position,
				Gear = item.GetGearName(),
				InstanceId = item.m_InstanceID,
				Time = now
			};
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Pickup sync] " + ex.Message);
		}
	}

	private static void Picked(GearItem item)
	{
		try
		{
			if (item == null || !Candidates.TryGetValue(item.Pointer, out var candidate))
			{
				return;
			}
			Candidates.Remove(item.Pointer);
			// The game may mark the item as picked before it reaches the inventory, so that flag says nothing here;
			// the old PlayerManager patches, when they do run, register the pickup in SentRecently instead.
			if (Time.realtimeSinceStartup - candidate.Time > 60f || !MyMod.InOnline() || SentRecently(candidate.InstanceId))
			{
				return;
			}
			string gear = candidate.Gear;
			MelonLogger.Msg("Pickedup " + gear + " (inventory)");
			if (!Pathes.DuppableGearItem(gear) && item.GetComponent<NarrativeCollectibleItem>() == null && !Pathes.GarbadgeFilter(gear))
			{
				Shared.AddPickedGear(candidate.Position, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent, ClientUser.myId, candidate.InstanceId, gear, needSync: true);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Pickup sync] " + ex.Message);
		}
	}
}
