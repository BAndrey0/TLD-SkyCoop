using System;
using GameServer;
using HarmonyLib;
using Il2Cpp;

namespace SkyCoop;

// Doors and lids that belong to a container (cabinets, lockers, wardrobes) are OpenClose objects of their own. The
// "someone is using this" check covered only the container, so the door could still be opened while the other player
// had the cabinet open, and the two animations broke the door a little more every time. Such doors cannot be used
// while another player has the container open, and their remote open/close is not replayed (see MyMod.ApplyOpenables).
public static class ContainerDoorGuard
{
	[HarmonyPatch(typeof(OpenClose), "PerformInteraction")]
	private static class BlockBusyContainerDoor
	{
		private static bool Prefix(OpenClose __instance, ref bool __result)
		{
			try
			{
				if (!MyMod.InOnline() || __instance == null || __instance.TryCast<OpenClose>() == null)
				{
					return true;
				}
				Container container = ContainerOf(__instance);
				string user = (container != null) ? OtherUser(container) : null;
				if (user == null)
				{
					return true;
				}
				HUDMessage.AddMessage(user + " IS USING THIS");
				GameAudioManager.PlayGUIError();
				__result = false;
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	public static Container ContainerOf(OpenClose door)
	{
		if (door == null)
		{
			return null;
		}
		Container container = door.GetComponent<Container>();
		if (container == null)
		{
			container = door.GetComponentInParent<Container>();
		}
		if (container == null)
		{
			container = door.GetComponentInChildren<Container>();
		}
		return container;
	}

	private static string OtherUser(Container container)
	{
		ObjectGuid guid = container.GetComponent<ObjectGuid>();
		if (guid == null)
		{
			return null;
		}
		string id = guid.Get();
		for (int i = 0; i < MyMod.playersData.Count; i++)
		{
			if (i == ClientUser.myId)
			{
				continue;
			}
			DataStr.ContainerOpenSync open = MyMod.playersData[i]?.m_Container;
			if (open != null && open.m_Guid == id && open.m_LevelGUID == MyMod.level_guid)
			{
				return MyMod.playersData[i].m_Name;
			}
		}
		return null;
	}
}
