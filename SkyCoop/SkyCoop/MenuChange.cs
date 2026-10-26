using System;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using TinyJSON;
using UnityEngine;

namespace SkyCoop;

public class MenuChange
{
	[HarmonyPatch(typeof(BasicMenu), "UpdateDescription", null)]
	internal class BasicMenu_UpdateDescription_Post
	{
		private static void Postfix(BasicMenu __instance, int buttonIndex)
		{
			if (__instance == null || buttonIndex >= __instance.m_ItemModelList.Count || __instance.m_ItemModelList[buttonIndex] == null)
			{
				return;
			}
			if (__instance.m_ItemModelList[buttonIndex].m_DescriptionText == "GAMEPLAY_Description30")
			{
				__instance.m_DescriptionLabel.text = "People who supported the project.";
			}
			if (__instance.m_ItemModelList[buttonIndex].m_DescriptionText == "GAMEPLAY_Description31")
			{
				if (MyMod.MyLobby == "")
				{
					__instance.m_DescriptionLabel.text = "Host or join a multiplayer session.";
				}
				else
				{
					__instance.m_DescriptionLabel.text = "Show lobby you are currently in.";
				}
			}
			if (__instance.m_ItemModelList[buttonIndex].m_DescriptionText == "GAMEPLAY_Description32")
			{
				__instance.m_DescriptionLabel.text = "Invite your steam friend to this P2P session. Once you invite someone, they will join the lobby first and then will be connected to your server.";
			}
			if (MenuMode == "Multiplayer")
			{
				if (__instance.m_ItemModelList[buttonIndex].m_Id == "NewSurvival")
				{
					__instance.m_DescriptionLabel.text = "Configure and host a session.";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "LoadSurvival")
				{
					if (SteamConnect.CanUseSteam)
					{
						__instance.m_DescriptionLabel.text = "Find a server or join by IP address.";
					}
					else
					{
						__instance.m_DescriptionLabel.text = "Join by IP address.";
					}
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "Feats")
				{
					__instance.m_DescriptionLabel.text = "Change your nickname and customize your appeal.";
				}
			}
			else if (MenuMode == "Join")
			{
				if (__instance.m_ItemModelList[buttonIndex].m_Id == "NewSurvival")
				{
					__instance.m_DescriptionLabel.text = "Browse public servers.";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "LoadSurvival")
				{
					__instance.m_DescriptionLabel.text = "Connect to a server by IP address.";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "Feats")
				{
					__instance.m_DescriptionLabel.text = "Opens steam friends overlay.";
				}
			}
			else if (MenuMode == "MultiProfileSettings")
			{
				if (__instance.m_ItemModelList[buttonIndex].m_Id == "NewSurvival")
				{
					__instance.m_DescriptionLabel.text = "Change your character name. Other players will see it ingame.";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "LoadSurvival")
				{
					__instance.m_DescriptionLabel.text = "Toggle supporter bonuses and select flairs.";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "Feats")
				{
					__instance.m_DescriptionLabel.text = "Copy to clipboard EGS or Steam Account ID.";
				}
			}
			else if (MenuMode == "Lobby")
			{
				if (__instance.m_ItemModelList[buttonIndex].m_Id == "ResumeSurvival")
				{
					__instance.m_DescriptionLabel.text = "Start your session if everyone is ready, or if you do not want to wait for other players.\n(Players will be able to join, even after the session is already started).";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "NewSurvival")
				{
					__instance.m_DescriptionLabel.text = "Create or load a sandbox game.";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "LoadSurvival")
				{
					__instance.m_DescriptionLabel.text = "Invite your steam friend to this lobby.";
				}
				else if (__instance.m_ItemModelList[buttonIndex].m_Id == "Feats")
				{
					__instance.m_DescriptionLabel.text = "Copy invite link to clipboard.";
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "Initialize", null)]
	public class Panel_MainMenu_Start
	{
		public static void Postfix(Panel_MainMenu __instance)
		{
			MelonLogger.Msg("[UI] Trying modify main menu...");
			try
			{
				AddButton(__instance, "MULTIPLAYER", 4, 1);
				MoveUpMainMenuWordmark(Convert.ToInt16(__instance.m_BasicMenu.m_MenuItems.Count.ToString()));
			}
			catch (Exception ex)
			{
				MelonLogger.Error("[UI] Could not add the MULTIPLAYER menu entry: " + ex);
			}
		}
	}

	[HarmonyPatch(typeof(Panel_PauseMenu), "Initialize", null)]
	public class Panel_PauseMenu_Start
	{
		public static void Postfix(Panel_PauseMenu __instance)
		{
			AddButtonPause(__instance, "INVITE", 0, InvitePlus);
			int quit = __instance.m_MenuItems.IndexOf(Panel_PauseMenu.PauseMenuItemType.Quit);
			AddButtonPause(__instance, "QUIT TO MAIN MENU", (quit < 0) ? __instance.m_MenuItems.Count : quit, ReturnToMenuPlus);
		}
	}

	// The menu is rebuilt from item types, so custom items need their actions resolved here too
	// (keyboard, gamepad and double click go through these actions instead of the button hook).
	[HarmonyPatch(typeof(Panel_PauseMenu), "GetActionFromType")]
	public class Panel_PauseMenu_GetActionFromType
	{
		public static void Postfix(Panel_PauseMenu __instance, Panel_PauseMenu.PauseMenuItemType type, ref Il2CppSystem.Action __result)
		{
			if ((int)type == 30 + InvitePlus)
			{
				__result = (Action)PauseInvite;
			}
			else if ((int)type == 30 + ReturnToMenuPlus)
			{
				Panel_PauseMenu panel = __instance;
				__result = (Action)delegate
				{
					PauseReturnToMenu(panel);
				};
			}
		}
	}

	[HarmonyPatch(typeof(Panel_PauseMenu), "Update", null)]
	public class Panel_PauseMenu_Update
	{
		public static void Postfix(Panel_PauseMenu __instance)
		{
			FixUpButtonStrings(__instance);
		}
	}

	[HarmonyPatch(typeof(Panel_Sandbox), "Update", null)]
	public class Panel_Sandbox_Update
	{
		public static void Postfix(Panel_Sandbox __instance)
		{
			if (__instance == null || __instance.m_BasicMenu == null || __instance.m_BasicMenu.m_MenuItems == null)
			{
				return;
			}
			List<BasicMenu.BasicMenuItemView> menuItems = __instance.m_BasicMenu.m_MenuItems;
			for (int i = 0; i < menuItems.Count; i++)
			{
				BasicMenu.BasicMenuItemView basicMenuItemView = menuItems[i];
				if (basicMenuItemView != null && !(basicMenuItemView.m_Button == null) && !(basicMenuItemView.m_Button.gameObject == null) && basicMenuItemView.m_Button.gameObject.GetComponent<Comps.UiButtonPressHook>() == null)
				{
					basicMenuItemView.m_Button.gameObject.AddComponent<Comps.UiButtonPressHook>().m_CustomId = i;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Sandbox), "OnClickBack", null)]
	public class Panel_Sandbox_OnClickBack
	{
		public static bool Prefix(Panel_Sandbox __instance)
		{
			if (MenuMode == "Join")
			{
				ChangeMenuItems("Multiplayer");
				return false;
			}
			if (MenuMode == "MultiProfileSettings")
			{
				ChangeMenuItems("Multiplayer");
				return false;
			}
			if (MenuMode == "Nothing")
			{
				MyMod.HostMenuClose();
				return false;
			}
			if (MenuMode == "Lobby")
			{
				if (MyMod.LobbyUI != null)
				{
					MyMod.LobbyUI.SetActive(value: false);
				}
				// Used to fall through to the native Back with MenuMode still "Lobby" and the Steam lobby still joined,
				// which left the main menu unresponsive. Leave the lobby and return to the multiplayer menu instead.
				MelonLogger.Msg("[Menu] Back from lobby: leaving lobby " + MyMod.MyLobby);
				CoopReturnToMenu.LeaveLobby();
				ChangeMenuItems("Multiplayer");
				return false;
			}
			else
			{
				if (MenuMode == "LobbySettings")
				{
					if (!MyMod.StartServerAfterSelectSave)
					{
						ChangeMenuItems("Lobby");
					}
					else
					{
						MyMod.StartServerAfterSelectSave = false;
						ChangeMenuItems("Multiplayer");
					}
					return false;
				}
				if (MenuMode == "NewGameSelect")
				{
					ChangeMenuItems("LobbySettings");
					return false;
				}
				if (MenuMode == "Multiplayer")
				{
					ChangeMenuItems("Original");
					return true;
				}
				if (MenuMode == "Browser")
				{
					MenuMode = "Join";
					Transform parent = GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, 0, 0, 5);
					GameCompat.SetChildActive(parent, 1, state: true);
					GameCompat.SetChildActive(parent, 2, state: true);
					GameCompat.SetChildActive(parent, 4, state: true);
					GameCompat.SetChildActive(parent, 5, state: true);
					if (MyMod.ServerBrowser != null)
					{
						MyMod.ServerBrowser.SetActive(value: false);
					}
					return false;
				}
				if (MenuMode == "Customize")
				{
					if (MyMod.TargetFlairSlot == -1)
					{
						MyMod.CustomizeUiPanel("Close");
						MenuMode = "MultiProfileSettings";
						Transform parent2 = GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, 0, 0, 5);
						GameCompat.SetChildActive(parent2, 1, state: true);
						GameCompat.SetChildActive(parent2, 2, state: true);
						GameCompat.SetChildActive(parent2, 4, state: true);
						GameCompat.SetChildActive(parent2, 5, state: true);
						GameCompat.SetChildActive(GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, default(int), default(int)), 3, state: true);
						SetDefaultCamera();
					}
					else
					{
						MyMod.CustomizeUiPanel("Main");
					}
					return false;
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_ChooseSandbox), "OnClickBack", null)]
	public class Panel_ChooseSandbox_OnClickBack
	{
		public static bool Prefix(Panel_ChooseSandbox __instance)
		{
			if (MenuMode == "LobbySettings")
			{
				__instance.Enable(enable: false);
				InterfaceManager.TrySetPanelEnabled<Panel_Sandbox>(enable: true);
				ChangeMenuItems("LobbySettings");
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_MainMenu), "OnLoadSaveSlot", null)]
	public class Panel_MainMenu_OnLoadSaveSlot
	{
		public static bool Prefix(Panel_MainMenu __instance, SaveSlotType saveSlotType, int slotIndex)
		{
			if (MenuMode == "LobbySettings" || MyMod.StartServerAfterSelectSave)
			{
				if (!CheckSaveRNG(SaveGameSlotHelper.GetSaveSlotInfo(SaveSlotType.SANDBOX, slotIndex).m_SaveSlotName))
				{
					MelonLogger.Msg("[Panel_MainMenu][OnLoadSaveSlot] CheckSaveRNG == false");
					InterfaceManager.TrySetPanelEnabled<Panel_ChooseSandbox>(enable: true);
					return false;
				}
				MelonLogger.Msg("[Panel_MainMenu][OnLoadSaveSlot] CheckSaveRNG == true");
			}
			if (MenuMode == "LobbySettings" && !MyMod.StartServerAfterSelectSave)
			{
				SaveSlotInfo saveSlotInfo = SaveGameSlotHelper.GetSaveSlotInfo(SaveSlotType.SANDBOX, slotIndex);
				InterfaceManager.TrySetPanelEnabled<Panel_ChooseSandbox>(enable: false);
				MyMod.SelectedSaveSeed = GetSaveSeed(slotIndex);
				MyMod.SelectedSaveName = saveSlotInfo.m_SaveSlotName;
				if (Utils.SceneIsTransition(saveSlotInfo.m_Location))
				{
					saveSlotInfo.m_Region = Utils.GetHardcodedRegionForLocation(saveSlotInfo.m_Location);
				}
				string text = "";
				if (string.IsNullOrEmpty(saveSlotInfo.m_Region))
				{
					string hardcodedRegionForLocation = Utils.GetHardcodedRegionForLocation(saveSlotInfo.m_Location);
					text = ((hardcodedRegionForLocation != "") ? hardcodedRegionForLocation : ((!(saveSlotInfo.m_Location == "MineTransitionZone") && !(saveSlotInfo.m_Location == "HighwayMineTransitionZone")) ? saveSlotInfo.m_Location : saveSlotInfo.m_Location));
				}
				else
				{
					text = saveSlotInfo.m_Region;
				}
				MyMod.LobbyStartingRegion = (int)RegionCompat.FromSceneName(text);
				MyMod.LobbyStartingExperience = (int)saveSlotInfo.m_XPMode;
				SteamConnect.Main.SetSaveSlotSeed(MyMod.SelectedSaveSeed);
				SteamConnect.Main.SetNewGameSettings(MyMod.LobbyStartingRegion, MyMod.LobbyStartingExperience);
				MelonLogger.Msg("Selected save with seed " + MyMod.SelectedSaveSeed);
				InterfaceManager.TrySetPanelEnabled<Panel_Sandbox>(enable: true);
				ChangeMenuItems("Lobby");
				SetLobbyState("SelectedSave");
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_SelectExperience), "OnClickBack", null)]
	public class Panel_SelectExperience_OnClickBack
	{
		public static bool Prefix(Panel_SelectExperience __instance)
		{
			if (MenuMode == "NewGameSelect")
			{
				__instance.Enable(enable: false);
				InterfaceManager.TrySetPanelEnabled<Panel_Sandbox>(enable: true);
				ChangeMenuItems("LobbySettings");
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_SelectExperience), "OnExperienceClicked", null)]
	public class Panel_SelectExperience_OnExperienceClicked
	{
		public static bool Prefix(Panel_SelectExperience __instance)
		{
			Panel_SelectExperience.XPModeMenuItem selectedMenuItem = __instance.GetSelectedMenuItem();
			if (selectedMenuItem == null)
			{
				return false;
			}
			ExperienceModeType selectedExperienceMode = ExperienceCompat.GetSelectedExperienceMode(__instance);
			if (!Environment.GetCommandLineArgs().Contains("-customexp") && selectedExperienceMode == ExperienceModeType.Custom)
			{
				MyMod.NoCustomExp();
				return false;
			}
			if (MenuMode == "NewGameSelect")
			{
				if (!SteamConnect.IsMyLobby)
				{
					__instance.Enable(enable: false);
					InterfaceManager.TrySetPanelEnabled<Panel_Sandbox>(enable: true);
					ChangeMenuItems("Lobby");
					return false;
				}
				if (selectedMenuItem == null)
				{
					return false;
				}
				TempExperience = (int)selectedExperienceMode;
				__instance.Enable(enable: false);
				if (MyMod.ServerConfig.m_PlayersSpawnType != 2)
				{
					InterfaceManager.TrySetPanelEnabled<Panel_SelectRegion_Map>(enable: true);
				}
				else
				{
					MyMod.LobbyStartingRegion = 6;
					MyMod.LobbyStartingExperience = TempExperience;
					SteamConnect.Main.SetNewGameSettings(MyMod.LobbyStartingRegion, MyMod.LobbyStartingExperience);
					SteamConnect.Main.SetLobbyState("SelectedNewSave");
					__instance.Enable(enable: false);
					MyMod.m_Panel_Sandbox.Enable(enable: true);
					ChangeMenuItems("Lobby");
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_Badges), "OnCancel", null)]
	public class Panel_Badges_OnCancel
	{
		public static bool Prefix(Panel_PauseMenu __instance)
		{
			if (MenuMode == "LobbySettings")
			{
				__instance.Enable(enable: false);
				InterfaceManager.TrySetPanelEnabled<Panel_Sandbox>(enable: true);
				ChangeMenuItems("LobbySettings");
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(BasicMenu), "InternalClickAction", null)]
	internal class BasicMenu_InternalClickAction_Pre
	{
		private static bool Prefix(int index, BasicMenu __instance, bool doAction)
		{
			if (!doAction)
			{
				return true;
			}
			if (__instance == null || __instance.m_ItemModelList == null || index < 0 || index >= __instance.m_ItemModelList.Count)
			{
				return true;
			}
			BasicMenu.BasicMenuItemModel basicMenuItemModel = __instance.m_ItemModelList[index];
			if (basicMenuItemModel == null)
			{
				return true;
			}
			if (MyMod.m_Panel_Sandbox != null && __instance == MyMod.m_Panel_Sandbox.m_BasicMenu && MenuMode != "Original")
			{
				// Enter / gamepad confirm on a multiplayer row. The native survival actions (resume, new game, load)
				// must never run from here: they started an ordinary offline game from the multiplayer menu.
				if (IsSandboxMenuMode)
				{
					HandleSandboxClick(index);
				}
				return false;
			}
			if (basicMenuItemModel.m_LabelText == "Donaters")
			{
				Application.OpenURL("https://filigrani.github.io/SkyCoop/");
				return false;
			}
			if (basicMenuItemModel.m_LabelText == "MULTIPLAYER" || (MyMod.m_Panel_MainMenu != null && __instance == MyMod.m_Panel_MainMenu.m_BasicMenu && basicMenuItemModel.m_Id == "31") || basicMenuItemModel.m_LabelText == "LOBBY")
			{
				MyMod.m_Panel_Sandbox = InterfaceManager.LoadPanel<Panel_Sandbox>();
				if (!InterfaceManager.TrySetPanelEnabled<Panel_Sandbox>(enable: true))
				{
					return false;
				}
				MyMod.m_Panel_Sandbox.m_TitleLocalizationId = "MULTIPLAYER";
				MyMod.m_Panel_Sandbox.m_BasicMenu.UpdateTitle("MULTIPLAYER");
				ChangeMenuItems((basicMenuItemModel.m_LabelText == "LOBBY" || MyMod.MyLobby != "") ? "Lobby" : "Multiplayer");
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(UIButton), "OnClick")]
	internal class UIButton_Press
	{
		private static bool Prefix(UIButton __instance)
		{
			if (__instance.gameObject != null && __instance.gameObject.GetComponent<Comps.UiButtonPressHook>() != null)
			{
				int customId = __instance.gameObject.GetComponent<Comps.UiButtonPressHook>().m_CustomId;
				if (customId != -1)
				{
					Panel_PauseMenu panel_PauseMenu = GameCompat.Panel<Panel_PauseMenu>();
					if (panel_PauseMenu != null && panel_PauseMenu.isActiveAndEnabled)
					{
						if (customId == 0)
						{
							PauseInvite();
						}
						else if (customId == 1)
						{
							PauseReturnToMenu(panel_PauseMenu);
						}
					}
					else
					{
						if (!(MyMod.m_Panel_Sandbox != null) || !MyMod.m_Panel_Sandbox.isActiveAndEnabled || !(MenuMode != "Original"))
						{
							return true;
						}
						HandleSandboxClick(customId);
					}
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(BasicMenu), "OnItemDoubleClicked")]
	internal class BasicMenu_OnItemDoubleClicked
	{
		private static bool Prefix(BasicMenu __instance)
		{
			// The first click of a double click is already handled by the button hook; the second one would run
			// the native survival action of the row (the way into the ordinary game menus).
			if (MyMod.m_Panel_Sandbox != null && __instance == MyMod.m_Panel_Sandbox.m_BasicMenu && MenuMode != "Original")
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Panel_Sandbox), "Enable")]
	internal class Panel_Sandbox_Enable
	{
		private static void Postfix(Panel_Sandbox __instance, bool enable)
		{
			// Returning from the game's options (or another sub panel) rebuilds the survival rows; put the multiplayer ones back.
			if (enable && __instance == MyMod.m_Panel_Sandbox && IsSandboxMenuMode)
			{
				try
				{
					ChangeMenuItems(MenuMode);
				}
				catch (Exception ex)
				{
					MelonLogger.Warning("[Menu] Restoring " + MenuMode + " rows: " + ex.Message);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Panel_Sandbox), "ConfigureMenu")]
	private static class Panel_Sandbox_ConfigureMenu
	{
		private static bool Prefix(Panel_Sandbox __instance)
		{
			if (SaveGameSlotHelper.GetNumSaveSlots(SaveSlotType.SANDBOX) <= 0)
			{
				MelonLogger.Msg(ConsoleColor.Yellow, "No saves found, patching menu to not shitup ui");
				if (__instance.m_BasicMenu == null)
				{
					MelonLogger.Msg(ConsoleColor.Red, "OH NO __instance.m_BasicMenu null");
					return false;
				}
				__instance.m_BasicMenu.Reset();
				__instance.m_BasicMenu.UpdateTitle(__instance.m_TitleLocalizationId, "", Vector3.zero);
				for (int i = 0; i < __instance.m_MenuItems.Count; i++)
				{
					__instance.AddMenuItem(i);
				}
				__instance.m_BasicMenu.SetBackAction((Action)__instance.OnClickBack);
				__instance.m_BasicMenu.EnableConfirm(enable: false, "GAMEPLAY_Select");
				return false;
			}
			return true;
		}
	}

