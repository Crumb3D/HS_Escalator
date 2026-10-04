using System;
using System.Collections.Generic;
using UnityEngine;

public class HSEscalatorHalf
{
    public Vector3i Pos;
    public bool IsUpper;
    public int Height;
    public BlockValue Bv;
    public sbyte Density;
    public TextureFullArray Tex;
}

public static class HSEscalatorWorld
{
    public static int TexChannels
    {
        get { return System.Runtime.InteropServices.Marshal.SizeOf(typeof(TextureFullArray)) / 8; }
    }

    public static bool TryReadHalf(World world, Vector3i pos, out HSEscalatorHalf half)
    {
        half = null;
        if (world == null) return false;
        var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
        if (chunk == null) return false;
        var bv = world.GetBlock(pos);
        if (bv.isair || bv.ischild) return false;
        var block = bv.Block;
        if (block == null || block.shape == null) return false;
        try { if (block.shape.IsTerrain()) return false; } catch { return false; }
        if (block.isOversized) return false;
        if (block.isMultiBlock) return false;

        var name = block.GetBlockName() ?? "";
        string shapeName = "";
        try { shapeName = block.shape.GetName() ?? ""; } catch { }
        if (Forbidden(name) || Forbidden(shapeName)) return false;

        Bounds box;
        if (!UnionBounds(bv, out box))
        {
            if (!IsNamedHalf(shapeName) && !IsNamedHalf(name)) return false;
            box = new Bounds(new Vector3(0.5f, 0.25f, 0.5f), new Vector3(1f, 0.5f, 1f));
        }
        else
        {
            if (box.size.y < 0.38f || box.size.y > 0.62f) return false;
            if (box.size.x < 0.85f || box.size.z < 0.85f) return false;
        }

        bool upper = box.center.y > 0.5f;
        int lx = World.toBlockXZ(pos.x), ly = World.toBlockY(pos.y), lz = World.toBlockXZ(pos.z);
        half = new HSEscalatorHalf
        {
            Pos = pos,
            IsUpper = upper,
            Height = upper ? 2 * pos.y + 2 : 2 * pos.y + 1,
            Bv = bv,
            Density = chunk.GetDensity(lx, ly, lz),
            Tex = chunk.GetTextureFullArray(lx, ly, lz)
        };
        return true;
    }

    static bool UnionBounds(BlockValue bv, out Bounds box)
    {
        box = new Bounds();
        try
        {
            var bounds = bv.Block.shape.GetBounds(bv);
            if (bounds == null || bounds.Length == 0) return false;
            box = bounds[0];
            for (int i = 1; i < bounds.Length; i++) box.Encapsulate(bounds[i]);
            return box.size.x > 0.01f && box.size.y > 0.01f && box.size.z > 0.01f;
        }
        catch
        {
            return false;
        }
    }

    static bool IsNamedHalf(string n)
    {
        var c = Compact(n);
        return c.Equals("half", StringComparison.OrdinalIgnoreCase)
            || c.Equals("halfblock", StringComparison.OrdinalIgnoreCase);
    }

    static bool Forbidden(string n)
    {
        var c = Compact(n);
        if (c.Length == 0) return false;
        return Contains(c, "wedge") || Contains(c, "ramp") || Contains(c, "plate")
            || Contains(c, "sheet") || Contains(c, "stair") || Contains(c, "ladder")
            || Contains(c, "door") || Contains(c, "pole") || Contains(c, "pyramid")
            || Contains(c, "quarter") || Contains(c, "eighth") || Contains(c, "tip")
            || Contains(c, "billboard") || Contains(c, "terrain");
    }

