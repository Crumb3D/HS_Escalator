using System;
using System.Collections.Generic;
using UnityEngine;

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

    public int SlotCount { get { return 2 * Math.Max(2, Length); } }

    public float LoopLength { get { return 2f * Math.Max(2, Length); } }

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
        var belt = TryFromHeights(new[] { 10, 10, 11, 12, 13, 14, 14, 14 }, out _);
        if (belt == null) fails.Add("beltHeights rejected");
        else
        {
            if (Math.Abs(belt.BeltHeight(0f) - 10f) > 0.01f) fails.Add("belt low comb");
            if (Math.Abs(belt.BeltHeight(1f) - 10f) > 0.01f) fails.Add("belt low feed step should be level");
            if (Math.Abs(belt.BeltHeight(2f) - 11f) > 0.01f) fails.Add("belt rise starts after feed step");
            if (Math.Abs(belt.BeltHeight(5f) - 14f) > 0.01f) fails.Add("belt meets high");
            if (Math.Abs(belt.BeltHeight(6f) - 14f) > 0.01f) fails.Add("belt high feed step should be level");
            if (Math.Abs(belt.BeltHeight(7f) - 14f) > 0.01f) fails.Add("belt high comb");
            for (int d = 1; d < 180; d += 7)
            {
                float col, top, fold, spin;
                bool ret;
                belt.SlotPose(7f + d / 180f, out col, out top, out fold, out spin, out ret);
                float rad = fold * (float)Math.PI / 180f;
                float reach = 0.5f * (float)Math.Sin(rad) + 0.25f * Math.Abs((float)Math.Cos(rad));
                if (top - 0.25f + reach > 7f + 0.001f) { fails.Add("fold pokes above comb plate at " + d); break; }
            }
        }
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

    public bool TryWorldCol(float x, float z, out float col, out int lane)
    {
        col = 0f;
        lane = 0;
        if (Length < 1 || Width < 1) return false;
        if (RunAxis == 0)
        {
            if (RunSign == 0) return false;
            col = (x - OriginX) / RunSign;
            lane = Mathf.FloorToInt(z - LaneMinZ);
        }
        else
        {
            if (RunSign == 0) return false;
            col = (z - OriginZ) / RunSign;
            lane = Mathf.FloorToInt(x - LaneMinX);
        }
        if (col < -0.35f || col > Length - 0.65f) return false;
        if (lane < 0 || lane >= Width) return false;
        return true;
    }

    // The whole deck, including both end landings. Feet can sit on this column or the step beside it.
    public bool OnRidingSurface(Vector3 feet)
    {
        float col;
        int lane;
        if (!OverDeck(feet.x, feet.z, out col, out lane)) return false;
        int c = (int)Math.Floor(col);
        if (c < 0) c = 0;
        if (c >= Length) c = Length - 1;
        for (int k = c - 1; k <= c + 1; k++)
        {
            if (k < 0 || k >= Length) continue;
            float surface = BeltHeight(k) * 0.5f - 0.02f;
            float dy = feet.y - surface;
            if (dy >= -0.6f && dy <= 0.9f) return true;
        }
        return false;
    }

    bool OverDeck(float x, float z, out float col, out int lane)
    {
        col = 0f;
        lane = 0;
        if (Length < 1 || Width < 1) return false;
        if (RunAxis == 0)
        {
            if (RunSign == 0) return false;
            col = (x - OriginX) / RunSign;
            lane = Mathf.FloorToInt(z - LaneMinZ);
        }
        else
        {
            if (RunSign == 0) return false;
            col = (z - OriginZ) / RunSign;
            lane = Mathf.FloorToInt(x - LaneMinX);
        }
        if (col < -0.2f || col >= Length + 0.15f) return false;
        if (lane < 0 || lane >= Width) return false;
        return true;
    }

    public bool InReturnCavity(Vector3 pos)
    {
        float col;
        int lane;
        if (!TryWorldCol(pos.x, pos.z, out col, out lane)) return false;
        float top = BeltHeight(col) * 0.5f;
        return pos.y < top - 0.2f && pos.y > top - 1.4f;
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

    // The first and last column sit under the comb plates, and the column next to each
    // stays level so treads slide out from under a plate flat. The rise is one half-step
    // per column in between; Length >= 4 + rise leaves room for it.
    public float BeltHeight(float col)
    {
        if (Heights == null || Heights.Length == 0) return 0f;
        if (IsWalkway) return Heights[0];
        float start = Heights[0];
        float end = Heights[Length - 1];
        if (col <= 1f) return start;
        if (col >= Length - 2) return end;
        float risen = start + (end >= start ? col - 1f : 1f - col);
        if (end >= start)
        {
            if (risen > end) return end;
            if (risen < start) return start;
        }
        else
        {
            if (risen < end) return end;
            if (risen > start) return start;
        }
        return risen;
    }

    public int PrototypeCol()
    {
        if (Heights == null || Heights.Length == 0 || IsWalkway) return 0;
        for (int c = 0; c < Heights.Length; c++)
            if (Heights[c] != Heights[0]) return c;
        return 0;
    }

    // s is loop parameter in [0, 2H). One step per column on top and on the return, so the treads stay packed.
    public void SlotPose(float s, out float col, out float treadTop, out float foldDeg, out float foldSpin, out bool onReturn)
    {
        int H = Math.Max(2, Length);
        float last = H - 1f;
        s = Wrap(s, 2f * H);
        foldSpin = 0f;

        if (s < H)
        {
            if (s < last)
            {
                col = s;
                foldSpin = 0f;
                onReturn = false;
            }
            else
            {
                col = last;
                foldSpin = 180f * (s - last);
                onReturn = foldSpin >= 90f;
            }
        }
        else
        {
            float u = s - H;
            if (u <= last)
            {
                col = last - u;
                foldSpin = 180f;
                onReturn = true;
            }
            else
            {
                col = 0f;
                foldSpin = 180f + 180f * (u - last);
                onReturn = foldSpin <= 270f;
            }
        }
        if (col < 0f) col = 0f;
        if (col > last) col = last;
        foldDeg = foldSpin <= 180f ? foldSpin : 360f - foldSpin;
        float top = BeltHeight(col) * 0.5f;
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
            // Centre drops 1 m over the fold, and never so little that a corner rises above the comb.
            float rad = foldDeg * (float)Math.PI / 180f;
            float reach = 0.5f * (float)Math.Sin(rad) + 0.25f * Math.Abs((float)Math.Cos(rad));
            float drop = Math.Max(0.25f + foldDeg / 180f, reach);
            treadTop = top + 0.25f - drop;
            onReturn = foldDeg >= 90f;
        }
    }

    public bool InHinge(float foldDeg)
    {
        return foldDeg > 20f && foldDeg < 160f;
    }

    public void OutsideLanding(bool lowEnd, out int x, out int z)
    {
        OutsideLanding(lowEnd, 0, out x, out z);
    }

    public void OutsideLanding(bool lowEnd, int lane, out int x, out int z)
    {
        int col = lowEnd ? 0 : Length - 1;
        int stepX, stepZ;
        WorldXZ(col, lane, out stepX, out stepZ);
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
