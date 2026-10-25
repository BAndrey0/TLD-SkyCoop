using System;
using HarmonyLib;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoop;

public static class SteamJoinUi
{
	[HarmonyPatch(typeof(MyMod), "UpdateCanvasUis")]
	private static class UpdateCanvasPostfix
	{
		private static void Postfix()
		{
			Update();
		}
	}

	private static GameObject panel;

	private static Button joinButton;

	private static Text status;

	private static string trackedLobby;

	private static bool actualGuest;

	private static bool hostAdvertised;

	private static float nextLobbyCheck;

	private static float nextWarning;

	private static float lastClick = -10f;

	public static GameObject Panel => panel;

	public static Button JoinButton => joinButton;

	public static bool IsVisible
	{
		get
		{
			if (panel != null)
			{
				return panel.activeInHierarchy;
			}
			return false;
		}
	}

	public static void Update()
	{
		try
		{
			if (!SteamConnect.CanUseSteam || !Shared.IsMainMenuScene(MyMod.level_name) || string.IsNullOrEmpty(MyMod.MyLobby) || MyMod.iAmHost || (MyMod.sendMyPosition && !SteamConnect.Main.JoinNeedsWorldData) || MyMod.UiCanvas == null)
			{
				if (panel != null)
				{
					panel.SetActive(value: false);
				}
				trackedLobby = null;
				actualGuest = false;
				return;
			}
			if (trackedLobby != MyMod.MyLobby || Time.unscaledTime >= nextLobbyCheck)
			{
				trackedLobby = MyMod.MyLobby;
				nextLobbyCheck = Time.unscaledTime + 0.5f;
				actualGuest = false;
				hostAdvertised = false;
				if (ulong.TryParse(trackedLobby, out var result))
				{
					CSteamID steamIDLobby = new CSteamID(result);
					CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(steamIDLobby);
					CSteamID steamID = SteamUser.GetSteamID();
					actualGuest = lobbyOwner.m_SteamID != 0L && steamID.m_SteamID != 0L && lobbyOwner != steamID;
					if (actualGuest)
					{
						hostAdvertised = SteamMatchmaking.GetLobbyGameServer(steamIDLobby, out var _, out var _, out var psteamIDGameServer) && psteamIDGameServer.m_SteamID != 0;
					}
				}
			}
			if (!actualGuest)
			{
				if (panel != null)
				{
					panel.SetActive(value: false);
				}
				return;
			}
			EnsurePanel();
			panel.SetActive(value: true);
			panel.transform.SetAsLastSibling();
			joinButton.interactable = Time.unscaledTime - lastClick >= 1f;
			string joinStatus = SteamConnect.Main.JoinStatus;
			status.text = ((!string.IsNullOrEmpty(joinStatus)) ? joinStatus : (hostAdvertised ? "Хост готов. Нажмите, чтобы войти." : "Ожидание запуска мира хостом."));
		}
		catch (Exception ex)
		{
			if (panel != null)
			{
				panel.SetActive(value: false);
			}
			if (Time.unscaledTime >= nextWarning)
			{
				nextWarning = Time.unscaledTime + 10f;
				MelonLogger.Warning("[Steam join UI] Could not update lobby action: " + ex.Message);
			}
		}
	}

	private static void EnsurePanel()
	{
		if (panel != null && joinButton != null && status != null && panel.transform.parent == MyMod.UiCanvas.transform)
		{
			return;
		}
		if (panel != null)
		{
			UnityEngine.Object.Destroy(panel);
		}
		Font font = null;
		foreach (Text componentsInChild in MyMod.UiCanvas.GetComponentsInChildren<Text>(includeInactive: true))
		{
			if (componentsInChild != null && componentsInChild.font != null)
			{
				font = componentsInChild.font;
				break;
			}
		}
		if (font == null)
		{
			font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		}
		if (font == null)
		{
			throw new InvalidOperationException("No UI font is available");
		}
		panel = new GameObject("SkyCoop_SteamJoin");
		RectTransform rectTransform = panel.AddComponent<RectTransform>();
		rectTransform.SetParent(MyMod.UiCanvas.transform, worldPositionStays: false);
		Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(1f, 0f));
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = new Vector2(1f, 0f);
		rectTransform.anchoredPosition = new Vector2(-30f, 28f);
		rectTransform.sizeDelta = new Vector2(400f, 116f);
		panel.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.12f, 0.96f);
		GameObject gameObject = new GameObject("EnterGame");
		RectTransform rectTransform2 = gameObject.AddComponent<RectTransform>();
		rectTransform2.SetParent(rectTransform, worldPositionStays: false);
		anchorMin = (rectTransform2.anchorMax = new Vector2(0.5f, 1f));
		rectTransform2.anchorMin = anchorMin;
		rectTransform2.pivot = new Vector2(0.5f, 1f);
		rectTransform2.anchoredPosition = new Vector2(0f, -12f);
		rectTransform2.sizeDelta = new Vector2(368f, 44f);
		Image image = gameObject.AddComponent<Image>();
		image.color = new Color(0.12f, 0.4f, 0.52f, 1f);
		joinButton = gameObject.AddComponent<Button>();
		joinButton.targetGraphic = image;
		joinButton.onClick.AddListener((Action)OnJoinClick);
		Text text = MakeText("Label", rectTransform2, font, 20);
		text.text = "ВОЙТИ В ИГРУ";
		RectTransform component = text.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
		status = MakeText("Status", rectTransform, font, 16);
		RectTransform component2 = status.GetComponent<RectTransform>();
		anchorMin = (component2.anchorMax = new Vector2(0.5f, 0f));
		component2.anchorMin = anchorMin;
		component2.pivot = new Vector2(0.5f, 0f);
		component2.anchoredPosition = new Vector2(0f, 8f);
		component2.sizeDelta = new Vector2(368f, 46f);
		MelonLogger.Msg("[Steam join UI] Explicit guest lobby action created.");
	}

	private static Text MakeText(string name, Transform parent, Font font, int size)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.AddComponent<RectTransform>().SetParent(parent, worldPositionStays: false);
		Text text = gameObject.AddComponent<Text>();
		text.font = font;
		text.fontSize = size;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = Color.white;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		text.raycastTarget = false;
		return text;
	}

	private static void OnJoinClick()
	{
		try
		{
			if (IsVisible && SteamConnect.CanUseSteam && Shared.IsMainMenuScene(MyMod.level_name) && !MyMod.iAmHost && (!MyMod.sendMyPosition || SteamConnect.Main.JoinNeedsWorldData) && !string.IsNullOrEmpty(MyMod.MyLobby) && ulong.TryParse(MyMod.MyLobby, out var result))
			{
				CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(new CSteamID(result));
				CSteamID steamID = SteamUser.GetSteamID();
				if (lobbyOwner.m_SteamID != 0L && steamID.m_SteamID != 0L && !(lobbyOwner == steamID) && !(Time.unscaledTime - lastClick < 1f))
				{
					lastClick = Time.unscaledTime;
					SteamConnect.Main.TryJoinLobbyGame(manual: true);
					Update();
				}
			}
		}
		catch (Exception ex)
		{
			if (status != null)
			{
				status.text = "Не удалось начать подключение. Попробуйте ещё раз.";
			}
			MelonLogger.Warning("[Steam join UI] Join action failed: " + ex.Message);
		}
	}
}
