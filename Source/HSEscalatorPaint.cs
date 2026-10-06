using System;
using System.IO;
using UnityEngine;

// The world paint atlas is a compressed Texture2DArray shared by every block; writing to it or
// swapping it blacked out the whole paintbrush. The moving treads get their own 2D material instead.
public static class HSEscalatorPaint
{
    static Texture2D grate;
    static Material grateMat;
    static bool shaderMissing;

    public static Texture2D Grate()
    {
        if (grate != null) return grate;
        try
        {
            var root = HSEscalatorMod.ModPath ?? "";
            var path = Path.Combine(root, "Resources", "hs_escalator_grate.jpg");
            if (!File.Exists(path))
            {
                HSEscalatorDebug.Warn("Escalator grate texture missing: " + path);
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
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
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

    // Returns false when the grate cannot be used, so the caller keeps the block's own look.
    public static bool Apply(Transform model, HSEscalatorPinnedLooks pin)
    {
        if (model == null) return false;
        var mat = GrateMaterial();
        if (mat == null) return false;
        bool any = false;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r != null ? r.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) continue;
            var mesh = Unwrap(mf.sharedMesh);
            if (mesh == null) continue;
            mf.sharedMesh = mesh;
            if (pin != null) pin.Own(mesh);
            int subs = Math.Max(1, mesh.subMeshCount);
            var mats = new Material[subs];
            for (int i = 0; i < subs; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.SetPropertyBlock(null);
            any = true;
        }
        return any;
    }

    static Material GrateMaterial()
    {
        if (grateMat != null) return grateMat;
        if (shaderMissing) return null;
        var tex = Grate();
        if (tex == null) return null;
        var sh = StepShader();
        if (sh == null)
        {
            shaderMissing = true;
            HSEscalatorDebug.Warn("No 2D shader for the grate; treads keep the block look.");
            return null;
        }
        var m = new Material(sh) { hideFlags = HideFlags.DontUnloadUnusedAsset };
        m.name = "hs_escalator_grate_mat";
        m.mainTexture = tex;
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        grateMat = m;
        HSEscalatorDebug.Info("Grate material " + sh.name);
        return grateMat;
    }

    // Block meshes carry atlas UVs and paint-tinted vertex colours. One grate tile per face, white colours.
    static Mesh Unwrap(Mesh src)
    {
        Mesh mesh;
        try { mesh = UnityEngine.Object.Instantiate(src); }
        catch { return null; }
        if (mesh == null || mesh.vertexCount < 3) return null;
        mesh.name = "hs_escalator_tread";
        var verts = mesh.vertices;
        var norms = mesh.normals;
        bool haveN = norms != null && norms.Length == verts.Length;
        var uvs = new Vector2[verts.Length];
        var b = mesh.bounds;
        float dx = Mathf.Max(0.0001f, b.size.x);
        float dy = Mathf.Max(0.0001f, b.size.y);
        float dz = Mathf.Max(0.0001f, b.size.z);
        for (int i = 0; i < verts.Length; i++)
        {
            var p = verts[i];
            var n = haveN ? norms[i] : (p - b.center);
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
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
        if (!haveN) mesh.RecalculateNormals();
        return mesh;
    }

    static Shader StepShader()
    {
        var names = new[]
        {
            "Legacy Shaders/Diffuse",
            "Mobile/Diffuse",
            "Legacy Shaders/VertexLit",
            "Unlit/Texture"
        };
        for (int i = 0; i < names.Length; i++)
        {
            var s = Shader.Find(names[i]);
            if (s != null && s.isSupported && s.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) < 0)
                return s;
        }
        return null;
    }
}
