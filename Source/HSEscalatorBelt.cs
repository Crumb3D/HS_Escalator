using System;
using System.Collections.Generic;
using UnityEngine;

public class HSEscalatorSlotPose
{
    public float Col;
    public float TreadTop;
    public float FoldDeg;
    public float FoldSpin;
    public bool OnReturn;
    public Vector3 Center;
}

public class HSEscalatorBelt
{
    public GameObject Root;
    readonly List<Transform> slots = new List<Transform>();
    readonly List<Collider[]> slotCols = new List<Collider[]>();
    readonly List<Vector3> lastCenter = new List<Vector3>();
    readonly List<Vector3> lastDelta = new List<Vector3>();
    readonly List<Transform> combs = new List<Transform>();
    readonly List<Vector3> combWorld = new List<Vector3>();
    HSEscalatorConfigData bound;
    HSEscalatorPath path;

    public void Bind(HSEscalatorConfigData d)
    {
        bound = d;
        path = d != null ? d.ToPath() : null;
    }

    public bool IsBuilt { get { return Root != null; } }

    public void Rebuild(World world)
    {
        Destroy();
        if (bound == null || path == null || bound.Steps == null || bound.Steps.Count == 0) return;
        if (GameManager.IsDedicatedServer)
        {
            Root = new GameObject("HSEscalator_" + bound.EscalatorId);
            var erb = Root.AddComponent<Rigidbody>();
            erb.isKinematic = true;
            erb.useGravity = false;
            BuildCombPlates(null);
            Apply(bound.Phase, 0f);
            return;
        }
        Root = new GameObject("HSEscalator_" + bound.EscalatorId);
        var rb = Root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var pin = Root.AddComponent<HSEscalatorPinnedLooks>();
        int n = path.SlotCount;
        int layer = 16;
        int proto = path.PrototypeCol();
        for (int i = 0; i < n; i++)
        {
            var slot = new GameObject("slot" + i);
            slot.transform.SetParent(Root.transform, false);
            BuildSlot(world, slot.transform, proto, pin, ref layer);
            slots.Add(slot.transform);
            slotCols.Add(slot.GetComponentsInChildren<Collider>(true));
            lastCenter.Add(Vector3.zero);
            lastDelta.Add(Vector3.zero);
        }
        BuildCombPlates(pin);
        Apply(bound.Phase, 0f);
        HSEscalatorDebug.Verbose("Belt " + bound.EscalatorId + ": " + n + " slots from " + bound.Steps.Count + " cells");
    }

    void BuildSlot(World world, Transform slot, int srcCol, HSEscalatorPinnedLooks pin, ref int layer)
    {
        foreach (var cell in bound.Steps)
        {
            if (cell.Col != srcCol) continue;
            var bv = HSEscalatorWorld.BlockOf(cell);
            var holder = new GameObject("lane" + cell.Lane);
            holder.transform.SetParent(slot, false);
            holder.transform.localPosition = LaneLocal(cell.Lane);
            try
            {
                var ic = ItemClass.GetForId(bv.ToItemType());
                if (ic != null)
                {
                    int x, z;
                    path.WorldXZ(srcCol, cell.Lane, out x, out z);
                    int y = HSEscalatorPath.BlockYFromHeight(path.Heights[srcCol]);
                    var worldPos = new Vector3(x, y, z);
                    var model = ic.CloneModel(world, bv.ToItemValue(), worldPos, holder.transform, _textureFullArray: HSEscalatorWorld.FromLongs(cell.Tex));
                    if (model != null)
                    {
                        model.localPosition = new Vector3(0f, 0.25f, 0f);
                        model.localRotation = bv.Block.shape.GetRotation(bv);
                        foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                        foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                        if (!HSEscalatorPaint.Apply(model, pin)) pin.Keep(model);
                    }
                }
            }
            catch (Exception e)
            {
                HSEscalatorDebug.Warn("Step model failed: " + e.Message);
            }

            if (bv.Block == null || !bv.Block.IsCollideMovement) continue;
            Bounds[] bounds = null;
            try { bounds = bv.Block.shape.GetBounds(bv); } catch { }
            if (bounds == null || bounds.Length == 0) bounds = new[] { new Bounds(new Vector3(0.5f, 0.25f, 0.5f), new Vector3(1f, 0.5f, 1f)) };
            foreach (var b in bounds)
            {
                if (b.size.x < 0.001f || b.size.y < 0.001f || b.size.z < 0.001f) continue;
                var bgo = new GameObject("col");
                bgo.transform.SetParent(holder.transform, false);
                bgo.transform.localPosition = b.center;
                bgo.layer = layer;
                bgo.AddComponent<BoxCollider>().size = b.size;
            }
        }
    }

