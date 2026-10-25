using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using SkyCoop;
using UnityEngine;

namespace GameServer;

public class Packet : IDisposable
{
	public class AnimalArrow
	{
		public float m_Condition = 100f;

		public Vector3 m_Position = new Vector3(0f, 0f, 0f);

		public Vector3 m_Angle = new Vector3(0f, 0f, 0f);

		public float m_Depth;

		public string m_LocaName = "";
	}

	private List<byte> buffer;

	private byte[] readableBuffer;

	private int readPos;

	private bool disposed;

	public static void Log(string TXT, Shared.LoggerColor Color = Shared.LoggerColor.White)
	{
		MelonLogger.Msg(TXT);
	}

	public Packet()
	{
		buffer = new List<byte>();
		readPos = 0;
	}

	public Packet(int _id)
	{
		buffer = new List<byte>();
		readPos = 0;
		Write(_id);
	}

	public int ReturnSize()
	{
		return buffer.Count;
	}

	public int ReturnUnusedBytesCount()
	{
		return buffer.Count - readPos;
	}

	public Packet(byte[] _data)
	{
		buffer = new List<byte>();
		readPos = 0;
		SetBytes(_data);
	}

	public void SetBytes(byte[] _data)
	{
		Write(_data);
		readableBuffer = buffer.ToArray();
	}

