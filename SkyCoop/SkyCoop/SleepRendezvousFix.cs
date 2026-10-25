using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class SleepRendezvousFix
{
	private static GameObject boundRoot;

	private static int childSignature;

	private static int lastClickFrame = -1;

	private static int requestedHours;

	private static bool waiting;

	private static EventDelegate.Callback waitCallback;

	private static Il2CppSystem.Collections.Generic.List<EventDelegate> clickActions;

	private static readonly HashSet<int> boundButtonIds = new HashSet<int>();

	public static bool Waiting
	{
		get
		{
			if (waiting && MyMod.WaitForSleepLable != null)
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
		bool flag = panel.isActiveAndEnabled && !panel.m_ShowPassTime && MyMod.InOnline();
		new_button.SetActive(flag);
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
		if (waiting)
		{
			if (MyMod.WaitForSleepLable == null || !MyMod.WaitForSleepLable.activeSelf)
			{
				waiting = false;
				requestedHours = 0;
			}
			else if (panel.m_ShowPassTime || MyMod.SaveExitPending || !NativeSleepAllowed(explain: false))
			{
				CancelWaiting(explain: false);
				NativeSleepAllowed(explain: true);
			}
			else
			{
				panel.m_SleepHours = requestedHours;
				MyMod.MyCycleSkip = requestedHours;
			}
		}
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

	public static void ClickWait()
	{
		if (lastClickFrame == Time.frameCount)
		{
			return;
		}
		lastClickFrame = Time.frameCount;
		Panel_Rest panel_Rest = GameCompat.Panel<Panel_Rest>();
		if (!(panel_Rest != null) || !panel_Rest.isActiveAndEnabled || panel_Rest.m_ShowPassTime || !MyMod.InOnline() || MyMod.SaveExitPending || waiting)
		{
			return;
		}
		if (panel_Rest.m_SleepHours <= 0)
		{
			HUDMessage.AddMessage("Выбери хотя бы один час сна.");
		}
		else
		{
			if (!NativeSleepAllowed(explain: true))
			{
				return;
			}
			if (MyMod.WaitForSleepLable == null)
			{
				HUDMessage.AddMessage("Окно ожидания сна ещё не готово.");
				return;
			}
			requestedHours = panel_Rest.m_SleepHours;
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
			if (MyMod.InOnline() && GameManager.GetPlayerManagerComponent() != null)
			{
				MyMod.SyncSleeping();
			}
			Status = "Ожидание сна отменено";
			if (explain)
			{
				HUDMessage.AddMessage(Status);
			}
			MelonLogger.Msg("[Sleep wait] Waiting cancelled.");
		}
	}

	public static void ProcessReadiness(int sleepers, int required, bool everyoneReady, int commonHours)
	{
		if (!Waiting)
		{
			return;
		}
		Status = "ЖДЁМ ОСТАЛЬНЫХ ИГРОКОВ\nГотовы: " + sleepers + "/" + required + "\nESC — отмена";
		SetLabel(Status);
		if (!everyoneReady)
		{
			return;
		}
		if (commonHours <= 0 || commonHours > requestedHours)
		{
			Status = "ЖДЁМ ПОДТВЕРЖДЕНИЯ ВРЕМЕНИ СНА\nESC — отмена";
			SetLabel(Status);
			return;
		}
		Panel_Rest panel_Rest = GameCompat.Panel<Panel_Rest>();
		if (panel_Rest == null || !panel_Rest.isActiveAndEnabled || panel_Rest.m_ShowPassTime || MyMod.SaveExitPending || !NativeSleepAllowed(explain: true))
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
		panel_Rest.m_SleepHours = num2;
		MyMod.MyCycleSkip = num2;
		Status = "Начинаем сон";
		MelonLogger.Msg("[Sleep wait] All participants ready; starting native rest, commonHours=" + num2 + ", requestedHours=" + num);
		panel_Rest.OnRest();
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
