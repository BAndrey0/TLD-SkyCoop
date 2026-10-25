using Il2CppTLD.PDID;
using UnityEngine;

namespace SkyCoop;

public static class ObjectGuidManager
{
	public static GameObject Lookup(string guid)
	{
		if (string.IsNullOrEmpty(guid))
		{
			return null;
		}
		return PdidTable.GetGameObject(guid);
	}

	public static string GenerateNewGuidString()
	{
		return PdidTable.GenerateNewID();
	}

	public static void UnRegisterGuid(string guid)
	{
		if (!string.IsNullOrEmpty(guid))
		{
			PdidTable.RuntimeUnregister(guid);
		}
	}
}
