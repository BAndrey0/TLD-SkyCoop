using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// Visual item picker on F2 for testing: a grid of every gear item with icon and localized name,
// search, type tabs and amount, click to put the item into the inventory. Same permission as the
// cheat console. The catalog comes from the game's console autocomplete tables.
public static class ItemSpawner
{
	public const KeyCode ToggleKey = KeyCode.F2;

	private const int Columns = 8;

	private const int Rows = 4;

	private const float Cell = 104f;

	private const int LoadsPerFrame = 6;

	private static readonly int[] Amounts = new int[3] { 1, 5, 10 };

	private static bool open;

	private static int openedFrame;

	private static string search = "";

	private static string category = "";

	private static int page;

	private static int amount = 1;

	private static string status = "";

	private static float savedInputDisableTime;

	private static List<string> allGear;

	private static Dictionary<string, List<string>> categories;

	private static readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

	private static int preloadIndex;

	private static bool iconsBroken;

	public static bool IsOpen => open;

	public static void Tick()
	{
		if (MyMod.DedicatedServerAppMode)
		{
			return;
		}
		if (open)
		{
			if (!CheatConsole.Allowed)
			{
				Close();
				return;
			}
			if (Time.frameCount != openedFrame && KeyboardUtilities.InputManager.GetKeyDown(ToggleKey))
			{
				// The IMGUI KeyDown event for F2 does not arrive reliably; close through the same input that opens it.
				Close();
				return;
			}
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
			PreloadNames();
			return;
		}
		if (!CheatConsole.IsOpen && KeyboardUtilities.InputManager.GetKeyDown(ToggleKey))
		{
			if (!CheatConsole.Allowed)
			{
				HUDMessage.AddMessage("Читы выключены настройками сервера");
				return;
			}
			Open();
		}
	}

	private static void Open()
	{
		EnsureCatalog();
		open = true;
		openedFrame = Time.frameCount;
		status = (allGear.Count == 0) ? "Каталог предметов пуст — зайдите в мир и откройте снова" : "Клик по предмету — взять. Esc или F2 — закрыть";
		savedInputDisableTime = InputManager.m_InputDisableTime;
		InputManager.m_InputDisableTime = float.PositiveInfinity;
		MelonLogger.Msg("[Cheats] Item window opened, " + allGear.Count + " items");
	}

	private static void Close()
	{
		open = false;
		InputManager.m_InputDisableTime = savedInputDisableTime;
	}

	private static void EnsureCatalog()
	{
		if (allGear != null && allGear.Count > 0)
		{
			return;
		}
		allGear = new List<string>();
		categories = new Dictionary<string, List<string>>();
		try
		{
			if (ConsoleManager.m_SearchStringToGearNames == null || ConsoleManager.m_SearchStringToGearNames.Count == 0)
			{
				ConsoleManager.StoreAutoCompleteGearNames();
			}
			HashSet<string> seen = new HashSet<string>();
			Il2CppSystem.Collections.Generic.Dictionary<string, string>.Enumerator names = ConsoleManager.m_SearchStringToGearNames.GetEnumerator();
			while (names.MoveNext())
			{
				string gear = names.Current.Value;
				if (!string.IsNullOrEmpty(gear) && gear.StartsWith("GEAR_", StringComparison.OrdinalIgnoreCase) && seen.Add(gear))
				{
					allGear.Add(gear);
				}
			}
			allGear.Sort(StringComparer.OrdinalIgnoreCase);
			Il2CppSystem.Collections.Generic.Dictionary<string, Il2CppSystem.Collections.Generic.List<string>>.Enumerator groups = ConsoleManager.m_ComponentNameToGearNames.GetEnumerator();
			while (groups.MoveNext())
			{
				List<string> members = new List<string>();
				Il2CppSystem.Collections.Generic.List<string> source = groups.Current.Value;
				for (int i = 0; i < source.Count; i++)
				{
					if (seen.Contains(source[i]))
					{
						members.Add(source[i]);
					}
				}
				if (members.Count > 0)
				{
					categories[groups.Current.Key] = members;
				}
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Cheats] Item catalog failed: " + ex.Message);
		}
	}