    const float CombThick = 0.05f;

    // A fixed plate over the first and last column: riders step on and off it while the
    // treads fold underneath, so the hinge never leaves a hole to fall into.
    void BuildCombPlates(HSEscalatorPinnedLooks pin)
    {
        int last = path.Length - 1;
        int lanes = Math.Max(1, path.Width);
        var cols = last > 0 ? new[] { 0, last } : new[] { 0 };
        foreach (int c in cols)
        {
            int x0, z0, x1, z1;
            path.WorldXZ(c, 0, out x0, out z0);
            path.WorldXZ(c, lanes - 1, out x1, out z1);
            float top = HSEscalatorPath.TreadTop(path.Heights[c]);
            var world = new Vector3(
                (Math.Min(x0, x1) + Math.Max(x0, x1) + 1) * 0.5f,
                top + CombThick * 0.5f,
                (Math.Min(z0, z1) + Math.Max(z0, z1) + 1) * 0.5f);
            var size = path.RunAxis == 0
                ? new Vector3(1f, CombThick, lanes)
                : new Vector3(lanes, CombThick, 1f);

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = c == 0 ? "comb_low" : "comb_high";
            plate.layer = 16;
            plate.transform.SetParent(Root.transform, false);
            plate.transform.localScale = size;
            var r = plate.GetComponent<MeshRenderer>();
            var grate = pin != null ? HSEscalatorPaint.GrateMaterial() : null;
            if (r != null && grate != null)
            {
                var m = new Material(grate) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                m.mainTextureScale = path.RunAxis == 0 ? new Vector2(1f, lanes) : new Vector2(lanes, 1f);
                r.sharedMaterial = m;
                pin.Own(m);
            }
            else if (r != null && pin == null)
                r.enabled = false;
            combs.Add(plate.transform);
            combWorld.Add(world);
        }
    }

    public static bool OnCombPlate(HSEscalatorPath path, Vector3 feet)
    {
        if (path == null) return false;
        float x = feet.x, z = feet.z;
        int lanes = Math.Max(1, path.Width);
        for (int end = 0; end < 2; end++)
        {
            int c = end == 0 ? 0 : path.Length - 1;
            int x0, z0, x1, z1;
            path.WorldXZ(c, 0, out x0, out z0);
            path.WorldXZ(c, lanes - 1, out x1, out z1);
            if (x < Math.Min(x0, x1) || x > Math.Max(x0, x1) + 1 || z < Math.Min(z0, z1) || z > Math.Max(z0, z1) + 1) continue;
            float top = HSEscalatorPath.TreadTop(path.Heights[c]) + CombThick;
            if (feet.y > top - 0.2f && feet.y < top + 1.2f) return true;
        }
        return false;
    }

    Vector3 LaneLocal(int lane)
    {
        if (path.RunAxis == 0) return new Vector3(0f, 0f, lane);
        return new Vector3(lane, 0f, 0f);
    }

    public void Apply(float phase, float unusedDt)
    {
        if (path == null) return;
        if (Root == null && slots.Count == 0) return;
        for (int i = 0; i < combs.Count && i < combWorld.Count; i++)
            if (combs[i] != null) combs[i].position = combWorld[i] - Origin.position;
        int n = path.SlotCount;
        for (int i = 0; i < slots.Count && i < n; i++)
        {
            float s = phase + i;
            HSEscalatorSlotPose pose;
            Eval(s, 0, out pose);
            var t = slots[i];
            t.position = pose.Center;
            t.rotation = FoldRotation(pose.FoldSpin);
            bool solid = !pose.OnReturn && pose.FoldDeg < 20f;
            if (i < slotCols.Count)
            {
                var cols = slotCols[i];
                if (cols != null)
                    for (int c = 0; c < cols.Length; c++)
                        if (cols[c] != null && cols[c].enabled != solid) cols[c].enabled = solid;
            }
            if (i < lastCenter.Count)
            {
                var was = lastCenter[i];
                lastDelta[i] = was.sqrMagnitude > 0.0001f ? pose.Center - was : Vector3.zero;
                lastCenter[i] = pose.Center;
            }
        }
    }