    static bool Contains(string compact, string token)
    {
        return compact.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string Compact(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var chars = new char[s.Length];
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == ' ' || c == '-' || c == '_' || c == ':') continue;
            chars[n++] = c;
        }
        return new string(chars, 0, n);
    }

    public static string DisplayName(BlockValue bv)
    {
        try
        {
            var n = bv.Block.GetLocalizedBlockName();
            if (!string.IsNullOrEmpty(n)) return n;
        }
        catch { }
        return bv.Block != null ? bv.Block.GetBlockName() : "block";
    }

    public static bool IsSolid(World world, Vector3i pos)
    {
        if (world == null || world.GetChunkFromWorldPos(pos) == null) return false;
        var bv = world.GetBlock(pos);
        if (bv.isair) return false;
        try { if (bv.Block != null && bv.Block.shape != null && bv.Block.shape.IsTerrain()) return true; } catch { }
        return bv.Block != null && bv.Block.IsCollideMovement;
    }

    public static string ScanDeck(World world, HSEscalatorConfigData d, out HSEscalatorPath path)
    {
        path = null;
        if (world == null) return "no world";
        if (d == null || d.End1 == null || d.End2 == null) return "Set End 1 and End 2 on the steps.";
        int x1 = d.End1[0], y1 = d.End1[1], z1 = d.End1[2];
        int x2 = d.End2[0], y2 = d.End2[1], z2 = d.End2[2];
        int dx = x2 - x1, dz = z2 - z1;
        if (dx == 0 && dz == 0) return "Ends are the same block. Mark opposite corners of the step deck.";

        int spanX = Math.Abs(dx) + 1;
        int spanZ = Math.Abs(dz) + 1;
        int minY = Math.Min(y1, y2);
        int maxY = Math.Max(y1, y2);

        // Sample a few columns to see which axis the height changes along.
        int runAxis;
        if (spanX == 1) runAxis = 2;
        else if (spanZ == 1) runAxis = 0;
        else
        {
            int changeX = HeightChangeAlong(world, x1, x2, z1, minY, maxY, 0);
            int changeZ = HeightChangeAlong(world, z1, z2, x1, minY, maxY, 2);
            if (changeX > 0 && changeZ == 0) runAxis = 0;
            else if (changeZ > 0 && changeX == 0) runAxis = 2;
            else runAxis = spanX >= spanZ ? 0 : 2;
        }

        int runSign, length, width, originX, originZ, laneMinX, laneMinZ;
        if (runAxis == 0)
        {
            runSign = dx >= 0 ? 1 : -1;
            length = spanX;
            width = spanZ;
            originX = x1;
            originZ = 0;
            laneMinX = 0;
            laneMinZ = Math.Min(z1, z2);
        }
        else
        {
            runSign = dz >= 0 ? 1 : -1;
            length = spanZ;
            width = spanX;
            originX = 0;
            originZ = z1;
            laneMinX = Math.Min(x1, x2);
            laneMinZ = 0;
        }
        if (length < 3) return "Need at least 3 steps along the run. This span is " + length + ".";
        if (width < 1 || width > 8) return "Width must be 1 to 8. This span is " + width + ".";
        if (length * width > 256) return "Deck is too big (" + length + "x" + width + "). Limit is 256 step cells.";

        var probe = new HSEscalatorPath
        {
            Length = length,
            Width = width,
            RunAxis = runAxis,
            RunSign = runSign,
            OriginX = originX,
            OriginZ = originZ,
            LaneMinX = laneMinX,
            LaneMinZ = laneMinZ
        };

        var heights = new int[length];
        var found = new HSEscalatorHalf[length, width];
        for (int c = 0; c < length; c++)
        {
            int colHeight = int.MinValue;
            for (int w = 0; w < width; w++)
            {
                int x, z;
                probe.WorldXZ(c, w, out x, out z);
                HSEscalatorHalf half;
                string why = FindStep(world, x, z, minY, maxY, out half);
                if (why != null) return why;
                if (colHeight == int.MinValue) colHeight = half.Height;
                else if (half.Height != colHeight)
                    return "Tread not level at " + x + " " + half.Pos.y + " " + z + ".";
                found[c, w] = half;
            }
            heights[c] = colHeight;
        }

        string layoutErr;
        path = HSEscalatorPath.TryFromHeights(heights, out layoutErr);
        if (path == null) return layoutErr;
        path.Width = width;
        path.RunAxis = runAxis;
        path.RunSign = runSign;
        path.OriginX = originX;
        path.OriginZ = originZ;
        path.LaneMinX = laneMinX;
        path.LaneMinZ = laneMinZ;

        for (int c = 0; c < length; c++)
        for (int w = 0; w < width; w++)
        {
            var half = found[c, w];
            var under = new Vector3i(half.Pos.x, half.Pos.y - 1, half.Pos.z);
            if (world.GetChunkFromWorldPos(under) == null)
                return "Chunk not loaded under the step at " + half.Pos + ".";
            if (IsSolid(world, under))
                return "Dig out " + DisplayName(world.GetBlock(under)) + " under the step (" + under.x + " " + under.y + " " + under.z + ").";
            for (int up = 1; up <= 2; up++)
            {
                var air = new Vector3i(half.Pos.x, half.Pos.y + up, half.Pos.z);
                if (world.GetChunkFromWorldPos(air) == null)
                    return "Chunk not loaded above the step at " + air + ".";
                if (IsSolid(world, air))
                    return DisplayName(world.GetBlock(air)) + " is above the step at " + air + ".";
            }
        }
        return null;
    }

    static int HeightChangeAlong(World world, int a1, int a2, int other, int minY, int maxY, int axis)
    {
        int seen = int.MinValue;
        int changes = 0;
        int step = a2 >= a1 ? 1 : -1;
        for (int a = a1; a != a2 + step; a += step)
        {
            int x = axis == 0 ? a : other;
            int z = axis == 0 ? other : a;
            HSEscalatorHalf half;
            if (FindStep(world, x, z, minY, maxY, out half) != null) continue;
            if (seen != int.MinValue && half.Height != seen) changes++;
            seen = half.Height;
        }
        return changes;
    }

    static string FindStep(World world, int x, int z, int minY, int maxY, out HSEscalatorHalf half)
    {
        half = null;
        HSEscalatorHalf extra = null;
        int lo = minY - 1, hi = maxY + 1;
        for (int y = lo; y <= hi; y++)
        {
            var pos = new Vector3i(x, y, z);
            if (world.GetChunkFromWorldPos(pos) == null)
                return "Chunk not loaded at " + pos + ".";
            HSEscalatorHalf found;
            if (!TryReadHalf(world, pos, out found))
            {
                var bv = world.GetBlock(pos);
                if (!bv.isair && bv.Block != null && !bv.ischild && y >= minY && y <= maxY && bv.Block.IsCollideMovement)
                {
                    bool terrain = false;
                    try { terrain = bv.Block.shape != null && bv.Block.shape.IsTerrain(); } catch { }
                    if (!terrain)
                        return DisplayName(bv) + " at " + pos + " is not a half-block.";
                }
                continue;
            }
            if (half == null) half = found;
            else extra = found;
        }
        if (half == null)
            return "No half-block at " + x + " " + z + ".";
        if (extra != null)
            return "Two half-blocks at " + x + " " + z + ". Only the step.";
        return null;
    }

    public static string Capture(World world, HSEscalatorConfigData d, HSEscalatorPath path)
    {
        if (world == null || d == null || path == null) return "nothing to capture";
        var cells = new List<HSEscalatorStepCell>();
        var changes = new List<BlockChangeInfo>();
        for (int c = 0; c < path.Length; c++)
        for (int w = 0; w < path.Width; w++)
        {
            int x, z;
            path.WorldXZ(c, w, out x, out z);
            int y = HSEscalatorPath.BlockYFromHeight(path.Heights[c]);
            var pos = new Vector3i(x, y, z);
            HSEscalatorHalf half;
            if (!TryReadHalf(world, pos, out half))
                return "Step missing at " + pos + " while capturing.";
            cells.Add(new HSEscalatorStepCell
            {
                Col = c,
                Lane = w,
                Raw = half.Bv.rawData,
                Damage = half.Bv.damage,
                Density = half.Density,
                Tex = ToLongs(half.Tex)
            });
            changes.Add(new BlockChangeInfo(pos, BlockValue.Air, MarchingCubes.DensityAir));
        }
        world.SetBlocksRPC(changes);
        d.Steps = cells;
        d.HasDeck = true;
        d.Captured = true;
        d.Length = path.Length;
        d.Width = path.Width;
        d.RunAxis = path.RunAxis;
        d.RunSign = path.RunSign;
        d.OriginX = path.OriginX;
        d.OriginZ = path.OriginZ;
        d.LaneMinX = path.LaneMinX;
        d.LaneMinZ = path.LaneMinZ;
        d.Heights = (int[])path.Heights.Clone();
        d.IsWalkway = path.IsWalkway;
        d.RiseHalf = path.RiseHalf;
        d.Phase = 0f;
        d.StopReason = null;
        HSEscalatorDebug.Info("Captured " + cells.Count + " step(s) for " + d.EscalatorId);
        return null;
    }

    public static void Restore(World world, HSEscalatorConfigData d)
    {
        if (world == null || d == null || !d.Captured || d.Steps == null || d.Steps.Count == 0) return;
        var path = d.ToPath();
        if (path == null) return;
        var changes = new List<BlockChangeInfo>();
        foreach (var s in d.Steps)
        {
            int x, z;
            path.WorldXZ(s.Col, s.Lane, out x, out z);
            int y = HSEscalatorPath.BlockYFromHeight(path.Heights[Math.Min(s.Col, path.Heights.Length - 1)]);
            var pos = new Vector3i(x, y, z);
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            if (!world.GetBlock(pos).isair) continue;
            var bv = new BlockValue(s.Raw, s.Damage);
            changes.Add(new BlockChangeInfo(pos, bv, s.Density, FromLongs(s.Tex)));
        }
        if (changes.Count > 0) world.SetBlocksRPC(changes);
        d.Captured = false;
        HSEscalatorDebug.Info("Restored " + changes.Count + " step(s) for " + d.EscalatorId);
    }

    public static void EnsureCapturedRemoved(World world, HSEscalatorConfigData d)
    {
        if (world == null || d == null || !d.Captured || d.Steps == null) return;
        var path = d.ToPath();
        if (path == null) return;
        var changes = new List<BlockChangeInfo>();
        foreach (var s in d.Steps)
        {
            int x, z;
            path.WorldXZ(s.Col, s.Lane, out x, out z);
            int y = HSEscalatorPath.BlockYFromHeight(path.Heights[Math.Min(s.Col, path.Heights.Length - 1)]);
            var pos = new Vector3i(x, y, z);
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair) continue;
            if (bv.rawData == s.Raw) changes.Add(new BlockChangeInfo(pos, BlockValue.Air, MarchingCubes.DensityAir));
        }
        if (changes.Count > 0) world.SetBlocksRPC(changes);
    }

    public static string CheckMechanism(World world, HSEscalatorConfigData d)
    {
        if (world == null || d == null || !d.HasDeck) return null;
        var path = d.ToPath();
        if (path == null) return null;
        for (int c = 0; c < path.Length; c++)
        for (int w = 0; w < path.Width; w++)
        {
            int x, z;
            path.WorldXZ(c, w, out x, out z);
            int y = HSEscalatorPath.BlockYFromHeight(path.Heights[c]);
            var step = new Vector3i(x, y, z);
            if (world.GetChunkFromWorldPos(step) == null) return "part of the escalator is too far away to load (" + step + ")";
            if (d.Captured)
            {
                var fill = world.GetBlock(step);
                if (!fill.isair && fill.Block != null && fill.Block.IsCollideMovement)
                    return DisplayName(fill) + " is in a step cell (" + step + ")";
            }
            var under = new Vector3i(x, y - 1, z);
            if (IsSolid(world, under))
                return DisplayName(world.GetBlock(under)) + " is in the return (" + under + ")";
            for (int up = 1; up <= 2; up++)
            {
                var air = new Vector3i(x, y + up, z);
                if (IsSolid(world, air))
                    return DisplayName(world.GetBlock(air)) + " is in the headroom (" + air + ")";
            }
        }
        return null;
    }

    public static bool LandingWalkable(World world, HSEscalatorConfigData d, bool lowEnd, out string problem)
    {
        problem = null;
        var path = d != null ? d.ToPath() : null;
        if (world == null || path == null) { problem = "no escalator"; return false; }
        int x, z;
        path.OutsideLanding(lowEnd, out x, out z);
        int col = lowEnd ? 0 : path.Length - 1;
        float want = HSEscalatorPath.TreadTop(path.Heights[col]);
        int y0 = HSEscalatorPath.BlockYFromHeight(path.Heights[col]);
        bool found = false;
        for (int y = y0 - 1; y <= y0; y++)
        {
            var pos = new Vector3i(x, y, z);
            if (world.GetChunkFromWorldPos(pos) == null) continue;
            var bv = world.GetBlock(pos);
            if (bv.isair) continue;
            Bounds box;
            float top = y + 1f;
            if (UnionBounds(bv, out box)) top = y + box.max.y;
            if (Math.Abs(top - want) <= 0.55f) found = true;
            if (bv.Block != null && bv.Block.IsCollideMovement && box.size.y > 0.7f && y == y0)
            {
                problem = DisplayName(bv) + " blocks the exit at " + pos;
                return false;
            }
        }
        if (!found)
        {
            problem = "no landing floor flush with the " + (lowEnd ? "low" : "high") + " end at " + x + " " + y0 + " " + z;
            return false;
        }
        return true;
    }

    public static long[] ToLongs(TextureFullArray tex)
    {
        int n = TexChannels;
        var a = new long[n];
        for (int i = 0; i < n; i++) a[i] = tex[i];
        return a;
    }

    public static TextureFullArray FromLongs(long[] a)
    {
        var tex = TextureFullArray.Default;
        if (a == null) return tex;
        for (int i = 0; i < a.Length && i < TexChannels; i++) tex[i] = a[i];
        return tex;
    }

    public static BlockValue BlockOf(HSEscalatorStepCell s)
    {
        return new BlockValue(s.Raw, s.Damage);
    }
}
