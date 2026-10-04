using System;
using System.Collections.Generic;
using UnityEngine;

public class HSEscalatorController : MonoBehaviour
{
    public HSEscalatorConfigData Bound;
    readonly HSEscalatorBelt belt = new HSEscalatorBelt();
    readonly List<Entity> riders = new List<Entity>();

    static readonly List<HSEscalatorController> all = new List<HSEscalatorController>();

    const float PowerCheckInterval = 0.25f;
    const float ObstructionInterval = 0.4f;
    const float StateInterval = 0.35f;

    float nextPower;
    float nextObstruction;
    float nextState;
    float jogUntil;
    bool beltReady;

    static HSEscalatorConfigData D { get { return HSEscalatorConfig.Data; } }

    public static HSEscalatorController Of(HSEscalatorConfigData d)
    {
        if (d == null) return null;
        return all.Find(c => c != null && c.Bound != null && c.Bound.EscalatorId == d.EscalatorId);
    }

    public static void EnsureCreated()
    {
        foreach (var d in HSEscalatorConfig.Escalators) Ensure(d);
    }

    public static HSEscalatorController Ensure(HSEscalatorConfigData d)
    {
        if (d == null) return null;
        var c = Of(d);
        if (c != null) { c.Bound = d; c.belt.Bind(d); return c; }
        var go = new GameObject("HSEscalator_" + d.EscalatorId + "_ctrl");
        DontDestroyOnLoad(go);
        c = go.AddComponent<HSEscalatorController>();
        c.Bound = d;
        c.belt.Bind(d);
        all.Add(c);
        return c;
    }

    public static void OnWorldShuttingDown()
    {
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        foreach (var c in all)
        {
            if (c == null || c.Bound == null) continue;
            if (HSEscalatorNet.IsAuthority && world != null) HSEscalatorWorld.Restore(world, c.Bound);
            c.belt.Destroy();
        }
        if (HSEscalatorNet.IsAuthority) HSEscalatorConfig.Save();
    }

    void Push()
    {
        if (Bound != null) HSEscalatorConfig.Use(Bound);
    }

    public string StatusText()
    {
        var d = Bound ?? D;
        if (d == null) return "No escalator.";
        var extra = string.IsNullOrEmpty(d.StopReason) ? "" : " stopped: " + d.StopReason;
        return HSEscalatorConfig.Summary(d) + extra + (d.Running ? " phase " + d.Phase.ToString("0.00") : "");
    }

    public string Jog()
    {
        if (Bound == null || !Bound.Captured) return "Capture the step deck first (Set End 1 and Set End 2).";
        Bound.StopReason = null;
        Bound.Running = true;
        jogUntil = Time.unscaledTime + 4f;
        return "Jogging " + Bound.EscalatorId + " for 4 seconds.";
    }

    public string Reverse()
    {
        if (Bound == null) return "No escalator.";
        Bound.Direction = Bound.Direction < 0 ? 1 : -1;
        Bound.StopReason = null;
        HSEscalatorConfig.Save();
        HSEscalatorNet.BroadcastState(Bound);
        return Bound.EscalatorId + " now going " + (Bound.Direction > 0 ? "forward" : "reverse") + ".";
    }

    public static string ReverseDrive(Vector3i pos)
    {
        var d = HSEscalatorConfig.DriveOwner(pos);
        if (d == null) return Localization.Get("hsescalatorUnregistered");
        HSEscalatorConfig.Use(d);
        var c = Ensure(d);
        return c != null ? c.Reverse() : "No controller.";
    }

    public void ApplyRemoteState(float phase, int dir, bool running, string reason)
    {
        if (Bound == null) return;
        Bound.Phase = phase;
        Bound.Direction = dir;
        Bound.Running = running;
        Bound.StopReason = reason;
    }

    public void RebuildBelt()
    {
        beltReady = false;
        belt.Bind(Bound);
        belt.Destroy();
    }

    void LateUpdate()
    {
        try
        {
            Tick();
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Controller tick failed", e);
        }
    }

    void Tick()
    {
        if (Bound == null) return;
        Push();
        belt.Bind(Bound);
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return;

        if (Bound.Captured && Bound.HasDeck)
        {
            if (HSEscalatorNet.IsAuthority && !beltReady)
                HSEscalatorWorld.EnsureCapturedRemoved(world, Bound);
            if (!belt.IsBuilt)
            {
                belt.Rebuild(world);
                beltReady = true;
            }
        }
        else if (belt.IsBuilt)
        {
            belt.Destroy();
            beltReady = false;
        }

        if (!Bound.Captured || !Bound.HasDeck) return;

        if (HSEscalatorNet.IsAuthority)
        {
            if (Time.unscaledTime >= nextPower)
            {
                nextPower = Time.unscaledTime + PowerCheckInterval;
                TickPower();
            }
            if (Time.unscaledTime >= nextObstruction)
            {
                nextObstruction = Time.unscaledTime + ObstructionInterval;
                TickObstruction(world);
            }
            if (Bound.Running)
            {
                float dt = Time.deltaTime;
                Bound.Phase += Bound.Direction * Bound.Speed * dt;
                var L = Bound.ToPath();
                float loop = L != null ? L.LoopLength : 2f;
                if (loop < 1f) loop = 1f;
                Bound.Phase %= loop;
                if (Bound.Phase < 0f) Bound.Phase += loop;
            }
            if (Time.unscaledTime >= nextState)
            {
                nextState = Time.unscaledTime + StateInterval;
                HSEscalatorNet.BroadcastState(Bound);
            }
        }

        belt.Apply(Bound.Phase, Time.deltaTime);
        if (Bound.Running) CarryRiders(world);
    }