	// Rows of the borrowed Panel_Sandbox menu in multiplayer modes; used by mouse clicks (button hook) and by
	// keyboard, gamepad and double click (BasicMenu actions), which used to run the native survival actions.
	public static void HandleSandboxClick(int customId)
	{
		// A click can reach here from the button hook and again from a BasicMenu action; act on it once.
		if (Time.realtimeSinceStartup - lastSandboxClick < 0.3f)
		{
			return;
		}
		lastSandboxClick = Time.realtimeSinceStartup;
		GameAudioManager.PlayGUIButtonClick();
		if (customId == optionsRow && (MenuMode == "Multiplayer" || MenuMode == "Lobby"))
		{
			OpenGameOptions();
			return;
		}
		if (MenuMode == "Multiplayer")
		{
			switch (customId)
			{
			case 1:
			{
				MenuMode = "Nothing";
				Transform parent = GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, 0, 0, 5);
				GameCompat.SetChildActive(parent, 1, state: false);
				GameCompat.SetChildActive(parent, 2, state: false);
				GameCompat.SetChildActive(parent, 4, state: false);
				GameCompat.SetChildActive(parent, 5, state: false);
				MyMod.HostMenu(Ignore: true);
				break;
			}
			case 2:
				if (SteamConnect.CanUseSteam)
				{
					ChangeMenuItems("Join");
				}
				else
				{
					ConnectByIp();
				}
				break;
			case 3:
				ChangeMenuItems("MultiProfileSettings");
				break;
			}
		}
		else if (MenuMode == "MultiProfileSettings")
		{
			switch (customId)
			{
			case 1:
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "HOW DO YOU WANT TO BE CALLED?", MyMod.MyChatName, Panel_Confirmation.ButtonLayout.Button_2, "GAMEPLAY_Apply", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
				break;
			case 2:
				OpenFlairsMenu();
				break;
			case 3:
				if (Supporters.IsLoaded())
				{
					GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "Your ID was copied to clipboard", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null);
					GUIUtility.systemCopyBuffer = Supporters.MyID;
				}
				else
				{
					GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "Can't detect your account ID", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null);
				}
				break;
			}
		}
		else if (MenuMode == "Join")
		{
			switch (customId)
			{
			case 1:
			{
				Transform parent2 = GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, 0, 0, 5);
				GameCompat.SetChildActive(parent2, 1, state: false);
				GameCompat.SetChildActive(parent2, 2, state: false);
				GameCompat.SetChildActive(parent2, 4, state: false);
				GameCompat.SetChildActive(parent2, 5, state: false);
				MenuMode = "Browser";
				SteamConnect.Main.BrowseServers();
				break;
			}
			case 2:
				ConnectByIp();
				break;
			case 3:
				if (SteamConnect.CanUseSteam)
				{
					SteamConnect.Main.OpenFriends();
				}
				break;
			}
		}
		else if (MenuMode == "Lobby")
		{
			switch (customId)
			{
			case 0:
				if (!SteamConnect.IsMyLobby || !CanStartServer() || !SteamConnect.CanUseSteam)
				{
					return;
				}
				CloseLobbyUI();
				if (SteamConnect.CanUseSteam)
				{
					if (MyMod.LobbyState == "SelectedNewSave")
					{
						SteamConnect.Main.CreateSaveForHosting();
					}
					else if (MyMod.LobbyState == "SelectedSave")
					{
						SteamConnect.Main.LoadSaveForHosting();
					}
					SetLobbyState("StartingGame");
				}
				break;
			case 1:
				if (SteamConnect.IsMyLobby)
				{
					ChangeMenuItems("LobbySettings");
				}
				break;
			case 2:
				if (SteamConnect.CanUseSteam)
				{
					SteamConnect.Main.MakeFriendListUI();
				}
				break;
			case 3:
				if (SteamConnect.CanUseSteam)
				{
					SteamConnect.Main.CopyInviteLink();
					GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, "Link copied to clipboard.", Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null);
				}
				break;
			}
		}
		else if (MenuMode == "LobbySettings")
		{
			if (!MyMod.StartServerAfterSelectSave && !SteamConnect.IsMyLobby)
			{
				ChangeMenuItems("Lobby");
				return;
			}
			switch (customId)
			{
			case 1:
				if (!MyMod.StartServerAfterSelectSave)
				{
					MenuMode = "NewGameSelect";
				}
				CloseLobbyUI();
				MyMod.m_Panel_Sandbox.OnClickNew();
				break;
			case 2:
				CloseLobbyUI();
				MyMod.m_Panel_Sandbox.OnClickLoad();
				break;
			case 3:
				CloseLobbyUI();
				MyMod.m_Panel_Sandbox.OnClickFeats();
				break;
			}
		}
	}

