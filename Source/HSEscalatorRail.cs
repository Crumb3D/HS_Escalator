using System;
using System.Collections.Generic;
using UnityEngine;

// Side of the escalator, like a real one: rubber rail on the step edge, a metal skirt down to
// the tread, glass above it so the wall block is covered, and the rubber looping under with the
// belt. Any block in the side cell (sheet through cube) turns that on. A glass sheet under the
// return lets you see the steps coming back.
public class HSEscalatorRail
{
    const float AboveTread = 0.90f;
    const float ReturnDrop = 1f;
    const float NewelR = 0.5f;
    const float Repeat = 2f;
    const float SkirtHigh = 0.28f;

    enum Kind { None, Wall, Piece, End }

    readonly List<Transform> strips = new List<Transform>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Material> mats = new List<Material>();
    Material rubber;
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
        for (int i = 0; i < mats.Count; i++)
            if (mats[i] != null) UnityEngine.Object.Destroy(mats[i]);
        mats.Clear();
        rubber = null;
    }

    public void Apply(float phase)
    {
        if (strips.Count == 0) return;
        var pos = -Origin.position;
        for (int i = 0; i < strips.Count; i++)
            if (strips[i] != null) strips[i].position = pos;
        if (rubber != null) rubber.mainTextureOffset = new Vector2(-phase * Repeat, 0f);
    }

    public static int LayoutSig(World world, HSEscalatorPath path)
    {
        if (world == null || path == null || path.Heights == null || path.Length < 1) return 0;
        unchecked
        {
            int h = path.Length * 397 ^ path.Width * 17 ^ path.RunAxis ^ path.RunSign;
            for (int side = 0; side < 2; side++)
            for (int c = 0; c < path.Length; c++)
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

    public void Build(World world, HSEscalatorPath path, Transform root, bool glassSides)
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

        BuildUnderside(path, root, shader);

        int sides = 0;
        for (int side = 0; side < 2; side++)
        {
            var outward = path.RunAxis == 0
                ? new Vector3(0f, 0f, side == 0 ? -1f : 1f)
                : new Vector3(side == 0 ? -1f : 1f, 0f, 0f);
            int start = -1;
            bool any = false;
            for (int c = 0; c <= path.Length; c++)
            {
                bool on = c < path.Length && KindAt(world, path, c, side) != Kind.None;
                if (on)
                {
                    any = true;
                    if (start < 0) start = c;
                    continue;
                }
                if (start >= 0)
                {
                    BuildSide(path, root, shader, start, c - 1, along, outward, glassSides);
                    start = -1;
                }
            }
            if (any) sides++;
        }

        if (rubber != null)
            HSEscalatorDebug.Info("Handrail on the step edge, looping under. Sides covered: " + sides + ".");
    }

    void BuildUnderside(HSEscalatorPath path, Transform root, Shader shader)
    {
        var left = new List<Vector3>();
        var right = new List<Vector3>();
        for (int c = 0; c < path.Length; c++)
        {
            float y = path.BeltHeight(c) * 0.5f - ReturnDrop - 0.56f;
            float x0, z0, x1, z1;
            Boundary(path, c, 0, out x0, out z0);
            Boundary(path, c, 1, out x1, out z1);
            left.Add(new Vector3(x0, y, z0));
            right.Add(new Vector3(x1, y, z1));
        }
        var mesh = Deck(left, right);
        if (mesh == null) return;
        SpawnGlass(mesh, new Color(0.72f, 0.86f, 0.92f, 0.35f), root, shader);
    }

    void BuildSide(HSEscalatorPath path, Transform root, Shader shader, int a, int b, Vector3 along, Vector3 outward, bool glassSides)
    {
        var skirtLo = new List<Vector3>();
        var skirtHi = new List<Vector3>();
        var glassLo = new List<Vector3>();
        var glassHi = new List<Vector3>();
        var glowLo = new List<Vector3>();
        var glowHi = new List<Vector3>();
        var rail = new List<Vector3>();

        int from = a;
        int to = b;
        if (a == b)
        {
            // One column still needs a length of panel.
            from = a;
            to = a;
        }

        for (int c = from; c <= to; c++)
            AddStation(path, c, outward, skirtLo, skirtHi, glassLo, glassHi, glowLo, glowHi, rail);

        if (a == b && along.sqrMagnitude > 0.5f)
        {
            Stretch(skirtLo, along);
            Stretch(skirtHi, along);
            Stretch(glassLo, along);
            Stretch(glassHi, along);
            Stretch(glowLo, along);
            Stretch(glowHi, along);
            Stretch(rail, along);
        }

        var skirt = Panel(skirtLo, skirtHi, outward, 0.03f);
        var glass = Panel(glassLo, glassHi, outward, 0.018f);
        var glow = Panel(glowLo, glowHi, outward, 0.012f);
        SpawnSide(skirt, glassSides, root, shader);
        SpawnSide(glass, glassSides, root, shader);
        if (glow != null) Spawn(glow, Solid(shader, new Color(0.15f, 0.9f, 0.38f)), root);

        bool full = a == 0 && b == path.Length - 1 && path.Length >= 2;
        if (full)
        {
            var high = Arc(rail[rail.Count - 1], along, false);
            var lowTop = rail[0];
            for (int i = 1; i < high.Count; i++) rail.Add(high[i]);
            for (int c = b; c >= a; c--) rail.Add(ReturnPoint(path, c, outward));
            var low = Arc(lowTop, -along, true);
            for (int i = 1; i < low.Count; i++) rail.Add(low[i]);
            SpawnSide(NewelPlate(high, outward), glassSides, root, shader);
            SpawnSide(NewelPlate(low, outward), glassSides, root, shader);
        }

        var tube = Tube(rail, outward);
        if (tube == null) return;
        if (rubber == null)
        {
            rubber = Solid(shader, Color.white);
            rubber.mainTexture = Groove();
            rubber.mainTextureScale = new Vector2(Repeat, 1f);
            if (rubber.HasProperty("_Cull")) rubber.SetInt("_Cull", 0);
        }
        Spawn(tube, rubber, root);
    }

    static void AddStation(HSEscalatorPath path, int c, Vector3 outward,
        List<Vector3> skirtLo, List<Vector3> skirtHi, List<Vector3> glassLo, List<Vector3> glassHi,
        List<Vector3> glowLo, List<Vector3> glowHi, List<Vector3> rail)
    {
        float x, z;
        Boundary(path, c, outward, out x, out z);
        float tread = path.BeltHeight(c) * 0.5f;
        var edge = new Vector3(x, 0f, z);
        // Panel sits on the wall face. The rail sits just onto the step, where a hand rests.
        var wall = edge + outward * 0.02f;
        var hand = edge - outward * 0.05f;
        // From under the returning steps up to the balustrade, so both step runs are covered.
        skirtLo.Add(new Vector3(wall.x, tread - ReturnDrop - 0.55f, wall.z));
        skirtHi.Add(new Vector3(wall.x, tread + SkirtHigh, wall.z));
        glassLo.Add(new Vector3(wall.x, tread + SkirtHigh - 0.02f, wall.z));
        glassHi.Add(new Vector3(wall.x, tread + AboveTread - 0.03f, wall.z));
        var glowAt = edge - outward * 0.03f;
        glowLo.Add(new Vector3(glowAt.x, tread + 0.02f, glowAt.z));
        glowHi.Add(new Vector3(glowAt.x, tread + 0.07f, glowAt.z));
        rail.Add(new Vector3(hand.x, tread + AboveTread, hand.z));
    }

    static void Stretch(List<Vector3> pts, Vector3 along)
    {
        if (pts.Count != 1) return;
        var p = pts[0];
        pts.Insert(0, p - along * 0.45f);
        pts.Add(p + along * 0.45f);
    }

    static Vector3 ReturnPoint(HSEscalatorPath path, int c, Vector3 outward)
    {
        float x, z;
        Boundary(path, c, outward, out x, out z);
        var hand = new Vector3(x, 0f, z) - outward * 0.05f;
        return new Vector3(hand.x, path.BeltHeight(c) * 0.5f + AboveTread - ReturnDrop, hand.z);
    }

    static List<Vector3> Arc(Vector3 top, Vector3 runOut, bool fromReturn)
    {
        var pts = new List<Vector3>();
        if (runOut.sqrMagnitude < 0.5f) runOut = Vector3.forward;
        runOut.Normalize();
        var hub = top;
        hub.y -= NewelR;
        int n = 12;
        for (int i = 0; i <= n; i++)
        {
            int k = fromReturn ? n - i : i;
            float a = Mathf.PI * k / n;
            pts.Add(hub + runOut * (Mathf.Sin(a) * NewelR) + Vector3.up * (Mathf.Cos(a) * NewelR));
        }
        return pts;
    }

    static Mesh NewelPlate(List<Vector3> arc, Vector3 side)
    {
        if (arc == null || arc.Count < 3) return null;
        var hub = (arc[0] + arc[arc.Count - 1]) * 0.5f;
        // The cheek sits in the balustrade, a few centimetres thick.
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float thick = 0.04f;
        for (int s = -1; s <= 1; s += 2)
        {
            int b = verts.Count;
            verts.Add(hub + side * (thick * s));
            for (int i = 0; i < arc.Count; i++) verts.Add(arc[i] + side * (thick * s));
            for (int i = 0; i < arc.Count - 1; i++)
            {
                if (s > 0)
                {
                    tris.Add(b); tris.Add(b + i + 1); tris.Add(b + i + 2);
                }
                else
                {
                    tris.Add(b); tris.Add(b + i + 2); tris.Add(b + i + 1);
                }
            }
        }
        var mesh = new Mesh { name = "hs_escalator_newel" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void Boundary(HSEscalatorPath path, int col, int side, out float x, out float z)
    {
        int lane = side == 0 ? 0 : Math.Max(0, path.Width - 1);
        int sx, sz;
        path.WorldXZ(col, lane, out sx, out sz);
        if (path.RunAxis == 0)
        {
            x = sx + 0.5f;
            z = side == 0 ? sz : sz + 1f;
        }
        else
        {
            z = sz + 0.5f;
            x = side == 0 ? sx : sx + 1f;
        }
    }

    static void Boundary(HSEscalatorPath path, int col, Vector3 outward, out float x, out float z)
    {
        int side = 0;
        if (path.RunAxis == 0) side = outward.z > 0f ? 1 : 0;
        else side = outward.x > 0f ? 1 : 0;
        Boundary(path, col, side, out x, out z);
    }

    static Kind KindAt(World world, HSEscalatorPath path, int col, int side)
    {
        int x, y, z;
        Cell(path, col, side, out x, out y, out z);
        var best = Kind.None;
        for (int dy = -1; dy <= 2; dy++)
        {
            var k = Look(world, x, y + dy, z);
            if (k == Kind.End || k == Kind.Piece) return k;
            if (k == Kind.Wall) best = Kind.Wall;
        }
        return best;
    }

    public static void SideCell(HSEscalatorPath path, int col, int side, out int x, out int y, out int z)
    {
        Cell(path, col, side, out x, out y, out z);
    }

    public static bool IsSupport(World world, int x, int y, int z)
    {
        var k = Look(world, x, y, z);
        return k == Kind.Wall || k == Kind.End || k == Kind.Piece;
    }

    public static bool HasSide(World world, HSEscalatorPath path, int col, int side)
    {
        return KindAt(world, path, col, side) != Kind.None;
    }

    // Thin box on the skirt and glass. The support cell itself stays open, including under the steps.
    public static bool BalustradeBox(HSEscalatorPath path, int col, int side, out Vector3 center, out Vector3 size)
    {
        center = Vector3.zero;
        size = Vector3.zero;
        if (path == null || col < 0 || col >= path.Length) return false;
        var outward = path.RunAxis == 0
            ? new Vector3(0f, 0f, side == 0 ? -1f : 1f)
            : new Vector3(side == 0 ? -1f : 1f, 0f, 0f);
        float x, z;
        Boundary(path, col, side, out x, out z);
        float tread = path.BeltHeight(col) * 0.5f;
        const float thick = 0.12f;
        float bottom = tread - 0.04f;
        float top = tread + AboveTread + 0.05f;
        center = new Vector3(x, (bottom + top) * 0.5f, z) + outward * (0.02f + thick * 0.5f);
        size = path.RunAxis == 0
            ? new Vector3(1f, top - bottom, thick)
            : new Vector3(thick, top - bottom, 1f);
        return true;
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
        if (name == "hsescalatorHide") return Kind.Wall;
        if (name.StartsWith("hsescalatorEnd", StringComparison.Ordinal)) return Kind.End;
        if (name.StartsWith("hsescalatorSlope", StringComparison.Ordinal)
            || name.StartsWith("hsescalatorFlat", StringComparison.Ordinal))
            return Kind.Piece;
        if (name.StartsWith("hsescalator", StringComparison.Ordinal)) return Kind.None;
        try
        {
            if (bv.Block.shape != null && bv.Block.shape.IsTerrain()) return Kind.None;
        }
        catch { return Kind.None; }
        return Kind.Wall;
    }

    Material Solid(Shader shader, Color color)
    {
        var m = new Material(shader) { hideFlags = HideFlags.DontUnloadUnusedAsset, color = color };
        mats.Add(m);
        return m;
    }

    static readonly Color GlassColor = new Color(0.75f, 0.88f, 0.95f, 0.32f);
    static readonly Color MetalColor = new Color(0.16f, 0.17f, 0.18f);

    void SpawnSide(Mesh mesh, bool glass, Transform root, Shader shader)
    {
        if (mesh == null) return;
        if (glass) SpawnGlass(mesh, GlassColor, root, shader);
        else Spawn(mesh, Solid(shader, MetalColor), root);
    }

    void SpawnGlass(Mesh mesh, Color color, Transform root, Shader fallback)
    {
        Paint(mesh, color);
        Spawn(mesh, ClearMat(fallback, color), root);
    }

    // 3.2 GameManager and 3.3 GameManager both Shader.Find("Unlit/Transparent Colored").
    // That shader tints by vertex colour, so the mesh has to carry the alpha.
    Material ClearMat(Shader fallback, Color color)
    {
        var found = FindShader(new[]
        {
            "Unlit/Transparent Colored",
            "Unlit/Transparent",
            "Legacy Shaders/Transparent/Diffuse",
            "Legacy Shaders/Transparent/VertexLit"
        });
        var m = Solid(found != null ? found : fallback, color);
        if (m.mainTexture == null) m.mainTexture = White();
        if (m.HasProperty("_Cull")) m.SetInt("_Cull", 0);
        m.renderQueue = 3000;
        return m;
    }

    static void Paint(Mesh mesh, Color color)
    {
        if (mesh == null) return;
        var cols = new Color[mesh.vertexCount];
        for (int i = 0; i < cols.Length; i++) cols[i] = color;
        mesh.colors = cols;
    }

    static Texture2D whiteTex;

    static Texture2D White()
    {
        if (whiteTex != null) return whiteTex;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, false)
        {
            name = "hs_escalator_glass",
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        whiteTex = tex;
        return tex;
    }

    void Spawn(Mesh mesh, Material mat, Transform root)
    {
        if (mesh == null) return;
        var go = new GameObject(mesh.name);
        go.transform.SetParent(root, false);
        go.transform.position = -Origin.position;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
        strips.Add(go.transform);
        meshes.Add(mesh);
    }

    static Mesh Panel(List<Vector3> bottom, List<Vector3> top, Vector3 outward, float halfThick)
    {
        if (bottom == null || top == null || bottom.Count < 2 || top.Count != bottom.Count) return null;
        if (outward.sqrMagnitude < 0.5f) outward = Vector3.forward;
        outward.Normalize();
        int n = bottom.Count;
        var verts = new Vector3[n * 4];
        for (int i = 0; i < n; i++)
        {
            var o = outward * halfThick;
            int v = i * 4;
            verts[v] = bottom[i] + o;
            verts[v + 1] = bottom[i] - o;
            verts[v + 2] = top[i] - o;
            verts[v + 3] = top[i] + o;
        }
        int quads = (n - 1) * 4 + 2;
        var tris = new int[quads * 6];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;
            AddQuad(tris, verts, ref t, a, a + 3, b + 3, b, outward);
            AddQuad(tris, verts, ref t, a + 1, b + 1, b + 2, a + 2, -outward);
            AddQuad(tris, verts, ref t, a + 3, a + 2, b + 2, b + 3, Vector3.up);
            AddQuad(tris, verts, ref t, a, b, b + 1, a + 1, Vector3.down);
        }
        AddQuad(tris, verts, ref t, 0, 1, 2, 3, bottom[0] - bottom[1]);
        int e = (n - 1) * 4;
        AddQuad(tris, verts, ref t, e, e + 3, e + 2, e + 1, bottom[n - 1] - bottom[n - 2]);
        var mesh = new Mesh { name = "hs_escalator_panel" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh Deck(List<Vector3> left, List<Vector3> right)
    {
        if (left == null || right == null || left.Count < 2 || right.Count != left.Count) return null;
        int n = left.Count;
        var verts = new Vector3[n * 2];
        for (int i = 0; i < n; i++)
        {
            verts[i * 2] = left[i];
            verts[i * 2 + 1] = right[i];
        }
        var tris = new int[(n - 1) * 12];
        int t = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2;
            int b = (i + 1) * 2;
            AddQuad(tris, verts, ref t, a, a + 1, b + 1, b, Vector3.up);
            AddQuad(tris, verts, ref t, a, b, b + 1, a + 1, Vector3.down);
        }
        var mesh = new Mesh { name = "hs_escalator_under" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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

        const float halfW = 0.04f;
        const float halfH = 0.028f;
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
            var wide = side * halfW;
            var tall = up * halfH;
            int o = i * 4;
            verts[o] = p[i] + wide + tall;
            verts[o + 1] = p[i] - wide + tall;
            verts[o + 2] = p[i] - wide - tall;
            verts[o + 3] = p[i] + wide - tall;
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
        return FindShader(new[]
        {
            "Legacy Shaders/Diffuse",
            "Mobile/Diffuse",
            "Legacy Shaders/VertexLit",
            "Unlit/Texture"
        });
    }

    static Shader FindShader(string[] names)
    {
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