    public bool Eval(float s, int lane, out HSEscalatorSlotPose pose)
    {
        pose = new HSEscalatorSlotPose();
        if (path == null) return false;
        float col, top, fold, spin;
        bool ret;
        path.SlotPose(s, out col, out top, out fold, out spin, out ret);
        pose.Col = col;
        pose.TreadTop = top;
        pose.FoldDeg = fold;
        pose.FoldSpin = spin;
        pose.OnReturn = ret;
        float x, z;
        path.WorldXZ(col, lane, out x, out z);
        pose.Center = new Vector3(x + 0.5f, top - 0.25f, z + 0.5f) - Origin.position;
        return true;
    }

    Quaternion FoldRotation(float foldSpin)
    {
        if (path == null) return Quaternion.identity;
        float spin = foldSpin % 360f;
        if (spin < 0f) spin += 360f;
        if (spin < 0.5f || spin > 359.5f) return Quaternion.identity;
        // One rotation sense for the whole loop: top dives 0→180, bottom
        // keeps going 180→360 instead of unwinding the other way.
        var travel = path.RunAxis == 0
            ? new Vector3(path.RunSign, 0f, 0f)
            : new Vector3(0f, 0f, path.RunSign);
        var axis = Vector3.Cross(travel, Vector3.up);
        if (axis.sqrMagnitude < 0.0001f) return Quaternion.identity;
        return Quaternion.AngleAxis(-spin, axis.normalized);
    }

    public bool StepUnder(Vector3 worldFeet, out HSEscalatorSlotPose pose, out Vector3 delta)
    {
        pose = null;
        delta = Vector3.zero;
        if (path == null || bound == null) return false;
        int n = path.SlotCount;
        int best = -1;
        float bestD = float.MaxValue;
        HSEscalatorSlotPose bestPose = null;
        for (int i = 0; i < n; i++)
        {
            HSEscalatorSlotPose p;
            if (!Eval(bound.Phase + i, 0, out p)) continue;
            if (p.OnReturn || p.FoldDeg >= 80f) continue;
            var c = p.Center + Origin.position;
            // Feet must be on this tread's surface (or the comb plate just above it), not merely over it.
            float dy = worldFeet.y - p.TreadTop;
            if (dy < -0.3f || dy > 0.35f) continue;
            float across = path.Width * 0.5f + 0.35f;
            float along;
            if (path.RunAxis == 0)
            {
                float midZ = path.LaneMinZ + (path.Width - 1) * 0.5f + 0.5f;
                if (Math.Abs(worldFeet.z - midZ) > across) continue;
                along = worldFeet.x - c.x;
            }
            else
            {
                float midX = path.LaneMinX + (path.Width - 1) * 0.5f + 0.5f;
                if (Math.Abs(worldFeet.x - midX) > across) continue;
                along = worldFeet.z - c.z;
            }
            if (Math.Abs(along) > 0.6f) continue;
            float d2 = along * along + 4f * dy * dy;
            if (d2 < bestD) { bestD = d2; best = i; bestPose = p; }
        }
        if (best < 0) return false;
        pose = bestPose;
        if (best < lastDelta.Count) delta = lastDelta[best];
        return true;
    }

    public void Destroy()
    {
        slots.Clear();
        slotCols.Clear();
        lastCenter.Clear();
        lastDelta.Clear();
        combs.Clear();
        combWorld.Clear();
        if (Root != null) UnityEngine.Object.Destroy(Root);
        Root = null;
    }
}

public class HSEscalatorPinnedLooks : MonoBehaviour
{
    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

    public void Keep(Transform model)
    {
        if (model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;
            var copies = new Material[shared.Length];
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null) continue;
                var m = new Material(shared[i]) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                owned.Add(m);
                copies[i] = m;
            }
            r.materials = copies;
        }
    }

    public void Own(UnityEngine.Object obj)
    {
        if (obj != null) owned.Add(obj);
    }

    void OnDestroy()
    {
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] != null) Destroy(owned[i]);
        owned.Clear();
    }
}
