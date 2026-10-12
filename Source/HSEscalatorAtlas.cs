using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

// Same path as HS_Lift: a paint's TextureId is a uvMapping id, and the grate is written
// into a free opaque-atlas slot (or the array is enlarged when 3.3 has none left).
[HarmonyPatch(typeof(TextureAtlasBlocks), "LoadTextureAtlas")]
public static class HSEscalatorAtlasPatch
{
    public static int GrateId = 513;
    public static bool Available;
    const int SilverId = 267;
    const string TexName = "hs_escalator_grate";
    const string TexFile = "hs_escalator_grate.jpg";

    static void Postfix(TextureAtlasBlocks __instance, int _idx)
    {
        try
        {
            if (_idx != MeshDescription.MESH_OPAQUE) return;
            if (GameManager.IsDedicatedServer) return;
            if (__instance == null) return;
            Available = false;
            var arr = __instance.diffuseTexture as Texture2DArray;
            if (arr == null)
            {
                HSEscalatorDebug.Warn("Opaque atlas is not a Texture2DArray; Escalator Grate paint is hidden.");
                BindPaintIds();
                return;
            }
            int id, slot;
            if (!PickSlot(__instance, arr.depth, out id, out slot))
            {
                int vanillaDepth = arr.depth;
                if (!GrowArrays(__instance, 1))
                {
                    HSEscalatorDebug.Warn("No free block-texture slot (" + vanillaDepth + ") and the array could not be enlarged. Escalator Grate paint is hidden.");
                    BindPaintIds();
                    return;
                }
                arr = (Texture2DArray)__instance.diffuseTexture;
                var map = __instance.uvMapping ?? new UVRectTiling[0];
                slot = vanillaDepth;
                id = map.Length;
                HSEscalatorDebug.Info("Block texture array enlarged " + vanillaDepth + " -> " + arr.depth + " for Escalator Grate.");
            }
            GrateId = id;
            Available = Install(__instance, arr, id, slot, TexFile, TexName);
            HSEscalatorDebug.Info("Escalator Grate paint " + (Available ? "ready" : "FAILED") + ": id " + GrateId + " slot " + slot + " (atlas " + arr.depth + ")");
            BindPaintIds();
        }
        catch (Exception e)
        {
            Available = false;
            BindPaintIds();
            HSEscalatorDebug.Error("Escalator Grate paintbrush atlas failed", e);
        }
    }

    static bool PickSlot(TextureAtlasBlocks atlas, int depth, out int id, out int slot)
    {
        id = 0;
        slot = 0;
        var map = atlas.uvMapping ?? new UVRectTiling[0];
        int existing = FindByName(map, TexName);
        if (existing >= 0 && map[existing].index > 0 && map[existing].index < depth)
        {
            id = existing;
            slot = map[existing].index;
            return true;
        }
        var used = new HashSet<int> { 0 };
        for (int i = 0; i < map.Length; i++)
        {
            if (string.IsNullOrEmpty(map[i].textureName)) continue;
            int n = Mathf.Clamp(Mathf.RoundToInt(map[i].uv.width * map[i].uv.height), 1, 16);
            for (int k = 0; k < n; k++) used.Add(map[i].index + k);
        }
        for (int s = depth - 1; s > 0; s--)
        {
            if (used.Contains(s)) continue;
            slot = s;
            id = map.Length;
            return true;
        }
        return false;
    }

    static Texture2DArray grownDiffuse, grownNormal, grownSpecular;

    static bool GrowArrays(TextureAtlasBlocks atlas, int extra)
    {
        if (SystemInfo.copyTextureSupport == UnityEngine.Rendering.CopyTextureSupport.None) return false;
        var diff = atlas.diffuseTexture as Texture2DArray;
        if (diff == null) return false;
        int depth = diff.depth + extra;
        var newDiff = Grow(diff, depth);
        if (newDiff == null) return false;
        var normal = atlas.normalTexture as Texture2DArray;
        var specular = atlas.specularTexture as Texture2DArray;
        var newNormal = normal != null ? Grow(normal, depth) : null;
        var newSpecular = specular != null ? Grow(specular, depth) : null;
        if ((normal != null && newNormal == null) || (specular != null && newSpecular == null))
        {
            DestroyTex(newDiff);
            DestroyTex(newNormal);
            DestroyTex(newSpecular);
            return false;
        }
        atlas.diffuseTexture = newDiff;
        if (newNormal != null) atlas.normalTexture = newNormal;
        if (newSpecular != null) atlas.specularTexture = newSpecular;
        DestroyTex(grownDiffuse);
        DestroyTex(grownNormal);
        DestroyTex(grownSpecular);
        grownDiffuse = newDiff;
        grownNormal = newNormal;
        grownSpecular = newSpecular;
        return true;
    }

    static Texture2DArray Grow(Texture2DArray src, int depth)
    {
        try
        {
            var flags = src.mipmapCount > 1
                ? UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain
                : UnityEngine.Experimental.Rendering.TextureCreationFlags.None;
            var dst = new Texture2DArray(src.width, src.height, depth, src.graphicsFormat, flags, src.mipmapCount);
            dst.name = src.name + "_hsescalator";
            dst.filterMode = src.filterMode;
            dst.wrapModeU = src.wrapModeU;
            dst.wrapModeV = src.wrapModeV;
            dst.anisoLevel = src.anisoLevel;
            dst.mipMapBias = src.mipMapBias;
            dst.Apply(false, true);
            int copy = Math.Min(src.depth, depth);
            for (int e = 0; e < copy; e++)
                Graphics.CopyTexture(src, e, dst, e);
            return dst;
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Could not enlarge texture array " + src.name + ": " + e.Message);
            return null;
        }
    }