    void TickPower()
    {
        if (Time.unscaledTime < jogUntil) return;
        string problem;
        bool powered = HSEscalatorPower.IsDrivePowered(Bound, out problem);
        if (!powered)
        {
            if (Bound.Running)
            {
                Bound.Running = false;
                Bound.StopReason = problem;
            }
            return;
        }
        if (!Bound.Running && string.IsNullOrEmpty(Bound.StopReason))
            Bound.Running = true;
    }

    void TickObstruction(World world)
    {
        var err = HSEscalatorWorld.CheckMechanism(world, Bound);
        if (err == null)
        {
            if (!string.IsNullOrEmpty(Bound.StopReason) && IsMechanismStop(Bound.StopReason))
                Bound.StopReason = null;
            return;
        }
        Stop(err);
    }

    static bool IsMechanismStop(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return false;
        return reason.IndexOf("return", StringComparison.OrdinalIgnoreCase) >= 0
            || reason.IndexOf("headroom", StringComparison.OrdinalIgnoreCase) >= 0
            || reason.IndexOf("step cell", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void Stop(string reason)
    {
        Bound.Running = false;
        Bound.StopReason = reason;
        HSEscalatorDebug.Info(Bound.EscalatorId + " stopped: " + reason);
        HSEscalatorNet.BroadcastState(Bound);
        HSEscalatorNet.TellLocal(string.Format(Localization.Get("hsescalatorStopped"), reason));
    }

    void CarryRiders(World world)
    {
        if (world == null) return;
        var path = Bound.ToPath();
        if (path == null) return;
        CarryPlayers(world, path);
        CarryEntities(world, path);
    }

    void CarryPlayers(World world, HSEscalatorPath path)
    {
        var locals = world.GetLocalPlayers();
        if (locals == null) return;
        for (int i = 0; i < locals.Count; i++)
        {
            var player = locals[i] as EntityPlayerLocal;
            var fp = player != null ? player.vp_FPController : null;
            if (fp == null || player.AttachedToEntity != null) continue;
            HSEscalatorSlotPose pose;
            Vector3 delta;
            if (!belt.StepUnder(player.position, out pose, out delta)) continue;
            if (path.InHinge(pose.FoldDeg))
            {
                if (HSEscalatorNet.IsAuthority) Stop("someone is on a folding step (the comb)");
                continue;
            }
            if (LeavingOntoLanding(world, path, player.position, pose))
            {
                string land;
                bool low = pose.Col < path.Length * 0.5f;
                if (HSEscalatorNet.IsAuthority && !HSEscalatorWorld.LandingWalkable(world, Bound, low, out land))
                    Stop(land);
                continue;
            }
            if (delta.sqrMagnitude > 0.000001f)
                fp.SetPosition(fp.Transform.position + delta);
        }
    }

    void CarryEntities(World world, HSEscalatorPath path)
    {
        riders.Clear();
        int x0, z0, x1, z1;
        path.WorldXZ(0, 0, out x0, out z0);
        path.WorldXZ(path.Length - 1, Math.Max(0, path.Width - 1), out x1, out z1);
        float minY = HSEscalatorPath.TreadTop(path.Heights[0]) - 1.5f;
        float maxY = HSEscalatorPath.TreadTop(path.Heights[path.Length - 1]) + 2.5f;
        var bb = new Bounds();
        bb.SetMinMax(
            new Vector3(Math.Min(x0, x1) - 1f, Math.Min(minY, HSEscalatorPath.TreadTop(path.Heights[path.Length - 1]) - 1.5f), Math.Min(z0, z1) - 1f),
            new Vector3(Math.Max(x0, x1) + 2f, Math.Max(maxY, HSEscalatorPath.TreadTop(path.Heights[0]) + 2.5f), Math.Max(z0, z1) + 2f));
        world.GetEntitiesInBounds(typeof(Entity), bb, riders);
        for (int i = 0; i < riders.Count; i++)
        {
            var e = riders[i];
            if (e == null || e.AttachedToEntity != null) continue;
            if (e is EntityPlayer) continue;
            if (e is EntityFallingBlock) continue;
            HSEscalatorSlotPose pose;
            Vector3 delta;
            if (!belt.StepUnder(e.position, out pose, out delta)) continue;
            if (path.InHinge(pose.FoldDeg))
            {
                if (HSEscalatorNet.IsAuthority) Stop("something is on a folding step (the comb)");
                continue;
            }
            if (delta.sqrMagnitude > 0.000001f)
            {
                e.SetPosition(e.position + delta, true);
                var v = e as EntityVehicle;
                if (v != null) v.PhysicsResetAndSleep();
            }
        }
    }

    bool LeavingOntoLanding(World world, HSEscalatorPath path, Vector3 feet, HSEscalatorSlotPose pose)
    {
        int lx, lz;
        path.OutsideLanding(true, out lx, out lz);
        int hx, hz;
        path.OutsideLanding(false, out hx, out hz);
        int fx = Mathf.FloorToInt(feet.x);
        int fz = Mathf.FloorToInt(feet.z);
        return (fx == lx && fz == lz) || (fx == hx && fz == hz);
    }
}
