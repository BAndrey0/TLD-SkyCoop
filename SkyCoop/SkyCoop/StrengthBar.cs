using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SkyCoop;

// The Strength meter drawn with the game's own HUD pieces: a copy of the sprint ring (same track, fill and glow
// sprites) to the left of it, with the crafting icon from the HUD atlas instead of the running man. Falls back to the
// plain IMGUI bar in CoopFatigue when the HUD layout is not what this expects.
public static class StrengthBar
{
	private static GameObject root;

	private static UISprite fill;

	private static UISprite icon;

	private static int fillWidth;

	private static bool failed;

	private static bool logged;

	private static readonly List<UIWidget> widgets = new List<UIWidget>();

	private static readonly List<float> baseAlpha = new List<float>();

	public static bool Available => root != null && !failed;

	public static bool Ensure()
	{
		if (failed)
		{
			return false;
		}
		if (root != null)
		{
			return true;
		}
		try
		{
			Panel_HUD hud = GameCompat.Panel<Panel_HUD>();
			if (hud == null || hud.m_SprintBar == null || hud.m_Sprite_SprintBar == null)
			{
				return false;
			}
			GameObject source = hud.m_SprintBar.gameObject;
			root = UnityEngine.Object.Instantiate(source, source.transform.parent);
			root.name = "SkyCoopStrengthBar";
			// Placed to the left of the sprint ring at the same height, mirrored around it by one ring width.
			float ring = Mathf.Max(hud.m_Sprite_SprintBar.width, hud.m_SprintBar.width);
			root.transform.localPosition = source.transform.localPosition + new Vector3(0f - (ring + 12f), 0f, 0f);
			fill = Counterpart(source.transform, hud.m_Sprite_SprintBar);
			icon = Counterpart(source.transform, hud.m_Sprite_SprintCenter) ?? Counterpart(source.transform, hud.m_Sprite_SprintBarX);
			Hide(Counterpart(source.transform, hud.m_Sprite_SprintReductionBar));
			Hide(Counterpart(source.transform, hud.m_Sprite_SprintBuffBar));
			UISprite crossCopy = Counterpart(source.transform, hud.m_Sprite_SprintBarX);
			if (crossCopy != null && icon != crossCopy)
			{
				Hide(crossCopy);
			}
			if (fill == null)
			{
				throw new InvalidOperationException("sprint fill sprite not found in the copy");
			}
			fillWidth = fill.width;
			PickIcon();
			widgets.Clear();
			baseAlpha.Clear();
			foreach (UIWidget widget in root.GetComponentsInChildren<UIWidget>(includeInactive: true))
			{
				widgets.Add(widget);
				baseAlpha.Add((widget.alpha > 0.01f) ? widget.alpha : 1f);
			}
			MelonLogger.Msg("[Strength bar] Built from the sprint bar, fill type " + fill.type + ", icon " + ((icon != null) ? icon.spriteName : "none"));
			return true;
		}
		catch (Exception ex)
		{
			failed = true;
			if (root != null)
			{
				UnityEngine.Object.Destroy(root);
			}
			root = null;
			MelonLogger.Warning("[Strength bar] Using the simple bar: " + ex.Message);
			return false;
		}
	}

	private static UISprite Counterpart(Transform sourceRoot, UISprite original)
	{
		if (original == null)
		{
			return null;
		}
		string path = RelativePath(sourceRoot, original.transform);
		if (path == null)
		{
			return null;
		}
		Transform copy = (path.Length == 0) ? root.transform : root.transform.Find(path);
		return (copy != null) ? copy.GetComponent<UISprite>() : null;
	}

	private static string RelativePath(Transform rootTransform, Transform target)
	{
		List<string> parts = new List<string>();
		Transform current = target;
		while (current != null && current != rootTransform)
		{
			parts.Insert(0, current.name);
			current = current.parent;
		}
		if (current != rootTransform)
		{
			return null;
		}
		return string.Join("/", parts);
	}

	private static void Hide(UISprite sprite)
	{
		if (sprite != null)
		{
			sprite.gameObject.SetActive(value: false);
		}
	}

	// An icon that reads as "work": the HUD atlas has no Strength icon, so the closest existing one is used.
	private static void PickIcon()
	{
		if (icon == null || icon.atlas == null || icon.atlas.spriteList == null)
		{
			return;
		}
		List<string> names = new List<string>();
		for (int i = 0; i < icon.atlas.spriteList.Count; i++)
		{
			UISpriteData data = icon.atlas.spriteList[i];
			if (data != null && !string.IsNullOrEmpty(data.name))
			{
				names.Add(data.name);
			}
		}
		if (!logged)
		{
			logged = true;
			MelonLogger.Msg("[Strength bar] HUD atlas sprites: " + string.Join(", ", names));
		}
		string[] wanted = new string[8] { "crafting", "craft", "hammer", "hatchet", "axe", "tool", "fatigue", "strength" };
		foreach (string want in wanted)
		{
			foreach (string name in names)
			{
				if (name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					icon.spriteName = name;
					return;
				}
			}
		}
	}

	public static void Update(float value01, float alpha)
	{
		if (!Ensure())
		{
			return;
		}
		try
		{
			root.SetActive(alpha > 0.01f);
			if (alpha <= 0.01f)
			{
				return;
			}
			if (fill.type == UIBasicSprite.Type.Filled)
			{
				fill.fillAmount = value01;
			}
			else
			{
				fill.width = Mathf.Max(2, Mathf.RoundToInt(fillWidth * value01));
			}
			for (int i = 0; i < widgets.Count; i++)
			{
				if (widgets[i] != null)
				{
					widgets[i].alpha = baseAlpha[i] * alpha;
				}
			}
		}
		catch (Exception ex)
		{
			failed = true;
			MelonLogger.Warning("[Strength bar] Disabled: " + ex.Message);
			if (root != null)
			{
				UnityEngine.Object.Destroy(root);
			}
			root = null;
		}
	}

	public static void HideNow()
	{
		if (root != null)
		{
			root.SetActive(value: false);
		}
	}
}
