using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Developer console on F1 for testing, allowed by the server "cheats" setting
// (m_CheatsMode: 0 nobody, 1 host only, 2 everyone; always allowed outside co-op).
// Release 2.55 ships without the uConsole object/prefab, but the command registry and the game's
// commands (gear_add, god, fly, ...) still exist, so this draws its own IMGUI window and runs them
// through uConsole.RunCommand. Adds "give <GEAR_name> [count]" for inventory items.
public static class CheatConsole
{
	[HarmonyPatch(typeof(uConsole), nameof(uConsole.Log))]
	private static class MirrorConsoleLog
	{
		private static bool Prefix(string text)
		{
			Print(text);
			// Without the native console object its Log has nowhere to write.
			return uConsole.m_Instance != null;
		}
	}

	public const KeyCode ToggleKey = KeyCode.F1;

	private const int MaxLines = 14;

	private static readonly string[] CommonGear = new string[]
	{
		"GEAR_Rifle", "GEAR_Revolver", "GEAR_FlareA", "GEAR_Flashlight", "GEAR_Hatchet", "GEAR_HatchetImprovised",
		"GEAR_FireAxe", "GEAR_KnifeImprovised", "GEAR_WoodworkingTools", "GEAR_TechnicalBackpack", "GEAR_Crampons",
		"GEAR_MedicalSupplies_hangar", "GEAR_Softwood", "GEAR_ReclaimedWoodB", "GEAR_Stick", "GEAR_Stone",
		"GEAR_Cloth", "GEAR_CannedSardines", "GEAR_GranolaBar", "GEAR_Soda", "GEAR_BasicWoolHat", "GEAR_BasicWoolScarf"
	};

	private static readonly List<string> Lines = new List<string>();

	private static readonly List<string> History = new List<string>();

	private static bool open;

	private static int openedFrame;

	private static string input = "";

	private static int historyIndex;

	private static float savedInputDisableTime;

	public static bool IsOpen => open;

	public static bool Allowed
	{
		get
		{
			if (!MyMod.InOnline())
			{
				return true;
			}
			if (MyMod.ServerConfig.m_CheatsMode == 2)
			{
				return true;
			}
			if (MyMod.ServerConfig.m_CheatsMode == 1)
			{
				return MyMod.iAmHost;
			}
			return false;
		}
	}

	public static void RegisterCommands()
	{
		uConsole.RegisterCommand("give", (Action)Give);
	}

	public static void Tick()
	{
		if (MyMod.DedicatedServerAppMode)
		{
			return;
		}
		if (open)
		{
			if (!Allowed)
			{
				Close();
				Hud("Читы выключены настройками сервера");
			}
			return;
		}
		if (!KeyboardUtilities.InputManager.GetKeyDown(ToggleKey))
		{
			return;
		}
		if (!Allowed)
		{
			Hud("Читы выключены настройками сервера");
			return;
		}
		Open();
	}

	private static void Open()
	{
		try
		{
			if (!uConsole.CommandIsRegistered("gear_add"))
			{
				ConsoleManager.RegisterCommands();
			}
			if (!uConsole.CommandIsRegistered("give"))
			{
				RegisterCommands();
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Cheats] Registering commands failed: " + ex.Message);
		}
		open = true;
		openedFrame = Time.frameCount;
		input = "";
		historyIndex = History.Count;
		savedInputDisableTime = InputManager.m_InputDisableTime;
		InputManager.m_InputDisableTime = float.PositiveInfinity;
		if (Lines.Count == 0)
		{
			Print("Консоль: help — список, give <предмет> [кол-во], Enter — выполнить, Esc/F1 — закрыть");
		}
		MelonLogger.Msg("[Cheats] Console opened");
	}

	private static void Close()
	{
		open = false;
		InputManager.m_InputDisableTime = savedInputDisableTime;
	}

