using System;
using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoop;

public class TFHUD
{
	public static GameObject TF2HUDLeft = null;

	public static GameObject TF2HUDRight = null;

	public static bool Enabled = true;

	public static void Init(AssetBundle Bundle, Transform CanvasTransform)
	{
		TF2HUDLeft = MyMod.SpawnModAsset(Bundle.LoadAsset<GameObject>("TF_HUDLeft"), CanvasTransform);
		TF2HUDLeft.SetActive(value: false);
		TF2HUDRight = MyMod.SpawnModAsset(Bundle.LoadAsset<GameObject>("TF_HUDRight"), CanvasTransform);
		TF2HUDRight.SetActive(value: false);
	}

	public static void DisableOriginalHUD()
	{
		if ((bool)GameCompat.Panel<Panel_HUD>())
		{
			GameCompat.Panel<Panel_HUD>().m_RegularSizeGroup.gameObject.SetActive(value: false);
			GameCompat.Panel<Panel_HUD>().m_SmallSizeGroup.gameObject.SetActive(value: false);
			GameCompat.Panel<Panel_HUD>().m_LargeSizeGroup.gameObject.SetActive(value: false);
			GameCompat.Panel<Panel_HUD>().m_AimingStaminaBar.gameObject.transform.parent.gameObject.SetActive(value: false);
			GameCompat.Panel<Panel_HUD>().m_EquipItemPopup.gameObject.SetActive(value: false);
		}
	}