    static void DestroyTex(Texture t)
    {
        if (t != null) UnityEngine.Object.Destroy(t);
    }

    static int FindByName(UVRectTiling[] map, string name)
    {
        for (int i = 0; i < map.Length; i++)
            if (map[i].textureName == name) return i;
        return -1;
    }

    static void BindPaintIds()
    {
        var list = BlockTextureData.list;
        if (list == null) return;
        for (int i = 0; i < list.Length; i++)
            if (list[i] != null) Apply(list[i]);
    }

    public static void Apply(BlockTextureData paint)
    {
        if (paint == null || paint.Name != "txName_HSEscalatorGrate") return;
        paint.TextureID = (ushort)(Available ? GrateId : SilverId);
        paint.Hidden = !Available;
    }

    static bool Install(TextureAtlasBlocks atlas, Texture2DArray arr, int id, int slot, string file, string name)
    {
        var img = LoadImage(file, name);
        if (img == null) return false;
        if (!WriteSlice(arr, slot, img))
        {
            HSEscalatorDebug.Warn("Could not write " + name + " into atlas slot " + slot + " (" + arr.format + ").");
            return false;
        }
        int silverSlot = SilverSlot(atlas.uvMapping);
        CopySlice(atlas.normalTexture as Texture2DArray, silverSlot, slot);
        CopySlice(atlas.specularTexture as Texture2DArray, silverSlot, slot);
        SetUv(atlas, id, slot, name);
        return true;
    }

    static int SilverSlot(UVRectTiling[] map)
    {
        if (map != null && SilverId < map.Length && map[SilverId].index > 0) return map[SilverId].index;
        return 0;
    }

    static void SetUv(TextureAtlasBlocks atlas, int id, int slot, string name)
    {
        var map = atlas.uvMapping ?? new UVRectTiling[0];
        if (map.Length <= id)
        {
            var grown = new UVRectTiling[id + 1];
            Array.Copy(map, grown, map.Length);
            map = grown;
            atlas.uvMapping = map;
        }
        UVRectTiling uv = SilverId < map.Length ? map[SilverId] : default(UVRectTiling);
        uv.uv = new Rect(0f, 0f, 1f, 1f);
        if (uv.blockW < 1) uv.blockW = 1;
        if (uv.blockH < 1) uv.blockH = 1;
        if (uv.color.a < 0.01f && uv.color.r < 0.01f && uv.color.g < 0.01f && uv.color.b < 0.01f)
            uv.color = Color.white;
        uv.index = slot;
        uv.textureName = name;
        map[id] = uv;
    }

    static bool WriteSlice(Texture2DArray arr, int slot, Texture2D src)
    {
        var fitted = Fit(src, arr.width, arr.height);
        if (fitted == null) return false;
        Texture2D conv = null;
        try
        {
            conv = Encode(fitted, arr);
            if (conv == null) return false;
            int mips = Math.Min(conv.mipmapCount, arr.mipmapCount);
            for (int m = 0; m < mips; m++)
                Graphics.CopyTexture(conv, 0, m, arr, slot, m);
            return true;
        }
        finally
        {
            if (conv != null) UnityEngine.Object.Destroy(conv);
            if (fitted != src) UnityEngine.Object.Destroy(fitted);
        }
    }

    static Texture2D Encode(Texture2D rgba, Texture2DArray arr)
    {
        bool mips = arr.mipmapCount > 1;
        if (arr.format == TextureFormat.DXT1 || arr.format == TextureFormat.DXT5)
        {
            var raw = new Texture2D(arr.width, arr.height, arr.format == TextureFormat.DXT1 ? TextureFormat.RGB24 : TextureFormat.RGBA32, mips, false);
            raw.SetPixels32(rgba.GetPixels32());
            raw.Apply(mips, false);
            raw.Compress(true);
            if (raw.format == arr.format) return raw;
            UnityEngine.Object.Destroy(raw);
            return null;
        }
        var conv = new Texture2D(arr.width, arr.height, arr.format, mips);
        conv.wrapMode = TextureWrapMode.Repeat;
        conv.filterMode = FilterMode.Bilinear;
        if (Graphics.ConvertTexture(rgba, conv)) return conv;
        UnityEngine.Object.Destroy(conv);
        return null;
    }

    static void CopySlice(Texture2DArray arr, int from, int to)
    {
        if (arr == null || from < 0 || to < 0 || from >= arr.depth || to >= arr.depth) return;
        for (int m = 0; m < arr.mipmapCount; m++)
            Graphics.CopyTexture(arr, from, m, arr, to, m);
    }

    static Texture2D Fit(Texture2D src, int w, int h)
    {
        if (src == null) return null;
        if (src.width == w && src.height == h) return src;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        dst.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
        dst.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        dst.wrapMode = TextureWrapMode.Repeat;
        return dst;
    }

    static Texture2D LoadImage(string file, string name)
    {
        try
        {
            var path = Path.Combine(HSEscalatorMod.ModPath ?? "", "Resources", file);
            if (!File.Exists(path))
            {
                HSEscalatorDebug.Warn("Escalator Grate texture missing: " + path);
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return tex;
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Escalator Grate texture load failed: " + e.Message);
            return null;
        }
    }
}

[HarmonyPatch(typeof(BlockTextureData), "Init")]
public static class HSEscalatorPaintIdPatch
{
    static void Postfix(BlockTextureData __instance)
    {
        if (__instance == null || string.IsNullOrEmpty(__instance.Name)) return;
        HSEscalatorAtlasPatch.Apply(__instance);
    }
}