	public static void Draw()
	{
		if (!open)
		{
			return;
		}
		Event current = Event.current;
		if (current != null && current.type == EventType.KeyDown)
		{
			KeyCode keyCode = current.keyCode;
			if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter)
			{
				string command = input.Trim();
				input = "";
				current.Use();
				if (command.Length > 0)
				{
					Execute(command);
				}
			}
			else if (keyCode == KeyCode.Escape || (keyCode == ToggleKey && Time.frameCount != openedFrame))
			{
				current.Use();
				Close();
				return;
			}
			else if (keyCode == KeyCode.UpArrow && History.Count > 0)
			{
				historyIndex = Math.Max(0, historyIndex - 1);
				input = History[historyIndex];
				current.Use();
			}
			else if (keyCode == KeyCode.DownArrow && History.Count > 0)
			{
				historyIndex = Math.Min(History.Count, historyIndex + 1);
				input = (historyIndex < History.Count) ? History[historyIndex] : "";
				current.Use();
			}
			else if (keyCode == KeyCode.Backspace)
			{
				if (input.Length > 0)
				{
					input = input.Substring(0, input.Length - 1);
				}
				current.Use();
			}
			else if (current.character != '\0' && !char.IsControl(current.character) && Time.frameCount != openedFrame)
			{
				// GUI.TextField / SetNextControlName are broken in this Il2Cpp interop, so typing is read from key events.
				input += current.character;
				current.Use();
			}
		}
		float width = Mathf.Min(Screen.width - 40f, 1100f);
		float lineHeight = 22f;
		float height = lineHeight * (MaxLines + 2) + 10f;
		GUI.Box(new Rect(20f, 20f, width, height), "");
		for (int i = 0; i < Lines.Count; i++)
		{
			GUI.Label(new Rect(30f, 25f + lineHeight * i, width - 20f, lineHeight), Lines[i]);
		}
		string cursor = ((int)(Time.realtimeSinceStartup * 2f) % 2 == 0) ? "_" : " ";
		GUI.Label(new Rect(30f, 25f + lineHeight * MaxLines + 5f, width - 20f, lineHeight + 4f), "> " + input + cursor);
	}

	private static void Execute(string command)
	{
		History.Add(command);
		historyIndex = History.Count;
		Print("> " + command);
		if (command == "help")
		{
			Print("give <GEAR_имя> [кол-во] — предмет в инвентарь (give без параметров — примеры)");
			Print("gear_add <GEAR_имя> [кол-во], add_all_gear, god, fly, set_time <часы>, map_reveal,");
			Print("afflictions_cure, spawn_wolf, spawn_bear, skip <часы> (мод)");
			return;
		}
		if (!Allowed)
		{
			Print("Читы выключены настройками сервера");
			return;
		}
		try
		{
			Il2CppSystem.Object result = uConsole.RunCommand(command);
			if (result != null)
			{
				Print(result.ToString());
			}
			MelonLogger.Msg("[Cheats] " + command);
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Cheats] RunCommand failed, invoking directly: " + ex.Message);
			InvokeDirect(command);
		}
	}

	// Fallback when RunCommand needs the missing console object: parse arguments and call the registered delegate.
	private static void InvokeDirect(string command)
	{
		try
		{
			string[] tokens = command.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (!uConsole.m_CommandsDict.ContainsKey(tokens[0]))
			{
				Print("Нет команды " + tokens[0] + " (help — список)");
				return;
			}
			uConsole.CommandDelegate commandDelegate = uConsole.m_CommandsDict[tokens[0]];
			uConsole.BuildArgListFromTokens(tokens);
			if (commandDelegate.m_Command != null)
			{
				commandDelegate.m_Command.Invoke();
			}
			else if (commandDelegate.m_CommandReturn != null)
			{
				Il2CppSystem.Object result = commandDelegate.m_CommandReturn.Invoke();
				if (result != null)
				{
					Print(result.ToString());
				}
			}
			MelonLogger.Msg("[Cheats] " + command + " (direct)");
		}
		catch (Exception ex)
		{
			Print("Ошибка: " + ex.Message);
			MelonLogger.Warning("[Cheats] " + command + " failed: " + ex);
		}
	}

	private static void Print(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		foreach (string line in text.Split('\n'))
		{
			Lines.Add(line);
		}
		while (Lines.Count > MaxLines)
		{
			Lines.RemoveAt(0);
		}
	}

	private static void Give()
	{
		if (!Allowed)
		{
			Print("Читы выключены настройками сервера");
			return;
		}
		if (uConsole.GetNumParameters() == 0)
		{
			Print("give <GEAR_имя> [количество]   например: give GEAR_Rifle, give Softwood 10");
			Print("Имена с учётом регистра. Частые: " + string.Join(", ", CommonGear));
			return;
		}
		string name = uConsole.GetString();
		int count = 1;
		if (uConsole.NextParameterIsInt())
		{
			count = Mathf.Clamp(uConsole.GetInt(), 1, 100);
		}
		name = name.StartsWith("GEAR_", StringComparison.OrdinalIgnoreCase) ? ("GEAR_" + name.Substring(5)) : ("GEAR_" + name);
		PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
		if (playerManager == null || MyMod.level_name == "Empty" || Shared.IsMainMenuScene(MyMod.level_name))
		{
			Print("Сначала зайдите в мир");
			return;
		}
		if (MyMod.GetGearItemObject(name) == null)
		{
			Print("Нет предмета " + name + " (имя с учётом регистра, give без параметров покажет примеры)");
			return;
		}
		GearItem gearItem = playerManager.InstantiateItemInPlayerInventory(name, count);
		Print((gearItem != null) ? ("Выдано: " + name + " x" + count) : ("Не удалось выдать " + name));
		MelonLogger.Msg("[Cheats] give " + name + " x" + count + " -> " + (gearItem != null));
	}

	private static void Hud(string text)
	{
		try
		{
			HUDMessage.AddMessage(text);
		}
		catch
		{
		}
		MelonLogger.Msg("[Cheats] " + text);
	}
}
