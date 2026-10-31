using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class GameCompat
{
	private static readonly Dictionary<string, GameObject> AssetCache = new();
	// Survival 2.59 moved object identity to PdidObjectBase.
	public static string Get(this ObjectGuid guid) => guid != null ? guid.PDID : "";

	public static bool TryLoadAsset(string name, out GameObject prefab)
	{
		if (AssetCache.TryGetValue(name, out prefab) && prefab != null) return true;
		var handle = Il2CppTLD.AddressableAssets.AssetHelper.SafeLoadAssetAsync<GameObject>(name);
		prefab = handle.WaitForCompletion();
		if (prefab != null) AssetCache[name] = prefab;
		return prefab != null;
	}

	public static GearItem LoadGearItemPrefab(string name)
	{
		return TryLoadAsset(name, out var prefab) ? prefab.GetComponent<GearItem>() : null;
	}

	public static string GetGearDisplayName(string name)
	{
		var gear = LoadGearItemPrefab(name);
		return gear != null ? gear.DisplayName : name;
	}
	public const float InteractionRange = 2.5f;

	private static readonly Dictionary<string, Texture2D> s_TextureCache = new Dictionary<string, Texture2D>();

	public static void SetChildActive(Transform parent, int index, bool state)
	{
		if (!(parent == null) && index >= 0 && index < parent.childCount)
		{
			parent.GetChild(index).gameObject.SetActive(state);
		}
	}

	public static Transform ResolveChildPath(Component root, params int[] path)
	{
		if (root == null)
		{
			return null;
		}
		return ResolveChildPath(root.transform, path);
	}

	public static Transform ResolveChildPath(Transform root, params int[] path)
	{
		Transform transform = root;
		foreach (int num in path)
		{
			if (transform == null || num < 0 || num >= transform.childCount)
			{
				return null;
			}
			transform = transform.GetChild(num);
		}
		return transform;
	}

	public static string StripCloneSuffix(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return "";
		}
		int num = name.IndexOf("(Clone)", StringComparison.Ordinal);
		if (num >= 0)
		{
			name = name.Substring(0, num);
		}
		return name.Trim();
	}

	public static string GetGearName(this GearItem gi)
	{
		if (gi == null)
		{
			return "";
		}
		return StripCloneSuffix(gi.name);
	}

	public static void SetGearName(this GearItem gi, string name)
	{
		if (!(gi == null))
		{
			gi.name = name;
		}
	}

	public static string GetGearName(this GearItemObject gio)
	{
		if (!string.IsNullOrEmpty(gio.m_GearItemName))
		{
			return StripCloneSuffix(gio.m_GearItemName);
		}
		if (!(gio.m_GearItem != null))
		{
			return "";
		}
		return StripCloneSuffix(gio.m_GearItem.name);
	}

	public static void Set(this ObjectGuid guid, string value)
	{
		if (!(guid == null))
		{
			ObjectGuid.MaybeAttachObjectGuidAddOrReplace(guid.gameObject, value);
		}
	}

	public static string GetDisplayName(this GearItem gi)
	{
		if (gi == null)
		{
			return "";
		}
		return gi.DisplayName;
	}

	public static GameObject GetInteractiveObjectUnderCrosshair(this PlayerManager pm)
	{
		if (pm == null)
		{
			return null;
		}
		return pm.GetInteractiveObjectUnderCrosshairs(2.5f);
	}

	public static ItemWeight Kilograms(float kilograms)
	{
		return ItemWeight.FromKilograms(kilograms);
	}

	public static float ToKilograms(this ItemWeight weight)
	{
		return weight.ToQuantity(1f);
	}

	public static ItemLiquidVolume Liters(float liters)
	{
		return ItemLiquidVolume.FromLiters(liters);
	}

	public static float ToLiters(this ItemLiquidVolume volume)
	{
		return volume.ToQuantity(1f);
	}

	public static void CopyFirstPersonItemSetup(FirstPersonItem target, string referenceGearName)
	{
		if (target == null)
		{
			return;
		}
		GameObject gearItemObject = MyMod.GetGearItemObject(referenceGearName);
		FirstPersonItem firstPersonItem = ((gearItemObject != null) ? gearItemObject.GetComponent<FirstPersonItem>() : null);
		if (!(firstPersonItem == null))
		{
			target.m_PlayerStateTransitions = firstPersonItem.m_PlayerStateTransitions;
			target.m_WieldAudioEvent = firstPersonItem.m_WieldAudioEvent;
			target.m_UnwieldAudioEvent = firstPersonItem.m_UnwieldAudioEvent;
			if (string.IsNullOrEmpty(target.m_FirstPersonObjectName))
			{
				target.m_FirstPersonObjectName = firstPersonItem.m_FirstPersonObjectName;
			}
		}
	}

	public static int GetFPSMeshID(this GearItem gi)
	{
		if (gi == null || gi.m_FirstPersonItem == null)
		{
			return -1;
		}
		return gi.m_FirstPersonItem.GetMeshID();
	}

	public static T Panel<T>() where T : Panel_Base
	{
		try
		{
			if (InterfaceManager.TryGetPanel<T>(out var panel))
			{
				return panel;
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[SkyCoop] Could not reach panel " + typeof(T).Name + ": " + ex.Message);
		}
		return null;
	}

	public static GearItem GetSelectedGearItem(this Panel_Inventory panel)
	{
		if (panel == null || panel.m_FilteredInventoryList == null)
		{
			return null;
		}
		int num = panel.m_SelectedItemIndex + panel.m_FirstItemDisplayedIndex;
		if (num < 0 || num >= panel.m_FilteredInventoryList.Count)
		{
			return null;
		}
		return panel.m_FilteredInventoryList[num].m_GearItem;
	}

	public static bool InspectDroppedGearCompat(this PlayerManager player, GearItem item)
	{
		return DroppedInspectSync.Begin(player, item);
	}

	public static bool ProcessPickupItemInteractionCompat(this PlayerManager pm, GearItem item)
	{
		if (pm == null || item == null)
		{
			return false;
		}
		return pm.ProcessPickupItemInteraction(item, forceEquip: false, forceEquipImmediate: false, skipAudio: false);
	}

	public static GearItem InstantiateItemInPlayerInventory(this PlayerManager pm, string gearName, int units)
	{
		if (pm == null)
		{
			return null;
		}
		GearItem gearItemPrefab = MyMod.GetGearItemPrefab(gearName);
		if (gearItemPrefab == null)
		{
			MelonLogger.Warning("[SkyCoop] No gear prefab named " + gearName);
			return null;
		}
		return pm.InstantiateItemInPlayerInventory(gearItemPrefab, units);
	}

	public static void CancelSearch(this Container container)
	{
		if (!(container == null))
		{
			container.BeginContainerClose();
		}
	}

	public static void CancelHarvest(this Harvestable harvestable)
	{
		Panel_GenericProgressBar panel_GenericProgressBar = Panel<Panel_GenericProgressBar>();
		if (panel_GenericProgressBar != null)
		{
			panel_GenericProgressBar.Cancel();
		}
	}

	public static void DeserializeFromJson(this GearItem gi, string json)
	{
		if (!(gi == null) && !string.IsNullOrEmpty(json))
		{
			GearItemSaveDataProxy gearItemSaveDataProxy = Utils.DeserializeObject<GearItemSaveDataProxy>(json);
			if (gearItemSaveDataProxy != null)
			{
				gi.Deserialize(gearItemSaveDataProxy, applyPositioningFix: false);
			}
		}
	}

	public static GearItem GetClosestMatchStackable(this Inventory inventory, string gearName, float normalizedCondition)
	{
		if (inventory == null || inventory.m_Items == null)
		{
			return null;
		}
		GearItem result = null;
		float num = float.MaxValue;
		for (int i = 0; i < inventory.m_Items.Count; i++)
		{
			GearItem gearItem = inventory.m_Items[i];
			if (!(gearItem == null) && !(gearItem.m_StackableItem == null) && !(gearItem.GetGearName() != StripCloneSuffix(gearName)))
			{
				float num2 = Mathf.Abs(gearItem.GetNormalizedCondition() - normalizedCondition);
				if (!(num2 > gearItem.m_StackableItem.m_StackConditionDifferenceConstraint) && num2 < num)
				{
					num = num2;
					result = gearItem;
				}
			}
		}
		return result;
	}

	public static Texture2D GetCachedTexture(string key)
	{
		if (s_TextureCache.TryGetValue(key, out var value) && value != null)
		{
			return value;
		}
		return null;
	}

	public static void CacheTexture(string key, Texture2D texture)
	{
		s_TextureCache[key] = texture;
	}

	public static vp_FPSWeapon GetFPSWeapon(this FirstPersonItem fpi)
	{
		if (fpi == null)
		{
			return null;
		}
		vp_FPSWeapon weapon = null;
		fpi.TryGetFPSWeapon(out weapon);
		return weapon;
	}
}
