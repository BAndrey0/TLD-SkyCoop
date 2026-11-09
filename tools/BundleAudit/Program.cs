using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

var path = args.Length > 0 ? args[0] : "C:/TLD-Coop/player/game/Mods/multiplayerstuff.unity3d";
var am = new AssetsManager();
var bunInst = am.LoadBundleFile(path);
var inst = am.LoadAssetsFileFromBundle(bunInst, 0);

var gameObjects = new Dictionary<long, AssetFileInfo>();
var transforms = new Dictionary<long, AssetFileInfo>();
var renderers = new Dictionary<long, AssetFileInfo>();
foreach (var info in inst.file.AssetInfos)
{
    if (info.TypeId == (int)AssetClassID.GameObject) gameObjects[info.PathId] = info;
    if (info.TypeId == (int)AssetClassID.Transform) transforms[info.PathId] = info;
    if (info.TypeId == (int)AssetClassID.SkinnedMeshRenderer) renderers[info.PathId] = info;
}

AssetTypeValueField GetBase(long pathId) => am.GetBaseField(inst, inst.file.GetAssetInfo(pathId), AssetReadFlags.None);

AssetFileInfo FindGameObject(string name)
{
    foreach (var kv in gameObjects)
    {
        var baseField = GetBase(kv.Key);
        if (baseField["m_Name"].AsString == name) return kv.Value;
    }
    return null;
}

long GetTransformId(AssetFileInfo go)
{
    var baseField = GetBase(go.PathId);
    var arr = baseField["m_Component"]["Array"];
    foreach (var data in arr.Children)
    {
        var component = data["component"];
        if (component["m_FileID"].AsInt == 0 && transforms.ContainsKey(component["m_PathID"].AsLong))
            return component["m_PathID"].AsLong;
    }
    return 0;
}

AssetFileInfo GetGameObjectFromTransform(long transformId)
{
    var tr = GetBase(transformId);
    var goId = tr["m_GameObject"]["m_PathID"].AsLong;
    return inst.file.GetAssetInfo(goId);
}

string GetName(AssetFileInfo go) => GetBase(go.PathId)["m_Name"].AsString;

bool HasSkinnedMeshRenderer(AssetFileInfo go)
{
    var baseField = GetBase(go.PathId);
    var arr = baseField["m_Component"]["Array"];
    foreach (var data in arr.Children)
    {
        var component = data["component"];
        if (component["m_FileID"].AsInt == 0 && renderers.ContainsKey(component["m_PathID"].AsLong))
            return true;
    }
    return false;
}

void Dump(long transformId, int depth)
{
    var go = GetGameObjectFromTransform(transformId);
    var name = GetName(go);
    var indent = new string(' ', depth * 2);
    var smr = HasSkinnedMeshRenderer(go) ? " [SMR]" : "";
    Console.WriteLine(indent + name + smr);
    var tr = GetBase(transformId);
    var children = tr["m_Children"]["Array"];
    foreach (var child in children.Children)
    {
        if (child["m_FileID"].AsInt == 0) Dump(child["m_PathID"].AsLong, depth + 1);
    }
}

var player = FindGameObject("multiplayerPlayer");
if (player == null)
{
    Console.WriteLine("multiplayerPlayer not found");
    return;
}
Console.WriteLine("multiplayerPlayer pathId=" + player.PathId);
var trId = GetTransformId(player);
Dump(trId, 0);

am.UnloadAll(true);
