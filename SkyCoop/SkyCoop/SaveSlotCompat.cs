using System;
using System.Text;
using System.Text.Json;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using MelonLoader;

namespace SkyCoop;

public static class SaveSlotCompat
{
	private sealed class WriteScope : System.IDisposable
	{
		private readonly SlotData previous;

		public WriteScope(SlotData slot)
		{
			previous = writeTarget;
			writeTarget = slot ?? throw new System.ArgumentNullException("slot");
		}

		public void Dispose()
		{
			writeTarget = previous;
		}
	}

	[System.ThreadStatic]
	private static SlotData writeTarget;

	public static System.IDisposable UseWriteTarget(SlotData slot)
	{
		return new WriteScope(slot);
	}

	private static SlotData ResolveSlot(SaveSlotType gameMode, Episode episode, uint gameId, string name)
	{
		if (writeTarget != null && writeTarget.m_InternalName == name)
		{
			return writeTarget;
		}
		SlotData saveSlotFromName = SaveGameSlots.GetSaveSlotFromName(name);
		if (saveSlotFromName != null)
		{
			return saveSlotFromName;
		}
		saveSlotFromName = SaveGameSlots.CreateSlot(name, gameMode, gameId, episode);
		if (saveSlotFromName != null)
		{
			SaveGameSlots.AddSlotData(saveSlotFromName);
		}
		return saveSlotFromName;
	}

	public static bool SaveDataToSlot(SaveSlotType gameMode, Episode episode, uint gameId, string name, string key, string data)
	{
		try
		{
			SlotData slotData = ResolveSlot(gameMode, episode, gameId, name);
			if (slotData == null)
			{
				return false;
			}
			Il2CppSystem.String obj = data ?? "";
			bool result = SaveGameSlots.SaveDataToSlot(slotData, key, obj);
			if (key == "skycoop_genversion")
			{
				MelonLogger.Msg("SAVE_GEN_WRITE success=" + result + " present=" + slotData.HasData(key) + " payloadType=" + obj.GetIl2CppType().FullName);
			}
			return result;
		}
		catch (System.Exception ex)
		{
			MelonLogger.Warning("[SkyCoop] Could not write \"" + key + "\" to save slot " + name + ": " + ex.Message);
			return false;
		}
	}

	public static string LoadDataFromSlot(string name, string key)
	{
		try
		{
			if (key == "global" && SaveGameSlots.TryLoadDataFromSlot<GlobalSaveGameFormat>(name, key, out var data) && data != null)
			{
				return Utils.SerializeObject(data);
			}
			SlotData saveSlotFromName = SaveGameSlots.GetSaveSlotFromName(name);
			if (saveSlotFromName != null && saveSlotFromName.HasData(key))
			{
				saveSlotFromName.Lock();
				try
				{
					if (saveSlotFromName.m_Dict.TryGetValue(key, out var value) && value != null)
					{
						Il2CppStructArray<byte> il2CppStructArray = LZF.Decompress(value);
						if (il2CppStructArray != null)
						{
							byte[] array = new byte[il2CppStructArray.Length];
							for (int i = 0; i < array.Length; i++)
							{
								array[i] = il2CppStructArray[i];
							}
							using JsonDocument jsonDocument = JsonDocument.Parse(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(array));
							if (jsonDocument.RootElement.ValueKind == JsonValueKind.String)
							{
								return jsonDocument.RootElement.GetString();
							}
						}
					}
				}
				finally
				{
					saveSlotFromName.UnLock();
				}
			}
			string data2 = null;
			if (SaveGameSlots.TryLoadDataFromSlot<string>(name, key, out data2) && data2 != null)
			{
				return data2;
			}
		}
		catch (System.Exception ex)
		{
			MelonLogger.Warning("[SkyCoop] Could not read \"" + key + "\" from save slot " + name + ": " + ex.Message);
		}
		return null;
	}
}
