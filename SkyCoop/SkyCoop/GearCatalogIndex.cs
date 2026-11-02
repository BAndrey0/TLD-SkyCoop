using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MelonLoader;
using UnityEngine;

namespace SkyCoop;
public static class GearCatalogIndex
{
 public static bool InWorld=>WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_guid)&&WildlifeSpawnAuthorityPolicy.WorldScene(MyMod.level_name)&&!MyMod.LoadingScreenIsOn;
 public static void Build(out List<string> all,out Dictionary<string,List<string>> sections)
 {
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var components=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
  void Add(string key){string gear=GearCatalogPolicy.GearName(key);if(gear!=null)names.Add(gear);}
  var catalogs=new List<string>();string main=Path.Combine(Application.dataPath,"StreamingAssets","aa","catalog.json");if(File.Exists(main))catalogs.Add(main);
  string dlc=Path.Combine(Environment.CurrentDirectory,"tld_dlc");if(Directory.Exists(dlc))catalogs.AddRange(Directory.GetFiles(dlc,"catalog.json",SearchOption.AllDirectories));
  foreach(string file in catalogs)try{foreach(string gear in GearCatalogPolicy.ReadCatalog(file))Add(gear);}catch(Exception ex){MelonLogger.Warning("[Catalog] "+Path.GetFileName(file)+": "+ex.Message);}
  all=names.OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToList();sections=new Dictionary<string,List<string>>();foreach(string section in GearCatalogPolicy.Sections)sections[section]=new List<string>();
  foreach(string gear in all){components.TryGetValue(gear,out var types);sections[GearCatalogPolicy.Section(gear,types)].Add(gear);}
  MelonLogger.Msg("[Catalog] "+all.Count+" names, "+sections.Count+" sections; metadata only");
 }
}