	private static float lastSandboxClick = -10f;

	// Row of the borrowed survival menu shown as the game's own settings in multiplayer modes, -1 when unavailable.
	private static int optionsRow = -1;

	private static readonly string[] SandboxModes = new string[5] { "Multiplayer", "Join", "MultiProfileSettings", "Lobby", "LobbySettings" };

	public static bool IsSandboxMenuMode => Array.IndexOf(SandboxModes, MenuMode) >= 0;

	public static string MenuMode = "Original";

	public static int TempExperience = 0;

	public const int InvitePlus = 2;

	public const int ReturnToMenuPlus = 3;

	private static string nativeQuitLabel;

	public static void SetDefaultCamera()
	{
		GameManager.GetPlayerManagerComponent().TeleportPlayer(new Vector3(48.08f, 3.33f, 63.81f), GameManager.GetCurrentCamera().transform.rotation);
	}

	public static void SetFlairsCamera()
	{
		GameManager.GetPlayerManagerComponent().TeleportPlayer(new Vector3(-7.71f, 16.85f, 11.47f), GameManager.GetCurrentCamera().transform.rotation);
	}

	public static void MoveUpMainMenuWordmark(int numOfMainMenuItems)
	{
		GameObject gameObject = GameObject.Find("Panel_MainMenu/MainPanel/Main/TLD_wordmark");
		if (!(gameObject == null))
		{
			gameObject.transform.localPosition += new Vector3(0f, (numOfMainMenuItems - 6) * 30, 0f);
		}
	}

