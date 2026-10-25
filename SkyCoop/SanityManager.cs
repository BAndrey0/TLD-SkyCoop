using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

public class SanityManager
{
	public static bool m_CanSeeSanity = false;

	public static GameObject m_CurrentRefMan = null;

	public static bool m_SeenRefMan = false;

	public static bool m_Insanity = false;

	public static bool m_Nightmares = false;

	public static float m_CurrentSanity = 300f;

	public static float m_PreviousSanity = 300f;

	public static float m_MaxSanity = 300f;

	public static bool m_GoingTriggerNightmare = false;

	public static bool m_KickedOut = false;

	public static bool m_EnterInDoor = false;

	public static float m_DaySanityRegeneration = 0.2f;

	public static float m_NightSanityDecrease = 0.23f;

	public static float m_NightOutDoorsSanityDecrease = 0.4f;

	public static float m_ScarySceneSanityDecrease = 0.23f;

	public static float m_FullDarknessSanityDecrease = 0.15f;

	public static float m_SleepingSanityRegereation = 5f;

	public static float m_DayRegenerationCap = 180f;

	public static string m_TransitionDataBackup = "";

	public static string m_PreNightmareOutDoorScene = "";

	public static string m_PreNightmareOutDoorEnterPoint = "";

	public static string m_PreNightmareInDoorScene = "";

	public static string m_PreNightmareInDoorSceneWithGUID = "";

	public static bool m_Debug = false;

	public static List<string> m_ScaryScenes = new List<string> { "DamTransitionZone", "Dam", "BearCave" };

	public static void Update()
	{
		CheckSeeingRefMan();
	}

	public static void ProcessedSleep(float Hours)
	{
		AddSanity(m_SleepingSanityRegereation * Hours);
	}

	public static void StartSleeping()
	{
		if (IsNight() && m_CurrentSanity < 10f)
		{
			m_GoingTriggerNightmare = true;
		}
	}

	public static void AddSanity(float Value)
	{
		m_CurrentSanity += Value;
		if (m_CurrentSanity < 0f)
		{
			m_CurrentSanity = 0f;
		}
		float num = m_MaxSanity;
		if (m_Insanity)
		{
			num = m_MaxSanity / 2f;
		}
		if (m_CurrentSanity > num)
		{
			m_CurrentSanity = num;
		}
	}

	public static void DayBenefit()
	{
		if (!IsScaryScene() && !IsNight() && m_CurrentSanity < m_DayRegenerationCap)
		{
			AddSanity(m_DaySanityRegeneration);
		}
	}

	public static void NightAffection()
	{
		if (IsNight())
		{
			if (InDoors())
			{
				AddSanity(0f - m_NightSanityDecrease);
			}
			else
			{
				AddSanity(0f - m_NightOutDoorsSanityDecrease);
			}
		}
	}

	public static void ScaryPlaceAffection()
	{
		if (IsScaryScene())
		{
			AddSanity(0f - m_ScarySceneSanityDecrease);
		}
	}

	public static void FullDarknessAffection()
	{
		if (IsFullDarkness())
		{
			AddSanity(0f - m_FullDarknessSanityDecrease);
		}
	}

	public static float GetRate()
	{
		return m_CurrentSanity - m_PreviousSanity;
	}

