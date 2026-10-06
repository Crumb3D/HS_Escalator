using System;
using System.Collections.Generic;
using UnityEngine;

// Rubber handrail on each side of the deck. A placed side piece (end, slope, flat) gets the
// rail in its channel, including a newel return when an end piece is at the landing. A solid
// wall with no piece gets the same moving rail stuck to the wall. Nothing there, no rail.
public class HSEscalatorRail
{
    const float AboveTread = 0.90f;
    const float ChannelInset = 0.09f;
    const float WallInset = 0.04f;
    const float NewelR = 0.22f;
    const float Repeat = 2f;

    enum Kind { None, Wall, Piece, End }

    readonly List<Transform> strips = new List<Transform>();
    readonly List<Mesh> meshes = new List<Mesh>();
    Material mat;
    static Texture2D groove;
    static bool shaderMissing;

    public void Clear()
    {
        for (int i = 0; i < meshes.Count; i++)
            if (meshes[i] != null) UnityEngine.Object.Destroy(meshes[i]);
        meshes.Clear();
        for (int i = 0; i < strips.Count; i++)
            if (strips[i] != null) UnityEngine.Object.Destroy(strips[i].gameObject);
        strips.Clear();
        if (mat != null) UnityEngine.Object.Destroy(mat);
        mat = null;
    }

    public void Apply(float phase)
    {
        if (strips.Count == 0) return;
        var pos = -Origin.position;
        for (int i = 0; i < strips.Count; i++)
            if (strips[i] != null) strips[i].position = pos;
        if (mat != null) mat.mainTextureOffset = new Vector2(-phase * Repeat, 0f);
    }

    public static int LayoutSig(World world, HSEscalatorPath path)
    {
        if (world == null || path == null || path.Heights == null || path.Length < 1) return 0;
        unchecked
        {
            int h = path.Length * 397 ^ path.Width * 17 ^ path.RunAxis ^ path.RunSign;
            for (int side = 0; side < 2; side++)
            for (int c = -1; c <= path.Length; c++)
            {
                int x, y, z;
                Cell(path, c, side, out x, out y, out z);
                for (int dy = -1; dy <= 2; dy++)
                {
                    if (y + dy < 0 || y + dy > 255) continue;
                    var bv = world.GetBlock(new Vector3i(x, y + dy, z));
                    h = h * 31 + bv.type;
                    h = h * 31 + bv.rotation;
                }
            }
            return h;
        }
    }

    public void Build(World world, HSEscalatorPath path, Transform root)
    {
        Clear();
        if (world == null || path == null || root == null || path.Heights == null || path.Length < 1) return;
        if (path.RunSign == 0) return;
        var shader = RailShader();
        if (shader == null)
        {
            if (!shaderMissing)
            {
                shaderMissing = true;
                HSEscalatorDebug.Warn("No shader for the handrail. The side pieces still place.");
            }
            return;
        }

        var along = path.RunAxis == 0
            ? new Vector3(path.RunSign, 0f, 0f)
            : new Vector3(0f, 0f, path.RunSign);
        var across = path.RunAxis == 0 ? Vector3.forward : Vector3.right;

        int stripsMade = 0;
        var note = new string[2];
        for (int side = 0; side < 2; side++)
        {
            int pieces = 0, walls = 0, ends = 0;
            int lowEnd = KindAt(world, path, -1, side) == Kind.End ? -1
                : KindAt(world, path, 0, side) == Kind.End ? 0 : int.MinValue;
            int highEnd = KindAt(world, path, path.Length, side) == Kind.End ? path.Length
                : KindAt(world, path, path.Length - 1, side) == Kind.End ? path.Length - 1 : int.MinValue;

            var pts = new List<Vector3>();
            for (int c = -1; c <= path.Length; c++)
            {
                var kind = KindAt(world, path, c, side);
                if (kind == Kind.None)
                {
                    stripsMade += Flush(pts, across, along, root);
                    pts.Clear();
                    continue;
                }
                if (kind == Kind.End) ends++;
                else if (kind == Kind.Piece) pieces++;
                else walls++;

                var p = Point(path, c, side, kind == Kind.Wall);
                if (c == lowEnd)
                {
                    AddNewel(pts, p, -along, true);
                    pts.Add(p);
                    pts.Add(p + along * 0.48f);
                }
                else if (c == highEnd)
                {
                    pts.Add(p - along * 0.48f);
                    pts.Add(p);
                    AddNewel(pts, p, along, false);
                }
                else pts.Add(p);
            }
            stripsMade += Flush(pts, across, along, root);
            note[side] = ends + pieces + walls == 0
                ? "nothing"
                : (ends > 0 ? ends + " end " : "") + (pieces > 0 ? pieces + " side " : "") + (walls > 0 ? walls + " wall" : "");
        }

        if (stripsMade == 0) return;
        var tex = Groove();
        mat = new Material(shader) { hideFlags = HideFlags.DontUnloadUnusedAsset };
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(Repeat, 1f);
        mat.color = Color.white;
        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", 0);
        for (int i = 0; i < strips.Count; i++)
        {
            var r = strips[i] != null ? strips[i].GetComponent<MeshRenderer>() : null;
            if (r != null) r.sharedMaterial = mat;
        }
        HSEscalatorDebug.Info("Handrail — one side: " + note[0].Trim() + ". Other side: " + note[1].Trim() + ".");
    }