	public static void AddButton(Panel_MainMenu __instance, string name, int order, int plus)
	{
		Panel_MainMenu.MainMenuItem mainMenuItem = new Panel_MainMenu.MainMenuItem();
		mainMenuItem.m_LabelLocalizationId = name;
		mainMenuItem.m_Type = (Panel_MainMenu.MainMenuItem.MainMenuItemType)(30 + plus);
		__instance.m_MenuItems.Insert(order, mainMenuItem);
		string id = __instance.m_MenuItems[order].m_Type.ToString();
		int type = (int)__instance.m_MenuItems[order].m_Type;
		__instance.m_BasicMenu.AddItem(id, type, order, name, "", "", (Action)__instance.OnSandbox, Color.gray, Color.white);
	}

	public static void SetLobbyState(string state)
	{
		SteamConnect.Main.SetLobbyState(state);
	}

	public static void ReturnOriginalButtons(BasicMenu menu, int index)
	{
		BasicMenu.BasicMenuItemView menuRow = GetMenuRow(menu, index);
		if (menuRow != null && !(menuRow.m_Display == null) && !(menuRow.m_Button == null))
		{
			Comps.UiButtonKeyboardPressSkip component = menuRow.m_Display.GetComponent<Comps.UiButtonKeyboardPressSkip>();
			if (component != null)
			{
				menuRow.m_Button.onClick = component.m_Click;
			}
			menuRow.m_Display.SetActive(value: true);
		}
	}

