using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using Il2CppSteamworks;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Threading;
using MelonLoader;
using Steamworks;

namespace SkyCoop;

public static class SharedSteamCallbacks
{
	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct Message
	{
		public int User;

		public int Id;

		public System.IntPtr Data;

		public int Size;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct Completed
	{
		public ulong Handle;

		public int Id;

		public uint Size;
	}

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate int GetPipe();

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void RunFrame(int pipe);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.I1)]
	private delegate bool Next(int pipe, System.IntPtr message);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate void Free(int pipe);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.I1)]
	private delegate bool Result(int pipe, ulong call, System.IntPtr data, int size, int id, [MarshalAs(UnmanagedType.I1)] out bool failed);

	[HarmonyPatch]
	private static class SharedPumpPatch
	{
		private static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.Method(typeof(Steamworks.SteamAPI).Assembly.GetType("Steamworks.CallbackDispatcher", throwOnError: true), "RunFrame");
			yield return AccessTools.Method(typeof(Il2CppSteamworks.CallbackDispatcher), "RunFrame");
		}

		[HarmonyPriority(int.MaxValue)]
		private static bool Prefix(bool __0, MethodBase __originalMethod)
		{
			if (!Enabled || !SteamConnect.CanUseSteam || __0)
			{
				return true;
			}
			if (__originalMethod.DeclaringType == typeof(Il2CppSteamworks.CallbackDispatcher))
			{
				GamePumpCalls++;
			}
			else
			{
				ManagedPumpCalls++;
			}
			Pump();
			return false;
		}
	}

	[HarmonyPatch]
	private static class ManagedExceptionLogPatch
	{
		private static MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(Steamworks.SteamAPI).Assembly.GetType("Steamworks.CallbackDispatcher", throwOnError: true), "ExceptionHandler");
		}

		private static bool Prefix(System.Exception __0)
		{
			MelonLogger.Error("[Steam callbacks] Handler failed: " + __0);
			return false;
		}
	}

	private static GetPipe getPipe;

	private static RunFrame run;

	private static Next next;

	private static Free free;

	private static Result getResult;

	private static System.IntPtr messageBuffer;

	private static int pumping;

	private static object registryLock;

	private static IDictionary managedCallbacks;

	private static IDictionary managedResults;

	private static MethodInfo managedCallback;

	private static MethodInfo managedResult;

	private static MethodInfo unregisterResult;

	public static long ManagedPumpCalls;

	public static long GamePumpCalls;

	public static long ManagedEvents;

	public static long GameEvents;

	public static long ManagedResults;

	public static long GameResults;

	public static bool Enabled { get; private set; }

	public static void Initialize(string library)
	{
		if (!Enabled)
		{
			Assembly assembly = typeof(Steamworks.SteamAPI).Assembly;
			System.Type? type = assembly.GetType("Steamworks.CallbackDispatcher", throwOnError: true);
			managedCallbacks = (IDictionary)type.GetField("m_registeredCallbacks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
			managedResults = (IDictionary)type.GetField("m_registeredCallResults", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
			registryLock = type.GetField("m_sync", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
			managedCallback = AccessTools.Method(typeof(Steamworks.Callback), "OnRunCallback");
			managedResult = AccessTools.Method(typeof(Steamworks.CallResult), "OnRunCallResult");
			unregisterResult = AccessTools.Method(typeof(Steamworks.CallResult), "SetUnregistered");
			if (Marshal.SizeOf<Message>() != Marshal.SizeOf(assembly.GetType("Steamworks.CallbackMsg_t", throwOnError: true)))
			{
				throw new System.InvalidOperationException("Steam callback ABI size mismatch");
			}
			System.IntPtr library2 = System.Runtime.InteropServices.NativeLibrary.Load(library);
			getPipe = Bind<GetPipe>(library2, "SteamAPI_GetHSteamPipe");
			run = Bind<RunFrame>(library2, "SteamAPI_ManualDispatch_RunFrame");
			next = Bind<Next>(library2, "SteamAPI_ManualDispatch_GetNextCallback");
			free = Bind<Free>(library2, "SteamAPI_ManualDispatch_FreeLastCallback");
			getResult = Bind<Result>(library2, "SteamAPI_ManualDispatch_GetAPICallResult");
			messageBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<Message>());
			Enabled = true;
			MelonLogger.Msg("[Steam callbacks] Shared game/mod dispatcher enabled.");
		}
	}

	private static T Bind<T>(System.IntPtr library, string name) where T : System.Delegate
	{
		return Marshal.GetDelegateForFunctionPointer<T>(System.Runtime.InteropServices.NativeLibrary.GetExport(library, name));
	}

	private static object[] ManagedSnapshot(IDictionary dictionary, object key, bool remove)
	{
		lock (registryLock)
		{
			if (!dictionary.Contains(key))
			{
				return System.Array.Empty<object>();
			}
			System.Collections.Generic.List<object> list = new System.Collections.Generic.List<object>();
			foreach (object item in (IEnumerable)dictionary[key])
			{
				list.Add(item);
			}
			if (remove)
			{
				dictionary.Remove(key);
			}
			return list.ToArray();
		}
	}

	private static void Invoke(System.Action action)
	{
		try
		{
			action();
		}
		catch (System.Exception ex)
		{
			MelonLogger.Error("[Steam callbacks] Dispatch failed: " + ((ex is TargetInvocationException ex2) ? ex2.InnerException : ex));
		}
	}

	private static Il2CppSteamworks.Callback[] GameCallbackSnapshot(int id)
	{
		if (!Il2CppSteamworks.CallbackDispatcher.IsInitialized)
		{
			return System.Array.Empty<Il2CppSteamworks.Callback>();
		}
		Il2CppSystem.Object sync = Il2CppSteamworks.CallbackDispatcher.m_sync;
		Il2CppSystem.Threading.Monitor.Enter(sync);
		try
		{
			Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.List<Il2CppSteamworks.Callback>> registeredCallbacks = Il2CppSteamworks.CallbackDispatcher.m_registeredCallbacks;
			if (registeredCallbacks == null || !registeredCallbacks.TryGetValue(id, out var value) || value == null)
			{
				return System.Array.Empty<Il2CppSteamworks.Callback>();
			}
			System.Collections.Generic.List<Il2CppSteamworks.Callback> list = new System.Collections.Generic.List<Il2CppSteamworks.Callback>();
			for (int i = 0; i < value.Count; i++)
			{
				list.Add(value[i]);
			}
			return list.ToArray();
		}
		finally
		{
			Il2CppSystem.Threading.Monitor.Exit(sync);
		}
	}

	private static Il2CppSteamworks.CallResult[] GameResultSnapshot(ulong handle)
	{
		if (!Il2CppSteamworks.CallbackDispatcher.IsInitialized)
		{
			return System.Array.Empty<Il2CppSteamworks.CallResult>();
		}
		Il2CppSystem.Object sync = Il2CppSteamworks.CallbackDispatcher.m_sync;
		Il2CppSystem.Threading.Monitor.Enter(sync);
		try
		{
			Il2CppSystem.Collections.Generic.Dictionary<ulong, Il2CppSystem.Collections.Generic.List<Il2CppSteamworks.CallResult>> registeredCallResults = Il2CppSteamworks.CallbackDispatcher.m_registeredCallResults;
			if (registeredCallResults == null || !registeredCallResults.TryGetValue(handle, out var value) || value == null)
			{
				return System.Array.Empty<Il2CppSteamworks.CallResult>();
			}
			System.Collections.Generic.List<Il2CppSteamworks.CallResult> list = new System.Collections.Generic.List<Il2CppSteamworks.CallResult>();
			for (int i = 0; i < value.Count; i++)
			{
				list.Add(value[i]);
			}
			registeredCallResults.Remove(handle);
			return list.ToArray();
		}
		finally
		{
			Il2CppSystem.Threading.Monitor.Exit(sync);
		}
	}

	private static void FinalizeManagedResult(object callback)
	{
		lock (registryLock)
		{
			foreach (IEnumerable value in managedResults.Values)
			{
				foreach (object item in value)
				{
					if (item == callback)
					{
						return;
					}
				}
			}
			unregisterResult.Invoke(callback, null);
		}
	}

	private static void FinalizeGameResult(Il2CppSteamworks.CallResult callback)
	{
		Il2CppSystem.Object sync = Il2CppSteamworks.CallbackDispatcher.m_sync;
		Il2CppSystem.Threading.Monitor.Enter(sync);
		try
		{
			Il2CppSystem.Collections.Generic.Dictionary<ulong, Il2CppSystem.Collections.Generic.List<Il2CppSteamworks.CallResult>> registeredCallResults = Il2CppSteamworks.CallbackDispatcher.m_registeredCallResults;
			if (registeredCallResults != null)
			{
				Il2CppSystem.Collections.Generic.Dictionary<ulong, Il2CppSystem.Collections.Generic.List<Il2CppSteamworks.CallResult>>.Enumerator enumerator = registeredCallResults.GetEnumerator();
				while (enumerator.MoveNext())
				{
					Il2CppSystem.Collections.Generic.List<Il2CppSteamworks.CallResult> value = enumerator.Current.Value;
					if (value == null)
					{
						continue;
					}
					for (int i = 0; i < value.Count; i++)
					{
						Il2CppSteamworks.CallResult callResult = value[i];
						if (callResult != null && callResult.Pointer == callback.Pointer)
						{
							return;
						}
					}
				}
			}
			callback.SetUnregistered();
		}
		finally
		{
			Il2CppSystem.Threading.Monitor.Exit(sync);
		}
	}

	private static void Deliver(Message message)
	{
		object[] managed = System.Array.Empty<object>();
		Il2CppSteamworks.Callback[] game = System.Array.Empty<Il2CppSteamworks.Callback>();
		Invoke(delegate
		{
			managed = ManagedSnapshot(managedCallbacks, message.Id, remove: false);
		});
		Invoke(delegate
		{
			game = GameCallbackSnapshot(message.Id);
		});
		object[] array = managed;
		foreach (object callback in array)
		{
			Invoke(delegate
			{
				managedCallback.Invoke(callback, new object[1] { message.Data });
				ManagedEvents++;
			});
		}
		Il2CppSteamworks.Callback[] array2 = game;
		foreach (Il2CppSteamworks.Callback callback2 in array2)
		{
			Invoke(delegate
			{
				callback2.OnRunCallback(message.Data);
				GameEvents++;
			});
		}
	}

	private static void DeliverResult(Completed completed, System.IntPtr data, bool failed)
	{
		object[] managed = System.Array.Empty<object>();
		Il2CppSteamworks.CallResult[] game = System.Array.Empty<Il2CppSteamworks.CallResult>();
		Invoke(delegate
		{
			managed = ManagedSnapshot(managedResults, completed.Handle, remove: true);
		});
		Invoke(delegate
		{
			game = GameResultSnapshot(completed.Handle);
		});
		object[] array = managed;
		foreach (object callback in array)
		{
			Invoke(delegate
			{
				try
				{
					managedResult.Invoke(callback, new object[3] { data, failed, completed.Handle });
					ManagedResults++;
				}
				finally
				{
					FinalizeManagedResult(callback);
				}
			});
		}
		Il2CppSteamworks.CallResult[] array2 = game;
		foreach (Il2CppSteamworks.CallResult callback2 in array2)
		{
			Invoke(delegate
			{
				try
				{
					callback2.OnRunCallResult(data, failed, completed.Handle);
					GameResults++;
				}
				finally
				{
					FinalizeGameResult(callback2);
				}
			});
		}
	}

	internal static void Pump()
	{
		if (System.Threading.Interlocked.CompareExchange(ref pumping, 1, 0) != 0)
		{
			return;
		}
		try
		{
			int num = getPipe();
			if (num == 0)
			{
				return;
			}
			run(num);
			for (int i = 0; i < 1024; i++)
			{
				if (!next(num, messageBuffer))
				{
					break;
				}
				try
				{
					Message message = Marshal.PtrToStructure<Message>(messageBuffer);
					if (message.Id == 703)
					{
						if (message.Size != Marshal.SizeOf<Completed>())
						{
							throw new System.InvalidOperationException("Unexpected Steam API completion payload size");
						}
						Completed completed = Marshal.PtrToStructure<Completed>(message.Data);
						int num2 = checked((int)completed.Size);
						if (num2 < 0 || num2 > 4194304)
						{
							throw new System.InvalidOperationException("Invalid Steam call result size");
						}
						System.IntPtr intPtr = Marshal.AllocHGlobal(num2);
						try
						{
							if (getResult(num, completed.Handle, intPtr, num2, completed.Id, out var failed))
							{
								DeliverResult(completed, intPtr, failed);
							}
						}
						finally
						{
							Marshal.FreeHGlobal(intPtr);
						}
					}
					else
					{
						Deliver(message);
					}
				}
				catch (System.Exception ex)
				{
					MelonLogger.Error("[Steam callbacks] Pump failed: " + ex);
				}
				finally
				{
					free(num);
				}
			}
		}
		finally
		{
			System.Threading.Volatile.Write(ref pumping, 0);
		}
	}
}
