using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace TinyJSON;

public static class JSON
{
	private static readonly Type includeAttrType = typeof(Include);

	private static readonly Type excludeAttrType = typeof(Exclude);

	private static readonly Type decodeAliasAttrType = typeof(DecodeAlias);

	private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

	private const BindingFlags instanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private const BindingFlags staticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly MethodInfo decodeTypeMethod = typeof(JSON).GetMethod("DecodeType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly MethodInfo decodeListMethod = typeof(JSON).GetMethod("DecodeList", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly MethodInfo decodeDictionaryMethod = typeof(JSON).GetMethod("DecodeDictionary", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly MethodInfo decodeArrayMethod = typeof(JSON).GetMethod("DecodeArray", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly MethodInfo decodeMultiRankArrayMethod = typeof(JSON).GetMethod("DecodeMultiRankArray", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

	public static Variant Load(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			throw new ArgumentNullException("json");
		}
		return Decoder.Decode(json);
	}

	public static string Dump(object data)
	{
		return Dump(data, EncodeOptions.None);
	}

	public static string Dump(object data, EncodeOptions options)
	{
		if (data != null)
		{
			Type type = data.GetType();
			if (!type.IsEnum && !type.IsPrimitive && !type.IsArray)
			{
				MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (MethodInfo methodInfo in methods)
				{
					if (methodInfo.GetCustomAttributes(inherit: false).AnyOfType(typeof(BeforeEncode)) && methodInfo.GetParameters().Length == 0)
					{
						methodInfo.Invoke(data, null);
					}
				}
			}
		}
		return Encoder.Encode(data, options);
	}

	public static void MakeInto<T>(Variant data, out T item)
	{
		item = DecodeType<T>(data);
	}

	public static void Populate<T>(Variant data, T item) where T : class
	{
		if (item == null)
		{
			throw new ArgumentNullException("item");
		}
		DecodeFields(data, ref item);
	}

	private static Type FindType(string fullName)
	{
		if (fullName == null)
		{
			return null;
		}
		if (typeCache.TryGetValue(fullName, out var value))
		{
			return value;
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			value = assemblies[i].GetType(fullName);
			if (value != null)
			{
				typeCache.Add(fullName, value);
				return value;
			}
		}
		return null;
	}

	private static T DecodeType<T>(Variant data)
	{
		if (data == null)
		{
			return default(T);
		}
		Type typeFromHandle = typeof(T);
		if (typeFromHandle.IsEnum)
		{
			return (T)Enum.Parse(typeFromHandle, data.ToString(CultureInfo.InvariantCulture));
		}
		if (typeFromHandle.IsPrimitive || typeFromHandle == typeof(string) || typeFromHandle == typeof(decimal))
		{
			return (T)Convert.ChangeType(data, typeFromHandle);
		}
		if (typeFromHandle == typeof(Guid))
		{
			return (T)(object)new Guid(data.ToString(CultureInfo.InvariantCulture));
		}
		if (typeFromHandle.IsArray)
		{
			if (typeFromHandle.GetArrayRank() == 1)
			{
				return (T)decodeArrayMethod.MakeGenericMethod(typeFromHandle.GetElementType()).Invoke(null, new object[1] { data });
			}
			if (!(data is ProxyArray proxyArray))
			{
				throw new DecodeException("Variant is expected to be a ProxyArray here, but it is not.");
			}
			int[] array = new int[typeFromHandle.GetArrayRank()];
			if (proxyArray.CanBeMultiRankArray(array))
			{
				Type elementType = typeFromHandle.GetElementType();
				if (elementType == null)
				{
					throw new DecodeException("Array element type is expected to be not null, but it is.");
				}
				Array array2 = Array.CreateInstance(elementType, array);
				MethodInfo methodInfo = decodeMultiRankArrayMethod.MakeGenericMethod(elementType);
				try
				{
					methodInfo.Invoke(null, new object[4] { proxyArray, array2, 1, array });
				}
				catch (Exception innerException)
				{
					throw new DecodeException("Error decoding multidimensional array. Did you try to decode into an array of incompatible rank or element type?", innerException);
				}
				return (T)Convert.ChangeType(array2, typeof(T));
			}
			throw new DecodeException("Error decoding multidimensional array; JSON data doesn't seem fit this structure.");
		}
		if (typeof(IList).IsAssignableFrom(typeFromHandle))
		{
			return (T)decodeListMethod.MakeGenericMethod(typeFromHandle.GetGenericArguments()).Invoke(null, new object[1] { data });
		}
		if (typeof(IDictionary).IsAssignableFrom(typeFromHandle))
		{
			return (T)decodeDictionaryMethod.MakeGenericMethod(typeFromHandle.GetGenericArguments()).Invoke(null, new object[1] { data });
		}
		string typeHint = ((data as ProxyObject) ?? throw new InvalidCastException("ProxyObject expected when decoding into '" + typeFromHandle.FullName + "'.")).TypeHint;
		T instance;
		if (typeHint != null && typeHint != typeFromHandle.FullName)
		{
			Type type = FindType(typeHint);
			if (type == null)
			{
				throw new TypeLoadException("Could not load type '" + typeHint + "'.");
			}
			if (!typeFromHandle.IsAssignableFrom(type))
			{
				throw new InvalidCastException("Cannot assign type '" + typeHint + "' to type '" + typeFromHandle.FullName + "'.");
			}
			instance = (T)Activator.CreateInstance(type);
			typeFromHandle = type;
		}
		else
		{
			instance = Activator.CreateInstance<T>();
		}
		DecodeFields(data, ref instance);
		return instance;
	}