	public static void ReturnOriginalButtons(Transform Grid, int index)
	{
		Grid.GetChild(index).GetChild(0).GetComponent<UILabel>();
		UIButton component = Grid.GetChild(index).GetComponent<UIButton>();
		if (Grid.GetChild(index).gameObject.GetComponent<Comps.UiButtonKeyboardPressSkip>() != null)
		{
			Comps.UiButtonKeyboardPressSkip component2 = Grid.GetChild(index).gameObject.GetComponent<Comps.UiButtonKeyboardPressSkip>();
			component.onClick = component2.m_Click;
		}
	}

	public static BasicMenu.BasicMenuItemView GetMenuRow(BasicMenu menu, int index)
	{
		if (menu == null || menu.m_MenuItems == null || index < 0 || index >= menu.m_MenuItems.Count)
		{
			return null;
		}
		return menu.m_MenuItems[index];
	}

	public static void OverrideMenuButton(BasicMenu menu, int index, string txt, bool onClickHack = true)
	{
		BasicMenu.BasicMenuItemView menuRow = GetMenuRow(menu, index);
		if (menuRow == null || menuRow.m_Display == null)
		{
			return;
		}
		UIButton button = menuRow.m_Button;
		if (onClickHack && button != null)
		{
			Comps.UiButtonKeyboardPressSkip uiButtonKeyboardPressSkip = menuRow.m_Display.GetComponent<Comps.UiButtonKeyboardPressSkip>();
			if (uiButtonKeyboardPressSkip == null)
			{
				uiButtonKeyboardPressSkip = menuRow.m_Display.AddComponent<Comps.UiButtonKeyboardPressSkip>();
			}
			uiButtonKeyboardPressSkip.m_Click = button.onClick;
			button.onClick = null;
		}
		if (menuRow.m_Label != null)
		{
			menuRow.m_Label.mText = txt;
			menuRow.m_Label.text = txt;
			menuRow.m_Label.ProcessText();
		}
		menuRow.m_Display.SetActive(value: true);
	}

	public static void OverrideMenuButton(Transform Grid, int index, string txt, bool onClickHack = true)
	{
		UILabel component = Grid.GetChild(index).GetChild(0).GetComponent<UILabel>();
		UIButton component2 = Grid.GetChild(index).GetComponent<UIButton>();
		if (onClickHack)
		{
			if (Grid.GetChild(index).gameObject.GetComponent<Comps.UiButtonKeyboardPressSkip>() == null)
			{
				Grid.GetChild(index).gameObject.AddComponent<Comps.UiButtonKeyboardPressSkip>().m_Click = component2.onClick;
				component2.onClick = null;
			}
			else if (Grid.GetChild(index).gameObject.GetComponent<Comps.UiButtonKeyboardPressSkip>() != null)
			{
				Grid.GetChild(index).gameObject.GetComponent<Comps.UiButtonKeyboardPressSkip>().m_Click = component2.onClick;
				component2.onClick = null;
			}
		}
		component.mText = txt;
		component.text = txt;
		component.ProcessText();
		Grid.GetChild(index).gameObject.SetActive(value: true);
	}

	public static void ClearMenuButtons(BasicMenu menu)
	{
		if (menu == null || menu.m_MenuItems == null)
		{
			return;
		}
		for (int i = 0; i < menu.m_MenuItems.Count; i++)
		{
			BasicMenu.BasicMenuItemView basicMenuItemView = menu.m_MenuItems[i];
			if (basicMenuItemView != null && basicMenuItemView.m_Display != null)
			{
				basicMenuItemView.m_Display.SetActive(value: false);
			}
		}
	}

	public static void ClearMenuButtons(Transform Grid)
	{
		for (int i = 0; i <= 6; i++)
		{
			Grid.GetChild(i).gameObject.SetActive(value: false);
		}
	}

	public static void FixUpButtonStrings(Panel_PauseMenu __instance)
	{
		if (__instance == null || __instance.m_BasicMenu == null)
		{
			return;
		}
		List<BasicMenu.BasicMenuItemModel> itemModelList = __instance.m_BasicMenu.m_ItemModelList;
		List<BasicMenu.BasicMenuItemView> menuItems = __instance.m_BasicMenu.m_MenuItems;
		if (itemModelList == null || menuItems == null)
		{
			return;
		}
		string inviteId = ((Panel_PauseMenu.PauseMenuItemType)(30 + InvitePlus)).ToString();
		string returnId = ((Panel_PauseMenu.PauseMenuItemType)(30 + ReturnToMenuPlus)).ToString();
		string quitId = Panel_PauseMenu.PauseMenuItemType.Quit.ToString();
		bool coop = CoopReturnToMenu.CanRequest;
		for (int i = 0; i < itemModelList.Count && i < menuItems.Count; i++)
		{
			BasicMenu.BasicMenuItemModel model = itemModelList[i];
			BasicMenu.BasicMenuItemView view = menuItems[i];
			if (model == null || view == null)
			{
				continue;
			}
			if (model.m_Id == inviteId)
			{
				if (!SteamConnect.CanUseSteam)
				{
					SetPauseItem(model, view, "INVITE (STEAM ONLY)", selectable: false, 0);
				}
				else if (MyMod.MyLobby == "")
				{
					SetPauseItem(model, view, "INVITE (NO LOBBY)", selectable: false, 0);
				}
				else
				{
					SetPauseItem(model, view, "INVITE", selectable: true, 0);
				}
			}
			else if (model.m_Id == returnId)
			{
				SetPauseItem(model, view, CoopReturnToMenu.InProgress ? "SAVING..." : "QUIT TO MAIN MENU", !CoopReturnToMenu.InProgress && !MyMod.SaveExitPending, 1);
			}
			else if (model.m_Id == quitId)
			{
				// In co-op the native Quit closes the game (after saving); outside co-op it keeps its own label and behaviour.
				if (nativeQuitLabel == null && view.m_Label != null)
				{
					nativeQuitLabel = view.m_Label.text;
				}
				string label = coop ? "QUIT GAME" : nativeQuitLabel;
				if (!string.IsNullOrEmpty(label) && view.m_Label != null && view.m_Label.text != label)
				{
					SetLabel(view.m_Label, label);
				}
			}
		}
	}

	private static void SetPauseItem(BasicMenu.BasicMenuItemModel model, BasicMenu.BasicMenuItemView view, string text, bool selectable, int hookId)
	{
		model.m_LabelText = text;
		model.m_Selectable = selectable;
		if (view.m_Label != null && view.m_Label.text != text)
		{
			SetLabel(view.m_Label, text);
		}
		if (view.m_Button != null && view.m_Button.gameObject != null && view.m_Button.gameObject.GetComponent<Comps.UiButtonPressHook>() == null)
		{
			view.m_Button.gameObject.AddComponent<Comps.UiButtonPressHook>().m_CustomId = hookId;
		}
	}

