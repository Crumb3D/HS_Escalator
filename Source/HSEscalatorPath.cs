using System;
using System.Collections.Generic;

// Pure layout math. Heights are tread tops in half-meters (lower half at Y => 2Y+1, upper => 2Y+2).
public class HSEscalatorPath
{
    public int Length;
    public int Width;
    public int RunAxis;
    public int RunSign;
    public int OriginX;
    public int OriginZ;
    public int LaneMinX;
    public int LaneMinZ;
    public int[] Heights;
    public bool IsWalkway;
    public int RiseHalf;
    public int LowFlat;
    public int HighFlat;

    public int SlotCount { get { return 2 * Math.Max(2, Length - 1); } }

    public float LoopLength { get { return 2f * Math.Max(1, Length - 1); } }

    public static string SelfTest()
    {
        var fails = new List<string>();
        Expect(new[] { 10, 10, 10 }, true, true, 0, "flat3", fails);
        Expect(new[] { 10, 10 }, false, false, 0, "flat2", fails);
        Expect(new[] { 10, 10, 11, 12, 13, 14, 14, 14 }, true, false, 4, "rise2", fails);
        Expect(new[] { 10, 10, 12, 14, 14, 14 }, false, false, 0, "skip", fails);
        Expect(new[] { 10, 11, 12, 13, 14 }, false, false, 0, "oneFlat", fails);
        Expect(new[] { 10, 10, 11, 12, 13, 14, 14 }, false, false, 0, "shortRise2", fails);
        Expect(new[] { 10, 10, 11, 11, 11 }, true, false, 1, "riseHalf", fails);
        Expect(new[] { 10, 10, 10, 11, 12, 13, 14, 14, 14 }, true, false, 4, "extraFlats", fails);
        Expect(new[] { 20, 20, 19, 18, 17, 16, 16, 16 }, true, false, 4, "downhill", fails);
        if (fails.Count == 0) return "path tests ok";
        return "path tests failed: " + string.Join("; ", fails.ToArray());
    }

    static void Expect(int[] heights, bool ok, bool walkway, int riseHalf, string name, List<string> fails)
    {
        string err;
        var p = TryFromHeights(heights, out err);
        if (ok)
        {
            if (p == null) { fails.Add(name + " rejected: " + err); return; }
            if (p.IsWalkway != walkway) fails.Add(name + " walkway=" + p.IsWalkway);
            if (p.RiseHalf != riseHalf) fails.Add(name + " rise=" + p.RiseHalf);
        }
        else if (p != null) fails.Add(name + " should have failed");
    }

    public static HSEscalatorPath TryFromHeights(int[] heights, out string error)
    {
        error = null;
        if (heights == null || heights.Length < 3)
        {
            error = "Need 3 steps in a line.";
            return null;
        }
        int h = heights.Length;
        int first = heights[0];
        int last = heights[h - 1];
        bool allSame = true;
        for (int i = 1; i < h; i++)
            if (heights[i] != first) { allSame = false; break; }

        var path = new HSEscalatorPath
        {
            Length = h,
            Heights = (int[])heights.Clone(),
            IsWalkway = allSame,
            RiseHalf = Math.Abs(last - first)
        };

        if (allSame)
        {
            path.LowFlat = h;
            path.HighFlat = h;
            return path;
        }

        int dir = last > first ? 1 : -1;
        int iLead = 0;
        while (iLead < h && heights[iLead] == first) iLead++;
        int iTrail = 0;
        while (iTrail < h && heights[h - 1 - iTrail] == last) iTrail++;
        if (iLead < 2)
        {
            error = "Need 2 flat steps at the low end.";
            return null;
        }
        if (iTrail < 2)
        {
            error = "Need 2 flat steps at the high end.";
            return null;
        }
        int expected = first + dir;
        int firstLanding = h - iTrail;
        for (int i = iLead; i <= firstLanding; i++)
        {
            if (i == firstLanding)
            {
                if (heights[i] != last || expected != last)
                {
                    error = "Rise does not meet the far end.";
                    return null;
                }
                break;
            }
            if (heights[i] != expected)
            {
                error = "Step " + (i + 1) + " skips a half-step. Fix that column.";
                return null;
            }
            expected += dir;
        }
        int need = 4 + path.RiseHalf;
        if (h < need)
        {
            int add = need - h;
            error = "Up " + path.RiseHalf + ". Need " + need + " long, have " + h + ".";
            return null;
        }
        path.LowFlat = dir > 0 ? iLead : iTrail;
        path.HighFlat = dir > 0 ? iTrail : iLead;
        return path;
    }

