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

	private static List<string> allGear;

	private static Dictionary<string, List<string>> categories;

	private static readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();

	private static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

	private static int preloadIndex;

	private static bool iconsBroken;
	private static List<string> filteredCache;
	private static string filteredSearch,filteredCategory;
	private static int filteredNames=-1,filteredCatalog=-1;
	private static bool catalogWorld;

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
			if (Time.frameCount != openedFrame && KeyboardUtilities.InputManager.GetTogglePressed(ToggleKey))
			{
				Close();
				return;
			}
			OverlayInput.Keep();
			PreloadNames();
			return;
		}
		if (!CheatConsole.IsOpen && KeyboardUtilities.InputManager.GetTogglePressed(ToggleKey))
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
        category="";search="";page=0;
		open = true;
		openedFrame = Time.frameCount;
		status = (allGear.Count == 0) ? "Каталог предметов пуст — зайдите в мир и откройте снова" : "Клик по предмету — взять. Esc или F2 — закрыть";
		OverlayInput.Acquire();
		MelonLogger.Msg("[Cheats] Item window opened, " + allGear.Count + " items");
	}

	private static void Close()
	{
		if (open)
		{
			open = false;
			icons.Clear();
			OverlayInput.Release();
		}
	}

	private static void EnsureCatalog()
	{
		bool world=GearCatalogIndex.InWorld;
		if(allGear!=null&&allGear.Count>0&&(catalogWorld||!world))return;
		GearCatalogIndex.Build(out allGear,out categories);preloadIndex=0;filteredCache=null;
		catalogWorld=world;
	}
	internal static string[] AuditCatalog()
	{
		EnsureCatalog();return categories.SelectMany(c=>c.Value.Select(g=>c.Key+"|"+g)).ToArray();
	}

	private const string CommandsTab = "#commands";

	private static void DrawCommands(float x0, float top, float width)
	{
		if(!GearCatalogIndex.InWorld){GUI.Label(new Rect(x0+10,top,width-20,40),"Команды доступны после входа в мир");return;}
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
		Button("Выбрать персонажа (F4)", delegate { Close(); NativeAppearance.Open(); });
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
            "Materials" => "Материалы", "Camping" => "Лагерь", "Fishing" => "Рыбалка", "Decorations" => "Декор", "Quest" => "Квестовые", "Other" => "Прочее",
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

	// Localization lookup only. Loading every gear prefab here retained models,
	// materials and textures for the whole catalog merely to obtain its label.
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
			string key="GAMEPLAY_"+name;
			if (Localization.Exists(key))
			{
				name = Localization.Get(key);
			}
		}
		catch
		{
		}
		displayNames[gear] = name;
	}

	private static Texture2D IconOf(string gear)
	{
		if(!GearCatalogIndex.InWorld)return null;
		if (icons.TryGetValue(gear, out Texture2D texture))
		{
			return texture;
		}
		if(icons.Count>=128)icons.Clear();
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
		if(filteredCache!=null&&filteredSearch==search&&filteredCategory==category&&filteredNames==displayNames.Count&&filteredCatalog==allGear.Count)return filteredCache;
		filteredSearch=search;filteredCategory=category;filteredNames=displayNames.Count;filteredCatalog=allGear.Count;
		IEnumerable<string> source = (category != "" && categories.TryGetValue(category, out List<string> members)) ? members : allGear;
		if (search.Length == 0)
		{
			return filteredCache=source.ToList();
		}
		return filteredCache=source.Where(g=>g.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0||NameOf(g).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).ToList();
	}

	public static void Draw()
	{
		if (!open)
		{
			return;
		}
		OverlayInput.Keep();
		Event current = Event.current;
		if (current != null && current.type == EventType.KeyDown)
		{
			if (current.keyCode == KeyCode.Escape || (current.keyCode == ToggleKey && Time.frameCount != openedFrame))
			{
				current.Use();
				KeyboardUtilities.InputManager.MarkToggled(ToggleKey);
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

		List<string> keys = new List<string> { "" };
		keys.AddRange(GearCatalogPolicy.Sections);keys.Add(CommandsTab);
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
