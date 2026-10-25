using System;

namespace TinyJSON;

internal static class TinyJSONExtensions
{
	public static bool AnyOfType(this object[] attributes, Type type)
	{
		if (attributes == null)
		{
			return false;
		}
		foreach (object obj in attributes)
		{
			if (obj != null && type.IsAssignableFrom(obj.GetType()))
			{
				return true;
			}
		}
		return false;
	}
}
