using System;
using System.Text;
using Il2Cpp;
using MelonLoader;

namespace SkyCoop;

public static class ContainerLootSync
{
	private static Container openBox;

	private static string openScene;

	private static string openGuid;

	private static bool closeClaimed;

	private static bool uploadPending;

	private static bool closeUpload;

	private static string uploadScene;

	private static string uploadGuid;

	private static string uploadChecksum;

	public static bool HasPendingUpload => uploadPending;

	public static void AcceptedOpen(Container box)
	{
		openBox = box;
		openScene = MyMod.level_guid;
		openGuid = box.GetComponent<ObjectGuid>().Get();
		closeClaimed = false;
		uploadPending = false;
		closeUpload = false;
	}

	public static bool TryBeginClose(Container box)
	{
		if (box == null || openBox != box || openScene != MyMod.level_guid || openGuid != box.GetComponent<ObjectGuid>().Get() || closeClaimed)
		{
			MelonLogger.Msg("[Container loot] Ignored duplicate/stale close");
			return false;
		}
		closeClaimed = true;
		return true;
	}

	public static void AwaitUpload()
	{
		closeUpload = true;
	}

	public static void TrackUpload(string scene, string guid, string compressed)
	{
		closeUpload = closeUpload && closeClaimed && scene == openScene && guid == openGuid;
		uploadScene = scene;
		uploadGuid = guid;
		uploadChecksum = Shared.SHA256CheckSum(compressed);
		uploadPending = true;
	}

	public static bool UploadMatches(string scene, string guid, string checksum)
	{
		if (HasPendingUpload && uploadScene == scene && uploadGuid == guid)
		{
			return uploadChecksum == checksum;
		}
		return false;
	}

	public static void CompleteUpload()
	{
		if (!HasPendingUpload)
		{
			return;
		}
		uploadPending = false;
		Container container = openBox;
		if (closeUpload && container != null && openScene == MyMod.level_guid)
		{
			container.BeginContainerClose();
			if (container.m_CloseAudio.Length == 0)
			{
				GameAudioManager.PlayGUIButtonBack();
			}
			GameManager.GetPlayerManagerComponent().MaybeRevealPolaroidDiscoveryOnClose();
			Panel_Container panel_Container = GameCompat.Panel<Panel_Container>();
			if (panel_Container.m_Container == container)
			{
				panel_Container.Enable(enable: false);
			}
		}
		closeUpload = false;
		Shared.ContainerDecompressedDataBackup = "";
		Shared.ContainerGUIDDataBackup = "";
	}

	public static bool MatchesPending(string scene, string guid)
	{
		Container goingToOpenContinaer = MyMod.GoingToOpenContinaer;
		if (goingToOpenContinaer != null && scene == MyMod.level_guid)
		{
			return goingToOpenContinaer.GetComponent<ObjectGuid>()?.Get() == guid;
		}
		return false;
	}

	public static bool Reserve(int client, string scene, string guid)
	{
		if (MyMod.MyContainer != null && MyMod.MyContainer.m_LevelGUID == scene && MyMod.MyContainer.m_Guid == guid)
		{
			return false;
		}
		for (int i = 1; i < MyMod.playersData.Count; i++)
		{
			if (i != client)
			{
				DataStr.ContainerOpenSync containerOpenSync = MyMod.playersData[i]?.m_Container;
				if (containerOpenSync != null && containerOpenSync.m_LevelGUID == scene && containerOpenSync.m_Guid == guid)
				{
					return false;
				}
			}
		}
		MyMod.playersData[client].m_Container = new DataStr.ContainerOpenSync
		{
			m_Guid = guid,
			m_LevelGUID = scene,
			m_LevelID = MyMod.playersData[client].m_Levelid
		};
		return true;
	}

	public static void RejectPending(string scene, string guid)
	{
		if (MatchesPending(scene, guid))
		{
			Container goingToOpenContinaer = MyMod.GoingToOpenContinaer;
			CoopContainerFlow.Rejected(goingToOpenContinaer);
			MyMod.GoingToOpenContinaer = null;
			if (MyMod.LastPacketForRepeat == MyMod.ResendPacketType.Container)
			{
				MyMod.DiscardRepeatPacket();
			}
			GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Normal);
			GameManager.GetPlayerManagerComponent().m_Container = null;
			goingToOpenContinaer.m_SearchInProgress = false;
			goingToOpenContinaer.m_OpenInProgress = false;
			goingToOpenContinaer.BeginContainerClose();
			HUDMessage.AddMessage("Контейнер уже осматривают");
		}
	}

	public static string NativeSnapshot(Container box)
	{
		if (box == null)
		{
			return "";
		}
		if (box.m_NotPopulated)
		{
			box.PopulateContents();
		}
		box.InstantiateContents();
		string text = box.Serialize();
		MelonLogger.Msg("[Container loot] Native snapshot guid=" + box.GetComponent<ObjectGuid>()?.Get() + " items=" + box.m_Items.Count);
		string text2 = Shared.CompressString(text);
		if (Encoding.UTF8.GetByteCount(text2) > 48000)
		{
			throw new InvalidOperationException("Initial container snapshot exceeds safe packet size");
		}
		return text2;
	}

	public static string GetOrInitialize(string scene, string guid, string initial)
	{
		int containerState = MPSaveManager.GetContainerState(scene, guid);
		if (containerState == 0)
		{
			return "";
		}
		string text = MPSaveManager.LoadContainer(scene, guid);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (containerState != -1 && containerState != 2)
		{
			return "";
		}
		if (string.IsNullOrEmpty(initial))
		{
			return "";
		}
		if (Utils.DeserializeObject<ContainerSaveDataProxy>(Shared.DecompressString(initial)) == null)
		{
			throw new InvalidOperationException("Invalid initial native container snapshot");
		}
		MPSaveManager.SaveContainer(scene, guid, initial);
		MPSaveManager.SetConstainerState(scene, guid, 1, DontNotify: true);
		MelonLogger.Msg("[Container loot] Initialized first search scene=" + scene + " guid=" + guid);
		return initial;
	}
}
