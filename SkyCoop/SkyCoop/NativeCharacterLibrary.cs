using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;


namespace SkyCoop;

// Load the owner's own story meshes. No game meshes/textures are shipped in the
// mod. Keep this locator private: story addresses must not replace Survival assets.
public static class NativeCharacterLibrary
{
	public static readonly string[] Names = { "Маккензи", "Астрид", "Джеремайя", "Молли" };
	private static readonly string[] Assets = {
		"Assets/ArtAssets/Characters/Mackenzie_Ep5/Mackenzie_Ep5.fbx",
		"Assets/ArtAssets/Characters/Astrid_Ep5/AstridOutdoor_Ep5.fbx",
		"Assets/ArtAssets/Characters/Jeremiah_Ep5/Jeremiah_Ep5.fbx",
		"Assets/ArtAssets/Characters/Molly_Ep5/Molly_Ep5.fbx" };
	private static IResourceLocator locator;
	private static readonly HashSet<int> Failed = new();
	private static readonly Dictionary<int,GameObject> Cache = new();
	private static readonly Dictionary<string, AssetBundle> IsolatedBundles = new(StringComparer.OrdinalIgnoreCase);
	public static string Status { get; private set; } = "Не загружены";
	public static GameObject Load(int choice)
	{
		if (choice < 0 || choice >= Assets.Length || Failed.Contains(choice)) return null;
		if (Cache.TryGetValue(choice,out var cached) && cached != null) return cached;
		try
		{
			if (!SteamApps.BIsDlcInstalled(new AppId_t(2095000))) { Status="Нужен установленный WINTERMUTE"; return null; }
			if (locator == null)
			{
				string root = Path.Combine(Application.dataPath,"..","tld_dlc","episode5","tld_Data","StreamingAssets","aa_ep5");
				string original = Path.Combine(root,"catalog.json");
				if (!File.Exists(original)) { Status="Ресурсы Episode 5 не найдены в копии игры"; return null; }
				string directory = Path.Combine(Environment.CurrentDirectory,"UserData","NativeCharacters");
				Directory.CreateDirectory(directory);
				string catalog = Path.Combine(directory,"catalog.json");
				File.WriteAllText(catalog, File.ReadAllText(original).Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}",Path.GetFullPath(root).Replace("\\","\\\\")));
				var operation = Addressables.LoadContentCatalogAsync(catalog,false,"SkyCoopCharacters");
				locator = operation.WaitForCompletion();
				if (locator == null) throw new Exception("Story catalog failed to load");
				Addressables.RemoveResourceLocator(locator);
			}
			if (!locator.Locate(new Il2CppSystem.String(IL2CPP.ManagedStringToIl2Cpp(Assets[choice])),Il2CppType.Of<GameObject>(),out var locations) || locations == null)
				throw new Exception("Story mesh location not found: " + Assets[choice]);
			string rootBundle = Addressables.ResolveInternalId(locations[0].Dependencies[0].InternalId);
			foreach (string path in NativeAssetIsolation.Plan(rootBundle, Assets[choice]))
			{
				if (IsolatedBundles.ContainsKey(path)) continue;
				var bytes = NativeAssetIsolation.ReadIsolated(path, path == rootBundle ? Assets[choice] : null);
				string temporary = Path.Combine(Environment.CurrentDirectory, "UserData", "NativeCharacters", Path.GetFileName(path) + ".isolated");
				File.WriteAllBytes(temporary, bytes);
				var bundle = AssetBundle.LoadFromFile(temporary);
				if (bundle == null) throw new InvalidDataException("Cannot load isolated story bundle: " + Path.GetFileName(path));
				IsolatedBundles.Add(path, bundle);
			}
			GameObject prefab = IsolatedBundles[rootBundle].LoadAsset<GameObject>("skycoop/character");
			if (prefab == null)
			{
				foreach(var candidate in IsolatedBundles[rootBundle].LoadAssetWithSubAssets<GameObject>("skycoop/character"))
				{
					if(candidate==null)continue;
					MelonLogger.Msg("[Native object] " + candidate.name);
					if(candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0){prefab=candidate;break;}
				}
			}
			if (prefab == null) throw new Exception("Story mesh is unavailable");
			prefab.hideFlags |= HideFlags.DontUnloadUnusedAsset; ReleaseBundles(); Cache[choice] = prefab; Status="Доступны игровые модели";
			MelonLogger.Msg("[Native character] Loaded " + Names[choice]);
			return prefab;
		}
		catch (Exception ex) { ReleaseBundles(); Failed.Add(choice); Status=ex.Message; MelonLogger.Warning("[Native character] " + ex); return null; }
	}
	private static void ReleaseBundles()
	{
		foreach (var pair in IsolatedBundles)
		{
			try { if(pair.Value != null) pair.Value.Unload(false); } catch(Exception ex) { MelonLogger.Warning("[Native cleanup] " + ex.Message); }
			string temporary=Path.Combine(Environment.CurrentDirectory,"UserData","NativeCharacters",Path.GetFileName(pair.Key)+".isolated");
			try { File.Delete(temporary); } catch(IOException) { }
		}
		IsolatedBundles.Clear();
	}
}