	public static void Update()
	{
		if (!Enabled)
		{
			if (TF2HUDRight != null)
			{
				TF2HUDRight.SetActive(value: false);
			}
			if (TF2HUDRight != null)
			{
				TF2HUDLeft.SetActive(value: false);
			}
			return;
		}
		DisableOriginalHUD();
		if (!(TF2HUDLeft != null) || !(TF2HUDRight != null))
		{
			return;
		}
		bool flag = false;
		if (GameCompat.Panel<Panel_HUD>() != null && GameCompat.Panel<Panel_HUD>().IsEnabled() && !GameCompat.Panel<Panel_HUD>().GetHideHudElements())
		{
			flag = (((!GameCompat.Panel<Panel_Rest>() || !GameCompat.Panel<Panel_Rest>().IsEnabled()) && !GameManager.IsMainMenuActive() && !GameManager.GetPlayerManagerComponent().IsInspectModeActive() && !HUDManager.DoNotRenderHUD() && !InterfaceManager.IsOverlayActiveCached() && !GameCompat.Panel<Panel_HUD>().m_HideHudElements) ? true : false);
		}
		TF2HUDLeft.SetActive(flag);
		TF2HUDRight.SetActive(flag);
		if (!flag)
		{
			return;
		}
		Condition conditionComponent = GameManager.GetConditionComponent();
		Freezing freezingComponent = GameManager.GetFreezingComponent();
		Hunger hungerComponent = GameManager.GetHungerComponent();
		Thirst thirstComponent = GameManager.GetThirstComponent();
		PlayerMovement playerMovementComponent = GameManager.GetPlayerMovementComponent();
		Fatigue fatigueComponent = GameManager.GetFatigueComponent();
		Transform child = TF2HUDLeft.transform.GetChild(0);
		if (!conditionComponent || !freezingComponent || !hungerComponent || !thirstComponent || !playerMovementComponent || !fatigueComponent)
		{
			return;
		}
		Text component = child.GetChild(6).gameObject.GetComponent<Text>();
		Text component2 = child.GetChild(7).gameObject.GetComponent<Text>();
		Image component3 = child.GetChild(5).gameObject.GetComponent<Image>();
		component.text = Convert.ToInt32(conditionComponent.m_CurrentHP).ToString();
		if (conditionComponent.m_CurrentHP < conditionComponent.m_MaxHP)
		{
			component2.text = Convert.ToInt32(conditionComponent.m_MaxHP).ToString();
		}
		else
		{
			component2.text = "";
		}
		float num = 100f / conditionComponent.m_MaxHP * conditionComponent.m_CurrentHP;
		bool active = conditionComponent.m_CurrentHP < 45f;
		bool active2 = conditionComponent.m_CurrentHP > 100f;
		child.GetChild(3).gameObject.SetActive(active);
		child.GetChild(2).gameObject.SetActive(active2);
		component3.fillAmount = num / 100f;
		Image component4 = TF2HUDLeft.transform.GetChild(1).GetChild(2).gameObject.GetComponent<Image>();
		Image component5 = TF2HUDLeft.transform.GetChild(2).GetChild(2).gameObject.GetComponent<Image>();
		Image component6 = TF2HUDLeft.transform.GetChild(3).GetChild(2).gameObject.GetComponent<Image>();
		Image component7 = TF2HUDLeft.transform.GetChild(4).GetChild(2).gameObject.GetComponent<Image>();
		float num2 = 100f / freezingComponent.m_MaxFreezing * (freezingComponent.m_MaxFreezing - freezingComponent.m_CurrentFreezing);
		component4.fillAmount = num2 / 100f;
		float num3 = 100f / thirstComponent.m_MaxThirst * (thirstComponent.m_MaxThirst - thirstComponent.m_CurrentThirst);
		component5.fillAmount = num3 / 100f;
		float num4 = 100f / hungerComponent.m_MaxReserveCalories * hungerComponent.m_CurrentReserveCalories;
		component6.fillAmount = num4 / 100f;
		float num5 = 100f / fatigueComponent.m_MaxFatigue * (fatigueComponent.m_MaxFatigue - fatigueComponent.m_CurrentFatigue);
		component7.fillAmount = num5 / 100f;
		Image component8 = TF2HUDRight.transform.GetChild(0).GetChild(3).gameObject.GetComponent<Image>();
		Image component9 = TF2HUDRight.transform.GetChild(0).GetChild(4).gameObject.GetComponent<Image>();
		float num6 = 100f / playerMovementComponent.m_MaxSprintStamina * playerMovementComponent.m_SprintStamina;
		float clothingSprintReduction = GameManager.GetPlayerManagerComponent().GetClothingSprintReduction();
		component8.fillAmount = num6 / 100f;
		component9.fillAmount = clothingSprintReduction / 100f;
		TF2HUDRight.transform.GetChild(0).gameObject.SetActive(playerMovementComponent.m_SprintStamina != playerMovementComponent.m_MaxSprintStamina);
		vp_FPSCamera vpFPSCamera = GameManager.GetVpFPSCamera();
		if (vpFPSCamera != null && vpFPSCamera.m_CurrentWeapon != null)
		{
			GearItem gearItem = vpFPSCamera.m_CurrentWeapon.m_GearItem;
			int num7 = vpFPSCamera.m_CurrentWeapon.GetAmmoCount();
			int num8 = 0;
			if ((bool)GameManager.GetInventoryComponent())
			{
				num8 = GameManager.GetInventoryComponent().GetAmmoAvailableForWeapon(vpFPSCamera.m_CurrentWeapon.m_GearItem);
			}
			if ((bool)gearItem.m_BowItem)
			{
				num7 = gearItem.m_BowItem.GetNumArrowsInInventory(gearItem.m_BowItem.SelectedArrow);
			}
			if (num7 + num8 > 0)
			{
				TF2HUDRight.transform.GetChild(1).gameObject.SetActive(value: true);
				TF2HUDRight.transform.GetChild(1).GetChild(1).gameObject.GetComponent<Text>().text = num7.ToString();
				TF2HUDRight.transform.GetChild(1).GetChild(2).gameObject.GetComponent<Text>().text = num8.ToString();
				return;
			}
			TF2HUDRight.transform.GetChild(1).gameObject.SetActive(value: false);
			if (gearItem.m_FlareItem != null)
			{
				num7 = (gearItem.m_FlareItem.IsBurning() ? Convert.ToInt32(gearItem.m_FlareItem.GetNormalizedBurnTimeLeft() * 100f) : GameManager.GetInventoryComponent().GetNumFlares(gearItem.m_FlareItem.m_Type));
			}
			if (gearItem.m_MatchesItem != null)
			{
				num7 = GameManager.GetInventoryComponent().GetNumMatches();
			}
			if (gearItem.m_TorchItem != null)
			{
				num7 = ((!gearItem.m_TorchItem.IsBurning()) ? GameManager.GetInventoryComponent().GetNumTorches() : (100 - Convert.ToInt32(gearItem.m_TorchItem.GetBurnProgress() * 100f)));
			}
			if (gearItem.m_KeroseneLampItem != null)
			{
				num7 = Convert.ToInt32(gearItem.m_KeroseneLampItem.GetPercentFuelRemaining());
			}
			if (gearItem.m_StoneItem != null)
			{
				num7 = GameManager.GetInventoryComponent().GetNumStones();
			}
			if (gearItem.m_FlashlightItem != null)
			{
				num7 = Convert.ToInt32(gearItem.m_FlashlightItem.GetNormalizedCharge() * 100f);
			}
			if (gearItem.m_SprayPaintCan != null)
			{
				num7 = Convert.ToInt32(gearItem.GetNormalizedCondition() * 100f);
			}
			if (gearItem.m_NoiseMakerItem != null)
			{
				num7 = ((!gearItem.m_NoiseMakerItem.IsWickLit()) ? GameManager.GetInventoryComponent().GetNumNoiseMakers() : Convert.ToInt32(gearItem.m_NoiseMakerItem.GetNormalizedWickTimeLeft() * 100f));
			}
			TF2HUDRight.transform.GetChild(2).gameObject.SetActive(num7 > 0);
			TF2HUDRight.transform.GetChild(2).GetChild(1).gameObject.GetComponent<Text>().text = num7.ToString();
		}
		else
		{
			TF2HUDRight.transform.GetChild(1).gameObject.SetActive(value: false);
			TF2HUDRight.transform.GetChild(2).gameObject.SetActive(value: false);
		}
	}
}
