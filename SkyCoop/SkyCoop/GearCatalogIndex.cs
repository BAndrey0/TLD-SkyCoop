using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
namespace SkyCoop;
public static class GearCatalogIndex
{
 public static bool InWorld=>WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_guid)&&WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_name)&&!MyMod.LoadingScreenIsOn;
 public static void Build(out List<string> all,out Dictionary<string,List<string>> sections)
 {
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var components=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  void Add(string key){string gear=GearCatalogPolicy.GearName(key);if(gear!=null)names.Add(gear);}
  if(InWorld)try
  {
   // The native autocomplete initializer loads GameManager dependencies. Do
   // not invoke it in the title screen merely to browse metadata.
   if(ConsoleManager.m_SearchStringToGearNames==null||ConsoleManager.m_SearchStringToGearNames.Count==0)ConsoleManager.StoreAutoCompleteGearNames();
   if(ConsoleManager.m_SearchStringToGearNames!=null)foreach(var pair in ConsoleManager.m_SearchStringToGearNames)Add(pair.Value);
   if(ConsoleManager.m_ComponentNameToGearNames!=null)foreach(var group in ConsoleManager.m_ComponentNameToGearNames)
    foreach(string name in group.Value){Add(name);if(!components.TryGetValue(name,out var types))components[name]=types=new HashSet<string>();types.Add(group.Key);}
  }catch(Exception ex){MelonLogger.Warning("[Catalog] Autocomplete: "+ex.Message);}
  try
  {
   var locators=Addressables.ResourceLocators.GetEnumerator();var move=locators.Cast<Il2CppSystem.Collections.IEnumerator>();
   while(move.MoveNext())
   {
    var keys=locators.Current.Keys.GetEnumerator();var next=keys.Cast<Il2CppSystem.Collections.IEnumerator>();
    while(next.MoveNext())Add(keys.Current?.ToString());
   }
  }
  catch(Exception ex){MelonLogger.Warning("[Catalog] Runtime addresses: "+ex.Message);}
  var catalogs=new List<string>();string main=Path.Combine(Application.dataPath,"StreamingAssets","aa","catalog.json");if(File.Exists(main))catalogs.Add(main);
  string dlc=Path.Combine(Environment.CurrentDirectory,"tld_dlc");if(Directory.Exists(dlc))catalogs.AddRange(Directory.GetFiles(dlc,"catalog.json",SearchOption.AllDirectories));
  foreach(string file in catalogs)try{foreach(string gear in GearCatalogPolicy.ReadCatalog(file))Add(gear);}catch(Exception ex){MelonLogger.Warning("[Catalog] "+Path.GetFileName(file)+": "+ex.Message);}
  all=names.OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToList();sections=new Dictionary<string,List<string>>();foreach(string section in GearCatalogPolicy.Sections)sections[section]=new List<string>();
  foreach(string gear in all){components.TryGetValue(gear,out var types);sections[GearCatalogPolicy.Section(gear,types)].Add(gear);}
  MelonLogger.Msg("[Catalog] "+all.Count+" names, "+sections.Count+" sections; metadata only");
 }
}
