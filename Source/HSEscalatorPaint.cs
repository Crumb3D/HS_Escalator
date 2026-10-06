using System;
using System.IO;
using UnityEngine;

public static class HSEscalatorPaint
{
    const string PaintName = "txName_HSEscalatorGrate";
    const int PreferId = 242;

    public static int PaintId = PreferId;

    static Texture2D grate;
    static Material grateMat;
    static Shader stepShader;
    static bool atlasTried;

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
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.name = "hs_escalator_grate";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            grate = tex;
            HSEscalatorDebug.Info("Loaded grate " + tex.width + "x" + tex.height);
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Could not load grate texture: " + e.Message);
        }
        return grate;
    }

    public static void EnsureInPaintbrush()
    {
        try
        {
            var list = BlockTextureData.list;
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null || list[i].Name != PaintName) continue;
                PaintId = list[i].ID;
                InjectPaintbrushSlice(list[i]);
                return;
            }
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Paintbrush grate: " + e.Message);
        }
    }

    public static void Apply(Transform model, HSEscalatorPinnedLooks pin)
    {
        var mat = GrateMaterial();
        if (mat == null || model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            Unwrap(r);
            r.sharedMaterial = mat;
            if (pin != null) pin.Own(mat);
        }
    }

    static Material GrateMaterial()
    {
        if (grateMat != null) return grateMat;
        var tex = Grate();
        var sh = StepShader();
        if (tex == null || sh == null) return null;
        var m = new Material(sh) { hideFlags = HideFlags.DontUnloadUnusedAsset };
        m.name = "hs_escalator_grate_mat";
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        m.mainTexture = tex;
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") is Texture2DArray)
        {
            UnityEngine.Object.Destroy(m);
            HSEscalatorDebug.Warn("Grate shader still wants a texture array; skipped.");
            return null;
        }
        grateMat = m;
        HSEscalatorDebug.Info("Grate material " + sh.name);
        return grateMat;
    }

    static void Unwrap(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null) return;
        Mesh mesh = null;
        try { mesh = mf.mesh; } catch { }
        if (mesh == null || mesh.vertexCount < 3) return;
        var verts = mesh.vertices;
        var norms = mesh.normals;
        var uvs = new Vector2[verts.Length];
        var b = mesh.bounds;
        float dx = Mathf.Max(0.0001f, b.size.x);
        float dy = Mathf.Max(0.0001f, b.size.y);
        float dz = Mathf.Max(0.0001f, b.size.z);
        bool haveN = norms != null && norms.Length == verts.Length;
        for (int i = 0; i < verts.Length; i++)
        {
            var p = verts[i];
            float ax, ay, az;
            if (haveN)
            {
                var n = norms[i];
                ax = Mathf.Abs(n.x);
                ay = Mathf.Abs(n.y);
                az = Mathf.Abs(n.z);
            }
            else
            {
                ax = Mathf.Abs(p.x - b.center.x);
                ay = Mathf.Abs(p.y - b.center.y);
                az = Mathf.Abs(p.z - b.center.z);
            }
            if (ay >= ax && ay >= az)
                uvs[i] = new Vector2((p.x - b.min.x) / dx, (p.z - b.min.z) / dz);
            else if (ax >= az)
                uvs[i] = new Vector2((p.z - b.min.z) / dz, (p.y - b.min.y) / dy);
            else
                uvs[i] = new Vector2((p.x - b.min.x) / dx, (p.y - b.min.y) / dy);
        }
        mesh.uv = uvs;
        var white = new Color[verts.Length];
        for (int i = 0; i < white.Length; i++) white[i] = Color.white;
        mesh.colors = white;
    }

    static Shader StepShader()
    {
        if (stepShader != null) return stepShader;
        var names = new[]
        {
            "Legacy Shaders/Diffuse",
            "Mobile/Diffuse",
            "Diffuse",
            "Unlit/Texture",
            "Legacy Shaders/VertexLit",
            "Mobile/VertexLit"
        };
        for (int i = 0; i < names.Length; i++)
        {
            var s = Shader.Find(names[i]);
            if (!BadShader(s))
            {
                stepShader = s;
                return stepShader;
            }
        }
        try
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.hideFlags = HideFlags.HideAndDontSave;
            var r = cube.GetComponent<Renderer>();
            var s = r != null && r.sharedMaterial != null ? r.sharedMaterial.shader : null;
            UnityEngine.Object.Destroy(cube);
            if (!BadShader(s))
            {
                stepShader = s;
                return stepShader;
            }
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Primitive shader failed: " + e.Message);
        }
        return null;
    }

    static bool BadShader(Shader s)
    {
        if (s == null) return true;
        var n = s.name;
        return n.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("UI/", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("GUI", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Array", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void InjectPaintbrushSlice(BlockTextureData data)
    {
        if (atlasTried || data == null) return;
        atlasTried = true;
        if (GameManager.IsDedicatedServer) return;
        var tex = Grate();
        if (tex == null) return;
        var meshes = MeshDescription.meshes;
        if (meshes == null || meshes.Length == 0 || meshes[0] == null) return;
        var arr = meshes[0].TexDiffuse as Texture2DArray;
        if (arr == null)
        {
            HSEscalatorDebug.Warn("No opaque paint atlas to put the grate on.");
            return;
        }
        int slice = FreeSlice(arr.depth, data.TextureID);
        if (slice < 0)
        {
            HSEscalatorDebug.Warn("No free paint atlas slice for the grate.");
            return;
        }
        var fitted = Fit(tex, arr.width, arr.height);
        if (fitted == null) return;
        try
        {
            if (!Graphics.ConvertTexture(fitted, 0, arr, slice))
            {
                HSEscalatorDebug.Warn("Could not write grate into paint atlas slice " + slice);
                return;
            }
            data.TextureID = (ushort)slice;
            HSEscalatorDebug.Info("Paintbrush grate on atlas slice " + slice + " (" + arr.width + "x" + arr.height + " " + arr.format + ")");
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Paintbrush grate inject failed: " + e.Message);
        }
    }

    static int FreeSlice(int depth, int current)
    {
        var used = new bool[depth];
        var list = BlockTextureData.list;
        if (list != null)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null) continue;
                int id = list[i].TextureID;
                if (id >= 0 && id < depth) used[id] = true;
            }
        }
        if (current >= 0 && current < depth) used[current] = true;
        if (depth > 0) used[0] = true;
        for (int i = depth - 1; i >= 1; i--)
            if (!used[i]) return i;
        return -1;
    }

    static Texture2D Fit(Texture2D src, int w, int h)
    {
        if (src == null) return null;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        t.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        t.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return t;
    }
}
