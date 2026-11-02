using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text;
namespace SkyCoop;

// Metadata only: inventory browsing must not instantiate/load every prefab.
public static class GearCatalogPolicy
{
 public static readonly string[] Sections={"FoodItem","LiquidItem","ClothingItem","GunItem","AmmoItem","ToolsItem","FirstAidItem","FuelSourceItem","Materials","Camping","Fishing","ResearchItem","Decorations","Quest","Other"};
 public static string GearName(string key)
 {
  if(string.IsNullOrWhiteSpace(key))return null;
  string name=key.Replace('\\','/');name=name.Substring(name.LastIndexOf('/')+1);
  if(name.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-7);
  else if(name.IndexOf('.')>=0)return null;
  return name.StartsWith("GEAR_",StringComparison.OrdinalIgnoreCase)&&name.Length>5?name:null;
 }
 public static IEnumerable<string> ReadCatalog(string file)
 {
  using var stream=File.OpenRead(file);using var doc=JsonDocument.Parse(stream);
  var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  if(doc.RootElement.TryGetProperty("m_InternalIds",out var ids))foreach(var id in ids.EnumerateArray())
  {if(id.ValueKind==JsonValueKind.String){string name=GearName(id.GetString());if(name!=null)result.Add(name);}}
  // Address aliases also live in the serialized key table. Read length-prefixed
  // ASCII records directly, without entering IL2CPP generic enumerators.
  if(doc.RootElement.TryGetProperty("m_KeyDataString",out var keys))
   foreach(string name in ReadKeyNames(Convert.FromBase64String(keys.GetString())))result.Add(name);
  return result;
 }
 public static IEnumerable<string> ReadKeyNames(byte[] data)
 {
  var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  for(int i=4;i+5<data.Length;i++)
  {
   if(data[i]!=0)continue; // ASCII string tag
   int length=BitConverter.ToInt32(data,i+1);
   if(length<6||length>2048||length>data.Length-i-5)continue;
   int start=i+5;bool ascii=true;
   for(int j=start;j<start+length;j++)if(data[j]<32||data[j]>126){ascii=false;break;}
   if(!ascii)continue;
   string name=GearName(Encoding.ASCII.GetString(data,start,length));if(name!=null)result.Add(name);
   i=start+length-1;
  }
  return result;
 }
 public static string Section(string gear,ISet<string> types=null)
 {
  string name=gear.ToLowerInvariant();bool Has(params string[] keys){foreach(string key in keys)if(name.Contains(key))return true;return false;}
  bool Type(params string[] keys){if(types==null)return false;foreach(string key in keys)if(types.Contains(key))return true;return false;}
  if(Type("NarrativeCollectibleItem","KeyItem"))return "Quest";
  if(Has("kerosene","lanternfuel","accelerant","gasoline"))return "FuelSourceItem";
  if(Type("DecorationItem","DecorationItemComponent")||Has("decoration","decor_","furniture","shelf","chair","curtain","rug_","pictureframe","cushion","pillow","tablecloth"))return "Decorations";
  if(Type("ClothingItem")||Has("boots","shoes","socks","trousers","pants","parka","jacket","coat","mittens","gloves","balaclava","toque","scarf","woolhat","sweater","longunderwear","vest","tshirt","cap_"))return "ClothingItem";
  if(Type("AmmoItem","ArrowItem","GunpowderItem")||Has("cartridge","casing","ammunition","ammo","bullet","gunpowder","arrow","flareshell"))return "AmmoItem";
  if(Type("GunItem","BowItem")||Has("rifle","revolver","flaregun","bow_","woodbow","noisemaker"))return "GunItem";
  if(Type("FirstAidItem","MedicalTreatmentItem")||Has("bandage","antiseptic","antibiotic","painkiller","emergencystim","dressing","medicine","antitoxin","insulin"))return "FirstAidItem";
  if(Type("WaterSupply","LiquidItem")||Has("water","soda","coffee","tea","juice","broth","flask"))return "LiquidItem";
  if(Type("FoodItem","CookingIngredientItem")||Has("meat","fishcooked","fishraw","venison","rabbitcooked","rabbitraw","pie","stew","porridge","flour","salt","sugar","potato","carrot","peach","beans","chocolate","jerky","ration","cattailstalk","peanut","sardine","soup","pancake","oats","cracker","granola"))return "FoodItem";
  if(Type("ResearchItem")||Has("book","manual"))return "ResearchItem";
  if(Type("FuelSourceItem","TorchItem","FlareItem","MatchesItem")||Has("firewood","softwood","hardwood","reclaimedwood","stick","coal","tinder","matches","flare","torch","lanternfuel","kerosene","accelerant"))return "FuelSourceItem";
  if(Type("SnareItem","FishingTackleItem")||Has("fishing","snare","lure","tipup","bait"))return "Fishing";
  if(Type("Bed","SnowShelterItem")||Has("bedroll","sleepingbag","shelter","travois","bearskinbed"))return "Camping";
  if(Type("ToolsItem","ToolItem","KeroseneLampItem","CookingPotItem","CookingSkilletItem")||Has("knife","hatchet","hacksaw","hammer","prybar","crowbar","sewingkit","whetstone","tool","lantern","lamp","flashlight","firestriker","magnifying","cookpot","cookingpot","skillet","canopener","battery","wire","transmitter","repairkit"))return "ToolsItem";
  if(Has("hide","pelt","gut","leather","cloth","scrap","sapling","maple","birch","feather","moosehide","bearskin","deerskin","wolfskin","ptarmigandown","lead","reishi","rosehip","resin"))return "Materials";
  if(Type("NarrativeCollectibleItem","KeyItem")||Has("key","quest","mission","note","letter","postcard","journal","document","polaroid","map_","cachecode","lockbox"))return "Quest";
  return "Other";
 }
}