	private static void SetLabel(UILabel label, string text)
	{
		label.mText = text;
		label.text = text;
		label.ProcessText();
	}

	public static void PauseInvite()
	{
		if (SteamConnect.CanUseSteam && MyMod.MyLobby != "")
		{
			SteamConnect.Main.MakeFriendListUI();
		}
	}

	public static void PauseReturnToMenu(Panel_PauseMenu panel)
	{
		if (CoopReturnToMenu.CanRequest)
		{
			CoopReturnToMenu.Request();
		}
		else if (panel != null)
		{
			// Outside co-op the game's own Quit already saves and returns to the main menu.
			panel.OnQuitGame();
		}
	}

	public static void CustomButtonClick(int _ID)
	{
		MelonLogger.Msg("New hook, clicked " + _ID);
	}

	public static void AddButtonPause(Panel_PauseMenu __instance, string name, int order, int plus)
	{
		Panel_PauseMenu.PauseMenuItemType item = (Panel_PauseMenu.PauseMenuItemType)(30 + plus);
		__instance.m_MenuItems.Insert(order, item);
		Action action = (plus == ReturnToMenuPlus) ? ((Action)delegate
		{
			PauseReturnToMenu(__instance);
		}) : ((plus == InvitePlus) ? new Action(PauseInvite) : ((Action)delegate
		{
			CustomButtonClick(order);
		}));
		string id = __instance.m_MenuItems[order].ToString();
		int value = (int)__instance.m_MenuItems[order];
		__instance.m_BasicMenu.AddItem(id, value, order, name, "", "", action, Color.green, Color.white);
	}

	public static bool SetSaveSlotInfo(int Seed, string name)
	{
		SaveSlotInfo saveSlotInfo = null;
		List<SaveSlotInfo> sortedSaveSlots = SaveGameSystem.GetSortedSaveSlots(Episode.One, SaveSlotType.SANDBOX);
		for (int i = 0; i < sortedSaveSlots.Count; i++)
		{
			SaveSlotInfo saveSlotInfo2 = sortedSaveSlots[i];
			if (Seed != 0)
			{
				if (saveSlotInfo2.m_UserDefinedName == Seed.ToString())
				{
					saveSlotInfo = saveSlotInfo2;
					break;
				}
			}
			else if (saveSlotInfo2.m_SaveSlotName == name)
			{
				saveSlotInfo = saveSlotInfo2;
				break;
			}
		}
		if (saveSlotInfo != null)
		{
			UtilsPanelChoose.RefreshDetails(saveSlotInfo, MyMod.LobbyDeitailsStrct, UtilsPanelChoose.DetailsType.Sandbox);
			return true;
		}
		return false;
	}

	public static int GetSaveSeed(int index)
	{
		SaveSlotInfo saveSlotInfo = SaveGameSlotHelper.GetSaveSlotInfo(SaveSlotType.SANDBOX, index);
		if (saveSlotInfo == null)
		{
			MelonLogger.Msg(ConsoleColor.Red, "Can't load save on step 0");
			return 0;
		}
		string text = SaveSlotCompat.LoadDataFromSlot(saveSlotInfo.m_SaveSlotName, "global");
		if (string.IsNullOrEmpty(text))
		{
			MelonLogger.Msg(ConsoleColor.Red, "Can't load save on step 1");
			return 0;
		}
		GlobalSaveGameFormat globalSaveGameFormat = Utils.DeserializeObject<GlobalSaveGameFormat>(text);
		if (string.IsNullOrEmpty(globalSaveGameFormat.m_GameManagerSerialized))
		{
			MelonLogger.Msg(ConsoleColor.Red, "Can't load save on step 2");
			return 0;
		}
		GameManagerSaveDataProxy gameManagerSaveDataProxy = Utils.DeserializeObject<GameManagerSaveDataProxy>(globalSaveGameFormat.m_GameManagerSerialized);
		if (gameManagerSaveDataProxy != null)
		{
			if (!string.IsNullOrEmpty(gameManagerSaveDataProxy.m_SceneTransitionDataSerialized))
			{
				return Utils.DeserializeObject<SceneTransitionData>(gameManagerSaveDataProxy.m_SceneTransitionDataSerialized).m_GameRandomSeed;
			}
			MelonLogger.Msg(ConsoleColor.Red, "Can't load save on step 4 " + gameManagerSaveDataProxy.m_SceneTransitionDataSerialized);
			return 0;
		}
		MelonLogger.Msg(ConsoleColor.Red, "Can't load save on step 3 " + gameManagerSaveDataProxy.m_SceneTransitionDataSerialized);
		return 0;
	}

	public static void ConnectByIp()
	{
		bool flag = false;
		bool flag2 = false;
		if (false)
		{
			MyMod.DoWaitForConnect(Steam: true);
			if (flag2)
			{
				MyMod.DoKickMessage("Wrong mod version! Server using version 0.15.2");
			}
		}
		else if (flag)
		{
			DataStr.SaveSlotSync obj = new DataStr.SaveSlotSync
			{
				m_SaveSlotType = 3,
				m_Episode = 0,
				m_ExperienceMode = 10,
				m_Location = 0,
				m_Seed = -1294300353,
				m_CustomExperienceStr = "gsHMbj8PKxsjmaGO98IB",
				m_FixedSpawnScene = ""
			};
			MyMod.PendingSave = obj;
			MyMod.InterloperHook = true;
			MyMod.CheckHaveSaveFileToJoin(obj);
		}
		else
		{
			GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.Rename, "Input server address", MPSaveManager.GetLastConnectedServer(), Panel_Confirmation.ButtonLayout.Button_2, "Connect", "GAMEPLAY_Cancel", Panel_Confirmation.Background.Transperent, null, null);
		}
	}

	public static bool CanStartServer()
	{
		if (MyMod.LobbyState == "SelectedSave" || MyMod.LobbyState == "SelectedNewSave")
		{
			return true;
		}
		return false;
	}

	public static void SetUILableText(UILabel lab, string Txt)
	{
		lab.mText = Txt;
		lab.text = Txt;
		lab.ProcessText();
		if (lab.gameObject.GetComponent<UILocalize>() != null)
		{
			UnityEngine.Object.Destroy(lab.gameObject.GetComponent<UILocalize>());
		}
	}

