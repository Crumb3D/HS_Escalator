using System;
using System.Collections.Generic;
using UnityEngine;

public class HSEscalatorSlotPose
{
    public float Col;
    public float TreadTop;
    public float FoldDeg;
    public bool OnReturn;
    public Vector3 Center;
}

public class HSEscalatorBelt
{
    public GameObject Root;
    readonly List<Transform> slots = new List<Transform>();
    readonly List<Vector3> lastCenter = new List<Vector3>();
    readonly List<Vector3> lastDelta = new List<Vector3>();
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
        for (int i = 0; i < n; i++)
        {
            var slot = new GameObject("slot" + i);
            slot.transform.SetParent(Root.transform, false);
            int srcCol = i % path.Length;
            BuildSlot(world, slot.transform, srcCol, pin, ref layer);
            slots.Add(slot.transform);
            lastCenter.Add(Vector3.zero);
            lastDelta.Add(Vector3.zero);
        }
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
                        model.localPosition = Vector3.zero;
                        model.localRotation = bv.Block.shape.GetRotation(bv);
                        foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                        foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                        pin.Keep(model);
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

    Vector3 LaneLocal(int lane)
    {
        if (path.RunAxis == 0) return new Vector3(0f, 0f, lane);
        return new Vector3(lane, 0f, 0f);
    }

    public void Apply(float phase, float unusedDt)
    {
        if (path == null) return;
        if (Root == null && slots.Count == 0) return;
        int n = path.SlotCount;
        for (int i = 0; i < slots.Count && i < n; i++)
        {
            float s = phase + i;
            HSEscalatorSlotPose pose;
            Eval(s, 0, out pose);
            var t = slots[i];
            t.position = pose.Center;
            t.rotation = FoldRotation(pose.FoldDeg);
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
        float col, top, fold;
        bool ret;
        path.SlotPose(s, out col, out top, out fold, out ret);
        pose.Col = col;
        pose.TreadTop = top;
        pose.FoldDeg = fold;
        pose.OnReturn = ret;
        float x, z;
        path.WorldXZ(col, lane, out x, out z);
        pose.Center = new Vector3(x + 0.5f, top - 0.25f, z + 0.5f) - Origin.position;
        return true;
    }

    Quaternion FoldRotation(float foldDeg)
    {
        if (path == null || foldDeg < 0.5f) return Quaternion.identity;
        var axis = path.RunAxis == 0 ? Vector3.forward : Vector3.right;
        if (path.RunSign < 0) foldDeg = -foldDeg;
        return Quaternion.AngleAxis(foldDeg, axis);
    }

    public bool StepUnder(Vector3 worldFeet, out HSEscalatorSlotPose pose, out Vector3 delta)
    {
        pose = null;
        delta = Vector3.zero;
        if (path == null || bound == null) return false;
        int n = path.SlotCount;
        int best = -1;
        float bestD = 0.65f * 0.65f;
        HSEscalatorSlotPose bestPose = null;
        for (int i = 0; i < n; i++)
        {
            HSEscalatorSlotPose p;
            if (!Eval(bound.Phase + i, 0, out p)) continue;
            if (p.OnReturn || p.FoldDeg > 25f) continue;
            var c = p.Center + Origin.position;
            float dx = worldFeet.x - c.x;
            float dz = worldFeet.z - c.z;
            float dy = worldFeet.y - p.TreadTop;
            if (dy < -0.35f || dy > 1.4f) continue;
            float across = path.Width * 0.5f + 0.35f;
            if (path.RunAxis == 0)
            {
                float midZ = path.LaneMinZ + (path.Width - 1) * 0.5f + 0.5f;
                if (Math.Abs(worldFeet.z - midZ) > across) continue;
            }
            else
            {
                float midX = path.LaneMinX + (path.Width - 1) * 0.5f + 0.5f;
                if (Math.Abs(worldFeet.x - midX) > across) continue;
            }
            float d2 = dx * dx + dz * dz;
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
        lastCenter.Clear();
        lastDelta.Clear();
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

    void OnDestroy()
    {
        for (int i = 0; i < owned.Count; i++)
            if (owned[i] != null) Destroy(owned[i]);
        owned.Clear();
    }
}
