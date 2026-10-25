using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.OptionalContent;
using MelonLoader;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace SkyCoop;

internal static class DroppedGearDiagnostics
{
	private static readonly HashSet<string> Notified = new HashSet<string>(StringComparer.Ordinal);

	private static string Safe(string value)
	{
		if (value == null)
		{
			return "<null>";
		}
		value = value.Replace('\r', ' ').Replace('\n', ' ');
		if (value.Length <= 180)
		{
			return value;
		}
		return value.Substring(0, 180);
	}

	private static string Identity(DataStr.DroppedGearItemDataPacket packet)
	{
		return "key=" + packet.m_Hash + " gear=" + Safe((packet.m_Extra != null) ? packet.m_Extra.m_GearName : null) + " packetScene=" + Safe(packet.m_LevelGUID) + " localScene=" + Safe(MyMod.level_guid);
	}

	public static void ObserveVisual(DataStr.DroppedGearItemDataPacket packet, bool journalReplay)
	{
		if (packet != null)
		{
			bool flag = packet.m_LevelGUID == MyMod.level_guid;
			MelonLogger.Msg("[Drop sync] visual event " + Identity(packet) + " role=" + (MyMod.iAmHost ? "host" : "guest") + " origin=" + (journalReplay ? "journal-replay" : "network-or-local-publication") + " sceneMatch=" + flag + (flag ? "" : " result=not-instantiated-in-another-scene"));
		}
	}

	private static string ContentState()
	{
		string text = "cachedSteamDlc2091330Installed=" + (SteamConnect.CanUseSteam ? SteamConnect.HasDLC.ToString() : "unknown");
		try
		{
			OptionalContentManager instance = OptionalContentManager.Instance;
			if (instance == null)
			{
				return text + " optionalManager=unavailable";
			}
			System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
			System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>();
			if (instance.OwnedContent != null)
			{
				Il2CppSystem.Collections.Generic.List<string>.Enumerator enumerator = instance.OwnedContent.GetEnumerator();
				while (enumerator.MoveNext())
				{
					string current = enumerator.Current;
					list.Add(Safe(current));
				}
			}
			if (instance.InstalledContent != null)
			{
				Il2CppSystem.Collections.Generic.Dictionary<string, IResourceLocator>.Enumerator enumerator2 = instance.InstalledContent.GetEnumerator();
				while (enumerator2.MoveNext())
				{
					Il2CppSystem.Collections.Generic.KeyValuePair<string, IResourceLocator> current2 = enumerator2.Current;
					list2.Add(Safe(current2.Key));
				}
			}
			return text + " optionalReady=" + instance.IsReadyToBeUsed + " optionalState=" + instance.State.ToString() + " ownedContent=[" + string.Join(",", list) + "] installedCatalogs=[" + string.Join(",", list2) + "]";
		}
		catch (Exception ex)
		{
			return text + " optionalState=unknown(" + Safe(ex.GetType().Name) + ")";
		}
	}

	private static void Notice(DataStr.DroppedGearItemDataPacket packet)
	{
		string text = ((packet.m_Extra != null) ? packet.m_Extra.m_GearName : null);
		string item = packet.m_LevelGUID + "\n" + text;
		if (!Notified.Add(item))
		{
			return;
		}
		try
		{
			HUDMessage.AddMessage("CO-OP: cannot display " + Safe(text) + ". See MelonLoader/Latest.log.", highPriority: true, ignoreOverlayActive: true);
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("[Drop sync] HUD notice unavailable: " + Safe(ex.GetType().Name));
		}
	}

	public static void ObserveResult(DataStr.DroppedGearItemDataPacket packet)
	{
		if (packet == null || packet.m_Extra == null || packet.m_Extra.m_Variant == -2)
		{
			return;
		}
		if (MyMod.DroppedGearsObjs.TryGetValue(packet.m_Hash, out var value) && value != null)
		{
			MelonLogger.Msg("[Drop sync] replica registered " + Identity(packet) + " active=" + value.activeInHierarchy);
			return;
		}
		string text = "unknown";
		try
		{
			text = ((MyMod.GetGearItemObject(packet.m_Extra.m_GearName) != null) ? "available" : "unavailable");
		}
		catch (Exception ex)
		{
			text = "unknown(" + Safe(ex.GetType().Name) + ")";
		}
		MelonLogger.Warning("[Drop sync] replica missing " + Identity(packet) + " nativePrefab=" + text + " " + ContentState() + " cause=UNKNOWN; unavailable/unready catalog or creation failure must be distinguished from the receiver log. No fallback item created.");
		Notice(packet);
	}

	public static void CreationFailed(DataStr.DroppedGearItemDataPacket packet, Exception exception)
	{
		if (packet != null)
		{
			MelonLogger.Warning("[Drop sync] creation threw " + Identity(packet) + " exception=" + Safe(exception.GetType().Name) + " message=" + Safe(exception.Message) + " " + ContentState());
			Notice(packet);
		}
	}
}