	public static void SetNewGameInfo(int Region, int ExpMode)
	{
		Transform child = MyMod.LobbyNewGame.transform.GetChild(1);
		SetUILableText(MyMod.LobbyNewGame.transform.GetChild(0).GetChild(0).gameObject.GetComponent<UILabel>(), Localization.Get("GAMEPLAY_NewGame"));
		SetUILableText(child.GetChild(2).GetChild(0).gameObject.GetComponent<UILabel>(), "INFO");
		SetUILableText(child.GetChild(2).GetChild(1).gameObject.GetComponent<UILabel>(), "NEW SAVE WILL BE CREATED");
		SetUILableText(child.GetChild(3).GetChild(0).gameObject.GetComponent<UILabel>(), Localization.Get("GAMEPLAY_XPMode"));
		SetUILableText(child.GetChild(3).GetChild(1).gameObject.GetComponent<UILabel>(), ExperienceCompat.GetLocalizedName((ExperienceModeType)ExpMode));
		SetUILableText(child.GetChild(4).GetChild(0).gameObject.GetComponent<UILabel>(), Localization.Get("GAMEPLAY_Region"));
		if (Region == 6)
		{
			if (MyMod.ServerConfig.m_PlayersSpawnType != 1)
			{
				SetUILableText(child.GetChild(4).GetChild(1).gameObject.GetComponent<UILabel>(), Localization.Get("GAMEPLAY_RandomRegion"));
			}
			else
			{
				SetUILableText(child.GetChild(4).GetChild(1).gameObject.GetComponent<UILabel>(), Localization.Get("GAMEPLAY_Custom"));
			}
		}
		else
		{
			SetUILableText(child.GetChild(4).GetChild(1).gameObject.GetComponent<UILabel>(), RegionCompat.GetLocalizedName((Shared.GameRegion)Region));
		}
		child.GetChild(5).gameObject.SetActive(value: false);
		child.GetChild(6).gameObject.SetActive(value: false);
		child.GetChild(7).gameObject.SetActive(value: false);
		child.GetChild(8).gameObject.SetActive(value: false);
		child.GetChild(9).gameObject.SetActive(value: false);
		GameRegion gameRegion = (GameRegion)Region;
		string regionSaveSlotTextureName = UtilsPanelChoose.GetRegionSaveSlotTextureName(gameRegion.ToString());
		string regionLargeTextureName = UtilsPanelChoose.GetRegionLargeTextureName(gameRegion.ToString());
		MyMod.LobbyNewGame.transform.GetChild(2).gameObject.GetComponent<UISprite>().spriteName = regionSaveSlotTextureName;
		MyMod.LobbyNewGame.transform.GetChild(3).gameObject.GetComponent<UITexture>().mainTexture = Utils.GetLargeTexture(regionLargeTextureName);
		Texture mainTexture = MyMod.BundleAsset<Texture>("NewGamePreview");
		child.GetChild(1).gameObject.GetComponent<UITexture>().mainTexture = mainTexture;
	}