	private const string CommandsTab = "#commands";

	private static void DrawCommands(float x0, float top, float width)
	{
		PlayerManager playerManager = GameManager.GetPlayerManagerComponent();
		bool god = playerManager != null && playerManager.m_God;
		float buttonWidth = (width - 40f) / 3f;
		float buttonHeight = 34f;
		int index = 0;
		void Button(string title, Action action)
		{
			Rect rect = new Rect(x0 + 10f + (index % 3) * (buttonWidth + 10f), top + (index / 3) * (buttonHeight + 8f), buttonWidth, buttonHeight);
			index++;
			if (GUI.Button(rect, title))
			{
				try
				{
					action();
				}
				catch (Exception ex)
				{
					status = "Ошибка: " + ex.Message;
					MelonLogger.Warning("[Cheats] " + title + " failed: " + ex);
				}
			}
		}
		Button(god ? "Бессмертие: ВКЛ" : "Бессмертие: выкл", delegate
		{
			Run("god");
		});
		Button("Полёт (вкл/выкл)", delegate
		{
			Run("fly");
		});
		Button("Невидимость для животных (вкл/выкл)", delegate
		{
			Run("ghost");
		});
		Button("Восстановить здоровье, сытость, сон", RestoreStats);
		Button("Вылечить все болезни и травмы", delegate
		{
			Run("afflictions_cure");
		});
		Button("Открыть всю карту", delegate
		{
			Run("map_reveal");
		});
		Button("Время: утро (8:00)", delegate
		{
			SkipTo(8);
		});
		Button("Время: день (12:00)", delegate
		{
			SkipTo(12);
		});
		Button("Время: вечер (18:00)", delegate
		{
			SkipTo(18);
		});
		Button("Время: ночь (0:00)", delegate
		{
			SkipTo(0);
		});
		Button("Пропустить 1 час", delegate
		{
			Skip(1);
		});
		Button("Открыть консоль (F1)", delegate
		{
			Close();
			CheatConsole.OpenFromWindow();
		});
	}

	private static void Run(string command)
	{
		CheatConsole.Execute(command);
		status = "Выполнено: " + command;
	}

	private static void RestoreStats()
	{
		if (GameManager.GetPlayerManagerComponent() == null || Shared.IsMainMenuScene(MyMod.level_name))
		{
			status = "Сначала зайдите в мир";
			return;
		}
		GameManager.GetConditionComponent().m_CurrentHP = GameManager.GetConditionComponent().GetAdjustedMaxHP();
		GameManager.GetHungerComponent().m_CurrentReserveCalories = GameManager.GetHungerComponent().m_MaxReserveCalories;
		GameManager.GetThirstComponent().m_CurrentThirst = 0f;
		GameManager.GetFatigueComponent().m_CurrentFatigue = 0f;
		GameManager.GetFreezingComponent().m_CurrentFreezing = 0f;
		status = "Здоровье, сытость, жажда, усталость и холод восстановлены";
		MelonLogger.Msg("[Cheats] restore stats");
	}

	// In co-op the world clock belongs to the host (SkyCoop overrides time of day every frame).
	private static void Skip(int hours)
	{
		if (!MyMod.InOnline())
		{
			CheatConsole.Execute("set_time " + ((GameManager.GetTimeOfDayComponent().GetHour() + hours) % 24));
			status = "Время сдвинуто на " + hours + " ч";
			return;
		}
		if (!MyMod.iAmHost)
		{
			status = "Время в коопе меняет только хост";
			return;
		}
		Shared.SkipRTTime(hours);
		status = "Время сдвинуто на " + hours + " ч";
		MelonLogger.Msg("[Cheats] skip " + hours + "h");
	}

