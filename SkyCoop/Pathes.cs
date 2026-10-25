using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using Il2CppAK;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using Il2CppTLD.Stats;
using Il2CppVoice;
using MelonLoader;
using TinyJSON;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCoop;

public class Pathes
{
	[HarmonyPatch(typeof(GearItem), "Drop")]
	public class GearItemDrop
	{
		private static int ShouldDrop;

		private static int Had;

		public static bool Prefix(GearItem __instance, int numUnits)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.GetGearName() == "GEAR_SCHeatPack" || __instance.GetGearName() == "GEAR_SCHeatPackC" || __instance.GetGearName() == "GEAR_SCEmergencyRation" || __instance.GetGearName() == "GEAR_SCLiquidLead")
			{
				GameAudioManager.PlayGUIError();
				HUDMessage.AddMessage("YOU CAN'T DROP THIS", highPriority: true, ignoreOverlayActive: true);
				return false;
			}
			MelonLogger.Msg("Item dropped " + __instance.GetGearName());
			if (MyMod.InOnline() && __instance.gameObject.GetComponent<Comps.IgnoreDropOverride>() == null)
			{
				ShouldDrop = numUnits;
				if (__instance.m_StackableItem != null)
				{
					Had = __instance.m_StackableItem.m_Units;
				}
				else
				{
					Had = 1;
				}
			}
			return true;
		}

		public static void Postfix(GearItem __instance, int numUnits, GearItem __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.GetGearName() == "GEAR_SCHeatPack" || __instance.GetGearName() == "GEAR_SCHeatPackC" || __instance.GetGearName() == "GEAR_SCEmergencyRation" || __instance.GetGearName() == "GEAR_SCLiquidLead")
			{
				return;
			}
			if (MyMod.InOnline() && __instance.gameObject.GetComponent<Comps.IgnoreDropOverride>() == null)
			{
				if (!(__instance.gameObject.GetComponent<Bed>() != null) || __instance.gameObject.GetComponent<Bed>().m_BedRollState != BedRollState.Placed)
				{
					int variant = 0;
					if ((__instance.gameObject.GetComponent<FoodItem>() != null && __instance.gameObject.GetComponent<FoodItem>().m_Opened) || (__instance.gameObject.GetComponent<SmashableItem>() != null && __instance.gameObject.GetComponent<SmashableItem>().m_HasBeenSmashed))
					{
						variant = 1;
					}
					if (__instance.gameObject.GetComponent<KeroseneLampItem>() != null && __instance.gameObject.GetComponent<KeroseneLampItem>().m_On)
					{
						variant = 1;
					}
					if (Had - ShouldDrop > 0)
					{
						MyMod.SendDropItem(__instance, ShouldDrop, Had, samepose: false, variant);
					}
					else
					{
						MyMod.SendDropItem(__instance, 0, 0, samepose: false, variant);
					}
					if (__result != null && __result.gameObject != null && __result != __instance)
					{
						UnityEngine.Object.Destroy(__result.gameObject);
					}
				}
			}
			else if (MyMod.InOnline() && __instance.gameObject.GetComponent<Comps.IgnoreDropOverride>() != null)
			{
				MelonLogger.Msg("[Postfix]Gear has IgnoreDropOverride doing drop without sync");
				UnityEngine.Object.Destroy(__instance.gameObject.GetComponent<Comps.IgnoreDropOverride>());
			}
		}
	}

	[HarmonyPatch(typeof(Panel_GearSelect), "DoFirePickerAction")]
	private static class Panel_GearSelect_DoFirePickerAction
	{
		internal static void Prefix(Panel_GearSelect __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			GearItem selectedItem = __instance.GetSelectedItem();
			if (selectedItem != null)
			{
				MelonLogger.Msg("Gear going to be placed adding IgnoreDropOverride");
				selectedItem.gameObject.AddComponent<Comps.IgnoreDropOverride>();
			}
		}
	}

	[HarmonyPatch(typeof(CookingPotItem), "StartCooking")]
	private static class CookingPotItem_StartCooking
	{
		internal static void Prefix(CookingPotItem __instance, GearItem gearItemToCook)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (gearItemToCook != null)
			{
				MelonLogger.Msg("Going to cook " + gearItemToCook.GetGearName() + " IgnoreDropOverride");
				gearItemToCook.gameObject.AddComponent<Comps.IgnoreDropOverride>();
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "RestoreTransform")]
	private static class PlayerManager_RestoreTransform
	{
		private static GameObject saveObj;

		internal static void Prefix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline())
			{
				MelonLogger.Msg("Going to cancle placement");
				if (__instance.m_ObjectToPlace != null && __instance.m_ObjectToPlace.GetComponent<Comps.DropFakeOnLeave>() != null)
				{
					saveObj = __instance.m_ObjectToPlace;
				}
			}
		}

		internal static void Postfix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.InOnline() || !(saveObj != null))
			{
				return;
			}
			if ((bool)saveObj.GetComponent<ObjectGuid>())
			{
				foreach (DataStr.UniversalSyncableObjectSpawner @object in ExpeditionEditor.m_Objects)
				{
					if (@object.m_GUID == saveObj.GetComponent<ObjectGuid>().Get())
					{
						ExpeditionEditor.ToggleColisionOfObject(saveObj, Status: true);
						MelonLogger.Msg("Expedition editor object placed!");
						return;
					}
				}
			}
			MelonLogger.Msg("Return gear to fake one");
			int variant = 0;
			if (saveObj.GetComponent<Bed>() != null && saveObj.GetComponent<Bed>().m_BedRollState == BedRollState.Placed)
			{
				variant = 1;
			}
			if ((saveObj.gameObject.GetComponent<FoodItem>() != null && saveObj.gameObject.GetComponent<FoodItem>().m_Opened) || (saveObj.gameObject.GetComponent<SmashableItem>() != null && saveObj.gameObject.GetComponent<SmashableItem>().m_HasBeenSmashed))
			{
				variant = 1;
			}
			if (saveObj.gameObject.GetComponent<KeroseneLampItem>() != null && saveObj.gameObject.GetComponent<KeroseneLampItem>().m_On)
			{
				variant = 1;
			}
			MyMod.SendDropItem(saveObj.gameObject.GetComponent<GearItem>(), 0, 0, samepose: true, variant);
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "PlaceMeshInWorld")]
	private static class PlayerManager_PlaceMeshInWorld
	{
		private static GameObject saveObj;

		internal static void Prefix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline() && __instance.m_ObjectToPlace != null)
			{
				MelonLogger.Msg("Going to place mesh!");
				saveObj = __instance.m_ObjectToPlace;
			}
		}

		internal static void Postfix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.InOnline() || !(saveObj != null))
			{
				return;
			}
			if ((bool)saveObj.GetComponent<ObjectGuid>())
			{
				foreach (DataStr.UniversalSyncableObjectSpawner @object in ExpeditionEditor.m_Objects)
				{
					if (@object.m_GUID == saveObj.GetComponent<ObjectGuid>().Get())
					{
						@object.m_Position = saveObj.transform.position;
						@object.m_Rotation = saveObj.transform.rotation;
						ExpeditionEditor.ToggleColisionOfObject(saveObj, Status: true);
						MelonLogger.Msg("Expedition editor object placed!");
						return;
					}
				}
			}
			MelonLogger.Msg("Gear placed!");
			int variant = 0;
			if (saveObj.GetComponent<Bed>() != null && saveObj.GetComponent<Bed>().m_BedRollState == BedRollState.Placed)
			{
				variant = 1;
			}
			if ((saveObj.gameObject.GetComponent<FoodItem>() != null && saveObj.gameObject.GetComponent<FoodItem>().m_Opened) || (saveObj.gameObject.GetComponent<SmashableItem>() != null && saveObj.gameObject.GetComponent<SmashableItem>().m_HasBeenSmashed))
			{
				variant = 1;
			}
			if (saveObj.gameObject.GetComponent<KeroseneLampItem>() != null && saveObj.gameObject.GetComponent<KeroseneLampItem>().m_On)
			{
				variant = 1;
			}
			Comps.DropFakeOnLeave component = saveObj.GetComponent<Comps.DropFakeOnLeave>();
			if (component != null)
			{
				component.m_OldPossition = saveObj.transform.position;
				component.m_OldRotation = saveObj.transform.rotation;
				MyMod.SendDropItem(saveObj.gameObject.GetComponent<GearItem>(), 0, 0, samepose: true, variant);
				return;
			}
			GearItem component2 = saveObj.gameObject.GetComponent<GearItem>();
			if (component2 == null)
			{
				MelonLogger.Msg(saveObj.gameObject.name + " has not GearItem");
			}
			else if (component2.m_Cookable == null || !component2.m_Cookable.IsNearFire())
			{
				component = saveObj.AddComponent<Comps.DropFakeOnLeave>();
				component.m_OldPossition = saveObj.transform.position;
				component.m_OldRotation = saveObj.transform.rotation;
				MyMod.SendDropItem(saveObj.gameObject.GetComponent<GearItem>(), 0, 0, samepose: true, variant);
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "InteractiveObjectsProcessAltFire")]
	private static class PlayerManager_InteractiveObjectsProcessAltFire
	{
		internal static void Prefix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!__instance.GetInteractiveObjectUnderCrosshair())
			{
				return;
			}
			if ((bool)__instance.GetInteractiveObjectUnderCrosshair().GetComponent<LoadScene>())
			{
				LoadScene component = __instance.GetInteractiveObjectUnderCrosshair().GetComponent<LoadScene>();
				if (GameManager.GetWeatherComponent().IsIndoorEnvironment())
				{
					if (MyMod.iAmHost)
					{
						MyMod.ShowKnockersPicker(component.gameObject, MPSaveManager.GetKnocksOnScene(MyMod.level_guid));
					}
					if (MyMod.sendMyPosition)
					{
						MyMod.TempDoor = component.gameObject;
						MyMod.DoPleaseWait("Looking through the peephole", "Please wait...");
						MyMod.SetRepeatPacket(MyMod.ResendPacketType.Cancel, 10);
						using Packet packet = new Packet(146);
						packet.Write(MyMod.level_guid);
						SendTCPData(packet);
					}
				}
				else if (component.m_SceneCanBeInstanced && !string.IsNullOrEmpty(component.m_GUID) && component.m_Active)
				{
					if (component.GetComponent<Comps.DoorLockedOnKey>() == null)
					{
						MyMod.SelectKeys(__instance.GetInteractiveObjectUnderCrosshair(), MyMod.KeysAction.LOCK);
					}
				}
				else
				{
					HUDMessage.AddMessage("This door can't be locked!");
				}
			}
			else if (MyMod.ExpeditionEditorUI != null && MyMod.ExpeditionEditorUI.activeSelf)
			{
				if ((bool)__instance.GetInteractiveObjectUnderCrosshair().GetComponent<Container>())
				{
					ExpeditionEditor.AddContainer(__instance.GetInteractiveObjectUnderCrosshair().GetComponent<ObjectGuid>().Get());
				}
				else if ((bool)__instance.GetInteractiveObjectUnderCrosshair().GetComponent<Harvestable>())
				{
					ExpeditionEditor.AddPlant(__instance.GetInteractiveObjectUnderCrosshair().GetComponent<ObjectGuid>().Get());
				}
				else if ((bool)__instance.GetInteractiveObjectUnderCrosshair().GetComponent<BreakDown>())
				{
					ExpeditionEditor.AddBreakdown(__instance.GetInteractiveObjectUnderCrosshair().GetComponent<ObjectGuid>().Get());
				}
			}
			else
			{
				MyMod.PlaceDroppedGear(__instance.GetInteractiveObjectUnderCrosshair());
				MelonLogger.Msg("Going to place object");
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Rest), "Update")]
	private static class Panel_Rest_Update
	{
		internal static void Postfix(Panel_Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.MyCycleSkip = __instance.m_SleepHours;
			SleepRendezvousFix.Tick(__instance);
		}
	}

	[HarmonyPatch(typeof(Panel_Rest), "OnCancel")]
	private static class Panel_Rest_Close
	{
		internal static void Postfix(Panel_Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			SleepRendezvousFix.CancelWaiting(explain: false);
			MelonLogger.Msg("Sleeping menu close.");
			if (MyMod.SleepingButtons != null)
			{
				MyMod.SleepingButtons.SetActive(value: true);
			}
			if (MyMod.WaitForSleepLable != null)
			{
				MyMod.WaitForSleepLable.SetActive(value: false);
			}
			if (__instance.m_Bed != null && __instance.m_Bed.gameObject != null && __instance.m_Bed.gameObject.GetComponent<Comps.FakeBedDummy>() != null)
			{
				UnityEngine.Object.Destroy(__instance.m_Bed.gameObject);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Rest), "OnPickUp")]
	private static class Panel_Rest_Close2
	{
		internal static bool Prefix(Panel_Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_Bed != null && __instance.m_Bed.gameObject != null && __instance.m_Bed.gameObject.GetComponent<Comps.FakeBedDummy>() != null)
			{
				if (__instance.m_Bed.gameObject.GetComponent<Comps.FakeBedDummy>().m_LinkedFakeObject != null)
				{
					MyMod.PickupDroppedGear(__instance.m_Bed.gameObject.GetComponent<Comps.FakeBedDummy>().m_LinkedFakeObject);
				}
				UnityEngine.Object.Destroy(__instance.m_Bed.gameObject);
				__instance.m_Bed = null;
				__instance.Enable(enable: false);
				return false;
			}
			return true;
		}

		internal static void Postfix(Panel_Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("Sleeping menu close.");
		}
	}

	[HarmonyPatch(typeof(Panel_Rest), "OnRest")]
	private static class Panel_Rest_Close3
	{
		public static Bed saveObj;

		internal static void Prefix(Panel_Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			saveObj = __instance.m_Bed;
		}

		internal static void Postfix(Panel_Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("Sleeping menu close.");
		}
	}

	[HarmonyPatch(typeof(PassTime), "End")]
	private static class Panel_Rest_Close4
	{
		public static Bed saveObj;

		internal static void Prefix(PassTime __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			saveObj = __instance.m_Bed;
		}

		internal static void Postfix(PassTime __instance)
		{
			if (saveObj != null && saveObj.gameObject.GetComponent<Comps.FakeBedDummy>() != null)
			{
				if (MyMod.CrazyPatchesLogger)
				{
					StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
					MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
					MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
				}
				UnityEngine.Object.Destroy(saveObj.gameObject);
				MelonLogger.Msg("Dummy bed removed");
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Confirmation), "OnCancel")]
	private static class Panel_Confirmation_OnCancel
	{
		internal static void Postfix(Panel_Confirmation __instance)
		{
			if (CharcoalItem.m_CharcoalItemInUseForSurvey != null)
			{
				CharcoalItem.m_CharcoalItemInUseForSurvey.StopSurveyAudio();
				GameManager.GetPlayerManagerComponent().UnequipItemInHands();
				CharcoalItem.m_CharcoalItemInUseForSurvey = null;
			}
			if (!Shared.CloseContainerOnCancle)
			{
				return;
			}
			Shared.CloseContainerOnCancle = false;
			DataStr.ContainerOpenSync containerOpenSync = new DataStr.ContainerOpenSync();
			containerOpenSync.m_Guid = "NULL";
			if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(66))
				{
					packet.Write(containerOpenSync);
					SendTCPData(packet);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Confirmation), "OnConfirm")]
	private static class Panel_Confirmation_Ok
	{
		internal static void Postfix(Panel_Confirmation __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "INPUT SERVER ADDRESS")
			{
				ClientUser.DoConnectToIp(__instance.m_CurrentGroup.m_InputField.GetText());
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "INVALID CONTAINER DATA")
			{
				Shared.CloseContainerOnCancle = false;
				if (MyMod.GoingToOpenContinaer != null)
				{
					MyMod.OpenFakeContainer(MyMod.GoingToOpenContinaer);
				}
			}
			if (__instance.m_CurrentGroup != null && (__instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "YOU GOT A NEW FLAIR!" || __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "YOU GOT NEW FLAIRS!"))
			{
				Supporters.SetFlairsUpdateData();
				MyMod.NotificationString = "";
				MyMod.m_Panel_Sandbox.Enable(enable: true);
				MenuChange.ChangeMenuItems("MultiProfileSettings");
				MenuChange.OpenFlairsMenu();
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "HOW DO YOU WANT TO BE CALLED?")
			{
				string text = __instance.m_CurrentGroup.m_InputField.GetText();
				if (!MyMod.ValidNickName(text))
				{
					if (MyMod.m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
					{
						GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "ERROR", "\nSorry nickname should not contain non latin symbols", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
					}
				}
				else
				{
					MyMod.MyChatName = text;
					MPSaveManager.SaveMyName(text);
				}
			}
			if (__instance.m_CurrentGroup != null)
			{
				string text2 = __instance.m_CurrentGroup.m_InputField.GetText();
				string text3 = "";
				bool flag = true;
				bool flag2 = true;
				string text4 = "seed";
				if (__instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "INPUT SECRET SEED FOR KEY")
				{
					flag = true;
					text4 = "seed";
				}
				else if (__instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "INPUT NAME FOR KEY")
				{
					flag = false;
					text4 = "name";
				}
				else
				{
					flag2 = false;
				}
				if (flag2)
				{
					if (text2.Contains('_'))
					{
						text3 = "Sorry " + text4 + " should not contain _ symbol";
					}
					else if (!MyMod.ValidNickName(text2))
					{
						text3 = "Sorry " + text4 + " should not contain non latin symbols";
					}
					if (text3 != "")
					{
						if (MyMod.m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
						{
							GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "ERROR", "\n" + text3, Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
						}
					}
					else if (flag)
					{
						MyMod.PendingKeySeed = text2;
						GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "Input name for key", "", Panel_Confirmation.ButtonLayout.Button_2, "Next", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
					}
					else
					{
						MyMod.PendingKeyName = text2;
						if ((bool)MyMod.PendingRegisterKey)
						{
							MyMod.LocksmithWork(MyMod.PendingRegisterKey, 1);
						}
					}
				}
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "DATA FOR DEBUG KEY")
			{
				string text5 = __instance.m_CurrentGroup.m_InputField.GetText();
				if (text5.Contains('_'))
				{
					string[] array = text5.Split('_');
					MPSaveManager.AlignKey(GameManager.GetPlayerManagerComponent().AddItemCONSOLE("GEAR_SCDoorKey", 1), array[0], array[1]);
				}
				else
				{
					GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "ERROR", "\nIt should be seed_name format!", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null, null);
				}
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "NOTE MESSAGE")
			{
				string text6 = __instance.m_CurrentGroup.m_InputField.GetText();
				if (CharcoalItem.m_CharcoalItemInUseForSurvey != null)
				{
					CharcoalItem.m_CharcoalItemInUseForSurvey.StopSurveyAudio();
					GameManager.GetPlayerAnimationComponent().Trigger_Generic_Unstow();
					CharcoalItem.m_CharcoalItemInUseForSurvey.ConsumeCharcoal();
					CharcoalItem.m_CharcoalItemInUseForSurvey = null;
					MyMod.CreateCustomNote(text6);
				}
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "INPUT GUID TO TELEPORT TO")
			{
				string text7 = __instance.m_CurrentGroup.m_InputField.GetText();
				bool flag3 = false;
				for (int i = 0; i < BaseAiManager.m_BaseAis.Count; i++)
				{
					GameObject gameObject = BaseAiManager.m_BaseAis[i].gameObject;
					if (gameObject != null && gameObject.GetComponent<ObjectGuid>() != null && gameObject.GetComponent<ObjectGuid>().Get() == text7)
					{
						flag3 = true;
						GameManager.GetPlayerManagerComponent().TeleportPlayer(gameObject.transform.position, gameObject.transform.rotation);
						break;
					}
				}
				if (!flag3)
				{
					HUDMessage.AddMessage("Animal with GUID " + text7 + " not exist!");
				}
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "INPUT ID OF PLAYER TELEPORT TO")
			{
				int num = int.Parse(__instance.m_CurrentGroup.m_InputField.GetText());
				if (num > MyMod.MaxPlayers - 1 || num < 0)
				{
					HUDMessage.AddMessage("Invalid ID of player!");
				}
				else if (MyMod.playersData[num].m_Levelid != MyMod.levelid)
				{
					HUDMessage.AddMessage("You and player on different scenes!");
				}
				else
				{
					GameManager.GetPlayerManagerComponent().TeleportPlayer(MyMod.playersData[num].m_Position, MyMod.playersData[num].m_Rotation);
					HUDMessage.AddMessage("You has been teleported to " + MyMod.playersData[num].m_Name);
				}
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "DISCONNECT TIMEOUT")
			{
				Application.Quit();
			}
			if (__instance.m_CurrentGroup != null && __instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "DISCONNECTED" && MyMod.QuitWithoutSaving)
			{
				Application.Quit();
			}
			if (__instance.m_CurrentGroup == null || !(__instance.m_CurrentGroup.m_MessageLabel_InputFieldTitle.text == "INPUT GUID TO TRACK"))
			{
				return;
			}
			string text8 = __instance.m_CurrentGroup.m_InputField.GetText();
			bool flag4 = false;
			for (int j = 0; j < BaseAiManager.m_BaseAis.Count; j++)
			{
				GameObject gameObject2 = BaseAiManager.m_BaseAis[j].gameObject;
				if (gameObject2 != null && gameObject2.GetComponent<ObjectGuid>() != null && gameObject2.GetComponent<ObjectGuid>().Get() == text8)
				{
					flag4 = true;
					MyMod.DebugAnimalGUID = text8;
					MyMod.DebugAnimalGUIDLast = text8;
					MyMod.DebugLastAnimal = gameObject2;
					HUDMessage.AddMessage("Found animal starting tracking");
					break;
				}
			}
			if (!flag4)
			{
				HUDMessage.AddMessage("Animal with GUID " + text8 + " not exist!");
			}
		}
	}

	[HarmonyPatch(typeof(FirstPersonLightSource), "TurnOnEffects")]
	internal class FirstPersonLightSource_Start
	{
		public static void Prefix(FirstPersonLightSource __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.MyLightSource = true;
		}
	}

	[HarmonyPatch(typeof(FirstPersonLightSource), "TurnOffEffects")]
	internal class FirstPersonLightSource_End
	{
		public static void Prefix(FirstPersonLightSource __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.MyLightSource = false;
		}
	}

	[HarmonyPatch(typeof(ObjectAnim), "Play")]
	internal class ObjectAnim_Hack
	{
		public static void Postfix(ObjectAnim __instance, string name)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.gameObject != null && __instance.gameObject.GetComponent<Comps.ContainersSync>() != null && __instance.gameObject.GetComponent<Comps.ContainersSync>().m_LastAnim != name)
			{
				__instance.gameObject.GetComponent<Comps.ContainersSync>().m_LastAnim = name;
				__instance.gameObject.GetComponent<Comps.ContainersSync>().CallSync();
			}
		}
	}

	[HarmonyPatch(typeof(BowItem), "ReleaseFire")]
	internal class BowItem_Shoot
	{
		public static void Prefix(BowItem __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!__instance.m_GearArrow || __instance.m_BowState != BowState.Aim)
			{
				return;
			}
			Transform transform = GameManager.GetPlayerAnimationComponent().m_ArrowFirePropPoint.transform;
			Transform playerTransform = GameManager.GetPlayerTransform();
			MelonLogger.Msg("[BowItem] Arrow Fire! " + __instance.m_GearArrow.gameObject.name);
			DataStr.ShootSync shootSync = new DataStr.ShootSync();
			shootSync.m_projectilename = "GEAR_Arrow";
			shootSync.m_position = playerTransform.TransformPoint(transform.position);
			shootSync.m_rotation = playerTransform.rotation * transform.rotation;
			shootSync.m_sceneguid = MyMod.level_guid;
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(24);
				packet.Write(shootSync);
				SendTCPData(packet);
			}
			if (!MyMod.iAmHost)
			{
				return;
			}
			using (new Packet(24))
			{
				ServerSend.SHOOTSYNC(0, shootSync, toEveryOne: true);
			}
		}
	}

	[HarmonyPatch(typeof(ArrowItem), "HandleCollisionWithObject")]
	internal class ArrowItem_Shoot
	{
		public static void Postfix(ArrowItem __instance, GameObject collider, Vector3 collisionPoint, Vector3 collisionNormal)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			bool flag = true;
			if (__instance.m_EmbeddedInAi != null)
			{
				GameObject gameObject = __instance.m_EmbeddedInAi.gameObject;
				if (gameObject.GetComponent<Comps.AnimalUpdates>() != null)
				{
					Comps.AnimalUpdates component = gameObject.GetComponent<Comps.AnimalUpdates>();
					ArrowSaveData arrowSaveData = __instance.Serialize();
					DataStr.AnimalArrow item = new DataStr.AnimalArrow
					{
						m_Position = arrowSaveData.m_LocalPosition,
						m_Angle = arrowSaveData.m_LocalAngles,
						m_Depth = arrowSaveData.m_EmbeddedDepth,
						m_LocaName = arrowSaveData.m_LocalName
					};
					component.m_Arrows.Add(item);
					MyMod.AddFakeArrowToAnimal(component, component.m_Arrows.Count - 1);
					flag = false;
					UnityEngine.Object.Destroy(__instance.gameObject);
					return;
				}
				if (gameObject.GetComponent<Comps.AnimalActor>() != null)
				{
					flag = false;
					UnityEngine.Object.Destroy(__instance.gameObject);
				}
			}
			else if (__instance.GetSurfaceResponseInfo(Utils.GetMaterialTagForObjectAtPosition(collider, collisionPoint), isSecondaryHit: false).impactEffect == ArrowImpactEffectType.ArrowImpactEffect_Flesh)
			{
				Transform transform = collider.transform;
				while (transform.gameObject.GetComponent<Comps.AnimalActor>() == null && transform.parent != null)
				{
					transform = transform.parent;
				}
				if (transform.gameObject.GetComponent<Comps.AnimalActor>() != null)
				{
					flag = false;
					UnityEngine.Object.Destroy(__instance.gameObject);
				}
			}
			if (__instance.gameObject.GetComponent<Comps.DestoryArrowOnHit>() != null)
			{
				UnityEngine.Object.Destroy(__instance.gameObject);
			}
		}
	}

	[HarmonyPatch(typeof(vp_Bullet), "Start")]
	internal class vp_Bullet_Start
	{
		public static bool Prefix(vp_Bullet __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null && __instance.gameObject != null)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(MyMod.GetGearItemObject("GEAR_Arrow"), new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f));
				gameObject.GetComponent<GearItem>().m_ArrowItem.Fire(1f);
				UnityEngine.Object.Destroy(gameObject);
				bool flag = true;
				if (__instance.gameObject.GetComponent<Comps.ClientProjectile>() != null)
				{
					flag = false;
					_ = __instance.gameObject.GetComponent<Comps.ClientProjectile>().m_ClientID;
				}
				if (flag)
				{
					SanityManager.AddSanity(-10f);
				}
				float num = 0f;
				float num2 = 0f;
				Vector3 vector = new Vector3(0f, 0f, 0f);
				float maxAngleDegrees = 0f;
				if (__instance.m_GunType == GunType.Rifle)
				{
					StatsManager.IncrementValue(StatID.RifleShot);
					maxAngleDegrees = GameManager.GetSkillRifle().GetAimAssistAngleDegrees();
				}
				else if (__instance.m_GunType == GunType.Revolver)
				{
					StatsManager.IncrementValue(StatID.RevolverShot);
					maxAngleDegrees = GameManager.GetSkillRevolver().GetAimAssistAngleDegrees();
				}
				Vector3 position = __instance.transform.position;
				Vector3 p = position + __instance.transform.forward * 100f;
				int layerMask = Utils.m_WeaponProjectileCollisionLayerMask | 0x8000000;
				MelonLogger.Msg("Doing raycast for bullet");
				if (AiUtils.RaycastWithAimAssist(__instance.transform.position, __instance.transform.forward, out var hit, __instance.Range, __instance.MinDistanceForAimAssist, __instance.Accuracy, maxAngleDegrees, layerMask))
				{
					p = hit.point;
					Vector3 localScale = __instance.transform.localScale;
					__instance.transform.parent = hit.transform;
					__instance.transform.localPosition = hit.transform.InverseTransformPoint(hit.point);
					__instance.transform.rotation = Quaternion.LookRotation(hit.normal);
					if (hit.transform.lossyScale == Vector3.one)
					{
						num = UnityEngine.Random.Range(0, 360);
						__instance.transform.Rotate(Vector3.forward, num, Space.Self);
					}
					else
					{
						__instance.transform.parent = null;
						__instance.transform.localScale = localScale;
						__instance.transform.parent = hit.transform;
					}
					if ((bool)hit.collider)
					{
						__instance.SpawnImpactEffects(hit);
					}
					BaseAi baseAiFromObject = AiUtils.GetBaseAiFromObject(hit.collider.gameObject);
					Comps.AnimalActor animalActor = ((!hit.collider.gameObject) ? null : ((hit.collider.gameObject.layer == 16) ? hit.collider.gameObject.GetComponent<Comps.AnimalActor>() : ((hit.collider.gameObject.layer != 27) ? null : hit.collider.gameObject.transform.GetComponentInParent<Comps.AnimalActor>())));
					Comps.PlayerBulletDamage component = hit.collider.gameObject.GetComponent<Comps.PlayerBulletDamage>();
					MelonLogger.Msg("Trying understand what hit point is");
					if (baseAiFromObject != null)
					{
						MelonLogger.Msg("This is baseAI animal");
						float num3 = Vector3.Distance(GameManager.GetPlayerTransform().position, hit.collider.transform.position);
						LocalizedDamage component2 = hit.collider.GetComponent<LocalizedDamage>();
						if (__instance.m_GunType == GunType.Rifle)
						{
							StatsManager.IncrementValue(StatID.SuccessfulHits_Rifle);
						}
						else if (__instance.m_GunType == GunType.Revolver)
						{
							StatsManager.IncrementValue(StatID.SuccessfulHits_Revolver);
						}
						WeaponSource weapon = __instance.m_GunType.ToWeaponSource();
						float bleedOutMinutes = component2.GetBleedOutMinutes(weapon);
						float num4 = __instance.Damage * component2.GetDamageScale(weapon);
						if ((double)num3 < (double)__instance.Accuracy)
						{
							if (!baseAiFromObject.m_IgnoreCriticalHits && component2.RollChanceToKill(weapon))
							{
								num4 = float.PositiveInfinity;
							}
						}
						else
						{
							float num5 = (num3 - __instance.Accuracy) * __instance.DamageFalloffPerMeterBeyondEffectiveRange;
							num4 = Mathf.Max(__instance.MinimumDamageFalloffBeyondEffectiveRange, num4 - num5);
						}
						if (!Utils.IsZero(num4) || baseAiFromObject.ForceApplyDamage())
						{
							if (baseAiFromObject.GetAiMode() != AiMode.Dead && flag)
							{
								if (__instance.m_GunType == GunType.Rifle)
								{
									GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Rifle, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
									MelonLogger.Msg("Got skill upgrade from your own shoot Rifle");
								}
								else if (__instance.m_GunType == GunType.Revolver)
								{
									GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Revolver, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
									MelonLogger.Msg("Got skill upgrade from your own shoot Revolver");
								}
							}
							if (flag)
							{
								baseAiFromObject.SetupDamageForAnim(hit.collider.transform.position, GameManager.GetPlayerTransform().position, component2);
								baseAiFromObject.ApplyDamage(num4, bleedOutMinutes, DamageSource.Player, hit.collider.name);
							}
						}
					}
					else if (animalActor != null)
					{
						MelonLogger.Msg("This is AnimalActor animal");
						WeaponSource weapon2 = __instance.m_GunType.ToWeaponSource();
						float num6 = Vector3.Distance(GameManager.GetPlayerTransform().position, hit.collider.transform.position);
						LocalizedDamage component3 = hit.collider.GetComponent<LocalizedDamage>();
						float num7 = __instance.Damage * component3.GetDamageScale(weapon2);
						if ((double)num6 < (double)__instance.Accuracy)
						{
							if (!component3.RollChanceToKill(weapon2))
							{
								num7 = float.PositiveInfinity;
							}
						}
						else
						{
							float num8 = (num6 - __instance.Accuracy) * __instance.DamageFalloffPerMeterBeyondEffectiveRange;
							num7 = Mathf.Max(__instance.MinimumDamageFalloffBeyondEffectiveRange, num7 - num8);
						}
						if (MyMod.iAmHost)
						{
							ServerSend.ANIMALDAMAGE(0, animalActor.gameObject.GetComponent<ObjectGuid>().Get(), num7);
						}
						else if (MyMod.sendMyPosition)
						{
							using Packet packet = new Packet(83);
							packet.Write(animalActor.gameObject.GetComponent<ObjectGuid>().Get());
							packet.Write(num7);
							SendTCPData(packet);
						}
						if (num7 > 0f && animalActor.m_Hp > 0f && flag)
						{
							if (__instance.m_GunType == GunType.Rifle)
							{
								GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Rifle, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
								MelonLogger.Msg("Got skill upgrade from your own shoot Rifle");
							}
							else if (__instance.m_GunType == GunType.Revolver)
							{
								GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Revolver, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
								MelonLogger.Msg("Got skill upgrade from your own shoot Revolver");
							}
						}
					}
					else if (component != null && flag)
					{
						MelonLogger.Msg("You damaged other player on " + component.m_Damage);
						int type = component.m_Type;
						if (MyMod.sendMyPosition)
						{
							using Packet packet2 = new Packet(29);
							packet2.Write((float)component.m_Damage);
							packet2.Write(type);
							packet2.Write(component.m_ClientId);
							packet2.Write(_value: false);
							SendTCPData(packet2);
						}
						if (MyMod.iAmHost)
						{
							using (new Packet(29))
							{
								ServerSend.BULLETDAMAGE(component.m_ClientId, component.m_Damage, type, 0);
							}
						}
					}
					else
					{
						MelonLogger.Msg("Bullet hit something different");
						GearItem gearItemFromObject = Utils.GetGearItemFromObject(hit.collider.gameObject);
						if ((bool)gearItemFromObject)
						{
							num2 = UnityEngine.Random.Range(0f, __instance.m_GearImpactUpwardForce);
							vector = new Vector3(UnityEngine.Random.Range(0f - __instance.m_GearImpactTorqueForce, __instance.m_GearImpactTorqueForce), UnityEngine.Random.Range(0f - __instance.m_GearImpactTorqueForce, __instance.m_GearImpactTorqueForce), UnityEngine.Random.Range(0f - __instance.m_GearImpactTorqueForce, __instance.m_GearImpactTorqueForce));
							Vector3 force = -hit.normal * __instance.m_GearImpactForce + Vector3.up * num2;
							gearItemFromObject.ApplyForce(force, vector);
						}
					}
					Renderer component4 = __instance.gameObject.GetComponent<Renderer>();
					if (component4 != null && component4.enabled)
					{
						vp_DecalManager.Add(__instance.gameObject);
					}
					else
					{
						UnityEngine.Object.Destroy(__instance.gameObject);
					}
				}
				else
				{
					UnityEngine.Object.Destroy(__instance.gameObject);
					Utils.DebugBulletHit(position, p);
				}
				return false;
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(vp_FPSShooter), "Start")]
	internal class vp_FPSShooter_Start
	{
		public static void Postfix(vp_FPSShooter __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null && __instance.gameObject != null && __instance.ProjectilePrefab != null)
			{
				if (__instance.gameObject.name == "Rifle" && __instance.ProjectilePrefab.name == "PistolBullet")
				{
					MyMod.PistolBulletPrefab = __instance.ProjectilePrefab;
				}
				if (__instance.gameObject.name == "Revolver" && __instance.ProjectilePrefab.name == "RevolverBullet")
				{
					MyMod.RevolverBulletPrefab = __instance.ProjectilePrefab;
				}
			}
		}
	}

	[HarmonyPatch(typeof(vp_FPSShooter), "Fire")]
	internal class vp_FPSShooter_End
	{
		public static void Prefix(vp_FPSShooter __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_Weapon == null || (double)Time.time < (double)__instance.m_NextAllowedFireTime || __instance.m_Weapon.ReloadInProgress() || !GameManager.GetPlayerAnimationComponent().IsAllowedToFire(__instance.m_Weapon.m_GunItem.m_AllowHipFire) || GameManager.GetPlayerAnimationComponent().IsReloading())
			{
				return;
			}
			if (__instance.m_Weapon.GetAmmoCount() < 1)
			{
				MyMod.SendMultiplayerAudio("PLAY_RIFLE_DRY_3D");
				return;
			}
			if (__instance.m_Weapon.m_GunItem.m_IsJammed)
			{
				MyMod.SendMultiplayerAudio("PLAY_RIFLE_DRY_3D");
				return;
			}
			Vector3 position = __instance.m_Camera.transform.position;
			Quaternion rotation = __instance.m_Camera.transform.rotation;
			for (int i = 0; i < __instance.ProjectileCount; i++)
			{
				if (!(__instance.ProjectilePrefab != null))
				{
					continue;
				}
				if (__instance.ProjectileCustomPrefab)
				{
					MelonLogger.Msg("[vp_FPSShooter] Flaregun projectile spawn! " + __instance.ProjectilePrefab.name);
				}
				else
				{
					MelonLogger.Msg("[vp_FPSShooter] Bullet projectile spawn " + __instance.ProjectilePrefab.name);
				}
				DataStr.ShootSync shootSync = new DataStr.ShootSync();
				shootSync.m_projectilename = __instance.ProjectilePrefab.name;
				shootSync.m_position = position;
				shootSync.m_rotation = rotation;
				shootSync.m_sceneguid = MyMod.level_guid;
				if (__instance.ProjectilePrefab.name == "PistolBullet")
				{
					shootSync.m_skill = GameManager.GetSkillRifle().GetEffectiveRange();
					if (MyMod.playersData[0].m_Mimic && MyMod.players[0] != null && MyMod.players[0] != null && MyMod.players[0].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
					{
						string preAnimStateHands = "RifleShoot";
						if (MyMod.playersData[0] != null && MyMod.playersData[0].m_AnimState == "Ctrl")
						{
							preAnimStateHands = "RifleShoot_Sit";
						}
						MyMod.players[0].GetComponent<Comps.MultiplayerPlayerAnimator>().m_PreAnimStateHands = preAnimStateHands;
					}
				}
				if (MyMod.sendMyPosition)
				{
					using Packet packet = new Packet(24);
					packet.Write(shootSync);
					SendTCPData(packet);
				}
				if (MyMod.iAmHost)
				{
					using (new Packet(24))
					{
						ServerSend.SHOOTSYNC(0, shootSync, toEveryOne: true);
					}
					if (shootSync.m_projectilename == "GEAR_FlareGunAmmoSingle")
					{
						ExpeditionManager.RegisterFlaregunShot(MyMod.level_guid, GameManager.GetPlayerTransform().position);
					}
				}
				if (MyMod.ExpeditionEditorUI != null && MyMod.ExpeditionEditorUI.activeSelf && shootSync.m_projectilename == "GEAR_FlareGunAmmoSingle")
				{
					ExpeditionEditor.AddGear("GEAR_FlareGunShoot", position, rotation, "");
				}
				MyMod.PushActionToMyDoll("Shoot");
			}
		}
	}

	[HarmonyPatch(typeof(GearItem), "ManualStart")]
	private static class GearItem_ManualStart
	{
		private static bool warnedMissingWoodHarvestDependencies;

		internal static void Postfix(GearItem __instance)
		{
			string text = "initial gear access";
			string text2 = "<unavailable>";
			try
			{
				text2 = __instance.name;
				if (MyMod.CrazyPatchesLogger)
				{
					StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
					MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
					MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
				}
				if (__instance.name == "GEAR_MedicalSupplies_hangar")
				{
					__instance.WeightKG = GameCompat.Kilograms(0.5f);
				}
				text = "custom first person setup";
				if (MyMod.IsCustomHandItem(__instance.name) || MyMod.IsUserGeneratedHandItem(__instance.name))
				{
					if (__instance.name == "GEAR_SCPhoto")
					{
						if (__instance.gameObject.GetComponent<FirstPersonItem>() == null)
						{
							FirstPersonItem firstPersonItem = __instance.gameObject.AddComponent<FirstPersonItem>();
							firstPersonItem.m_FirstPersonObjectName = "Stone";
							GameCompat.CopyFirstPersonItemSetup(firstPersonItem, "GEAR_Stone");
							__instance.m_FirstPersonItem = firstPersonItem;
						}
					}
					else if (__instance.name == "GEAR_SCMapPiece")
					{
						if (__instance.gameObject.GetComponent<FirstPersonItem>() == null)
						{
							FirstPersonItem firstPersonItem2 = __instance.gameObject.AddComponent<FirstPersonItem>();
							firstPersonItem2.m_FirstPersonObjectName = "Stone";
							GameCompat.CopyFirstPersonItemSetup(firstPersonItem2, "GEAR_Stone");
							__instance.m_FirstPersonItem = firstPersonItem2;
						}
					}
					else if (__instance.name != "GEAR_Hacksaw")
					{
						if (__instance.gameObject.GetComponent<FirstPersonItem>() == null)
						{
							FirstPersonItem firstPersonItem3 = __instance.gameObject.AddComponent<FirstPersonItem>();
							firstPersonItem3.m_FirstPersonObjectName = "Flare";
							GameCompat.CopyFirstPersonItemSetup(firstPersonItem3, "GEAR_FlareA");
							__instance.m_FirstPersonItem = firstPersonItem3;
						}
					}
					else if (__instance.gameObject.GetComponent<FirstPersonItem>() == null)
					{
						FirstPersonItem firstPersonItem4 = __instance.gameObject.AddComponent<FirstPersonItem>();
						firstPersonItem4.m_FirstPersonObjectName = "Revolver";
						GameCompat.CopyFirstPersonItemSetup(firstPersonItem4, "GEAR_Revolver");
						__instance.m_FirstPersonItem = firstPersonItem4;
					}
				}
				else if (__instance.name == "GEAR_SCNote")
				{
					if (__instance.gameObject.GetComponent<FirstPersonItem>() == null)
					{
						FirstPersonItem firstPersonItem5 = __instance.gameObject.AddComponent<FirstPersonItem>();
						firstPersonItem5.m_FirstPersonObjectName = "Stone";
						GameCompat.CopyFirstPersonItemSetup(firstPersonItem5, "GEAR_Stone");
						__instance.m_FirstPersonItem = firstPersonItem5;
					}
				}
				else if (!(__instance.name == "GEAR_ScrapLead"))
				{
					if (__instance.name == "GEAR_Softwood" || __instance.name == "GEAR_Hardwood")
					{
						GameObject gameObject = Resources.Load<GameObject>("GEAR_SCWoodForm");
						GearItem gearItem = ((gameObject != null) ? gameObject.GetComponent<GearItem>() : null);
						GameObject gameObject2 = ((gearItem != null) ? Resources.Load<GameObject>("GEAR_Knife") : null);
						ToolsItem toolsItem = ((gameObject2 != null) ? gameObject2.GetComponent<ToolsItem>() : null);
						if (gearItem != null && toolsItem != null)
						{
							Harvest harvest = __instance.gameObject.GetComponent<Harvest>();
							if (harvest == null)
							{
								harvest = __instance.gameObject.AddComponent<Harvest>();
							}
							harvest.m_Audio = "PLAY_HARVESTINGGENERIC";
							harvest.m_DurationMinutes = 15;
							harvest.m_YieldGear = new GearItem[1] { gearItem };
							harvest.m_YieldGearUnits = new int[1] { 3 };
							harvest.m_RequiredTools = new ToolsItem[1] { toolsItem };
							__instance.m_Harvest = harvest;
						}
						else if (!warnedMissingWoodHarvestDependencies)
						{
							warnedMissingWoodHarvestDependencies = true;
							MelonLogger.Warning("[SkyCoop] Custom wood-form crafting unavailable: wood-form prefab=" + (gearItem != null) + ", knife tool=" + (toolsItem != null) + ". Keeping stock wood behavior.");
						}
					}
					else if (__instance.name == "GEAR_SCWoodFormB")
					{
						EvolveItem evolveItem = __instance.gameObject.AddComponent<EvolveItem>();
						evolveItem.m_GearItem = __instance;
						evolveItem.m_GearItemToBecome = MyMod.GetGearItemPrefab("GEAR_SCDoorKeyLeadTemp");
						evolveItem.m_RequireIndoors = false;
						evolveItem.m_TimeToEvolveGameDays = 0.04f;
						__instance.m_EvolveItem = evolveItem;
					}
				}
				text = "photo and special item setup";
				if (MyMod.IsUserGeneratedHandItem(__instance.name))
				{
					if (__instance.m_ObjectGuid != null)
					{
						MelonLogger.Msg("Going render photo " + __instance.m_ObjectGuid.m_Guid);
						Texture2D photoTexture = MPSaveManager.GetPhotoTexture(__instance.m_ObjectGuid.m_Guid, __instance.GetGearName());
						if ((bool)photoTexture)
						{
							__instance.gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = photoTexture;
						}
					}
				}
				else if (__instance.name == "GEAR_CookingPot")
				{
					if (__instance.gameObject.GetComponent<ClothingItem>() == null)
					{
						ClothingItem clothingItem = __instance.gameObject.AddComponent<ClothingItem>();
						clothingItem.m_Region = ClothingRegion.Head;
						clothingItem.m_MinLayer = ClothingLayer.Mid;
						clothingItem.m_MaxLayer = ClothingLayer.Mid;
						clothingItem.m_DailyHPDecayWhenWornInside = 0f;
						clothingItem.m_DailyHPDecayWhenWornOutside = 0f;
						clothingItem.m_Warmth = -5f;
						clothingItem.m_WarmthWhenWet = -10f;
						clothingItem.m_Windproof = 50f;
						clothingItem.m_Toughness = 25f;
						clothingItem.m_SprintBarReductionPercent = 0f;
						clothingItem.m_Waterproofness = 1f;
						clothingItem.m_DryPercentPerHour = 0f;
						clothingItem.m_DryPercentPerHourNoFire = 0f;
						clothingItem.m_FreezePercentPerHour = 0f;
						clothingItem.m_DryBonusWhenNotWorn = 0f;
						clothingItem.m_PaperDollTextureName = "PaperDoll_POT";
						clothingItem.m_PaperDollBlendmapName = "";
						clothingItem.m_EquippedLayer = ClothingLayer.Mid;
						__instance.m_ClothingItem = clothingItem;
						clothingItem.m_GearItem = __instance;
						__instance.gameObject.AddComponent<Comps.CookpotHelmet>();
						__instance.gameObject.GetComponent<Comps.CookpotHelmet>().m_GearItem = __instance;
						__instance.gameObject.GetComponent<Comps.CookpotHelmet>().m_ClothingItem = clothingItem;
					}
				}
				else if ((__instance.name == "GEAR_SCHeatPack" || __instance.name == "GEAR_SCHeatPackC") && __instance.gameObject.GetComponent<HeatSource>() == null)
				{
					HeatSource heatSource = __instance.gameObject.AddComponent<HeatSource>();
					heatSource.m_TempIncrease = 10f;
					heatSource.m_MaxTempIncrease = 10f;
					heatSource.m_StartingTemp = 10f;
					heatSource.m_MaxTempIncreaseInnerRadius = 2f;
					heatSource.m_MaxTempIncreaseOuterRadius = 1f;
					heatSource.m_StartOn = true;
					heatSource.m_TimeToReachMaxTempMinutes = 1f;
				}
				text = "research component check";
				if ((bool)__instance.m_ResearchItem)
				{
					text = "book progress setup";
					MyMod.PatchBookReadTime(__instance);
				}
				text = "custom gear GUID and components";
				if (__instance.name == "GEAR_SCDoorKey" || __instance.name == "GEAR_SCDoorKeyLead")
				{
					if (__instance.m_ObjectGuid == null)
					{
						__instance.m_ObjectGuid = __instance.gameObject.AddComponent<ObjectGuid>();
					}
					else if (!string.IsNullOrEmpty(__instance.m_ObjectGuid.m_Guid) && __instance.m_ObjectGuid.m_Guid.Contains('_'))
					{
						__instance.m_DisplayNameOverrideLocID = __instance.m_ObjectGuid.m_Guid.Split('_')[0];
					}
				}
				if (__instance.name == "GEAR_SCSanityBook")
				{
					if (__instance.m_ObjectGuid == null)
					{
						__instance.m_ObjectGuid = __instance.gameObject.AddComponent<ObjectGuid>();
						__instance.m_ObjectGuid.Set(ObjectGuidManager.GenerateNewGuidString());
					}
					if (__instance.m_ResearchItem == null)
					{
						__instance.m_ResearchItem = __instance.gameObject.AddComponent<ResearchItem>();
						__instance.m_ResearchItem.m_SkillType = SkillType.None;
						__instance.m_ResearchItem.m_TimeRequirementHours = 15;
						__instance.m_ResearchItem.m_ReadAudio = "Play_ResearchBook";
					}
				}
				if (__instance.name == "GEAR_SCFirstAidBook")
				{
					if (__instance.m_ObjectGuid == null)
					{
						__instance.m_ObjectGuid = __instance.gameObject.AddComponent<ObjectGuid>();
						__instance.m_ObjectGuid.Set(ObjectGuidManager.GenerateNewGuidString());
					}
					if (__instance.m_ResearchItem == null)
					{
						__instance.m_ResearchItem = __instance.gameObject.AddComponent<ResearchItem>();
						__instance.m_ResearchItem.m_SkillType = (SkillType)10;
						__instance.m_ResearchItem.m_TimeRequirementHours = 5;
						__instance.m_ResearchItem.m_SkillPoints = 10;
						__instance.m_ResearchItem.m_NoBenefitAtSkillLevel = 5;
						__instance.m_ResearchItem.m_ReadAudio = "Play_ResearchBook";
					}
				}
				if (__instance.name == "GEAR_SCDecoy" && __instance.m_Scent == null)
				{
					__instance.m_Scent = __instance.gameObject.AddComponent<Scent>();
					__instance.m_Scent.m_ScentCategory = ScentRangeCategory.RAW_MEAT;
					__instance.GetComponent<BoxCollider>().enabled = false;
				}
			}
			catch (System.NullReferenceException)
			{
				MelonLogger.Error("[Gear ManualStart] gear=" + text2 + " stage=" + text);
				throw;
			}
		}
	}

	[HarmonyPatch(typeof(ResearchItem), "Read")]
	private static class ResearchItem_Read
	{
		internal static void Postfix(ResearchItem __instance, float timeOfDayHours)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(__instance.gameObject.GetComponent<GearItem>() != null))
			{
				return;
			}
			GearItem component = __instance.gameObject.GetComponent<GearItem>();
			if ((bool)component.m_ObjectGuid)
			{
				string text = component.m_ObjectGuid.Get();
				MelonLogger.Msg("Have read book " + component.GetGearName() + " GUID " + text + " Progress " + __instance.m_ElapsedHours + "/" + __instance.m_TimeRequirementHours);
				if (MyMod.BooksResearched.ContainsKey(text))
				{
					MyMod.BooksResearched.Remove(text);
				}
				MyMod.BooksResearched.Add(text, __instance.m_ElapsedHours);
			}
		}
	}

	[HarmonyPatch(typeof(ClothingSlot), "SetPaperDollTexture")]
	private static class ClothingSlot_SetPaperDollTexture
	{
		internal static void Postfix(ClothingSlot __instance, GearItem gi)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!gi || !gi.m_ClothingItem || string.IsNullOrEmpty(gi.m_ClothingItem.m_PaperDollTextureName) || !(gi.GetGearName() == "GEAR_CookingPot"))
			{
				return;
			}
			for (int i = 0; i < __instance.m_PaperDollSlotWidgets.Count; i++)
			{
				__instance.m_PaperDollSlotWidgets[i].enabled = true;
				string text = "PaperDoll_POT";
				if (PlayerManager.m_VoicePersona == VoicePersona.Female)
				{
					text += "_F";
				}
				Texture2D texture2D = GameCompat.GetCachedTexture(text);
				if (!texture2D)
				{
					texture2D = MyMod.BundleAsset<Texture2D>(text);
					GameCompat.CacheTexture(text, texture2D);
				}
				__instance.m_PaperDollSlots[i].mainTexture = texture2D;
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "ProcessPickupItemInteraction")]
	private static class Inventory_Pickup
	{
		internal static void Prefix(PlayerManager __instance, GearItem item)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(item == null) && !item.m_BeenInPlayerInventory)
			{
				MelonLogger.Msg("Pickedup " + item.GetGearName());
				item.m_BeenInPlayerInventory = true;
				Vector3 position = item.gameObject.transform.position;
				int instanceID = item.m_InstanceID;
				string gearName = item.GetGearName();
				NarrativeCollectibleItem component = item.GetComponent<NarrativeCollectibleItem>();
				if (gearName == "GEAR_Knife" && Supporters.ConfiguratedBenefits.m_Knife)
				{
					item.SetGearName("GEAR_JeremiahKnife");
					string json = item.SerializeToString();
					UnityEngine.Object.Destroy(item.gameObject);
					GameObject gameObject = UnityEngine.Object.Instantiate(MyMod.GetGearItemObject("GEAR_JeremiahKnife"), position, item.gameObject.transform.rotation);
					gameObject.GetComponent<GearItem>().DeserializeFromJson(json);
					gameObject.GetComponent<GearItem>().m_BeenInPlayerInventory = true;
					component = gameObject.GetComponent<NarrativeCollectibleItem>();
					__instance.ProcessPickupItemInteractionCompat(gameObject.GetComponent<GearItem>());
				}
				if (!DuppableGearItem(gearName) && !(component != null) && !GarbadgeFilter(gearName))
				{
					Shared.AddPickedGear(position, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent, ClientUser.myId, instanceID, gearName, needSync: true);
				}
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "ProcessPickupWithNoInspectScreen")]
	private static class Inventory_Pickup2
	{
		internal static void Prefix(PlayerManager __instance, GearItem pickupItem)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(pickupItem == null) && !pickupItem.m_BeenInPlayerInventory)
			{
				MelonLogger.Msg("Pickedup " + pickupItem.GetGearName());
				pickupItem.m_BeenInPlayerInventory = true;
				Vector3 position = pickupItem.gameObject.transform.position;
				int instanceID = pickupItem.m_InstanceID;
				string gearName = pickupItem.GetGearName();
				NarrativeCollectibleItem component = pickupItem.GetComponent<NarrativeCollectibleItem>();
				if (gearName == "GEAR_Knife" && Supporters.ConfiguratedBenefits.m_Knife)
				{
					pickupItem.SetGearName("GEAR_JeremiahKnife");
					string json = pickupItem.SerializeToString();
					UnityEngine.Object.Destroy(pickupItem.gameObject);
					GameObject gameObject = UnityEngine.Object.Instantiate(MyMod.GetGearItemObject("GEAR_JeremiahKnife"), position, pickupItem.gameObject.transform.rotation);
					gameObject.GetComponent<GearItem>().DeserializeFromJson(json);
					gameObject.GetComponent<GearItem>().m_BeenInPlayerInventory = true;
					component = gameObject.GetComponent<NarrativeCollectibleItem>();
					__instance.ProcessPickupWithNoInspectScreen(gameObject.GetComponent<GearItem>(), immediate: true);
				}
				if (!DuppableGearItem(gearName) && !(component != null) && !GarbadgeFilter(gearName))
				{
					Shared.AddPickedGear(position, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent, ClientUser.myId, instanceID, gearName, needSync: true);
				}
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "InitializeObjectToPlace")]
	private static class Inventory_Pickup3
	{
		public static void Prefix(PlayerManager __instance, GameObject go)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(go != null) || !(go.GetComponent<GearItem>() != null))
			{
				return;
			}
			GearItem component = go.GetComponent<GearItem>();
			if (!component.m_BeenInPlayerInventory)
			{
				MelonLogger.Msg("Pickedup " + component.GetGearName());
				component.m_BeenInPlayerInventory = true;
				Vector3 position = component.gameObject.transform.position;
				int instanceID = component.m_InstanceID;
				string gearName = component.GetGearName();
				NarrativeCollectibleItem component2 = component.GetComponent<NarrativeCollectibleItem>();
				if (gearName == "GEAR_Knife" && Supporters.ConfiguratedBenefits.m_Knife)
				{
					component.SetGearName("GEAR_JeremiahKnife");
					string json = component.SerializeToString();
					UnityEngine.Object.Destroy(component.gameObject);
					GameObject gameObject = UnityEngine.Object.Instantiate(MyMod.GetGearItemObject("GEAR_JeremiahKnife"), position, component.gameObject.transform.rotation);
					gameObject.GetComponent<GearItem>().DeserializeFromJson(json);
					gameObject.GetComponent<GearItem>().m_BeenInPlayerInventory = true;
					component2 = gameObject.GetComponent<NarrativeCollectibleItem>();
					__instance.InitializeObjectToPlace(gameObject);
				}
				if (!DuppableGearItem(gearName) && !(component2 != null) && !GarbadgeFilter(gearName))
				{
					Shared.AddPickedGear(position, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent, ClientUser.myId, instanceID, gearName, needSync: true);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Container), "Start")]
	private static class Container_Start
	{
		internal static void Prefix(Container __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.gameObject.GetComponent<Comps.ContainersSync>() == null)
			{
				__instance.gameObject.AddComponent<Comps.ContainersSync>().m_Obj = __instance.gameObject;
			}
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "OnSelectFeatsBack")]
	internal class Panel_MainMenu_FeatBack
	{
		public static bool Prefix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null)
			{
				MyMod.SelectGenderForConnection();
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "OnSelectFeatsContinue")]
	internal class Panel_MainMenu_FeatContinue
	{
		public static bool Prefix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null)
			{
				GameAudioManager.PlayGuiConfirm();
				FeatEnabledTracker.m_FeatsEnabledThisSandbox = new Il2CppSystem.Collections.Generic.List<FeatType>();
				for (int i = 0; i < __instance.m_SelectedFeats.Count; i++)
				{
					for (int j = 0; j < GameManager.GetFeatsManager().GetNumFeats(); j++)
					{
						Feat featFromIndex = GameManager.GetFeatsManager().GetFeatFromIndex(j);
						if (__instance.m_SelectedFeats[i] == featFromIndex.m_LocalizedDisplayName.m_LocalizationID)
						{
							FeatEnabledTracker.m_FeatsEnabledThisSandbox.Add(featFromIndex.m_FeatType);
						}
					}
				}
				if (!MyMod.ShouldCreateSaveForHost)
				{
					MyMod.ForcedCreateSave(MyMod.PendingSave);
				}
				else
				{
					MyMod.SelectNameForHostSaveFile();
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "OnSelectSlotNameContinue")]
	internal class Panel_MainMenu_OnSelectSlotNameContinue
	{
		public static bool Prefix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null && MyMod.ShouldCreateSaveForHost)
			{
				string inputFieldText = GameCompat.Panel<Panel_Confirmation>().GetInputFieldText();
				MyMod.ForcedCreateSave(MyMod.PendingSave, inputFieldText);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_SelectRegion_Map), "OnSelectRegionContinue")]
	internal class Panel_SelectRegion_Map_Done
	{
		public static bool Prefix(Panel_SelectRegion_Map __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null || MenuChange.MenuMode == "NewGameSelect")
			{
				return false;
			}
			return true;
		}

		public static void Postfix(Panel_SelectRegion_Map __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null)
			{
				GameManager.m_StartRegion = __instance.m_SelectedItem.Region;
				MyMod.PendingSave.m_Location = (int)RegionCompat.FromRegionSpecification(__instance.m_SelectedItem.Region);
				__instance.Enable(enable: false);
				MyMod.SelectGenderForConnection();
			}
			if (MenuChange.MenuMode == "NewGameSelect")
			{
				MyMod.LobbyStartingRegion = (int)RegionCompat.FromRegionSpecification(__instance.m_SelectedItem.Region);
				MyMod.LobbyStartingExperience = MenuChange.TempExperience;
				SteamConnect.Main.SetNewGameSettings(MyMod.LobbyStartingRegion, MyMod.LobbyStartingExperience);
				SteamConnect.Main.SetLobbyState("SelectedNewSave");
				__instance.Enable(enable: false);
				MyMod.m_Panel_Sandbox.Enable(enable: true);
				MenuChange.ChangeMenuItems("Lobby");
			}
		}
	}

	[HarmonyPatch(typeof(Panel_SelectRegion_Map), "OnClickBack")]
	internal class Panel_SelectRegion_Map_OnClickBack
	{
		public static bool Prefix(Panel_SelectRegion_Map __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null || MenuChange.MenuMode == "NewGameSelect")
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_SelectSurvivor), "OnSelectSurvivorMale")]
	internal class Panel_SelectSurvivor_Select1
	{
		public static bool Prefix(Panel_SelectSurvivor __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null)
			{
				PlayerManager.m_VoicePersona = VoicePersona.Male;
				__instance.Enable(enable: false);
				MyMod.SelectBagesForConnection();
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_SelectSurvivor), "OnSelectSurvivorFemale")]
	internal class Panel_SelectSurvivor_Select2
	{
		public static bool Prefix(Panel_SelectSurvivor __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.PendingSave != null)
			{
				PlayerManager.m_VoicePersona = VoicePersona.Female;
				__instance.Enable(enable: false);
				MyMod.SelectBagesForConnection();
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(BaseAi), "AnimSetTrigger")]
	private static class GetID
	{
		internal static void Postfix(BaseAi __instance, int id)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(__instance != null) || !(__instance.gameObject != null) || !(__instance.gameObject.GetComponent<ObjectGuid>() != null) || !__instance.gameObject.activeSelf)
			{
				return;
			}
			Comps.AnimalUpdates component = __instance.gameObject.GetComponent<Comps.AnimalUpdates>();
			if (!MyMod.AnimalsController && (!component || !component.m_MyControlled))
			{
				return;
			}
			GameObject gameObject = __instance.gameObject;
			DataStr.AnimalTrigger animalTrigger = new DataStr.AnimalTrigger();
			animalTrigger.m_Guid = gameObject.GetComponent<ObjectGuid>().Get();
			animalTrigger.m_Trigger = id;
			if (MyMod.iAmHost)
			{
				using (new Packet(23))
				{
					ServerSend.ANIMALSYNCTRIGG(0, animalTrigger, toEveryOne: true);
				}
			}
			if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(23))
				{
					packet.Write(animalTrigger);
					SendTCPData(packet);
				}
			}
		}
	}

	[HarmonyPatch(typeof(BaseAi), "Awake")]
	private static class AI_Hack
	{
		internal static void Postfix(BaseAi __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null && __instance.gameObject != null)
			{
				GameObject gameObject = __instance.gameObject;
				Comps.AnimalUpdates component = gameObject.GetComponent<Comps.AnimalUpdates>();
				if (component == null)
				{
					gameObject.AddComponent<Comps.AnimalUpdates>();
					component = gameObject.GetComponent<Comps.AnimalUpdates>();
				}
				component.m_Animal = gameObject;
			}
		}
	}

	[HarmonyPatch(typeof(BaseAi), "DecimateCarcass")]
	private static class BaseAi_DecimateCarcass
	{
		internal static bool Prefix(BaseAi __instance, float hours)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(BaseAi), "ProcessInvestigateFood")]
	private static class BaseAi_ProcessInvestigateFood
	{
		internal static bool Prefix(BaseAi __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null && __instance.gameObject != null && __instance.gameObject.GetComponent<ObjectGuid>() != null && __instance.gameObject.activeSelf)
			{
				Comps.AnimalUpdates component = __instance.gameObject.GetComponent<Comps.AnimalUpdates>();
				if (MyMod.AnimalsController || ((bool)component && component.m_MyControlled))
				{
					if (__instance.m_InvestigateFoodObject == null)
					{
						__instance.ReturnToDefaultBehavior();
					}
					else
					{
						GearItem component2 = __instance.m_InvestigateFoodObject.GetComponent<GearItem>();
						if (component2 == null)
						{
							__instance.ReturnToDefaultBehavior();
						}
						else if ((double)GameManager.GetFireManagerComponent().GetDistanceToClosestFire(component2.transform.position) < (double)__instance.m_InvestigateFoodAvoidFireDistance)
						{
							__instance.ReturnToDefaultBehavior();
						}
						else if (GameManager.GetPlayerAnimationComponent().IsAiming() && __instance.IsPlayerFacingAi())
						{
							__instance.ReturnToDefaultBehavior();
						}
						else if ((double)Utils.XZDistance(GameManager.GetPlayerTransform().position, __instance.m_CachedTransform.position) < (double)__instance.m_MaxPlayerApproachDistanceToInvestigateFood)
						{
							__instance.ReturnToDefaultBehavior();
						}
						else if (component2.m_InPlayerInventory)
						{
							__instance.ReturnToDefaultBehavior();
						}
						else
						{
							bool flag = __instance.CloseEnoughToEatObject(__instance.m_InvestigateFoodObject);
							if (flag)
							{
								__instance.MoveAgentStop();
							}
							else
							{
								flag = __instance.m_MoveAgent.HasReachedDestination();
							}
							if (!flag && !__instance.m_MoveAgent.HasPath())
							{
								__instance.ReturnToDefaultBehavior();
							}
							else if (flag)
							{
								Comps.FakeDecoy component3 = component2.gameObject.GetComponent<Comps.FakeDecoy>();
								if ((bool)component2 && (bool)component3)
								{
									Comps.DroppedGearDummy dropGearDummy = component3.m_DropGearDummy;
									if ((bool)dropGearDummy)
									{
										if (MyMod.iAmHost)
										{
											Shared.AnimalDestoryGear(dropGearDummy.m_SearchKey, MyMod.level_guid);
										}
										if (MyMod.sendMyPosition)
										{
											using Packet packet = new Packet(187);
											packet.Write(dropGearDummy.m_SearchKey);
											packet.Write(MyMod.level_guid);
											SendTCPData(packet);
										}
									}
									GearManager.DestroyGearObject(component2.gameObject);
									__instance.m_CurrentTarget = GameManager.GetPlayerManagerComponent().m_AiTarget;
									__instance.m_SuppressFleeAudio = true;
									__instance.m_UseRetreatSpeedInFlee = true;
									__instance.SetAiMode(AiMode.Flee);
								}
								else
								{
									__instance.ReturnToDefaultBehavior();
								}
							}
							else if (__instance.m_CurrentTarget == null)
							{
								__instance.MaybeClearTarget();
							}
							else
							{
								__instance.ScanForNewTarget();
							}
						}
					}
				}
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(BaseAi), "SetBestFeedingTarget")]
	private static class BaseAi_SetBestFeedingTarget
	{
		internal static bool Prefix(BaseAi __instance, Vector3 feederPos)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null && __instance.gameObject != null && __instance.gameObject.GetComponent<ObjectGuid>() != null && __instance.gameObject.activeSelf)
			{
				Comps.AnimalUpdates component = __instance.gameObject.GetComponent<Comps.AnimalUpdates>();
				if (MyMod.AnimalsController || ((bool)component && component.m_MyControlled))
				{
					float num = float.PositiveInfinity;
					BaseAi baseAi = null;
					for (int i = 0; i < BaseAiManager.m_BaseAis.Count; i++)
					{
						BaseAi baseAi2 = BaseAiManager.m_BaseAis[i];
						if ((bool)baseAi2 && baseAi2.gameObject.activeSelf && (bool)baseAi2.m_BodyHarvest && baseAi2.m_BodyHarvest.enabled)
						{
							float num2 = Utils.DistanceSqr(feederPos, baseAi2.m_CachedTransform.position);
							if ((double)num2 < (double)num)
							{
								num = num2;
								baseAi = baseAi2;
							}
						}
					}
					if (baseAi != null && (double)num <= (double)Utils.Sqr(5f))
					{
						__instance.m_TargetBaseAi = baseAi;
						__instance.m_TargetBodyHarvest = baseAi.GetComponent<BodyHarvest>();
					}
					else if ((bool)GameManager.m_BodyHarvestManager)
					{
						BodyHarvest bodyHarvest = null;
						float num3 = float.PositiveInfinity;
						for (int j = 0; j < BodyHarvestManager.m_BodyHarvestList.Count; j++)
						{
							BodyHarvest bodyHarvest2 = BodyHarvestManager.m_BodyHarvestList[j];
							if ((bool)bodyHarvest2 && bodyHarvest2.gameObject.activeSelf && bodyHarvest2.enabled && (bool)bodyHarvest2.gameObject.GetComponent<Comps.AnimalCorpseObject>())
							{
								float num4 = Utils.DistanceSqr(feederPos, bodyHarvest2.transform.position);
								if ((double)num4 < (double)num3)
								{
									num3 = num4;
									bodyHarvest = bodyHarvest2;
								}
							}
						}
						if (bodyHarvest != null && (double)num3 <= (double)Utils.Sqr(5f))
						{
							__instance.m_TargetBodyHarvest = bodyHarvest;
							MelonLogger.Msg(System.ConsoleColor.DarkMagenta, "Animal found feeding target that is AnimalCorpseObject");
						}
					}
				}
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(BodyHarvestInteraction), "PerformInteraction")]
	private static class BodyHarvest_OutOfSomeoneAlready
	{
		internal static bool Prefix(BodyHarvestInteraction __instance)
		{
			BodyHarvest bodyHarvest = __instance.m_BodyHarvest;
			if (bodyHarvest == null)
			{
				return true;
			}
			return PrefixInternal(bodyHarvest);
		}

		private static bool PrefixInternal(BodyHarvest bh)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			GameObject gameObject = bh.gameObject;
			if (gameObject.GetComponent<ObjectGuid>() != null)
			{
				for (int i = 0; i < MyMod.playersData.Count; i++)
				{
					if (MyMod.playersData[i] != null && MyMod.playersData[i].m_HarvestingAnimal == gameObject.GetComponent<ObjectGuid>().Get())
					{
						HUDMessage.AddMessage(MyMod.playersData[i].m_Name + " IS BUSY WITH THIS ALREADY");
						return false;
					}
				}
				if (MyMod.InOnline())
				{
					MyMod.RequestAnimalCorpseInteration(bh);
					return false;
				}
				return true;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_BodyHarvest), "HarvestSuccessful")]
	private static class Panel_BodyHarvest_Hack
	{
		internal static void Prefix(Panel_BodyHarvest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(__instance != null) || !(__instance.m_BodyHarvest != null) || !(__instance.m_BodyHarvest.gameObject != null) || !(__instance.m_BodyHarvest.gameObject.GetComponent<ObjectGuid>() != null))
			{
				return;
			}
			MelonLogger.Msg("Harvested meat " + __instance.m_MenuItem_Meat.HarvestAmount);
			MelonLogger.Msg("Harvested guts " + __instance.m_MenuItem_Gut.HarvestAmount);
			MelonLogger.Msg("Harvested hide " + __instance.m_MenuItem_Hide.HarvestAmount);
			DataStr.HarvestStats harvestStats = new DataStr.HarvestStats();
			harvestStats.m_Meat = __instance.m_MenuItem_Meat.HarvestAmount.ToKilograms();
			harvestStats.m_Guts = __instance.m_MenuItem_Gut.HarvestUnits;
			harvestStats.m_Hide = __instance.m_MenuItem_Hide.HarvestUnits;
			harvestStats.m_Guid = "";
			if (__instance.m_BodyHarvest.gameObject.GetComponent<ObjectGuid>() != null)
			{
				harvestStats.m_Guid = __instance.m_BodyHarvest.gameObject.GetComponent<ObjectGuid>().Get();
			}
			if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(27))
				{
					packet.Write(harvestStats);
					SendTCPData(packet);
					return;
				}
			}
			if (MyMod.iAmHost)
			{
				Shared.OnAnimalCorpseChanged(harvestStats.m_Guid, harvestStats.m_Meat, harvestStats.m_Guts, harvestStats.m_Hide);
			}
		}
	}

	[HarmonyPatch(typeof(BaseAi), "ApplyDamage", new System.Type[]
	{
		typeof(float),
		typeof(float),
		typeof(DamageSource),
		typeof(string)
	})]
	private static class AI_Hack_Damage
	{
		internal static bool Prefix(BaseAi __instance, float damage)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			Comps.AnimalUpdates component = __instance.gameObject.GetComponent<Comps.AnimalUpdates>();
			bool result = false;
			if (MyMod.AnimalsController || component.m_MyControlled)
			{
				result = true;
			}
			return result;
		}
	}

	[HarmonyPatch(typeof(ArrowItem), "InflictDamage", new System.Type[]
	{
		typeof(GameObject),
		typeof(float),
		typeof(bool),
		typeof(string),
		typeof(Vector3)
	})]
	private static class ArrowItem_DamageFix
	{
		internal static bool Prefix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			return false;
		}

		internal static void Postfix(ArrowItem __instance, GameObject victim, float damageScalar, bool stickIn, string collider, Vector3 collisionPoint, BaseAi __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			BaseAi baseAi = null;
			if (victim.layer == 16)
			{
				baseAi = victim.GetComponent<BaseAi>();
			}
			else if (victim.layer == 27)
			{
				baseAi = victim.transform.GetComponentInParent<BaseAi>();
			}
			if (baseAi == null)
			{
				__result = null;
				return;
			}
			LocalizedDamage component = victim.GetComponent<LocalizedDamage>();
			StatsManager.IncrementValue(StatID.SuccessfulHits_Bow);
			float bleedOutMinutes = component.GetBleedOutMinutes(WeaponSource.Arrow);
			float damage = __instance.m_VictimDamage * damageScalar * component.GetDamageScale(WeaponSource.Arrow);
			if (!baseAi.m_IgnoreCriticalHits && component.RollChanceToKill(WeaponSource.Arrow))
			{
				damage = float.PositiveInfinity;
			}
			if (baseAi.GetAiMode() != AiMode.Dead)
			{
				if (__instance.gameObject != null && __instance.gameObject.GetComponent<Comps.DestoryArrowOnHit>() == null)
				{
					MelonLogger.Msg("I am hit target with bow");
					GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Archery, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox);
				}
				else
				{
					MelonLogger.Msg("Other player hit targer with bow");
				}
			}
			baseAi.SetupDamageForAnim(collisionPoint, GameManager.GetPlayerTransform().position, component);
			baseAi.ApplyDamage(damage, (!stickIn) ? 0f : bleedOutMinutes, DamageSource.Player, collider);
			SanityManager.AddSanity(-20f);
			if ((double)__instance.m_EmbeddedDepth > 0.0)
			{
				__instance.m_EmbeddedInAi = baseAi;
			}
			__result = baseAi;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "ReleaseThrownObject")]
	public class PlayerManager_Throw
	{
		public static void Prefix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_ThrownItem != null)
			{
				MyMod.SaveThrowingItem = __instance.m_ThrownItem;
			}
			else
			{
				MelonLogger.Msg("[PlayerManager][PreFix] Trying throw NULL somehow, wot?");
			}
		}

		public static void Postfix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.SaveThrowingItem != null)
			{
				MelonLogger.Msg("[PlayerManager][Postfix] ReleaseThrownObject SaveThrowingItem " + MyMod.SaveThrowingItem.name);
				if (!MyMod.SaveThrowingItem.name.StartsWith("GEAR_Stone") && !MyMod.SaveThrowingItem.name.StartsWith("GEAR_NoiseMaker"))
				{
					return;
				}
				Vector3 position = MyMod.SaveThrowingItem.transform.position;
				Quaternion rotation = MyMod.SaveThrowingItem.transform.rotation;
				MelonLogger.Msg("[PlayerManager][Postfix] Throwing stone " + position.x + " y " + position.y + " z " + position.z);
				if (MyMod.SaveThrowingItem.name.StartsWith("GEAR_NoiseMaker") && !MyMod.ServerConfig.m_PVP)
				{
					MyMod.SaveThrowingItem.m_NoiseMakerItem.m_PlayerDamageInflictionInRadius = 15f;
					MyMod.SaveThrowingItem.m_NoiseMakerItem.m_PlayerDamageRadius = MyMod.SaveThrowingItem.m_NoiseMakerItem.m_AIDamageRadius;
				}
				DataStr.ShootSync shootSync = new DataStr.ShootSync();
				shootSync.m_position = position;
				shootSync.m_rotation = rotation;
				if (MyMod.SaveThrowingItem.name.StartsWith("GEAR_Stone"))
				{
					shootSync.m_projectilename = "GEAR_Stone";
				}
				if (MyMod.SaveThrowingItem.name.StartsWith("GEAR_NoiseMaker"))
				{
					shootSync.m_projectilename = "GEAR_NoiseMaker";
				}
				shootSync.m_skill = 0f;
				shootSync.m_camera_forward = GameManager.GetVpFPSCamera().transform.forward;
				shootSync.m_camera_right = GameManager.GetVpFPSCamera().transform.right;
				shootSync.m_camera_up = GameManager.GetVpFPSCamera().transform.up;
				shootSync.m_sceneguid = MyMod.level_guid;
				if (MyMod.sendMyPosition)
				{
					using Packet packet = new Packet(24);
					packet.Write(shootSync);
					SendTCPData(packet);
				}
				if (MyMod.iAmHost)
				{
					using (new Packet(24))
					{
						ServerSend.SHOOTSYNC(0, shootSync, toEveryOne: true);
					}
				}
				MyMod.PushActionToMyDoll("Shoot");
			}
			else
			{
				MelonLogger.Msg("[PlayerManager][PostFix] SaveThrowingItem is NULL");
			}
		}
	}

	[HarmonyPatch(typeof(NoiseMakerItem), "ProcessItemInHandDetonated")]
	public class NoiseMakerItem_ProcessItemInHandDetonated
	{
		public static void Postfix(NoiseMakerItem __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			DataStr.ShootSync shootSync = new DataStr.ShootSync();
			shootSync.m_position = GameManager.GetPlayerObject().transform.position;
			shootSync.m_rotation = GameManager.GetPlayerObject().transform.rotation;
			shootSync.m_projectilename = "GEAR_NoiseMaker";
			shootSync.m_skill = 1f;
			shootSync.m_camera_forward = GameManager.GetVpFPSCamera().transform.forward;
			shootSync.m_camera_right = GameManager.GetVpFPSCamera().transform.right;
			shootSync.m_camera_up = GameManager.GetVpFPSCamera().transform.up;
			shootSync.m_sceneguid = MyMod.level_guid;
			MelonLogger.Msg("[ProcessItemInHandDetonated] NoiseMaker burn explotion sync");
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(24);
				packet.Write(shootSync);
				SendTCPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.SHOOTSYNC(0, shootSync, toEveryOne: true);
			}
		}
	}

	[HarmonyPatch(typeof(UIButton), "OnClick")]
	public class UIButton_OnClick
	{
		public static void Postfix(UIButton __instance)
		{
			if (SleepRendezvousFix.IsWaitButton(__instance))
			{
				return;
			}
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(__instance.gameObject != null) || !(__instance.gameObject.transform.parent != null) || !(__instance.gameObject.transform.parent != null) || !(__instance.hoverSprite == "genericButton_over 1"))
			{
				return;
			}
			if (__instance.gameObject.transform.parent.name == "WaitForEveryoneButton")
			{
				if (!MyMod.CanSleep(domessage: false))
				{
					MyMod.CanSleep(domessage: true);
					return;
				}
				if (MyMod.SleepingButtons != null)
				{
					MyMod.SleepingButtons.SetActive(value: false);
				}
				if (MyMod.WaitForSleepLable != null)
				{
					MyMod.WaitForSleepLable.SetActive(value: true);
				}
			}
			else
			{
				if (!(__instance.gameObject.transform.parent.name == "LieDownButton"))
				{
					return;
				}
				if (!MyMod.AtBed)
				{
					MyMod.OutOfBedPosition = GameManager.GetPlayerTransform().position;
					if ((bool)GameCompat.Panel<Panel_Rest>().m_Bed)
					{
						GameManager.GetPlayerManagerComponent().TeleportPlayer(GameCompat.Panel<Panel_Rest>().m_Bed.m_BodyPlacementTransform.position, GameCompat.Panel<Panel_Rest>().m_Bed.m_BodyPlacementTransform.rotation);
						MyMod.AtBed = true;
					}
				}
				else
				{
					GameManager.GetPlayerManagerComponent().TeleportPlayer(MyMod.OutOfBedPosition, GameManager.GetPlayerTransform().rotation);
					MyMod.AtBed = false;
				}
				GameCompat.Panel<Panel_Rest>().OnCancel();
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "LoadScene", new System.Type[]
	{
		typeof(string),
		typeof(string)
	})]
	public class GameManager_LoadSceneOverLoad1
	{
		public static void Prefix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg(System.ConsoleColor.Yellow, "Loading other scene...");
			MyMod.ApplyOtherCampfires = false;
			AutoSaveAllTheCost = true;
		}
	}

	[HarmonyPatch(typeof(GameManager), "LoadScene", new System.Type[]
	{
		typeof(string),
		typeof(string)
	})]
	public class GameManager_LoadSceneOverLoad2
	{
		public static void Prefix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg(System.ConsoleColor.Yellow, "Loading other scene...");
			MyMod.ApplyOtherCampfires = false;
		}
	}

	[HarmonyPatch(typeof(Il2Cpp.SceneManager), "OnSceneLoaded")]
	public class SceneManager_Load
	{
		public static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[SceneManager] OnSceneLoaded");
			if (MyMod.level_name == "Boot")
			{
				if (uConsole.m_Instance == null)
				{
					MelonLogger.Msg("No uConsole present, creating one.");
					UnityEngine.Object.Instantiate(Resources.Load("uConsole"));
				}
				string text = "";
				if (GameManager.GetVersionString().Contains("S"))
				{
					text = "Steam";
				}
				else if (GameManager.GetVersionString().Contains("E"))
				{
					text = "EGS";
					MyMod.LoadChatName();
				}
				else
				{
					text = "Unknown";
					MyMod.LoadChatName();
				}
				if (text != "Unknown")
				{
					MelonLogger.Msg(text + " version");
				}
				else
				{
					MelonLogger.Msg("Unknown build of game, not using SteamWorks.NET");
				}
				bool flag = false;
				if (text == "Steam")
				{
					string[] commandLineArgs = System.Environment.GetCommandLineArgs();
					for (int i = 0; i < commandLineArgs.Length; i++)
					{
						if ((commandLineArgs[i] == "connect_lobby" || commandLineArgs[i] == "+connect_lobby") && i + 1 <= commandLineArgs.Length - 1)
						{
							MyMod.SteamLobbyToJoin = commandLineArgs[i + 1];
							MyMod.ConnectedSteamWorks = true;
							MyMod.NeedConnectAfterLoad = 3;
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					string[] commandLineArgs2 = System.Environment.GetCommandLineArgs();
					for (int j = 0; j < commandLineArgs2.Length; j++)
					{
						if ((commandLineArgs2[j] == "cmd" || commandLineArgs2[j] == "-cmd") && j + 1 <= commandLineArgs2.Length - 1)
						{
							string item = commandLineArgs2[j + 1].Replace("@", " ");
							MyMod.AutoCMDs.Add(item);
						}
					}
				}
				if (MyMod.DedicatedServerAppMode)
				{
					MyMod.StartDSAfterLoad = 3;
				}
			}
			if (MyMod.level_name != "Empty" && MyMod.level_name != "Boot" && !Shared.IsMainMenuScene(MyMod.level_name))
			{
				MelonLogger.Msg("Loading scene finished " + MyMod.level_name + " health is: " + GameManager.GetConditionComponent().m_CurrentHP);
				MyMod.NotNeedToPauseUntilLoaded = false;
			}
		}
	}

	[HarmonyPatch(typeof(BreakDown), "DoBreakDown")]
	private static class DoBreakDown_Sync
	{
		internal static void Postfix(BreakDown __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			string text = "";
			string text2 = "";
			if (__instance.gameObject != null)
			{
				ObjectGuid component = __instance.gameObject.GetComponent<ObjectGuid>();
				if (component != null)
				{
					text = component.Get();
				}
				if (__instance.gameObject.transform.parent != null)
				{
					ObjectGuid component2 = __instance.gameObject.transform.parent.GetComponent<ObjectGuid>();
					if (component2 != null)
					{
						text2 = component2.Get();
					}
				}
			}
			MyMod.BakePreSpawnedGearsList();
			if (text == "" && text2 == "")
			{
				MelonLogger.Msg("Deny listing that furn, cause have not any GUIDs, so it should be duppable");
			}
			else
			{
				MyMod.SendBrokeFurniture(text, text2, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent);
			}
		}
	}

	[HarmonyPatch(typeof(LoadScene), "Start")]
	private static class LoadScene_SeededGUIDHook
	{
		internal static bool Prefix(LoadScene __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!__instance.m_Active || __instance.m_StartHasBeenCalled)
			{
				return false;
			}
			__instance.m_StartHasBeenCalled = true;
			__instance.m_FadeOutStarted = false;
			if (__instance.m_LoadSceneParent != null)
			{
				__instance.m_LoadSceneParent.Start();
				__instance.m_GUID = __instance.m_LoadSceneParent.m_GUID;
			}
			else
			{
				__instance.m_GUID = MyMod.GenerateSeededGUID(GameManager.m_SceneTransitionData.m_GameRandomSeed, __instance.gameObject.transform.position);
			}
			if (__instance.m_TransitionOnContact)
			{
				__instance.GetComponent<Collider>().isTrigger = true;
				vp_Layer.Set(__instance.gameObject, 21);
			}
			else if (__instance.gameObject.layer != 21)
			{
				vp_Layer.Set(__instance.gameObject, 19);
			}
			if (__instance.m_LoadSceneParent != null)
			{
				__instance.m_LoadSceneParent.Register(__instance);
			}
			__instance.Lock = __instance.gameObject.GetComponent<Lock>();
			if (!__instance.Lock)
			{
				return false;
			}
			__instance.Lock.RollLockedState();
			return false;
		}
	}

	[HarmonyPatch(typeof(LoadSceneParent), "Start")]
	private static class LoadSceneParent_SeededGUIDHook
	{
		internal static bool Prefix(LoadSceneParent __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_StartHasBeenCalled)
			{
				return false;
			}
			__instance.m_StartHasBeenCalled = true;
			__instance.m_GUID = MyMod.GenerateSeededGUID(GameManager.m_SceneTransitionData.m_GameRandomSeed, __instance.gameObject.transform.position);
			return false;
		}
	}

	[HarmonyPatch(typeof(SaveGameSystem), "SaveGlobalData")]
	public static class SaveGameSystemPatch_SaveSceneData
	{
		public static void Postfix(SlotData slot)
		{
			if (slot == null)
			{
				return;
			}
			SaveSlotType gameMode = slot.m_GameMode;
			string internalName = slot.m_InternalName;
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			SlotData saveSlotFromName = SaveGameSlots.GetSaveSlotFromName(internalName);
			MelonLogger.Msg("SAVE_MOD_METADATA slot=" + internalName + " cachedSame=" + (saveSlotFromName != null && saveSlotFromName.Pointer == slot.Pointer));
			using (SaveSlotCompat.UseWriteTarget(slot))
			{
				SaveDeployedRopes(gameMode, internalName);
				SaveSnowShelters(gameMode, internalName);
				SaveGenVersion(gameMode, internalName);
				SaveRealtimeTime(gameMode, internalName);
				if (MyMod.ServerConfig.m_PlayersSpawnType != 3 && MyMod.iAmHost)
				{
					SaveFixedSpawn(gameMode, internalName);
					SaveFixedSpawnPosition(gameMode, internalName);
				}
				SaveBookReaded(gameMode, internalName);
				SaveSeedRadioFQ(gameMode, internalName);
				SaveDeathCreates(gameMode, internalName);
				SaveCustomSkills(gameMode, internalName);
				SaveUGUID(gameMode, internalName);
				SaveSanity(gameMode, internalName);
			}
		}
	}

	[HarmonyPatch(typeof(SaveGameSystem), "RestoreGlobalData")]
	public static class SaveGameSystemPatch_RestoreGlobalData
	{
		public static void Postfix(string name)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[Saving] Loading " + name + "...");
			LoadDeployedRopes(name);
			LoadSnowShelters(name);
			LoadGenVersion(name);
			LoadRealtimeTime(name);
			LoadFixedSpawn(name);
			LoadFixedSpawnPosition(name);
			LoadReadedBooks(name);
			LoadCustomSkills(name);
			LoadSanity(name);
			MyMod.RadioFrequency = Mathf.Round(LoadRadioFQ(name) * 10f) * 0.1f;
			LoadDeathCreates(name);
			LoadUGUID(name);
		}
	}

	[HarmonyPatch(typeof(GameManager), "LoadMainMenu")]
	public static class GameManager_BackToMenu
	{
		public static void Prefix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
		}
	}

	[HarmonyPatch(typeof(Panel_BreakDown), "OnBreakDown")]
	public static class Panel_BreakDown_AudioStart
	{
		public static void Postfix(Panel_BreakDown __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(__instance.m_BreakDown != null))
			{
				return;
			}
			GameObject gameObject = __instance.m_BreakDown.gameObject;
			string guid = "";
			string parentGuid = "";
			if (gameObject != null)
			{
				ObjectGuid component = gameObject.GetComponent<ObjectGuid>();
				if (component != null)
				{
					guid = component.Get();
				}
				if (gameObject.transform.parent != null)
				{
					ObjectGuid component2 = gameObject.transform.parent.GetComponent<ObjectGuid>();
					if (component2 != null)
					{
						parentGuid = component2.Get();
					}
				}
			}
			DataStr.BrokenFurnitureSync brokenFurnitureSync = new DataStr.BrokenFurnitureSync();
			brokenFurnitureSync.m_Guid = guid;
			brokenFurnitureSync.m_ParentGuid = parentGuid;
			brokenFurnitureSync.m_LevelID = MyMod.levelid;
			brokenFurnitureSync.m_LevelGUID = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(51);
				packet.Write(brokenFurnitureSync);
				SendTCPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.FURNBREAKINGGUID(0, brokenFurnitureSync, toEveryOne: true);
			}
			MelonLogger.Msg("Starting braking object " + brokenFurnitureSync.m_Guid + " audio");
		}
	}

	[HarmonyPatch(typeof(Panel_BreakDown), "StopAudioAndRumbleEffects")]
	public static class Panel_BreakDown_AudioStop
	{
		public static void Postfix(Panel_BreakDown __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(52);
				packet.Write(_value: true);
				SendTCPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.FURNBREAKINSTOP(0, _msg: true, toEveryOne: true);
			}
			MelonLogger.Msg("Stop braking object");
		}
	}

	[HarmonyPatch(typeof(BreakDown), "PerformInteraction")]
	public static class BreakDown_ProcessInteraction
	{
		public static bool Prefix(BreakDown __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			GameObject gameObject = __instance.gameObject;
			string guid = "";
			string parentGuid = "";
			if (gameObject != null)
			{
				ObjectGuid component = gameObject.GetComponent<ObjectGuid>();
				if (component != null)
				{
					guid = component.Get();
				}
				if (gameObject.transform.parent != null)
				{
					ObjectGuid component2 = gameObject.transform.parent.GetComponent<ObjectGuid>();
					if (component2 != null)
					{
						parentGuid = component2.Get();
					}
				}
			}
			DataStr.BrokenFurnitureSync brokenFurnitureSync = new DataStr.BrokenFurnitureSync();
			brokenFurnitureSync.m_Guid = guid;
			brokenFurnitureSync.m_ParentGuid = parentGuid;
			brokenFurnitureSync.m_LevelID = MyMod.levelid;
			brokenFurnitureSync.m_LevelGUID = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
			for (int i = 0; i < MyMod.playersData.Count; i++)
			{
				if (MyMod.playersData[i] != null)
				{
					DataStr.BrokenFurnitureSync brakingObject = MyMod.playersData[i].m_BrakingObject;
					if (brakingObject.m_Guid == brokenFurnitureSync.m_Guid && brakingObject.m_ParentGuid == brokenFurnitureSync.m_ParentGuid && brakingObject.m_LevelID == MyMod.levelid && brakingObject.m_LevelGUID == MyMod.level_guid)
					{
						HUDMessage.AddMessage(MyMod.playersData[i].m_Name + " IS BREAKING THIS");
						GameAudioManager.PlayGUIError();
						return false;
					}
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ContainerInteraction), "PerformInteraction")]
	public static class PlayerManager_ProcessContainerInteraction
	{
		public static bool Prefix(ContainerInteraction __instance)
		{
			return PrefixInternal(__instance.m_Container);
		}

		private static bool PrefixInternal(Container c)
		{
			if (MyMod.InOnline() && !CoopContainerFlow.CanStartInteraction(c))
			{
				HUDMessage.AddMessage("Заканчиваем предыдущий обыск…");
				return false;
			}
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			string guid = "";
			if (c != null && c.gameObject != null && c.gameObject.GetComponent<ObjectGuid>() != null)
			{
				guid = c.gameObject.GetComponent<ObjectGuid>().Get();
			}
			DataStr.ContainerOpenSync containerOpenSync = new DataStr.ContainerOpenSync();
			containerOpenSync.m_Guid = guid;
			containerOpenSync.m_LevelID = MyMod.levelid;
			containerOpenSync.m_LevelGUID = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
			for (int i = 0; i < MyMod.playersData.Count; i++)
			{
				if (MyMod.playersData[i] != null && MyMod.playersData[i].m_Container != null)
				{
					DataStr.ContainerOpenSync container = MyMod.playersData[i].m_Container;
					if (container.m_Guid == containerOpenSync.m_Guid && container.m_LevelID == MyMod.levelid && container.m_LevelGUID == MyMod.level_guid)
					{
						HUDMessage.AddMessage(MyMod.playersData[i].m_Name + " IS USING THIS");
						GameAudioManager.PlayGUIError();
						return false;
					}
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(RopeAnchorPoint), "Start")]
	public static class RopeAnchorPoint_Hook
	{
		public static void Postfix(RopeAnchorPoint __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.IsDead && !GameManager.GetPlayerStruggleComponent().InStruggle() && !__instance.m_StartHasBeenCalled)
			{
				MelonLogger.Msg("Rope Start");
				Shared.AddDeployedRopes(__instance.gameObject.transform.position, __instance.m_RopeDeployed, __instance.m_RopeSnapped, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent, needSync: true);
			}
		}
	}

	[HarmonyPatch(typeof(RopeAnchorPoint), "SnapRope")]
	public static class RopeAnchorPoint_Snapped
	{
		public static void Postfix(RopeAnchorPoint __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.IsDead && !GameManager.GetPlayerStruggleComponent().InStruggle())
			{
				MelonLogger.Msg("Rope Snapped");
				Shared.AddDeployedRopes(__instance.gameObject.transform.position, __instance.m_RopeDeployed, __instance.m_RopeSnapped, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent, needSync: true);
			}
		}
	}

	[HarmonyPatch(typeof(RopeAnchorPoint), "ActionFinished")]
	public static class RopeAnchorPoint_TakeOrDeploy
	{
		public static void Prefix(RopeAnchorPoint __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.IsDead || GameManager.GetPlayerStruggleComponent().InStruggle() || __instance.m_ActionState == RopeAnchorActionState.None || !(GameManager.m_PlayerManager != null))
			{
				return;
			}
			PlayerControlMode controlMode = GameManager.GetPlayerManagerComponent().GetControlMode();
			if (controlMode == PlayerControlMode.DeployRope || controlMode == PlayerControlMode.TakeRope)
			{
				bool flag = false;
				if (controlMode == PlayerControlMode.DeployRope)
				{
					MelonLogger.Msg("Rope Deployed");
					flag = true;
				}
				else
				{
					MelonLogger.Msg("Rope Taken");
					flag = false;
				}
				Shared.AddDeployedRopes(__instance.gameObject.transform.position, flag, __instance.m_RopeSnapped, MyMod.levelid, GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent, needSync: true);
			}
		}
	}

	[HarmonyPatch(typeof(RopeAnchorPoint), "Update")]
	public static class RopeAnchorPoint_Update
	{
		public static void Prefix(RopeAnchorPoint __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_RopeDeployed)
			{
				__instance.enabled = false;
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "RollSpawnChance", new System.Type[]
	{
		typeof(GameObject),
		typeof(float)
	})]
	public static class GameManager_SeededRandom
	{
		public static bool Prefix(GameObject go, float spawnChance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			return false;
		}

		public static void Postfix(GameObject go, float spawnChance, ref bool __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			int num = (int)go.transform.position.x;
			int num2 = (int)go.transform.position.y;
			int num3 = (int)go.transform.position.z;
			int randomSeed = GameManager.GetRandomSeed(num + num2 + num3);
			spawnChance = Mathf.Clamp(spawnChance, 0f, 100f);
			System.Random rNG = new System.Random(randomSeed);
			int num4 = (MyMod.RollChanceSeeded(spawnChance, rNG) ? 1 : 0);
			if (num4 == 0)
			{
				go.SetActive(value: false);
			}
			__result = num4 != 0;
		}
	}

	[HarmonyPatch(typeof(GameManager), "GetRandomSeed")]
	public static class GameManager_GetRandomSeed
	{
		public static bool Prefix(int seed)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			return false;
		}

		public static void Postfix(int seed, ref int __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (EmulateOldRandom)
			{
				if (EmuGenVersion == 1)
				{
					__result = seed ^ GameManager.m_SceneTransitionData.m_GameRandomSeed;
				}
				return;
			}
			string text = "";
			if (GameManager.m_SceneTransitionData.m_SceneSaveFilenameNextLoad != null && (GameManager.m_SceneTransitionData.m_SceneSaveFilenameNextLoad != "" || GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent != ""))
			{
				if (GameManager.m_SceneTransitionData.m_SceneSaveFilenameNextLoad != "")
				{
					if (GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent != GameManager.m_SceneTransitionData.m_SceneSaveFilenameNextLoad)
					{
						text = GameManager.m_SceneTransitionData.m_SceneSaveFilenameNextLoad;
					}
					else if (GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent == GameManager.m_SceneTransitionData.m_SceneSaveFilenameNextLoad)
					{
						text = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
					}
				}
				else
				{
					text = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
				}
			}
			else if (GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent != null)
			{
				text = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
			}
			else
			{
				UnityEngine.SceneManagement.SceneManager.GetActiveScene();
				text = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
			}
			int hashCode = Shared.StableHash(text);
			__result = seed + GameManager.m_SceneTransitionData.m_GameRandomSeed + hashCode;
			string logKey = text + "|" + GameManager.m_SceneTransitionData.m_GameRandomSeed;
			if (logKey != LastLoggedSeed)
			{
				// Host and guests must print the same base for the same scene, otherwise their loot differs.
				LastLoggedSeed = logKey;
				MelonLogger.Msg("[Loot seed] scene=" + text + " gameSeed=" + GameManager.m_SceneTransitionData.m_GameRandomSeed + " base=" + (GameManager.m_SceneTransitionData.m_GameRandomSeed + hashCode));
			}
		}

		private static string LastLoggedSeed;
	}

	[HarmonyPatch(typeof(RandomSpawnObject), "ActivateRandomObject")]
	public static class RandomSpawnObject_SeededFix
	{
		public static bool Prefix(RandomSpawnObject __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			return false;
		}

		public static void Postfix(RandomSpawnObject __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>(__instance.m_ObjectList);
			System.Collections.Generic.List<int> list2 = new System.Collections.Generic.List<int>(__instance.m_Weights);
			GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
			UnityEngine.Random.State state = UnityEngine.Random.state;
			UnityEngine.Random.InitState(GameManager.GetRandomSeed(__instance.transform.position.GetHashCode()));
			int numObjectsToEnableCurrentXPMode = __instance.GetNumObjectsToEnableCurrentXPMode();
			for (int i = 0; i < numObjectsToEnableCurrentXPMode; i++)
			{
				int num = 0;
				foreach (int item2 in list2)
				{
					num += item2;
				}
				if (num == 0)
				{
					UnityEngine.Random.state = state;
					return;
				}
				System.Collections.Generic.List<MapDetail> list3 = new System.Collections.Generic.List<MapDetail>();
				foreach (GameObject item3 in list)
				{
					if ((bool)item3)
					{
						MapDetail[] collection = item3.GetComponentsInChildren<MapDetail>();
						list3.AddRange(collection);
					}
				}
				int num2 = UnityEngine.Random.Range(0, num);
				int num3 = 0;
				int num4 = -1;
				for (int j = 0; j < list2.Count; j++)
				{
					int num5 = num3 + list2[j];
					if (num2 >= num3 && num2 < num5)
					{
						if ((bool)list[j])
						{
							list[j].SetActive(value: true);
							MapDetail[] array = list[j].GetComponentsInChildren<MapDetail>();
							foreach (MapDetail item in array)
							{
								list3.Remove(item);
							}
							__instance.RecheckObjectDisablers(list[j]);
						}
						num4 = j;
						break;
					}
					num3 = num5;
				}
				if (num4 >= 0)
				{
					list2.RemoveAt(num4);
					list.RemoveAt(num4);
				}
			}
			UnityEngine.Random.state = state;
		}
	}

	[HarmonyPatch(typeof(PrefabSpawn), "SpawnObjects")]
	private static class SpawnObjectCrap
	{
		private static bool Prefix(PrefabSpawn __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			return false;
		}

		private static void Postfix(PrefabSpawn __instance, ref GameObject __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_PrefabList == null)
			{
				__result = null;
				return;
			}
			int num = 0;
			int num2 = 0;
			num2 = ((!(__instance.gameObject.GetComponent<ObjectGuid>() != null) || !(__instance.gameObject.GetComponent<ObjectGuid>().Get() != "")) ? __instance.gameObject.transform.position.GetHashCode() : Shared.StableHash(__instance.gameObject.GetComponent<ObjectGuid>().Get()));
			int num3 = 0;
			for (int i = 0; i < __instance.m_PrefabList.Length; i++)
			{
				num3 = Mathf.Max(num3, __instance.m_PrefabList[i].m_SetId);
			}
			System.Collections.Generic.List<System.Collections.Generic.List<PrefabSpawn.Element>> list = new System.Collections.Generic.List<System.Collections.Generic.List<PrefabSpawn.Element>>();
			for (int j = 0; j <= num3; j++)
			{
				list.Add(new System.Collections.Generic.List<PrefabSpawn.Element>());
			}
			for (int k = 0; k < __instance.m_PrefabList.Length; k++)
			{
				PrefabSpawn.Element element = __instance.m_PrefabList[k];
				list[element.m_SetId].Add(element);
			}
			int num4 = 0;
			System.Collections.Generic.List<int> list2 = new System.Collections.Generic.List<int>();
			for (int l = 0; l < list.Count; l++)
			{
				list2.Add(0);
				int num5 = 0;
				for (int m = 0; m < list[l].Count; m++)
				{
					num5 += list[l][m].m_SpawnWeight;
				}
				list2[l] = num5;
				num4 += num5;
			}
			num++;
			int b = new System.Random(GameManager.GetRandomSeed(num2 + num)).Next(__instance.m_NumToSpawnMin, __instance.m_NumToSpawnMax + 1);
			int num6 = Mathf.Min((num3 == 0) ? __instance.m_PrefabList.Length : list.Count, b);
			int num7 = num6;
			if (__instance.m_NumToSpawnMin != 1 || __instance.m_NumToSpawnMax != 1)
			{
				num7 = Mathf.Max(0, num7 - GameManager.GetExperienceModeManagerComponent().GetReduceMaxItemsInContainer());
			}
			float num8 = __instance.m_ChanceOfNoSpawn;
			if (num7 == 0 && num6 != 0)
			{
				num7 = num6;
				num8 = Mathf.Min(75f, num8);
			}
			else if ((double)num8 > 0.0)
			{
				num8 = (float)(100.0 - (100.0 - (double)num8) * (double)GameManager.GetExperienceModeManagerComponent().GetGearSpawnChanceScale());
			}
			System.Random rNG = new System.Random(GameManager.GetRandomSeed(num2));
			if (MyMod.RollChanceSeeded(num8, rNG))
			{
				__result = null;
				return;
			}
			System.Collections.Generic.List<GameObject> list3 = new System.Collections.Generic.List<GameObject>();
			while (num7 > 0)
			{
				num++;
				int num9 = new System.Random(GameManager.GetRandomSeed(num2 + num)).Next(0, num4);
				int num10 = 0;
				for (int n = 0; n < list.Count; n++)
				{
					int num11 = num10 + list2[n];
					if (num9 < num11)
					{
						int num12 = num10;
						for (int num13 = 0; num13 < list[n].Count; num13++)
						{
							PrefabSpawn.Element element2 = list[n][num13];
							int num14 = num12 + element2.m_SpawnWeight;
							if (num9 < num14)
							{
								GameObject gameObject = __instance.SpawnObject(element2);
								if (gameObject != null)
								{
									list3.Add(gameObject);
								}
								if (list.Count == 1)
								{
									num4 -= element2.m_SpawnWeight;
									list[n].RemoveAt(num13);
								}
								break;
							}
							num12 = num14;
						}
						if (list.Count > 1)
						{
							list.RemoveAt(n);
							num4 -= list2[n];
							list2.RemoveAt(n);
						}
						break;
					}
					num10 = num11;
				}
				num7--;
			}
			__result = null;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "DrinkFromWaterSupply")]
	internal class PlayerManager_DrinkFromWaterSupply_Patch
	{
		internal static float restoreTimeToDrink;

		internal static void Prefix(PlayerManager __instance, WaterSupply ws, ItemLiquidVolume volumeAvailable)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			restoreTimeToDrink = ws.m_TimeToDrinkSeconds;
			if (!MyMod.ServerConfig.m_FastConsumption)
			{
				ws.m_TimeToDrinkSeconds = 10f;
			}
		}

		internal static void Postfix(PlayerManager __instance, WaterSupply ws)
		{
			ws.m_TimeToDrinkSeconds = restoreTimeToDrink;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "UseFoodInventoryItem")]
	internal class PlayerManager_UseFoodInventoryItem_Patch
	{
		internal static float restoreTimeToEat;

		internal static void Prefix(PlayerManager __instance, GearItem gi)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			restoreTimeToEat = gi.m_FoodItem.m_TimeToEatSeconds;
			if (!MyMod.ServerConfig.m_FastConsumption)
			{
				gi.m_FoodItem.m_TimeToEatSeconds = 11f;
			}
		}

		internal static void Postfix(PlayerManager __instance, GearItem gi)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			gi.m_FoodItem.m_TimeToEatSeconds = restoreTimeToEat;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "GetInteractiveObjectHoverText")]
	internal class PlayerManager_GetInteractiveObjectDisplayText
	{
		internal static void Postfix(PlayerManager __instance, ref string __result)
		{
			PostfixInternal(__instance, __instance.GetInteractiveObjectUnderCrosshair(), ref __result);
		}

		private static void PostfixInternal(PlayerManager __instance, GameObject interactiveObject, ref string __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(interactiveObject != null))
			{
				return;
			}
			if (interactiveObject.GetComponent<Comps.MultiplayerPlayer>() != null)
			{
				Comps.MultiplayerPlayer component = interactiveObject.GetComponent<Comps.MultiplayerPlayer>();
				int num = 0;
				string text = "";
				if (MyMod.playersData[component.m_ID] != null)
				{
					num = MyMod.playersData[component.m_ID].m_Levelid;
					text = MyMod.playersData[component.m_ID].m_LevelGuid;
				}
				if (MyMod.levelid == num || !(MyMod.level_guid != text))
				{
					string displayText = GetPriorityActionForPlayer(component.m_ID, component).m_DisplayText;
					string text2 = "";
					text2 = ((MyMod.playersData[component.m_ID] == null || !(MyMod.playersData[component.m_ID].m_Name != "")) ? "Player" : MyMod.playersData[component.m_ID].m_Name);
					if ((bool)GameManager.GetPlayerManagerComponent().m_ItemInHands && MyMod.IsCustomHandItem(GameManager.GetPlayerManagerComponent().m_ItemInHands.GetGearName()))
					{
						__result = text2;
					}
					else
					{
						__result = text2 + "\n" + displayText;
					}
				}
			}
			else if (interactiveObject.GetComponent<Comps.DroppedGearDummy>() != null)
			{
				Comps.DroppedGearDummy component2 = interactiveObject.GetComponent<Comps.DroppedGearDummy>();
				if (string.IsNullOrEmpty(component2.m_Extra.m_Dropper))
				{
					string text3 = "[87DBF5]";
					string text4 = "[-]";
					if (component2.m_Extra.m_Variant == -2)
					{
						text3 = "[50C878]";
					}
					__result = text3 + component2.m_LocalizedDisplayName + text4;
					return;
				}
				if (KeyboardUtilities.InputManager.GetKey(KeyCode.LeftShift))
				{
					string text5 = DropTimeFormat(MyMod.MinutesFromStartServer - component2.m_Extra.m_DroppedTime);
					if (string.IsNullOrEmpty(text5))
					{
						__result = component2.m_LocalizedDisplayName + "\nDropped just now";
					}
					else
					{
						__result = component2.m_LocalizedDisplayName + "\nDropped " + text5 + " ago";
					}
					return;
				}
				string text6 = "Dropped";
				if (interactiveObject.name.Contains("GEAR_KeroseneLampB"))
				{
					__result = GetLampHoverString(component2);
					return;
				}
				if (interactiveObject.name.Contains("GEAR_Snare"))
				{
					__result = GetSnareHoverString(component2);
					return;
				}
				string text7 = " " + text6 + " by " + component2.m_Extra.m_Dropper;
				if (component2.m_Extra.m_GoalTime == 0)
				{
					if (text7 != "")
					{
						__result = component2.m_LocalizedDisplayName + "\n" + text7;
					}
					else
					{
						__result = component2.m_LocalizedDisplayName;
					}
				}
				else if (component2.m_Extra.m_GoalTime == -1)
				{
					__result = component2.m_LocalizedDisplayName + "\nCannot be cured here" + text7;
				}
				else
				{
					__result = GetCureableHoverString(component2, interactiveObject.name);
				}
			}
			else if (interactiveObject.gameObject.name.Contains("Rabbit") && interactiveObject.GetComponent<Comps.AnimalCorpseObject>() != null)
			{
				__result = "Pickup";
			}
			else if (interactiveObject.gameObject.GetComponent<Comps.DeathDropContainer>() != null)
			{
				Comps.DeathDropContainer component3 = interactiveObject.gameObject.GetComponent<Comps.DeathDropContainer>();
				if (component3.m_DeathTime == -1)
				{
					__result = component3.m_Owner + "'s Backpack\nDropped long time ago";
				}
				else
				{
					__result = component3.m_Owner + "'s Backpack\nDropped " + DropTimeFormat(MyMod.MinutesFromStartServer - component3.m_DeathTime);
				}
			}
			else if (interactiveObject.gameObject.GetComponent<Comps.FakeRockCache>() != null)
			{
				__result = interactiveObject.gameObject.GetComponent<Comps.FakeRockCache>().m_Owner + "'s " + Localization.Get("GAMEPLAY_RadialRockCache");
			}
			else if ((bool)interactiveObject.GetComponent<Comps.ExpeditionInteractive>())
			{
				__result = interactiveObject.GetComponent<Comps.ExpeditionInteractive>().m_Data.m_ObjectText;
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "FindInteractiveObject", new System.Type[]
	{
		typeof(RaycastHit),
		typeof(GearItem),
		typeof(GameObject)
	}, new ArgumentType[]
	{
		ArgumentType.Normal,
		ArgumentType.Ref,
		ArgumentType.Ref
	})]
	internal class PlayerManager_FindInteractiveObject
	{
		internal static void Postfix(RaycastHit hit, ref GearItem gi, ref GameObject interactiveObj)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (hit.transform.gameObject != null)
			{
				if (hit.transform.gameObject.GetComponent<Comps.DroppedGearDummy>() != null)
				{
					gi = new GearItem();
					interactiveObj = hit.transform.gameObject;
				}
				else if (hit.transform.gameObject.GetComponent<Comps.PlayerBulletDamage>() != null)
				{
					Comps.PlayerBulletDamage component = hit.transform.gameObject.GetComponent<Comps.PlayerBulletDamage>();
					int clientId = component.m_ClientId;
					if (MyMod.playersData[clientId] != null && component.m_Player != null)
					{
						int levelid = MyMod.playersData[clientId].m_Levelid;
						string levelGuid = MyMod.playersData[clientId].m_LevelGuid;
						if (MyMod.levelid == levelid && MyMod.level_guid == levelGuid)
						{
							gi = new GearItem();
							interactiveObj = component.m_Player;
						}
					}
				}
				else if (hit.transform.gameObject.GetComponent<Comps.FakeRockCache>() != null)
				{
					gi = new GearItem();
					interactiveObj = hit.transform.gameObject;
				}
				else if (hit.transform.gameObject.GetComponent<Comps.ExpeditionInteractive>() != null)
				{
					gi = new GearItem();
					interactiveObj = hit.transform.gameObject;
				}
			}
			if (!hit.collider || !hit.collider.gameObject)
			{
				return;
			}
			if (hit.collider.gameObject.layer == 16)
			{
				if ((bool)hit.collider.gameObject.GetComponent<Comps.AnimalCorpseObject>())
				{
					gi = new GearItem();
					interactiveObj = hit.collider.gameObject.GetComponent<Comps.AnimalCorpseObject>().gameObject;
				}
			}
			else if (hit.collider.gameObject.layer == 27 && (bool)hit.collider.gameObject.transform.GetComponentInParent<Comps.AnimalCorpseObject>())
			{
				gi = new GearItem();
				interactiveObj = hit.collider.gameObject.transform.GetComponentInParent<Comps.AnimalCorpseObject>().gameObject;
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "InteractiveObjectsProcessInteraction")]
	internal class PlayerManager_InteractiveObjectsProcessInteraction
	{
		internal static bool Prefix(PlayerManager __instance)
		{
			if (__instance.GetInteractiveObjectUnderCrosshair() != null && __instance.GetInteractiveObjectUnderCrosshair().GetComponent<Comps.FakeRockCache>() != null)
			{
				return false;
			}
			return true;
		}

		internal static void Postfix(PlayerManager __instance, ref bool __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(__instance.GetInteractiveObjectUnderCrosshair() != null))
			{
				return;
			}
			GameObject interactiveObjectUnderCrosshair = __instance.GetInteractiveObjectUnderCrosshair();
			if (interactiveObjectUnderCrosshair.GetComponent<Comps.MultiplayerPlayer>() != null)
			{
				Comps.MultiplayerPlayer component = interactiveObjectUnderCrosshair.GetComponent<Comps.MultiplayerPlayer>();
				string action = GetPriorityActionForPlayer(component.m_ID, component).m_Action;
				string processText = GetPriorityActionForPlayer(component.m_ID, component).m_ProcessText;
				if ((bool)GameManager.GetPlayerManagerComponent().m_ItemInHands && MyMod.IsCustomHandItem(GameManager.GetPlayerManagerComponent().m_ItemInHands.GetGearName()))
				{
					__result = false;
					return;
				}
				switch (action)
				{
				case "Invite":
					MyMod.SendInvietToPlayer(component.m_ID);
					__result = true;
					break;
				case "Stim":
					if (GameManager.m_PlayerManager != null && GameManager.m_PlayerManager.m_ItemInHands != null && GameManager.m_PlayerManager.m_ItemInHands.m_EmergencyStim != null)
					{
						MyMod.EmergencyStimBeforeUse = GameManager.m_PlayerManager.m_ItemInHands;
						GameManager.GetPlayerManagerComponent().UseInventoryItem(GameManager.m_PlayerManager.m_ItemInHands);
						MyMod.DoLongAction(component, processText, action);
						__result = true;
					}
					break;
				case "Lit":
					if (GameManager.m_PlayerManager != null && GameManager.m_PlayerManager.PlayerHoldingTorchThatCanBeLit() && component.m_TorchIgniter != null && component.m_LightSourceOn)
					{
						GameManager.GetPlayerManagerComponent().m_ItemInHands.m_TorchItem.IgniteDelayed(2f, "", isFire: true);
						GameManager.GetPlayerAnimationComponent().LookAt(component.m_TorchIgniter.transform.position);
						__result = true;
					}
					break;
				case "Examine":
					MyMod.DoLongAction(component, processText, action);
					__result = true;
					break;
				}
			}
			else if (interactiveObjectUnderCrosshair.GetComponent<Comps.DroppedGearDummy>() != null)
			{
				MelonLogger.Msg("Trying interact with fake gear");
				if (interactiveObjectUnderCrosshair.GetComponent<Comps.FakeBed>() == null)
				{
					MelonLogger.Msg("Trying pickup fake drop");
					MyMod.PickupDroppedGear(interactiveObjectUnderCrosshair);
					__result = true;
				}
				else
				{
					MyMod.UseFakeBed(interactiveObjectUnderCrosshair);
					__result = true;
				}
			}
			else if (interactiveObjectUnderCrosshair.GetComponent<Comps.SpecialItemDummy>() != null)
			{
				MelonLogger.Msg("Trying interact with special item dummy");
				MyMod.PickupSpecialItem(interactiveObjectUnderCrosshair, interactiveObjectUnderCrosshair.GetComponent<Comps.SpecialItemDummy>().m_Extra.m_PhotoGUID);
				__result = true;
			}
			else if (interactiveObjectUnderCrosshair.GetComponent<Comps.AnimalCorpseObject>() != null && interactiveObjectUnderCrosshair.name.Contains("Rabbit"))
			{
				if (interactiveObjectUnderCrosshair.GetComponent<ObjectGuid>() != null)
				{
					MyMod.AttemptToPickupRabbit(interactiveObjectUnderCrosshair.GetComponent<ObjectGuid>().Get());
				}
				__result = true;
			}
			else if (interactiveObjectUnderCrosshair.GetComponent<Comps.FakeRockCache>() != null)
			{
				MyMod.ShowRockStashActionPicker(interactiveObjectUnderCrosshair);
				__result = true;
			}
			else if (interactiveObjectUnderCrosshair.GetComponent<Comps.ExpeditionInteractive>() != null)
			{
				__result = interactiveObjectUnderCrosshair.GetComponent<Comps.ExpeditionInteractive>().TryInteract();
			}
		}
	}

	[HarmonyPatch(typeof(Container), "BeginContainerClose")]
	public static class Container_UsingSyncClose
	{
		private static bool Prefix(Container __instance)
		{
			return !CoopContainerFlow.CloseFromNative(__instance);
		}

		public static void Postfix(Container __instance, bool __runOriginal)
		{
			if (!__runOriginal)
			{
				return;
			}
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.MyContainer == null)
			{
				return;
			}
			MyMod.MyContainer = null;
			DataStr.ContainerOpenSync containerOpenSync = new DataStr.ContainerOpenSync();
			containerOpenSync.m_Guid = "NULL";
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(66);
				packet.Write(containerOpenSync);
				SendTCPData(packet);
			}
			if (!MyMod.iAmHost)
			{
				return;
			}
			using (new Packet(66))
			{
				ServerSend.CONTAINERINTERACT(0, containerOpenSync, toEveryOne: true);
			}
		}
	}

	[HarmonyPatch(typeof(Container), "BeginContainerOpen")]
	public static class Container_UsingSyncOpen
	{
		public static void Postfix(Container __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			CoopContainerFlow.Prefetch(__instance);
			DataStr.ContainerOpenSync containerOpenSync = new DataStr.ContainerOpenSync();
			containerOpenSync.m_LevelID = MyMod.levelid;
			containerOpenSync.m_LevelGUID = MyMod.level_guid;
			containerOpenSync.m_Inspected = __instance.m_Inspected;
			if (__instance.gameObject != null)
			{
				GameObject gameObject = __instance.gameObject;
				if (gameObject.GetComponent<ObjectGuid>() != null)
				{
					containerOpenSync.m_Guid = gameObject.GetComponent<ObjectGuid>().Get();
				}
				for (int i = 0; i < __instance.m_Items.Count; i++)
				{
					GearItem gearItem = __instance.m_Items[i];
					if (gearItem != null && gearItem.GetGearName().Contains("MountainTownFarmKey"))
					{
						return;
					}
				}
			}
			if (MyMod.MyContainer != null && MyMod.MyContainer.Equals(containerOpenSync))
			{
				return;
			}
			MyMod.MyContainer = containerOpenSync;
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(66);
				packet.Write(MyMod.MyContainer);
				SendTCPData(packet);
			}
			if (MyMod.iAmHost)
			{
				using (new Packet(66))
				{
					ServerSend.CONTAINERINTERACT(0, MyMod.MyContainer, toEveryOne: true);
				}
			}
		}
	}

	[HarmonyPatch(typeof(ContainerInteraction), "GetHoverText")]
	public static class Container_GetInteractiveDisplayText
	{
		public static void Postfix(ContainerInteraction __instance, ref string __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_Container != null && __instance.m_Container.IsInspected() && __instance.gameObject.GetComponent<Comps.ContainersSync>() != null)
			{
				if (!__instance.gameObject.GetComponent<Comps.ContainersSync>().m_Empty)
				{
					__result = __result + "\n" + Localization.Get("GAMEPLAY_SearchedPostfix");
				}
				else
				{
					__result = __result + "\n" + Localization.Get("GAMEPLAY_EmptyPostfix");
				}
			}
		}
	}

	[HarmonyPatch(typeof(HarvestableInteraction), "BeginHold")]
	public static class Harvestable_DoHarvest
	{
		public static void Postfix(HarvestableInteraction __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			SendHarvestPlantState("Start", __instance.m_Harvestable);
		}
	}

	[HarmonyPatch(typeof(HarvestableInteraction), "EndHold")]
	public static class Harvestable_CancelHarvest
	{
		public static void Postfix(HarvestableInteraction __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			SendHarvestPlantState("Cancel", __instance.m_Harvestable);
		}
	}

	[HarmonyPatch(typeof(Harvestable), "RollSpawnChance")]
	public static class Harvestable_RollSpawnChance
	{
		public static bool Prefix(Harvestable __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			int seed = 0;
			if ((bool)__instance.gameObject.GetComponent<ObjectGuid>())
			{
				seed = Shared.GetVectorHash(__instance.gameObject.transform.position);
			}
			System.Random rNG = new System.Random(seed);
			if (!__instance.m_IgnoreCustomModeSpawnChance && GameManager.InCustomMode())
			{
				__instance.m_SpawnChance *= GameManager.GetExperienceModeManagerComponent().GetCustomPlantSpawnModifier();
			}
			if (Utils.Approximately(__instance.m_SpawnChance, 100f) || MyMod.RollChanceSeeded(__instance.m_SpawnChance, rNG))
			{
				return false;
			}
			__instance.m_Harvested = true;
			__instance.gameObject.SetActive(value: false);
			return false;
		}
	}

	[HarmonyPatch(typeof(Harvestable), "Harvest")]
	public static class Harvestable_CompletedHarvest
	{
		public static void Postfix(Harvestable __instance)
		{
			OnHarvestFinished(__instance, success: true);
		}

		private static void OnHarvestFinished(Harvestable __instance, bool success)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (success)
			{
				SendHarvestPlantState("Done", __instance);
			}
		}
	}

	[HarmonyPatch(typeof(HarvestableInteraction), "PerformInteraction")]
	public static class Harvestable_ProcessInteraction
	{
		public static bool Prefix(HarvestableInteraction __instance)
		{
			if (!(__instance.m_Harvestable == null))
			{
				return PrefixInternal(__instance.m_Harvestable);
			}
			return true;
		}

		private static bool PrefixInternal(Harvestable __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			string text = "";
			if (__instance.gameObject != null)
			{
				GameObject gameObject = __instance.gameObject;
				if (gameObject.GetComponent<ObjectGuid>() != null)
				{
					text = gameObject.GetComponent<ObjectGuid>().Get();
				}
				for (int i = 0; i < MyMod.playersData.Count; i++)
				{
					if (MyMod.playersData[i] != null && MyMod.playersData[i].m_HarvestingAnimal == text)
					{
						HUDMessage.AddMessage(MyMod.playersData[i].m_Name + " IS ALREADY COLLECTING THIS");
						return false;
					}
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_Boot), "Update")]
	public class Panel_Boot_Update
	{
		public static bool Prefix(Panel_Boot __instance)
		{
			if (MyMod.KillOnUpdate)
			{
				return false;
			}
			return true;
		}

		public static void Postfix(Panel_Boot __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.KillOnUpdate)
			{
				if ((bool)InputManager.instance && InputManager.GetEscapePressed(InputManager.m_CurrentContext))
				{
					Application.Quit();
				}
			}
			else if ((NeedSkipCauseConnect() || MyMod.DedicatedServerAppMode) && __instance.m_MainDisclaimerContinue != null)
			{
				__instance.m_MainDisclaimerContinue.gameObject.SetActive(value: false);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Boot), "Initialize")]
	public class Panel_Boot_Initialize
	{
		public static void Postfix(Panel_Boot __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (PlayerPrefs.GetString("Language", "English") == "Russian")
			{
				MyMod.DefaultIsRussian = true;
			}
			if (MyMod.KillOnUpdate)
			{
				UnityEngine.Object.Destroy(__instance.m_MainDisclaimerContinue.gameObject.GetComponent<UILocalize>());
				__instance.m_MainDisclaimerContinue.text = "Press Esc to Exit";
				__instance.m_MainDisclaimerContinue.mText = "Press Esc to Exit";
				__instance.m_MainDisclaimerContinue.ProcessText();
				__instance.m_MainDisclaimerContinue.gameObject.SetActive(value: true);
				for (int i = 1; i <= 3; i++)
				{
					Transform transform = __instance.gameObject.transform.Find("Label_Disclaimer_" + i);
					if (!transform)
					{
						continue;
					}
					GameObject gameObject = transform.gameObject;
					if (i < 3)
					{
						UILabel component = gameObject.GetComponent<UILabel>();
						UILocalize component2 = gameObject.GetComponent<UILocalize>();
						if ((bool)component2)
						{
							UnityEngine.Object.Destroy(component2);
						}
						string text = "Installation of missing plugins finished!";
						if (i == 2)
						{
							text = "The missing plugins for the mod have been installed. Please restart the game.\nIf this your first installation, autoupating plugin will download dependencies, so it can take a while.";
						}
						if ((bool)component)
						{
							component.mText = text;
							component.text = text;
							component.ProcessText();
						}
						gameObject.gameObject.SetActive(value: true);
					}
					else
					{
						gameObject.gameObject.SetActive(value: false);
					}
				}
			}
			else if (NeedSkipCauseConnect() || MyMod.DedicatedServerAppMode)
			{
				for (int j = 1; j <= 3; j++)
				{
					__instance.gameObject.transform.Find($"Label_Disclaimer_{j}")?.gameObject.SetActive(value: false);
				}
				__instance.NotifyDoneLoading();
			}
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "UpdateFading")]
	internal class Panel_MainMenu_UpdateFading
	{
		private static void Postfix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			RemoveSinglePlayer(__instance);
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "Enable")]
	internal class Panel_MainMenu_Enable
	{
		private static void Postfix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			RemoveSinglePlayer(__instance);
			BasicMenu basicMenu = ((__instance == null) ? null : __instance.m_BasicMenu);
			if (basicMenu != null && basicMenu.m_ItemModelList != null)
			{
				for (int i = 0; i < basicMenu.m_ItemModelList.Count; i++)
				{
					BasicMenu.BasicMenuItemModel basicMenuItemModel = basicMenu.m_ItemModelList[i];
					if (basicMenuItemModel != null && basicMenuItemModel.m_Id == "31")
					{
						basicMenu.SetItemSelected(i);
						break;
					}
				}
			}
			if (!NeedSkipCauseConnect() && !MyMod.DedicatedServerAppMode)
			{
				return;
			}
			MoviePlayer.m_HasIntroPlayedForMainMenu = true;
			Transform transform = GameCompat.ResolveChildPath(MyMod.m_Panel_MainMenu, 0, 0, 0, 5, 2);
			if (!(transform == null))
			{
				for (int j = 0; j < 4 && j < transform.childCount; j++)
				{
					transform.GetChild(j).gameObject.SetActive(value: false);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "OnMainMenuTop")]
	internal class Panel_MainMenu_OnMainMenuTop
	{
		private static void Postfix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			RemoveSinglePlayer(__instance);
		}
	}

	[HarmonyPatch(typeof(TextInputField), "Start")]
	internal class TextInputField_MoreLetters
	{
		private static void Postfix(TextInputField __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_Input.characterLimit < 100)
			{
				__instance.m_MaxLength = 100u;
				__instance.m_Input.characterLimit = (int)__instance.m_MaxLength;
			}
		}
	}

	[HarmonyPatch(typeof(SnowShelter), "Start")]
	internal class SnowShelter_Serialize
	{
		private static bool Prefix(SnowShelter __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_StartHasBeenCalled)
			{
				return false;
			}
			__instance.m_StartHasBeenCalled = true;
			if (__instance.gameObject != null && __instance.gameObject.GetComponent<Comps.DoNotSerializeThis>() == null)
			{
				SnowShelterManager.Add(__instance);
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(Panel_SnowShelterBuild), "BuildFinished")]
	public static class Panel_SnowShelterBuild_BuildFinished
	{
		public static void Prefix(Panel_SnowShelterBuild __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_SnowShelter != null)
			{
				MelonLogger.Msg("Shelter builded!");
				GameObject gameObject = __instance.m_SnowShelter.gameObject;
				Shared.ShelterCreated(gameObject.transform.position, gameObject.transform.rotation, MyMod.levelid, MyMod.level_guid, needSync: true);
			}
		}
	}

	[HarmonyPatch(typeof(SnowShelter), "DismantleFinished")]
	public static class SnowShelter_DismantleFinished
	{
		public static void Postfix(SnowShelter __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("Shelter removed!");
			Shared.ShelterRemoved(__instance.gameObject.transform.position, MyMod.levelid, MyMod.level_guid, needSync: true);
		}
	}

	[HarmonyPatch(typeof(Panel_SnowShelterInteract), "OnUse")]
	public static class Panel_SnowShelterInteract_OnUse
	{
		public static void Postfix(Panel_SnowShelterInteract __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(__instance.m_SnowShelter != null))
			{
				return;
			}
			MelonLogger.Msg("Entering shelter");
			DataStr.ShowShelterByOther showShelterByOther = new DataStr.ShowShelterByOther();
			showShelterByOther.m_Position = __instance.m_SnowShelter.gameObject.transform.position;
			showShelterByOther.m_Rotation = __instance.m_SnowShelter.gameObject.transform.rotation;
			showShelterByOther.m_LevelID = MyMod.levelid;
			showShelterByOther.m_LevelGUID = MyMod.level_guid;
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(76);
				packet.Write(showShelterByOther);
				SendTCPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.USESHELTER(0, showShelterByOther, toEveryOne: true);
			}
		}
	}

	[HarmonyPatch(typeof(SnowShelterManager), "EnterShelter")]
	public static class SnowShelterManager_EnterShelter
	{
		public static void Postfix(SnowShelterManager __instance, SnowShelter ss)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(ss != null))
			{
				return;
			}
			MelonLogger.Msg("Entering shelter");
			DataStr.ShowShelterByOther showShelterByOther = new DataStr.ShowShelterByOther();
			showShelterByOther.m_Position = ss.gameObject.transform.position;
			showShelterByOther.m_Rotation = ss.gameObject.transform.rotation;
			showShelterByOther.m_LevelID = MyMod.levelid;
			showShelterByOther.m_LevelGUID = MyMod.level_guid;
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(76);
				packet.Write(showShelterByOther);
				SendTCPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.USESHELTER(0, showShelterByOther, toEveryOne: true);
			}
		}
	}

	[HarmonyPatch(typeof(SnowShelterManager), "ExitShelter")]
	public static class SnowShelterManager_ExitShelter
	{
		public static void Postfix(SnowShelterManager __instance, SnowShelter ss)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!(ss != null))
			{
				return;
			}
			MelonLogger.Msg("Exiting shelter");
			DataStr.ShowShelterByOther showShelterByOther = new DataStr.ShowShelterByOther();
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(76);
				packet.Write(showShelterByOther);
				SendTCPData(packet);
			}
			if (MyMod.iAmHost)
			{
				ServerSend.USESHELTER(0, showShelterByOther, toEveryOne: true);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_SnowShelterInteract), "OnDismantle")]
	public static class Panel_SnowShelterInteract_OnDismantle
	{
		public static bool Prefix(Panel_SnowShelterInteract __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			DataStr.ShowShelterByOther showShelterByOther = new DataStr.ShowShelterByOther();
			if (__instance.m_SnowShelter != null && __instance.m_SnowShelter.gameObject != null)
			{
				showShelterByOther.m_Position = __instance.m_SnowShelter.gameObject.transform.position;
				showShelterByOther.m_Rotation = __instance.m_SnowShelter.gameObject.transform.rotation;
				showShelterByOther.m_LevelID = MyMod.levelid;
				showShelterByOther.m_LevelGUID = MyMod.level_guid;
			}
			for (int i = 0; i < MyMod.playersData.Count; i++)
			{
				if (MyMod.playersData[i] != null)
				{
					DataStr.ShowShelterByOther shelter = MyMod.playersData[i].m_Shelter;
					if (showShelterByOther == shelter)
					{
						HUDMessage.AddMessage(MyMod.playersData[i].m_Name + " INSIDE, CAN'T DISMANTLE THIS!");
						return false;
					}
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Fire), "AddFuel")]
	public static class Fire_AddFuel
	{
		public static void Postfix(Fire __instance, GearItem fuel)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.gameObject != null && fuel != null)
			{
				MelonLogger.Msg("[Fire][AddFuel] " + __instance.gameObject.name + " fuel is " + fuel.GetGearName());
			}
			else if (__instance.gameObject != null && fuel == null)
			{
				MelonLogger.Msg("[Fire][AddFuel] " + __instance.gameObject.name + " fuel is null");
			}
			else if (__instance.gameObject == null)
			{
				MelonLogger.Msg("[Fire][AddFuel] firesource is null");
			}
			if (!__instance.m_StartedByPlayer)
			{
				MelonLogger.Msg("Assign firesource of other player to myself");
				__instance.m_StartedByPlayer = true;
				MyMod.SendMyFire(__instance);
			}
			if (__instance.m_StartedByPlayer)
			{
				MelonLogger.Msg("Added fuel to fire " + fuel.GetGearName());
				MyMod.SendMyFire(__instance, fuel.GetGearName());
			}
		}
	}

	[HarmonyPatch(typeof(CookingSlot), "CanCookingSlotBeUsed")]
	public static class CookingSlot_CanBeInteractedWith
	{
		public static bool Prefix(CookingSlot __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_GearPlacePoint != null && __instance.m_GearPlacePoint.m_FireToAttach != null && __instance.m_GearPlacePoint.m_FireToAttach.IsBurning() && !__instance.m_GearPlacePoint.m_FireToAttach.m_StartedByPlayer)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(CookingPotItem), "CanOpenCookingInterface")]
	public static class CookingPotItem_CanOpenCookingInterface
	{
		public static bool Prefix(CookingPotItem __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.m_FireBeingUsed != null && __instance.m_FireBeingUsed.IsBurning() && !__instance.m_FireBeingUsed.m_StartedByPlayer)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(CookingPotItem), "AttachToFire")]
	public static class CookingPotItem_AttachToFire
	{
		public static bool Prefix(CookingPotItem __instance, Fire fire, GearPlacePoint gpp)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (fire != null && fire.IsBurning() && !fire.m_StartedByPlayer)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(FireplaceInteraction), "GetHoverText")]
	public static class WoodStove_GetHoverText
	{
		public static void Postfix(FireplaceInteraction __instance, ref string __result)
		{
			if (!(__instance == null) && !(__instance.TryCast<WoodStove>() == null))
			{
				if (MyMod.CrazyPatchesLogger)
				{
					StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
					MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
					MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
				}
				if (__instance.Fire != null && __instance.Fire.IsBurning() && !__instance.Fire.m_StartedByPlayer)
				{
					__result += "\nSHARED WARMTH\nADD YOUR OWN FUEL TO COOK";
				}
			}
		}
	}

	[HarmonyPatch(typeof(Campfire), "GetHoverText")]
	public static class Campfire_GetHoverText
	{
		public static void Postfix(Campfire __instance, ref string __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.Fire != null && __instance.Fire.IsBurning() && !__instance.Fire.m_StartedByPlayer)
			{
				__result += "\nSHARED WARMTH\nADD YOUR OWN FUEL TO COOK";
			}
		}
	}

	[HarmonyPatch(typeof(InterfaceManager), "Awake")]
	public static class UI_InterfaceManager
	{
		public static void Prefix(InterfaceManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null)
			{
				MyMod.m_InterfaceManager = __instance;
				MelonLogger.Msg("[Legacy UIs] Captured link on InterfaceManager!");
			}
		}
	}

	[HarmonyPatch(typeof(Panel_SelectSurvivor), "Initialize")]
	public static class UI_Panel_SelectSurvivor
	{
		public static void Prefix(Panel_SelectSurvivor __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null)
			{
				MyMod.m_Panel_SelectSurvivor = __instance;
				MelonLogger.Msg("[Legacy UIs] Captured link on Panel_SelectSurvivor!");
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Sandbox), "Initialize")]
	public static class Panel_Sandbox_Initialize
	{
		public static void Prefix(Panel_Sandbox __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null)
			{
				MyMod.m_Panel_Sandbox = __instance;
				MelonLogger.Msg("[Legacy UIs] Captured link on Panel_Sandbox!");
			}
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "Initialize")]
	public static class UI_Panel_MainMenu
	{
		public static void Prefix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null)
			{
				MyMod.m_Panel_MainMenu = __instance;
				MelonLogger.Msg("[Legacy UIs] Captured link on Panel_MainMenu!");
			}
		}
	}

	[HarmonyPatch(typeof(Panel_ChooseSandbox), "Initialize")]
	public static class Panel_ChooseSandbox_Initialize
	{
		public static void Prefix(Panel_ChooseSandbox __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance != null)
			{
				MyMod.m_Panel_ChooseSandbox = __instance;
				MelonLogger.Msg("[Legacy UIs] Captured link on Panel_ChooseSandbox!");
			}
		}
	}

	[HarmonyPatch(typeof(Condition), "KillPlayer")]
	public class Condition_Test
	{
		public static bool Prefix(Condition __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[Condition] PlayerDeath");
			if (MyMod.InOnline())
			{
				__instance.ForceKill();
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_Rest), "DoRest")]
	private static class Panel_Rest_DoRest
	{
		internal static void Prefix(Panel_Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[Panel_Rest] DoRest");
			if (__instance.m_Bed != null && __instance.m_Bed.gameObject != null && __instance.m_Bed.gameObject.GetComponent<Comps.FakeBedDummy>() != null)
			{
				MelonLogger.Msg("[Rest] Saving link on fake bed");
				FakeBedRoll = __instance.m_Bed.gameObject;
			}
		}
	}

	[HarmonyPatch(typeof(Rest), "EndSleeping")]
	public class Rest_StopSleep
	{
		public static void Postfix(Rest __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[Rest] EndSleeping");
			if (MyMod.IsDead)
			{
				GameManager.GetPlayerManagerComponent().m_God = false;
			}
			if (FakeBedRoll != null)
			{
				MelonLogger.Msg("Dummy bed removed");
				UnityEngine.Object.Destroy(FakeBedRoll);
			}
			SanityManager.ProcessedSleep(__instance.m_NumSecondsSleeping / 360f);
		}
	}

	[HarmonyPatch(typeof(Rest), "BeginSleeping", new System.Type[]
	{
		typeof(Bed),
		typeof(int),
		typeof(int)
	})]
	public class Rest_BeginSleeping
	{
		public static void Postfix(Rest __instance, Bed b, int durationHours, int maxHours)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			SanityManager.StartSleeping();
		}
	}

	[HarmonyPatch(typeof(Rest), "BeginSleeping", new System.Type[]
	{
		typeof(Bed),
		typeof(int),
		typeof(int),
		typeof(float),
		typeof(Rest.PassTimeOptions),
		typeof(Il2CppSystem.Action)
	})]
	public class Rest_BeginSleeping2
	{
		public static void Postfix(Rest __instance, Bed b, int durationHours, int maxHours, float fadeOutDuration, Rest.PassTimeOptions options, Il2CppSystem.Action onSleepEnd)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			SanityManager.StartSleeping();
		}
	}

	[HarmonyPatch(typeof(Panel_Loading), "Enable")]
	internal static class Panel_Loading_Toggle
	{
		private static void Postfix(Panel_Loading __instance, bool enable)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!enable)
			{
				MelonLogger.Msg(System.ConsoleColor.Yellow, "[Scene Load] Everything finished loading!");
				MyMod.SendAfterLoadingFinished = 2;
				if (!Shared.IsMainMenuScene(MyMod.level_name) && MyMod.level_name != "Empty" && MyMod.level_name != "Boot")
				{
					MyMod.UpdateEverything = 2;
				}
			}
			MyMod.LoadingScreenIsOn = enable;
			SpawnSafety.LoadingChanged(enable);
		}
	}

	[HarmonyPatch(typeof(MissionServicesManager), "SceneLoadCompleted")]
	public static class MissionServicesManager_Loaded
	{
		public static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[MissionServicesManager] SceneLoadCompleted!");
		}
	}

	[HarmonyPatch(typeof(SaveGameSystem), "LoadSceneData")]
	public static class SaveGameSystemPatch_LoadSceneData
	{
		public static void Postfix(string name)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[Scene Load] Saved data for scene is loaded!");
			MyMod.UpdateEverything = 2;
		}
	}

	[HarmonyPatch(typeof(Panel_Loading), "OnQuoteFinished")]
	public static class Panel_Loading_OnQuoteFinished
	{
		public static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg(System.ConsoleColor.Yellow, "[Cold Start] Scene loaded!");
			MyMod.UpdateEverything = 2;
		}
	}

	[HarmonyPatch(typeof(InputManager), "PauseGame")]
	public static class InputManager_DuckYOuPause
	{
		public static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline())
			{
				GameManager.m_IsPaused = false;
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Diagnosis), "OnBack")]
	public static class Panel_Diagnosis_OnBack
	{
		public static void Postfix(Panel_Diagnosis __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("Stop diagnosis");
			if (MyMod.DiagnosisDummy != null)
			{
				UnityEngine.Object.Destroy(MyMod.DiagnosisDummy);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Log), "RefreshSelectedSkillDescriptionView")]
	public static class Panel_Log_Initialize
	{
		public static void Postfix(Panel_Log __instance)
		{
			if (__instance.m_SkillImageLarge.mainTexture == null)
			{
				__instance.m_SkillImageLarge.mainTexture = MyMod.BundleAsset<Texture2D>(__instance.m_SkillsDisplayList[__instance.m_SkillListSelectedIndex].m_Skill.m_SkillImage);
			}
		}
	}

	[HarmonyPatch(typeof(SkillsManager), "Awake")]
	public static class SkillsManagerInitialize
	{
		public static void Postfix(SkillsManager __instance)
		{
			MyMod.AddNewSkills(__instance);
		}
	}

	[HarmonyPatch(typeof(Panel_Diagnosis), "PostTreatment")]
	public static class Panel_Diagnosis_PostTreatment
	{
		public static void Prefix(Panel_Diagnosis __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.DiagnosisDummy != null)
			{
				MelonLogger.Msg("Cured other player");
				DataStr.AffictionSync obj = new DataStr.AffictionSync
				{
					m_Location = (int)__instance.GetSelectedAffliction().m_Location,
					m_Type = (int)__instance.GetSelectedAfflictionType()
				};
				if (ShouldKeepAfterTreatment(__instance.GetSelectedAfflictionType()))
				{
					__instance.GetSelectedAffliction().m_Remedy1Complete = false;
					__instance.GetSelectedAffliction().m_RestHours = 1f;
				}
				MyMod.SendCureAffliction(obj);
				ImproveFirstAidSkill(obj.m_Type);
			}
			new System.Collections.Generic.List<string> { "PLAY_SNDVOSMMAC2150", "PLAY_SNDVOSMMACE422950_01", "PLAY_SNDVOSMMAC1770", "PLAY_SNDVOSMMAC1820", "PLAY_SNDVOSMMAC1750" };
			if (PlayerManager.m_VoicePersona == VoicePersona.Male)
			{
				System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
				list.Add("PLAY_SNDVOSMMAC590");
				list.Add("PLAY_SNDVOSMMAC1175UNCLESAM");
				list.Add("PLAY_SNDVOSMMAC2250");
				list.Add("PLAY_SNDVOSMMAC2300");
				list.Add("PLAY_SNDVOSMMAC1980");
				System.Random random = new System.Random();
				__instance.m_TreatmentSuccessVO = list[random.Next(list.Count)];
			}
			else
			{
				__instance.m_TreatmentSuccessVO = "Play_SurvivorTreat";
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "TreatAffliction")]
	public static class PlayerManager_TreatAffliction
	{
		public static void Postfix(PlayerManager __instance, FirstAidItem firstAidItem, AfflictionType afflictionType, AfflictionBodyArea location, int afflictionId)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (afflictionType != AfflictionType.SprainedAnkle && afflictionType != AfflictionType.SprainedWrist && afflictionType != AfflictionType.SprainedWristMajor && afflictionType != AfflictionType.SprainPain)
			{
				switch (MyMod.GetFirstAidSkillTier())
				{
				case 4:
					MyMod.ApplyTreatmentRegeneration(33f, 0.25f, "Self Treatment");
					GameManager.GetConditionComponent().AddHealth(5f, DamageSource.FirstAid);
					break;
				case 5:
					MyMod.ApplyTreatmentRegeneration(35f, 0.25f, "Self Treatment");
					GameManager.GetConditionComponent().AddHealth(5f, DamageSource.FirstAid);
					break;
				}
				MyMod.IncressCustomSkill(MyMod.CustomSkills.FirstAid);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Diagnosis), "RefreshRightPage")]
	public static class Panel_Diagnosis_RefreshRightPage
	{
		public static void Postfix(Panel_Diagnosis __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			__instance.m_LabelCause.color = Color.gray;
			if (!(__instance.m_SelectedAffButton != null))
			{
				return;
			}
			AfflictionType afflictionType = __instance.GetSelectedAffliction().m_Definition.m_AfflictionType;
			if (__instance.GetSelectedAffliction().m_RestHours == 1f && afflictionType != AfflictionType.Frostbite && afflictionType != AfflictionType.CabinFever)
			{
				NGUITools.SetActive(__instance.m_RightPageObject, state: false);
				if (afflictionType == AfflictionType.InfectionRisk || Affliction.IsBadAffliction(afflictionType))
				{
					__instance.m_LabelCause.text = "AFFLICTION TREATED\nPATIENT NEEDS TIME TO HEAL";
					__instance.m_LabelCause.color = new Color(0.25f, 1f, 0f);
				}
			}
		}
	}

	[HarmonyPatch(typeof(SpawnRegionManager), "Deserialize")]
	public static class SpawnRegionManager_Deserialize
	{
		public static bool Prefix(SpawnRegionManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[SpawnRegionManager] Deserialize");
			return false;
		}
	}

	[HarmonyPatch(typeof(BaseAiManager), "Deserialize")]
	public static class BaseAiManager_Deserialize
	{
		public static bool Prefix(BaseAiManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[BaseAiManager] Deserialize");
			_ = MyMod.AnimalsController;
			return false;
		}
	}

	[HarmonyPatch(typeof(BaseAiManager), "Serialize")]
	public static class BaseAiManager_Serialize
	{
		public static bool Prefix(BaseAiManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[BaseAiManager] Serialize");
			_ = MyMod.AnimalsController;
			return false;
		}
	}

	[HarmonyPatch(typeof(BodyHarvestManager), "Serialize")]
	public static class BodyHarvestManager_Serialize
	{
		public static bool Prefix(BodyHarvestManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[BodyHarvestManager] Prefix Serialize");
			return false;
		}

		public static void Postfix(BodyHarvestManager __instance, ref string __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[BodyHarvestManager] Postfix Serialize");
			BodyHarvestSaveList bodyHarvestSaveList = new BodyHarvestSaveList();
			bodyHarvestSaveList.m_SerializedBodyHarvests.Clear();
			for (int i = 0; i < BodyHarvestManager.m_BodyHarvestList.Count; i++)
			{
				BodyHarvest bodyHarvest = BodyHarvestManager.m_BodyHarvestList[i];
				if (bodyHarvest != null && bodyHarvest.gameObject.activeSelf && bodyHarvest.enabled && bodyHarvest.gameObject.GetComponent<GearItem>() == null && bodyHarvest.m_Ravaged && bodyHarvest.m_StartRavaged && bodyHarvest.gameObject.GetComponent<Comps.AnimalCorpseObject>() == null)
				{
					bodyHarvestSaveList.m_SerializedBodyHarvests.Add(new BodyHarvestSaveData
					{
						m_Position = bodyHarvest.transform.position,
						m_Rotation = bodyHarvest.transform.rotation,
						m_PrefabName = bodyHarvest.gameObject.name,
						m_Guid = ObjectGuid.GetGuidFromGameObject(bodyHarvest.gameObject),
						m_SerializedBodyHarvest = bodyHarvest.Serialize()
					});
				}
			}
			__result = Utils.SerializeObject(bodyHarvestSaveList);
		}
	}

	[HarmonyPatch(typeof(BodyHarvestManager), "Deserialize")]
	public static class BodyHarvestManager_Deserialize
	{
		public static bool Prefix(BodyHarvestManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[BodyHarvestManager] Prefix Deserialize");
			return false;
		}

		public static void Postfix(BodyHarvestManager __instance, string text)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MelonLogger.Msg("[BodyHarvestManager] Postfix Deserialize");
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			Il2CppSystem.Collections.Generic.List<BodyHarvestSaveData>.Enumerator enumerator = Utils.DeserializeObject<BodyHarvestSaveList>(text).m_SerializedBodyHarvests.GetEnumerator();
			while (enumerator.MoveNext())
			{
				BodyHarvestSaveData current = enumerator.Current;
				GameObject gameObject = ObjectGuidManager.Lookup(current.m_Guid);
				BodyHarvest bodyHarvest = null;
				if (gameObject != null)
				{
					bodyHarvest = gameObject.GetComponent<BodyHarvest>();
				}
				if (bodyHarvest == null)
				{
					bodyHarvest = BodyHarvestManager.InstantiateBodyHarvestGameObject(current);
					if (!(bodyHarvest != null))
					{
						continue;
					}
					ObjectGuid.MaybeAttachObjectGuidAndRegister(bodyHarvest.gameObject, current.m_Guid);
				}
				else
				{
					bodyHarvest.transform.position = current.m_Position;
					bodyHarvest.transform.rotation = current.m_Rotation;
				}
				bodyHarvest.Deserialize(current.m_SerializedBodyHarvest, isSceneLoad: true);
				BodyHarvestManager.MaybeRestoreCorpseState(bodyHarvest);
				BodyHarvestManager.MaybeRestoreCarcassSite(bodyHarvest);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_BodyHarvest), "QuarterSuccessful")]
	internal static class Panel_BodyHarvest_QuarterSuccessful
	{
		private static void Prefix(Panel_BodyHarvest __instance)
		{
			if (!MyMod.InOnline() || !(__instance.m_BodyHarvest != null) || !(__instance.m_BodyHarvest.gameObject != null) || !__instance.m_BodyHarvest.gameObject.GetComponent<ObjectGuid>())
			{
				return;
			}
			string text = __instance.m_BodyHarvest.gameObject.GetComponent<ObjectGuid>().Get();
			if (MyMod.iAmHost)
			{
				Shared.OnAnimalQuarted(text);
				ServerSend.QUARTERANIMAL(0, text, toAll: true);
			}
			else if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(109))
				{
					packet.Write(text);
					SendTCPData(packet);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_BodyHarvest), "SpawnQuarters")]
	internal static class Panel_BodyHarvest_SpawnQuarters
	{
		private static void Prefix(Panel_BodyHarvest __instance, out System.Collections.Generic.List<int> __state)
		{
			__state = new System.Collections.Generic.List<int>();
			if (!MyMod.InOnline())
			{
				return;
			}
			foreach (BodyHarvest item in UnityEngine.Object.FindObjectsOfType<BodyHarvest>())
			{
				if (item != null)
				{
					__state.Add(item.GetInstanceID());
				}
			}
		}

		private static void Postfix(Panel_BodyHarvest __instance, System.Collections.Generic.List<int> __state)
		{
			if (!MyMod.InOnline() || __state == null)
			{
				return;
			}
			foreach (BodyHarvest item in UnityEngine.Object.FindObjectsOfType<BodyHarvest>())
			{
				if (!(item == null) && !__state.Contains(item.GetInstanceID()))
				{
					GearItem component = item.GetComponent<GearItem>();
					if (component != null)
					{
						MyMod.SendDropItem(component);
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_BodyHarvest), "RandomRotateGutsOrHide")]
	internal static class Panel_BodyHarvest_RandomRotateGutsOrHide
	{
		private static void Postfix(Panel_BodyHarvest __instance, Transform transform)
		{
			if (MyMod.InOnline() && transform != null && transform.gameObject != null && (bool)transform.gameObject.GetComponent<GearItem>())
			{
				MyMod.SendDropItem(transform.gameObject.GetComponent<GearItem>());
			}
		}
	}

	[HarmonyPatch(typeof(BaseAi), "PlayStruggleAudio")]
	internal static class BaseAi_PlayStruggleAudio
	{
		private static void Prefix(BaseAi __instance, string audioEventName)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline() && (bool)__instance.gameObject && (bool)__instance.gameObject.GetComponent<ObjectGuid>())
			{
				SendAnimalAudio(audioEventName, __instance.gameObject.GetComponent<ObjectGuid>().Get());
			}
		}
	}

	[HarmonyPatch(typeof(BaseAi), "OnAudioEvent")]
	internal static class BaseAi_OnAudioEvent
	{
		private static void Prefix(BaseAi __instance, string audioEventName)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline() && (bool)__instance.gameObject && (bool)__instance.gameObject.GetComponent<ObjectGuid>())
			{
				SendAnimalAudio(audioEventName, __instance.gameObject.GetComponent<ObjectGuid>().Get());
			}
		}
	}

	[HarmonyPatch(typeof(BaseAi), "UpdateAttackingAudio")]
	internal static class BaseAi_UpdateAttackingAudio
	{
		private static void Prefix(BaseAi __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline() && !__instance.m_CurrentTarget && __instance.m_CurrentTarget.IsPlayer() && __instance.m_AttackingLoopAudioID == 0 && !((double)__instance.m_TimeInModeSeconds <= 1.5) && (bool)__instance.gameObject && (bool)__instance.gameObject.GetComponent<ObjectGuid>())
			{
				SendAnimalAudio(__instance.m_ChasingAudio, __instance.gameObject.GetComponent<ObjectGuid>().Get());
			}
		}
	}

	[HarmonyPatch(typeof(SpawnRegion), "Spawn")]
	internal static class SpawnRegion_Spawn
	{
		private static bool Prefix(SpawnRegion __instance, WildlifeMode wildlifeMode)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.InOnline())
			{
				return true;
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(ConsoleManager), "spawn_common")]
	internal static class ConsoleManager_Spawn
	{
		private static bool Prefix(ConsoleManager __instance, string prefabName, float dist)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.InOnline())
			{
				return true;
			}
			return MyMod.AnimalsController;
		}
	}

	[HarmonyPatch(typeof(ConsoleManager), "CONSOLE_next_weather")]
	internal static class ConsoleManager_CONSOLE_next_weather
	{
		private static bool Prefix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.InOnline())
			{
				return true;
			}
			return false;
		}

		private static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.iAmHost)
			{
				Shared.ForceNextWeather();
			}
		}
	}

	[HarmonyPatch(typeof(ConsoleManager), "CONSOLE_next_weatherset")]
	internal static class ConsoleManager_CONSOLE_next_weatherset
	{
		private static bool Prefix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.InOnline())
			{
				return true;
			}
			return false;
		}

		private static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.iAmHost)
			{
				Shared.ForceNextWeatherSet();
			}
		}
	}

	[HarmonyPatch(typeof(SpawnRegion), "MaybeReRollActive")]
	internal static class SpawnRegion_MaybeReRollActive
	{
		private static void Postfix(SpawnRegion __instance)
		{
			if (MyMod.InOnline() && __instance.gameObject != null)
			{
				Comps.SpawnRegionSimple component = __instance.gameObject.GetComponent<Comps.SpawnRegionSimple>();
				if (component != null)
				{
					component.UpdateFromManager();
				}
			}
		}
	}

	[HarmonyPatch(typeof(SpawnRegion), "AdjustActiveSpawnRegionPopulation")]
	internal static class SpawnRegion_AdjustActiveSpawnRegionPopulation
	{
		private static bool Prefix()
		{
			return !MyMod.InOnline();
		}
	}

	[HarmonyPatch(typeof(BaseAi), "MaybeApplyAttack")]
	internal static class BaseAi_InitialHostAttackGate
	{
		private static bool Prefix(BaseAi __instance)
		{
			return !SpawnSafety.DeferLocalAttack(__instance);
		}
	}

	[HarmonyPatch(typeof(Container), "ShowContainerContents")]
	internal static class Container_ShowInspectedContents
	{
		private static bool Prefix(Container __instance)
		{
			if (!MyMod.InOnline() || __instance.m_SearchInProgress)
			{
				return true;
			}
			CoopContainerFlow.MarkReopen(__instance);
			MyMod.OpenFakeContainer(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(Container), "ShowItemsAfterSearch")]
	internal static class Container_OnOpenComplete
	{
		private static bool Prefix(Container __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline())
			{
				MelonLogger.Msg("Opening container, request fake container");
				if (CoopContainerFlow.StopNativeAdvance(__instance))
				{
					return false;
				}
				if (CoopContainerFlow.NativePresentationAllowed(__instance))
				{
					return true;
				}
				CoopContainerFlow.ClearReopen(__instance);
				MyMod.OpenFakeContainer(__instance);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_Container), "OnDone")]
	internal static class Panel_Container_Close
	{
		private static bool Prefix(Panel_Container __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			Container container = __instance.m_Container;
			if (MyMod.InOnline())
			{
				if (__instance.ShouldEnterSectionNav() && __instance.m_Container != null && !__instance.m_Container.m_FilterLocked)
				{
					__instance.EnterSectionNav();
				}
				else
				{
					if (__instance.m_DraggedItem != null)
					{
						__instance.m_DraggedItem.TryForceDrop(dropOnHovered: false);
						__instance.FinishDragDrop();
					}
					MyMod.CloseFakeContainer(container);
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "InstantiateItemAtPlayersFeet", new System.Type[]
	{
		typeof(GearItem),
		typeof(int)
	})]
	internal static class PlayerManager_InstantiateItemAtPlayersFeet
	{
		private static void Postfix(GearItem gearItemPrefab, int numUnits, GearItem __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline())
			{
				MyMod.SendDropItem(__result);
			}
		}
	}

	[HarmonyPatch]
	internal static class Application_QuitSaveGate
	{
		[HarmonyPriority(int.MaxValue)]
		private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(Application), "Quit", System.Type.EmptyTypes);
			yield return AccessTools.Method(typeof(Application), "Quit", new System.Type[1] { typeof(int) });
		}

		private static bool Prefix()
		{
			bool num = MyMod.HandleWantsToQuit();
			if (!num)
			{
				MelonLogger.Msg("[SaveQuit] Programmatic quit intercepted before shutdown.");
			}
			return num;
		}
	}

	[HarmonyPatch(typeof(Panel_PauseMenu), "DoQuitGame")]
	internal static class Panel_PauseMenu_Quit
	{
		private static bool Prefix(Panel_PauseMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline())
			{
				MyMod.RequestSaveBeforeQuit();
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(GameManager), "OnApplicationFocus")]
	internal static class GameManager_OnApplicationFocus
	{
		private static bool Prefix(bool focusStatus)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(OpenClose), "Open", new System.Type[]
	{
		typeof(bool),
		typeof(bool)
	})]
	internal static class OpenClose_Open
	{
		private static void Postfix(OpenClose __instance, bool isImmediate, bool fromLink)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (isImmediate)
			{
				return;
			}
			string gUID = "";
			if (__instance.gameObject != null && __instance.gameObject.GetComponent<ObjectGuid>() != null)
			{
				gUID = __instance.gameObject.GetComponent<ObjectGuid>().Get();
			}
			if (MyMod.InOnline())
			{
				if (MyMod.iAmHost)
				{
					Shared.ChangeOpenableThingState(MyMod.level_guid, gUID, state: true);
				}
				else
				{
					MyMod.SendOpenableThing(MyMod.level_guid, gUID, state: true);
				}
			}
		}
	}

	[HarmonyPatch(typeof(OpenClose), "Close", new System.Type[]
	{
		typeof(bool),
		typeof(bool)
	})]
	internal static class OpenClose_Close
	{
		private static void Postfix(OpenClose __instance, bool isImmediate, bool fromLink)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (isImmediate)
			{
				return;
			}
			string gUID = "";
			if (__instance.gameObject != null && __instance.gameObject.GetComponent<ObjectGuid>() != null)
			{
				gUID = __instance.gameObject.GetComponent<ObjectGuid>().Get();
			}
			if (MyMod.InOnline())
			{
				if (MyMod.iAmHost)
				{
					Shared.ChangeOpenableThingState(MyMod.level_guid, gUID, state: false);
				}
				else
				{
					MyMod.SendOpenableThing(MyMod.level_guid, gUID, state: false);
				}
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "LoadSceneWithLoadingScreen")]
	private static class GameManager_LoadSceneWithLoadingScreen
	{
		private static void Prefix(ref string sceneName)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InterloperHook || !(MyMod.OverridedSceneForSpawn == ""))
			{
				if (MyMod.InterloperHook)
				{
					MyMod.InterloperHook = false;
				}
				if (MyMod.OverridedSceneForSpawn != "")
				{
					sceneName = MyMod.OverridedSceneForSpawn;
					MyMod.OverridedSceneForSpawn = "";
				}
				MelonLogger.Msg(System.ConsoleColor.Magenta, "[LoadSceneWithLoadingScreen] " + sceneName);
			}
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "TeleportPlayerAfterSceneLoad")]
	private static class PlayerManager_TeleportPlayerAfterSceneLoad
	{
		private static void Postfix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.OverridedPositionForSpawn != Vector3.zero)
			{
				__instance.TeleportPlayer(MyMod.OverridedPositionForSpawn, GameManager.GetPlayerTransform().rotation);
				MelonLogger.Msg(System.ConsoleColor.Magenta, "[TeleportPlayerAfterSceneLoad] X " + MyMod.OverridedPositionForSpawn.x + " Y " + MyMod.OverridedPositionForSpawn.y + " Z " + MyMod.OverridedPositionForSpawn.z);
				MyMod.OverridedPositionForSpawn = Vector3.zero;
			}
			if (!string.IsNullOrEmpty(SanityManager.m_TransitionDataBackup))
			{
				GameManager.m_SceneTransitionData = Utils.DeserializeObject<SceneTransitionData>(SanityManager.m_TransitionDataBackup);
				SanityManager.m_TransitionDataBackup = "";
				GameManager.ForceSaveGame();
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "Start")]
	internal static class GameManager_Start
	{
		private static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.UpdateSceneGUID();
		}
	}

	[HarmonyPatch(typeof(GameManager), "LoadSlotOnStart")]
	internal static class GameManager_LoadSlotOnStart
	{
		private static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.UpdateSceneGUID();
		}
	}

	[HarmonyPatch(typeof(GearItem), "Deserialize")]
	public class GearItem_Deserialize
	{
		public static void Postfix(GearItem __instance, GearItemSaveDataProxy proxy, bool applyPositioningFix)
		{
			if (__instance.GetGearName() == "GEAR_SCDecoy" && proxy != null)
			{
				UnityEngine.Object.Destroy(__instance.gameObject);
			}
		}
	}

	[HarmonyPatch(typeof(SpawnRegion), "Start")]
	private static class SpawnRegion_Start
	{
		private static void Postfix(SpawnRegion __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (__instance.gameObject != null && __instance.gameObject.GetComponent<Comps.SpawnRegionSimple>() == null)
			{
				Comps.SpawnRegionSimple spawnRegionSimple = __instance.gameObject.AddComponent<Comps.SpawnRegionSimple>();
				spawnRegionSimple.m_Region = __instance;
				spawnRegionSimple.m_GUID = __instance.gameObject.GetComponent<ObjectGuid>().Get();
			}
		}
	}

	[HarmonyPatch(typeof(SpawnRegion), "UpdateFromManager")]
	private static class SpawnRegion_UpdateFromManager
	{
		private static void Postfix(SpawnRegion __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "DisableMainMenuButtonColliders")]
	private static class Panel_MainMenu_DisableMainMenuButtonColliders
	{
		private static void Postfix(Panel_MainMenu __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MenuChange.ChangeMenuItems("Original");
		}
	}

	[HarmonyPatch(typeof(WildlifeItem), "OnReleased")]
	private static class WildlifeItem_OnReleased
	{
		internal static bool Prefix(WildlifeItem __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline())
			{
				if (MyMod.AnimalsController)
				{
					MyMod.OnReleaseRabbit(ClientUser.myId);
				}
				else if (MyMod.iAmHost)
				{
					ServerSend.RELEASERABBIT(0, MyMod.level_guid);
				}
				else if (MyMod.sendMyPosition)
				{
					using Packet packet = new Packet(113);
					packet.Write(MyMod.level_guid);
					SendTCPData(packet);
				}
				__instance.ResetControls();
				if ((bool)__instance.gameObject)
				{
					UnityEngine.Object.Destroy(__instance.gameObject);
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(StoneItem), "OnCollisionEnter")]
	private static class StoneItem_OnCollisionEnter
	{
		private static bool Prefix(StoneItem __instance, Collision collisionInfo)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.InOnline())
			{
				Il2CppStructArray<ContactPoint> contacts = collisionInfo.contacts;
				for (int i = 0; i < contacts.Length; i++)
				{
					Comps.AnimalActor componentInParent = contacts[i].otherCollider.GetComponentInParent<Comps.AnimalActor>();
					if (!(componentInParent != null) || !componentInParent.gameObject.GetComponent<ObjectGuid>())
					{
						continue;
					}
					if (MyMod.sendMyPosition)
					{
						using Packet packet = new Packet(114);
						packet.Write(componentInParent.gameObject.GetComponent<ObjectGuid>().Get());
						packet.Write(componentInParent.m_ClientController);
						SendTCPData(packet);
					}
					else if (MyMod.iAmHost)
					{
						ServerSend.HITRABBIT(componentInParent.m_ClientController, componentInParent.gameObject.GetComponent<ObjectGuid>().Get());
					}
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "UseFPSMeshItem")]
	private static class PlayerManager_UseFPSMeshItem
	{
		private static bool Prefix(PlayerManager __instance, GearItem gi, ref bool __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			int fPSMeshID = gi.GetFPSMeshID();
			if (fPSMeshID == -1)
			{
				__result = false;
				return false;
			}
			if (GameManager.GetPlayerManagerComponent().m_ItemInHands != null && GameManager.GetPlayerManagerComponent().m_ItemInHands.GetFPSMeshID() == fPSMeshID && GameManager.GetPlayerManagerComponent().m_ItemInHands != null)
			{
				if (GameManager.GetPlayerManagerComponent().m_ItemInHands.GetGearName() == gi.GetGearName())
				{
					__result = false;
					return false;
				}
				__instance.EquipItem(gi, fromDeserialize: false);
				GameManager.GetPlayerAnimationComponent().SetItemEquippedByPlayer(equippedByPlayer: true);
				__result = true;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "UnequipItemInHands")]
	private static class PlayerManager_UnequipItemInHands
	{
		private static bool Prefix(PlayerManager __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.VanilaRadio && (bool)__instance.m_ItemInHands && __instance.m_ItemInHands.GetFPSMeshID() == 17)
			{
				GameManager.GetPlayerManagerComponent().UnequipItemInHandsSkipAnimation();
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "EquipItem")]
	private static class PlayerManager_EquipItem
	{
		private static void Prefix(PlayerManager __instance, GearItem gi)
		{
			if (!MyMod.IsUserGeneratedHandItem(gi.GetGearName()) && !(gi.GetGearName() == "GEAR_SCNote"))
			{
				return;
			}
			StoneItem stoneItem = gi.gameObject.AddComponent<StoneItem>();
			stoneItem.m_RigidBody = gi.gameObject.AddComponent<Rigidbody>();
			stoneItem.m_RigidBody.isKinematic = true;
			stoneItem.m_GearItem = gi;
			stoneItem.m_CanThrow = false;
			gi.m_StoneItem = stoneItem;
			if (!gi.m_ObjectGuid || string.IsNullOrEmpty(gi.m_ObjectGuid.m_Guid))
			{
				return;
			}
			if (gi.GetGearName() == "GEAR_SCPhoto")
			{
				if ((bool)MyMod.ViewModelPhoto)
				{
					MyMod.ViewModelPhoto.gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = gi.gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture;
				}
			}
			else if (gi.GetGearName() == "GEAR_SCMapPiece" && (bool)MyMod.ViewModelMap)
			{
				MyMod.ViewModelMap.gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture = gi.gameObject.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.mainTexture;
			}
		}

		private static void Postfix(PlayerManager __instance, GearItem gi)
		{
			if (!MyMod.IsUserGeneratedHandItem(gi.GetGearName()) && !(gi.GetGearName() == "GEAR_SCNote"))
			{
				return;
			}
			StoneItem component = gi.gameObject.GetComponent<StoneItem>();
			if ((bool)component)
			{
				if ((bool)component.m_RigidBody)
				{
					UnityEngine.Object.Destroy(component.m_RigidBody);
				}
				UnityEngine.Object.Destroy(component);
			}
			gi.m_StoneItem = null;
		}
	}

	[HarmonyPatch(typeof(ItemDescriptionPage), "GetEquipButtonLocalizationId")]
	private static class ItemDescriptionPage_GetEquipButtonLocalizationId
	{
		private static void Postfix(ItemDescriptionPage __instance, GearItem gi, ref string __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (gi != null)
			{
				if (MyMod.IsCustomHandItem(gi.GetGearName()) || MyMod.IsUserGeneratedHandItem(gi.GetGearName()) || gi.GetGearName() == "GEAR_SCHeatPackB" || gi.GetGearName() == "GEAR_SCPresent")
				{
					__result = "GAMEPLAY_Use";
				}
				else if (gi.GetGearName() == "GEAR_SCNote")
				{
					__result = "GAMEPLAY_Read";
				}
			}
		}
	}

	[HarmonyPatch(typeof(ItemDescriptionPage), "CanDrop", new System.Type[] { typeof(GearItem) })]
	private static class ItemDescriptionPage_CanDrop
	{
		private static void Postfix(ItemDescriptionPage __instance, GearItem gi, ref bool __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (gi != null && (gi.GetGearName() == "GEAR_SCHeatPack" || gi.GetGearName() == "GEAR_SCHeatPackC" || gi.GetGearName() == "GEAR_SCEmergencyRation" || gi.GetGearName() == "GEAR_SCLiquidLead"))
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(ItemDescriptionPage), "OnEquip")]
	private static class ItemDescriptionPage_OnEquip
	{
		private static void Postfix(ItemDescriptionPage __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.ProcessGivingItem(SelectOnly: true);
			GearItem selectedGearItem = GameCompat.Panel<Panel_Inventory>().GetSelectedGearItem();
			if (selectedGearItem != null)
			{
				if (selectedGearItem.GetGearName() == "GEAR_SCHeatPackB")
				{
					OpenHeatPack(selectedGearItem);
				}
				else if (selectedGearItem.GetGearName() == "GEAR_SCPresent")
				{
					OpenPresent(selectedGearItem);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_HUD), "OnCollectibleNoteReadingClickBack")]
	private static class Panel_HUD_OnCollectibleNoteReadingClickBack
	{
		private static void Postfix(Panel_HUD __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (HaveReadFakeNote)
			{
				__instance.m_InspectMode_StandardElementsParent.SetActive(value: false);
				__instance.m_InspectMode_InventoryStatusSprite.gameObject.SetActive(value: false);
				__instance.m_InspectMode_Equip.gameObject.SetActive(value: false);
				__instance.m_InspectMode_Take.gameObject.SetActive(value: false);
				__instance.m_InspectMode_PutBack.gameObject.SetActive(value: false);
				HaveReadFakeNote = false;
				GameManager.GetPlayerManagerComponent().m_Gear = null;
			}
		}
	}

	[HarmonyPatch(typeof(PlayerAnimation), "OnAnimationEvent_Generic_HiddenComplete")]
	private static class PlayerAnimation_OnAnimationEvent_Generic_HiddenComplete
	{
		private static void Postfix(PlayerAnimation __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.RetakeItem();
		}
	}

	[HarmonyPatch(typeof(PlayerAnimation), "OnAnimationEvent_Generic_Throw_ReleaseItem")]
	private static class PlayerAnimation_OnAnimationEvent_Generic_Throw_ReleaseItem
	{
		private static void Postfix(PlayerAnimation __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.ShouldPerformAttack)
			{
				MyMod.ShouldPerformAttack = false;
				MyMod.MeleeHit();
			}
		}
	}

	[HarmonyPatch(typeof(PlayerAnimation), "UpdateAnimatorSpeed")]
	private static class PlayerAnimation_UpdateAnimatorSpeed
	{
		private static bool Prefix(PlayerAnimation __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			bool result = true;
			if (MyMod.ShouldReEquipFaster || MyMod.ShouldPerformAttack)
			{
				result = false;
			}
			if (__instance.GetState() == PlayerAnimation.State.Showing)
			{
				MyMod.ShouldReEquipFaster = false;
			}
			return result;
		}
	}

	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new System.Type[] { typeof(GearItem) })]
	private static class Utils_GetInventoryIconTexture
	{
		private static void Postfix(GearItem gi, ref Texture2D __result)
		{
			Apply(gi, ref __result);
		}

		internal static void Apply(GearItem gi, ref Texture2D __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (gi == null)
			{
				return;
			}
			if (!MyMod.VanilaRadio && gi.GetGearName() == "GEAR_HandheldShortwave")
			{
				Texture2D texture2D = MyMod.BundleAsset<Texture2D>("ico_GearItem__HandheldShortwave");
				if (texture2D != null)
				{
					__result = texture2D;
				}
			}
			if (gi.GetGearName() == "GEAR_Shovel")
			{
				Texture2D texture2D2 = MyMod.BundleAsset<Texture2D>("ico_GearItem__Shovel");
				if (texture2D2 != null)
				{
					__result = texture2D2;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new System.Type[]
	{
		typeof(GearItem),
		typeof(float)
	})]
	private static class Utils_GetInventoryIconTextureWithCondition
	{
		private static void Postfix(GearItem gi, ref Texture2D __result)
		{
			Utils_GetInventoryIconTexture.Apply(gi, ref __result);
		}
	}

	[HarmonyPatch(typeof(Utils), "GetInventoryGridIconTexture")]
	private static class Utils_GetInventoryGridIconTexture
	{
		private static void Postfix(Utils __instance, string name, ref Texture2D __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (!MyMod.VanilaRadio && name == "ico_GearItem__HandheldShortwave")
			{
				__result = MyMod.BundleAsset<Texture2D>("ico_GearItem__HandheldShortwave");
			}
			if (name == "ico_GearItem__Shovel")
			{
				__result = MyMod.BundleAsset<Texture2D>("ico_GearItem__Shovel");
			}
		}
	}

	[HarmonyPatch(typeof(StartGear), "AddAllToInventory")]
	private static class StartGear_AddAllToInventory
	{
		private static void Postfix()
		{
			GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_HandheldShortwave", 1);
			GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_SCHeatPack", 1);
			GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_SCEmergencyRation", 1);
		}
	}

	[HarmonyPatch(typeof(Panel_Log), "UpdateMissionsPage")]
	private static class Panel_Log_UpdateMissionsPage
	{
		private static void Postfix(Panel_Log __instance)
		{
			if (MyMod.CurrentCustomChalleng.m_Started)
			{
				__instance.m_TimerObject.SetActive(MyMod.CurrentCustomChalleng.m_Time != 0);
				__instance.m_MissionNameLabel.text = MyMod.CurrentChallengeRules.m_Name;
				__instance.m_MissionNameHeaderLabel.text = MyMod.CurrentChallengeRules.m_Name;
				__instance.m_TimerLabel.text = GameCompat.Panel<Panel_ActionsRadial>().m_MissionTimerLabel.text;
				__instance.m_ChallengeTexture.mainTexture = Utils.GetLargeTexture("challenge_HopelessRescue");
				Utils.SetActive(__instance.m_ObjectiveTransform.gameObject, active: false);
			}
		}
	}

	[HarmonyPatch(typeof(InterfaceManager), "IsUsingSurvivalTabs")]
	private static class InterfaceManager_IsUsingSurvivalTabs
	{
		private static void Postfix(InterfaceManager __instance, ref bool __result)
		{
			if (MyMod.CurrentCustomChalleng.m_Started)
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(KeroseneLampItem), "ReduceFuel")]
	private static class KeroseneLampItem_ReduceFuel
	{
		private static bool Prefix(KeroseneLampItem __instance, float hoursBurned)
		{
			if (MyMod.OverrideLampReduceFuel == -1)
			{
				return true;
			}
			if (KeroseneLampItem.m_InfiniteLampOn)
			{
				MyMod.OverrideLampReduceFuel = -1;
				return true;
			}
			float num = __instance.GetModifiedFuelBurnLitersPerHour().ToLiters() / 60f;
			__instance.m_CurrentFuelLiters -= GameCompat.Liters((float)MyMod.OverrideLampReduceFuel * num);
			__instance.m_CurrentFuelLiters = GameCompat.Liters(Mathf.Clamp(__instance.m_CurrentFuelLiters.ToLiters(), 0f, __instance.m_MaxFuel.ToLiters()));
			MelonLogger.Msg("[KeroseneLampItem][ReduceFuel] Override lamp fuel. Patchedup time " + __instance.m_CurrentFuelLiters);
			MelonLogger.Msg("[KeroseneLampItem][ReduceFuel] Game wanted remove " + hoursBurned + " hours but we replace it on " + MyMod.OverrideLampReduceFuel + " minutes");
			MyMod.OverrideLampReduceFuel = -1;
			return false;
		}
	}

	[HarmonyPatch(typeof(PlayerManager), "OnCompletedDecalPlaceDown")]
	private static class PlayerManager_OnCompletedDecalPlaceDown
	{
		private static void Prefix(PlayerManager __instance)
		{
			if (__instance.m_DecalToPlace != null)
			{
				DeBugMenu.Replica = __instance.m_DecalToPlace;
				DeBugMenu.Replica.m_ColorTint = __instance.GetColourForDecalColour(__instance.m_DecalToPlaceColour);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_ChallengeComplete), "Initialize")]
	private static class Panel_ChallengeComplete_Initialize
	{
		private static void Prefix(Panel_ChallengeComplete __instance)
		{
			MyMod.m_Panel_ChallengeComplete = __instance;
		}
	}

	[HarmonyPatch(typeof(Panel_Log), "OnQuitToMainMenu")]
	private static class Panel_Log_OnQuitToMainMenu
	{
		private static bool Prefix(Panel_Log __instance)
		{
			MyMod.BeginRespawn();
			return false;
		}
	}

	[HarmonyPatch(typeof(GameManager), "HandlePlayerDeath")]
	private static class GameManager_HandlePlayerDeath
	{
		private static bool Prefix(GameManager __instance)
		{
			if (GameCompat.Panel<Panel_Log>().IsEnabled())
			{
				GameCompat.Panel<Panel_Log>().ExitInterfaceOnDeath();
			}
			if (GameManager.m_Rest.IsSleeping())
			{
				GameManager.m_Rest.EndSleeping(interrupted: true);
			}
			GameManager.m_PlayerManager.UnequipImmediate(saveLastItem: false);
			GameManager.m_PlayerManager.SetControlMode(PlayerControlMode.Dead);
			AudioCallback[] array = GameManager.GetPlayerObject().transform.parent.GetComponentsInChildren<AudioCallback>();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].ToggleDisableAudioEvents(toggle: true);
			}
			switch (ExperienceModeManager.GetCurrentExperienceModeType())
			{
			case ExperienceModeType.ChallengeRescue:
			case ExperienceModeType.ChallengeHunted:
			case ExperienceModeType.ChallengeWhiteout:
			case ExperienceModeType.ChallengeNomad:
			case ExperienceModeType.ChallengeHuntedPart2:
			case ExperienceModeType.ChallengeArchivist:
			case ExperienceModeType.ChallengeDeadManWalking:
			case ExperienceModeType.ChallengeNowhereToHide:
				break;
			default:
				if (ExperienceCompat.IsStoryMode())
				{
					InterfaceManager.LoadPanel<Panel_ChallengeComplete>().ShowPanel(Panel_ChallengeComplete.Options.None);
				}
				else
				{
					GameCompat.Panel<Panel_Log>().EnableDeathView();
				}
				break;
			}
			BaseAiManager.ResetAudioLoops();
			UIInput.selection = null;
			GameManager.m_Log.WriteLogToFile();
			GameManager.CancelPendingSave();
			GameCompat.Panel<Panel_OptionsMenu>().ApplyHudType();
			if (ExperienceCompat.IsStoryMode())
			{
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(SaveGameSystem), "SaveCompletedInternal")]
	private static class SaveGameSlots_SaveCompletedInternal
	{
		private static void Postfix(bool success)
		{
			if (!success)
			{
				MelonLogger.Error("[SaveGameSystem] Save failed; the game will remain open.");
				ResetPendingQuitAfterSaveFailure();
			}
			else
			{
				string currentSaveName = SaveGameSystem.GetCurrentSaveName();
				MelonLogger.Msg(System.ConsoleColor.Green, "SAVE_NATIVE_COMPLETED slot=" + currentSaveName);
				OnSaveCompleted(currentSaveName);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Log), "EnableDeathView")]
	private static class Panel_Log_EnableDeathView
	{
		private static void Postfix(Panel_Log __instance)
		{
			MyMod.IsDead = true;
			MyMod.DropAll();
		}
	}

	[HarmonyPatch(typeof(Panel_Log), "GetOnBackText")]
	private static class Panel_Log_GetOnBackText
	{
		private static void Postfix(Panel_Log __instance, ref string __result)
		{
			if (__instance.QuitToMainMenuOnBack())
			{
				__result = "Respawn";
			}
		}
	}

	[HarmonyPatch(typeof(Cairn), "PerformInteraction")]
	private static class Cairn_ProcessInteraction
	{
		private static void Postfix(Cairn __instance)
		{
			MyMod.AddFoundCairn(__instance.m_JournalEntryNumber);
		}
	}

	[HarmonyPatch(typeof(MillingMachine), "PerformInteraction")]
	private static class MillingMachine_ProcessInteraction
	{
		private static bool Prefix(MillingMachine __instance)
		{
			if (__instance.isActiveAndEnabled && Supporters.ConfiguratedBenefits.m_Knife && (bool)GameManager.GetPlayerManagerComponent().m_ItemInHands)
			{
				GearItem itemInHands = GameManager.GetPlayerManagerComponent().m_ItemInHands;
				if (itemInHands.GetGearName() == "GEAR_KnifeImprovised")
				{
					DataStr.PriorityActionForOtherPlayer customAction = MyMod.GetCustomAction("Excision");
					MyMod.DoLongAction(itemInHands.gameObject, customAction.m_ProcessText, customAction.m_Action, customAction.m_ActionDuration, customAction.m_Hold);
					return false;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Keypad), "PerformInteraction")]
	private static class Keypad_ProcessInteraction
	{
		private static void Prefix(Keypad __instance)
		{
			MelonLogger.Msg(System.ConsoleColor.Blue, "[Papers codes] Interact with keypad that has code: " + __instance.m_Code);
			MyMod.RestoreCodeFromGears();
		}
	}

	[HarmonyPatch(typeof(Lock), "PrepareForceLock")]
	private static class LoadScene_ForceLockBegin
	{
		private static bool Prefix(Lock __instance)
		{
			if ((bool)__instance.gameObject.GetComponent<Comps.DoorLockedOnKey>())
			{
				MyMod.SelectKeys(__instance.gameObject, MyMod.KeysAction.OPEN);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(uConsoleLog), "Add")]
	internal static class uConsoleLog_Add
	{
		private static void Postfix(string text)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.DedicatedServerAppMode)
			{
				MelonLogger.Msg(System.ConsoleColor.DarkGray, "[InGameConsole] " + text);
			}
		}
	}

	[HarmonyPatch(typeof(GameManager), "ForceSaveGame")]
	internal static class GameManager_ForceSaveGame
	{
		private static void Prefix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.DedicatedServerAppMode)
			{
				SaveGameSystem.AbortScreenshot();
			}
		}

		private static void Postfix()
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (MyMod.DedicatedServerAppMode)
			{
				SaveGameSystem.AbortScreenshot();
			}
		}
	}

	[HarmonyPatch(typeof(CharcoalItem), "StartDetailSurvey")]
	internal static class CharcoalItem_StartDetailSurvey
	{
		private static bool Prefix(CharcoalItem __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			MyMod.ShowCharCoalPicker(__instance.gameObject, __instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(Panel_Log), "ExitInterface")]
	internal static class Panel_Log_ExitInterface
	{
		private static void Postfix(Panel_Log __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			string text = __instance.m_NotesTextField.GetText();
			if (!string.IsNullOrEmpty(text))
			{
				if (CharcoalItem.m_CharcoalItemInUseForSurvey != null)
				{
					CharcoalItem.m_CharcoalItemInUseForSurvey.StopSurveyAudio();
					GameManager.GetPlayerAnimationComponent().Trigger_Generic_Unstow();
					CharcoalItem.m_CharcoalItemInUseForSurvey.ConsumeCharcoal();
					CharcoalItem.m_CharcoalItemInUseForSurvey = null;
					MyMod.CreateCustomNote(text);
					__instance.m_NotesTextField.SetText("");
					__instance.Enable(enable: false);
				}
			}
			else if (CharcoalItem.m_CharcoalItemInUseForSurvey != null)
			{
				CharcoalItem.m_CharcoalItemInUseForSurvey.StopSurveyAudio();
				GameManager.GetPlayerManagerComponent().UnequipItemInHands();
				CharcoalItem.m_CharcoalItemInUseForSurvey = null;
				__instance.m_NotesTextField.SetText("");
				__instance.Enable(enable: false);
			}
		}
	}

	[HarmonyPatch(typeof(CabinFever), "DisabledForGameMode")]
	internal static class CabinFever_DisabledForXPMode
	{
		private static void Postfix(CabinFever __instance, ref bool __result)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			__result = true;
		}
	}

	[HarmonyPatch(typeof(IntestinalParasites), "IntestinalParasitesStart")]
	internal static class IntestinalParasites_IntestinalParasitesStart
	{
		private static bool Prefix(IntestinalParasites __instance, bool nofx)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			if (GameManager.GetPlayerManagerComponent().PlayerIsDead() || InterfaceManager.IsPanelEnabled<Panel_ChallengeComplete>() || __instance.GetNumDosesRequired() == 0)
			{
				return false;
			}
			float hp = -5 * __instance.m_NumPiecesEatenThisRiskCycle;
			float fatigueValue = 8 * __instance.m_NumPiecesEatenThisRiskCycle;
			float thirstValue = 9 * __instance.m_NumPiecesEatenThisRiskCycle;
			GameManager.GetConditionComponent().AddHealth(hp, DamageSource.IntestinalParasites);
			FoodPoisoning foodPoisoningComponent = GameManager.GetFoodPoisoningComponent();
			foodPoisoningComponent.FoodPoisoningStart("GAMEPLAY_TaintedFood", displayIcon: true);
			GameManager.GetFatigueComponent().AddFatigue(fatigueValue);
			GameManager.GetThirstComponent().AddThirst(thirstValue);
			GameManager.GetHungerComponent().AddReserveCalories(-300f);
			foodPoisoningComponent.m_DurationHours = foodPoisoningComponent.m_DurationHoursMin * (float)__instance.m_NumPiecesEatenThisRiskCycle;
			MelonLogger.Msg(System.ConsoleColor.Blue, "Intestinal Parasites risk evolved to food poison, m_NumPiecesEatenThisRiskCycle " + __instance.m_NumPiecesEatenThisRiskCycle + " Damage " + hp);
			__instance.m_NumPiecesEatenThisRiskCycle = 0;
			__instance.m_HasParasiteRisk = false;
			__instance.m_HasParasites = false;
			StatsManager.IncrementValue(StatID.IntestinalParasites);
			if (nofx)
			{
				return false;
			}
			GameManager.GetPlayerVoiceComponent().Play(__instance.m_ParasitesVO, Il2CppVoice.Priority.Critical);
			return false;
		}
	}

	[HarmonyPatch(typeof(GameManager), "AllowedToSave")]
	internal static class GameManager_AllowedToSave
	{
		private static void Postfix(GameManager __instance, SaveState state, ref bool __result)
		{
			if (state != SaveState.InMemory)
			{
				return;
			}
			MelonLogger.Msg("[GameManager][AllowedToSave] state " + state);
			MelonLogger.Msg("s_SaveGameDelayTime " + GameManager.s_SaveGameDelayTime);
			MelonLogger.Msg("(double)GameManager.s_SaveGameDelayTime <= (double)Time.time = " + ((double)GameManager.s_SaveGameDelayTime <= (double)Time.time));
			MelonLogger.Msg("AllowedToLoadActiveGame() " + GameManager.AllowedToLoadActiveGame());
			MelonLogger.Msg("m_AllowSaveWithoutGrounding " + GameManager.m_AllowSaveWithoutGrounding);
			MelonLogger.Msg("GameManager.m_PendingSave " + GameManager.m_PendingSave);
			if (!__result)
			{
				if (AutoSaveAllTheCost)
				{
					MelonLogger.Msg(System.ConsoleColor.Red, "Can't autosave now, but we really need to, saving... ");
					AutoSaveAllTheCost = false;
					__result = true;
				}
			}
			else
			{
				AutoSaveAllTheCost = false;
			}
		}
	}

	[HarmonyPatch(typeof(WeatherSet), "Update")]
	internal static class WeatherSet_Update
	{
		private static void Prefix(WeatherSet __instance)
		{
			WeatherTransition.m_WeatherTransitionTimeScalar = 0f;
		}

		private static void Postfix(WeatherSet __instance)
		{
			WeatherTransition.m_WeatherTransitionTimeScalar = 1f;
		}
	}

	[HarmonyPatch(typeof(Panel_ActionPicker), "OnSelect")]
	internal static class Panel_ActionPicker_OnSelect
	{
		private static void Postfix(Panel_ActionPicker __instance)
		{
			if (ShowInvitesAfterPicker)
			{
				ShowInvitesAfterPicker = false;
				if (!MyMod.iAmHost)
				{
					using (Packet packet = new Packet(165))
					{
						MyMod.DoPleaseWait("Please wait...", "Calling to coordinator...");
						MyMod.SetRepeatPacket(MyMod.ResendPacketType.ExpeditionInvites);
						packet.Write(_value: true);
						MyMod.SendUDPData(packet);
						return;
					}
				}
				MyMod.ShowInvitesPicker(null, ExpeditionManager.GetInviteForClient(Shared.GetMacAddress()));
			}
			else if (ShowCluesAfterPicker)
			{
				ShowCluesAfterPicker = false;
				MyMod.ShowCluesPicker();
			}
		}
	}

	[HarmonyPatch(typeof(EquipItemPopup), "UpdateAmmoStatus")]
	internal static class EquipItemPopup_UpdateAmmoStatus
	{
		private static void Postfix(EquipItemPopup __instance)
		{
			GearItem itemInHands = GameManager.GetPlayerManagerComponent().m_ItemInHands;
			if ((bool)itemInHands && (MyMod.IsCustomHandItem(itemInHands.GetGearName()) || itemInHands.GetGearName() == "GEAR_HandheldShortwave"))
			{
				__instance.HideAmmoPopup();
			}
		}
	}

	[HarmonyPatch(typeof(EquipItemPopup), "ShowEquippedItem")]
	internal static class EquipItemPopup_ShowEquippedItem
	{
		private static void Postfix(EquipItemPopup __instance, GearItem gi)
		{
			if ((bool)gi)
			{
				if (MyMod.IsCustomHandItem(gi.GetGearName()))
				{
					__instance.HideAmmoPopup();
				}
				string text = "";
				string text2 = "";
				bool flag = Utils.IsGamepadActive();
				bool flag2 = false;
				if (gi.GetGearName() == "GEAR_HandheldShortwave")
				{
					__instance.m_ButtonPromptScrollWheel.ShowPromptForKey("Change frequency", "Scroll");
					__instance.m_ButtonPromptReload.ShowPromptForKey(Localization.Get("GAMEPLAY_Help"), "Reload");
					text2 = "Expeditions";
					flag2 = true;
				}
				else if (gi.GetGearName() == "GEAR_SCNote")
				{
					text = "Read";
					flag2 = true;
				}
				if (flag2)
				{
					__instance.ShowItemIcons(text, text2, showAmmo: false);
					__instance.m_ButtonPromptFire.ShowPromptForKey(flag ? text : text2, flag ? "Fire" : "AltFire");
					__instance.MaybeRepositionFireButtonPrompt(flag ? text2 : text);
					__instance.m_ButtonPromptAltFire.ShowPromptForKey(flag ? text2 : text, flag ? "AltFire" : "Interact");
					__instance.MaybeRepositionAltFireButtonPrompt(flag ? text : text2);
				}
				__instance.m_GearItem = gi;
			}
		}
	}

	[HarmonyPatch(typeof(RockCache), "SetupInWorld")]
	internal static class RockCache_SetupInWorld
	{
		private static bool Prefix(RockCache __instance)
		{
			GameObject gameObject = __instance.gameObject;
			Comps.FakeRockCache fakeRockCache = gameObject.GetComponent<Comps.FakeRockCache>();
			if (fakeRockCache == null)
			{
				fakeRockCache = gameObject.AddComponent<Comps.FakeRockCache>();
			}
			string text = ObjectGuidManager.GenerateNewGuidString();
			ObjectGuid objectGuid = gameObject.GetComponent<ObjectGuid>();
			if (objectGuid == null)
			{
				objectGuid = gameObject.AddComponent<ObjectGuid>();
			}
			objectGuid.Set(text);
			fakeRockCache.m_GUID = text;
			fakeRockCache.m_Owner = MyMod.MyChatName;
			fakeRockCache.m_Rocks = __instance.m_NumRocksFromDismantle;
			fakeRockCache.Created();
			UnityEngine.Object.Destroy(__instance);
			return false;
		}
	}

	[HarmonyPatch(typeof(Panel_GenericProgressBar), "ProgressBarEnded")]
	internal static class Panel_GenericProgressBar_ProgressBarEnded
	{
		private static void Postfix(Panel_GenericProgressBar __instance, bool success, bool playerCancel)
		{
			if (FakeRockCacheCallback != null)
			{
				GameManager.s_IsAISuspended = false;
				if (success)
				{
					FakeRockCacheCallback.DismantleFinished();
				}
				GameManager.GetPlayerManagerComponent().m_ObjectToPlaceRockCache = null;
				FakeRockCacheCallback = null;
				if (MyMod.sendMyPosition)
				{
					using (Packet packet = new Packet(52))
					{
						packet.Write(_value: true);
						SendTCPData(packet);
						return;
					}
				}
				ServerSend.FURNBREAKINSTOP(0, _msg: true, toEveryOne: true);
			}
			else if (HeatPackOpenCallback != null)
			{
				if (success)
				{
					OpenHeatPackFinished(HeatPackOpenCallback);
				}
				HeatPackOpenCallback = null;
			}
			else if (PresentOpenCallback != null)
			{
				if (success)
				{
					OpenPresentFinished(PresentOpenCallback);
				}
				PresentOpenCallback = null;
			}
			else
			{
				if (!SmeltingLead)
				{
					return;
				}
				if (success)
				{
					GearItem bestGearItemWithName = GameManager.GetInventoryComponent().GetBestGearItemWithName("GEAR_RecycledCan");
					if ((bool)bestGearItemWithName)
					{
						GameManager.GetInventoryComponent().DestroyGear(bestGearItemWithName.gameObject);
					}
					GearItem bestGearItemWithName2 = GameManager.GetInventoryComponent().GetBestGearItemWithName("GEAR_ScrapLead");
					if ((bool)bestGearItemWithName2)
					{
						if ((bool)bestGearItemWithName2.m_StackableItem && bestGearItemWithName2.m_StackableItem.m_Units > 1)
						{
							bestGearItemWithName2.m_StackableItem.m_Units--;
						}
						else
						{
							GameManager.GetInventoryComponent().DestroyGear(bestGearItemWithName2.gameObject);
						}
					}
					GameManager.GetPlayerVoiceComponent().Play("PLAY_FIRESUCCESS", Il2CppVoice.Priority.Critical);
					GearItem gearItem = GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_SCLiquidLead", 1);
					string text = Localization.Get("GAMEPLAY_ItemAddedToInventory").Replace("{item-name}", gearItem.DisplayName);
					GameManager.GetLogComponent().AddItem(text);
					GearMessage.AddMessage(gearItem, Localization.Get("GAMEPLAY_Added"), gearItem.DisplayName, highPriority: false, showOnHUDOnly: false);
				}
				SmeltingLead = false;
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Map), "DoDetailSurvey")]
	internal static class Panel_Map_DoDetailSurvey
	{
		private static void Postfix(Panel_Map __instance, SurveyType surveyType)
		{
			if (surveyType != SurveyType.Charcoal)
			{
				return;
			}
			Vector3 position = GameManager.GetPlayerTransform().position;
			if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(173))
				{
					packet.Write(MyMod.level_guid);
					packet.Write(position);
					SendTCPData(packet);
					return;
				}
			}
			ExpeditionManager.RegisterCharcoalDrawing(MyMod.level_guid, position);
		}
	}

	[HarmonyPatch(typeof(InputManager), "GetOpenActionsPanelHeldDown")]
	internal static class InputManager_GetOpenActionsPanelHeldDown
	{
		private static void Postfix(InputManager __instance, bool __result)
		{
			if (MyMod.ForcedShowExpeditionHUDSeconds > 0)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(GearItem), "ManualUpdate")]
	internal static class GearItem_ManualUpdate
	{
		private static void Postfix(GearItem __instance)
		{
			if (MyMod.CrazyPatchesLogger)
			{
				StackTrace stackTrace = new StackTrace(new StackFrame(needFileInfo: true));
				MelonLogger.Msg(System.ConsoleColor.Blue, "----------------------------------------------------");
				MelonLogger.Msg(System.ConsoleColor.Gray, " Stack trace for current level: {0}", stackTrace.ToString());
			}
			bool flag = __instance.GetGearName() == "GEAR_SCHeatPack" || __instance.GetGearName() == "GEAR_SCHeatPackC";
			bool flag2 = __instance.GetGearName() == "GEAR_SCLiquidLead";
			if (flag || flag2)
			{
				__instance.m_CurrentHP--;
				if (flag)
				{
					__instance.gameObject.SetActive(value: true);
					__instance.gameObject.transform.GetChild(0).gameObject.SetActive(value: false);
				}
				if (__instance.m_CurrentHP <= 0f)
				{
					string text = Localization.Get("GAMEPLAY_Ruined").Replace("{item-name}", __instance.DisplayName);
					GameManager.GetLogComponent().AddItem(text);
					GearMessage.AddMessage(__instance, Localization.Get("GAMEPLAY_RuinedPopup"), __instance.DisplayName, highPriority: false, showOnHUDOnly: false);
					GameManager.GetInventoryComponent().DestroyGear(__instance.gameObject);
				}
			}
		}
	}

	public enum PresentRarity
	{
		Shit,
		Low,
		Medium,
		High,
		Epic,
		Legend
	}

	[HarmonyPatch(typeof(Panel_ActionPicker), "ShowActionPicker", new System.Type[]
	{
		typeof(GameObject),
		typeof(Il2CppSystem.Collections.Generic.IEnumerable<ActionPickerItemData>)
	})]
	internal static class Panel_ActionPicker_ShowActionPicker
	{
		private static void Postfix(Panel_ActionPicker __instance, GameObject interactionObject)
		{
			MelonLogger.Msg("Panel_ActionPicker Postfix ShowActionPicker");
			if (interactionObject != null && ((bool)interactionObject.GetComponent<CookingSlot>() || (bool)interactionObject.GetComponent<Campfire>() || (bool)interactionObject.GetComponent<WoodStove>()))
			{
				ShowHookedCookingSlotPicker(interactionObject);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_ActionsRadial), "CanPlaceFromRadial")]
	private static class Panel_ActionsRadial_CanPlaceFromRadial
	{
		private static void Postfix(Panel_ActionsRadial __instance, GearItem gi, ref bool __result)
		{
			if (__result && (gi.GetGearName() == "GEAR_SCEmergencyRation" || gi.GetGearName() == "GEAR_SCLiquidLead"))
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Container), "CanMoveItemToContainerInMoveAll")]
	private static class Panel_Container_CanMoveItemToContainerInMoveAll
	{
		private static void Postfix(Panel_Container __instance, InventoryGridDataItem dataItem, ref bool __result)
		{
			GearItem gearItem = dataItem?.m_GearItem;
			if (!(gearItem == null))
			{
				PostfixInternal(__instance, gearItem, ref __result);
			}
		}

		private static void PostfixInternal(Panel_Container __instance, GearItem gearItem, ref bool __result)
		{
			if (__result && (gearItem.GetGearName() == "GEAR_SCEmergencyRation" || gearItem.GetGearName() == "GEAR_HeatPack" || gearItem.GetGearName() == "GEAR_HeatPackC" || gearItem.GetGearName() == "GEAR_SCLiquidLead"))
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Container), "ItemPassesFilter", new System.Type[]
	{
		typeof(GearItem),
		typeof(string)
	})]
	private static class Panel_Container_ItemPassesFilter
	{
		private static void Postfix(Panel_Container __instance, GearItem pi, ref bool __result)
		{
			if (__result && (pi.GetGearName() == "GEAR_SCEmergencyRation" || pi.GetGearName() == "GEAR_SCHeatPack" || pi.GetGearName() == "GEAR_SCHeatPackC" || pi.GetGearName() == "GEAR_SCLiquidLead"))
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(WildlifeItem), "StartKillAnimation")]
	private static class WildlifeItem_StartKillAnimation
	{
		private static void Postfix(WildlifeItem __instance)
		{
			MelonLogger.Msg("[WildlifeItem] StartKillAnimation");
			SanityManager.AddSanity(-30f);
		}
	}

	[HarmonyPatch(typeof(GenericStatusBarSpawner), "Awake")]
	private static class GenericStatusBarSpawner_Awake
	{
		private static void Postfix(GenericStatusBarSpawner __instance)
		{
			float num = 0f;
			float num2 = 0f;
			float num3 = 1f;
			if (__instance.gameObject.transform.parent.name.Contains("Large"))
			{
				num = 160f;
				num3 = 1.8f;
				num2 = 29f;
			}
			else if (__instance.gameObject.transform.parent.name.Contains("Regular"))
			{
				num = 80f;
				num3 = 1f;
			}
			else
			{
				if (!__instance.gameObject.transform.parent.name.Contains("Small"))
				{
					return;
				}
				num = 40f;
				num3 = 0.5f;
			}
			if (__instance.m_MainSpriteName == "ico_status_hunger1")
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(__instance.m_Prefab, __instance.gameObject.transform.parent);
				gameObject.transform.localPosition = new Vector3(__instance.gameObject.transform.localPosition.x + num, gameObject.transform.localPosition.y + num2, gameObject.transform.localPosition.z);
				gameObject.transform.localScale = new Vector3(num3, num3, num3);
				StatusBar component = gameObject.GetComponent<StatusBar>();
				component.m_StatusBarType = (StatusBar.StatusBarType)5;
				component.m_OuterBoxSprite.spriteName = "ico_xpModeNowhereToHide2020";
				component.m_SpriteWhenEmpty.spriteName = "ico_xpModeNowhereToHide2020";
				component.m_ThresholdCritical = 0.3f;
				component.m_HighThreshold = 0.42f;
				component.m_MediumThreshold = 0.3f;
				Comps.StatusBarHelper statusBarHelper = gameObject.AddComponent<Comps.StatusBarHelper>();
				statusBarHelper.m_Root = component.m_HierarchyRoot;
				statusBarHelper.m_RootTrack = __instance.m_SpawnedStatusBar.m_HierarchyRoot;
			}
		}
	}

	[HarmonyPatch(typeof(StatusBar), "GetRateOfChange")]
	private static class StatusBar_GetRateOfChange
	{
		private static void Postfix(StatusBar __instance, ref float __result)
		{
			if (__instance.m_StatusBarType == (StatusBar.StatusBarType)5)
			{
				__result = 0f - SanityManager.GetRate();
			}
		}
	}

	[HarmonyPatch(typeof(StatusBar), "GetFillValues")]
	private static class StatusBar_GetFillValue
	{
		private static void Postfix(StatusBar __instance, ref StatusBar.FillValues __result)
		{
			if (__instance.m_StatusBarType == (StatusBar.StatusBarType)5)
			{
				__result.m_Fill = (__result.m_NormalizedValue = (float)(double)SanityManager.m_CurrentSanity / SanityManager.m_MaxSanity);
			}
		}
	}

	[HarmonyPatch(typeof(ResearchItem), "OnResearchComplete")]
	private static class ResearchItem_OnResearchComplete
	{
		private static bool Prefix(ResearchItem __instance)
		{
			GearItem component = __instance.GetComponent<GearItem>();
			if ((bool)component)
			{
				if (component.GetGearName() == "GEAR_SCSanityBook")
				{
					SanityManager.m_CanSeeSanity = true;
					GameCompat.Panel<Panel_HUD>().ShowBuffNotification("Hidden Knowlanages", "Sanity status revealed", "ico_xpModeNowhereToHide2020");
					return false;
				}
				if (component.GetGearName() == "GEAR_SCFirstAidBook")
				{
					MyMod.IncressCustomSkill(MyMod.CustomSkills.FirstAid, __instance.m_SkillPoints);
					return false;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_Inventory_Examine), "RefreshReadPanel")]
	private static class Panel_Inventory_Examine_RefreshReadPanel
	{
		private static void Postfix(Panel_Inventory_Examine __instance)
		{
			if (!__instance.m_GearItem || !__instance.m_GearItem.m_ResearchItem)
			{
				return;
			}
			if (__instance.m_GearItem.GetGearName() == "GEAR_SCSanityBook")
			{
				for (int i = 0; i < __instance.m_ItemIcons.Length; i++)
				{
					__instance.m_ItemIcons[i].gameObject.SetActive(value: false);
				}
				__instance.m_ResearchIcon.spriteName = "ico_xpModeNowhereToHide2020";
				__instance.m_SkillImprovedLabel.text = "Gives ability to know your sanity status.";
				__instance.m_SkillProgressBarLabel.text = "";
				__instance.m_SkillProgressBar.value = 0f;
				__instance.m_SkillProgressBarSprite.fillAmount = 0f;
				__instance.m_SkillProgressBar.gameObject.SetActive(value: false);
				__instance.m_SkillLevelLabel.transform.parent.gameObject.SetActive(value: false);
			}
			else
			{
				__instance.m_SkillProgressBar.gameObject.SetActive(value: true);
				__instance.m_SkillLevelLabel.transform.parent.gameObject.SetActive(value: true);
				Skill skillByType = GetSkillByType(GameManager.m_SkillsManager, __instance.m_GearItem.m_ResearchItem.m_SkillType);
				if ((bool)skillByType)
				{
					__instance.m_SkillProgressBarLabel.text = skillByType.m_DisplayName;
					__instance.m_SkillLevelLabel.text = (skillByType.GetCurrentTierNumber() + 1).ToString();
					__instance.m_SkillProgressBar.value = skillByType.GetProgressToNextLevelAsNormalizedValue(0);
					__instance.m_SkillProgressBarSprite.fillAmount = skillByType.GetProgressToNextLevelAsNormalizedValue(__instance.m_GearItem.m_ResearchItem.m_SkillPoints);
				}
			}
		}
	}

	[HarmonyPatch(typeof(ResearchItem), "GetSkillNameLocalized")]
	private static class ResearchItem_GetSkillNameLocalized
	{
		private static void Postfix(ResearchItem __instance, ref string __result)
		{
			if (__instance.m_SkillType > SkillType.Gunsmithing)
			{
				Skill customSkill = MyMod.GetCustomSkill((MyMod.CustomSkills)__instance.m_SkillType);
				if (customSkill != null)
				{
					__result = customSkill.m_DisplayName;
				}
			}
		}
	}

	[HarmonyPatch(typeof(SkillsManager), "GetSkill")]
	private static class SkillsManager_GetSkill
	{
		private static void Postfix(SkillsManager __instance, SkillType skillType, ref Skill __result)
		{
			if (skillType > SkillType.Gunsmithing)
			{
				Skill customSkill = MyMod.GetCustomSkill((MyMod.CustomSkills)skillType);
				if (customSkill != null)
				{
					__result = customSkill;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Inventory_Examine), "OnRead")]
	private static class Panel_Inventory_Examine_OnRead
	{
		private static bool Prefix(Panel_Inventory_Examine __instance)
		{
			if ((bool)__instance.m_GearItem && __instance.m_GearItem.GetGearName() == "GEAR_SCSanityBook")
			{
				if (SanityManager.m_CanSeeSanity || __instance.m_GearItem.m_ResearchItem.IsResearchComplete())
				{
					GameAudioManager.PlayGUIError();
					HUDMessage.AddMessage(Localization.Get("GAMEPLAY_ResearchAlreadyCompleted"), highPriority: true, ignoreOverlayActive: true);
				}
				else
				{
					__instance.StartRead(__instance.m_HoursToRead * 60, __instance.m_GearItem.m_ResearchItem.m_ReadAudio);
				}
				return false;
			}
			return true;
		}
	}

	public static bool AutoSaveAllTheCost = false;

	public static int EmuGenVersion = -1;

	public static bool EmulateOldRandom = false;

	public static GameObject FakeBedRoll = null;

	public static string OverrideConditionOverTimeCause = "";

	public static string OverrideHeadacheCause = "";

	public static bool QuitOnSave = false;

	public static bool HaveReadFakeNote = false;

	public static bool ShowInvitesAfterPicker = false;

	public static bool ShowCluesAfterPicker = false;

	public static Comps.FakeRockCache FakeRockCacheCallback = null;

	public static GearItem HeatPackOpenCallback = null;

	public static GearItem PresentOpenCallback = null;

	public static bool SmeltingLead = false;

	public static System.Collections.Generic.List<string> ShitTier = new System.Collections.Generic.List<string>
	{
		"GEAR_Stick", "GEAR_Tinder", "GEAR_Stone", "GEAR_Cloth", "GEAR_NewsprintRoll", "GEAR_PaperStack", "GEAR_ReclaimedWoodB", "GEAR_LeatherShoes", "GEAR_BasicGloves", "GEAR_TeeShirt",
		"GEAR_BaseballCap", "GEAR_RecycledCan", "GEAR_SkiBoots"
	};

	public static System.Collections.Generic.List<string> LowTier = new System.Collections.Generic.List<string>
	{
		"GEAR_CarBattery", "GEAR_DustingSulfur", "GEAR_ScrapLead", "GEAR_ScrapMetal", "GEAR_StumpRemover", "GEAR_Rope", "GEAR_Snare", "GEAR_Water500ml", "GEAR_DogFood", "GEAR_EnergyBar",
		"GEAR_KetchupChips", "GEAR_CottonHoodie", "GEAR_Leather", "GEAR_CottonSocks", "GEAR_MackinawJacket", "GEAR_BasicWinterCoat", "GEAR_CanOpener", "GEAR_SimpleTools", "GEAR_HeavyBandage", "GEAR_BottlePainKillers",
		"GEAR_BottleHydrogenPeroxide", "GEAR_BottleAntibiotics", "GEAR_RoseHipTea", "GEAR_ReishiTea", "GEAR_WaterPurificationTablets", "GEAR_BasicWoolHat", "GEAR_FleeceMittens", "GEAR_CoffeeCup", "GEAR_Jeans", "GEAR_BasicWoolScarf",
		"GEAR_LongUnderwear"
	};

	public static System.Collections.Generic.List<string> MediumTier = new System.Collections.Generic.List<string>
	{
		"GEAR_GreenTeaPackage", "GEAR_PeanutButter", "GEAR_Peaches", "GEAR_Soda", "GEAR_SodaEnergy", "GEAR_SodaGrape", "GEAR_SodaOrange", "GEAR_TomatoSoupCan", "GEAR_SewingKit", "GEAR_LampFuel",
		"GEAR_JerryCanRusty", "GEAR_CoffeeTin", "GEAR_BirchbarkPrepared", "GEAR_Firestriker", "GEAR_Mittens", "GEAR_WorkGloves", "GEAR_DownSkiJacket", "GEAR_WoolSocks", "GEAR_HighQualityTools", "GEAR_CookingPot",
		"GEAR_Knife", "GEAR_FlareA", "GEAR_BlueFlare", "GEAR_BirchbarkTea", "GEAR_Toque", "GEAR_CargoPants", "GEAR_InsulatedPants", "GEAR_WoolWrap", "GEAR_FishermanSweater"
	};

	public static System.Collections.Generic.List<string> HighTier = new System.Collections.Generic.List<string>
	{
		"GEAR_QualityWinterCoat", "GEAR_PremiumWinterCoat", "GEAR_MilitaryParka", "GEAR_HeavyParka", "GEAR_Arrow", "GEAR_RifleAmmoSingle", "GEAR_RevolverAmmoSingle", "GEAR_GunpowderCan", "GEAR_KeroseneLampB", "GEAR_Hacksaw",
		"GEAR_Hatchet", "GEAR_HomeMadeSoup", "GEAR_ClimbingSocks", "GEAR_Gauntlets", "GEAR_MRE", "GEAR_CombatPants", "GEAR_Balaclava"
	};

	public static System.Collections.Generic.List<string> EpicTier = new System.Collections.Generic.List<string> { "GEAR_RevolverAmmoBox", "GEAR_RifleAmmoBox", "GEAR_FlareGunAmmoSingle", "GEAR_EmergencyStim", "GEAR_RabbitSkinMittens", "GEAR_RabbitskinHat", "GEAR_BearHide", "GEAR_MooseHide", "GEAR_SCHeatPackB" };

	public static System.Collections.Generic.List<string> LegendTier = new System.Collections.Generic.List<string> { "GEAR_Bow", "GEAR_Revolver", "GEAR_Rifle", "GEAR_BearSkinCoat", "GEAR_MooseHideCloak", "GEAR_WolfSkinCape", "GEAR_EarMuffs", "GEAR_MooseHideBag", "GEAR_LongUnderwearWool" };

	public static void SendTCPData(Packet _packet)
	{
		MyMod.SendUDPData(_packet);
	}

	public static bool DuppableGearItem(string GearName)
	{
		if (GearName.Contains("TechnicalBackpack") || GearName.Contains("Crampons") || GearName.Contains("CanneryCodeNote") || GearName.Contains("BallisticVest") || GearName.Contains("BlackrockCodeNote") || GearName.Contains("MountainTownFarmKey"))
		{
			MelonLogger.Msg(System.ConsoleColor.Blue, "Item " + GearName + " is can be picked by other player");
			return true;
		}
		return false;
	}

	public static bool GarbadgeFilter(string GearName)
	{
		switch (GearName)
		{
		case "GEAR_CrowFeather":
		case "GEAR_Stone":
		case "GEAR_Stick":
			// Only exempt from pickup sync when the server allows duplicated spawns.
			return MyMod.ServerConfig.m_DuppedSpawns;
		default:
			return false;
		}
	}

	public static bool NeedSkipCauseConnect()
	{
		string[] commandLineArgs = System.Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if ((commandLineArgs[i] == "+connect_lobby" || commandLineArgs[i] == "connect_lobby") && i + 1 <= commandLineArgs.Length - 1)
			{
				MelonLogger.Msg("[STEAMWORKS.NET] Skip everything to connect to: " + commandLineArgs[i + 1]);
				return true;
			}
			if ((commandLineArgs[i] == "skip" || commandLineArgs[i] == "-skip") && i + 1 <= commandLineArgs.Length - 1)
			{
				return true;
			}
		}
		return false;
	}

	public static void SaveDeathCreates(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(MyMod.DeathCreates.ToArray());
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_DeathCreates", data);
	}

	public static void SaveUGUID(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(new string[1] { MyMod.MyUGUID });
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "UserGUID", data);
	}

	public static void SaveDeployedRopes(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(MyMod.DeployedRopes.ToArray());
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_ropes", data);
	}

	public static void SaveServerConfig(SaveSlotType gameMode, string name)
	{
		if (!MyMod.sendMyPosition)
		{
			string data = JSON.Dump(MyMod.ServerConfig);
			SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_cfg", data);
		}
	}

	public static void SaveSnowShelters(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(MyMod.ShowSheltersBuilded.ToArray());
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_shelters", data);
	}

	public static void SaveGenVersion(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(new int[1] { 5 });
		if (!MyMod.CantBeUsedForMP && SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_genversion", data))
		{
			MyMod.LastLoadedGenVersion = 5;
		}
	}

	public static void SaveSeedRadioFQ(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(new float[1] { MyMod.RadioFrequency });
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_FQ", data);
	}

	public static void SaveRealtimeTime(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(new int[1] { MyMod.MinutesFromStartServer });
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_rtt", data);
	}

	public static void SaveFixedSpawn(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(new string[1] { MyMod.SavedSceneForSpawn });
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_fixedS", data);
	}

	public static void SaveSanity(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(new float[1] { SanityManager.m_CurrentSanity });
		string data2 = JSON.Dump(new bool[1] { SanityManager.m_CanSeeSanity });
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_sanity", data);
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_sanitybookV2", data2);
	}

	public static void SaveFixedSpawnPosition(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(new Vector3[1] { MyMod.SavedPositionForSpawn });
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_fixedP", data);
	}

	public static void SaveBookReaded(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(MyMod.BooksResearched);
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_books", data);
	}

	public static void SaveCustomSkills(SaveSlotType gameMode, string name)
	{
		string data = JSON.Dump(MyMod.SaveCustomSkills());
		SaveSlotCompat.SaveDataToSlot(gameMode, SaveGameSystem.m_CurrentEpisode, SaveGameSystem.m_CurrentGameId, name, "skycoop_customskills", data);
	}

	public static void LoadDeathCreates(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_DeathCreates");
		if (text != null)
		{
			MyMod.DeathCreates = JSON.Load(text).Make<DataStr.DeathContainerData[]>().ToList();
		}
	}

	public static void LoadUGUID(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "UserGUID");
		if (text != null)
		{
			MyMod.MyUGUID = JSON.Load(text).Make<string[]>()[0];
		}
	}

	public static void LoadDeployedRopes(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_ropes");
		if (text == null)
		{
			return;
		}
		System.Collections.Generic.List<DataStr.ClimbingRopeSync> list = JSON.Load(text).Make<DataStr.ClimbingRopeSync[]>().ToList();
		for (int i = 0; i < list.Count; i++)
		{
			DataStr.ClimbingRopeSync item = list[i];
			if (!MyMod.DeployedRopes.Contains(item))
			{
				MyMod.DeployedRopes.Add(item);
			}
		}
	}

	public static void LoadSnowShelters(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_shelters");
		if (text == null)
		{
			return;
		}
		System.Collections.Generic.List<DataStr.ShowShelterByOther> list = JSON.Load(text).Make<DataStr.ShowShelterByOther[]>().ToList();
		for (int i = 0; i < list.Count; i++)
		{
			DataStr.ShowShelterByOther item = list[i];
			if (!MyMod.ShowSheltersBuilded.Contains(item))
			{
				MyMod.ShowSheltersBuilded.Add(item);
			}
		}
	}

	public static void LoadServerConfig(string name)
	{
		if (!MyMod.sendMyPosition)
		{
			string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_cfg");
			if (text != null)
			{
				MyMod.ServerConfig = JSON.Load(text).Make<DataStr.ServerConfigData>();
			}
		}
	}

	public static void LoadGenVersion(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_genversion");
		if (text != null)
		{
			MyMod.LastLoadedGenVersion = JSON.Load(text).Make<int[]>()[0];
			if (MyMod.LastLoadedGenVersion != 5)
			{
				MyMod.CantBeUsedForMP = true;
				MelonLogger.Msg(System.ConsoleColor.DarkRed, "This save file can't be use for multiplayer, because we created on old version of the mod, with Generation version " + MyMod.LastLoadedGenVersion + ". Release of mod you using right now has Generation version " + 5);
			}
		}
		else
		{
			MyMod.LastLoadedGenVersion = 0;
			MyMod.CantBeUsedForMP = true;
			MelonLogger.Msg(System.ConsoleColor.DarkRed, "This save file can't be use for multiplayer, because was created on old version of mod or without mod at all.");
		}
	}

	public static float LoadRadioFQ(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_FQ");
		if (text != null)
		{
			return JSON.Load(text).Make<float[]>()[0];
		}
		return UnityEngine.Random.Range(-25, 25);
	}

	public static void LoadRealtimeTime(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_rtt");
		if (text != null)
		{
			MyMod.MinutesFromStartServer = JSON.Load(text).Make<int[]>()[0];
		}
	}

	public static void LoadFixedSpawn(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_fixedS");
		if (text != null)
		{
			MyMod.SavedSceneForSpawn = JSON.Load(text).Make<string[]>()[0];
			MyMod.FixedPlaceLoaded = true;
		}
	}

	public static void LoadFixedSpawnPosition(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_fixedP");
		if (text != null)
		{
			MyMod.SavedPositionForSpawn = JSON.Load(text).Make<Vector3[]>()[0];
			MyMod.FixedPlaceLoaded = true;
		}
	}

	public static void LoadReadedBooks(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_books");
		if (text == null)
		{
			return;
		}
		System.Collections.Generic.Dictionary<string, float> dictionary = JSON.Load(text).Make<System.Collections.Generic.Dictionary<string, float>>();
		int num = 0;
		int num2 = 0;
		foreach (System.Collections.Generic.KeyValuePair<string, float> item in dictionary)
		{
			if (!MyMod.BooksResearched.ContainsKey(item.Key))
			{
				MyMod.BooksResearched.Add(item.Key, item.Value);
				num++;
			}
			else
			{
				MyMod.BooksResearched.Remove(item.Key);
				MyMod.BooksResearched.Add(item.Key, item.Value);
				num2++;
			}
		}
	}

	public static void LoadCustomSkills(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_customskills");
		if (text != null)
		{
			MyMod.LoadCustomSkills(JSON.Load(text).Make<System.Collections.Generic.List<int>>());
		}
	}

	public static void LoadSanity(string name)
	{
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_sanity");
		if (text != null)
		{
			SanityManager.m_CurrentSanity = JSON.Load(text).Make<float[]>()[0];
		}
		string text2 = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_sanitybookV2");
		if (text2 != null)
		{
			SanityManager.m_CanSeeSanity = JSON.Load(text2).Make<bool[]>()[0];
		}
		else
		{
			SanityManager.m_CanSeeSanity = false;
		}
	}

	public static void clearFolder(string FolderName)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(FolderName);
		FileInfo[] files = directoryInfo.GetFiles();
		for (int i = 0; i < files.Length; i++)
		{
			files[i].Delete();
		}
		DirectoryInfo[] directories = directoryInfo.GetDirectories();
		foreach (DirectoryInfo obj in directories)
		{
			clearFolder(obj.FullName);
			obj.Delete();
		}
	}

	public static string DropTimeFormat(int minutesLeft)
	{
		int num = minutesLeft / 60;
		if (num >= 24)
		{
			int num2 = num / 24;
			if (num2 != 1)
			{
				return num2 + " days";
			}
			return num2 + " day";
		}
		if (num >= 1)
		{
			if (num != 1)
			{
				return num + " hours";
			}
			return num + " hour";
		}
		if (minutesLeft >= 1)
		{
			if (minutesLeft != 1)
			{
				return minutesLeft + " minutes";
			}
			return minutesLeft + " minute";
		}
		return "";
	}

	public static string GetLampHoverString(Comps.DroppedGearDummy DGD)
	{
		string localizedDisplayName = DGD.m_LocalizedDisplayName;
		string dropper = DGD.m_Extra.m_Dropper;
		string text = "Dropped";
		string text2 = DropTimeFormat(DGD.m_Extra.m_GoalTime - MyMod.MinutesFromStartServer);
		string text3 = "Fuel left for " + text2;
		if (DGD.m_Extra.m_Variant != 0)
		{
			text = "Placed";
			text3 = ((!string.IsNullOrEmpty(text2)) ? ("Fuel left for " + text2 + text + " by " + dropper) : ("No Fuel left " + text + " by " + dropper));
			return localizedDisplayName + "\n" + text3;
		}
		return localizedDisplayName + "\n" + text + " by " + dropper;
	}

	public static string GetSnareHoverString(Comps.DroppedGearDummy DGD)
	{
		string localizedDisplayName = DGD.m_LocalizedDisplayName;
		string dropper = DGD.m_Extra.m_Dropper;
		string text = "Dropped";
		int num = MyMod.MinutesFromStartServer - DGD.m_Extra.m_DroppedTime;
		string text2 = DropTimeFormat(MyMod.TimeToDry("GEAR_Snare") - num + 1);
		if (DGD.m_Extra.m_Variant != 0)
		{
			text = "Setup";
			if (DGD.m_Extra.m_GoalTime == -1)
			{
				return localizedDisplayName + "\nCannot snare here " + text + " by " + dropper;
			}
			if (string.IsNullOrEmpty(text2))
			{
				text2 = "As soon as no one see";
			}
			if (DGD.m_Extra.m_Variant == 1)
			{
				return localizedDisplayName + "\nMay catch in: " + text2 + " " + text + " by " + dropper;
			}
			return localizedDisplayName + "\nWill catch in: " + text2 + " " + text + " by " + dropper;
		}
		return localizedDisplayName + "\n" + text + " by " + dropper;
	}

	public static string GetCureableHoverString(Comps.DroppedGearDummy DGD, string Name)
	{
		string localizedDisplayName = DGD.m_LocalizedDisplayName;
		string dropper = DGD.m_Extra.m_Dropper;
		string text = "dropped";
		int num = MyMod.MinutesFromStartServer - DGD.m_Extra.m_DroppedTime;
		string text2 = DropTimeFormat(MyMod.TimeToDry(Name) - num + 1);
		string text3 = "Cured";
		string text4 = "Cures in";
		if (Name.Contains("GEAR_SCWoodFormB"))
		{
			text3 = "Cooled";
			text4 = "Cools in";
		}
		if (DGD.m_Extra.m_GoalTime == -1)
		{
			return localizedDisplayName + "\nCannot be cured here, " + text + " by " + dropper;
		}
		if (string.IsNullOrEmpty(text2))
		{
			return localizedDisplayName + "\n" + text3 + ", " + text + " by " + dropper;
		}
		return localizedDisplayName + "\n" + text4 + ": " + text2 + ", " + text + " by " + dropper;
	}

	public static DataStr.PriorityActionForOtherPlayer GetPriorityActionForPlayer(int m_ID, Comps.MultiplayerPlayer mP)
	{
		_ = MyMod.playersData[m_ID];
		DataStr.PriorityActionForOtherPlayer priorityActionForOtherPlayer = new DataStr.PriorityActionForOtherPlayer();
		if (GameManager.m_PlayerManager != null && GameManager.m_PlayerManager.m_ItemInHands != null && GameManager.m_PlayerManager.m_ItemInHands.m_HandheldShortwaveItem != null)
		{
			return MyMod.GetActionForOtherPlayer("Invite");
		}
		if (GameManager.m_PlayerManager != null && GameManager.m_PlayerManager.m_ItemInHands != null && GameManager.m_PlayerManager.m_ItemInHands.m_EmergencyStim != null)
		{
			return MyMod.GetActionForOtherPlayer("Stim");
		}
		if (GameManager.m_PlayerManager != null && GameManager.m_PlayerManager.PlayerHoldingTorchThatCanBeLit() && mP.m_TorchIgniter != null && mP.m_LightSourceOn)
		{
			return MyMod.GetActionForOtherPlayer("Lit");
		}
		return MyMod.GetActionForOtherPlayer("Examine");
	}

	public static void SendHarvestPlantState(string state, Harvestable plant)
	{
		DataStr.HarvestableSyncData harvestableSyncData = new DataStr.HarvestableSyncData();
		harvestableSyncData.m_State = state;
		if (plant.gameObject != null)
		{
			GameObject gameObject = plant.gameObject;
			if (gameObject.GetComponent<ObjectGuid>() != null)
			{
				harvestableSyncData.m_Guid = gameObject.GetComponent<ObjectGuid>().Get();
			}
		}
		if (MyMod.sendMyPosition)
		{
			using Packet packet = new Packet(69);
			packet.Write(harvestableSyncData);
			SendTCPData(packet);
		}
		if (MyMod.iAmHost || !MyMod.InOnline())
		{
			using (new Packet(69))
			{
				ServerSend.HARVESTPLANT(0, harvestableSyncData, toEveryOne: true);
			}
			if (state == "Done")
			{
				MPSaveManager.AddHarvestedPlant(harvestableSyncData.m_Guid, MyMod.level_guid);
				ServerSend.LOOTEDHARVESTABLE(0, harvestableSyncData.m_Guid, MyMod.level_guid, MyMod.MinutesFromStartServer, toEveryOne: false);
			}
		}
	}

	public static void RemoveSinglePlayer(Panel_MainMenu __instance)
	{
		BasicMenu basicMenu = ((__instance == null) ? null : __instance.m_BasicMenu);
		if (basicMenu == null || basicMenu.m_ItemModelList == null)
		{
			return;
		}
		for (int i = 0; i < basicMenu.m_ItemModelList.Count; i++)
		{
			BasicMenu.BasicMenuItemModel basicMenuItemModel = basicMenu.m_ItemModelList[i];
			if (basicMenuItemModel == null)
			{
				continue;
			}
			BasicMenu.BasicMenuItemView menuRow = MenuChange.GetMenuRow(basicMenu, i);
			if (basicMenuItemModel.m_Id == "Sandbox" || basicMenuItemModel.m_Id == "Story" || basicMenuItemModel.m_Id == "TFTFTUpsell")
			{
				if (menuRow != null && menuRow.m_Display != null)
				{
					menuRow.m_Display.SetActive(value: false);
				}
			}
			else if (basicMenuItemModel.m_Id == "31")
			{
				string txt = (basicMenuItemModel.m_LabelText = ((MyMod.MyLobby != "") ? "LOBBY" : "MULTIPLAYER"));
				MenuChange.OverrideMenuButton(basicMenu, i, txt, onClickHack: false);
			}
			else if ((basicMenuItemModel.m_Id == "Quit" || basicMenuItemModel.m_Id == "Options" || basicMenuItemModel.m_Id == "Settings") && menuRow != null && menuRow.m_Display != null)
			{
				menuRow.m_Display.SetActive(value: true);
			}
		}
	}

	public static DataStr.WeatherVolunteerData GetWeatherVolunteerData(bool DoNext = false)
	{
		DataStr.WeatherVolunteerData weatherVolunteerData = new DataStr.WeatherVolunteerData();
		if (GameManager.m_Weather != null && GameManager.m_WeatherTransition != null && GameManager.m_WeatherTransition.m_CurrentWeatherSet != null && GameManager.GetUniStorm() != null)
		{
			Weather weatherComponent = GameManager.GetWeatherComponent();
			WeatherStage currentWeatherStage = weatherComponent.m_CurrentWeatherStage;
			WeatherTransition weatherTransitionComponent = GameManager.GetWeatherTransitionComponent();
			if (DoNext)
			{
				weatherTransitionComponent.ChooseNextWeatherSet(weatherTransitionComponent.m_Weights);
			}
			weatherVolunteerData.HighMin = weatherComponent.m_HighTempMinCelsius;
			weatherVolunteerData.HighMax = weatherComponent.m_HighTempMaxCelsius;
			weatherVolunteerData.LowMin = weatherComponent.m_LowTempMinCelsius;
			weatherVolunteerData.LowMax = weatherComponent.m_LowTempMaxCelsius;
			weatherVolunteerData.CoolingHours = weatherComponent.m_HourCoolingBegins;
			weatherVolunteerData.WarmingHours = weatherComponent.m_HourWarmingBegins;
			WeatherSetData currentWeatherSet = GameManager.GetWeatherTransitionComponent().m_CurrentWeatherSet;
			currentWeatherSet.m_WeatherStages[0].m_PreviousType = currentWeatherStage;
			weatherVolunteerData.WeatherType = (int)currentWeatherSet.m_CharacterizingType;
			weatherVolunteerData.WeatherDuration = currentWeatherSet.m_CurrentSetDuration;
			weatherVolunteerData.CurrentRegion = (int)RegionCompat.GetCurrentRegion();
			weatherVolunteerData.PreviousStage = (int)currentWeatherStage;
			for (int i = 0; i < currentWeatherSet.m_WeatherStages.Count; i++)
			{
				weatherVolunteerData.StageDuration.Add(currentWeatherSet.m_WeatherStages[i].m_CurrentDuration);
				weatherVolunteerData.StageTransition.Add(currentWeatherSet.m_WeatherStages[i].m_CurrentTransitionTime);
			}
			for (int j = 0; j < weatherComponent.m_WeatherSetsForScene.Count; j++)
			{
				if (weatherComponent.m_WeatherSetsForScene[j].name == currentWeatherSet.name)
				{
					weatherVolunteerData.SetIndex = j;
					break;
				}
			}
		}
		else
		{
			weatherVolunteerData.WeatherType = 4;
			weatherVolunteerData.WeatherDuration = 1f;
			if (GameManager.GetUniStorm() != null)
			{
				weatherVolunteerData.CurrentRegion = (int)RegionCompat.GetCurrentRegion();
			}
			else
			{
				weatherVolunteerData.CurrentRegion = 0;
			}
			weatherVolunteerData.StageDuration.Add(1f);
			weatherVolunteerData.StageTransition.Add(0f);
		}
		return weatherVolunteerData;
	}

	public static void SendWeatherVolunteerData()
	{
		using Packet packet = new Packet(152);
		packet.Write(GetWeatherVolunteerData(DoNext: true));
		SendTCPData(packet);
	}

	public static void RequestDropsForScene()
	{
		MyMod.DelayedGearsPickup = true;
		using Packet packet = new Packet(90);
		packet.Write(MyMod.levelid);
		packet.Write(MyMod.level_guid);
		packet.Write(GetWeatherVolunteerData());
		packet.Write(GetWeatherVolunteerData().CurrentRegion);
		MyMod.SetRepeatPacket(MyMod.ResendPacketType.Scene);
		SendTCPData(packet);
	}

	public static void LoadEverything()
	{
		MyMod.BakePreSpawnedGearsList();
		MelonLogger.Msg(System.ConsoleColor.Yellow, "Loading everything...");
		MyMod.SendSpawnData();
		MyMod.UpdateDeployedRopes();
		MyMod.LoadAllSnowSheltersByOther();
		MyMod.ApplyOtherCampfires = true;
		MelonLogger.Msg(System.ConsoleColor.Yellow, "Loading done!");
		MyMod.DroppedGearsObjs.Clear();
		MyMod.TrackableDroppedGearsObjs.Clear();
		MyMod.OpenableThings.Clear();
		MyMod.DoorsObjs.Clear();
		MPSaveManager.TrySpawnFestives(MyMod.level_name);
		if (!MyMod.DedicatedServerAppMode && (MyMod.iAmHost || !MyMod.InOnline()))
		{
			MyMod.PickedGearsBackup.Clear();
			System.Collections.Generic.Dictionary<long, DataStr.PickedGearSync> dictionary = MPSaveManager.LoadPickedGearsData(MyMod.level_guid);
			if (dictionary != null)
			{
				foreach (System.Collections.Generic.KeyValuePair<long, DataStr.PickedGearSync> item in dictionary)
				{
					MyMod.PickedGearsBackup.Add(item.Value);
					MyMod.RemovePickedGear(MPSaveManager.GetPickedGearKey(item.Value));
				}
			}
			System.Collections.Generic.Dictionary<string, DataStr.BrokenFurnitureSync> dictionary2 = MPSaveManager.LoadFurnsData(MyMod.level_guid);
			MyMod.FoundSomethingToBreak = false;
			if (dictionary2 != null)
			{
				foreach (System.Collections.Generic.KeyValuePair<string, DataStr.BrokenFurnitureSync> item2 in dictionary2)
				{
					if (item2.Value.m_Broken)
					{
						MyMod.RemoveBrokenFurniture(item2.Value.m_Guid, item2.Value.m_ParentGuid, ReBake: false);
					}
					else
					{
						MyMod.RepairBrokenFurniture(item2.Value.m_Guid, item2.Value.m_ParentGuid);
					}
				}
			}
			MyMod.BakePreSpawnedGearsList();
			if (MyMod.FoundSomethingToBreak)
			{
				MyMod.RemoveAttachedGears = 2;
			}
			else
			{
				MyMod.PickedGearsBackup.Clear();
			}
			foreach (System.Collections.Generic.KeyValuePair<string, DataStr.UniversalSyncableObject> item3 in MPSaveManager.GetUniversalSyncablesForScene(MyMod.level_guid))
			{
				MyMod.SpawnUniversalSyncableObject(item3.Value);
			}
			System.Collections.Generic.Dictionary<string, int> dictionary3 = MPSaveManager.LoadLootedContainersData(MyMod.level_guid);
			if (dictionary3 != null)
			{
				foreach (System.Collections.Generic.KeyValuePair<string, int> item4 in dictionary3)
				{
					int state = item4.Value;
					if (MPSaveManager.ContainerNotEmpty(MyMod.level_guid, item4.Key))
					{
						state = 1;
					}
					MyMod.RemoveLootFromContainer(item4.Key, state);
				}
			}
			System.Collections.Generic.Dictionary<string, int> dictionary4 = MPSaveManager.LoadHarvestedPlants(MyMod.level_guid);
			if (dictionary4 != null)
			{
				foreach (System.Collections.Generic.KeyValuePair<string, int> item5 in dictionary4)
				{
					if (item5.Value == -1)
					{
						MyMod.AddHarvastedPlant(item5.Key);
					}
					else
					{
						MyMod.RemoveHarvastedPlant(item5.Key);
					}
				}
			}
			foreach (System.Collections.Generic.KeyValuePair<string, DataStr.FakeRockCacheVisualData> rockCache in MPSaveManager.GetRockCaches(MyMod.level_guid))
			{
				MyMod.AddRockCache(rockCache.Value);
			}
			MyMod.LoadAllDropsForScene();
			MyMod.LoadAllOpenableThingsForScene();
			MyMod.LoadAniamlCorpsesForScene();
			foreach (DataStr.DeathContainerData deathCreate in MyMod.DeathCreates)
			{
				if (deathCreate.m_LevelKey == MyMod.level_guid)
				{
					MyMod.MakeDeathCreate(deathCreate);
				}
			}
			foreach (System.Collections.Generic.KeyValuePair<string, string> item6 in MPSaveManager.GetDoorsOnScene(MyMod.level_guid))
			{
				MyMod.AddLocksToDoorsByGUID(item6.Key.Split('_')[1]);
			}
			MyMod.CreateCairnsSearchList();
			if (MyMod.InOnline())
			{
				Shared.RegisterWeatherSetForRegion(0, GetWeatherVolunteerData());
			}
		}
		if (MyMod.sendMyPosition)
		{
			MyMod.DoPleaseWait("Please wait...", "Downloading scene drops...");
			RequestDropsForScene();
		}
		ExpeditionManager.MaySpawnSpeicalExpeditionItem(0, MyMod.level_guid);
	}

	public static bool ShouldKeepAfterTreatment(AfflictionType type)
	{
		if (type == AfflictionType.FoodPoisioning || type == AfflictionType.Dysentery || type == AfflictionType.BrokenRib || type == AfflictionType.Infection || type == AfflictionType.IntestinalParasites)
		{
			return true;
		}
		return false;
	}

	public static void ImproveFirstAidSkill(int Aff)
	{
		if (Aff == 5 || Aff == 7 || Aff == 17 || Aff == 27)
		{
			return;
		}
		MyMod.IncressCustomSkill(MyMod.CustomSkills.FirstAid, 2);
		if (!GameManager.m_Inventory || !GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_MedicalSupplies_hangar"))
		{
			return;
		}
		GearItem lowestConditionGearThatMatchesName = GameManager.GetInventoryComponent().GetLowestConditionGearThatMatchesName("GEAR_MedicalSupplies_hangar");
		if (lowestConditionGearThatMatchesName != null)
		{
			lowestConditionGearThatMatchesName.Degrade(1f);
			if (lowestConditionGearThatMatchesName.m_CurrentHP <= 1f)
			{
				GameManager.GetInventoryComponent().DestroyGear(lowestConditionGearThatMatchesName.gameObject);
			}
		}
	}

	public static void Play3dAudioOnAnimal(string GUID, int soundID)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if ((bool)gameObject)
		{
			GameAudioManager.Play3DSound((uint)soundID, gameObject);
		}
	}

	public static void Play3dAudioOnAnimal(string GUID, string soundID)
	{
		GameObject gameObject = ObjectGuidManager.Lookup(GUID);
		if ((bool)gameObject)
		{
			GameAudioManager.Play3DSound(soundID, gameObject);
		}
	}

	public static void SendAnimalAudio(int soundID, string GUID)
	{
		if (MyMod.iAmHost)
		{
			ServerSend.ANIMALAUDIO(0, soundID, GUID, MyMod.level_guid);
		}
		else if (MyMod.sendMyPosition)
		{
			using (Packet packet = new Packet(110))
			{
				packet.Write(_value: true);
				packet.Write(GUID);
				packet.Write(soundID);
				SendTCPData(packet);
			}
		}
	}

	public static void SendAnimalAudio(string soundID, string GUID)
	{
		if (MyMod.iAmHost)
		{
			ServerSend.ANIMALAUDIO(0, soundID, GUID, MyMod.level_guid);
		}
		else if (MyMod.sendMyPosition)
		{
			using (Packet packet = new Packet(110))
			{
				packet.Write(_value: false);
				packet.Write(GUID);
				packet.Write(soundID);
				SendTCPData(packet);
			}
		}
	}

	public static void DisplayNote(GearItem gi)
	{
		HaveReadFakeNote = true;
		string text = "Gived with console notes doesnt store any text.";
		if (gi.GetComponent<ObjectGuid>() != null)
		{
			text = Shared.DecompressString(gi.GetComponent<ObjectGuid>().Get());
		}
		Panel_HUD panel_HUD = GameCompat.Panel<Panel_HUD>();
		panel_HUD.m_CollectibleNoteObject.SetActive(value: true);
		panel_HUD.m_CollectibleNoteObjectText.text = text;
		panel_HUD.m_CollectibleNoteObjectText.alignment = NGUIText.Alignment.Center;
		panel_HUD.m_CollectibleNoteObjectTitle.text = gi.DisplayName;
		panel_HUD.m_InspectMode_StandardElementsParent.SetActive(value: false);
		panel_HUD.m_InspectMode_InventoryStatusSprite.gameObject.SetActive(value: false);
		panel_HUD.m_InspectMode_Equip.gameObject.SetActive(value: false);
		panel_HUD.m_InspectMode_Take.gameObject.SetActive(value: false);
		panel_HUD.m_InspectMode_PutBack.gameObject.SetActive(value: false);
		if ((float)panel_HUD.m_CollectibleNoteObjectText.height > panel_HUD.m_ScrollPanelHeight)
		{
			panel_HUD.m_CollectibleNoteObjectTitle.transform.localPosition = new Vector3(0f, panel_HUD.m_TitleY, 0f);
			panel_HUD.m_CollectibleNoteScrollObject.SetActive(value: true);
			panel_HUD.m_CollectibleNoteScrollView.Scroll(100f);
			UIButton componentInChildren = panel_HUD.m_CollectibleNoteScrollObject.GetComponentInChildren<UIButton>();
			if ((bool)componentInChildren)
			{
				componentInChildren.defaultColor = panel_HUD.m_SliderDefaultColor;
			}
		}
		else
		{
			panel_HUD.m_CollectibleNoteObjectText.transform.position = new Vector3(panel_HUD.m_CollectibleNoteObjectText.transform.position.x, 0f, 0f);
			panel_HUD.m_CollectibleNoteObjectTitle.transform.localPosition = new Vector3(0f, 0.5f * (float)panel_HUD.m_CollectibleNoteObjectText.height + panel_HUD.m_TitleYOffset, 0f);
			panel_HUD.m_CollectibleNoteScrollObject.SetActive(value: false);
		}
		gi.gameObject.SetActive(value: false);
		if (panel_HUD.m_HudDisplayMode == HudDisplayMode.Off)
		{
			NGUITools.SetActive(panel_HUD.m_NonEssentialHud, state: true);
		}
		panel_HUD.UpdateCollectibleNote();
	}

	public static void ResetPendingQuitAfterSaveFailure()
	{
		if (QuitOnSave)
		{
			QuitOnSave = false;
			MyMod.QuitWithoutSaving = false;
			MyMod.NoMelonQuit = false;
			MyMod.QuitWhenSaveOperationFinished = false;
			MyMod.PendingSaveHashToSend = 0L;
			MyMod.RemovePleaseWait();
		}
	}

	public static void OnSaveCompleted(string name = "")
	{
		if (MyMod.PendingRespawn)
		{
			SaveGameSystem.SetAsyncEnabled(enabled: true);
			MyMod.PendingRespawn = false;
			MelonLogger.Msg("[SaveGameSystem] SaveCompletedInternal PendingRespawn");
			SaveSlotInfo currentSaveSlotInfo = SaveGameSlotHelper.GetCurrentSaveSlotInfo();
			if (currentSaveSlotInfo == null)
			{
				MelonLogger.Msg("Wooh Save info is null, trying other way...");
				MelonLogger.Msg("SaveGameSystem.GetCurrentSaveName() = " + SaveGameSystem.GetCurrentSaveName());
				GameManager.LoadSaveGameSlot(SaveGameSystem.GetCurrentSaveName(), 0);
			}
			else
			{
				MelonLogger.Msg("[SaveGameSystem] Saving done, loading");
				GameManager.LoadSaveGameSlot(currentSaveSlotInfo);
			}
		}
		SaveSlotInfo currentSaveSlotInfo2 = SaveGameSlotHelper.GetCurrentSaveSlotInfo();
		string text = ((currentSaveSlotInfo2 != null) ? currentSaveSlotInfo2.m_SaveSlotName : name);
		string text2 = SaveSlotCompat.LoadDataFromSlot(text, "global");
		if (string.IsNullOrEmpty(text2))
		{
			MelonLogger.Error("[SaveGameSystem] Global save data unavailable for " + text + "; no save hash was generated. The game will remain open.");
			ResetPendingQuitAfterSaveFailure();
			return;
		}
		if (QuitOnSave)
		{
			MyMod.PendingSlotDiskCommit = text;
			MyMod.SaveCommitDrainAfterFrame = Time.frameCount + 1;
		}
		long saveFingerprint = Shared.GetSaveFingerprint(text2);
		if (currentSaveSlotInfo2 != null)
		{
			MelonLogger.Msg("[SaveGameSystem] " + currentSaveSlotInfo2.m_SaveSlotName + " just has been saved");
			MelonLogger.Msg("[SaveGameSystem] Save hash: " + saveFingerprint);
			if (QuitOnSave)
			{
				MyMod.PendingSaveHashToSend = saveFingerprint;
				MyMod.QuitWhenSaveOperationFinished = true;
			}
			else if (MyMod.sendMyPosition && MyMod.ServerConfig.m_SaveScamProtection && MyMod.MyUGUID != "")
			{
				using (Packet packet = new Packet(138))
				{
					packet.Write(MyMod.MyUGUID);
					packet.Write(saveFingerprint);
					packet.Write(_value: false);
					SendTCPData(packet);
					return;
				}
			}
			return;
		}
		MelonLogger.Msg("[SaveGameSystem] " + name + " just has been saved");
		MelonLogger.Msg("[SaveGameSystem] Save hash: " + saveFingerprint);
		if (QuitOnSave)
		{
			MyMod.PendingSaveHashToSend = saveFingerprint;
			MyMod.QuitWhenSaveOperationFinished = true;
		}
		else if (MyMod.sendMyPosition && MyMod.ServerConfig.m_SaveScamProtection && MyMod.MyUGUID != "")
		{
			using (Packet packet2 = new Packet(138))
			{
				packet2.Write(MyMod.MyUGUID);
				packet2.Write(saveFingerprint);
				packet2.Write(_value: false);
				SendTCPData(packet2);
			}
		}
	}

	public static void OpenHeatPackFinished(GearItem Heatpack)
	{
		GameManager.GetInventoryComponent().DestroyGear(Heatpack.gameObject);
		GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_SCHeatPackC", 1);
	}

	public static void OpenHeatPack(GearItem Heatpack)
	{
		HeatPackOpenCallback = Heatpack;
		GameCompat.Panel<Panel_GenericProgressBar>().Launch(Localization.Get("GAMEPLAY_OpeningProgress"), 1f, 0f, 0f, "Play_SndInvJerky", null, supressHeavyBreathing: true, skipRestoreInHands: true, null);
	}

	public static void SpawnAndTakeGiftGear()
	{
		GameCompat.Panel<Panel_Inventory>().Enable(enable: false);
		PresentRarity presentRarity = PresentRarity.Shit;
		System.Random random = new System.Random();
		string text = "GEAR_Stone";
		int num = random.Next(0, 100);
		if (num <= 5)
		{
			presentRarity = PresentRarity.Legend;
			text = LegendTier[random.Next(0, LegendTier.Count)];
		}
		else if (num <= 13)
		{
			presentRarity = PresentRarity.Epic;
			text = EpicTier[random.Next(0, EpicTier.Count)];
		}
		else if (num <= 16)
		{
			presentRarity = PresentRarity.High;
			text = HighTier[random.Next(0, HighTier.Count)];
		}
		else if (num <= 20)
		{
			presentRarity = PresentRarity.Medium;
			text = MediumTier[random.Next(0, MediumTier.Count)];
		}
		else if (num <= 22)
		{
			presentRarity = PresentRarity.Low;
			text = LowTier[random.Next(0, LowTier.Count)];
		}
		else
		{
			presentRarity = PresentRarity.Shit;
			text = ShitTier[random.Next(0, ShitTier.Count)];
		}
		GearItem gearItem = GameManager.GetPlayerManagerComponent().InstantiateItemAtPlayersFeet(MyMod.GetGearItemPrefab(text), 1);
		if (!(gearItem == null))
		{
			Comps.DropFakeOnLeave dropFakeOnLeave = gearItem.gameObject.AddComponent<Comps.DropFakeOnLeave>();
			dropFakeOnLeave.m_OldPossition = gearItem.gameObject.transform.position;
			dropFakeOnLeave.m_OldRotation = gearItem.gameObject.transform.rotation;
			GameManager.GetPlayerManagerComponent().ProcessPickupItemInteractionCompat(gearItem);
			switch (presentRarity)
			{
			case PresentRarity.Shit:
				AkSoundEngine.SetSwitch(SWITCHES.URGENCY.GROUP, SWITCHES.URGENCY.SWITCH.HIGH, GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject()));
				GameManager.GetPlayerVoiceComponent().Play("PLAY_FIREFAIL", Il2CppVoice.Priority.Critical);
				break;
			case PresentRarity.Low:
				AkSoundEngine.SetSwitch(SWITCHES.URGENCY.GROUP, SWITCHES.URGENCY.SWITCH.LOW, GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject()));
				GameManager.GetPlayerVoiceComponent().Play("PLAY_VOINSPECTOBJECT", Il2CppVoice.Priority.Critical);
				break;
			case PresentRarity.Medium:
				AkSoundEngine.SetSwitch(SWITCHES.URGENCY.GROUP, SWITCHES.URGENCY.SWITCH.MED, GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject()));
				GameManager.GetPlayerVoiceComponent().Play("PLAY_VOINSPECTOBJECT", Il2CppVoice.Priority.Critical);
				break;
			case PresentRarity.High:
				AkSoundEngine.SetSwitch(SWITCHES.URGENCY.GROUP, SWITCHES.URGENCY.SWITCH.MED, GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject()));
				GameManager.GetPlayerVoiceComponent().Play("PLAY_VOINSPECTOBJECT", Il2CppVoice.Priority.Critical);
				break;
			case PresentRarity.Epic:
				AkSoundEngine.SetSwitch(SWITCHES.URGENCY.GROUP, SWITCHES.URGENCY.SWITCH.HIGH, GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject()));
				GameManager.GetPlayerVoiceComponent().Play("PLAY_VOINSPECTOBJECT", Il2CppVoice.Priority.Critical);
				break;
			case PresentRarity.Legend:
				GameManager.GetPlayerVoiceComponent().Play("PLAY_ENTITYDEATHVO", Il2CppVoice.Priority.Critical);
				break;
			}
		}
	}

	public static void OpenPresentFinished(GearItem Box)
	{
		GameManager.GetPlayerManagerComponent().ConsumeUnitFromInventory(Box.gameObject);
		SpawnAndTakeGiftGear();
	}

	public static void OpenPresent(GearItem Box)
	{
		PresentOpenCallback = Box;
		GameCompat.Panel<Panel_GenericProgressBar>().Launch(Localization.Get("GAMEPLAY_OpeningProgress"), 3f, 0f, 0f, "Play_HarvestingCardboard", null, supressHeavyBreathing: true, skipRestoreInHands: true, null);
	}

	public static void TrySmeltLead(GameObject interactionObject)
	{
		interactionObject.GetComponent<CookingSlot>();
		bool flag = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_ScrapLead");
		bool flag2 = GameManager.GetInventoryComponent().HasNonRuinedItem("GEAR_RecycledCan");
		if (flag && flag2)
		{
			SmeltingLead = true;
			GameCompat.Panel<Panel_GenericProgressBar>().Launch("Smelting...", 10f, 10f, 0f, "PLAY_GASFIRE", null, supressHeavyBreathing: false, skipRestoreInHands: false, null);
		}
		else if (!flag && !flag2)
		{
			HUDMessage.AddMessage("Requires " + GearItem.GetGearDisplayName("GEAR_ScrapLead") + " and " + GearItem.GetGearDisplayName("GEAR_RecycledCan"));
		}
		else if (!flag && flag2)
		{
			HUDMessage.AddMessage("Requires " + GearItem.GetGearDisplayName("GEAR_ScrapLead"));
		}
		else if (flag && !flag2)
		{
			HUDMessage.AddMessage("Requires " + GearItem.GetGearDisplayName("GEAR_RecycledCan"));
		}
	}

	public static void ShowHookedCookingSlotPicker(GameObject objectInteractedWith)
	{
		Panel_ActionPicker panel_ActionPicker = GameCompat.Panel<Panel_ActionPicker>();
		System.Action action = delegate
		{
			TrySmeltLead(objectInteractedWith);
		};
		bool flag = false;
		CookingSlot component = objectInteractedWith.GetComponent<CookingSlot>();
		Campfire component2 = objectInteractedWith.GetComponent<Campfire>();
		WoodStove component3 = objectInteractedWith.GetComponent<WoodStove>();
		if (component != null && component.m_GearPlacePoint != null && component.m_GearPlacePoint.m_FireToAttach != null)
		{
			flag = component.m_GearPlacePoint.m_FireToAttach.IsBurning();
		}
		else if (component2 != null && component2.Fire != null)
		{
			flag = component2.Fire.IsBurning();
		}
		else if (component3 != null && component3.Fire != null)
		{
			flag = component3.Fire.IsBurning();
		}
		if (flag)
		{
			panel_ActionPicker.m_ActionPickerItemDataList.Add(new ActionPickerItemData("ico_forge", "Smelt lead", action));
			panel_ActionPicker.m_ObjectInteractedWith = objectInteractedWith;
			panel_ActionPicker.EnableWithCurrentList();
		}
	}

	public static Skill GetSkillByType(SkillsManager __instance, SkillType skillType)
	{
		Il2CppSystem.Collections.Generic.List<Skill>.Enumerator enumerator = __instance.m_Skills.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Skill current = enumerator.Current;
			if (current.m_SkillType == skillType)
			{
				return current;
			}
		}
		return null;
	}
}
