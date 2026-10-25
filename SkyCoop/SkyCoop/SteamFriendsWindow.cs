using System;
using System.Collections.Generic;
using MelonLoader;
using Steamworks;
using UnityEngine;

namespace SkyCoop;

// Invite and join through Steam friends without the Steam overlay. The co-op copy is started directly (not by
// Steam), so the overlay is not injected and ActivateGameOverlayInviteDialog / ActivateGameOverlay do nothing.
// The window lists friends and sends lobby invites itself; received invites show a prompt with an accept button.
public static class SteamFriendsWindow
{
	private class Friend
	{
		public CSteamID Id;

		public string Name;

		public bool Online;

		public bool InTld;

		public CSteamID Lobby;
	}

	private class Invite
	{
		public CSteamID From;

		public CSteamID Lobby;

		public string Name;

		public float Until;
	}

	private const uint TldAppId = 305620u;

	private const int PageSize = 12;

	private static readonly List<Friend> Friends = new List<Friend>();

	private static readonly List<Invite> Invites = new List<Invite>();

	private static readonly HashSet<ulong> Invited = new HashSet<ulong>();

	private static Callback<LobbyInvite_t> lobbyInviteCallback;

	private static bool open;

	private static bool joinMode;

	private static int page;

	private static string status = "";

	public static bool IsOpen => open;

	private static bool OverlayAvailable
	{
		get
		{
			try
			{
				return SteamUtils.IsOverlayEnabled();
			}
			catch
			{
				return false;
			}
		}
	}