	private static void SkipTo(int hour)
	{
		int current = MyMod.InOnline() ? MyMod.OverridedHourse : GameManager.GetTimeOfDayComponent().GetHour();
		int hours = (hour - current + 24) % 24;
		if (hours == 0)
		{
			status = "Уже " + hour + ":00";
			return;
		}
		Skip(hours);
	}

	private static string CategoryTitle(string key)
	{
		return key switch
		{
			CommandsTab => "Команды",
			"" => "Все",
			"FoodItem" => "Еда",
			"ClothingItem" => "Одежда",
			"GunItem" => "Оружие",
			"FirstAidItem" => "Аптечка",
			"ToolsItem" => "Инструменты",
			"FuelSourceItem" => "Топливо",
			"LiquidItem" => "Жидкости",
			"WaterSupply" => "Вода",
			"ResearchItem" => "Книги",
			"AmmoItem" => "Патроны",
			"FlareItem" => "Фальшфейеры",
			"TorchItem" => "Факелы",
			"MatchesItem" => "Спички",
			"KeroseneLampItem" => "Лампы",
			"Bed" => "Спальники",
			"SnareItem" => "Силки",
			"BowItem" => "Луки",
			"ArrowItem" => "Стрелы",
			"CookingPotItem" => "Посуда",
			"StackableItem" => "Стопки",
			_ => key.Replace("Item", ""),
		};
	}

	private static string NameOf(string gear)
	{
		if (displayNames.TryGetValue(gear, out string name))
		{
			return name;
		}
		return gear.Substring(5);
	}

	// Display names need the prefab; load a few per frame so opening the window does not freeze the game.
	private static void PreloadNames()
	{
		if (allGear == null)
		{
			return;
		}
		int loaded = 0;
		while (preloadIndex < allGear.Count && loaded < LoadsPerFrame)
		{
			LoadName(allGear[preloadIndex]);
			preloadIndex++;
			loaded++;
		}
	}

	private static void LoadName(string gear)
	{
		if (displayNames.ContainsKey(gear))
		{
			return;
		}
		string name = gear.Substring(5);
		try
		{
			GearItem prefab = GearItem.LoadGearItemPrefab(gear);
			if (prefab != null && !string.IsNullOrEmpty(prefab.DisplayName))
			{
				name = prefab.DisplayName;
			}
		}
		catch
		{
		}
		displayNames[gear] = name;
	}

	private static Texture2D IconOf(string gear)
	{
		if (icons.TryGetValue(gear, out Texture2D texture))
		{
			return texture;
		}
		try
		{
			texture = Utils.GetInventoryIconTextureFromPrefabName(gear);
		}
		catch
		{
			texture = null;
		}
		icons[gear] = texture;
		return texture;
	}

	private static List<string> Filtered()
	{
		IEnumerable<string> source = (category != "" && categories.TryGetValue(category, out List<string> members)) ? members : allGear;
		if (search.Length == 0)
		{
			return source.ToList();
		}
		string needle = search.ToLowerInvariant();
		return source.Where((string g) => g.ToLowerInvariant().Contains(needle) || NameOf(g).ToLowerInvariant().Contains(needle)).ToList();
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
			if (current.keyCode == KeyCode.Escape || (current.keyCode == ToggleKey && Time.frameCount != openedFrame))
			{
				current.Use();
				Close();
				return;
			}
			if (current.keyCode == KeyCode.Backspace)
			{
				if (search.Length > 0)
				{
					search = search.Substring(0, search.Length - 1);
					page = 0;
				}
				current.Use();
			}
			else if (current.character != '\0' && !char.IsControl(current.character) && Time.frameCount != openedFrame)
			{
				search += current.character;
				page = 0;
				current.Use();
			}
		}
		List<string> items = Filtered();
		int perPage = Columns * Rows;
		int pages = Math.Max(1, (items.Count + perPage - 1) / perPage);
		if (current != null && current.type == EventType.ScrollWheel)
		{
			page += (current.delta.y > 0f) ? 1 : -1;
			current.Use();
		}
		page = Mathf.Clamp(page, 0, pages - 1);