	public static void OpenFlairsMenu()
	{
		Transform parent = GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, 0, 0, 5);
		GameCompat.SetChildActive(parent, 1, state: false);
		GameCompat.SetChildActive(parent, 2, state: false);
		GameCompat.SetChildActive(parent, 4, state: false);
		GameCompat.SetChildActive(parent, 5, state: false);
		GameCompat.SetChildActive(GameCompat.ResolveChildPath(MyMod.m_Panel_Sandbox, default(int), default(int)), 3, state: false);
		MenuMode = "Customize";
		MyMod.CustomizeUiPanel("Main");
		SetFlairsCamera();
		if ((bool)MyMod.MyPlayerDoll)
		{
			MyMod.MyPlayerDoll.transform.position = new Vector3(-9f, 17.2f, 10.8f);
			MyMod.MyPlayerDoll.transform.rotation = new Quaternion(0f, 0.866f, 0f, -0.5f);
		}
		else
		{
			MelonLogger.Msg(ConsoleColor.Red, "MyPlayerDoll does not exist");
		}
	}

	public static void ChangeMenuItems(string mode)
	{
		BasicMenu basicMenu = ((MyMod.m_Panel_Sandbox == null) ? null : MyMod.m_Panel_Sandbox.m_BasicMenu);
		if (basicMenu == null)
		{
			return;
		}
		if (mode == "Original")
		{
			for (int r = 0; r < basicMenu.m_MenuItems.Count; r++)
			{
				ReturnOriginalButtons(basicMenu, r);
			}
			MenuMode = mode;
			return;
		}
		ClearMenuButtons(basicMenu);
		MenuMode = mode;
		optionsRow = FindOptionsRow(basicMenu);
		switch (mode)
		{
		case "Multiplayer":
			OverrideMenuButton(basicMenu, 1, "HOST SERVER");
			OverrideMenuButton(basicMenu, 2, "JOIN SERVER");
			OverrideMenuButton(basicMenu, 3, "PROFILE");
			if (optionsRow >= 0)
			{
				OverrideMenuButton(basicMenu, optionsRow, "GAME SETTINGS");
			}
			break;
		case "Join":
			OverrideMenuButton(basicMenu, 1, "BROWSE SERVERS");
			OverrideMenuButton(basicMenu, 2, "CONNECT BY IP");
			OverrideMenuButton(basicMenu, 3, "CONNECT TO FRIEND");
			break;
		case "MultiProfileSettings":
			OverrideMenuButton(basicMenu, 1, "CHANGE NICKNAME");
			OverrideMenuButton(basicMenu, 2, "CUSTOMIZATION");
			OverrideMenuButton(basicMenu, 3, "COPY ACCOUNT ID");
			break;
		case "Lobby":
		{
			if (SteamConnect.IsMyLobby)
			{
				if (CanStartServer())
				{
					OverrideMenuButton(basicMenu, 0, "START SERVER");
				}
				OverrideMenuButton(basicMenu, 1, "SELECT SAVE");
			}
			OverrideMenuButton(basicMenu, 2, "INVITE FRIEND");
			OverrideMenuButton(basicMenu, 3, "COPY INVITE LINK");
			if (optionsRow >= 0)
			{
				OverrideMenuButton(basicMenu, optionsRow, "GAME SETTINGS");
			}
			if (MyMod.LobbyUI != null)
			{
				MyMod.LobbyUI.SetActive(value: true);
			}
			if (MyMod.LobbyDeitails == null)
			{
				MelonLogger.Msg("[UI] Trying to copy details panel...");
				if ((bool)MyMod.m_Panel_ChooseSandbox.m_DetailObjects.m_Details)
				{
					GameObject lobbyDeitails = UnityEngine.Object.Instantiate(MyMod.m_Panel_ChooseSandbox.m_DetailObjects.m_Details, MyMod.m_Panel_Sandbox.transform);
					MelonLogger.Msg("[UI] Details copied!");
					MyMod.LobbyDeitails = lobbyDeitails;
					MyMod.LobbyDeitailsStrct = new UtilsPanelChoose.DetailsObjets();
					MyMod.LobbyNewGame = UnityEngine.Object.Instantiate(MyMod.m_Panel_ChooseSandbox.m_DetailObjects.m_Details, MyMod.m_Panel_Sandbox.transform);
					MyMod.LobbyNewGame.SetActive(value: false);
					UtilsPanelChoose.DetailsObjets lobbyDeitailsStrct = MyMod.LobbyDeitailsStrct;
					GameObject lobbyDeitails2 = MyMod.LobbyDeitails;
					Transform child = lobbyDeitails2.transform.GetChild(1);
					lobbyDeitailsStrct.m_TitleLabel = lobbyDeitails2.transform.GetChild(0).GetChild(0).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_ThumbnailTexture = child.GetChild(1).gameObject.GetComponent<UITexture>();
					lobbyDeitailsStrct.m_BackgroundTexture = lobbyDeitails2.transform.GetChild(3).gameObject.GetComponent<UITexture>();
					lobbyDeitailsStrct.m_DateLabel = child.GetChild(2).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_SurvivorLabel = child.GetChild(3).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_ConditionLabel = child.GetChild(4).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_XPLabel = child.GetChild(5).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_SurvivedLabel = child.GetChild(6).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_ExploredLabel = child.GetChild(7).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_RegionLabel = child.GetChild(8).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_LocationLabel = child.GetChild(9).GetChild(1).gameObject.GetComponent<UILabel>();
					lobbyDeitailsStrct.m_RegionSprite = lobbyDeitails2.transform.GetChild(2).gameObject.GetComponent<UISprite>();
					lobbyDeitailsStrct.m_CenteredTexture = null;
					for (int i = 0; i < MyMod.LobbyDeitails.transform.GetChild(4).GetChild(1).childCount; i++)
					{
						GameObject gameObject = MyMod.LobbyDeitails.transform.GetChild(4).GetChild(1).GetChild(i)
							.gameObject.transform.GetChild(1).gameObject;
						if ((bool)gameObject.GetComponent<AnimatedAlpha>())
						{
							UnityEngine.Object.Destroy(gameObject.GetComponent<AnimatedAlpha>());
						}
						if ((bool)gameObject.GetComponent<UITexture>())
						{
							gameObject.GetComponent<UITexture>().alpha = 0.4f;
						}
					}
					for (int j = 0; j < MyMod.LobbyNewGame.transform.GetChild(4).GetChild(1).childCount; j++)
					{
						GameObject gameObject2 = MyMod.LobbyNewGame.transform.GetChild(4).GetChild(1).GetChild(j)
							.gameObject.transform.GetChild(1).gameObject;
						if ((bool)gameObject2.GetComponent<AnimatedAlpha>())
						{
							UnityEngine.Object.Destroy(gameObject2.GetComponent<AnimatedAlpha>());
						}
						if ((bool)gameObject2.GetComponent<UITexture>())
						{
							gameObject2.GetComponent<UITexture>().alpha = 0.4f;
						}
					}
				}
			}
			bool flag = false;
			bool flag2 = false;
			if (!MyMod.LobbyDeitails || !MyMod.LobbyNewGame)
			{
				break;
			}
			if (MyMod.LobbyState == "SelectedSave")
			{
				bool flag3 = false;
				if ((!SteamConnect.IsMyLobby) ? SetSaveSlotInfo(MyMod.SelectedSaveSeed, "") : SetSaveSlotInfo(0, MyMod.SelectedSaveName))
				{
					flag = true;
				}
				else
				{
					SetNewGameInfo(MyMod.LobbyStartingRegion, MyMod.LobbyStartingExperience);
					flag2 = true;
				}
			}
			else if (MyMod.LobbyState == "SelectedNewSave")
			{
				SetNewGameInfo(MyMod.LobbyStartingRegion, MyMod.LobbyStartingExperience);
				flag2 = true;
			}
			MyMod.LobbyDeitails.SetActive(flag);
			MyMod.LobbyNewGame.SetActive(flag2);
			GameObject gameObject3 = null;
			if (flag)
			{
				gameObject3 = MyMod.LobbyDeitails;
			}
			else if (flag2)
			{
				gameObject3 = MyMod.LobbyNewGame;
			}
			if ((bool)gameObject3)
			{
				ExperienceModeType lobbyStartingExperience = (ExperienceModeType)MyMod.LobbyStartingExperience;
				int num = 0;
				switch (lobbyStartingExperience)
				{
				case ExperienceModeType.Pilgrim:
					num = 0;
					break;
				case ExperienceModeType.Voyageur:
					num = 1;
					break;
				case ExperienceModeType.Stalker:
					num = 2;
					break;
				case ExperienceModeType.Interloper:
					num = 3;
					break;
				}
				for (int k = 0; k < gameObject3.transform.GetChild(4).GetChild(1).childCount; k++)
				{
					gameObject3.transform.GetChild(4).GetChild(1).GetChild(k)
						.gameObject.SetActive(k == num);
				}
			}
			break;
		}
		case "LobbySettings":
			OverrideMenuButton(basicMenu, 1, "NEW GAME");
			if (SaveGameSlotHelper.GetNumSaveSlots(SaveSlotType.SANDBOX) > 0)
			{
				OverrideMenuButton(basicMenu, 2, "LOAD SAVE");
				OverrideMenuButton(basicMenu, 3, "FEATS");
			}
			break;
		}
	}

	// The survival menu's own Options row, or its last spare row; rows 0-3 are used by the multiplayer buttons.
	private static int FindOptionsRow(BasicMenu menu)
	{
		if (menu == null || menu.m_ItemModelList == null || menu.m_MenuItems == null)
		{
			return -1;
		}
		int count = Math.Min(menu.m_ItemModelList.Count, menu.m_MenuItems.Count);
		for (int i = 4; i < count; i++)
		{
			BasicMenu.BasicMenuItemModel model = menu.m_ItemModelList[i];
			if (model != null && model.m_Id != null && model.m_Id.IndexOf("Option", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return i;
			}
		}
		return (count > 4) ? (count - 1) : (-1);
	}

	private static void OpenGameOptions()
	{
		if (MyMod.m_Panel_Sandbox == null)
		{
			return;
		}
		MelonLogger.Msg("[Menu] Opening game options from " + MenuMode);
		CloseLobbyUI();
		MyMod.m_Panel_Sandbox.OnClickOptions();
	}

	public static void CloseLobbyUI()
	{
		if (MyMod.LobbyUI != null)
		{
			MyMod.LobbyUI.SetActive(value: false);
		}
	}

	public static int GetRNGGen(string name)
	{
		int result = 0;
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_genversion");
		if (text != null)
		{
			result = JSON.Load(text).Make<int[]>()[0];
		}
		return result;
	}

	public static bool CheckSaveRNG(string name)
	{
		int num = 0;
		string text = SaveSlotCompat.LoadDataFromSlot(name, "skycoop_genversion");
		if (text != null)
		{
			num = JSON.Load(text).Make<int[]>()[0];
			if (num != 5)
			{
				MelonLogger.Msg(ConsoleColor.DarkRed, "This save file can't be use for multiplayer, because we created on old version of the mod, with Generation version " + num + ". Release of mod you using right now has Generation version " + 5);
			}
		}
		else
		{
			MelonLogger.Msg(ConsoleColor.DarkRed, "This save file can't be use for multiplayer, because was created on old version of mod or without mod at all.");
		}
		if (num != 5)
		{
			string messageLocID = ((num != 0) ? ("You can't use this save file for hosting multiplayer! Because this save file has been created on old version of the mod that isn't compatible with current one! Save file Generation version " + num + ". Current one mod use now " + 5 + "!") : "You can't use this save file for hosting multiplayer! Because this save file has been created before mod has been installed, or on old version of the mod that isn't compatible with current one!");
			if (MyMod.m_InterfaceManager != null && GameCompat.Panel<Panel_Confirmation>() != null)
			{
				GameCompat.Panel<Panel_Confirmation>().AddConfirmation(Panel_Confirmation.ConfirmationType.ErrorMessage, messageLocID, Panel_Confirmation.ButtonLayout.Button_1, Panel_Confirmation.Background.Transperent, null);
			}
			return false;
		}
		return true;
	}
}