	// "Invite friend": the Steam overlay when it works, otherwise this window.
	public static void ShowInvite()
	{
		if (!SteamConnect.CanUseSteam || string.IsNullOrEmpty(MyMod.MyLobby))
		{
			return;
		}
		if (OverlayAvailable)
		{
			SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(ulong.Parse(MyMod.MyLobby)));
			return;
		}
		Open(join: false);
	}

	// "Connect to friend": friends who are in a co-op lobby, with a join button.
	public static void ShowJoin()
	{
		if (!SteamConnect.CanUseSteam)
		{
			return;
		}
		if (OverlayAvailable)
		{
			SteamFriends.ActivateGameOverlay("friends");
			return;
		}
		Open(join: true);
	}

	private static void Open(bool join)
	{
		joinMode = join;
		page = 0;
		status = join ? "Друзья в игре. Если друга нет в списке, попросите его прислать приглашение." : "Нажмите «Пригласить» — другу придёт приглашение в Steam.";
		Refresh();
		if (!open)
		{
			open = true;
			OverlayInput.Acquire();
		}
		MelonLogger.Msg("[Friends] Window opened, mode=" + (join ? "join" : "invite") + ", friends=" + Friends.Count + ", overlay=" + OverlayAvailable);
	}

	private static void Close()
	{
		if (open)
		{
			open = false;
			OverlayInput.Release();
		}
	}

	private static void Refresh()
	{
		Friends.Clear();
		try
		{
			int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
			for (int i = 0; i < count; i++)
			{
				CSteamID id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
				Friend friend = new Friend
				{
					Id = id,
					Name = SteamFriends.GetFriendPersonaName(id),
					Online = SteamFriends.GetFriendPersonaState(id) != EPersonaState.k_EPersonaStateOffline,
					Lobby = CSteamID.Nil
				};
				if (SteamFriends.GetFriendGamePlayed(id, out var game) && game.m_gameID.AppID().m_AppId == TldAppId)
				{
					friend.InTld = true;
					friend.Lobby = game.m_steamIDLobby;
				}
				if (friend.Online || friend.InTld)
				{
					Friends.Add(friend);
				}
			}
		}
		catch (Exception ex)
		{
			status = "Не удалось получить список друзей: " + ex.Message;
			MelonLogger.Warning("[Friends] " + ex);
		}
		Friends.Sort(delegate(Friend a, Friend b)
		{
			int c = b.InTld.CompareTo(a.InTld);
			return (c != 0) ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
		});
	}

	public static void Tick()
	{
		if (!SteamConnect.CanUseSteam)
		{
			return;
		}
		if (lobbyInviteCallback == null)
		{
			lobbyInviteCallback = Callback<LobbyInvite_t>.Create(OnLobbyInvite);
		}
		if (open)
		{
			OverlayInput.Keep();
		}
		Invites.RemoveAll((Invite i) => Time.realtimeSinceStartup > i.Until);
	}

	private static void OnLobbyInvite(LobbyInvite_t invite)
	{
		if (invite.m_ulGameID != TldAppId && invite.m_ulGameID != 0)
		{
			return;
		}
		CSteamID from = new CSteamID(invite.m_ulSteamIDUser);
		CSteamID lobby = new CSteamID(invite.m_ulSteamIDLobby);
		Invites.RemoveAll((Invite i) => i.Lobby == lobby);
		Invites.Add(new Invite
		{
			From = from,
			Lobby = lobby,
			Name = SteamFriends.GetFriendPersonaName(from),
			Until = Time.realtimeSinceStartup + 120f
		});
		MelonLogger.Msg("[Friends] Invite from " + invite.m_ulSteamIDUser + " to lobby " + invite.m_ulSteamIDLobby);
	}

	private static void Join(CSteamID lobby, CSteamID friend)
	{
		Close();
		Invites.RemoveAll((Invite i) => i.Lobby == lobby);
		MelonLogger.Msg("[Friends] Joining lobby " + lobby.m_SteamID);
		SteamConnect.Main.ShouldJoinLobby(new GameLobbyJoinRequested_t
		{
			m_steamIDLobby = lobby,
			m_steamIDFriend = friend
		});
	}

	public static void Draw()
	{
		DrawInvites();
		if (!open)
		{
			return;
		}
		OverlayInput.Keep();
		Event current = Event.current;
		if (current != null && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
		{
			current.Use();
			Close();
			return;
		}
		float width = 560f;
		float rowHeight = 30f;
		float height = 130f + rowHeight * PageSize;
		float x = ((float)Screen.width - width) / 2f;
		float y = Mathf.Max(20f, ((float)Screen.height - height) / 2f);
		int depth = GUI.depth;
		GUI.depth = -20000;
		GUI.Box(new Rect(x, y, width, height), joinMode ? "Подключиться к другу" : "Пригласить друга");
		GUI.Label(new Rect(x + 15f, y + 25f, width - 30f, 40f), status);
		int pages = Math.Max(1, (Friends.Count + PageSize - 1) / PageSize);
		page = Mathf.Clamp(page, 0, pages - 1);
		float rowY = y + 65f;
		if (Friends.Count == 0)
		{
			GUI.Label(new Rect(x + 15f, rowY, width - 30f, rowHeight), "Нет друзей в сети");
		}
		for (int i = page * PageSize; i < Friends.Count && i < (page + 1) * PageSize; i++)
		{
			Friend friend = Friends[i];
			string state = friend.InTld ? (friend.Lobby.IsValid() ? "  [в лобби The Long Dark]" : "  [в The Long Dark]") : "";
			GUI.Label(new Rect(x + 15f, rowY + 4f, width - 180f, rowHeight), friend.Name + state);
			Rect button = new Rect(x + width - 160f, rowY, 145f, rowHeight - 4f);
			if (joinMode)
			{
				bool enabled = GUI.enabled;
				GUI.enabled = friend.Lobby.IsValid();
				if (GUI.Button(button, "Войти"))
				{
					Join(friend.Lobby, friend.Id);
				}
				GUI.enabled = enabled;
			}
			else if (GUI.Button(button, Invited.Contains(friend.Id.m_SteamID) ? "Ещё раз" : "Пригласить"))
			{
				bool sent = !string.IsNullOrEmpty(MyMod.MyLobby) && SteamMatchmaking.InviteUserToLobby(new CSteamID(ulong.Parse(MyMod.MyLobby)), friend.Id);
				if (sent)
				{
					Invited.Add(friend.Id.m_SteamID);
				}
				status = sent ? ("Приглашение отправлено: " + friend.Name) : ("Не удалось пригласить " + friend.Name);
				MelonLogger.Msg("[Friends] Invite " + friend.Id.m_SteamID + " -> " + sent);
			}
			rowY += rowHeight;
		}
		float bottom = y + height - 40f;
		if (pages > 1)
		{
			if (GUI.Button(new Rect(x + 15f, bottom, 40f, 28f), "<"))
			{
				page--;
			}
			GUI.Label(new Rect(x + 62f, bottom + 4f, 80f, 24f), (page + 1) + " / " + pages);
			if (GUI.Button(new Rect(x + 130f, bottom, 40f, 28f), ">"))
			{
				page++;
			}
		}
		if (GUI.Button(new Rect(x + width - 290f, bottom, 130f, 28f), "Обновить"))
		{
			Refresh();
		}
		if (GUI.Button(new Rect(x + width - 150f, bottom, 135f, 28f), "Закрыть (Esc)"))
		{
			Close();
		}
		GUI.depth = depth;
	}

	// Received invites while in the main menu; in the world an accept would only say "already on server".
	private static void DrawInvites()
	{
		if (Invites.Count == 0 || !Shared.IsMainMenuScene(MyMod.level_name))
		{
			return;
		}
		Invite invite = Invites[Invites.Count - 1];
		float width = 460f;
		float x = ((float)Screen.width - width) / 2f;
		float y = 30f;
		int depth = GUI.depth;
		GUI.depth = -20000;
		GUI.Box(new Rect(x, y, width, 80f), invite.Name + " приглашает вас в игру");
		if (GUI.Button(new Rect(x + 40f, y + 35f, 180f, 32f), "Принять"))
		{
			Join(invite.Lobby, invite.From);
		}
		else if (GUI.Button(new Rect(x + width - 220f, y + 35f, 180f, 32f), "Отклонить"))
		{
			Invites.Remove(invite);
		}
		GUI.depth = depth;
	}
}