	private static void DecodeFields<T>(Variant data, ref T instance)
	{
		Type typeFromHandle = typeof(T);
		foreach (KeyValuePair<string, Variant> item in (IEnumerable<KeyValuePair<string, Variant>>)((data as ProxyObject) ?? throw new InvalidCastException("ProxyObject expected when decoding into '" + typeFromHandle.FullName + "'.")))
		{
			FieldInfo fieldInfo = typeFromHandle.GetField(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (fieldInfo == null)
			{
				FieldInfo[] fields = typeFromHandle.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo2 in fields)
				{
					object[] customAttributes = fieldInfo2.GetCustomAttributes(inherit: true);
					foreach (object obj in customAttributes)
					{
						if (decodeAliasAttrType.IsInstanceOfType(obj) && ((DecodeAlias)obj).Contains(item.Key))
						{
							fieldInfo = fieldInfo2;
							break;
						}
					}
				}
			}
			if (fieldInfo != null)
			{
				bool flag = fieldInfo.IsPublic;
				object[] customAttributes = fieldInfo.GetCustomAttributes(inherit: true);
				foreach (object o in customAttributes)
				{
					if (excludeAttrType.IsInstanceOfType(o))
					{
						flag = false;
					}
					if (includeAttrType.IsInstanceOfType(o))
					{
						flag = true;
					}
				}
				if (flag)
				{
					MethodInfo methodInfo = decodeTypeMethod.MakeGenericMethod(fieldInfo.FieldType);
					if (typeFromHandle.IsValueType)
					{
						object obj2 = instance;
						fieldInfo.SetValue(obj2, methodInfo.Invoke(null, new object[1] { item.Value }));
						instance = (T)obj2;
					}
					else
					{
						fieldInfo.SetValue(instance, methodInfo.Invoke(null, new object[1] { item.Value }));
					}
				}
			}
			PropertyInfo propertyInfo = typeFromHandle.GetProperty(item.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (propertyInfo == null)
			{
				PropertyInfo[] properties = typeFromHandle.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (PropertyInfo propertyInfo2 in properties)
				{
					object[] customAttributes = propertyInfo2.GetCustomAttributes(inherit: false);
					foreach (object obj3 in customAttributes)
					{
						if (decodeAliasAttrType.IsInstanceOfType(obj3) && ((DecodeAlias)obj3).Contains(item.Key))
						{
							propertyInfo = propertyInfo2;
							break;
						}
					}
				}
			}
			if (propertyInfo != null && propertyInfo.CanWrite && propertyInfo.GetCustomAttributes(inherit: false).AnyOfType(includeAttrType))
			{
				MethodInfo methodInfo2 = decodeTypeMethod.MakeGenericMethod(propertyInfo.PropertyType);
				if (typeFromHandle.IsValueType)
				{
					object obj4 = instance;
					propertyInfo.SetValue(obj4, methodInfo2.Invoke(null, new object[1] { item.Value }), null);
					instance = (T)obj4;
				}
				else
				{
					propertyInfo.SetValue(instance, methodInfo2.Invoke(null, new object[1] { item.Value }), null);
				}
			}
		}
		MethodInfo[] methods = typeFromHandle.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo3 in methods)
		{
			if (methodInfo3.GetCustomAttributes(inherit: false).AnyOfType(typeof(AfterDecode)))
			{
				methodInfo3.Invoke(instance, (methodInfo3.GetParameters().Length == 0) ? null : new object[1] { data });
			}
		}
	}