    int Flush(List<Vector3> pts, Vector3 across, Vector3 along, Transform root)
    {
        if (pts == null || pts.Count == 0) return 0;
        if (pts.Count == 1 && along.sqrMagnitude > 0.5f)
        {
            var only = pts[0];
            pts.Insert(0, only - along * 0.45f);
            pts.Add(only + along * 0.45f);
        }
        if (pts.Count < 2) return 0;
        var mesh = Tube(pts, across);
        pts.Clear();
        if (mesh == null) return 0;
        var go = new GameObject("rail");
        go.transform.SetParent(root, false);
        go.transform.position = -Origin.position;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
        strips.Add(go.transform);
        meshes.Add(mesh);
        return 1;
    }

    static void AddNewel(List<Vector3> pts, Vector3 cellCenter, Vector3 outward, bool fromBottom)
    {
        if (outward.sqrMagnitude < 0.5f) return;
        outward.Normalize();
        var hub = cellCenter + outward * 0.10f;
        hub.y = cellCenter.y - NewelR;
        int n = 8;
        for (int i = 0; i <= n; i++)
        {
            int k = fromBottom ? n - i : i;
            float a = Mathf.PI * k / n;
            pts.Add(hub + outward * (Mathf.Sin(a) * NewelR) + Vector3.up * (Mathf.Cos(a) * NewelR));
        }
    }

    static Vector3 Point(HSEscalatorPath path, int col, int side, bool wall)
    {
        int x, y, z;
        Cell(path, col, side, out x, out y, out z);
        float into = wall ? WallInset : ChannelInset;
        float yRail = path.BeltHeight(col) * 0.5f + AboveTread;
        if (path.RunAxis == 0)
        {
            float face = side == 0 ? z + 1 : z;
            return new Vector3(x + 0.5f, yRail, side == 0 ? face - into : face + into);
        }
        float faceX = side == 0 ? x + 1 : x;
        return new Vector3(side == 0 ? faceX - into : faceX + into, yRail, z + 0.5f);
    }

    static Kind KindAt(World world, HSEscalatorPath path, int col, int side)
    {
        int x, y, z;
        Cell(path, col, side, out x, out y, out z);
        var best = Kind.None;
        for (int dy = -1; dy <= 2; dy++)
        {
            var k = Look(world, x, y + dy, z);
            if (k == Kind.End) return Kind.End;
            if (k == Kind.Piece) best = Kind.Piece;
            else if (k == Kind.Wall && best == Kind.None) best = Kind.Wall;
        }
        return best;
    }

    static void Cell(HSEscalatorPath path, int col, int side, out int x, out int y, out int z)
    {
        int sample = col < 0 ? 0 : col >= path.Length ? path.Length - 1 : col;
        y = HSEscalatorPath.BlockYFromHeight(path.Heights[sample]);
        if (path.RunAxis == 0)
        {
            x = path.OriginX + col * path.RunSign;
            z = side == 0 ? path.LaneMinZ - 1 : path.LaneMinZ + path.Width;
        }
        else
        {
            z = path.OriginZ + col * path.RunSign;
            x = side == 0 ? path.LaneMinX - 1 : path.LaneMinX + path.Width;
        }
    }

