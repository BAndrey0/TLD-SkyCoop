using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Il2CppInterop.Runtime;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace SkyCoop;

// Prepared locally once from the owner's story resources. All four costumes
// share one bundle, instead of storing four copies of the same textures.
public static class NativeCharacterLibrary
{
 public static readonly string[] Names = { "Маккензи", "Астрид", "Джеремайя", "Молли" };
 private static readonly string[] Assets = {
  "Assets/ArtAssets/Characters/Mackenzie_Ep5/Mackenzie_Ep5.fbx",
  "Assets/ArtAssets/Characters/Astrid_Ep5/AstridOutdoor_Ep5.fbx",
  "Assets/ArtAssets/Characters/Jeremiah_Ep5/Jeremiah_Ep5.fbx",
  "Assets/ArtAssets/Characters/Molly_Ep5/Molly_Ep5.fbx" };
 private static readonly Dictionary<int,GameObject> Cache = new();
 private static readonly List<AssetBundle> Bundles = new();
 private static AssetBundle characters;
 public static string Folder => Path.Combine(Environment.CurrentDirectory,"UserData","CharacterSkins");
 public static string Status { get; private set; } = "Костюмы будут подготовлены при первом выборе";
 private static string Digest(byte[] bytes) { using var sha=SHA256.Create();return Convert.ToHexString(sha.ComputeHash(bytes)); }
 private static string DigestFile(string path) { using var sha=SHA256.Create();using var stream=File.OpenRead(path);return Convert.ToHexString(sha.ComputeHash(stream)); }
 public static GameObject Load(int choice)
 {
  if(choice<0||choice>=Assets.Length)return null;
  if(Cache.TryGetValue(choice,out var old)&&old!=null)return old;
  try
  {
   if(!SteamApps.BIsDlcInstalled(new AppId_t(2095000))){Status="Для подготовки костюмов нужен установленный WINTERMUTE";return null;}
   if(characters==null)
   {
    var files=ReadIndex();
    if(files==null){Prepare();files=ReadIndex();}
    if(files==null)throw new InvalidDataException("Prepared skin index is invalid");
    // Resolve all external resource bundles before loading any prefab.
    foreach(string file in files){var bundle=AssetBundle.LoadFromFile(Path.Combine(Folder,file));if(bundle==null)throw new InvalidDataException("Prepared bundle failed: "+file);Bundles.Add(bundle);}
    characters=Bundles[0];
   }
   var prefab=characters.LoadAsset<GameObject>("skycoop/character/"+choice);
   if(prefab==null)throw new InvalidDataException("Prepared costume is missing: "+Names[choice]);
   prefab.hideFlags|=HideFlags.DontUnloadUnusedAsset;Cache[choice]=prefab;
   Status="Костюмы: UserData/CharacterSkins";
   MelonLogger.Msg("[Native costume] Loaded "+Names[choice]+" prefab="+prefab.name);
   return prefab;
  }
  catch(Exception ex)
  {
   if(characters==null){foreach(var bundle in Bundles)if(bundle!=null)bundle.Unload(false);Bundles.Clear();}
   Status=ex.Message;MelonLogger.Warning("[Native costume] "+ex);return null;
  }
 }
 private static List<string> ReadIndex()
 {
  string index=Path.Combine(Folder,"skins.index");if(!File.Exists(index))return null;
  try
  {
   string[] lines=File.ReadAllLines(index);
   if(lines.Length<3||lines[0]!="SkyCoopCostumes1"||lines[1]!=DigestFile(Path.Combine(Environment.CurrentDirectory,"GameAssembly.dll")))return null;
   var files=new List<string>();
   for(int i=2;i<lines.Length;i++)
   {
    string[] parts=lines[i].Split('|');
    if(parts.Length!=5||Path.GetFileName(parts[0])!=parts[0])return null;
    string cached=Path.Combine(Folder,parts[0]);
    if(!File.Exists(cached)||DigestFile(cached)!=parts[1])return null;
    if(File.Exists(parts[2])){var source=new FileInfo(parts[2]);if(source.Length.ToString()!=parts[3]||source.LastWriteTimeUtc.Ticks.ToString()!=parts[4])return null;}
    files.Add(parts[0]);
   }
   return files;
  }
  catch(IOException){return null;}
 }
 private static void Prepare()
 {
  Status="Подготовка полных костюмов…";
  string root=Path.GetFullPath(Path.Combine(Application.dataPath,"..","tld_dlc","episode5","tld_Data","StreamingAssets","aa_ep5"));
  string original=Path.Combine(root,"catalog.json");
  if(!File.Exists(original))throw new FileNotFoundException("Episode 5 resources are missing in the co-op copy");
  Directory.CreateDirectory(Folder);
  string catalog=Path.Combine(Folder,"source.catalog.json");
  File.WriteAllText(catalog,File.ReadAllText(original).Replace("{UnityEngine.AddressableAssets.Addressables.RuntimePath}",root.Replace("\\","\\\\")));
  IResourceLocator locator=Addressables.LoadContentCatalogAsync(catalog,false,"SkyCoopCostumes").WaitForCompletion();
  if(locator==null)throw new InvalidDataException("Story catalog unavailable");
  Addressables.RemoveResourceLocator(locator);
  if(!locator.Locate(new Il2CppSystem.String(IL2CPP.ManagedStringToIl2Cpp(Assets[0])),Il2CppType.Of<GameObject>(),out var locations))throw new InvalidDataException("Story costume location unavailable");
  string rootBundle=Addressables.ResolveInternalId(locations[0].Dependencies[0].InternalId);
  var selected=new List<string>{rootBundle};
  foreach(string asset in Assets)foreach(string path in NativeAssetIsolation.Plan(rootBundle,asset))if(!selected.Contains(path))selected.Add(path);
  var index=new List<string>{"SkyCoopCostumes1",DigestFile(Path.Combine(Environment.CurrentDirectory,"GameAssembly.dll"))};
  for(int i=0;i<selected.Count;i++)
  {
   string path=selected[i];byte[] bytes=i==0?NativeAssetIsolation.ReadCharacterSet(path,Assets):NativeAssetIsolation.ReadIsolated(path);
   var drive=new DriveInfo(Path.GetPathRoot(Folder));if(drive.AvailableFreeSpace<bytes.LongLength+64L*1024*1024)throw new IOException("Not enough free space to prepare character costumes");
   string name=i+"-"+Path.GetFileName(path);string destination=Path.Combine(Folder,name);
   File.WriteAllBytes(destination+".partial",bytes);File.Move(destination+".partial",destination,true);
   var source=new FileInfo(path);
   index.Add(name+"|"+Digest(bytes)+"|"+path+"|"+source.Length+"|"+source.LastWriteTimeUtc.Ticks);
  }
  File.WriteAllLines(Path.Combine(Folder,"skins.index.partial"),index);
  File.Move(Path.Combine(Folder,"skins.index.partial"),Path.Combine(Folder,"skins.index"),true);
  MelonLogger.Msg("[Native costume] Prepared "+selected.Count+" shared resource bundles in "+Folder);
 }
}
