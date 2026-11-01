using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace SkyCoop;

// Story and Survival can use the same CAB identifier for different bundle
// contents. Isolate only the selected prefab's object graph, in memory.
public static class NativeAssetIsolation
{
	public static List<string> Plan(string rootBundle,string assetPath)
	{
		var cabFiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		foreach(string path in Directory.GetFiles(Path.GetDirectoryName(rootBundle),"*.bundle"))
		{
			var bundle=new AssetBundleFile();
			try{bundle.Read(new AssetsFileReader(path));foreach(string name in bundle.GetAllFileNames())if(!name.EndsWith(".resS",StringComparison.OrdinalIgnoreCase)&&!name.EndsWith(".resource",StringComparison.OrdinalIgnoreCase))cabFiles[name]=path;}
			finally{bundle.Close();}
		}
		var manager=new AssetsManager();var opened=new Dictionary<string,AssetsFileInstance>(StringComparer.OrdinalIgnoreCase);
		var selected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var seen=new HashSet<string>();
		try
		{
			AssetsFileInstance Open(string path)
			{
				selected.Add(path);if(opened.TryGetValue(path,out var old))return old;
				var file=manager.LoadAssetsFileFromBundle(manager.LoadBundleFile(path,true),0,false);opened[path]=file;return file;
			}
			void Follow(AssetsFileInstance file,AssetTypeValueField field)
			{
				if(field.TemplateField.Type.StartsWith("PPtr<",StringComparison.Ordinal))
				{
					long id=field["m_PathID"].AsLong;if(id==0)return;int external=field["m_FileID"].AsInt;
					string path=file.path;
					if(external>0)
					{
						string cab=Regex.Match(file.file.Metadata.Externals[external-1].PathName,"CAB-[a-fA-F0-9]{32}").Value;
						if(!cabFiles.TryGetValue(cab,out path))throw new InvalidDataException("Missing native bundle "+cab);
						selected.Add(path);
						string kind=field.TemplateField.Type;
						if(kind.Contains("Texture")||kind.Contains("Shader")||kind.Contains("Mesh")||kind.Contains("Avatar"))return;
						file=Open(path);
					}
					Walk(file,id);return;
				}
				foreach(var child in field.Children)Follow(file,child);
			}
			void Walk(AssetsFileInstance file,long id)
			{
				if(!seen.Add(file.path+":"+id))return;var info=file.file.GetAssetInfo(id);if(info==null)return;
				if(info.TypeId!=1&&info.TypeId!=4&&info.TypeId!=21&&info.TypeId!=23&&info.TypeId!=33&&info.TypeId!=95&&info.TypeId!=137)return;
				Follow(file,manager.GetBaseField(file,info));
			}
			var root=Open(rootBundle);AssetFileInfo container=null;
			foreach(var info in root.file.AssetInfos)if(info.TypeId==142){container=info;break;}
			if(container==null)throw new InvalidDataException("Native bundle has no asset container");
			long rootId=0;
			foreach(var pair in manager.GetBaseField(root,container)["m_Container"]["Array"].Children)
			{
				if(!string.Equals(pair.Children[0].AsString,assetPath,StringComparison.OrdinalIgnoreCase))continue;
				long id=pair.Children[1]["asset"]["m_PathID"].AsLong;
				if(root.file.GetAssetInfo(id)?.TypeId==1){rootId=id;break;}
			}
			if(rootId==0)throw new InvalidDataException("Native character root not found");
			Walk(root,rootId);return new List<string>(selected);
		}
		finally{manager.UnloadAll();}
	}
	public static byte[] ReadIsolated(string path, string assetPath = null)
	{
		if (assetPath != null)
		{
			var manager = new AssetsManager();
			try
			{
				var source = manager.LoadBundleFile(path, true);
				var file = manager.LoadAssetsFileFromBundle(source, 0, false);
				foreach (var meshInfo in file.file.AssetInfos)
					if (meshInfo.TypeId == 43)
					{
						var meshData = manager.GetBaseField(file, meshInfo);
						meshData["m_IsReadable"].AsBool = true;
						meshInfo.SetNewData(meshData);
					}
				foreach (var info in file.file.AssetInfos)
				{
					if (info.TypeId != 142) continue;
					var data = manager.GetBaseField(file, info);
					var entries = data["m_Container"]["Array"];
					AssetTypeValueField selected = null;
					foreach (var pair in entries.Children)
						if (string.Equals(pair.Children[0].AsString, assetPath, StringComparison.OrdinalIgnoreCase)
							&& file.file.GetAssetInfo(pair.Children[1]["asset"]["m_PathID"].AsLong)?.TypeId == 1)
						{ selected = pair; break; }
					if (selected == null) throw new InvalidDataException("Missing native prefab container");
					selected.Children[0].AsString = "skycoop/character";
					entries.Children = new List<AssetTypeValueField> { selected };
					info.SetNewData(data);
					break;
				}
				source.file.BlockAndDirInfo.DirectoryInfos[0].SetNewData(file.file);
				using var stream = new MemoryStream();
				using (var writer = new AssetsFileWriter(stream)) source.file.Write(writer);
				return RenameCabIdentifiers(stream.ToArray());
			}
			finally { manager.UnloadAll(); }
		}
		var bundle=new AssetBundleFile();var output=new MemoryStream();
		try{bundle.Read(new AssetsFileReader(path));using(var writer=new AssetsFileWriter(output)){bundle.Unpack(writer);writer.Flush();}return RenameCabIdentifiers(output.ToArray());}
		finally{bundle.Close();output.Dispose();}
	}
	public static byte[] RenameCabIdentifiers(byte[] data)
	{
		var renamed=new Dictionary<string,byte[]>();
		for(int i=0;i+36<=data.Length;i++)
		{
			if(data[i]!='C'||data[i+1]!='A'||data[i+2]!='B'||data[i+3]!='-')continue;
			bool valid=true;for(int j=4;j<36;j++){byte b=data[i+j];if(!(b>='0'&&b<='9'||b>='a'&&b<='f'||b>='A'&&b<='F')){valid=false;break;}}
			if(!valid)continue;string key=Encoding.ASCII.GetString(data,i,36);
			if(!renamed.TryGetValue(key,out var replacement))
			{
				using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(Encoding.ASCII.GetBytes("SkyCoopNative:"+key))).Replace("-","").Substring(0,32).ToLowerInvariant();replacement=Encoding.ASCII.GetBytes("CAB-"+hash);}
				renamed[key]=replacement;
			}
			Buffer.BlockCopy(replacement,0,data,i,36);i+=35;
		}
		return data;
	}
}
