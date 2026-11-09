using System;
using System.Collections.Generic;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace SkyCoop;

internal static class DeathRespawn
{
    private const float RescueSeconds = 180f;
    private const float RetrySeconds = .75f;
    private const float Reach = 3f;

    private sealed class Rescue
    {
        internal string Token;
        internal string Scene;
        internal float Deadline;
        internal int Rescuer = -1;
        internal float ReservedUntil;
        internal string Result;
    }

    private static readonly Dictionary<int, Rescue> hostRescues = new();
    private static readonly Dictionary<int, (string Token, float Expires)> nearby = new();
    private static object session;
    private static string token;
    private static string scene;
    private static float deadline;
    private static float nextNotice;
    private static bool giveUp;
    private static bool finalizing;
    private static int rescueTarget = -1;
    private static string rescueToken;
    private static GearItem reservedStim;
    private static bool paid;
    private static float nextRescue;
    private static float rescueTimeout;
    private static bool cursorCaptured;
    private static InputManager.CursorState cursorState;
    private static CursorLockMode cursorLock;
    private static bool cursorVisible;

    private static int LocalId => MyMod.iAmHost ? 0 : ClientUser.myId;

    internal static bool TryDown(Condition condition)
    {
        if (condition == null || finalizing || !MyMod.InOnline() || !WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_guid)) return false;
        if (token != null) return true;
        var manager = GameManager.GetPlayerManagerComponent();
        if (manager == null) return false;
        token = Guid.NewGuid().ToString("N");
        scene = MyMod.level_guid;
        deadline = Time.realtimeSinceStartup + RescueSeconds;
        nextNotice = 0f;
        giveUp = false;
        if (GameManager.m_Rest != null && GameManager.m_Rest.IsSleeping())
            GameManager.m_Rest.EndSleeping(interrupted: true);
        manager.UnequipImmediate(saveLastItem: false);
        manager.SetControlMode(PlayerControlMode.Dead);
        condition.m_CurrentHP = 0f;
        MyMod.IsDead = true;
        cursorState = InputManager.m_CursorState;
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;
        cursorCaptured = true;
        return true;
    }

    internal static bool CanRevive(int target) => nearby.TryGetValue(target, out var state) && state.Expires > Time.realtimeSinceStartup;

    internal static bool TryTreat(int target)
    {
        if (!nearby.TryGetValue(target, out var state) || state.Expires <= Time.realtimeSinceStartup)
            return target >= 0 && target < MyMod.playersData.Count &&
                (MyMod.playersData[target]?.m_Dead == true || MyMod.playersData[target]?.m_AnimState == "Knock");
        if (rescueTarget >= 0 || token != null) return true;
        var stim = GameManager.GetPlayerManagerComponent()?.m_ItemInHands;
        if (stim?.m_EmergencyStim == null || !stim.m_InPlayerInventory) return true;
        rescueTarget = target;
        rescueToken = state.Token;
        reservedStim = stim;
        rescueTimeout = Time.realtimeSinceStartup + 9f;
        nextRescue = 0f;
        return true;
    }

    private static void Send(string operation, int target, string id)
    {
        if (MyMod.iAmHost) ReceiveClient(0, MyMod.level_guid, target, operation + id);
        else ScenePlacementSync.SendRescueToHost(target, operation + id);
    }

    private static void Reply(int recipient, string area, int subject, string operation, string id)
    {
        if (recipient == 0) Receive(area, subject, operation + id);
        else ScenePlacementSync.SendRescueToGuest(recipient, area, subject, operation + id);
    }

    private static bool TryPosition(int id, string area, out Vector3 position)
    {
        position = default;
        if (id == 0)
        {
            if (MyMod.level_guid != area || GameManager.m_PlayerObject == null) return false;
            position = GameManager.GetPlayerTransform().position;
            return true;
        }
        if (id < 1 || id >= MyMod.playersData.Count || !Server.clients.TryGetValue(id, out var client) ||
            !client.IsBusy() || MyMod.playersData[id]?.m_LevelGuid != area) return false;
        position = MyMod.playersData[id].m_Position;
        return true;
    }

    internal static void ReceiveClient(int sender, string area, int subject, string message)
    {
        if (!MyMod.iAmHost || message.Length != 33 || !Guid.TryParseExact(message[1..], "N", out _) ||
            !TryPosition(sender, area, out var origin)) return;
        string id = message[1..];
        char operation = message[0];
        float now = Time.realtimeSinceStartup;
        if (operation == 'D')
        {
            if (sender != subject) return;
            if (!hostRescues.TryGetValue(subject, out var existing) || existing.Token != id)
                hostRescues[subject] = new Rescue { Token = id, Scene = area, Deadline = now + RescueSeconds };
        }
        if (!hostRescues.TryGetValue(subject, out var rescue) || rescue.Token != id || rescue.Scene != area) return;
        if (rescue.Result != null)
        {
            if (sender == subject) Reply(subject, area, subject, rescue.Result, id);
            if (sender == rescue.Rescuer && rescue.Result == "V") Reply(sender, area, subject, "C", id);
            return;
        }
        if (sender == subject && (operation == 'G' || now >= rescue.Deadline))
        {
            if (rescue.ReservedUntil > now) return;
            rescue.Result = "G";
            Reply(subject, area, subject, "G", id);
        }
        else if (operation == 'P' && sender == rescue.Rescuer && rescue.ReservedUntil > now)
        {
            rescue.Result = "V";
            Reply(subject, area, subject, "V", id);
            Reply(sender, area, subject, "C", id);
        }
        else if (operation == 'R' && sender != subject && now < rescue.Deadline &&
                 (!hostRescues.TryGetValue(sender, out var own) || own.Result != null))
        {
            if (!TryPosition(subject, area, out var fallen) || (origin - fallen).sqrMagnitude > Reach * Reach) return;
            if (rescue.ReservedUntil > now && rescue.Rescuer != sender) return;
            rescue.Rescuer = sender;
            rescue.ReservedUntil = now + 8f;
            Reply(sender, area, subject, "A", id);
        }
    }

    internal static void Receive(string area, int subject, string message)
    {
        if (area != MyMod.level_guid || message.Length != 33) return;
        string id = message[1..];
        switch (message[0])
        {
            case 'D':
                if (subject != LocalId) nearby[subject] = (id, Time.realtimeSinceStartup + 2.5f);
                break;
            case 'A' when subject == rescueTarget && id == rescueToken && !paid:
                var manager = GameManager.GetPlayerManagerComponent();
                if (token != null || manager == null || reservedStim == null || !reservedStim.m_InPlayerInventory ||
                    manager.m_ItemInHands != reservedStim) return;
                manager.UnequipImmediate(saveLastItem: false);
                GameManager.GetInventoryComponent().DestroyGear(reservedStim.gameObject);
                paid = true;
                nextRescue = 0f;
                Send("P", rescueTarget, rescueToken);
                break;
            case 'C' when subject == rescueTarget && id == rescueToken:
                ClearAttempt();
                break;
            case 'V' when subject == LocalId && id == token:
                Finish(revived: true);
                break;
            case 'G' when subject == LocalId && id == token:
                Finish(revived: false);
                break;
        }
    }

    private static void ClearAttempt()
    {
        rescueTarget = -1;
        reservedStim = null;
        rescueToken = null;
        paid = false;
    }

    private static void ReleaseCursor()
    {
        if (!cursorCaptured) return;
        cursorCaptured = false;
        InputManager.m_CursorState = cursorState;
        Cursor.visible = cursorVisible;
        Cursor.lockState = cursorLock;
    }

    private static void Finish(bool revived)
    {
        token = null;
        giveUp = false;
        ReleaseCursor();
        if (revived)
        {
            var condition = GameManager.GetConditionComponent();
            condition.m_CurrentHP = Mathf.Min(20f, condition.GetAdjustedMaxHP());
            MyMod.IsDead = false;
            GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Normal);
            HUDMessage.AddMessage("Вас привели в сознание");
            return;
        }
        finalizing = true;
        try
        {
            try { MyMod.DropAll(); }
            finally { MyMod.BeginRespawn(); }
        }
        finally { finalizing = false; }
    }

    private static float nextBroadcast;

    internal static void Tick()
    {
        float now = Time.realtimeSinceStartup;
        if (!MyMod.InOnline())
        {
            if (token != null) MyMod.IsDead = false;
            token = null;
            nearby.Clear();
            ClearAttempt();
            ReleaseCursor();
            return;
        }
        if (MyMod.iAmHost)
        {
            Server.clients.TryGetValue(1, out var client);
            if (!ReferenceEquals(session, client))
            {
                session = client;
                hostRescues.Clear();
            }
            if (now >= nextBroadcast)
            {
                nextBroadcast = now + 1f;
                foreach (var entry in hostRescues)
                {
                    var rescue = entry.Value;
                    if (rescue.Result != null || now >= rescue.Deadline || !TryPosition(entry.Key, rescue.Scene, out _)) continue;
                    ScenePlacementSync.BroadcastDowned(rescue.Scene, entry.Key, "D" + rescue.Token);
                    Receive(rescue.Scene, entry.Key, "D" + rescue.Token);
                }
            }
        }
        if (token != null && scene != MyMod.level_guid)
        {
            token = null;
            ReleaseCursor();
        }
        if (token != null)
        {
            InputManager.m_CursorState = InputManager.CursorState.Show;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (now >= nextNotice)
            {
                nextNotice = now + RetrySeconds;
                string current = token;
                Send("D", LocalId, current);
                if (giveUp && token == current) Send("G", LocalId, current);
            }
        }
        if (rescueTarget >= 0 && now >= rescueTimeout) ClearAttempt();
        if (rescueTarget >= 0 && now >= nextRescue)
        {
            nextRescue = now + RetrySeconds;
            Send(paid ? "P" : "R", rescueTarget, rescueToken);
        }
    }

    internal static void Draw()
    {
        if (token == null) return;
        int depth = GUI.depth;
        GUI.depth = -15000;
        float x = (Screen.width - 440f) * .5f;
        float y = Screen.height - 145f;
        int seconds = Mathf.Max(0, Mathf.CeilToInt(deadline - Time.realtimeSinceStartup));
        GUI.Box(new Rect(x, y, 440f, 104f), giveUp || seconds == 0 ? "Подготовка к возрождению…" :
            $"Вы без сознания. На помощь осталось {seconds / 60}:{seconds % 60:00}");
        if (!giveUp && seconds > 0 && GUI.Button(new Rect(x + 110f, y + 49f, 220f, 35f), "Сдаться и возродиться"))
        {
            giveUp = true;
            nextNotice = 0f;
        }
        GUI.depth = depth;
    }
}