		float width = Columns * Cell + 20f;
		float x0 = Mathf.Max(10f, (Screen.width - width) / 2f);
		float y0 = 40f;
		float tabsHeight = 26f * (1 + (categories.Count + 1) / 10);
		float height = 70f + tabsHeight + Rows * Cell + 40f;
		GUI.Box(new Rect(x0, y0, width, height), "Предметы");

		string cursor = ((int)(Time.realtimeSinceStartup * 2f) % 2 == 0) ? "_" : " ";
		GUI.Label(new Rect(x0 + 10f, y0 + 24f, width - 260f, 24f), "Поиск: " + search + cursor + "   (" + items.Count + ")");
		for (int i = 0; i < Amounts.Length; i++)
		{
			string label = (Amounts[i] == amount) ? ("[x" + Amounts[i] + "]") : ("x" + Amounts[i]);
			if (GUI.Button(new Rect(x0 + width - 240f + i * 76f, y0 + 22f, 70f, 24f), label))
			{
				amount = Amounts[i];
			}
		}

		List<string> keys = new List<string> { CommandsTab, "" };
		keys.AddRange(categories.Keys.OrderBy((string k) => CategoryTitle(k)));
		float tabWidth = (width - 20f) / 10f;
		for (int i = 0; i < keys.Count; i++)
		{
			string title = CategoryTitle(keys[i]);
			if (keys[i] == category)
			{
				title = "[" + title + "]";
			}
			if (GUI.Button(new Rect(x0 + 10f + (i % 10) * tabWidth, y0 + 52f + (i / 10) * 26f, tabWidth - 4f, 24f), title))
			{
				category = keys[i];
				page = 0;
			}
		}

		float gridTop = y0 + 56f + tabsHeight;
		if (category == CommandsTab)
		{
			DrawCommands(x0, gridTop, width);
			GUI.Label(new Rect(x0 + 10f, gridTop + Rows * Cell + 7f, width - 20f, 24f), status);
			return;
		}
		int first = page * perPage;
		for (int i = 0; i < perPage && first + i < items.Count; i++)
		{
			string gear = items[first + i];
			LoadName(gear);
			Rect cell = new Rect(x0 + 10f + (i % Columns) * Cell, gridTop + (i / Columns) * Cell, Cell - 6f, Cell - 6f);
			if (GUI.Button(cell, ""))
			{
				status = CheatConsole.GiveItem(gear, amount);
			}
			Texture2D icon = iconsBroken ? null : IconOf(gear);
			if (icon != null)
			{
				try
				{
					GUI.DrawTexture(new Rect(cell.x + (cell.width - 64f) / 2f, cell.y + 4f, 64f, 64f), icon, ScaleMode.ScaleToFit);
				}
				catch (Exception ex)
				{
					iconsBroken = true;
					MelonLogger.Warning("[Cheats] Item icons disabled: " + ex.Message);
				}
			}
			GUI.Label(new Rect(cell.x + 4f, cell.y + 68f, cell.width - 8f, 32f), NameOf(gear));
		}

		float bottom = gridTop + Rows * Cell + 4f;
		if (GUI.Button(new Rect(x0 + 10f, bottom, 40f, 26f), "<"))
		{
			page = Math.Max(0, page - 1);
		}
		GUI.Label(new Rect(x0 + 56f, bottom + 3f, 120f, 24f), "стр. " + (page + 1) + "/" + pages);
		if (GUI.Button(new Rect(x0 + 176f, bottom, 40f, 26f), ">"))
		{
			page = Math.Min(pages - 1, page + 1);
		}
		GUI.Label(new Rect(x0 + 230f, bottom + 3f, width - 240f, 24f), status);
	}
}