	public static void EveryInGameMinute()
	{
		m_PreviousSanity = m_CurrentSanity;
		DayBenefit();
		NightAffection();
		ScaryPlaceAffection();
		FullDarknessAffection();
		if (m_Debug)
		{
			MelonLogger.Msg("[SanityManager] Sanity " + m_CurrentSanity);
		}
		if (m_CurrentSanity < 30f)
		{
			MaySpawnRefMan();
		}
		if (m_CurrentSanity < 50f)
		{
			MayHearingHallucination();
		}
		if (!m_Nightmares)
		{
			return;
		}
		if (m_GoingTriggerNightmare)
		{
			if (GameManager.IsOutDoorsScene(GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent))
			{
				if (m_KickedOut)
				{
					m_KickedOut = false;
					if (m_CurrentSanity > 10f)
					{
						m_GoingTriggerNightmare = false;
						TriggerNightmare();
					}
				}
			}
			else if ((bool)GameManager.GetRestComponent() && GameManager.GetRestComponent().m_NumSecondsSleeping > 50f)
			{
				foreach (LoadScene item in UnityEngine.Object.FindObjectsOfType<LoadScene>())
				{
					if (GameManager.IsOutDoorsScene(item.m_SceneToLoad))
					{
						item.m_SoundDuringFadeIn = "";
						m_PreNightmareOutDoorEnterPoint = item.m_ExitPointName;
						m_PreNightmareOutDoorScene = item.m_SceneToLoad;
						m_PreNightmareInDoorScene = MyMod.level_name;
						m_PreNightmareInDoorSceneWithGUID = MyMod.level_guid;
						m_KickedOut = true;
						item.LoadLevelWhenFadeOutComplete();
					}
				}
			}
		}
		else if (m_EnterInDoor && GameManager.IsOutDoorsScene(GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent))
		{
			foreach (LoadScene item2 in UnityEngine.Object.FindObjectsOfType<LoadScene>())
			{
				bool flag = false;
				if (GameManager.IsOutDoorsScene(item2.m_SceneToLoad))
				{
					continue;
				}
				if (string.IsNullOrEmpty(item2.m_GUID))
				{
					if (item2.m_SceneToLoad == m_PreNightmareInDoorSceneWithGUID)
					{
						flag = true;
					}
				}
				else if (item2.m_SceneToLoad + "_" + item2.m_GUID == m_PreNightmareInDoorSceneWithGUID)
				{
					flag = true;
				}
				if (flag)
				{
					item2.m_SoundDuringFadeIn = "";
					m_EnterInDoor = false;
					item2.Activate();
				}
			}
		}
		if (MyMod.level_name == "BearCave" && m_CurrentSanity > 50f)
		{
			FinishNightmare();
		}
	}

	public static bool IsSleeping()
	{
		if ((bool)GameManager.GetRestComponent())
		{
			return GameManager.GetRestComponent().IsSleeping();
		}
		return false;
	}

	public static bool IsNight()
	{
		if (GameManager.m_TimeOfDay != null)
		{
			return GameManager.m_TimeOfDay.IsNight();
		}
		return false;
	}

	public static bool InDoors()
	{
		if ((bool)GameManager.GetWeatherComponent())
		{
			return GameManager.GetWeatherComponent().IsIndoorScene();
		}
		return false;
	}

	public static bool IsFullDarkness()
	{
		if ((bool)GameManager.GetWeatherComponent())
		{
			return GameManager.GetWeatherComponent().IsTooDarkForAction(ActionsToBlock.Reading);
		}
		return false;
	}

	public static bool IsScaryScene()
	{
		foreach (string scaryScene in m_ScaryScenes)
		{
			if (scaryScene == MyMod.level_name)
			{
				return true;
			}
		}
		return false;
	}

	public static void CheckSeeingRefMan()
	{
		if (m_CurrentRefMan != null)
		{
			if (m_CurrentRefMan.GetComponent<MeshRenderer>().isVisible && !m_SeenRefMan)
			{
				m_SeenRefMan = true;
				PlayerDamageEvent.SpawnAfflictionEvent("GAMEPLAY_AfflictionFearAfraid", "GAMEPLAY_Affliction", "ico_injury_eventEntity2", InterfaceManager.m_FirstAidRedColor);
				GameObject soundEmitterFromGameObject = GameAudioManager.GetSoundEmitterFromGameObject(GameManager.GetPlayerObject());
				AkSoundEngine.StopPlayingID(AkSoundEngine.PostEvent("Play_FearAffliction", soundEmitterFromGameObject, 4u, null, null), 40000);
				m_CurrentSanity = 0f;
			}
		}
		else
		{
			m_SeenRefMan = false;
		}
	}

	public static void MaySpawnRefMan(float chanceOverride = 0.005f, bool NoRestrictions = false)
	{
		if (new System.Random().NextDouble() < (double)chanceOverride && (NoRestrictions || (GameManager.m_TimeOfDay != null && GameManager.m_TimeOfDay.IsNight())))
		{
			SpawnRefMan();
		}
	}

