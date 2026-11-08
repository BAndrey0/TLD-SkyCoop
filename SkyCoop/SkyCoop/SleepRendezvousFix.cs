using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class SleepRendezvousFix
{
	private static GameObject boundRoot;

	private static int childSignature;

	private static int lastClickFrame = -1;

	private static int requestedHours;
	private static int offeredHours;
	private static string offerText;
	private static float lastOffer;
	private static readonly Dictionary<int, float> VoteExpires = new();
	private static UILabel restNotice;
	private static GameObject localRestButton;
	private static Vector3 restRowCenter;
	private static Vector3 restRowScale;
	private static EventDelegate.Callback localRestCallback;
	private static Il2CppSystem.Collections.Generic.List<EventDelegate> localRestActions;
	private static readonly System.Collections.Generic.List<int> VoteOrder = new();

	internal static void RecordVote(int id, int hours)
	{
		if (hours == 0)
		{
			VoteOrder.Remove(id);
			VoteExpires.Remove(id);
		}
		else
		{
			if (!VoteOrder.Contains(id)) VoteOrder.Add(id);
			if (id > 0) VoteExpires[id] = Time.realtimeSinceStartup + 4f;
		}
	}

	internal static int SelectOffer(Func<int, int> currentVote)
	{
		VoteOrder.RemoveAll(id => currentVote(id) == 0);
		return VoteOrder.Count == 0 ? 0 : currentVote(VoteOrder[0]);
	}

	internal static int ProposalOwner => VoteOrder.Count > 0 ? VoteOrder[0] : -1;

	internal static void ClearVotes() { VoteOrder.Clear(); VoteExpires.Clear(); }
	internal static bool VoteIsCurrent(int id) => VoteExpires.TryGetValue(id, out float expires) && Time.realtimeSinceStartup < expires;

	private static bool waiting;

	private static EventDelegate.Callback waitCallback;

	private static Il2CppSystem.Collections.Generic.List<EventDelegate> clickActions;

	private static readonly HashSet<int> boundButtonIds = new HashSet<int>();

	public static bool Waiting
	{
		get
		{
			if (waiting && requestedHours != 0 && MyMod.WaitForSleepLable != null &&
				GameCompat.Panel<Panel_Rest>()?.isActiveAndEnabled == true)
			{
				return MyMod.WaitForSleepLable.activeSelf;
			}
			return false;
		}
	}

	public static int RequestedHours => requestedHours;

	public static int AcceptedClicks { get; private set; }

	public static string Status { get; private set; } = "";

	public static bool IsWaitButton(UIButton button)
	{
		if (button == null || MyMod.new_button == null)
		{
			return false;
		}
		Transform transform = button.transform;
		Transform transform2 = MyMod.new_button.transform;
		if (!boundButtonIds.Contains(button.GetInstanceID()) && !(transform == transform2))
		{
			return transform.IsChildOf(transform2);
		}
		return true;
	}

	private static System.Collections.Generic.List<UIButton> GatherButtons(GameObject root, GenericButtonMouseSpawner spawner, GameObject stockSleepButton)
	{
		System.Collections.Generic.List<UIButton> result = new System.Collections.Generic.List<UIButton>();
		HashSet<int> seen = new HashSet<int>();
		HashSet<int> stockIds = new HashSet<int>();
		if (stockSleepButton != null && stockSleepButton != root)
		{
			foreach (UIButton componentsInChild in stockSleepButton.GetComponentsInChildren<UIButton>(includeInactive: true))
			{
				if (componentsInChild != null)
				{
					stockIds.Add(componentsInChild.GetInstanceID());
				}
			}
			GenericButtonMouseSpawner component = stockSleepButton.GetComponent<GenericButtonMouseSpawner>();
			if (component != null)
			{
				if (component.m_UIButtonArray != null)
				{
					foreach (UIButton item in component.m_UIButtonArray)
					{
						if (item != null)
						{
							stockIds.Add(item.GetInstanceID());
						}
					}
				}
				if (component.m_SpawnedObject != null)
				{
					foreach (UIButton componentsInChild2 in component.m_SpawnedObject.GetComponentsInChildren<UIButton>(includeInactive: true))
					{
						if (componentsInChild2 != null)
						{
							stockIds.Add(componentsInChild2.GetInstanceID());
						}
					}
				}
			}
		}
		Action<UIButton> action = delegate(UIButton button)
		{
			if (button != null && !stockIds.Contains(button.GetInstanceID()) && seen.Add(button.GetInstanceID()))
			{
				result.Add(button);
			}
		};
		foreach (UIButton componentsInChild3 in root.GetComponentsInChildren<UIButton>(includeInactive: true))
		{
			action(componentsInChild3);
		}
		if (spawner != null)
		{
			if (spawner.m_UIButtonArray != null)
			{
				foreach (UIButton item2 in spawner.m_UIButtonArray)
				{
					action(item2);
				}
			}
			if (spawner.m_SpawnedObject != null)
			{
				foreach (UIButton componentsInChild4 in spawner.m_SpawnedObject.GetComponentsInChildren<UIButton>(includeInactive: true))
				{
					action(componentsInChild4);
				}
			}
		}
		return result;
	}

	public static void Bind(Panel_Rest panel)
	{
		GameObject new_button = MyMod.new_button;
		if (new_button == null || panel == null)
		{
			return;
		}
		// The native sleep navigation parent is hidden on the pass-time tab.
		if (new_button.transform.parent != panel.transform) new_button.transform.SetParent(panel.transform, true);
		GenericButtonMouseSpawner component = new_button.GetComponent<GenericButtonMouseSpawner>();
		System.Collections.Generic.List<UIButton> list = GatherButtons(new_button, component, panel.m_SleepButton);
		int num = 17;
		foreach (UIButton item in list)
		{
			num = num * 31 + item.GetInstanceID();
		}
		if (boundRoot != new_button || num != childSignature || clickActions == null)
		{
			boundRoot = new_button;
			childSignature = num;
			if (waitCallback == null)
			{
				waitCallback = (Action)ClickWait;
			}
			clickActions = new Il2CppSystem.Collections.Generic.List<EventDelegate>();
			EventDelegate.Add(clickActions, waitCallback);
			if (component != null)
			{
				component.onClick = clickActions;
			}
			boundButtonIds.Clear();
			foreach (UIButton item2 in list)
			{
				item2.onClick = clickActions;
			}
			foreach (UIButton item3 in list)
			{
				boundButtonIds.Add(item3.GetInstanceID());
			}
			MelonLogger.Msg("[Sleep wait] Bound explicit wait action to " + list.Count + " native button(s).");
		}
		bool flag = panel.isActiveAndEnabled && MyMod.InOnline() && !waiting;
		foreach (var label in new_button.GetComponentsInChildren<UILabel>(true))
			label.text = panel.m_ShowPassTime ? "ПОДОЖДАТЬ ВСЕМ ВМЕСТЕ" : "СПАТЬ ВСЕМ ВМЕСТЕ";
		new_button.SetActive(flag);
		BindLocalRest(panel, new_button, flag);
		if (component != null)
		{
			component.enabled = true;
		}
		foreach (UIButton item4 in list)
		{
			item4.isEnabled = flag && !MyMod.SaveExitPending;
		}
		foreach (Collider componentsInChild in new_button.GetComponentsInChildren<Collider>(includeInactive: true))
		{
			componentsInChild.enabled = flag && !MyMod.SaveExitPending;
		}
	}

	private static void BindLocalRest(Panel_Rest panel, GameObject groupButton, bool groupVisible)
	{
		if (localRestButton == null || localRestButton.transform.parent != panel.transform)
		{
			if (localRestButton != null) UnityEngine.Object.Destroy(localRestButton);
			restRowCenter = groupButton.transform.localPosition;
			restRowScale = groupButton.transform.localScale;
			localRestButton = UnityEngine.Object.Instantiate(groupButton, panel.transform, false);
			localRestButton.name = "TLD Coop local rest";
			var localSpawner = localRestButton.GetComponent<GenericButtonMouseSpawner>();
			if (localSpawner != null && localSpawner.m_SpawnedObject != null &&
				!localSpawner.m_SpawnedObject.transform.IsChildOf(localRestButton.transform))
			{
				localSpawner.m_SpawnedObject = null;
				localSpawner.InstantiateObjectIfNoneExists();
			}
		}
		bool show = groupVisible && BedRest.CanBegin(panel);
		localRestButton.SetActive(show);
		var widget = groupButton.GetComponentInChildren<BoxCollider>(true);
		float width = widget != null ? Mathf.Abs(panel.transform.InverseTransformVector(widget.transform.TransformVector(Vector3.right * widget.size.x)).x) : 0f;
		if (groupButton.transform.localScale.x != restRowScale.x) width /= .82f;
		float offset = width > 0f ? width * .45f : .32f;
		groupButton.transform.localPosition = restRowCenter - Vector3.right * (show ? offset : 0f);
		groupButton.transform.localScale = show ? Vector3.Scale(restRowScale, new Vector3(.82f, 1f, 1f)) : restRowScale;
		if (!show) return;
		localRestButton.transform.localPosition = restRowCenter + Vector3.right * offset;
		localRestButton.transform.localScale = groupButton.transform.localScale;
		if (localRestActions == null)
		{
			localRestCallback = (Action)BedRest.Request;
			localRestActions = new Il2CppSystem.Collections.Generic.List<EventDelegate>();
			EventDelegate.Add(localRestActions, localRestCallback);
		}
		var spawner = localRestButton.GetComponent<GenericButtonMouseSpawner>();
		if (spawner != null) spawner.onClick = localRestActions;
		foreach (var button in GatherButtons(localRestButton, spawner, panel.m_SleepButton))
		{ button.onClick = localRestActions; button.isEnabled = true; }
		foreach (var label in localRestButton.GetComponentsInChildren<UILabel>(true))
		{
			var localizer = label.GetComponent<UILocalize>();
			if (localizer != null) localizer.enabled = false;
			label.text = "ЛЕЧЬ ОТДОХНУТЬ";
		}
	}

	public static void Tick(Panel_Rest panel)
	{
		if (panel == null || !panel.isActiveAndEnabled || !MyMod.InOnline())
		{
			if (waiting)
			{
				CancelWaiting(explain: false);
			}
			return;
		}
		Bind(panel);
		if (offeredHours != 0 && panel.m_ShowPassTime == (offeredHours < 0) && panel.m_SleepHours != Math.Abs(offeredHours))
		{
			panel.m_SleepHours = Math.Abs(offeredHours);
			panel.UpdateRestDurationLabel();
			panel.UpdateWakeTimeLabel();
		}
		if (waiting)
		{
			if (MyMod.WaitForSleepLable == null || !MyMod.WaitForSleepLable.activeSelf)
			{
				waiting = false;
				requestedHours = 0;
			}
			else if ((offeredHours != 0 && Math.Sign(offeredHours) != Math.Sign(requestedHours)) ||
				panel.m_ShowPassTime != (requestedHours < 0) || MyMod.SaveExitPending ||
				(requestedHours > 0 && !NativeSleepAllowed(explain: false)))
			{
				CancelWaiting(explain: false);
			}
			else
			{
				panel.m_SleepHours = Math.Abs(offeredHours != 0 ? offeredHours : requestedHours);
			}
		}
		MyMod.MyCycleSkip = waiting ? requestedHours : MyMod.IsSleeping ? MyMod.MyCycleSkip : 0;
	}

	private static bool NativeSleepAllowed(bool explain)
	{
		PlayerStruggle playerStruggleComponent = GameManager.GetPlayerStruggleComponent();
		if (playerStruggleComponent != null && playerStruggleComponent.InStruggle())
		{
			if (explain)
			{
				HUDMessage.AddMessage("Нельзя ждать сна во время нападения.");
			}
			return false;
		}
		if (!CoopFatigue.SleepAllowed(explain))
		{
			return false;
		}
		if (MyMod.CanSleep(domessage: false))
		{
			return true;
		}
		if (explain)
		{
			if (GameManager.m_BlockAbilityToRest && string.IsNullOrEmpty(GameManager.m_BlockedRestLocID))
			{
				HUDMessage.AddMessage("Сейчас нельзя спать.");
			}
			else
			{
				MyMod.CanSleep(domessage: true);
			}
		}
		return false;
	}

	// Set while the group's agreed sleep is being started; any other sleep start in co-op is a solo sleep.
	public static bool GroupStart;

	// Co-op sleep is a group action: a sleep started alone restored only that player and did not move the world clock.
	public static bool SoloSleepBlocked()
	{
		if (!MyMod.InOnline() || GroupStart || MyMod.IsDead)
		{
			return false;
		}
		if (MyMod.iAmHost)
		{
			foreach (System.Collections.Generic.KeyValuePair<int, GameServer.Client> client in GameServer.Server.clients)
			{
				if (client.Key > 0 && client.Value != null && !client.Value.RCON && client.Value.IsBusy())
				{
					return true;
				}
			}
			return false;
		}
		return true;
	}

	public static void ClickWait()
	{
		if (lastClickFrame == Time.frameCount)
		{
			return;
		}
		lastClickFrame = Time.frameCount;
		Panel_Rest panel_Rest = GameCompat.Panel<Panel_Rest>();
		if (!(panel_Rest != null) || !panel_Rest.isActiveAndEnabled || !MyMod.InOnline() || MyMod.SaveExitPending || waiting)
		{
			return;
		}
		bool passTime = panel_Rest.m_ShowPassTime;
		if (offeredHours != 0 && passTime != (offeredHours < 0))
		{
			HUDMessage.AddMessage(offeredHours < 0 ? "Группа выбрала ожидание. Открой вкладку ожидания." : "Группа выбрала сон. Открой вкладку сна.");
			return;
		}
		if (passTime && (!panel_Rest.GetPassTimeAllowed() || GameManager.GetPlayerStruggleComponent().InStruggle()))
		{
			HUDMessage.AddMessage("Сейчас нельзя ждать.");
			return;
		}
		if (panel_Rest.m_SleepHours <= 0)
		{
			HUDMessage.AddMessage("Выбери хотя бы один час.");
		}
		else
		{
			if (!passTime && !NativeSleepAllowed(explain: true))
			{
				return;
			}
			if (MyMod.WaitForSleepLable == null)
			{
				HUDMessage.AddMessage("Окно ожидания сна ещё не готово.");
				return;
			}
			requestedHours = offeredHours != 0 ? offeredHours : Math.Clamp(panel_Rest.m_SleepHours, 1, 24) * (passTime ? -1 : 1);
			if (MyMod.iAmHost) RecordVote(0, requestedHours);
			waiting = true;
			AcceptedClicks++;
			MyMod.MyCycleSkip = requestedHours;
			MyMod.WaitForSleepLable.SetActive(value: true);
			if (MyMod.SleepingButtons != null)
			{
				MyMod.SleepingButtons.SetActive(value: false);
			}
			Status = "ЖДЁМ ОСТАЛЬНЫХ ИГРОКОВ\nESC — отмена";
			SetLabel(Status);
			MyMod.SyncSleeping();
			MelonLogger.Msg("[Sleep wait] Waiting for group, requestedHours=" + requestedHours);
		}
	}

	public static void CancelWaiting(bool explain)
	{
		bool num = waiting || (MyMod.WaitForSleepLable != null && MyMod.WaitForSleepLable.activeSelf);
		waiting = false;
		requestedHours = 0;
		if (MyMod.WaitForSleepLable != null)
		{
			MyMod.WaitForSleepLable.SetActive(value: false);
		}
		if (MyMod.SleepingButtons != null)
		{
			MyMod.SleepingButtons.SetActive(value: true);
		}
		if (num)
		{
			MyMod.MyCycleSkip = 0;
			if (MyMod.iAmHost) RecordVote(0, 0);
			if (MyMod.InOnline() && GameManager.GetPlayerManagerComponent() != null)
			{
				MyMod.SyncSleeping();
			}
			Status = "Совместное ожидание отменено";
			if (explain)
			{
				HUDMessage.AddMessage(Status);
			}
			MelonLogger.Msg("[Sleep wait] Waiting cancelled.");
		}
	}

	internal static void TickNotice()
	{
		if (waiting && !Waiting) CancelWaiting(explain: false);
		string message = null;
		if (MyMod.InOnline() && !MyMod.LoadingScreenIsOn && !MyMod.IsDead && !Waiting &&
			!InterfaceManager.IsOverlayActiveImmediate())
		{
			message = Time.realtimeSinceStartup - lastOffer <= 4f ? offerText : null;
			if (BedRest.Active) message = string.IsNullOrEmpty(message) ? BedRest.Status : message + "\n" + BedRest.Status;
		}
		if (restNotice == null && !string.IsNullOrEmpty(message))
		{
			var source = GameCompat.Panel<Panel_HUD>()?.m_Label_Message;
			if (source == null || source.transform.parent == null) return;
			restNotice = UnityEngine.Object.Instantiate(source, source.transform.parent, false);
			restNotice.name = "TLD Coop rest notice";
			restNotice.transform.localPosition = source.transform.localPosition + Vector3.down * (Mathf.Max(28, source.fontSize) * 1.5f);
			var localizer = restNotice.GetComponent<UILocalize>();
			if (localizer != null) localizer.enabled = false;
		}
		if (restNotice == null) return;
		if (!string.IsNullOrEmpty(message)) { restNotice.text = message; restNotice.alpha = 1f; }
		restNotice.gameObject.SetActive(!string.IsNullOrEmpty(message));
	}

	public static void ProcessReadiness(int sleepers, int required, bool everyoneReady, int commonHours, string proposer)
	{
		lastOffer = Time.realtimeSinceStartup;
		offerText = commonHours == 0 || everyoneReady || string.IsNullOrEmpty(proposer) ? null :
			$"{proposer} предлагает {(commonHours < 0 ? "подождать" : "поспать")} · {Math.Abs(commonHours)} ч";
		offeredHours = commonHours;
		if (!Waiting)
		{
			return;
		}
		Status = (commonHours < 0 ? "ОЖИДАНИЕ" : "СОН") + " · " + Math.Abs(commonHours != 0 ? commonHours : requestedHours) + " ч\nГотовы: " + sleepers + "/" + required + "\nESC — отмена";
		SetLabel(Status);
		if (!everyoneReady)
		{
			return;
		}
		if (commonHours == 0 || Math.Sign(commonHours) != Math.Sign(requestedHours))
		{
			Status = "ЖДЁМ ПОДТВЕРЖДЕНИЯ ВРЕМЕНИ СНА\nESC — отмена";
			SetLabel(Status);
			return;
		}
		Panel_Rest panel_Rest = GameCompat.Panel<Panel_Rest>();
		if (panel_Rest == null || !panel_Rest.isActiveAndEnabled || panel_Rest.m_ShowPassTime != (commonHours < 0) || MyMod.SaveExitPending ||
			(commonHours > 0 && !NativeSleepAllowed(explain: true)))
		{
			CancelWaiting(explain: false);
			return;
		}
		int num = requestedHours;
		int num2 = commonHours;
		waiting = false;
		requestedHours = 0;
		MyMod.WaitForSleepLable.SetActive(value: false);
		if (MyMod.SleepingButtons != null)
		{
			MyMod.SleepingButtons.SetActive(value: true);
		}
		if (commonHours < 0)
		{
			CoopFatigue.ApplyPassTime(-commonHours);
			panel_Rest.Enable(false);
			HUDMessage.AddMessage("Совместное ожидание: " + Math.Abs(commonHours) + " ч");
			MyMod.SyncSleeping();
			return;
		}
		panel_Rest.m_SleepHours = num2;
		MyMod.MyCycleSkip = num2;
		Status = "Начинаем сон";
		MelonLogger.Msg("[Sleep wait] All participants ready; starting native rest, commonHours=" + num2 + ", requestedHours=" + num);
		GroupStart = true;
		try
		{
			panel_Rest.OnRest();
		}
		finally
		{
			GroupStart = false;
		}
		MyMod.SyncSleeping();
	}

	private static void SetLabel(string message)
	{
		if (!(MyMod.WaitForSleepLable == null))
		{
			UILabel component = MyMod.WaitForSleepLable.GetComponent<UILabel>();
			if (component != null)
			{
				component.text = message;
			}
		}
	}

	public static void Reset()
	{
		waiting = false;
		requestedHours = 0;
		lastClickFrame = -1;
		offeredHours = 0;
		offerText = null;
		lastOffer = 0f;
		ClearVotes();
		if (restNotice != null) restNotice.gameObject.SetActive(false);
		boundRoot = null;
		childSignature = 0;
		clickActions = null;
		boundButtonIds.Clear();
		Status = "";
		if (MyMod.WaitForSleepLable != null)
		{
			MyMod.WaitForSleepLable.SetActive(value: false);
		}
		if (MyMod.SleepingButtons != null)
		{
			MyMod.SleepingButtons.SetActive(value: true);
		}
	}
}
