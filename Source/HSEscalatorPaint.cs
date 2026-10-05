using System;
using System.IO;
using UnityEngine;

public static class HSEscalatorPaint
{
    static Texture2D grate;

    public static Texture2D Grate()
    {
        if (grate != null) return grate;
        try
        {
            var root = HSEscalatorMod.ModPath ?? "";
            var path = Path.Combine(root, "Resources", "hs_escalator_grate.jpg");
            if (!File.Exists(path))
                path = Path.Combine(root, "Source", "Blocks", "hs_escalator_grate.jpg");
            if (!File.Exists(path))
            {
                HSEscalatorDebug.Warn("Escalator grate texture missing.");
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), true))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = "hs_escalator_grate";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            grate = tex;
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Could not load grate texture: " + e.Message);
        }
        return grate;
    }

    public static void Apply(Transform model)
    {
        var tex = Grate();
        if (tex == null || model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.materials;
            if (mats == null) continue;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                m.mainTexture = tex;
                SetIfHas(m, "_MainTex", tex);
                SetIfHas(m, "_BaseMap", tex);
                SetIfHas(m, "_Albedo", tex);
                SetIfHas(m, "_Diffuse", tex);
                SetIfHas(m, "_BaseColorMap", tex);
            }
        }
    }

    static void SetIfHas(Material m, string name, Texture tex)
    {
        if (m.HasProperty(name)) m.SetTexture(name, tex);
    }
}