	private static List<T> DecodeList<T>(Variant data)
	{
		List<T> list = new List<T>();
		foreach (Variant item in (IEnumerable<Variant>)((data as ProxyArray) ?? throw new DecodeException("Variant is expected to be a ProxyArray here, but it is not.")))
		{
			list.Add(DecodeType<T>(item));
		}
		return list;
	}

	private static Dictionary<TKey, TValue> DecodeDictionary<TKey, TValue>(Variant data)
	{
		Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
		Type typeFromHandle = typeof(TKey);
		foreach (KeyValuePair<string, Variant> item in (IEnumerable<KeyValuePair<string, Variant>>)((data as ProxyObject) ?? throw new DecodeException("Variant is expected to be a ProxyObject here, but it is not.")))
		{
			TKey key = (TKey)(typeFromHandle.IsEnum ? Enum.Parse(typeFromHandle, item.Key) : Convert.ChangeType(item.Key, typeFromHandle));
			TValue value = DecodeType<TValue>(item.Value);
			dictionary.Add(key, value);
		}
		return dictionary;
	}

	private static T[] DecodeArray<T>(Variant data)
	{
		ProxyArray obj = (data as ProxyArray) ?? throw new DecodeException("Variant is expected to be a ProxyArray here, but it is not.");
		T[] array = new T[obj.Count];
		int num = 0;
		foreach (Variant item in (IEnumerable<Variant>)obj)
		{
			array[num++] = DecodeType<T>(item);
		}
		return array;
	}

	private static void DecodeMultiRankArray<T>(ProxyArray arrayData, Array array, int arrayRank, int[] indices)
	{
		int count = arrayData.Count;
		for (int i = 0; i < count; i++)
		{
			indices[arrayRank - 1] = i;
			if (arrayRank < array.Rank)
			{
				DecodeMultiRankArray<T>(arrayData[i] as ProxyArray, array, arrayRank + 1, indices);
			}
			else
			{
				array.SetValue(DecodeType<T>(arrayData[i]), indices);
			}
		}
	}

	public static void SupportTypeForAOT<T>()
	{
		DecodeType<T>(null);
		DecodeList<T>(null);
		DecodeArray<T>(null);
		DecodeDictionary<short, T>(null);
		DecodeDictionary<ushort, T>(null);
		DecodeDictionary<int, T>(null);
		DecodeDictionary<uint, T>(null);
		DecodeDictionary<long, T>(null);
		DecodeDictionary<ulong, T>(null);
		DecodeDictionary<float, T>(null);
		DecodeDictionary<double, T>(null);
		DecodeDictionary<decimal, T>(null);
		DecodeDictionary<bool, T>(null);
		DecodeDictionary<string, T>(null);
	}

	private static void SupportValueTypesForAOT()
	{
		SupportTypeForAOT<short>();
		SupportTypeForAOT<ushort>();
		SupportTypeForAOT<int>();
		SupportTypeForAOT<uint>();
		SupportTypeForAOT<long>();
		SupportTypeForAOT<ulong>();
		SupportTypeForAOT<float>();
		SupportTypeForAOT<double>();
		SupportTypeForAOT<decimal>();
		SupportTypeForAOT<bool>();
		SupportTypeForAOT<string>();
	}
}