	public void WriteLength()
	{
		buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count));
	}

	public void InsertInt(int _value)
	{
		buffer.InsertRange(0, BitConverter.GetBytes(_value));
	}

	public void InsertString(string _value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(_value);
		buffer.InsertRange(0, bytes);
		buffer.InsertRange(0, BitConverter.GetBytes(bytes.Length));
	}

	public void InsertClientInfo(int ID, string GUID)
	{
		List<byte> list = new List<byte>();
		list.AddRange(BitConverter.GetBytes(ID));
		byte[] bytes = Encoding.UTF8.GetBytes(GUID);
		list.AddRange(BitConverter.GetBytes(bytes.Length));
		list.AddRange(bytes);
		buffer.InsertRange(0, list);
	}

	public byte[] ToArray()
	{
		readableBuffer = buffer.ToArray();
		return readableBuffer;
	}

	public int Length()
	{
		return buffer.Count;
	}

	public int UnreadLength()
	{
		return Length() - readPos;
	}

	public void Reset(bool _shouldReset = true)
	{
		if (_shouldReset)
		{
			buffer.Clear();
			readableBuffer = null;
			readPos = 0;
		}
		else
		{
			readPos -= 4;
		}
	}

	public void Write(byte _value)
	{
		buffer.Add(_value);
	}

	public void Write(byte[] _value)
	{
		buffer.AddRange(_value);
	}

	public void Write(short _value)
	{
		buffer.AddRange(BitConverter.GetBytes(_value));
	}

	public void Write(int _value)
	{
		buffer.AddRange(BitConverter.GetBytes(_value));
	}

	public void Write(long _value)
	{
		buffer.AddRange(BitConverter.GetBytes(_value));
	}

	public void Write(float _value)
	{
		buffer.AddRange(BitConverter.GetBytes(_value));
	}

	public void Write(bool _value)
	{
		buffer.AddRange(BitConverter.GetBytes(_value));
	}

	public void Write(string _value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(_value);
		Write(bytes.Length);
		buffer.AddRange(bytes);
	}

	public void WriteUnicodeString(string _value)
	{
		Write(Encoding.Unicode.GetBytes(_value).Length);
		buffer.AddRange(Encoding.Unicode.GetBytes(_value));
	}

	public void Write(Vector3 _value)
	{
		Write(_value.x);
		Write(_value.y);
		Write(_value.z);
	}

	public void Write(Quaternion _value)
	{
		Write(_value.x);
		Write(_value.y);
		Write(_value.z);
		Write(_value.w);
	}

	public void Write(DataStr.WeatherProxies weather)
	{
		Write(weather.m_WeatherProxy);
		Write(weather.m_WeatherTransitionProxy);
		Write(weather.m_WindProxy);
	}

	public void Write(DataStr.GearItemDataPacket gear)
	{
		Write(gear.m_GearName);
		Write(gear.m_DataProxy);
		Write(gear.m_Water);
		Write(gear.m_SendedTo);
	}

	public void Write(DataStr.DroppedGearItemDataPacket gear)
	{
		Write(gear.m_GearID);
		Write(gear.m_Position);
		Write(gear.m_Rotation);
		Write(gear.m_LevelID);
		Write(gear.m_LevelGUID);
		Write(gear.m_Hash);
		Write(gear.m_Extra);
	}

	public void Write(DataStr.ShowShelterByOther shelter)
	{
		Write(shelter.m_Position);
		Write(shelter.m_Rotation);
		Write(shelter.m_LevelID);
		Write(shelter.m_LevelGUID);
	}

	public void Write(DataStr.AnimalSync obj)
	{
		Write(obj.m_position);
		Write(obj.m_rotation);
		Write(obj.m_guid);
		Write(obj.m_name);
		Write(obj.m_Hp);
		Write(obj.m_Bleeding);
		Write(obj.m_Controller);
		Write(obj.m_ProxySave);
		Write(obj.m_LevelD);
		Write(obj.m_SpawnRegionGUID);
	}

	public void Write(DataStr.AnimalAnimsSync obj)
	{
		Write(obj.AP_TurnAngle);
		Write(obj.AP_TurnSpeed);
		Write(obj.AP_Speed);
		Write(obj.AP_Wounded);
		Write(obj.AP_Roll);
		Write(obj.AP_Pitch);
		Write(obj.AP_TargetHeading);
		Write(obj.AP_TargetHeadingSmooth);
		Write(obj.AP_TapMeter);
		Write(obj.AP_AiState);
		Write(obj.AP_Corpse);
		Write(obj.AP_Dead);
		Write(obj.AP_DeadSide);
		Write(obj.AP_DamageBodyPart);
		Write(obj.AP_AttackId);
		Write(obj.AP_Stunned);
	}

	public void Write(DataStr.AnimalTrigger obj)
	{
		Write(obj.m_Guid);
		Write(obj.m_Trigger);
	}

	public void Write(DataStr.ShootSync obj)
	{
		Write(obj.m_position);
		Write(obj.m_rotation);
		Write(obj.m_projectilename);
		Write(obj.m_skill);
		Write(obj.m_camera_forward);
		Write(obj.m_camera_right);
		Write(obj.m_camera_up);
		Write(obj.m_lookat);
		Write(obj.m_sceneguid);
	}

	public void Write(DataStr.HarvestStats obj)
	{
		Write(obj.m_Meat);
		Write(obj.m_Guts);
		Write(obj.m_Hide);
		Write(obj.m_Guid);
	}

	public void Write(DataStr.SaveSlotSync obj)
	{
		Write(obj.m_Episode);
		Write(obj.m_SaveSlotType);
		Write(obj.m_Seed);
		Write(obj.m_ExperienceMode);
		Write(obj.m_Location);
		Write(obj.m_FixedSpawnScene);
		Write(obj.m_FixedSpawnPosition);
		Write(obj.m_CustomExperienceStr);
	}

	public void Write(DataStr.ContainerOpenSync obj)
	{
		Write(obj.m_Guid);
		Write(obj.m_State);
		Write(obj.m_LevelID);
		Write(obj.m_LevelGUID);
		Write(obj.m_Inspected);
	}

	public void Write(DataStr.AnimalAligner obj)
	{
		Write(obj.m_Proxy);
		Write(obj.m_Guid);
	}

	public void Write(DataStr.PlayerEquipmentData obj)
	{
		Write(obj.m_Arrows);
		Write(obj.m_HasAxe);
		Write(obj.m_HasMedkit);
		Write(obj.m_HasRevolver);
		Write(obj.m_HasRifle);
		Write(obj.m_Flares);
		Write(obj.m_BlueFlares);
	}

	public void Write(DataStr.MultiplayerChatMessage obj)
	{
		WriteUnicodeString(obj.m_By);
		WriteUnicodeString(obj.m_Message);
		Write(obj.m_Type);
		Write(obj.m_Global);
		Write(obj.m_Private);
	}

	public void Write(DataStr.MultiPlayerClientStatus obj)
	{
		Write(obj.m_ID);
		Write(obj.m_Name);
		Write(obj.m_Sleep);
		Write(obj.m_Dead);
		Write(obj.m_IsLoading);
	}

	public void Write(DataStr.PlayerClothingData obj)
	{
		Write(obj.m_Hat);
		Write(obj.m_Top);
		Write(obj.m_Bottom);
		Write(obj.m_Boots);
		Write(obj.m_Scarf);
		Write(obj.m_Balaclava);
	}

	public void Write(DataStr.BrokenFurnitureSync obj)
	{
		Write(obj.m_Guid);
		Write(obj.m_ParentGuid);
		Write(obj.m_LevelID);
		Write(obj.m_LevelGUID);
		Write(obj.m_Broken);
	}

	public void Write(DataStr.PickedGearSync obj)
	{
		Write(obj.m_Spawn);
		Write(obj.m_LevelID);
		Write(obj.m_LevelGUID);
		Write(obj.m_MyInstanceID);
		Write(obj.m_GearName);
	}

	public void Write(DataStr.ClimbingRopeSync obj)
	{
		Write(obj.m_Position);
		Write(obj.m_LevelID);
		Write(obj.m_LevelGUID);
		Write(obj.m_Deployed);
		Write(obj.m_Snapped);
	}

	public void Write(DataStr.ServerConfigData obj)
	{
		Write(obj.m_FastConsumption);
		Write(obj.m_DuppedSpawns);
		Write(obj.m_DuppedContainers);
		Write(obj.m_PlayersSpawnType);
		Write(obj.m_FireSync);
		Write(obj.m_CheatsMode);
		Write(obj.m_CheckModsValidation);
		Write(obj.m_SaveScamProtection);
		Write(obj.m_PVP);
	}

	public void Write(DataStr.HarvestableSyncData obj)
	{
		Write(obj.m_Guid);
		Write(obj.m_State);
	}

	public void Write(DataStr.FireSourcesSync obj)
	{
		Write(obj.m_Guid);
		Write(obj.m_LevelGUID);
		Write(obj.m_LevelId);
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_IsCampfire);
		Write(obj.m_FuelName);
		Write(obj.m_Fuel);
		Write(obj.m_CurrentHeat);
		Write(obj.m_MaxHeat);
		Write(obj.m_HeatInnerRadius);
		Write(obj.m_HeatOuterRadius);
		Write(obj.m_HeatMasked);
	}

	public void Write(DataStr.SlicedJsonData obj)
	{
		Write(obj.m_GearName);
		Write(obj.m_Hash);
		Write(obj.m_Str);
		Write(obj.m_SendTo);
		Write(obj.m_Last);
		Write(obj.m_Extra);
		Write(obj.m_CheckHash);
	}

	public void Write(DataStr.SlicedBytesData obj)
	{
		Write(obj.m_Hash);
		Write(obj.m_Action);
		Write(obj.m_SendTo);
		Write(obj.m_Last);
		Write(obj.m_Length);
		Write(obj.m_ExtraInt);
		Write(obj.m_Data);
	}

	public void Write(DataStr.ExtraDataForDroppedGear obj)
	{
		Write(obj.m_DroppedTime);
		Write(obj.m_GoalTime);
		Write(obj.m_Dropper);
		Write(obj.m_Variant);
		Write(obj.m_GearName);
		Write(obj.m_PhotoGUID);
		WriteUnicodeString(obj.m_ExpeditionNote);
	}

	public void Write(DataStr.AffictionSync obj)
	{
		Write(obj.m_Type);
		Write(obj.m_Location);
		WriteUnicodeString(obj.m_Case);
		Write(obj.m_ShouldBeTreated);
	}

	public void Write(DataStr.AnimalArrow Arrow)
	{
		Write(Arrow.m_Condition);
		Write(Arrow.m_Position);
		Write(Arrow.m_Angle);
		Write(Arrow.m_Depth);
		Write(Arrow.m_LocaName);
	}

	public void Write(List<DataStr.AnimalArrow> Arrows, int Count)
	{
		Write(Count);
		for (int i = 0; i < Arrows.Count; i++)
		{
			Write(Arrows[i]);
		}
	}

	public void Write(DataStr.AnimalCompactData obj)
	{
		Write(obj.m_PrefabName);
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_GUID);
		Write(obj.m_LastSeen);
		Write(obj.m_Health);
		Write(obj.m_Bleeding);
		Write(obj.m_TimeOfBleeding);
		Write(obj.m_RegionGUID);
		Write(obj.m_LastController);
		Write(obj.m_LastAiMode);
		Write(obj.m_Arrows, obj.m_ArrowsCount);
	}

	public void Write(DataStr.AnimalKilled obj)
	{
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_PrefabName);
		Write(obj.m_GUID);
		Write(obj.m_LevelGUID);
		Write(obj.m_CreatedTime);
		Write(obj.m_Meat);
		Write(obj.m_Guts);
		Write(obj.m_Hide);
		Write(obj.m_Knocked);
		Write(obj.m_RegionGUID);
	}

	public void Write(List<int> LIST)
	{
		Write(LIST.Count);
		for (int i = 0; i < LIST.Count; i++)
		{
			Write(LIST[i]);
		}
	}

	public void Write(List<float> LIST)
	{
		Write(LIST.Count);
		for (int i = 0; i < LIST.Count; i++)
		{
			Write(LIST[i]);
		}
	}

	public void Write(List<string> LIST)
	{
		Write(LIST.Count);
		for (int i = 0; i < LIST.Count; i++)
		{
			Write(LIST[i]);
		}
	}

	public void Write(List<ExpeditionManager.ExpeditionInvite> LIST)
	{
		Write(LIST.Count);
		for (int i = 0; i < LIST.Count; i++)
		{
			Write(LIST[i]);
		}
	}

	public void Write(DataStr.CustomChallengeData DAT)
	{
		Write(DAT.m_CurrentTask);
		Write(DAT.m_Time);
		Write(DAT.m_Started);
		Write(DAT.m_Done);
	}

	public DataStr.CustomChallengeData ReadChallengeData()
	{
		return new DataStr.CustomChallengeData
		{
			m_CurrentTask = ReadInt(),
			m_Time = ReadInt(),
			m_Started = ReadBool(),
			m_Done = ReadIntList()
		};
	}

	public List<int> ReadIntList()
	{
		List<int> list = new List<int>();
		int num = ReadInt();
		for (int i = 0; i < num; i++)
		{
			list.Add(ReadInt());
		}
		return list;
	}

	public List<float> ReadFloatList()
	{
		List<float> list = new List<float>();
		int num = ReadInt();
		for (int i = 0; i < num; i++)
		{
			list.Add(ReadFloat());
		}
		return list;
	}

	public List<string> ReadStringList()
	{
		List<string> list = new List<string>();
		int num = ReadInt();
		for (int i = 0; i < num; i++)
		{
			list.Add(ReadString());
		}
		return list;
	}

	public List<ExpeditionManager.ExpeditionInvite> ReadInvitesList()
	{
		List<ExpeditionManager.ExpeditionInvite> list = new List<ExpeditionManager.ExpeditionInvite>();
		int num = ReadInt();
		for (int i = 0; i < num; i++)
		{
			list.Add(ReadExpeditionInvite());
		}
		return list;
	}

	public void Write(DataStr.DeathContainerData obj)
	{
		Write(obj.m_Guid);
		Write(obj.m_LevelKey);
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_Owner);
		Write(obj.m_ContainerPrefab);
		Write(obj.m_DeathTime);
	}

	public DataStr.DeathContainerData ReadDeathContainer()
	{
		return new DataStr.DeathContainerData
		{
			m_Guid = ReadString(),
			m_LevelKey = ReadString(),
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_Owner = ReadString(),
			m_ContainerPrefab = ReadString(),
			m_DeathTime = ReadInt()
		};
	}

	public byte ReadByte(bool _moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			byte result = readableBuffer[readPos];
			if (_moveReadPos)
			{
				readPos++;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'byte'!");
	}

	public byte[] ReadBytes(int _length, bool _moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			byte[] result = buffer.GetRange(readPos, _length).ToArray();
			if (_moveReadPos)
			{
				readPos += _length;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'byte[]'!");
	}

	public short ReadShort(bool _moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			short result = BitConverter.ToInt16(readableBuffer, readPos);
			if (_moveReadPos)
			{
				readPos += 2;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'short'!");
	}

	public int ReadInt(bool _moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			if (readableBuffer == null)
			{
				Log("Could not read value of type 'int'! readableBuffer is null!!!");
				return 0;
			}
			int result = BitConverter.ToInt32(readableBuffer, readPos);
			if (_moveReadPos)
			{
				readPos += 4;
			}
			return result;
		}
		Log("Could not read value of type 'int'!");
		return 0;
	}

	public long ReadLong(bool _moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			long result = BitConverter.ToInt64(readableBuffer, readPos);
			if (_moveReadPos)
			{
				readPos += 8;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'long'!");
	}

	public float ReadFloat(bool _moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			float result = BitConverter.ToSingle(readableBuffer, readPos);
			if (_moveReadPos)
			{
				readPos += 4;
			}
			return result;
		}
		return -0.777f;
	}

	public bool ReadBool(bool _moveReadPos = true)
	{
		if (buffer.Count > readPos)
		{
			bool result = BitConverter.ToBoolean(readableBuffer, readPos);
			if (_moveReadPos)
			{
				readPos++;
			}
			return result;
		}
		throw new Exception("Could not read value of type 'bool'!");
	}

	public string ReadString(bool _moveReadPos = true)
	{
		try
		{
			int num = ReadInt();
			string text = Encoding.UTF8.GetString(readableBuffer, readPos, num);
			if (_moveReadPos && text.Length > 0)
			{
				readPos += num;
			}
			return text;
		}
		catch
		{
			throw new Exception("Could not read value of type 'string'!");
		}
	}

	public string ReadUnicodeString()
	{
		try
		{
			int num = ReadInt();
			string result = Encoding.Unicode.GetString(readableBuffer, readPos, num);
			readPos += num;
			return result;
		}
		catch
		{
			throw new Exception("Could not read value of type 'string'!");
		}
	}

	public Vector3 ReadVector3(bool _moveReadPos = true)
	{
		return new Vector3(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
	}

	public Quaternion ReadQuaternion(bool _moveReadPos = true)
	{
		return new Quaternion(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
	}

	public DataStr.WeatherProxies ReadWeather(bool _moveReadPos = true)
	{
		return new DataStr.WeatherProxies
		{
			m_WeatherProxy = ReadString(_moveReadPos),
			m_WeatherTransitionProxy = ReadString(_moveReadPos),
			m_WindProxy = ReadString(_moveReadPos)
		};
	}

	public DataStr.AnimalAnimsSync ReadAnimalAnim(bool _moveReadPos = true)
	{
		return new DataStr.AnimalAnimsSync
		{
			AP_TurnAngle = ReadFloat(),
			AP_TurnSpeed = ReadFloat(),
			AP_Speed = ReadFloat(),
			AP_Wounded = ReadFloat(),
			AP_Roll = ReadFloat(),
			AP_Pitch = ReadFloat(),
			AP_TargetHeading = ReadFloat(),
			AP_TargetHeadingSmooth = ReadFloat(),
			AP_TapMeter = ReadFloat(),
			AP_AiState = ReadInt(),
			AP_Corpse = ReadBool(),
			AP_Dead = ReadBool(),
			AP_DeadSide = ReadInt(),
			AP_DamageBodyPart = ReadInt(),
			AP_AttackId = ReadInt(),
			AP_Stunned = ReadBool()
		};
	}

	public DataStr.AnimalTrigger ReadAnimalTrigger(bool _moveReadPos = true)
	{
		return new DataStr.AnimalTrigger
		{
			m_Guid = ReadString(),
			m_Trigger = ReadInt()
		};
	}

	public DataStr.ShootSync ReadShoot(bool _moveReadPos = true)
	{
		return new DataStr.ShootSync
		{
			m_position = ReadVector3(),
			m_rotation = ReadQuaternion(),
			m_projectilename = ReadString(),
			m_skill = ReadFloat(),
			m_camera_forward = ReadVector3(),
			m_camera_right = ReadVector3(),
			m_camera_up = ReadVector3(),
			m_lookat = ReadBool(),
			m_sceneguid = ReadString()
		};
	}

	public DataStr.HarvestStats ReadHarvest(bool _moveReadPos = true)
	{
		return new DataStr.HarvestStats
		{
			m_Meat = ReadFloat(),
			m_Guts = ReadInt(),
			m_Hide = ReadInt(),
			m_Guid = ReadString()
		};
	}

	public DataStr.SaveSlotSync ReadSaveSlot(bool _moveReadPos = true)
	{
		return new DataStr.SaveSlotSync
		{
			m_Episode = ReadInt(),
			m_SaveSlotType = ReadInt(),
			m_Seed = ReadInt(),
			m_ExperienceMode = ReadInt(),
			m_Location = ReadInt(),
			m_FixedSpawnScene = ReadString(),
			m_FixedSpawnPosition = ReadVector3(),
			m_CustomExperienceStr = ReadString()
		};
	}

	public DataStr.ContainerOpenSync ReadContainer(bool _moveReadPos = true)
	{
		return new DataStr.ContainerOpenSync
		{
			m_Guid = ReadString(),
			m_State = ReadBool(),
			m_LevelID = ReadInt(),
			m_LevelGUID = ReadString(),
			m_Inspected = ReadBool()
		};
	}

	public DataStr.AnimalAligner ReadAnimalAligner(bool _moveReadPos = true)
	{
		return new DataStr.AnimalAligner
		{
			m_Proxy = ReadString(),
			m_Guid = ReadString()
		};
	}

	public DataStr.PlayerEquipmentData ReadEQ(bool _moveReadPos = true)
	{
		return new DataStr.PlayerEquipmentData
		{
			m_Arrows = ReadInt(),
			m_HasAxe = ReadBool(),
			m_HasMedkit = ReadBool(),
			m_HasRevolver = ReadBool(),
			m_HasRifle = ReadBool(),
			m_Flares = ReadInt(),
			m_BlueFlares = ReadInt()
		};
	}

	public DataStr.MultiplayerChatMessage ReadChat()
	{
		return new DataStr.MultiplayerChatMessage
		{
			m_By = ReadUnicodeString(),
			m_Message = ReadUnicodeString(),
			m_Type = ReadInt(),
			m_Global = ReadBool(),
			m_Private = ReadBool()
		};
	}

	public DataStr.MultiPlayerClientStatus ReadClientStatus()
	{
		return new DataStr.MultiPlayerClientStatus
		{
			m_ID = ReadInt(),
			m_Name = ReadString(),
			m_Sleep = ReadBool(),
			m_Dead = ReadBool(),
			m_IsLoading = ReadBool()
		};
	}

	public DataStr.PlayerClothingData ReadClothingData()
	{
		return new DataStr.PlayerClothingData
		{
			m_Hat = ReadString(),
			m_Top = ReadString(),
			m_Bottom = ReadString(),
			m_Boots = ReadString(),
			m_Scarf = ReadString(),
			m_Balaclava = ReadString()
		};
	}

	public DataStr.BrokenFurnitureSync ReadFurn()
	{
		return new DataStr.BrokenFurnitureSync
		{
			m_Guid = ReadString(),
			m_ParentGuid = ReadString(),
			m_LevelID = ReadInt(),
			m_LevelGUID = ReadString(),
			m_Broken = ReadBool()
		};
	}

	public DataStr.PickedGearSync ReadPickedGear()
	{
		return new DataStr.PickedGearSync
		{
			m_Spawn = ReadVector3(),
			m_LevelID = ReadInt(),
			m_LevelGUID = ReadString(),
			m_MyInstanceID = ReadInt(),
			m_GearName = ReadString()
		};
	}

	public DataStr.ClimbingRopeSync ReadRope()
	{
		return new DataStr.ClimbingRopeSync
		{
			m_Position = ReadVector3(),
			m_LevelID = ReadInt(),
			m_LevelGUID = ReadString(),
			m_Deployed = ReadBool(),
			m_Snapped = ReadBool()
		};
	}

	public DataStr.ServerConfigData ReadServerCFG()
	{
		return new DataStr.ServerConfigData
		{
			m_FastConsumption = ReadBool(),
			m_DuppedSpawns = ReadBool(),
			m_DuppedContainers = ReadBool(),
			m_PlayersSpawnType = ReadInt(),
			m_FireSync = ReadInt(),
			m_CheatsMode = ReadInt(),
			m_CheckModsValidation = ReadBool(),
			m_SaveScamProtection = ReadBool(),
			m_PVP = ReadBool()
		};
	}

	public DataStr.HarvestableSyncData ReadHarvestablePlant()
	{
		return new DataStr.HarvestableSyncData
		{
			m_Guid = ReadString(),
			m_State = ReadString()
		};
	}

	public DataStr.GearItemDataPacket ReadGearData()
	{
		return new DataStr.GearItemDataPacket
		{
			m_GearName = ReadString(),
			m_DataProxy = ReadString(),
			m_Water = ReadFloat(),
			m_SendedTo = ReadInt()
		};
	}

	public DataStr.DroppedGearItemDataPacket ReadDroppedGearData()
	{
		return new DataStr.DroppedGearItemDataPacket
		{
			m_GearID = ReadInt(),
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_LevelID = ReadInt(),
			m_LevelGUID = ReadString(),
			m_Hash = ReadInt(),
			m_Extra = ReadExtraDataForDropperGear()
		};
	}

	public DataStr.ShowShelterByOther ReadShelter()
	{
		return new DataStr.ShowShelterByOther
		{
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_LevelID = ReadInt(),
			m_LevelGUID = ReadString()
		};
	}

	public DataStr.FireSourcesSync ReadFire()
	{
		return new DataStr.FireSourcesSync
		{
			m_Guid = ReadString(),
			m_LevelGUID = ReadString(),
			m_LevelId = ReadInt(),
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_IsCampfire = ReadBool(),
			m_FuelName = ReadString(),
			m_Fuel = ReadFloat(),
			m_CurrentHeat = ReadFloat(),
			m_MaxHeat = ReadFloat(),
			m_HeatInnerRadius = ReadFloat(),
			m_HeatOuterRadius = ReadFloat(),
			m_HeatMasked = ReadBool()
		};
	}

	public DataStr.SlicedJsonData ReadSlicedGear()
	{
		return new DataStr.SlicedJsonData
		{
			m_GearName = ReadString(),
			m_Hash = ReadInt(),
			m_Str = ReadString(),
			m_SendTo = ReadInt(),
			m_Last = ReadBool(),
			m_Extra = ReadExtraDataForDropperGear(),
			m_CheckHash = ReadLong()
		};
	}

	public DataStr.SlicedBytesData ReadSlicedBytes()
	{
		DataStr.SlicedBytesData slicedBytesData = new DataStr.SlicedBytesData();
		slicedBytesData.m_Hash = ReadInt();
		slicedBytesData.m_Action = ReadString();
		slicedBytesData.m_SendTo = ReadInt();
		slicedBytesData.m_Last = ReadBool();
		slicedBytesData.m_Length = ReadInt();
		slicedBytesData.m_ExtraInt = ReadInt();
		slicedBytesData.m_Data = ReadBytes(slicedBytesData.m_Length);
		return slicedBytesData;
	}

	public DataStr.ExtraDataForDroppedGear ReadExtraDataForDropperGear()
	{
		return new DataStr.ExtraDataForDroppedGear
		{
			m_DroppedTime = ReadInt(),
			m_GoalTime = ReadInt(),
			m_Dropper = ReadString(),
			m_Variant = ReadInt(),
			m_GearName = ReadString(),
			m_PhotoGUID = ReadString(),
			m_ExpeditionNote = ReadUnicodeString()
		};
	}

	public DataStr.AffictionSync ReadAffiction()
	{
		return new DataStr.AffictionSync
		{
			m_Type = ReadInt(),
			m_Location = ReadInt(),
			m_Case = ReadUnicodeString(),
			m_ShouldBeTreated = ReadBool()
		};
	}

	public DataStr.AnimalArrow ReadArrow()
	{
		return new DataStr.AnimalArrow
		{
			m_Condition = ReadFloat(),
			m_Position = ReadVector3(),
			m_Angle = ReadVector3(),
			m_Depth = ReadFloat(),
			m_LocaName = ReadString()
		};
	}

	public DataStr.AnimalCompactData ReadAnimalCompactData()
	{
		DataStr.AnimalCompactData animalCompactData = new DataStr.AnimalCompactData();
		animalCompactData.m_PrefabName = ReadString();
		animalCompactData.m_Position = ReadVector3();
		animalCompactData.m_Rotation = ReadQuaternion();
		animalCompactData.m_GUID = ReadString();
		animalCompactData.m_LastSeen = ReadInt();
		animalCompactData.m_Health = ReadFloat();
		animalCompactData.m_Bleeding = ReadBool();
		animalCompactData.m_TimeOfBleeding = ReadInt();
		animalCompactData.m_RegionGUID = ReadString();
		animalCompactData.m_LastController = ReadInt();
		animalCompactData.m_LastAiMode = ReadInt();
		animalCompactData.m_ArrowsCount = ReadInt();
		for (int i = 0; i < animalCompactData.m_ArrowsCount; i++)
		{
			animalCompactData.m_Arrows.Add(ReadArrow());
		}
		return animalCompactData;
	}

	public DataStr.AnimalKilled ReadAnimalCorpse()
	{
		return new DataStr.AnimalKilled
		{
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_PrefabName = ReadString(),
			m_GUID = ReadString(),
			m_LevelGUID = ReadString(),
			m_CreatedTime = ReadInt(),
			m_Meat = ReadFloat(),
			m_Guts = ReadInt(),
			m_Hide = ReadInt(),
			m_Knocked = ReadBool(),
			m_RegionGUID = ReadString()
		};
	}

	public void Write(Supporters.SupporterBenefits obj)
	{
		Write(obj.m_Knife);
		Write(obj.m_BrightNick);
		Write(obj.m_Flairs);
	}

	public Supporters.SupporterBenefits ReadSupporterBenefits()
	{
		return new Supporters.SupporterBenefits
		{
			m_Knife = ReadBool(),
			m_BrightNick = ReadBool(),
			m_Flairs = ReadIntList()
		};
	}

	public void Write(DataStr.WeatherVolunteerData obj)
	{
		Write(obj.WeatherType);
		Write(obj.WeatherDuration);
		Write(obj.CurrentRegion);
		Write(obj.StageDuration);
		Write(obj.StageTransition);
		Write(obj.SetIndex);
		Write(obj.HighMin);
		Write(obj.HighMax);
		Write(obj.LowMin);
		Write(obj.LowMax);
		Write(obj.CoolingHours);
		Write(obj.WarmingHours);
		Write(obj.PreviousStage);
	}

	public DataStr.WeatherVolunteerData ReadWeatherVolunteerData()
	{
		return new DataStr.WeatherVolunteerData
		{
			WeatherType = ReadInt(),
			WeatherDuration = ReadFloat(),
			CurrentRegion = ReadInt(),
			StageDuration = ReadFloatList(),
			StageTransition = ReadFloatList(),
			SetIndex = ReadInt(),
			HighMin = ReadFloat(),
			HighMax = ReadFloat(),
			LowMin = ReadFloat(),
			LowMax = ReadFloat(),
			CoolingHours = ReadInt(),
			WarmingHours = ReadInt(),
			PreviousStage = ReadInt()
		};
	}

	public void Write(ExpeditionManager.ExpeditionInvite obj)
	{
		Write(obj.m_InviterMAC);
		Write(obj.m_InviterName);
		Write(obj.m_PersonToInviteMAC);
	}

	public ExpeditionManager.ExpeditionInvite ReadExpeditionInvite()
	{
		return new ExpeditionManager.ExpeditionInvite
		{
			m_InviterMAC = ReadString(),
			m_InviterName = ReadString(),
			m_PersonToInviteMAC = ReadString()
		};
	}

	public void Write(DataStr.SlicedBase64Data obj)
	{
		Write(obj.m_Slice);
		Write(obj.m_LastSliceIndex);
		Write(obj.m_SliceIndex);
		Write(obj.m_CheckSum);
		Write(obj.m_GUID);
		Write(obj.m_Purpose);
	}

	public DataStr.SlicedBase64Data ReadSlicedBase64Data()
	{
		return new DataStr.SlicedBase64Data
		{
			m_Slice = ReadString(),
			m_LastSliceIndex = ReadInt(),
			m_SliceIndex = ReadInt(),
			m_CheckSum = ReadString(),
			m_GUID = ReadString(),
			m_Purpose = ReadInt()
		};
	}

	public void Write(DataStr.FakeRockCacheVisualData obj)
	{
		Write(obj.m_GUID);
		Write(obj.m_LevelGUID);
		Write(obj.m_Owner);
		Write(obj.m_Position);
		Write(obj.m_Rotation);
	}

	public DataStr.FakeRockCacheVisualData ReadFakeRockCache()
	{
		return new DataStr.FakeRockCacheVisualData
		{
			m_GUID = ReadString(),
			m_LevelGUID = ReadString(),
			m_Owner = ReadString(),
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion()
		};
	}

	public void Write(DataStr.ExpeditionInteractiveData obj)
	{
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_Scale);
		Write(obj.m_ObjectText);
		Write(obj.m_InteractText);
		Write(obj.m_InteractTime);
		Write(obj.m_Tool);
		Write(obj.m_Material);
		Write(obj.m_MaterialCount);
		Write(obj.m_GUID);
		Write(obj.m_Audio);
		Write(obj.m_Yield);
		Write(obj.m_YieldCount);
		Write(obj.m_ObjectGroupToRemove);
	}

	public DataStr.ExpeditionInteractiveData ReadInteractiveData()
	{
		return new DataStr.ExpeditionInteractiveData
		{
			m_Position = ReadVector3(),
			m_Rotation = ReadVector3(),
			m_Scale = ReadVector3(),
			m_ObjectText = ReadString(),
			m_InteractText = ReadString(),
			m_InteractTime = ReadFloat(),
			m_Tool = ReadString(),
			m_Material = ReadString(),
			m_MaterialCount = ReadInt(),
			m_GUID = ReadString(),
			m_Audio = ReadString(),
			m_Yield = ReadString(),
			m_YieldCount = ReadInt(),
			m_ObjectGroupToRemove = ReadString()
		};
	}

	public void Write(DataStr.CougarStateSync obj)
	{
		Write(obj.m_Enabled);
		Write(obj.m_ActiveRegionName);
		Write(obj.m_RegionNames.Count);
		for (int i = 0; i < obj.m_RegionNames.Count; i++)
		{
			Write(obj.m_RegionNames[i]);
			Write(obj.m_ThreatLevels[i]);
			Write(obj.m_ThreatCooldowns[i]);
		}
	}

	public DataStr.CougarStateSync ReadCougarState()
	{
		DataStr.CougarStateSync cougarStateSync = new DataStr.CougarStateSync();
		cougarStateSync.m_Enabled = ReadBool();
		cougarStateSync.m_ActiveRegionName = ReadString();
		int num = ReadInt();
		for (int i = 0; i < num; i++)
		{
			cougarStateSync.m_RegionNames.Add(ReadString());
			cougarStateSync.m_ThreatLevels.Add(ReadInt());
			cougarStateSync.m_ThreatCooldowns.Add(ReadFloat());
		}
		return cougarStateSync;
	}

	public void Write(DataStr.TraderStateSync obj)
	{
		Write(obj.m_SerializedState);
	}

	public DataStr.TraderStateSync ReadTraderState()
	{
		return new DataStr.TraderStateSync
		{
			m_SerializedState = ReadString()
		};
	}

	public void Write(DataStr.TravoisSync obj)
	{
		Write(obj.m_GUID);
		Write(obj.m_LevelGUID);
		Write(obj.m_LevelID);
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_CarriedBy);
	}

	public DataStr.TravoisSync ReadTravoisSync()
	{
		return new DataStr.TravoisSync
		{
			m_GUID = ReadString(),
			m_LevelGUID = ReadString(),
			m_LevelID = ReadInt(),
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_CarriedBy = ReadInt()
		};
	}

	public void Write(DataStr.UniversalSyncableObject obj)
	{
		Write(obj.m_Prefab);
		Write(obj.m_Scene);
		Write(obj.m_GUID);
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_CreationTime);
		Write(obj.m_RemoveTime);
		Write(obj.m_ExpeditionBelong);
		Write(obj.m_InteractiveData);
	}

	public DataStr.UniversalSyncableObject ReadUniversalSyncable()
	{
		return new DataStr.UniversalSyncableObject
		{
			m_Prefab = ReadString(),
			m_Scene = ReadString(),
			m_GUID = ReadString(),
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_CreationTime = ReadInt(),
			m_RemoveTime = ReadInt(),
			m_ExpeditionBelong = ReadString(),
			m_InteractiveData = ReadInteractiveData()
		};
	}

	public void Write(ExpeditionManager.SpecialExpeditionItem obj)
	{
		Write(obj.m_GearReferenceName);
		WriteUnicodeString(obj.m_GearName);
		WriteUnicodeString(obj.m_GearDescription);
		Write(obj.m_ExpeditionAlias);
		Write(obj.m_ModelPrefab);
		Write(obj.m_GearIcon);
		Write(obj.m_Position);
		Write(obj.m_Rotation);
		Write(obj.m_Scene);
	}

	public ExpeditionManager.SpecialExpeditionItem ReadSpecialExpeditionItem()
	{
		return new ExpeditionManager.SpecialExpeditionItem
		{
			m_GearReferenceName = ReadString(),
			m_GearName = ReadUnicodeString(),
			m_GearDescription = ReadUnicodeString(),
			m_ExpeditionAlias = ReadString(),
			m_ModelPrefab = ReadString(),
			m_GearIcon = ReadString(),
			m_Position = ReadVector3(),
			m_Rotation = ReadQuaternion(),
			m_Scene = ReadString()
		};
	}

	protected virtual void Dispose(bool _disposing)
	{
		if (!disposed)
		{
			if (_disposing)
			{
				buffer = null;
				readableBuffer = null;
				readPos = 0;
			}
			disposed = true;
		}
	}

	public void Dispose()
	{
		Dispose(_disposing: true);
		GC.SuppressFinalize(this);
	}
}