	public static void MayHearingHallucination()
	{
		System.Random random = new System.Random();
		List<string> list = new List<string>();
		float num = 0.07f;
		if (MyMod.level_name == "Dam" || MyMod.level_name == "DamTransitionZone")
		{
			list.Add("PLAY_WOLFATTACK");
			list.Add("PLAY_TIMBERWOLFATTACK");
			list.Add("PLAY_TIMBERWOLFATTACKBITE");
			list.Add("PLAY_SNDMECHMETALVAULT2OPEN");
		}
		else
		{
			num = 0.05f;
			list.Add("PLAY_SURVIVORYOUNGMALEATTACKEDWOLF");
			list.Add("PLAY_SURVIVORFEMALEATTACKEDWOLF");
			list.Add("PLAY_DYSENTERY");
			list.Add("PLAY_INFECTION");
			list.Add("PLAY_RABBITKILL");
			list.Add("PLAY_REVOLVERFIRE");
			list.Add("PLAY_RIFLEFIRE");
			list.Add("PLAY_ENTITYATTACKSUCCESS");
			list.Add("PLAY_ENTITYDEMO");
			list.Add("PLAY_FPH_GEAR_BOLTCUTTERS_INTERACTION_CUT_SFX");
			if ((bool)GameManager.GetPlayerManagerComponent() && PlayerManager.m_VoicePersona == VoicePersona.Female)
			{
				list.Add("PLAY_SNDVOSMASTE33370");
				list.Add("PLAY_SNDVOSMASTE33390");
				list.Add("PLAY_SNDVOSMASTE33410");
				list.Add("PLAY_SNDVOSMASTSURVIVORSIGHTINGCLOSE01");
			}
			else
			{
				list.Add("PLAY_SNDVOSMMACE43550");
				list.Add("PLAY_SNDVOSMMAC200");
				list.Add("PLAY_SNDVOSMMAC140");
				list.Add("PLAY_SNDVOSMMACE43550");
				list.Add("PLAY_SNDVOSMMACE410000");
			}
			if (InDoors())
			{
				list.Add("PLAY_STIMPACK");
				list.Add("PLAY_KNOCKDOWNHIT");
				list.Add("PLAY_SNDMECHSAFETUMBLERFALL");
				list.Add("PLAY_BODYFALLLARGE");
				list.Add("PLAY_ENTITYOUTSIDECABIN");
				list.Add("PLAY_ENTITYSHORTATTACK");
				list.Add("PLAY_ENTITYDEMO");
				list.Add("PLAY_OPENINVENTORY");
				list.Add("PLAY_SNDMECHDOORWOODOPEN1");
				list.Add("PLAY_SNDMECHDOORWOODOPEN2");
			}
			else
			{
				list.Add("PLAY_WOLFATTACK");
				list.Add("PLAY_TIMBERWOLFATTACK");
				list.Add("PLAY_TIMBERWOLFATTACKBITE");
				list.Add("PLAY_ENTITYDEATHSFX");
				list.Add("PLAY_RIFLEFIRESPOTLIGHT");
				list.Add("PLAY_SNDMECHDOORMETALCHAINOPEN1");
			}
		}
		if (random.NextDouble() < (double)num)
		{
			GameAudioManager.PlaySound(list[random.Next(list.Count)], InterfaceManager.GetSoundEmitter());
		}
	}

	public static void SpawnRefMan()
	{
		GameObject gameObject = Resources.Load<GameObject>("ref_man_1_85_Prefab");
		if ((bool)gameObject)
		{
			new System.Random();
			Vector3 position = GameManager.GetPlayerTransform().transform.position;
			position -= GameManager.GetPlayerTransform().transform.forward * 2f;
			m_CurrentRefMan = UnityEngine.Object.Instantiate(gameObject, position, GameManager.GetPlayerTransform().transform.rotation);
			UnityEngine.Object.Destroy(m_CurrentRefMan, 5f);
		}
	}

	public static void TriggerNightmare()
	{
		if (m_Nightmares)
		{
			m_TransitionDataBackup = Utils.SerializeObject(GameManager.m_SceneTransitionData);
			GameManager.m_SceneTransitionData.m_SpawnPointName = "DefaultSpawnPoint";
			GameManager.m_SceneTransitionData.m_SpawnPointAudio = "";
			GameManager.m_SceneTransitionData.m_ForceNextSceneLoadTriggerScene = "";
			GameManager.m_SceneTransitionData.m_ForceNextSceneLoadTriggerScene = "";
			GameManager.m_SceneTransitionData.m_SceneLocationLocIDOverride = "";
			GameManager.LoadScene("BearCave", GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent);
		}
	}

	public static void FinishNightmare()
	{
		m_EnterInDoor = true;
		LoadScene loadScene = new GameObject().AddComponent<LoadScene>();
		loadScene.m_SceneToLoad = m_PreNightmareOutDoorScene;
		loadScene.m_ExitPointName = m_PreNightmareOutDoorEnterPoint;
		loadScene.Activate();
	}
}
