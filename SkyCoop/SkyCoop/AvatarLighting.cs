using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkyCoop;

internal static class AvatarLighting
{
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly int AmbientRed = Shader.PropertyToID("unity_SHAr");
    private static readonly int AmbientGreen = Shader.PropertyToID("unity_SHAg");
    private static readonly int AmbientBlue = Shader.PropertyToID("unity_SHAb");
    private static readonly List<Avatar> Avatars = new();
    private static MaterialPropertyBlock block;
    private static float nextUpdate;

    private sealed class Avatar
    {
        internal GameObject Root;
        internal List<Renderer> Renderers = new();
        internal Color? Ambient;
    }

    internal static void Apply(GameObject avatar)
    {
        var entry = new Avatar { Root = avatar };
        // The stock prefab keeps the wardrobe and body apart from hand props and lights.
        for (int i = 0; i < 2; i++)
            foreach (var renderer in avatar.transform.GetChild(i).GetComponentsInChildren<Renderer>(true))
                entry.Renderers.Add(renderer);
        foreach (var renderer in avatar.transform.GetChild(3).GetComponentsInChildren<Renderer>(true))
            if (renderer.name.Contains("backpack", System.StringComparison.OrdinalIgnoreCase))
                entry.Renderers.Add(renderer);
        Avatars.Add(entry);
    }

    internal static void Tick()
    {
        if (Avatars.Count == 0 || Time.realtimeSinceStartup < nextUpdate) return;
        nextUpdate = Time.realtimeSinceStartup + .25f;
        bool inWorld = GearCatalogIndex.InWorld;
        Color? ambient = inWorld && GameManager.GetUniStorm() != null
            ? Shader.GetGlobalColor(UniStormWeatherSystem.s_AmbientLightColorShaderID) : null;
        for (int i = Avatars.Count - 1; i >= 0; i--)
        {
            var entry = Avatars[i];
            if (entry.Root == null) { Avatars.RemoveAt(i); continue; }
            if (!ambient.HasValue || !entry.Root.activeInHierarchy || (entry.Ambient is { } before &&
                ((Vector4)(ambient.Value - before)).sqrMagnitude < .0004f)) continue;
            entry.Ambient = ambient;
            block ??= new MaterialPropertyBlock();
            foreach (var renderer in entry.Renderers)
            {
                if (renderer == null) continue;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                renderer.GetPropertyBlock(block);
                block.SetColor(Emission, Color.black);
                // Feed the game's ambient into Standard's constant SH term. Direct lights still light the mesh normally.
                block.SetVector(AmbientRed, new Vector4(0, 0, 0, ambient.Value.r));
                block.SetVector(AmbientGreen, new Vector4(0, 0, 0, ambient.Value.g));
                block.SetVector(AmbientBlue, new Vector4(0, 0, 0, ambient.Value.b));
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
