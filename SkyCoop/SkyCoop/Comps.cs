using System;
using System.Collections.Generic;
using GameServer;
using Il2Cpp;
using Il2CppAK;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCoop;

public class Comps
{
	public class DroppedGearDummy : MonoBehaviour
	{
		public string m_LocalizedDisplayName;

		public int m_SearchKey;

		public DataStr.ExtraDataForDroppedGear m_Extra = new DataStr.ExtraDataForDroppedGear();

		public GearItem m_DecoyDummy;

		public DroppedGearDummy(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class SpecialItemDummy : MonoBehaviour
	{
		public string m_LocalizedDisplayName;

		public DataStr.ExtraDataForDroppedGear m_Extra = new DataStr.ExtraDataForDroppedGear();

		public SpecialItemDummy(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class IgnoreDropOverride : MonoBehaviour
	{
		public IgnoreDropOverride(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class DropFakeOnLeave : MonoBehaviour
	{
		public bool m_ReturnedToWorld;

		public Vector3 m_OldPossition = new Vector3(0f, 0f, 0f);

		public Quaternion m_OldRotation = new Quaternion(0f, 0f, 0f, 0f);

		public DropFakeOnLeave(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class FakeBed : MonoBehaviour
	{
		public FakeBed(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class FakeBedDummy : MonoBehaviour
	{
		public GameObject m_LinkedFakeObject;

		public FakeBedDummy(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class DestoryArrowOnHit : MonoBehaviour
	{
		public DestoryArrowOnHit(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class DestoryStoneOnStop : MonoBehaviour
	{
		public GameObject m_Obj;

		public Rigidbody m_RB;

		public GearItem m_Gear;

		public bool m_ShouldSendDrop;

		private bool m_SendDrop;

		public DestoryStoneOnStop(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if (!(m_Obj != null) || !(m_RB != null) || !(m_Gear != null) || !m_RB.isKinematic)
			{
				return;
			}
			if (m_ShouldSendDrop)
			{
				if (!m_SendDrop)
				{
					m_SendDrop = true;
					MyMod.SendDropItem(m_Gear, 0, 0, samepose: true);
				}
			}
			else
			{
				UnityEngine.Object.Destroy(m_Obj);
			}
		}
	}

	public class ClientProjectile : MonoBehaviour
	{
		public int m_ClientID;

		public ClientProjectile(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class UiButtonPressHook : MonoBehaviour
	{
		public int m_CustomId;

		public UiButtonPressHook(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class UiButtonKeyboardPressSkip : MonoBehaviour
	{
		public Il2CppSystem.Collections.Generic.List<EventDelegate> m_Click;

		public UiButtonKeyboardPressSkip(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class DeathDropContainer : MonoBehaviour
	{
		public string m_Owner = "UNKNOWN";

		public int m_DeathTime = -1;

		public DeathDropContainer(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class DoorLockedOnKey : MonoBehaviour
	{
		public string m_Owner = "UNKNOWN";

		public string m_DoorKey = "";

		public DoorLockedOnKey(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class DoNotSerializeThis : MonoBehaviour
	{
		public DoNotSerializeThis(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class ContainersSync : MonoBehaviour
	{
		public GameObject m_Obj;

		public Container m_Cont;

		public string m_Guid = "";

		public string m_LastAnim = "";

		public bool m_Empty = true;

		public ContainersSync(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if (m_Obj != null)
			{
				if (m_Cont == null && m_Obj.GetComponent<Container>() != null)
				{
					m_Cont = m_Obj.GetComponent<Container>();
				}
				if (m_Guid == "" && m_Obj.GetComponent<ObjectGuid>() != null)
				{
					m_Guid = m_Obj.GetComponent<ObjectGuid>().Get();
				}
				if (m_Guid != "" && m_Cont != null)
				{
					_ = m_Cont.m_IsCorpse;
				}
			}
		}

		public void CallSync()
		{
			DataStr.ContainerOpenSync containerOpenSync = new DataStr.ContainerOpenSync();
			containerOpenSync.m_Guid = m_Guid;
			if (m_LastAnim == "close")
			{
				containerOpenSync.m_State = false;
			}
			else
			{
				containerOpenSync.m_State = true;
			}
			if (MyMod.iAmHost)
			{
				using (new Packet(31))
				{
					ServerSend.CONTAINEROPEN(0, containerOpenSync, toEveryOne: true);
				}
			}
			if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(31))
				{
					packet.Write(containerOpenSync);
					MyMod.SendUDPData(packet);
				}
			}
		}

		public void Open()
		{
			if (!(m_Cont != null) || m_Cont.m_ObjectAnims == null || !(m_LastAnim != "open") || m_Cont.m_PendingClose)
			{
				return;
			}
			// Remember the state before playing: the lid usually sits on a child object, and without this a second
			// "open" (both players opening together) was played over the first and twisted the lid.
			m_LastAnim = "open";
			for (int i = 0; i < m_Cont.m_ObjectAnims.Length; i++)
			{
				ObjectAnim objectAnim = m_Cont.m_ObjectAnims[i];
				if ((bool)objectAnim)
				{
					objectAnim.Play("open");
				}
			}
			if (m_Cont.gameObject != null && GameManager.GetPlayerObject() != null && Vector3.Distance(GameManager.GetPlayerObject().transform.position, m_Cont.gameObject.transform.position) < 30f)
			{
				m_Cont.PlayContainerOpenSound();
			}
		}

		public void Close()
		{
			if (!(m_Cont != null) || m_Cont.m_ObjectAnims == null || !(m_LastAnim != "close"))
			{
				return;
			}
			m_LastAnim = "close";
			for (int i = 0; i < m_Cont.m_ObjectAnims.Length; i++)
			{
				ObjectAnim objectAnim = m_Cont.m_ObjectAnims[i];
				if ((bool)objectAnim)
				{
					objectAnim.Play("close");
				}
			}
			if (m_Cont.gameObject != null && GameManager.GetPlayerObject() != null && Vector3.Distance(GameManager.GetPlayerObject().transform.position, m_Cont.gameObject.transform.position) < 30f)
			{
				m_Cont.PlayContainerCloseSound();
			}
			m_Cont.m_PendingClose = false;
		}
	}

	public class AnimalUpdates : MonoBehaviour
	{
		public GameObject m_Animal;

		public int NoResponce = 5;

		public int ReTakeCoolDown = 5;

		public float nextActionTimeNR;

		public float noresponce_perioud = 1f;

		public float nextActionSync;

		public float actionSync_perioud = 0.1f;

		public float nextActionBloodDrop;

		public float blooddrop_period = 0.15f;

		public float nextActionDampingOn;

		public float dampingOn_perioud = 1.5f;

		public Vector3 m_ToGo = new Vector3(0f, 0f, 0f);

		public Quaternion m_ToRotate = new Quaternion(0f, 0f, 0f, 0f);

		public bool m_CanSync;

		public float m_Hp = 100f;

		public bool m_Banned;

		public bool m_DampingIgnore;

		public int LastFoundPlayer = -1;

		public bool m_MyControlled;

		public bool m_PickedUp;

		public string m_RegionGUID = "";

		public bool m_AddedToRegion;

		public bool m_MarkToDestroy;

		public System.Collections.Generic.List<DataStr.AnimalArrow> m_Arrows = new System.Collections.Generic.List<DataStr.AnimalArrow>();

		public int LastDamager;

		public AnimalUpdates(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Start()
		{
			nextActionTimeNR = Time.time;
			nextActionSync = Time.time;
			nextActionBloodDrop = Time.time;
			nextActionDampingOn = Time.time + dampingOn_perioud;
		}

		public void ProcessArrows()
		{
			for (int i = 0; i < m_Arrows.Count; i++)
			{
				MyMod.AddFakeArrowToAnimal(this, i);
			}
		}

		public void UpdateFeeding()
		{
			if (!m_Animal || (!m_MyControlled && !MyMod.AnimalsController))
			{
				return;
			}
			BaseAi component = m_Animal.GetComponent<BaseAi>();
			if (!component || !component.m_TargetBodyHarvest)
			{
				return;
			}
			if (MyMod.iAmHost)
			{
				string gUID = component.m_TargetBodyHarvest.GetComponent<ObjectGuid>().Get();
				if (MyMod.GetAnimalMeatForFeeding(component.m_TargetBodyHarvest) != null)
				{
					Shared.OnAnimalCorpseChanged(gUID, 0.016f, 0, 0, m_Animal.GetComponent<ObjectGuid>().Get());
				}
			}
			if (MyMod.sendMyPosition)
			{
				string value = component.m_TargetBodyHarvest.GetComponent<ObjectGuid>().Get();
				using Packet packet = new Packet(188);
				packet.Write(value);
				packet.Write(m_Animal.GetComponent<ObjectGuid>().Get());
				MyMod.SendUDPData(packet);
			}
		}

		public void CallSync()
		{
			if (!(m_Animal != null) || m_Banned || !m_Animal.activeSelf || m_PickedUp || !(m_Animal.GetComponent<BaseAi>() != null))
			{
				return;
			}
			BaseAi component = m_Animal.GetComponent<BaseAi>();
			DataStr.AnimalCompactData compactDataForAnimal = MyMod.GetCompactDataForAnimal(component, m_Arrows);
			DataStr.AnimalAnimsSync animationDataFromAnimal = MyMod.GetAnimationDataFromAnimal(component);
			if (compactDataForAnimal == null || animationDataFromAnimal == null || string.IsNullOrEmpty(compactDataForAnimal.m_GUID))
			{
				return;
			}
			int num = (compactDataForAnimal.m_LastController = MyMod.GetClosestPlayerToAnimal(m_Animal, ReTakeCoolDown, ClientUser.myId, GameManager.GetPlayerTransform().position, MyMod.levelid));
			if (num != ClientUser.myId)
			{
				if (MyMod.RecreateAnimalToActor(m_Animal, m_Arrows, num))
				{
					m_Banned = true;
					m_MarkToDestroy = true;
				}
				else
				{
					compactDataForAnimal.m_LastController = ClientUser.myId;
					ReTakeCoolDown = 1;
				}
				MyMod.SendAnimalForValidPlayers(compactDataForAnimal, animationDataFromAnimal);
			}
			else
			{
				MyMod.SendAnimalForValidPlayers(compactDataForAnimal, animationDataFromAnimal);
			}
		}

		private void OnDestroy()
		{
			if ((bool)m_Animal && m_Animal.GetComponent<ObjectGuid>() != null && m_RegionGUID != "")
			{
				GameObject gameObject = ObjectGuidManager.Lookup(m_RegionGUID);
				if (gameObject != null && gameObject.GetComponent<SpawnRegionSimple>() != null)
				{
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
				}
			}
		}

		private void MaybeAddToSpawnRegion()
		{
			if (!m_AddedToRegion && m_RegionGUID != "")
			{
				m_AddedToRegion = true;
				GameObject gameObject = ObjectGuidManager.Lookup(m_RegionGUID);
				if ((bool)gameObject && m_Animal.GetComponent<ObjectGuid>() != null && gameObject.GetComponent<SpawnRegionSimple>() != null)
				{
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Add(m_Animal.GetComponent<ObjectGuid>().Get(), m_Animal);
				}
			}
		}

		public void DropArrows()
		{
			for (int i = 0; i < m_Arrows.Count; i++)
			{
				MyMod.SendDropItem(UnityEngine.Object.Instantiate(MyMod.GetGearItemObject("GEAR_Arrow"), Vector3.zero, Quaternion.identity).GetComponent<GearItem>(), 0, 0, samepose: false, 0, m_Animal);
			}
		}

		private void Update()
		{
			if (m_MarkToDestroy)
			{
				UnityEngine.Object.Destroy(m_Animal);
			}
			else
			{
				if (!(m_Animal != null) || m_PickedUp || m_Banned)
				{
					return;
				}
				MaybeAddToSpawnRegion();
				if (Time.time > nextActionSync)
				{
					nextActionSync += actionSync_perioud;
					// Skipped while leaving to the main menu: the transport is already gone and every animal threw here.
					if (MyMod.level_name != "Empty" && !Shared.IsMainMenuScene(MyMod.level_name) && (MyMod.AnimalsController || m_MyControlled) && MyMod.InOnline() && !CoopReturnToMenu.InProgress)
					{
						try
						{
							CallSync();
						}
						catch (Exception ex)
						{
							MelonLogger.Warning("[Wildlife] Animal sync skipped: " + ex.Message);
						}
					}
				}
				if (Time.time > nextActionTimeNR)
				{
					nextActionTimeNR += noresponce_perioud;
					UpdateFeeding();
					ReTakeCoolDown--;
					if (ReTakeCoolDown <= 0)
					{
						ReTakeCoolDown = 0;
					}
					if (!MyMod.AnimalsController && !m_MyControlled)
					{
						m_MarkToDestroy = true;
					}
				}
				if (!MyMod.InOnline() || !m_Animal.GetComponent<BaseAi>() || !m_Animal.GetComponent<ObjectGuid>() || (!MyMod.AnimalsController && !m_MyControlled))
				{
					return;
				}
				BaseAi component = m_Animal.GetComponent<BaseAi>();
				string animalPrefabName = MyMod.GetAnimalPrefabName(m_Animal.name);
				if (!(component.m_CurrentHP <= 0f) && component.m_CurrentMode != AiMode.Dead && component.m_CurrentMode != AiMode.Stunned)
				{
					return;
				}
				DropArrows();
				DataStr.AnimalKilled animalKilled = new DataStr.AnimalKilled();
				animalKilled.m_Position = m_Animal.transform.position;
				animalKilled.m_Rotation = m_Animal.transform.rotation;
				animalKilled.m_PrefabName = animalPrefabName;
				animalKilled.m_GUID = m_Animal.GetComponent<ObjectGuid>().Get();
				animalKilled.m_LevelGUID = MyMod.level_guid;
				animalKilled.m_CreatedTime = MyMod.MinutesFromStartServer;
				animalKilled.m_Knocked = component.m_CurrentMode == AiMode.Stunned;
				animalKilled.m_RegionGUID = m_RegionGUID;
				if (!animalKilled.m_Knocked)
				{
					MelonLogger.Msg("Animal been killed SpawnRegion " + m_RegionGUID);
				}
				else
				{
					MelonLogger.Msg("Animal been knocked SpawnRegion " + m_RegionGUID);
				}
				m_Banned = true;
				if (MyMod.iAmHost)
				{
					m_MarkToDestroy = true;
					ServerSend.ANIMALCORPSE(0, animalKilled, toAll: true);
					Shared.OnAnimalKilled(animalPrefabName, animalKilled.m_Position, animalKilled.m_Rotation, animalKilled.m_GUID, MyMod.level_guid, m_RegionGUID, animalKilled.m_Knocked);
				}
				else if (MyMod.sendMyPosition)
				{
					using (Packet packet = new Packet(106))
					{
						packet.Write(animalKilled);
						MyMod.SendUDPData(packet);
					}
					m_MarkToDestroy = true;
					MyMod.SpawnAnimalCorpse(animalPrefabName, animalKilled.m_Position, animalKilled.m_Rotation, animalKilled.m_GUID, m_RegionGUID);
				}
			}
		}
	}

	public class AnimalActor : MonoBehaviour
	{
		public GameObject m_Animal;

		public int NoResponce = 5;

		public int ReTakeCoolDown = 5;

		public float nextActionTimeNR;

		public float noresponce_perioud = 1f;

		public float nextActionSync;

		public float actionSync_perioud = 0.3f;

		public float nextActionBloodDrop;

		public float blooddrop_period = 0.15f;

		public float nextActionDampingOn;

		public float dampingOn_perioud = 1.5f;

		public Vector3 m_ToGo = new Vector3(0f, 0f, 0f);

		public Quaternion m_ToRotate = new Quaternion(0f, 0f, 0f, 0f);

		public float AP_TurnAngle;

		public float AP_TurnSpeed;

		public float AP_Speed;

		public float AP_Wounded;

		public float AP_Roll;

		public float AP_Pitch;

		public float AP_TargetHeading;

		public float AP_TargetHeadingSmooth;

		public float AP_TapMeter;

		public int AP_AiState;

		public bool AP_Corpse;

		public bool AP_Dead;

		public int AP_DeadSide;

		public int AP_DamageBodyPart;

		public int AP_AttackId;

		public bool AP_Stunned;

		public float m_Hp = 100f;

		public bool m_Bleeding;

		public int m_ClientController = -1;

		public bool m_Banned;

		public bool m_MarkToDestroy;

		public bool m_DampingIgnore;

		public float m_NextTakeoverAttempt;

		public string m_RegionGUID = "";

		public bool m_AddedToRegion;

		private int m_AnimParameter_TurnAngle;

		private int m_AnimParameter_TurnSpeed;

		private int m_AnimParameter_Speed;

		private int m_AnimParameter_Wounded;

		private int m_AnimParameter_Roll;

		private int m_AnimParameter_Pitch;

		private int m_AnimParameter_TargetHeading;

		private int m_AnimParameter_TargetHeadingSmooth;

		private int m_AnimParameter_TapMeter;

		private int m_AnimParameter_AiState;

		private int m_AnimParameter_Corpse;

		private int m_AnimParameter_Dead;

		private int m_AnimParameter_DamageSide;

		private int m_AnimParameter_DamageBodyPart;

		private int m_AnimParameter_AttackId;

		private int m_AnimParameter_Stunned;

		public System.Collections.Generic.Dictionary<string, int> m_AnimalAnimatorHashes = new System.Collections.Generic.Dictionary<string, int>();

		public bool m_AnimalAnimatorHashesReady;

		public Animator m_Animator;

		public System.Collections.Generic.List<DataStr.AnimalArrow> m_Arrows = new System.Collections.Generic.List<DataStr.AnimalArrow>();

		public uint m_FeedingAudioID;

		public uint m_FleeAudioId;

		public uint m_HoldGroundAudioID;

		public uint m_IdleAudioId;

		public uint m_SleepingLoopAudioID;

		public uint m_StalkingAudioID;

		public uint m_StalkingLoopAudioID;

		public uint m_StuggleAudioId;

		public uint m_WanderAudioId;

		public uint m_HideAndSeekAudioId;

		public uint m_JoinPackAudioId;

		public AiMode m_CurrentMode = AiMode.Idle;

		public AiMode m_NextMode = AiMode.Idle;

		public string m_EnterAttackModeAudio = "";

		public string m_EnterFleeModeAudio = "";

		public string m_HoldGroundAudio = "";

		public string m_IdleAudio = "";

		public string m_SleepingAudio = "";

		public string m_EnterStalkingAudio = "";

		public string m_WanderAudio = "";

		public string m_HideAndSeekAudio = "";

		public string m_JoinPackAudio = "";

		private bool m_AudioReady;

		public AnimalActor(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Start()
		{
			nextActionTimeNR = Time.time;
			nextActionSync = Time.time;
			nextActionBloodDrop = Time.time;
			nextActionDampingOn = Time.time + dampingOn_perioud;
		}

		public void ProcessArrows()
		{
			for (int i = 0; i < m_Arrows.Count; i++)
			{
				MyMod.AddFakeArrowToAnimal(this, i);
			}
		}

		public void AnimInit()
		{
			m_AnimParameter_TurnAngle = Animator.StringToHash("TurnAngle");
			m_AnimParameter_TurnSpeed = Animator.StringToHash("TurnSpeed");
			m_AnimParameter_Speed = Animator.StringToHash("Speed");
			m_AnimParameter_Wounded = Animator.StringToHash("Wounded");
			m_AnimParameter_Roll = Animator.StringToHash("Roll");
			m_AnimParameter_Pitch = Animator.StringToHash("Pitch");
			m_AnimParameter_TargetHeading = Animator.StringToHash("TargetHeading");
			m_AnimParameter_TargetHeadingSmooth = Animator.StringToHash("TargetHeadingSmooth");
			m_AnimParameter_TapMeter = Animator.StringToHash("TapMeter");
			m_AnimParameter_AiState = Animator.StringToHash("AiState");
			m_AnimParameter_Corpse = Animator.StringToHash("Corpse");
			m_AnimParameter_Dead = Animator.StringToHash("Dead");
			m_AnimParameter_DamageSide = Animator.StringToHash("DamageSide");
			m_AnimParameter_DamageBodyPart = Animator.StringToHash("DamageBodyPart");
			m_AnimParameter_AttackId = Animator.StringToHash("AttackId");
			m_AnimParameter_Stunned = Animator.StringToHash("Stunned");
			m_AnimalAnimatorHashesReady = true;
		}

		private void SetAnimations()
		{
			Animator animator = m_Animator;
			if (animator != null)
			{
				if (!m_AnimalAnimatorHashesReady)
				{
					AnimInit();
				}
				animator.SetFloat(m_AnimParameter_TurnAngle, AP_TurnAngle);
				animator.SetFloat(m_AnimParameter_TurnSpeed, AP_TurnSpeed);
				animator.SetFloat(m_AnimParameter_Speed, AP_Speed);
				animator.SetFloat(m_AnimParameter_Wounded, AP_Wounded);
				animator.SetFloat(m_AnimParameter_Roll, AP_Roll);
				animator.SetFloat(m_AnimParameter_Pitch, AP_Pitch);
				animator.SetFloat(m_AnimParameter_TargetHeading, AP_TargetHeading);
				animator.SetFloat(m_AnimParameter_TargetHeadingSmooth, AP_TargetHeadingSmooth);
				animator.SetFloat(m_AnimParameter_TapMeter, AP_TapMeter);
				animator.SetInteger(m_AnimParameter_AiState, AP_AiState);
				animator.SetBool(m_AnimParameter_Corpse, AP_Corpse);
				animator.SetBool(m_AnimParameter_Dead, AP_Dead);
				animator.SetInteger(m_AnimParameter_DamageSide, AP_DeadSide);
				animator.SetInteger(m_AnimParameter_DamageBodyPart, AP_DamageBodyPart);
				animator.SetInteger(m_AnimParameter_AttackId, AP_AttackId);
				animator.SetBool(m_AnimParameter_Stunned, AP_Stunned);
			}
		}

		private void SetPosition()
		{
			if (m_DampingIgnore)
			{
				m_Animal.transform.position = m_ToGo;
				m_Animal.transform.rotation = m_ToRotate;
			}
			else
			{
				float t = 1f - Mathf.Exp((0f - Time.deltaTime) * (float)MyMod.DeltaAnimalsMultiplayer);
				m_Animal.transform.position = Vector3.Lerp(m_Animal.transform.position, m_ToGo, t);
				m_Animal.transform.rotation = Quaternion.Lerp(m_Animal.transform.rotation, m_ToRotate, t);
			}
		}

		private void OnDestroy()
		{
			if (!m_Animal)
			{
				return;
			}
			if (m_Animal.GetComponent<ObjectGuid>() != null)
			{
				MyMod.ActorsList.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
				GameAudioManager.StopAllSoundsFromGameObject(m_Animal);
			}
			if (m_RegionGUID != "")
			{
				GameObject gameObject = ObjectGuidManager.Lookup(m_RegionGUID);
				if (gameObject != null && gameObject.GetComponent<SpawnRegionSimple>() != null)
				{
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
				}
			}
		}

		private void MaybeAddToSpawnRegion()
		{
			if (!m_AddedToRegion && m_RegionGUID != "")
			{
				m_AddedToRegion = true;
				GameObject gameObject = ObjectGuidManager.Lookup(m_RegionGUID);
				if (m_Animal.GetComponent<ObjectGuid>() != null && m_RegionGUID != "" && gameObject != null && gameObject.GetComponent<SpawnRegionSimple>() != null)
				{
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Add(m_Animal.GetComponent<ObjectGuid>().Get(), m_Animal);
				}
			}
		}

		private void ExitFeeding()
		{
			if (m_FeedingAudioID != 0)
			{
				AkSoundEngine.StopPlayingID(m_FeedingAudioID, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
			}
		}

		private void ExitFlee()
		{
			if (m_FleeAudioId != 0)
			{
				AkSoundEngine.StopPlayingID(m_FleeAudioId, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_FleeAudioId = 0u;
			}
		}

		private void ExitHoldGround()
		{
			if (m_HoldGroundAudioID != 0)
			{
				AkSoundEngine.StopPlayingID(m_HoldGroundAudioID, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_HoldGroundAudioID = 0u;
			}
		}

		private void ExitIdle()
		{
			if (m_IdleAudioId != 0)
			{
				AkSoundEngine.StopPlayingID(m_IdleAudioId, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_IdleAudioId = 0u;
			}
		}

		private void ExitInvestigateFood()
		{
			ExitFeeding();
		}

		private void ExitSleep()
		{
			if (m_SleepingLoopAudioID != 0)
			{
				AkSoundEngine.StopPlayingID(m_SleepingLoopAudioID, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_SleepingLoopAudioID = 0u;
			}
		}

		private void ExitStalking()
		{
			if (m_StalkingAudioID != 0)
			{
				AkSoundEngine.StopPlayingID(m_StalkingAudioID, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_StalkingAudioID = 0u;
			}
			if (m_StalkingLoopAudioID != 0)
			{
				AkSoundEngine.StopPlayingID(m_StalkingLoopAudioID, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_StalkingLoopAudioID = 0u;
			}
		}

		private void ExitStruggle()
		{
			if (m_StuggleAudioId != 0)
			{
				AkSoundEngine.StopPlayingID(m_StuggleAudioId, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_StuggleAudioId = 0u;
			}
		}

		private void ExitWander()
		{
			if (m_WanderAudioId != 0)
			{
				AkSoundEngine.StopPlayingID(m_WanderAudioId, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_WanderAudioId = 0u;
			}
		}

		private void ExitHideAndSeek()
		{
			if (m_HideAndSeekAudioId != 0)
			{
				AkSoundEngine.StopPlayingID(m_HideAndSeekAudioId, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_HideAndSeekAudioId = 0u;
			}
		}

		private void ExitJoinPack()
		{
			if (m_JoinPackAudioId != 0)
			{
				AkSoundEngine.StopPlayingID(m_JoinPackAudioId, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_JoinPackAudioId = 0u;
			}
		}

		private void EnterAttack()
		{
			GameAudioManager.Play3DSound(m_EnterAttackModeAudio, m_Animal);
		}

		private void EnterFlee()
		{
			if (!(m_EnterFleeModeAudio == "") && m_FleeAudioId == 0)
			{
				m_FleeAudioId = GameAudioManager.Play3DSound(m_EnterFleeModeAudio, m_Animal);
			}
		}

		private void EnterHoldGround()
		{
			if (!(m_HoldGroundAudio == ""))
			{
				m_HoldGroundAudioID = GameAudioManager.Play3DSound(m_HoldGroundAudio, m_Animal);
			}
		}

		private void EnterIdle()
		{
			if (!(m_IdleAudio == "") && m_IdleAudioId == 0)
			{
				m_IdleAudioId = GameAudioManager.Play3DSound(m_IdleAudio, m_Animal);
			}
		}

		private void EnterSleep()
		{
			if (!(m_SleepingAudio == ""))
			{
				m_SleepingLoopAudioID = GameAudioManager.Play3DSound(m_SleepingAudio, m_Animal);
			}
		}

		private void EnterStalking()
		{
			if (!(m_EnterStalkingAudio == ""))
			{
				m_StalkingAudioID = GameAudioManager.Play3DSound(m_EnterStalkingAudio, m_Animal);
				m_StalkingLoopAudioID = 0u;
			}
		}

		private void EnterWander()
		{
			if (m_WanderAudioId == 0 && !(m_WanderAudio == ""))
			{
				m_WanderAudioId = GameAudioManager.Play3DSound(m_WanderAudio, m_Animal);
			}
		}

		private void EnterHideAndSeek()
		{
			if (m_HideAndSeekAudioId == 0 && m_HideAndSeekAudio != "")
			{
				m_HideAndSeekAudioId = GameAudioManager.Play3DSound(m_HideAndSeekAudio, m_Animal);
			}
		}

		private void EnterJoinPack()
		{
			if (m_JoinPackAudioId == 0 && m_JoinPackAudio != "")
			{
				m_JoinPackAudioId = GameAudioManager.Play3DSound(m_JoinPackAudio, m_Animal);
			}
		}

		public void SetAiMode()
		{
			if (m_CurrentMode != m_NextMode)
			{
				switch (m_CurrentMode)
				{
				case AiMode.Feeding:
					ExitFeeding();
					break;
				case AiMode.Flee:
					ExitFlee();
					break;
				case AiMode.HoldGround:
					ExitHoldGround();
					break;
				case AiMode.Idle:
					ExitIdle();
					break;
				case AiMode.InvestigateFood:
					ExitInvestigateFood();
					break;
				case AiMode.Sleep:
					ExitSleep();
					break;
				case AiMode.Stalking:
					ExitStalking();
					break;
				case AiMode.Struggle:
					ExitStruggle();
					break;
				case AiMode.Wander:
					ExitWander();
					break;
				case AiMode.HideAndSeek:
					ExitHideAndSeek();
					break;
				case AiMode.JoinPack:
					ExitJoinPack();
					break;
				}
				switch (m_NextMode)
				{
				case AiMode.Attack:
					EnterAttack();
					break;
				case AiMode.Flee:
					EnterFlee();
					break;
				case AiMode.HoldGround:
					EnterHoldGround();
					break;
				case AiMode.Idle:
					EnterIdle();
					break;
				case AiMode.Sleep:
					EnterSleep();
					break;
				case AiMode.Stalking:
					EnterStalking();
					break;
				case AiMode.Wander:
					EnterWander();
					break;
				case AiMode.HideAndSeek:
					EnterHideAndSeek();
					break;
				case AiMode.JoinPack:
					EnterJoinPack();
					break;
				}
				m_CurrentMode = m_NextMode;
			}
		}

		private void SetAudioNames()
		{
			string text = m_Animal.gameObject.name;
			if (text.Contains("Bear"))
			{
				m_EnterAttackModeAudio = "Play_BearDetect";
				m_EnterFleeModeAudio = "Play_BearFlee";
				m_HoldGroundAudio = "Play_BearHoldGround";
				m_IdleAudio = "Play_BearIdle";
				m_SleepingAudio = "Play_BearSleeping";
				m_EnterStalkingAudio = "Play_BearAttack";
				m_WanderAudio = "Play_BearIdle";
			}
			else if (text.Contains("Wolf") && !text.Contains("gray"))
			{
				m_EnterAttackModeAudio = "Play_WolfAttackEnter";
				m_EnterFleeModeAudio = "PLAY_WolfWhine";
				m_HoldGroundAudio = "Play_WolfGrowlLoop";
				m_SleepingAudio = "Play_WolfSleeping";
				m_EnterStalkingAudio = "Play_WolfWarn";
			}
			else if (text.Contains("Wolf") && text.Contains("gray"))
			{
				m_EnterAttackModeAudio = "Play_TimberwolfAttackEnter";
				m_EnterFleeModeAudio = "Play_TimberwolfWhine";
				m_HoldGroundAudio = "Play_TimberwolfGrowlLoop";
				m_IdleAudio = "Play_TimberwolfIdle";
				m_SleepingAudio = "Play_TimberwolfSleeping";
				m_EnterStalkingAudio = "Play_TimberwolfWarn";
				m_HideAndSeekAudio = "Play_TimberwolfGrowlLoop";
			}
			else if (text.Contains("Rabbit"))
			{
				m_EnterFleeModeAudio = "Play_RabbitSqueal";
			}
			else if (text.Contains("Moose"))
			{
				m_EnterAttackModeAudio = "Play_MooseAttack";
				m_HoldGroundAudio = "Play_MooseAngry";
				m_EnterStalkingAudio = "Play_MooseAlerted";
			}
			else if (text.Contains("Cougar"))
			{
				m_EnterAttackModeAudio = "Play_Cougar_AI_EnterAttackMode";
				m_EnterFleeModeAudio = "Play_Cougar_AI_EnterFleeMode";
				m_HoldGroundAudio = "Play_Cougar_AI_HoldGround";
				m_IdleAudio = "Play_Cougar_AI_Idle";
				m_SleepingAudio = "Play_Cougar_AI_Sleeping";
				m_EnterStalkingAudio = "Play_Cougar_AI_EnterStalking";
				m_WanderAudio = "Play_Cougar_AI_Wander";
				m_HideAndSeekAudio = "Play_Cougar_AI_Stalking";
			}
			else if (text.Contains("Ptarmigan"))
			{
				m_EnterFleeModeAudio = "Play_PtarmiganFlee";
				m_HoldGroundAudio = "Play_PtarmiganHold";
				m_IdleAudio = "Play_PtarmiganIdle";
			}
			m_AudioReady = true;
		}

		private void Update()
		{
			if (m_MarkToDestroy)
			{
				UnityEngine.Object.Destroy(m_Animal);
			}
			else
			{
				if (!(m_Animal != null) || m_Banned)
				{
					return;
				}
				MaybeAddToSpawnRegion();
				if (!MyMod.AnimalsController || m_ClientController != ClientUser.myId)
				{
					SetAnimations();
					SetPosition();
				}
				if (m_ClientController == ClientUser.myId && Time.time >= m_NextTakeoverAttempt)
				{
					m_NextTakeoverAttempt = Time.time + 1f;
					if (MyMod.RecreateAnimalToSyncable(m_Animal, m_RegionGUID, m_Hp, m_Arrows))
					{
						m_Banned = true;
						m_MarkToDestroy = true;
					}
				}
				if (!m_AudioReady)
				{
					SetAudioNames();
				}
				if (Time.time > nextActionTimeNR)
				{
					nextActionTimeNR += noresponce_perioud;
					NoResponce--;
					if (NoResponce <= 0 && m_ClientController != ClientUser.myId)
					{
						GameAudioManager.StopAllSoundsFromGameObject(m_Animal);
						m_MarkToDestroy = true;
					}
				}
			}
		}
	}

	public class AnimalCorpseObject : MonoBehaviour
	{
		public GameObject m_Animal;

		public string m_RegionGUID = "";

		public bool m_AddedToRegion;

		public AnimalCorpseObject(IntPtr ptr)
			: base(ptr)
		{
		}

		public void Update()
		{
			if (!m_AddedToRegion && m_RegionGUID != "" && (bool)m_Animal.GetComponent<ObjectGuid>())
			{
				GameObject gameObject = ObjectGuidManager.Lookup(m_RegionGUID);
				if ((bool)gameObject && (bool)gameObject.GetComponent<SpawnRegionSimple>())
				{
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Add(m_Animal.GetComponent<ObjectGuid>().Get(), m_Animal);
					MelonLogger.Msg(ConsoleColor.Blue, "Animal corpse added to SpawnRegion " + m_RegionGUID);
					m_AddedToRegion = true;
				}
			}
		}

		private void OnDestroy()
		{
			if (!(m_Animal.GetComponent<ObjectGuid>() != null))
			{
				return;
			}
			MyMod.AnimalCorplesList.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
			if (m_RegionGUID != "")
			{
				GameObject gameObject = ObjectGuidManager.Lookup(m_RegionGUID);
				if ((bool)gameObject && (bool)gameObject.GetComponent<SpawnRegionSimple>())
				{
					gameObject.GetComponent<SpawnRegionSimple>().m_Animals.Remove(m_Animal.GetComponent<ObjectGuid>().Get());
				}
			}
		}
	}

	public class SpawnRegionSimple : MonoBehaviour
	{
		public SpawnRegion m_Region;

		public string m_GUID = "";

		public bool m_RolledToBeDisabled;

		public bool m_ChanceRolled;

		public int m_Spawned;

		public bool m_Banned;

		public bool m_CheckedForBan;

		public bool m_PendingBanCheck;

		public System.Collections.Generic.Dictionary<string, GameObject> m_Animals = new System.Collections.Generic.Dictionary<string, GameObject>();

		public SpawnRegionSimple(IntPtr ptr)
			: base(ptr)
		{
		}

		public void SetBanned(bool Banned)
		{
			m_CheckedForBan = true;
			m_PendingBanCheck = false;
			m_Banned = Banned;
			if (m_Banned)
			{
				MelonLogger.Msg(ConsoleColor.Cyan, "SpawnRegion " + m_Region.GetComponent<ObjectGuid>().Get() + " is banned");
			}
		}

		public void BanCheck()
		{
			m_PendingBanCheck = true;
			if (MyMod.iAmHost)
			{
				SetBanned(Shared.CheckSpawnRegionBanned(m_Region.GetComponent<ObjectGuid>().Get()));
			}
			if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(124))
				{
					packet.Write(m_Region.GetComponent<ObjectGuid>().Get());
					MyMod.SendUDPData(packet);
				}
			}
		}

		public void UpdateFromManager()
		{
			if (!MyMod.CanSpawnWildlifeInCurrentScene() || m_Region == null || !m_Region.isActiveAndEnabled)
			{
				return;
			}
			if (!m_ChanceRolled)
			{
				float percent = m_Region.m_ChanceActive;
				if (m_Region.m_ChanceActive > 85f)
				{
					percent = 85f;
				}
				System.Random rNG = new System.Random(GameManager.GetRandomSeed((int)m_Region.m_Center.x + (int)m_Region.m_Center.y + (int)m_Region.m_Center.z));
				if (!MyMod.RollChanceSeeded(percent, rNG))
				{
					m_RolledToBeDisabled = true;
				}
				m_ChanceRolled = true;
			}
			if (m_RolledToBeDisabled || m_Region.SpawningSupppressedByExperienceMode())
			{
				return;
			}
			if (MyMod.AnyOneClose(m_Region.m_Radius + MyMod.MinimalDistanceForSpawn, m_Region.m_Center) != -1 && MyMod.SpawnInBlizzard(m_Region))
			{
				if (!(m_Region != null) || !MyMod.AnimalsController || Math.Max(m_Spawned, MyMod.CountKnownWildlifeInRegion(m_GUID, m_Animals)) >= MyMod.CalculateTargetPopulation(m_Region))
				{
					return;
				}
				if (m_CheckedForBan)
				{
					if (!m_Banned)
					{
						MyMod.SimulateSpawnFromRegionSpawn(m_Region.GetComponent<ObjectGuid>().Get(), m_Region);
					}
				}
				else if (!m_PendingBanCheck)
				{
					BanCheck();
				}
			}
			else
			{
				if (!(m_Region != null) || m_Animals.Count <= 0)
				{
					return;
				}
				System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>();
				foreach (System.Collections.Generic.KeyValuePair<string, GameObject> animal in m_Animals)
				{
					GameObject value = animal.Value;
					if (value != null && value.GetComponent<BaseAi>() != null)
					{
						BaseAi component = value.GetComponent<BaseAi>();
						if (component.GetAiMode() != AiMode.Flee && component.GetAiMode() != AiMode.Dead && component.GetAiMode() != AiMode.Struggle && (component.m_CurrentTarget == null || !component.m_CurrentTarget.IsPlayer()))
						{
							list.Add(value);
						}
					}
				}
				for (int i = 0; i < list.Count; i++)
				{
					if ((bool)list[i].GetComponent<ObjectGuid>())
					{
						if (MyMod.iAmHost)
						{
							ServerSend.ANIMALDELETE(0, list[i].GetComponent<ObjectGuid>().Get());
						}
						else if (MyMod.sendMyPosition)
						{
							using Packet packet = new Packet(38);
							packet.Write(list[i].GetComponent<ObjectGuid>().Get());
							MyMod.SendUDPData(packet);
						}
					}
					UnityEngine.Object.Destroy(list[i]);
					m_Spawned--;
				}
			}
		}
	}

	public class LobbyHoverNickname : MonoBehaviour
	{
		public Button m_Btn;

		public string m_Name = "???";

		public GameObject HoverObj;

		public Text LableObj;

		public LobbyHoverNickname(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if ((bool)m_Btn && m_Btn.currentSelectionState == Selectable.SelectionState.Highlighted && MyMod.LobbyUI != null)
			{
				if (HoverObj == null)
				{
					HoverObj = MyMod.LobbyUI.transform.GetChild(3).gameObject;
					LableObj = HoverObj.transform.GetChild(0).gameObject.GetComponent<Text>();
				}
				HoverObj.SetActive(value: true);
				HoverObj.transform.position = new Vector3(m_Btn.gameObject.transform.position.x, HoverObj.transform.position.y, HoverObj.gameObject.transform.position.z);
				LableObj.text = m_Name;
			}
		}
	}

	public class CookpotHelmet : MonoBehaviour
	{
		public GearItem m_GearItem;

		public ClothingItem m_ClothingItem;

		public CookpotHelmet(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if ((bool)m_GearItem && (bool)m_ClothingItem)
			{
				if (GameCompat.Panel<Panel_Clothing>() != null && GameCompat.Panel<Panel_Clothing>().isActiveAndEnabled)
				{
					m_GearItem.m_ClothingItem = null;
				}
				else
				{
					m_GearItem.m_ClothingItem = m_ClothingItem;
				}
			}
		}
	}

	public class MultiplayerPlayer : MonoBehaviour
	{
		public int m_ID;

		public System.Collections.Generic.List<GameObject> m_DamageColiders = new System.Collections.Generic.List<GameObject>();

		public string m_HoldingItem = "";

		public string m_HoldingFood = "";

		public bool m_LightSourceOn;

		public bool m_HasAxe;

		public bool m_HasRifle;

		public bool m_HasRevolver;

		public bool m_HasMedkit;

		public int m_Arrows;

		public int m_Flares;

		public int m_BlueFlares;

		public GameObject m_Player;

		public string m_BreakingSound = "";

		public uint m_BreakingSoundReference;

		public int m_BloodLosts;

		public bool m_NeedAntiseptic;

		public uint m_HeavyBreathSoundReference;

		public int m_Character = -1;

		public GameObject m_TorchIgniter;

		public GameObject hand_r;

		public GameObject hand_l;

		public GameObject rifle;

		public GameObject revolver;

		public GameObject quiver;

		public GameObject axe;

		public GameObject medkit;

		public GameObject flares;

		public GameObject body;

		public GameObject clothing;

		public GameObject hip;

		public GameObject root;

		public GameObject extras;

		public GameObject MakenzyHead;

		public GameObject AstridHead;

		public GameObject bookOpen;

		public GameObject bookClosed;

		public GameObject meleeDummy;

		public float nextActionBloodDrop;

		public float blooddrop_period = 1.3f;

		public bool m_IsBeingInteractedWith;

		public float m_InteractTimer;

		public string m_ActionType = "";

		public MultiplayerPlayer(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Start()
		{
			nextActionBloodDrop = Time.time;
		}

		public void UpdateVisualItems()
		{
			if (m_HasRifle && m_HoldingItem != "GEAR_Rifle")
			{
				rifle.SetActive(value: true);
			}
			else
			{
				rifle.SetActive(value: false);
			}
			if (m_HasRevolver && m_HoldingItem != "GEAR_Revolver")
			{
				revolver.SetActive(value: true);
			}
			else
			{
				revolver.SetActive(value: false);
			}
			extras.transform.GetChild(2).gameObject.SetActive(m_HasRevolver);
			axe.SetActive(m_HasAxe);
			medkit.SetActive(m_HasMedkit);
			if (m_Arrows > 0)
			{
				quiver.SetActive(value: true);
				quiver.transform.GetChild(0).gameObject.SetActive(value: false);
				quiver.transform.GetChild(1).gameObject.SetActive(value: false);
				quiver.transform.GetChild(2).gameObject.SetActive(value: false);
				quiver.transform.GetChild(3).gameObject.SetActive(value: false);
				quiver.transform.GetChild(4).gameObject.SetActive(value: false);
				for (int i = 0; i < 4; i++)
				{
					quiver.transform.GetChild(i).gameObject.SetActive(value: true);
				}
			}
			else
			{
				quiver.SetActive(value: false);
			}
			if (m_Flares == 0 && m_BlueFlares == 0)
			{
				flares.SetActive(value: false);
				return;
			}
			flares.SetActive(value: true);
			GameObject gameObject = flares.transform.GetChild(1).gameObject;
			GameObject gameObject2 = flares.transform.GetChild(2).gameObject;
			int num = m_Flares;
			int num2 = m_BlueFlares;
			if (m_HoldingItem == "GEAR_FlareA")
			{
				num--;
			}
			if (m_HoldingItem == "GEAR_BlueFlare")
			{
				num2--;
			}
			if (num == 0)
			{
				gameObject.SetActive(value: false);
			}
			else
			{
				gameObject.SetActive(value: true);
			}
			if (num2 == 0)
			{
				gameObject2.SetActive(value: false);
			}
			else
			{
				gameObject2.SetActive(value: true);
			}
			gameObject.transform.GetChild(0).gameObject.SetActive(value: false);
			gameObject.transform.GetChild(1).gameObject.SetActive(value: false);
			gameObject2.transform.GetChild(0).gameObject.SetActive(value: false);
			gameObject2.transform.GetChild(1).gameObject.SetActive(value: false);
			if (num > 0 && num2 > 0)
			{
				gameObject.transform.GetChild(0).gameObject.SetActive(value: true);
				gameObject2.transform.GetChild(1).gameObject.SetActive(value: true);
				return;
			}
			if (num > 0)
			{
				if (num == 1)
				{
					gameObject.transform.GetChild(0).gameObject.SetActive(value: true);
				}
				else if (num > 1)
				{
					gameObject.transform.GetChild(0).gameObject.SetActive(value: true);
					gameObject.transform.GetChild(1).gameObject.SetActive(value: true);
				}
			}
			if (num2 > 0)
			{
				if (num2 == 1)
				{
					gameObject2.transform.GetChild(0).gameObject.SetActive(value: true);
				}
				else if (num2 > 1)
				{
					gameObject2.transform.GetChild(0).gameObject.SetActive(value: true);
					gameObject2.transform.GetChild(1).gameObject.SetActive(value: true);
				}
			}
		}

		public void SetupMapMarker()
		{
			int childCount = hand_l.transform.GetChild(2).GetChild(0).childCount;
			Transform child = hand_l.transform.GetChild(2).GetChild(0);
			for (int i = 0; i < childCount; i++)
			{
				child.GetChild(i).gameObject.SetActive(value: false);
			}
			Shared.GameRegion currentRegion = RegionCompat.GetCurrentRegion();
			if (currentRegion == Shared.GameRegion.MysteryLake)
			{
				child.GetChild(0).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.PlesantValley)
			{
				child.GetChild(1).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.HushedRiverValley)
			{
				child.GetChild(3).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.CoastalHighWay)
			{
				child.GetChild(4).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.DesolationPoint)
			{
				child.GetChild(6).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.BrokenRailroad)
			{
				child.GetChild(7).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.ForlornMuskeg)
			{
				child.GetChild(8).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.MountainTown)
			{
				child.GetChild(9).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.ForlornMuskeg)
			{
				child.GetChild(10).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.TimberwolfMountain)
			{
				child.GetChild(11).gameObject.SetActive(value: true);
			}
			if (currentRegion == Shared.GameRegion.AshCanyon)
			{
				child.GetChild(12).gameObject.SetActive(value: true);
			}
		}

		public void UpdateHeldItem()
		{
			hand_r.transform.GetChild(0).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(1).gameObject.SetActive(value: false);
			hand_l.transform.GetChild(0).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(2).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(3).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(4).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(5).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(6).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(7).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(8).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(9).gameObject.SetActive(value: false);
			hand_r.transform.GetChild(10).gameObject.SetActive(value: false);
			hand_l.transform.GetChild(2).gameObject.SetActive(value: false);
			hand_l.transform.GetChild(3).gameObject.SetActive(value: false);
			hand_l.transform.GetChild(4).gameObject.SetActive(value: false);
			hand_l.transform.GetChild(5).gameObject.SetActive(value: false);
			string animState = MyMod.playersData[m_ID].m_AnimState;
			if (m_HoldingFood == "" && animState != "Igniting")
			{
				if (m_HoldingItem.StartsWith("GEAR_Flare") != m_HoldingItem.Contains("Gun"))
				{
					hand_r.transform.GetChild(0).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_WoodMatches" || m_HoldingItem == "GEAR_PackMatches")
				{
					hand_r.transform.GetChild(1).gameObject.SetActive(value: true);
					hand_l.transform.GetChild(0).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_BlueFlare")
				{
					hand_r.transform.GetChild(2).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_Rifle")
				{
					hand_r.transform.GetChild(3).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_Revolver")
				{
					hand_r.transform.GetChild(4).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem.StartsWith("GEAR_SprayPaintCan"))
				{
					hand_r.transform.GetChild(5).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_Stone")
				{
					hand_r.transform.GetChild(6).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem.StartsWith("GEAR_KeroseneLamp"))
				{
					hand_r.transform.GetChild(7).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_Torch")
				{
					hand_r.transform.GetChild(8).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_Bow")
				{
					hand_l.transform.GetChild(3).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "Map")
				{
					hand_l.transform.GetChild(2).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_FlareGun")
				{
					hand_r.transform.GetChild(9).gameObject.SetActive(value: true);
				}
				if (m_HoldingItem == "GEAR_NoiseMaker")
				{
					hand_r.transform.GetChild(10).gameObject.SetActive(value: true);
				}
			}
			else
			{
				if (m_HoldingFood != "")
				{
					if (m_HoldingFood == "Water")
					{
						hand_l.transform.GetChild(4).gameObject.SetActive(value: true);
					}
					else
					{
						hand_l.transform.GetChild(5).gameObject.SetActive(value: true);
					}
				}
				if (animState == "Igniting")
				{
					hand_r.transform.GetChild(1).gameObject.SetActive(value: true);
					hand_l.transform.GetChild(0).gameObject.SetActive(value: true);
				}
			}
			if (m_LightSourceOn)
			{
				hand_r.transform.GetChild(0).GetChild(1).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(0).GetChild(2).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(1).GetChild(0).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(1).GetChild(1).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(1).GetChild(3).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(2).GetChild(1).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(2).GetChild(2).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(7).GetChild(0).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(8).GetChild(1).gameObject.SetActive(value: true);
			}
			else
			{
				hand_r.transform.GetChild(0).GetChild(1).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(0).GetChild(2).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(1).GetChild(0).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(1).GetChild(1).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(1).GetChild(3).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(2).GetChild(1).gameObject.SetActive(value: true);
				hand_r.transform.GetChild(2).GetChild(2).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(7).GetChild(0).gameObject.SetActive(value: false);
				hand_r.transform.GetChild(8).GetChild(1).gameObject.SetActive(value: false);
			}
			hand_r.transform.GetChild(0).GetChild(3).gameObject.SetActive(m_LightSourceOn);
			hand_r.transform.GetChild(1).GetChild(4).gameObject.SetActive(m_LightSourceOn);
			hand_r.transform.GetChild(2).GetChild(3).gameObject.SetActive(m_LightSourceOn);
			hand_r.transform.GetChild(7).GetChild(3).gameObject.SetActive(m_LightSourceOn);
			hand_r.transform.GetChild(8).GetChild(0).gameObject.SetActive(m_LightSourceOn);
		}

		private void UpdateOtherAffictions()
		{
			DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[m_ID];
			m_NeedAntiseptic = multiPlayerClientData.m_NeedAntiseptic;
		}

		private void BloodLostUpdate()
		{
			DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[m_ID];
			m_BloodLosts = multiPlayerClientData.m_BloodLosts;
			if (m_BloodLosts <= 1)
			{
				blooddrop_period = 1.3f;
			}
			else if (m_BloodLosts == 2)
			{
				blooddrop_period = 1f;
			}
			else if (m_BloodLosts == 3)
			{
				blooddrop_period = 0.7f;
			}
			else if (m_BloodLosts >= 4)
			{
				blooddrop_period = 0.5f;
			}
			if (!(Time.time > nextActionBloodDrop))
			{
				return;
			}
			nextActionBloodDrop += blooddrop_period;
			int levelid = multiPlayerClientData.m_Levelid;
			string levelGuid = multiPlayerClientData.m_LevelGuid;
			if (m_BloodLosts <= 0 || MyMod.levelid != levelid || !(MyMod.level_guid == levelGuid))
			{
				return;
			}
			MyMod.PlayMultiplayer3dAduio("PLAY_BLOODDROPS_3D", m_ID);
			Vector3 position = root.transform.position;
			position.y += 1f;
			Vector2 insideUnitCircle = UnityEngine.Random.insideUnitCircle;
			insideUnitCircle.Normalize();
			Vector2 vector = insideUnitCircle * UnityEngine.Random.Range(0f, 0.75f);
			position.x += vector.x;
			position.z += vector.y;
			position -= root.transform.forward * 0.5f;
			if (Physics.Raycast(position, Vector3.down, out var hitInfo, float.PositiveInfinity, Utils.m_PhysicalCollisionLayerMask) && !(hitInfo.collider == null))
			{
				Vector3 scale = new Vector3(0.05f, 2f, 0.05f) * UnityEngine.Random.Range(0.5f, 2f);
				int uvRectangleIndex = 7;
				if (Utils.RollChance(50f))
				{
					uvRectangleIndex = 6;
				}
				GameManager.GetDynamicDecalsManager().CreateDecal(hitInfo.point, m_Player.transform.rotation.eulerAngles.y, hitInfo.normal, uvRectangleIndex, scale, DecalProjectorType.PlayerBlood, GameManager.GetWeatherComponent().IsIndoorEnvironment());
			}
		}

		private void DoneAction()
		{
			MyMod.LongActionFinished(this, m_ActionType);
			MyMod.LongActionCanceled(this);
		}

		private void UpdateHead()
		{
			DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[m_ID];
			if (m_Character != multiPlayerClientData.m_Character)
			{
				MakenzyHead.SetActive(value: false);
				AstridHead.SetActive(value: false);
				if (multiPlayerClientData.m_Character == 0)
				{
					MakenzyHead.SetActive(value: true);
				}
				if (multiPlayerClientData.m_Character == 1)
				{
					AstridHead.SetActive(value: true);
				}
			}
		}

		private void Update()
		{
			if (!(m_Player != null))
			{
				return;
			}
			if (hand_r == null)
			{
				int index = 0;
				int index2 = 1;
				int index3 = 2;
				int index4 = 3;
				int index5 = 8;
				clothing = m_Player.transform.GetChild(index).gameObject;
				body = m_Player.transform.GetChild(index2).gameObject;
				extras = m_Player.transform.GetChild(index3).gameObject;
				hip = m_Player.transform.GetChild(index4).gameObject;
				root = hip.transform.GetChild(index5).gameObject;
				hand_r = root.transform.GetChild(0).GetChild(0).GetChild(0)
					.GetChild(1)
					.GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.gameObject;
				hand_l = root.transform.GetChild(0).GetChild(0).GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.gameObject;
				rifle = root.transform.GetChild(0).GetChild(1).gameObject;
				revolver = root.transform.GetChild(2).GetChild(1).gameObject;
				quiver = root.transform.GetChild(0).GetChild(2).gameObject;
				axe = root.transform.GetChild(0).GetChild(3).gameObject;
				medkit = root.transform.GetChild(0).GetChild(4).gameObject;
				flares = root.transform.GetChild(0).GetChild(5).gameObject;
				bookOpen = hand_l.transform.GetChild(6).gameObject;
				bookClosed = hand_l.transform.GetChild(7).gameObject;
				meleeDummy = hand_r.transform.GetChild(11).gameObject;
				MakenzyHead = body.transform.GetChild(1).GetChild(0).gameObject;
				AstridHead = body.transform.GetChild(1).GetChild(1).gameObject;
			}
			DataStr.MultiPlayerClientData multiPlayerClientData = MyMod.playersData[m_ID];
			BloodLostUpdate();
			UpdateOtherAffictions();
			FlashlightSync.UpdateRemote(this);
			if (m_IsBeingInteractedWith)
			{
				m_InteractTimer -= Time.deltaTime;
				if ((double)m_InteractTimer <= 0.0)
				{
					DoneAction();
				}
			}
			DataStr.PlayerEquipmentData playerEquipmentData = multiPlayerClientData.m_PlayerEquipmentData;
			if (playerEquipmentData.m_HoldingItem != m_HoldingItem || playerEquipmentData.m_LightSourceOn != m_LightSourceOn)
			{
				m_HoldingItem = playerEquipmentData.m_HoldingItem;
				m_LightSourceOn = playerEquipmentData.m_LightSourceOn;
				UpdateHeldItem();
				UpdateVisualItems();
			}
			if (playerEquipmentData.m_HasAxe != m_HasAxe || playerEquipmentData.m_HasMedkit != m_HasMedkit || playerEquipmentData.m_HasRifle != m_HasRifle || playerEquipmentData.m_HasRevolver != m_HasRevolver || playerEquipmentData.m_Arrows != m_Arrows || playerEquipmentData.m_Flares != m_Flares || playerEquipmentData.m_BlueFlares != m_BlueFlares)
			{
				m_HasMedkit = playerEquipmentData.m_HasMedkit;
				m_HasRifle = playerEquipmentData.m_HasRifle;
				m_HasRevolver = playerEquipmentData.m_HasRevolver;
				m_Arrows = playerEquipmentData.m_Arrows;
				m_Flares = playerEquipmentData.m_Flares;
				m_BlueFlares = playerEquipmentData.m_BlueFlares;
				UpdateVisualItems();
			}
			Vector3 position = multiPlayerClientData.m_Position;
			Quaternion rotation = multiPlayerClientData.m_Rotation;
			if (!multiPlayerClientData.m_Female)
			{
				position = multiPlayerClientData.m_Position;
				m_Player.transform.localScale = new Vector3(1f, 1f, 1f);
			}
			else
			{
				position = new Vector3(multiPlayerClientData.m_Position.x, multiPlayerClientData.m_Position.y - 0.194f, multiPlayerClientData.m_Position.z);
				m_Player.transform.localScale = new Vector3(0.867f, 1f, 0.867f);
			}
			if (m_HoldingItem == "Book")
			{
				if (multiPlayerClientData.m_AnimState == "Idle")
				{
					bookOpen.SetActive(value: true);
					bookClosed.SetActive(value: false);
				}
				else
				{
					bookOpen.SetActive(value: false);
					bookClosed.SetActive(value: true);
				}
			}
			else
			{
				bookOpen.SetActive(value: false);
				bookClosed.SetActive(value: false);
			}
			Vector3 position2 = Vector3.Lerp(m_Player.transform.position, position, Time.deltaTime * 20f);
			Quaternion rotation2 = Quaternion.Lerp(m_Player.transform.rotation, rotation, Time.deltaTime * 20f);
			m_Player.transform.position = position2;
			m_Player.transform.rotation = rotation2;
			string brakingSounds = multiPlayerClientData.m_BrakingSounds;
			if (m_BreakingSound != brakingSounds)
			{
				m_BreakingSound = brakingSounds;
				if (m_BreakingSoundReference != 0)
				{
					AkSoundEngine.StopPlayingID(m_BreakingSoundReference, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
					m_BreakingSoundReference = 0u;
				}
				if (m_BreakingSound != "")
				{
					m_BreakingSoundReference = GameAudioManager.PlaySound(brakingSounds, m_Player);
				}
			}
			if (multiPlayerClientData.m_HeavyBreath)
			{
				if (m_HeavyBreathSoundReference == 0)
				{
					m_HeavyBreathSoundReference = GameAudioManager.PlaySound("PLAY_BREATH_3D", m_Player);
				}
			}
			else if (m_HeavyBreathSoundReference != 0)
			{
				AkSoundEngine.StopPlayingID(m_HeavyBreathSoundReference, GameAudioManager.Instance.m_StopAudioFadeOutMicroseconds);
				m_HeavyBreathSoundReference = 0u;
			}
		}
	}

	public class MultiplayerPlayerClothingManager : MonoBehaviour
	{
		public string m_Hat = "";

		public string m_Top = "";

		public string m_Bottom = "";

		public string m_Boots = "";

		public string m_Scarf = "";

		public string m_Balaclava = "";

		public int m_DebugB;

		public int m_DebugT;

		public bool m_Debug;

		public GameObject m_Player;

		public GameObject clothing;

		public GameObject arms_middle;

		public GameObject arms_short;

		public GameObject arms_tiny;

		public GameObject arms_long;

		public MultiplayerPlayerClothingManager(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if (m_Player != null)
			{
				bool flag = false;
				if (clothing == null)
				{
					int index = 0;
					clothing = m_Player.transform.GetChild(index).gameObject;
					arms_middle = m_Player.transform.GetChild(1).GetChild(0).GetChild(0)
						.gameObject;
					arms_short = m_Player.transform.GetChild(1).GetChild(0).GetChild(1)
						.gameObject;
					arms_tiny = m_Player.transform.GetChild(1).GetChild(0).GetChild(2)
						.gameObject;
					arms_long = m_Player.transform.GetChild(1).GetChild(0).GetChild(3)
						.gameObject;
					flag = true;
				}
				int iD = m_Player.GetComponent<MultiplayerPlayer>().m_ID;
				DataStr.PlayerClothingData playerClothingData = MyMod.playersData[iD].m_PlayerClothingData;
				if (playerClothingData.m_Hat != m_Hat || playerClothingData.m_Top != m_Top || playerClothingData.m_Bottom != m_Bottom || playerClothingData.m_Boots != m_Boots || playerClothingData.m_Scarf != m_Scarf || playerClothingData.m_Balaclava != m_Balaclava || flag)
				{
					m_Hat = playerClothingData.m_Hat;
					m_Top = playerClothingData.m_Top;
					m_Bottom = playerClothingData.m_Bottom;
					m_Boots = playerClothingData.m_Boots;
					m_Scarf = playerClothingData.m_Scarf;
					m_Balaclava = playerClothingData.m_Balaclava;
					UpdateClothing();
				}
			}
		}

		public void UpdateClothing()
		{
			if (!(clothing != null))
			{
				return;
			}
			int iD = m_Player.GetComponent<MultiplayerPlayer>().m_ID;
			int childCount = clothing.transform.GetChild(0).childCount;
			int childCount2 = clothing.transform.GetChild(1).childCount;
			int childCount3 = clothing.transform.GetChild(2).childCount;
			int childCount4 = clothing.transform.GetChild(3).childCount;
			int num = -1;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			string text = "";
			string text2 = "Full";
			bool flag = false;
			int num6 = MyMod.playersData[iD].m_Character;
			GameObject gameObject = null;
			GameObject gameObject2 = null;
			if (m_Balaclava != "")
			{
				switch (num6)
				{
				case 0:
					num6 = 2;
					break;
				case 1:
					num6 = 3;
					break;
				}
			}
			for (int i = 0; i < m_Player.transform.GetChild(1).GetChild(1).childCount; i++)
			{
				m_Player.transform.GetChild(1).GetChild(1).GetChild(i)
					.gameObject.SetActive(value: false);
			}
			m_Player.transform.GetChild(1).GetChild(1).GetChild(num6)
				.gameObject.SetActive(value: true);
			if (num6 == 0)
			{
				gameObject2 = m_Player.transform.GetChild(1).GetChild(1).GetChild(0)
					.GetChild(2)
					.gameObject;
				gameObject = m_Player.transform.GetChild(1).GetChild(1).GetChild(0)
					.GetChild(3)
					.gameObject;
			}
			for (int j = 0; j < childCount; j++)
			{
				clothing.transform.GetChild(0).transform.GetChild(j).gameObject.SetActive(value: false);
			}
			for (int k = 0; k < childCount2; k++)
			{
				clothing.transform.GetChild(1).transform.GetChild(k).gameObject.SetActive(value: false);
			}
			for (int l = 0; l < childCount3; l++)
			{
				clothing.transform.GetChild(2).transform.GetChild(l).gameObject.SetActive(value: false);
			}
			for (int m = 0; m < childCount4; m++)
			{
				clothing.transform.GetChild(3).transform.GetChild(m).gameObject.SetActive(value: false);
			}
			if (m_Hat == "")
			{
				num = -1;
			}
			else if (m_Hat.Contains("BaseballCap"))
			{
				num = 0;
				if (num6 == 0)
				{
					text2 = "Short";
				}
			}
			else if (m_Hat.Contains("BasicWoolHat") || m_Hat.Contains("GEAR_WillToque"))
			{
				num = 1;
			}
			else if (m_Hat.Contains("Toque"))
			{
				num = 4;
			}
			else if (m_Hat.Contains("RabbitskinHat"))
			{
				num = 5;
			}
			else if (m_Hat.Contains("ImprovisedHat"))
			{
				num = 6;
			}
			else if (m_Hat.Contains("WoolWrapCap"))
			{
				num = 7;
				if (num6 == 0)
				{
					text2 = "Short";
				}
			}
			else
			{
				num = 1;
			}
			if (m_Top == "")
			{
				num2 = 0;
				text = "None";
			}
			else if (m_Top.Contains("CottonShirt"))
			{
				num2 = 1;
				text = "Middle";
			}
			else if (m_Top.Contains("AstridSweater") || m_Top.Contains("FleeceSweater"))
			{
				num2 = 2;
				text = "Short";
			}
			else if (m_Top.Contains("WillSweater"))
			{
				num2 = 3;
				text = "Short";
			}
			else if (m_Top.Contains("FishermanSweater"))
			{
				num2 = 4;
				text = "Short";
			}
			else if (m_Top.Contains("HeavyWoolSweater"))
			{
				num2 = 5;
				text = "Short";
			}
			else if (m_Top.Contains("PlaidShirt") || m_Top.Contains("MackinawJacket"))
			{
				num2 = 6;
				text = "Short";
			}
			else if (m_Top.Contains("CottonHoodie"))
			{
				num2 = 7;
				text = "Tiny";
			}
			else if (m_Top.Contains("CowichanSweater"))
			{
				num2 = 8;
				text = "Short";
			}
			else if (m_Top.Contains("WillShirt"))
			{
				num2 = 9;
				text = "Short";
			}
			else if (m_Top.Contains("WoolShirt"))
			{
				num2 = 10;
				text = "Short";
			}
			else if (m_Top.Contains("WoolSweater") && !m_Top.Contains("HeavyWoolSweater"))
			{
				num2 = 11;
				text = "Short";
			}
			else if (m_Top.Contains("DownVest") || m_Top.Contains("InsulatedVest"))
			{
				num2 = 12;
				text = "Long";
			}
			else if (m_Top.Contains("LightParka"))
			{
				num2 = 13;
				text = "Tiny";
			}
			else if ((m_Top.Contains("SkiJacket") && !m_Top.Contains("Down")) || m_Top.Contains("PremiumWinterCoat"))
			{
				num2 = 14;
				text = "Tiny";
			}
			else if (m_Top.Contains("DownSkiJacket"))
			{
				num2 = 15;
				text = "Tiny";
			}
			else if (m_Top.Contains("BearSkinCoat"))
			{
				num2 = 16;
				text = "Tiny";
			}
			else if (m_Top.Contains("MooseHideCloak"))
			{
				num2 = 17;
				text = "Tiny";
			}
			else if (m_Top.Contains("WolfSkinCape"))
			{
				num2 = 18;
				text = "Tiny";
			}
			else
			{
				num2 = 7;
				text = "Tiny";
			}
			arms_middle.SetActive(value: false);
			arms_short.SetActive(value: false);
			arms_tiny.SetActive(value: false);
			arms_long.SetActive(value: false);
			if (text != "None")
			{
				switch (text)
				{
				case "Tiny":
					arms_tiny.SetActive(value: true);
					break;
				case "Short":
					arms_short.SetActive(value: true);
					break;
				case "Middle":
					arms_middle.SetActive(value: true);
					break;
				case "Long":
					arms_long.SetActive(value: true);
					break;
				}
			}
			num3 = ((!(m_Bottom == "")) ? (m_Bottom.Contains("LongUnderwear") ? ((!m_Bottom.Contains("Wool")) ? 1 : 2) : ((m_Bottom.Contains("Jeans") || m_Bottom.Contains("GEAR_WillPants")) ? 3 : (m_Bottom.Contains("CombatPants") ? 4 : (m_Bottom.Contains("InsulatedPants") ? 5 : (m_Bottom.Contains("CargoPants") ? 6 : (m_Bottom.Contains("WorkPants") ? 7 : ((!m_Bottom.Contains("DeerSkinPants")) ? 3 : 8))))))) : 0);
			num4 = ((!(m_Boots == "")) ? (m_Boots.Contains("LeatherShoes") ? 1 : (m_Boots.Contains("CombatBoots") ? 2 : ((m_Boots.Contains("WorkBoots") || m_Boots.Contains("GEAR_BasicBoots") || m_Boots.Contains("GreyMotherBoots") || m_Boots.Contains("WillBoots") || m_Boots.Contains("AstridBoots") || m_Boots.Contains("MuklukBoots") || m_Boots.Contains("DeerSkinBoots")) ? 3 : (m_Boots.Contains("InsulatedBoots") ? 4 : (m_Boots.Contains("CottonSocks") ? 5 : (m_Boots.Contains("WoolSocks") ? 6 : (m_Boots.Contains("ClimbingSocks") ? 7 : ((!m_Boots.Contains("BasicShoes")) ? 3 : 8)))))))) : 0);
			if (m_Debug)
			{
				num2 = m_DebugT;
				num3 = m_DebugB;
				arms_tiny.SetActive(value: true);
			}
			num5 = ((!(m_Scarf != "")) ? (-1) : ((num2 != 0) ? 3 : 2));
			if (num != -1)
			{
				clothing.transform.GetChild(0).transform.GetChild(num).gameObject.SetActive(value: true);
			}
			if (num5 != -1)
			{
				clothing.transform.GetChild(0).transform.GetChild(num5).gameObject.SetActive(value: true);
			}
			if (flag)
			{
				clothing.transform.GetChild(1).transform.GetChild(0).gameObject.SetActive(value: true);
			}
			if (gameObject != null && gameObject2 != null)
			{
				if (text2 == "Full")
				{
					gameObject.SetActive(value: false);
					gameObject2.SetActive(value: true);
				}
				else if (text2 == "Short")
				{
					gameObject.SetActive(value: true);
					gameObject2.SetActive(value: false);
				}
			}
			clothing.transform.GetChild(1).transform.GetChild(num2).gameObject.SetActive(value: true);
			clothing.transform.GetChild(2).transform.GetChild(num3).gameObject.SetActive(value: true);
			clothing.transform.GetChild(3).transform.GetChild(num4).gameObject.SetActive(value: true);
		}
	}

	public class MultiplayerPlayerAnimator : MonoBehaviour
	{
		public Animator m_Animer;

		public string m_AnimStateHands = "No";

		public string m_PreAnimStateHands = "";

		public string m_AnimStateFingers = "No";

		public bool m_IsDrink;

		public bool m_MyDoll;

		public MultiplayerPlayerAnimator(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if (!(m_Animer != null))
			{
				return;
			}
			GameObject gameObject = m_Animer.gameObject;
			int index = ClientUser.myId;
			string text;
			string text2;
			if (!m_MyDoll)
			{
				index = gameObject.GetComponent<MultiplayerPlayer>().m_ID;
				if (MyMod.playersData[index] != null && MyMod.playersData[index].m_Levelid != MyMod.levelid)
				{
					return;
				}
				text = MyMod.playersData[index].m_AnimState;
				text2 = gameObject.GetComponent<MultiplayerPlayer>().m_HoldingItem;
			}
			else
			{
				text = MyMod.MyAnimState;
				text2 = MyMod.MyLightSourceName;
			}
			int tagHash = m_Animer.GetCurrentAnimatorStateInfo(0).tagHash;
			int num = Animator.StringToHash(text);
			m_Animer.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			if (text == "Fight")
			{
				num = Animator.StringToHash("Knock");
			}
			if (tagHash != num || (tagHash == Animator.StringToHash("Idle") && text2 == "Book"))
			{
				if (text == "Walk")
				{
					m_Animer.Play("Walk", 0);
				}
				if (text == "Idle")
				{
					if (tagHash == Animator.StringToHash("Harvesting"))
					{
						m_Animer.Play("StopHarvesting", 0);
					}
					else if (tagHash == Animator.StringToHash("HarvestingStanding"))
					{
						m_Animer.Play("StopHarvestingStanding", 0);
					}
					else if (tagHash == Animator.StringToHash("Igniting"))
					{
						m_Animer.Play("StopIgniting", 0);
						gameObject.GetComponent<MultiplayerPlayer>().UpdateHeldItem();
					}
					else if (tagHash == Animator.StringToHash("Ctrl"))
					{
						m_Animer.Play("Sit_To_Idle", 0);
					}
					else if (text2 != "Book")
					{
						m_Animer.Play("Idle", 0);
					}
					else if (tagHash != Animator.StringToHash("ReadingBook"))
					{
						if (tagHash == Animator.StringToHash("Ctrl"))
						{
							m_Animer.Play("StartReadSit", 0);
						}
						else
						{
							m_Animer.Play("StartRead", 0);
						}
					}
				}
				if (text == "Run")
				{
					m_Animer.Play("Run", 0);
				}
				if (text == "Ctrl")
				{
					string text3 = m_Animer.GetCurrentAnimatorClipInfo(0)[0].clip.name;
					if (tagHash == Animator.StringToHash("Harvesting"))
					{
						m_Animer.Play("StopHarvestingSit", 0);
					}
					else if (tagHash == Animator.StringToHash("HarvestingStanding"))
					{
						m_Animer.Play("StopHarvestingStandingSit", 0);
					}
					else if (tagHash == Animator.StringToHash("Igniting"))
					{
						m_Animer.Play("StopIgnitingSit", 0);
						gameObject.GetComponent<MultiplayerPlayer>().UpdateHeldItem();
					}
					else if (text3 != "Male Crouch Pose" && text3 != "Idle_To_Sit")
					{
						m_Animer.Play("Idle_To_Sit", 0);
					}
					else
					{
						m_Animer.Play("Ctrl", 0);
					}
				}
				if (text == "Sit")
				{
					m_Animer.Play("Sitting Idle", 0);
				}
				if (text == "Flex")
				{
					m_Animer.Play("Samba Dancing", 0);
				}
				if (text == "Cringe2")
				{
					m_Animer.Play("Cringe2", 0);
				}
				if (text == "Cringe1")
				{
					m_Animer.Play("Cringe1", 0);
				}
				if (text == "Knock" || text == "Fight")
				{
					m_Animer.Play("Knock", 0);
				}
				if (text == "Map")
				{
					m_Animer.Play("Map", 0);
				}
				if (text == "Sleep")
				{
					m_Animer.Play("Sleep", 0);
				}
				if (text == "RopeIdle")
				{
					string text4 = m_Animer.GetCurrentAnimatorClipInfo(0)[0].clip.name;
					if (text4 == "RopeMove" && text4 != "RopeStop")
					{
						m_Animer.Play("RopeStop", 0);
					}
					else
					{
						m_Animer.Play("RopeIdle", 0);
					}
				}
				if (text == "RopeMoving")
				{
					string text5 = m_Animer.GetCurrentAnimatorClipInfo(0)[0].clip.name;
					if (text5 == "RopeIdle" && text5 != "RopeStart")
					{
						m_Animer.Play("RopeStart", 0);
					}
					else
					{
						m_Animer.Play("RopeMoving", 0);
					}
				}
				if (text == "Eating")
				{
					if (!MyMod.ServerConfig.m_FastConsumption)
					{
						m_Animer.Play("Eating", 0);
					}
					else
					{
						m_Animer.Play("EatingFast", 0);
					}
				}
				if (text == "Drinking")
				{
					if (!MyMod.ServerConfig.m_FastConsumption)
					{
						m_Animer.Play("Drinking", 0);
					}
					else
					{
						m_Animer.Play("DrinkingFast", 0);
					}
				}
				if (text == "Harvesting")
				{
					if (tagHash == Animator.StringToHash("Idle"))
					{
						m_Animer.Play("StartHarvesting", 0);
					}
					else if (tagHash == Animator.StringToHash("Ctrl"))
					{
						m_Animer.Play("StartHarvestingSit", 0);
					}
					else
					{
						m_Animer.Play("Harvesting", 0);
					}
				}
				if (text == "HarvestingStanding")
				{
					if (tagHash == Animator.StringToHash("Idle"))
					{
						m_Animer.Play("StartHarvestingStanding", 0);
					}
					else if (tagHash == Animator.StringToHash("Ctrl"))
					{
						m_Animer.Play("StartHarvestingStandingSit", 0);
					}
					else
					{
						m_Animer.Play("HarvestingStanding", 0);
					}
				}
				if (text == "Igniting")
				{
					gameObject.GetComponent<MultiplayerPlayer>().UpdateHeldItem();
					if (tagHash == Animator.StringToHash("Idle"))
					{
						m_Animer.Play("StartIgniting", 0);
					}
					else if (tagHash == Animator.StringToHash("Ctrl"))
					{
						m_Animer.Play("StartIgnitingSit", 0);
					}
					else
					{
						m_Animer.Play("StartIgniting", 0);
					}
				}
			}
			if (m_AnimStateHands == "UnAimGunDone")
			{
				m_AnimStateHands = "No";
			}
			switch (text)
			{
			case "Drinking":
			case "Eating":
			case "Harvesting":
			case "HarvestingStanding":
			case "Igniting":
				m_PreAnimStateHands = "";
				m_AnimStateHands = "No";
				break;
			default:
				if (text2 == "GEAR_Rifle")
				{
					if (text != "Ctrl")
					{
						if (m_AnimStateHands != "Rifle" && m_AnimStateHands != "Rifle_Sit" && m_AnimStateHands != "RifleAim")
						{
							m_PreAnimStateHands = "Pick";
						}
						if ((m_MyDoll && MyMod.MyIsAiming) || (!m_MyDoll && MyMod.playersData[index].m_Aiming))
						{
							m_AnimStateHands = "RifleAim";
						}
						else
						{
							m_AnimStateHands = "Rifle";
						}
					}
					else
					{
						if (m_AnimStateHands != "Rifle" && m_AnimStateHands != "Rifle_Sit" && m_AnimStateHands != "RifleAim_Sit")
						{
							m_PreAnimStateHands = "Pick_Sit";
						}
						if ((m_MyDoll && MyMod.MyIsAiming) || (!m_MyDoll && MyMod.playersData[index].m_Aiming))
						{
							m_AnimStateHands = "RifleAim_Sit";
						}
						else
						{
							m_AnimStateHands = "Rifle_Sit";
						}
					}
				}
				else if (text2.StartsWith("GEAR_Revolver"))
				{
					if (text == "HoldGun")
					{
						if (m_AnimStateHands != "HoldGun" && m_AnimStateHands != "HoldGun_Sit")
						{
							m_PreAnimStateHands = "AimGun";
						}
						m_AnimStateHands = "HoldGun";
					}
					else if (text == "HoldGun_Sit")
					{
						if (m_AnimStateHands != "HoldGun" && m_AnimStateHands != "HoldGun_Sit")
						{
							m_PreAnimStateHands = "AimGun_Sit";
						}
						m_AnimStateHands = "HoldGun_Sit";
					}
					else if (m_AnimStateHands == "HoldGun")
					{
						m_PreAnimStateHands = "UnAimGun";
						m_AnimStateHands = "No";
					}
					else
					{
						m_AnimStateHands = "No";
					}
				}
				else if (text2.StartsWith("GEAR_KeroseneLamp"))
				{
					m_PreAnimStateHands = "";
					m_AnimStateHands = "HoldLantern";
				}
				else if (text2 == "GEAR_Bow")
				{
					m_AnimStateHands = "Bow";
				}
				else
				{
					m_PreAnimStateHands = "";
					m_AnimStateHands = "No";
				}
				break;
			}
			int tagHash2 = m_Animer.GetCurrentAnimatorStateInfo(1).tagHash;
			int num2 = Animator.StringToHash(m_AnimStateHands);
			if (tagHash2 != num2)
			{
				if (m_PreAnimStateHands == "")
				{
					if (tagHash2 == Animator.StringToHash("Rifle") && m_AnimStateHands == "RifleAim")
					{
						m_Animer.Play("StartRifleAim", 1);
					}
					else if (tagHash2 == Animator.StringToHash("RifleAim") && m_AnimStateHands == "Rifle")
					{
						m_Animer.Play("EndRifleAim", 1);
					}
					else if (tagHash2 == Animator.StringToHash("Rifle_Sit") && m_AnimStateHands == "RifleAim_Sit")
					{
						m_Animer.Play("StartRifleAim_Sit", 1);
					}
					else if (tagHash2 == Animator.StringToHash("RifleAim_Sit") && m_AnimStateHands == "Rifle_Sit")
					{
						m_Animer.Play("EndRifleAim_Sit", 1);
					}
					else
					{
						m_Animer.Play(m_AnimStateHands, 1);
					}
				}
				else
				{
					m_Animer.Play(m_PreAnimStateHands, 1);
					m_PreAnimStateHands = "";
				}
			}
			switch (text)
			{
			case "Drinking":
			case "Eating":
			case "Harvesting":
			case "HarvestingStanding":
			case "Igniting":
				m_AnimStateFingers = "No";
				break;
			default:
				switch (text2)
				{
				case "GEAR_Revolver":
				case "GEAR_FlareGun":
					m_AnimStateFingers = "HoldRevolver";
					break;
				case "GEAR_Stone":
				case "GEAR_FlareA":
				case "GEAR_BlueFlare":
				case "GEAR_Torch":
				case "GEAR_NoiseMaker":
					m_AnimStateFingers = "Hold";
					break;
				default:
					if (text2.StartsWith("GEAR_SprayPaint"))
					{
						m_AnimStateFingers = "HoldCan";
					}
					else if (text2 == "GEAR_PackMatches" || text2 == "GEAR_WoodMatches")
					{
						m_AnimStateFingers = "HoldMatch";
					}
					else
					{
						m_AnimStateFingers = "No";
					}
					break;
				}
				break;
			}
			int tagHash3 = m_Animer.GetCurrentAnimatorStateInfo(2).tagHash;
			int num3 = Animator.StringToHash(m_AnimStateFingers);
			if (tagHash3 != num3)
			{
				m_Animer.Play(m_AnimStateFingers, 2);
			}
			if (!m_MyDoll && (text == "Walk" || text == "Run"))
			{
				GameObject gameObject2 = gameObject.transform.GetChild(3).GetChild(8).GetChild(1)
					.GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.gameObject;
				GameObject gameObject3 = gameObject.transform.GetChild(3).GetChild(8).GetChild(2)
					.GetChild(0)
					.GetChild(0)
					.GetChild(0)
					.gameObject;
				float y = gameObject.transform.position.y;
				float y2 = gameObject3.transform.position.y;
				float y3 = gameObject2.transform.position.y;
				float num4 = y - y2;
				float num5 = y - y3;
				double num6 = -0.05;
				double num7 = -0.057;
				if ((double)num4 >= num7 && (double)num4 <= num6 && MyMod.StepState != 1)
				{
					MyMod.StepState = 1;
					MyMod.MaybeLeaveFootPrint(gameObject3.transform.position, gameObject);
					Utils.GetMaterialTagForObjectAtPosition(gameObject, gameObject3.transform.position);
					GameAudioManager.Play3DSound(EVENTS.PLAY_FOOTSTEPSWOLFWALK, gameObject);
				}
				if ((double)num5 >= num7 && (double)num5 <= num6 && MyMod.StepState != 2)
				{
					MyMod.StepState = 2;
					MyMod.MaybeLeaveFootPrint(gameObject2.transform.position, gameObject, bothFeet: false, 0f, leftfoot: true);
					Utils.GetMaterialTagForObjectAtPosition(gameObject, gameObject2.transform.position);
					GameAudioManager.Play3DSound(EVENTS.PLAY_FOOTSTEPSWOLFWALK, gameObject);
				}
			}
			else
			{
				MyMod.StepState = 0;
			}
		}

		public void Pickup()
		{
			GameObject gameObject = m_Animer.gameObject;
			int myId = ClientUser.myId;
			string text = MyMod.MyAnimState;
			int tagHash = m_Animer.GetCurrentAnimatorStateInfo(3).tagHash;
			int num = Animator.StringToHash("Pickup");
			if (!m_MyDoll)
			{
				myId = gameObject.GetComponent<MultiplayerPlayer>().m_ID;
				if (MyMod.playersData[myId] == null || MyMod.playersData[myId].m_Levelid != MyMod.levelid || MyMod.playersData[myId].m_LevelGuid != MyMod.level_guid)
				{
					return;
				}
				text = MyMod.playersData[myId].m_AnimState;
			}
			if (tagHash != num)
			{
				if (text != "Ctrl")
				{
					m_Animer.Play("DoPickup", 3);
				}
				else
				{
					m_Animer.Play("DoSitPickup", 3);
				}
			}
		}

		public void DoLeftHandEmote(string Emote)
		{
			_ = m_Animer.gameObject;
			_ = ClientUser.myId;
			int tagHash = m_Animer.GetCurrentAnimatorStateInfo(3).tagHash;
			Animator.StringToHash("Pickup");
			int num = Animator.StringToHash("No");
			if (tagHash == num)
			{
				m_Animer.Play(Emote, 3);
			}
		}

		public void MeleeAttack()
		{
			GameObject gameObject = m_Animer.gameObject;
			int myId = ClientUser.myId;
			_ = MyMod.MyAnimState;
			if (!m_MyDoll)
			{
				myId = gameObject.GetComponent<MultiplayerPlayer>().m_ID;
				if (MyMod.playersData[myId] == null || MyMod.playersData[myId].m_Levelid != MyMod.levelid || MyMod.playersData[myId].m_LevelGuid != MyMod.level_guid)
				{
					return;
				}
				_ = MyMod.playersData[myId].m_AnimState;
			}
			int tagHash = m_Animer.GetCurrentAnimatorStateInfo(4).tagHash;
			int num = Animator.StringToHash("Melee");
			if (tagHash != num)
			{
				m_Animer.Play("Melee", 4);
			}
		}

		public void Consumption()
		{
			GameObject gameObject = m_Animer.gameObject;
			int myId = ClientUser.myId;
			if (m_MyDoll)
			{
				return;
			}
			myId = gameObject.GetComponent<MultiplayerPlayer>().m_ID;
			if (MyMod.playersData[myId] != null && MyMod.playersData[myId].m_Levelid == MyMod.levelid && !(MyMod.playersData[myId].m_LevelGuid != MyMod.level_guid))
			{
				if (m_IsDrink)
				{
					MyMod.playersData[myId].m_AnimState = "Drinking";
					gameObject.GetComponent<MultiplayerPlayer>().m_HoldingFood = "Water";
				}
				else
				{
					MyMod.playersData[myId].m_AnimState = "Eating";
					gameObject.GetComponent<MultiplayerPlayer>().m_HoldingFood = "Food";
				}
				gameObject.GetComponent<MultiplayerPlayer>().UpdateHeldItem();
			}
		}

		public void StopConsumption()
		{
			GameObject gameObject = m_Animer.gameObject;
			int myId = ClientUser.myId;
			if (!m_MyDoll)
			{
				myId = gameObject.GetComponent<MultiplayerPlayer>().m_ID;
				if (MyMod.playersData[myId] != null && MyMod.playersData[myId].m_Levelid == MyMod.levelid && !(MyMod.playersData[myId].m_LevelGuid != MyMod.level_guid) && gameObject.GetComponent<MultiplayerPlayer>().m_HoldingFood != "")
				{
					gameObject.GetComponent<MultiplayerPlayer>().m_HoldingFood = "";
					gameObject.GetComponent<MultiplayerPlayer>().UpdateHeldItem();
				}
			}
		}
	}

	public class MultiplayerPlayerVoiceChatPlayer : MonoBehaviour
	{
		public AudioSource aSource;

		public AudioSource aSourceBgNoise;

		public System.Collections.Generic.List<DataStr.VoiceChatQueueElement> VoiceQueue = new System.Collections.Generic.List<DataStr.VoiceChatQueueElement>();

		public int m_ID;

		public bool m_RadioFilter;

		public static float PlayNextIn = 0f;

		public static float Trim = 0.03f;

		public bool PlayBgNoise;

		public bool IsLastClip;

		public MultiplayerPlayerVoiceChatPlayer(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if (aSource != null && Time.time > PlayNextIn)
			{
				if (IsLastClip)
				{
					IsLastClip = false;
					PlayBgNoise = false;
				}
				if (VoiceQueue.Count > 0)
				{
					PlayBgNoise = true;
					DataStr.VoiceChatQueueElement voiceChatQueueElement = VoiceQueue[0];
					PlayNextIn = Time.time + voiceChatQueueElement.m_Length;
					MyMod.PlayVoiceFromPlayerObject(aSource, voiceChatQueueElement.m_VoiceData, m_RadioFilter);
					VoiceQueue.RemoveAt(0);
					if (VoiceQueue.Count == 0)
					{
						IsLastClip = true;
					}
				}
			}
			if (aSourceBgNoise != null)
			{
				aSourceBgNoise.enabled = PlayBgNoise;
			}
		}
	}

	public class PlayerBulletDamage : MonoBehaviour
	{
		public int m_Damage;

		public GameObject m_Obj;

		public int m_ClientId;

		public GameObject m_Player;

		public int m_Type;

		public PlayerBulletDamage(IntPtr ptr)
			: base(ptr)
		{
		}

		public void OnCollisionEnter(Collision col)
		{
			if (!(col.gameObject.GetComponent<ArrowItem>() != null))
			{
				return;
			}
			ArrowItem component = col.gameObject.GetComponent<ArrowItem>();
			if (!(col.gameObject.GetComponent<DestoryArrowOnHit>() == null))
			{
				return;
			}
			component.m_ArrowMesh.GetComponent<BoxCollider>().enabled = false;
			MelonLogger.Msg("Arrow colided other player, and dealing damage " + m_Damage);
			if (MyMod.sendMyPosition)
			{
				using Packet packet = new Packet(29);
				packet.Write((float)m_Damage);
				packet.Write(m_Type);
				packet.Write(m_ClientId);
				packet.Write(_value: false);
				MyMod.SendUDPData(packet);
			}
			if (MyMod.iAmHost)
			{
				using (new Packet(29))
				{
					ServerSend.BULLETDAMAGE(m_ClientId, m_Damage, m_Type, 0);
				}
			}
		}

		public void SetLocaZone(GameObject t, MultiplayerPlayer pl, int bodyPart)
		{
			m_Obj = t;
			m_Obj.tag = "Flesh";
			m_Obj.layer = vp_Layer.Container;
			m_Type = bodyPart;
			if (m_Obj.name == "Chest")
			{
				m_Damage = 50;
			}
			else if (m_Obj.name.StartsWith("arm"))
			{
				m_Damage = 30;
			}
			else if (m_Obj.name.StartsWith("Head"))
			{
				m_Damage = 70;
			}
			else if (m_Obj.name.StartsWith("Thigh"))
			{
				m_Damage = 20;
			}
			m_Player = pl.m_Player;
			pl.m_DamageColiders.Add(m_Obj);
			m_ClientId = pl.m_ID;
		}
	}

	public class FakeRockCache : MonoBehaviour
	{
		public string m_GUID = "";

		public string m_Owner = "Unknown";

		public int m_Rocks;

		public int m_Sticks;

		public DataStr.FakeRockCacheVisualData m_VisualData = new DataStr.FakeRockCacheVisualData();

		public FakeRockCache(IntPtr ptr)
			: base(ptr)
		{
		}

		public void Created(bool Sync = true)
		{
			m_VisualData.m_GUID = m_GUID;
			m_VisualData.m_LevelGUID = MyMod.level_guid;
			m_VisualData.m_Owner = m_Owner;
			m_VisualData.m_Position = base.gameObject.transform.position;
			m_VisualData.m_Rotation = base.gameObject.transform.rotation;
			if (MyMod.sendMyPosition)
			{
				if (Sync)
				{
					using (Packet packet = new Packet(170))
					{
						packet.Write(m_VisualData);
						MyMod.SendUDPData(packet);
					}
				}
			}
			else
			{
				MPSaveManager.AddRockCach(m_VisualData, 0);
			}
		}

		public void Open()
		{
			if (base.gameObject != null && (bool)base.gameObject.GetComponent<Container>())
			{
				base.gameObject.GetComponent<Container>().ShowContainerContents();
			}
		}

		public void DismantleFinished()
		{
			if (m_Rocks > 0)
			{
				GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_Stone", m_Rocks);
				string message = Localization.Get("GAMEPLAY_Stone") + " (" + m_Rocks + ")";
				GearMessage.AddMessage("GEAR_Stone", Localization.Get("GAMEPLAY_Harvested"), message, highPriority: false, showOnHUDOnly: false);
			}
			if (m_Sticks > 0)
			{
				GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory("GEAR_Stick", m_Sticks);
				string message2 = Localization.Get("GAMEPLAY_Stick") + " (" + m_Sticks + ")";
				GearMessage.AddMessage("GEAR_Stick", Localization.Get("GAMEPLAY_Harvested"), message2, highPriority: false, showOnHUDOnly: false);
			}
			UnityEngine.Object.Destroy(base.gameObject);
			if (MyMod.sendMyPosition)
			{
				using (Packet packet = new Packet(172))
				{
					packet.Write(m_VisualData);
					MyMod.SendUDPData(packet);
					return;
				}
			}
			MPSaveManager.RemoveRockCach(m_VisualData, 0);
			ServerSend.REMOVEROCKCACH(0, m_VisualData, 1, m_VisualData.m_LevelGUID);
		}

		public void Remove()
		{
			DataStr.BrokenFurnitureSync brokenFurnitureSync = new DataStr.BrokenFurnitureSync();
			brokenFurnitureSync.m_Guid = m_GUID;
			brokenFurnitureSync.m_ParentGuid = "";
			brokenFurnitureSync.m_LevelID = MyMod.levelid;
			brokenFurnitureSync.m_LevelGUID = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
			if (MyMod.sendMyPosition)
			{
				MyMod.DoPleaseWait("Please wait...", "Preparing to dismantle...");
				MyMod.SetRepeatPacket(MyMod.ResendPacketType.Cancel, 10);
				MyMod.PendingRockCahceRemove = this;
				using (Packet packet = new Packet(171))
				{
					packet.Write(m_VisualData);
					MyMod.SendUDPData(packet);
				}
				using Packet packet2 = new Packet(51);
				packet2.Write(brokenFurnitureSync);
				MyMod.SendUDPData(packet2);
				return;
			}
			for (int i = 0; i < MyMod.playersData.Count; i++)
			{
				if (MyMod.playersData[i] != null && ((MyMod.playersData[i].m_BrakingObject != null && MyMod.playersData[i].m_BrakingObject.m_Guid == m_GUID) || (MyMod.playersData[i].m_Container != null && MyMod.playersData[i].m_Container.m_Guid == m_GUID)))
				{
					HUDMessage.AddMessage(MyMod.playersData[i].m_Name + " INTERACTING WITH THIS!");
					return;
				}
			}
			if (MPSaveManager.ContainerNotEmpty(MyMod.level_guid, m_GUID))
			{
				HUDMessage.AddMessage("Rock cache should be empty!");
				return;
			}
			ServerSend.FURNBREAKINGGUID(0, brokenFurnitureSync, toEveryOne: true);
			GameManager.s_IsAISuspended = true;
			Pathes.FakeRockCacheCallback = this;
			GameCompat.Panel<Panel_GenericProgressBar>().Launch(Localization.Get("GAMEPLAY_BreakingDownProgress"), 2f, 10f, 0f, "Play_RockCache", null, supressHeavyBreathing: false, skipRestoreInHands: false, null);
		}
	}

	public class LocalVariablesKit : MonoBehaviour
	{
		public string m_String = "";

		public int m_Int;

		public float m_Float;

		public double m_Double;

		public LocalVariablesKit(IntPtr ptr)
			: base(ptr)
		{
		}
	}

	public class ParticleSystemParasite : MonoBehaviour
	{
		public ParticleSystem m_ParticleSystem;

		public ParticleSystemParasite(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if (base.gameObject != null && m_ParticleSystem != null && !m_ParticleSystem.isPlaying)
			{
				m_ParticleSystem.Play();
			}
		}
	}

	public class StatusBarHelper : MonoBehaviour
	{
		public GameObject m_RootTrack;

		public GameObject m_Root;

		public StatusBarHelper(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
			if (m_RootTrack != null && m_Root != null)
			{
				m_Root.SetActive(m_RootTrack.activeSelf);
			}
			if (!SanityManager.m_CanSeeSanity)
			{
				m_Root.SetActive(value: false);
			}
		}
	}

	public class FakeDecoy : MonoBehaviour
	{
		public DroppedGearDummy m_DropGearDummy;

		public FakeDecoy(IntPtr ptr)
			: base(ptr)
		{
		}

		private void Update()
		{
		}
	}

	public class ExpeditionInteractive : MonoBehaviour
	{
		public DataStr.ExpeditionInteractiveData m_Data = new DataStr.ExpeditionInteractiveData();

		public ExpeditionInteractive(IntPtr ptr)
			: base(ptr)
		{
		}

		public void Load()
		{
			base.gameObject.transform.position = m_Data.m_Position;
			base.gameObject.transform.localEulerAngles = m_Data.m_Rotation;
			base.gameObject.transform.localScale = m_Data.m_Scale;
			base.gameObject.name = "ExpeditionInteractive_" + m_Data.m_ObjectText;
		}

		public bool TryInteract()
		{
			if (CanInteract())
			{
				MyMod.DoLongAction(base.gameObject, m_Data.m_InteractText, "ExpInt", m_Data.m_InteractTime, Hold: true, m_Data.m_Audio);
				MyMod.SendInteractingGUID(m_Data.m_GUID);
				return true;
			}
			return false;
		}

		public bool CanInteract()
		{
			bool flag = false;
			flag = string.IsNullOrEmpty(m_Data.m_Tool) || GameManager.GetInventoryComponent().HasNonRuinedItem(m_Data.m_Tool);
			bool flag2 = false;
			if (string.IsNullOrEmpty(m_Data.m_Material) || m_Data.m_MaterialCount == 0)
			{
				flag2 = true;
			}
			else if (GameManager.GetInventoryComponent().NumGearInInventory(m_Data.m_Material) >= m_Data.m_MaterialCount)
			{
				flag2 = true;
			}
			if (!flag && !flag2)
			{
				if (m_Data.m_MaterialCount > 1)
				{
					HUDMessage.AddMessage("Requires " + GameCompat.GetGearDisplayName(m_Data.m_Tool) + " and " + m_Data.m_MaterialCount + " " + GameCompat.GetGearDisplayName(m_Data.m_Material));
				}
				else
				{
					HUDMessage.AddMessage("Requires " + GameCompat.GetGearDisplayName(m_Data.m_Tool) + " and " + GameCompat.GetGearDisplayName(m_Data.m_Material));
				}
				GameAudioManager.PlayGUIError();
			}
			else if (!flag && flag2)
			{
				HUDMessage.AddMessage("Requires " + GameCompat.GetGearDisplayName(m_Data.m_Tool));
				GameAudioManager.PlayGUIError();
			}
			else if (flag && !flag2)
			{
				HUDMessage.AddMessage("Requires " + m_Data.m_MaterialCount + " " + GameCompat.GetGearDisplayName(m_Data.m_Material));
				GameAudioManager.PlayGUIError();
			}
			return flag && flag2;
		}
	}

	public static void RegisterComponents()
	{
		ClassInjector.RegisterTypeInIl2Cpp<AnimalUpdates>();
		ClassInjector.RegisterTypeInIl2Cpp<DestoryArrowOnHit>();
		ClassInjector.RegisterTypeInIl2Cpp<PlayerBulletDamage>();
		ClassInjector.RegisterTypeInIl2Cpp<ClientProjectile>();
		ClassInjector.RegisterTypeInIl2Cpp<MultiplayerPlayer>();
		ClassInjector.RegisterTypeInIl2Cpp<ContainersSync>();
		ClassInjector.RegisterTypeInIl2Cpp<UiButtonPressHook>();
		ClassInjector.RegisterTypeInIl2Cpp<DestoryStoneOnStop>();
		ClassInjector.RegisterTypeInIl2Cpp<MultiplayerPlayerAnimator>();
		ClassInjector.RegisterTypeInIl2Cpp<MultiplayerPlayerClothingManager>();
		ClassInjector.RegisterTypeInIl2Cpp<DoNotSerializeThis>();
		ClassInjector.RegisterTypeInIl2Cpp<MultiplayerPlayerVoiceChatPlayer>();
		ClassInjector.RegisterTypeInIl2Cpp<DroppedGearDummy>();
		ClassInjector.RegisterTypeInIl2Cpp<IgnoreDropOverride>();
		ClassInjector.RegisterTypeInIl2Cpp<DropFakeOnLeave>();
		ClassInjector.RegisterTypeInIl2Cpp<FakeBed>();
		ClassInjector.RegisterTypeInIl2Cpp<FakeBedDummy>();
		ClassInjector.RegisterTypeInIl2Cpp<AnimalActor>();
		ClassInjector.RegisterTypeInIl2Cpp<AnimalCorpseObject>();
		ClassInjector.RegisterTypeInIl2Cpp<SpawnRegionSimple>();
		ClassInjector.RegisterTypeInIl2Cpp<LobbyHoverNickname>();
		ClassInjector.RegisterTypeInIl2Cpp<CookpotHelmet>();
		ClassInjector.RegisterTypeInIl2Cpp<UiButtonKeyboardPressSkip>();
		ClassInjector.RegisterTypeInIl2Cpp<DeathDropContainer>();
		ClassInjector.RegisterTypeInIl2Cpp<DoorLockedOnKey>();
		ClassInjector.RegisterTypeInIl2Cpp<FakeRockCache>();
		ClassInjector.RegisterTypeInIl2Cpp<LocalVariablesKit>();
		ClassInjector.RegisterTypeInIl2Cpp<ParticleSystemParasite>();
		ClassInjector.RegisterTypeInIl2Cpp<ExpeditionInteractive>();
		ClassInjector.RegisterTypeInIl2Cpp<SpecialItemDummy>();
		ClassInjector.RegisterTypeInIl2Cpp<StatusBarHelper>();
		ClassInjector.RegisterTypeInIl2Cpp<FakeDecoy>();
	}
}