    static Kind Look(World world, int x, int y, int z)
    {
        if (world == null || y < 0 || y > 255) return Kind.None;
        var bv = world.GetBlock(new Vector3i(x, y, z));
        if (bv.isair || bv.Block == null) return Kind.None;
        var name = bv.Block.GetBlockName() ?? "";
        if (name.StartsWith("hsescalatorEnd", StringComparison.Ordinal)) return Kind.End;
        if (name.StartsWith("hsescalatorSlope", StringComparison.Ordinal)
            || name.StartsWith("hsescalatorFlat", StringComparison.Ordinal))
            return Kind.Piece;
        if (name.StartsWith("hsescalator", StringComparison.Ordinal)) return Kind.None;
        if (!bv.Block.IsCollideMovement) return Kind.None;
        try
        {
            if (bv.Block.shape != null && bv.Block.shape.IsTerrain()) return Kind.None;
        }
        catch { return Kind.None; }
        try
        {
            var bounds = bv.Block.shape != null ? bv.Block.shape.GetBounds(bv) : null;
            if (bounds != null && bounds.Length > 0)
            {
                float tall = 0f;
                for (int i = 0; i < bounds.Length; i++)
                    if (bounds[i].size.y > tall) tall = bounds[i].size.y;
                if (tall > 0.01f && tall < 0.75f) return Kind.None;
            }
        }
        catch { }
        return Kind.Wall;
    }

    static Mesh Tube(List<Vector3> src, Vector3 across)
    {
        var p = new List<Vector3>(src.Count);
        for (int i = 0; i < src.Count; i++)
        {
            if (p.Count > 0 && (src[i] - p[p.Count - 1]).sqrMagnitude < 0.0004f) continue;
            p.Add(src[i]);
        }
        int n = p.Count;
        if (n < 2) return null;

        var tang = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 delta = i == 0 ? p[1] - p[0]
                : i == n - 1 ? p[n - 1] - p[n - 2]
                : p[i + 1] - p[i - 1];
            tang[i] = delta.sqrMagnitude < 1e-6f ? Vector3.forward : delta.normalized;
        }
        var side = across.sqrMagnitude < 0.5f ? Vector3.forward : across.normalized;
        if (Vector3.Cross(side, tang[0]).y < 0f) side = -side;

        const float halfW = 0.036f;
        const float halfH = 0.018f;
        var verts = new Vector3[n * 4];
        var uvs = new Vector2[n * 4];
        var ups = new Vector3[n];
        float dist = 0f;
        var prevUp = Vector3.up;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) dist += Vector3.Distance(p[i], p[i - 1]);
            var up = Vector3.Cross(side, tang[i]);
            if (up.sqrMagnitude < 1e-6f) up = prevUp;
            else up.Normalize();
            if (Vector3.Dot(up, prevUp) < 0f) up = -up;
            prevUp = up;
            ups[i] = up;
            var a = side * halfW;
            var b = up * halfH;
            int o = i * 4;
            verts[o] = p[i] + a + b;
            verts[o + 1] = p[i] - a + b;
            verts[o + 2] = p[i] - a - b;
            verts[o + 3] = p[i] + a - b;
            uvs[o] = new Vector2(dist, 0.85f);
            uvs[o + 1] = new Vector2(dist, 0.85f);
            uvs[o + 2] = new Vector2(dist, 0.15f);
            uvs[o + 3] = new Vector2(dist, 0.15f);
        }

        int quads = (n - 1) * 4 + 2;
        var tris = new int[quads * 6];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;
            var up = ups[i];
            AddQuad(tris, verts, ref t, a, a + 1, b + 1, b, up);
            AddQuad(tris, verts, ref t, a + 3, a + 2, b + 2, b + 3, -up);
            AddQuad(tris, verts, ref t, a, a + 3, b + 3, b, side);
            AddQuad(tris, verts, ref t, a + 1, a + 2, b + 2, b + 1, -side);
        }
        AddQuad(tris, verts, ref t, 0, 3, 2, 1, -tang[0]);
        int e = (n - 1) * 4;
        AddQuad(tris, verts, ref t, e, e + 1, e + 2, e + 3, tang[n - 1]);

        var mesh = new Mesh { name = "hs_escalator_rail" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddQuad(int[] tris, Vector3[] verts, ref int t, int i0, int i1, int i2, int i3, Vector3 wantOut)
    {
        var n = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]);
        if (Vector3.Dot(n, wantOut) < 0f)
        {
            tris[t++] = i0; tris[t++] = i3; tris[t++] = i2;
            tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
        }
        else
        {
            tris[t++] = i0; tris[t++] = i1; tris[t++] = i2;
            tris[t++] = i0; tris[t++] = i2; tris[t++] = i3;
        }
    }

    static Shader RailShader()
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

    static Texture2D Groove()
    {
        if (groove != null) return groove;
        const int w = 64;
        const int h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
        {
            name = "hs_escalator_rail",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool grooveLine = (x % 16) < 3;
            bool top = y >= 5 && y <= 10;
            byte v = grooveLine ? (byte)28 : (top ? (byte)18 : (byte)8);
            px[y * w + x] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        groove = tex;
        return groove;
    }
}
