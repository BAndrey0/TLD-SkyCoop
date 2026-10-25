using System;
using System.Diagnostics;
using GameServer;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public static class CoopContainerFlow
{
	[HarmonyPatch(typeof(ContainerInteraction), "EndHold")]
	private static class CancelUnfinishedSearch
	{
		private static void Postfix(ContainerInteraction __instance)
		{
			if (request != null && request == __instance.m_Container && !activated)
			{
				CancelPending(failed: false);
			}
		}
	}

	private static readonly Stopwatch Clock = Stopwatch.StartNew();

	private static Container request;

	private static Container presentation;

	private static Container reopen;

	private static string scene;

	private static string buffered;

	private static bool ready;

	private static bool activated;

	private static bool finishRequested;

	private static double deadline;

	private static double started;

	private static double nextRetry;

	private static int closeAfterFrame;

	private static int legacyInputThroughFrame = -1;

	public static bool Enabled => true;

	public static bool HasActiveSearch
	{
		get
		{
			if (!(request != null))
			{
				return presentation != null;
			}
			return true;
		}
	}

	public static bool HandlesContainerInspectInput
	{
		get
		{
			if (!(presentation != null))
			{
				return Time.frameCount <= legacyInputThroughFrame;
			}
			return true;
		}
	}

	public static bool CanStartInteraction(Container box)
	{
		if (!MyMod.SaveExitPending && !CoopReturnToMenu.InProgress && !ContainerLootSync.HasPendingUpload && presentation == null)
		{
			if (!(request == null))
			{
				return request == box;
			}
			return true;
		}
		return false;
	}

	public static bool IsPresenting(Container box)
	{
		if (presentation != null)
		{
			return presentation == box;
		}
		return false;
	}

	public static bool NativePresentationAllowed(Container box)
	{
		if (IsPresenting(box))
		{
			return !finishRequested;
		}
		return false;
	}

	public static object Snapshot()
	{
		return new
		{
			pending = (request != null),
			ready = ready,
			activated = activated,
			presenting = (presentation != null),
			finishRequested = finishRequested,
			uploadPending = ContainerLootSync.HasPendingUpload,
			guid = ((presentation != null) ? presentation : request)?.GetComponent<ObjectGuid>()?.Get(),
			elapsed = ((request != null) ? (Clock.Elapsed.TotalSeconds - started) : 0.0)
		};
	}

	public static void Prefetch(Container box)
	{
		if (!(box == null) && MyMod.sendMyPosition && !ContainerLootSync.HasPendingUpload && !(request == box) && !(request != null) && !(presentation != null))
		{
			request = box;
			scene = MyMod.level_guid;
			buffered = null;
			ready = false;
			activated = false;
			started = Clock.Elapsed.TotalSeconds;
			deadline = started + 30.0;
			MyMod.GoingToOpenContinaer = box;
			SendRequest();
		}
	}

	private static void SendRequest()
	{
		if (request == null)
		{
			return;
		}
		using Packet packet = new Packet(94);
		packet.Write(scene);
		packet.Write(request.GetComponent<ObjectGuid>().Get());
		packet.Write(ContainerLootSync.NativeSnapshot(request));
		MyMod.SendUDPData(packet);
		nextRetry = Clock.Elapsed.TotalSeconds + 3.0;
	}

	public static void RetryPending()
	{
		if (request != null && !ready && scene == MyMod.level_guid && MyMod.sendMyPosition)
		{
			SendRequest();
		}
	}

	public static bool Activate(Container box)
	{
		if (!MyMod.sendMyPosition)
		{
			return false;
		}
		if (request == null)
		{
			Prefetch(box);
		}
		if (request != box)
		{
			return true;
		}
		activated = true;
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		if (playerManagerComponent != null)
		{
			playerManagerComponent.SetControlMode(PlayerControlMode.Locked);
			playerManagerComponent.m_Container = null;
		}
		GameCompat.Panel<Panel_HUD>()?.CancelItemProgressBar();
		if (ready)
		{
			Deliver();
		}
		return true;
	}

	public static bool BufferReply(string data)
	{
		if (request == null)
		{
			return false;
		}
		buffered = data;
		ready = true;
		if (MyMod.LastPacketForRepeat == MyMod.ResendPacketType.Container)
		{
			MyMod.DiscardRepeatPacket();
		}
		MelonLogger.Msg("[Container flow] Authoritative response after " + (Clock.Elapsed.TotalSeconds - started).ToString("F3") + "s; searchFinished=" + activated);
		if (activated)
		{
			Deliver();
		}
		return true;
	}

	private static void Deliver()
	{
		if (!(request == null) && ready)
		{
			object obj = buffered;
			request = null;
			buffered = null;
			ready = false;
			activated = false;
			if (obj == null)
			{
				obj = "";
			}
			MyMod.FinishOpeningFakeContainer((string)obj);
		}
	}

	public static void MarkReopen(Container box)
	{
		if (!IsPresenting(box))
		{
			reopen = box;
		}
	}

	public static void ClearReopen(Container box)
	{
		if (reopen == box)
		{
			reopen = null;
		}
	}

	public static void Present(Container box)
	{
		// An already searched container opens the native transfer panel so items can be put back in;
		// Panel_Container.OnDone -> CloseFakeContainer saves and uploads the contents.
		if (box != null && reopen == box)
		{
			reopen = null;
			Panel_Container panel = GameCompat.Panel<Panel_Container>();
			panel.SetContainer(box, box.m_LocalizedDisplayName.Text());
			panel.Enable(enable: true, resetFilter: true, null);
			return;
		}
		presentation = box;
		finishRequested = false;
		if (box == null)
		{
			return;
		}
		if (box.IsEmpty())
		{
			HUDMessage.AddMessage("Пусто");
		}
		List<Il2CppTLD.Gear.GearItemObject>.Enumerator enumerator = box.m_Items.GetEnumerator();
		while (enumerator.MoveNext())
		{
			GearItem gearItem = enumerator.Current;
			if (gearItem != null)
			{
				gearItem.m_BeenInspected = false;
			}
		}
		box.ShowItemsAfterSearch();
	}

	public static bool CloseFromNative(Container box)
	{
		if (request == box && !activated)
		{
			ClearRequest();
			return false;
		}
		if (!IsPresenting(box))
		{
			return false;
		}
		presentation = null;
		finishRequested = false;
		legacyInputThroughFrame = Time.frameCount + 2;
		MyMod.CloseFakeContainer(box);
		return true;
	}

	public static bool StopNativeAdvance(Container box)
	{
		if (!IsPresenting(box) || !finishRequested)
		{
			return false;
		}
		closeAfterFrame = Math.Max(closeAfterFrame, Time.frameCount + 1);
		return true;
	}

	public static void FinishSearch()
	{
		if (request != null)
		{
			CancelPending(failed: false);
		}
		if (presentation == null || finishRequested)
		{
			return;
		}
		finishRequested = true;
		closeAfterFrame = Time.frameCount + 1;
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		if (playerManagerComponent != null && playerManagerComponent.IsInspectModeActive())
		{
			playerManagerComponent.OnPutBackFromContainerInspection();
			if (playerManagerComponent.IsInspectModeActive())
			{
				playerManagerComponent.ExitInspectGearMode(hardExit: false);
			}
		}
	}

	public static void Tick()
	{
		if (request != null)
		{
			if (!MyMod.sendMyPosition || scene != MyMod.level_guid)
			{
				CancelPending(failed: false);
				return;
			}
			Transform playerTransform = GameManager.GetPlayerTransform();
			if (Clock.Elapsed.TotalSeconds > deadline)
			{
				CancelPending(failed: true);
				return;
			}
			if (!ready && Clock.Elapsed.TotalSeconds >= nextRetry)
			{
				SendRequest();
			}
			if (activated && !ready)
			{
				GameManager.GetPlayerManagerComponent()?.SetControlMode(PlayerControlMode.Locked);
			}
			if (activated && playerTransform != null && Vector3.Distance(playerTransform.position, request.transform.position) > 6f)
			{
				CancelPending(failed: false);
				return;
			}
			if (activated && KeyboardUtilities.InputManager.GetKeyDown(KeyCode.Escape))
			{
				CancelPending(failed: false);
				return;
			}
		}
		if (presentation != null && finishRequested && Time.frameCount >= closeAfterFrame)
		{
			PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
			if (!(playerManagerComponent != null) || !playerManagerComponent.IsInspectModeActive())
			{
				CloseFromNative(presentation);
			}
		}
	}

	private static void CancelPending(bool failed)
	{
		Container container = request;
		ClearRequest();
		if (container != null)
		{
			container.m_SearchInProgress = false;
			container.m_OpenInProgress = false;
			PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
			if (playerManagerComponent != null)
			{
				playerManagerComponent.m_Container = null;
				playerManagerComponent.SetControlMode(PlayerControlMode.Normal);
			}
			GameCompat.Panel<Panel_HUD>()?.CancelItemProgressBar();
			container.BeginContainerClose();
		}
		if (failed)
		{
			HUDMessage.AddMessage("Не удалось закончить обыск. Попробуйте ещё раз.");
		}
	}

	private static void ClearRequest()
	{
		if (request != null && !string.IsNullOrEmpty(scene))
		{
			Shared.Base64Slices.Remove(request.GetComponent<ObjectGuid>().Get() + "|" + scene);
		}
		if (MyMod.GoingToOpenContinaer == request)
		{
			MyMod.GoingToOpenContinaer = null;
		}
		request = null;
		buffered = null;
		ready = false;
		activated = false;
		if (MyMod.LastPacketForRepeat == MyMod.ResendPacketType.Container)
		{
			MyMod.DiscardRepeatPacket();
		}
	}

	public static void Rejected(Container box)
	{
		if (request == box)
		{
			ClearRequest();
		}
	}

	public static void RetryCorrupted(string packetScene, string guid)
	{
		if (!(request == null) && !(scene != packetScene) && !(request.GetComponent<ObjectGuid>().Get() != guid))
		{
			Shared.Base64Slices.Remove(guid + "|" + packetScene);
			ready = false;
			buffered = null;
			nextRetry = Clock.Elapsed.TotalSeconds + 0.5;
			MelonLogger.Warning("[Container flow] Re-requesting invalid snapshot; scene=" + packetScene + " guid=" + guid);
		}
	}

	public static void Reset()
	{
		request = null;
		presentation = null;
		reopen = null;
		scene = null;
		buffered = null;
		ready = (activated = (finishRequested = false));
		legacyInputThroughFrame = -1;
	}

	public static void Draw()
	{
		if (request != null && activated && !ready)
		{
			GUI.Label(new Rect((float)Screen.width / 2f - 85f, (float)Screen.height / 2f + 65f, 220f, 28f), "Осматриваем…  Esc — отмена");
		}
		else if (ContainerLootSync.HasPendingUpload)
		{
			GUI.Label(new Rect((float)Screen.width / 2f - 85f, (float)Screen.height / 2f + 65f, 220f, 28f), "Заканчиваем обыск…");
		}
		if (presentation != null && !finishRequested && GUI.Button(new Rect(Screen.width - 235, Screen.height - 70, 215f, 35f), "Закончить обыск"))
		{
			FinishSearch();
		}
	}
}