    public static int BlockYFromHeight(int height)
    {
        return (height - 1) / 2;
    }

    public static bool HeightIsUpper(int height)
    {
        return (height % 2) == 0;
    }

    public static float TreadTop(int height)
    {
        return height * 0.5f;
    }

    public void WorldXZ(int col, int lane, out int x, out int z)
    {
        col = ClampCol(col);
        if (lane < 0) lane = 0;
        if (lane >= Math.Max(1, Width)) lane = Math.Max(0, Width - 1);
        if (RunAxis == 0)
        {
            x = OriginX + col * RunSign;
            z = LaneMinZ + lane;
        }
        else
        {
            x = LaneMinX + lane;
            z = OriginZ + col * RunSign;
        }
    }

    public void WorldXZ(float col, int lane, out float x, out float z)
    {
        int c0 = (int)Math.Floor(col);
        int c1 = c0 + 1;
        float f = col - c0;
        int x0, z0, x1, z1;
        WorldXZ(c0, lane, out x0, out z0);
        if (c1 >= Length)
        {
            x = x0;
            z = z0;
            return;
        }
        WorldXZ(c1, lane, out x1, out z1);
        x = x0 + (x1 - x0) * f;
        z = z0 + (z1 - z0) * f;
    }

    public float HeightAt(float col)
    {
        if (Heights == null || Heights.Length == 0) return 0f;
        if (col <= 0f) return Heights[0];
        if (col >= Length - 1) return Heights[Length - 1];
        int c0 = (int)Math.Floor(col);
        float f = col - c0;
        return Heights[c0] + (Heights[c0 + 1] - Heights[c0]) * f;
    }

    // s is loop parameter in [0, LoopLength). foldDeg 0 = tread up, 180 = return (tread down).
    public void SlotPose(float s, out float col, out float treadTop, out float foldDeg, out bool onReturn)
    {
        float L = LoopLength;
        if (L < 2f) L = 2f;
        s = Wrap(s, L);
        float topEnd = Length - 1f;
        const float hinge = 0.4f;

        if (s < topEnd)
        {
            col = s;
            onReturn = false;
            foldDeg = 0f;
            if (s > topEnd - hinge)
                foldDeg = 180f * ((s - (topEnd - hinge)) / hinge);
        }
        else
        {
            col = L - s;
            onReturn = true;
            foldDeg = 180f;
            if (s > L - hinge)
                foldDeg = 180f - 180f * ((s - (L - hinge)) / hinge);
        }
        if (col < 0f) col = 0f;
        if (col > Length - 1) col = Length - 1;
        float top = HeightAt(col) * 0.5f;
        if (foldDeg <= 0.5f)
        {
            treadTop = top;
            onReturn = false;
        }
        else if (foldDeg >= 179.5f)
        {
            treadTop = top - 1f;
            onReturn = true;
        }
        else
        {
            float mid = top - 0.5f;
            float rad = foldDeg * (float)Math.PI / 180f;
            treadTop = mid + 0.25f * (float)Math.Cos(rad) + 0.25f;
            onReturn = foldDeg >= 90f;
        }
    }

    public bool InHinge(float foldDeg)
    {
        return foldDeg > 20f && foldDeg < 160f;
    }

    public void OutsideLanding(bool lowEnd, out int x, out int z)
    {
        int col = lowEnd ? 0 : Length - 1;
        int stepX, stepZ;
        WorldXZ(col, 0, out stepX, out stepZ);
        int back = lowEnd ? -RunSign : RunSign;
        if (RunAxis == 0)
        {
            x = stepX + back;
            z = stepZ;
        }
        else
        {
            x = stepX;
            z = stepZ + back;
        }
    }

    int ClampCol(int col)
    {
        if (col < 0) return 0;
        if (col >= Length) return Length - 1;
        return col;
    }

    static float Wrap(float s, float L)
    {
        if (L <= 0f) return 0f;
        s %= L;
        if (s < 0f) s += L;
        return s;
    }
}
