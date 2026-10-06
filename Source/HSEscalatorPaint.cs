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

    const string TreadMeshName = "hs_escalator_tread";
    static bool loggedPainted;

    // Block clones fill their mesh after CloneModel returns, so painting is retried by a
    // component until the mesh is there. Returns false when the grate cannot be used at all.
    public static bool Apply(Transform model, HSEscalatorPinnedLooks pin)
    {
        if (model == null || GrateMaterial() == null) return false;
        var skin = model.gameObject.AddComponent<HSEscalatorGrateSkin>();
        skin.Pin = pin;
        skin.Paint();
        return true;
    }

    // Paints every renderer under root that carries a block mesh; already-painted ones only get their material checked.
    public static int PaintAll(Transform root, HSEscalatorPinnedLooks pin)
    {
        var mat = GrateMaterial();
        if (mat == null || root == null) return 0;
        int painted = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r != null ? r.GetComponent<MeshFilter>() : null;
            var src = mf != null ? mf.sharedMesh : null;
            if (src == null || src.vertexCount < 3 || !src.isReadable) continue;
            if (src.name != TreadMeshName)
            {
                var mesh = Unwrap(src);
                if (mesh == null) continue;
                mf.sharedMesh = mesh;
                if (pin != null) pin.Own(mesh);
                if (!loggedPainted)
                {
                    loggedPainted = true;
                    HSEscalatorDebug.Info("Grate painted on moving treads (" + mesh.vertexCount + " verts per step)");
                }
            }
            if (r.sharedMaterial != mat || r.sharedMaterials.Length != Math.Max(1, mf.sharedMesh.subMeshCount))
            {
                int subs = Math.Max(1, mf.sharedMesh.subMeshCount);
                var mats = new Material[subs];
                for (int i = 0; i < subs; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.SetPropertyBlock(null);
            }
            painted++;
        }
        return painted;
    }

    public static Material GrateMaterial()
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
        mesh.name = TreadMeshName;
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

    // Lists what a step clone actually holds, for when nothing on it could be painted.
    public static string Describe(Transform root)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            var m = mf != null ? mf.sharedMesh : null;
            sb.Append(r.GetType().Name).Append(" '").Append(r.name).Append("' mesh=")
              .Append(m == null ? "none" : m.name + " v" + m.vertexCount + (m.isReadable ? "" : " unreadable"))
              .Append(" mat=").Append(r.sharedMaterial != null ? r.sharedMaterial.name : "none").Append("; ");
        }
        return sb.Length == 0 ? "no renderers" : sb.ToString();
    }
}

// Keeps a step clone wearing the grate: the game fills or swaps the clone's mesh after it is made.
public class HSEscalatorGrateSkin : MonoBehaviour
{
    public HSEscalatorPinnedLooks Pin;
    float nextCheck;
    float born = -1f;
    bool reported;
    static int reports;

    public void Paint()
    {
        if (born < 0f) born = Time.time;
        int n = HSEscalatorPaint.PaintAll(transform, Pin);
        if (n == 0 && !reported && Time.time - born > 3f)
        {
            reported = true;
            if (reports++ < 3)
                HSEscalatorDebug.Warn("Step clone has nothing to paint: " + HSEscalatorPaint.Describe(transform));
        }
    }

    void LateUpdate()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        Paint();
    }
}
