using System;
using System.IO;
using System.Linq;
using System.Text;
using AssetsTools.NET;
using SkyCoop;

string cab="CAB-0123456789abcdef0123456789abcdef";
string input=cab+".resS|archive:/"+cab+"/"+cab+"|CAB-not-a-guid";
byte[] renamed=NativeAssetIsolation.RenameCabIdentifiers(Encoding.ASCII.GetBytes(input));
string output=Encoding.ASCII.GetString(renamed);
if(output.Length!=input.Length||output.Contains(cab)||!output.EndsWith("CAB-not-a-guid")||output.Substring(0,36)!=output.Substring(51,36))throw new Exception("CAB rewrite corrupted identifiers");
Console.WriteLine("PASS: isolated identifiers retain offsets and shared external references");
if(args.Length>=2)
{
 var plan=NativeAssetIsolation.Plan(args[0],args[1]);
 Console.WriteLine("Selected bundles="+plan.Count+" compressed MiB="+Math.Round(plan.Sum(p=>new FileInfo(p).Length)/1048576d,1));
 foreach(string path in plan)Console.WriteLine(path);
 byte[] data=NativeAssetIsolation.ReadIsolated(args[0],args[1]);var bundle=new AssetBundleFile();
 bundle.Read(new AssetsFileReader(new MemoryStream(data)));
 Console.WriteLine("Rewritten bundle readable: "+string.Join(", ",bundle.GetAllFileNames()));bundle.Close();
}
