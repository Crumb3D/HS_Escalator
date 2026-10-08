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
    float nextRail;
    float nextHide;
    float jogUntil;
    float occupiedUntil;
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
            if (HSEscalatorNet.IsAuthority && world != null)
                HSEscalatorWorld.Restore(world, c.Bound, true);
            c.belt.Destroy();
        }
        if (HSEscalatorNet.IsAuthority) HSEscalatorConfig.Save();
        HSEscalatorConfig.Unload();
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
        return SetDirection(Bound != null && Bound.Direction < 0 ? 1 : -1);
    }

    string SetDirection(int dir)
    {
        if (Bound == null) return "No escalator.";
        Bound.Direction = dir < 0 ? -1 : 1;
        Bound.StopReason = null;
        HSEscalatorConfig.Save();
        HSEscalatorNet.BroadcastState(Bound);
        return Bound.EscalatorId + " now going " + (Bound.Direction > 0 ? "forward" : "reverse") + ".";
    }

    public static string ReverseDrive(Vector3i pos)
    {
        return DriveCommand(pos, "reverse");
    }

    public static string DriveCommand(Vector3i pos, string cmd)
    {
        var d = HSEscalatorConfig.DriveOwner(pos);
        if (d == null) return Localization.Get("hsescalatorUnregistered");
        HSEscalatorConfig.Use(d);
        var c = Ensure(d);
        return c != null ? c.ApplyPanel(cmd) : "No controller.";
    }

    public string ApplyPanel(string cmd)
    {
        if (Bound == null) return "No escalator.";
        if (cmd == "forward") return SetDirection(1);
        if (cmd == "reverse") return SetDirection(-1);
        if (cmd == "toggleDir") return Reverse();
        if (cmd == "start")
        {
            Bound.WantedOn = true;
            Bound.StopReason = null;
            Bound.Running = true;
            HSEscalatorConfig.Save();
            HSEscalatorNet.BroadcastState(Bound);
            return Bound.EscalatorId + " started.";
        }
        if (cmd == "stop")
        {
            Bound.WantedOn = false;
            Bound.Running = false;
            HSEscalatorConfig.Save();
            HSEscalatorNet.BroadcastState(Bound);
            return Bound.EscalatorId + " stopped.";
        }
        if (cmd == "always")
        {
            Bound.RunWhenOccupied = false;
            HSEscalatorConfig.Save();
            return Bound.EscalatorId + " runs whenever it has power.";
        }
        if (cmd == "occupied")
        {
            Bound.RunWhenOccupied = true;
            HSEscalatorConfig.Save();
            return Bound.EscalatorId + " runs when something is on it.";
        }
        if (cmd == "speed1" || cmd == "speed2" || cmd == "speed3")
        {
            int gear = cmd == "speed2" ? 2 : cmd == "speed3" ? 3 : 1;
            Bound.Speed = HSEscalatorConfigData.SpeedForGear(gear);
            HSEscalatorConfig.Save();
            return Bound.EscalatorId + " speed " + gear + ".";
        }
        return "Unknown panel command.";
    }

    public void ApplyRemoteState(float phase, int dir, bool running, string reason)
    {
        if (Bound == null) return;
        Bound.Phase = phase;
        Bound.Direction = dir;
        Bound.Running = running;
        Bound.StopReason = reason;
    }

    void AdvancePhase(float dt)
    {
        if (Bound == null) return;
        Bound.Phase += Bound.Direction * Bound.Speed * dt;
        var L = Bound.ToPath();
        float loop = L != null ? L.LoopLength : 2f;
        if (loop < 1f) loop = 1f;
        Bound.Phase %= loop;
        if (Bound.Phase < 0f) Bound.Phase += loop;
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
            if (HSEscalatorNet.IsAuthority)
            {
                HSEscalatorWorld.EnsureCapturedRemoved(world, Bound);
                if (Time.unscaledTime >= nextHide)
                {
                    nextHide = Time.unscaledTime + 1f;
                    HSEscalatorWorld.HideSupports(world, Bound);
                }
            }
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
                TickPower(world);
            }
            if (Time.unscaledTime >= nextObstruction)
            {
                nextObstruction = Time.unscaledTime + ObstructionInterval;
                TickObstruction(world);
            }
            if (Bound.Running) AdvancePhase(Time.deltaTime);
            if (Time.unscaledTime >= nextState)
            {
                nextState = Time.unscaledTime + StateInterval;
                HSEscalatorNet.BroadcastState(Bound);
            }
        }
        else if (Bound.Running)
        {
            AdvancePhase(Time.deltaTime);
        }

        if (belt.IsBuilt && Time.unscaledTime >= nextRail)
        {
            nextRail = Time.unscaledTime + 1f;
            belt.RefreshRails(world);
        }

        belt.Apply(Bound.Phase, Time.deltaTime);
        if (Bound.Running) CarryRiders(world);
        EjectTrapped(world, Bound.ToPath());
    }

    void TickPower(World world)
    {
        if (Time.unscaledTime < jogUntil) return;
        string problem;
        bool powered = HSEscalatorPower.IsDrivePowered(Bound, out problem);
        if (problem == HSEscalatorPower.ChunksNotReady) return;
        if (!powered)
        {
            if (Bound.Running || IsPowerStop(Bound.StopReason))
            {
                Bound.Running = false;
                Bound.StopReason = problem;
            }
            return;
        }
        if (IsPowerStop(Bound.StopReason))
            Bound.StopReason = null;
        if (!Bound.WantedOn)
        {
            Bound.Running = false;
            return;
        }
        if (Bound.RunWhenOccupied)
        {
            if (SomeoneOnBelt(world)) occupiedUntil = Time.unscaledTime + 0.75f;
            if (Time.unscaledTime > occupiedUntil)
            {
                Bound.Running = false;
                return;
            }
        }
        if (!Bound.Running && string.IsNullOrEmpty(Bound.StopReason))
            Bound.Running = true;
    }

    static bool IsPowerStop(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return false;
        return reason.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0
            || reason.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public bool SomeoneOnBelt(World world)
    {
        if (world == null || Bound == null) return false;
        var locals = world.GetLocalPlayers();
        if (locals != null)
        {
            for (int i = 0; i < locals.Count; i++)
            {
                var p = locals[i] as Entity;
                if (p != null && OnRidingSurface(p.position)) return true;
            }
        }
        riders.Clear();
        var path = Bound.ToPath();
        if (path == null) return false;
        int x0, z0, x1, z1;
        path.WorldXZ(0, 0, out x0, out z0);
        path.WorldXZ(path.Length - 1, Math.Max(0, path.Width - 1), out x1, out z1);
        float lo = HSEscalatorPath.TreadTop(path.Heights[0]);
        float hi = HSEscalatorPath.TreadTop(path.Heights[path.Length - 1]);
        var bb = new Bounds();
        bb.SetMinMax(
            new Vector3(Math.Min(x0, x1) - 1f, Math.Min(lo, hi) - 1.5f, Math.Min(z0, z1) - 1f),
            new Vector3(Math.Max(x0, x1) + 2f, Math.Max(lo, hi) + 2.5f, Math.Max(z0, z1) + 2f));
        world.GetEntitiesInBounds(typeof(Entity), bb, riders);
        for (int i = 0; i < riders.Count; i++)
        {
            var e = riders[i];
            if (e == null || e is EntityFallingBlock) continue;
            if (OnRidingSurface(e.position)) return true;
        }
        return false;
    }

    bool OnRidingSurface(Vector3 feet)
    {
        var path = Bound != null ? Bound.ToPath() : null;
        return path != null && path.OnRidingSurface(feet);
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
            if (LeavingOntoLanding(world, path, player.position, pose))
                continue;
            if (pose.FoldDeg > 12f) continue;
            if (HSEscalatorBelt.OnCombPlate(path, player.position)) delta.y = 0f;
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
            bool onStep = belt.StepUnder(e.position, out pose, out delta);
            if (!onStep && !OnTread(path, e.position)) continue;
            if (onStep && pose.FoldDeg > 12f) continue;
            var move = delta;
            if (HSEscalatorBelt.OnCombPlate(path, e.position)) move.y = 0f;
            if (move.sqrMagnitude < 0.000001f)
                move = BeltTravel(path, 1.6f);
            else
                move += BeltTravel(path, 1.0f);
            if (move.sqrMagnitude > 0.000001f)
            {
                e.SetPosition(e.position + move, true);
                var v = e as EntityVehicle;
                if (v != null) v.PhysicsResetAndSleep();
            }
        }
    }

    void EjectTrapped(World world, HSEscalatorPath path)
    {
        if (world == null || path == null) return;
        riders.Clear();
        int x0, z0, x1, z1;
        path.WorldXZ(0, 0, out x0, out z0);
        path.WorldXZ(path.Length - 1, Math.Max(0, path.Width - 1), out x1, out z1);
        float lo = HSEscalatorPath.TreadTop(path.Heights[0]);
        float hi = HSEscalatorPath.TreadTop(path.Heights[path.Length - 1]);
        var bb = new Bounds();
        bb.SetMinMax(
            new Vector3(Math.Min(x0, x1) - 0.25f, Math.Min(lo, hi) - 1.6f, Math.Min(z0, z1) - 0.25f),
            new Vector3(Math.Max(x0, x1) + 1.25f, Math.Max(lo, hi) + 1.2f, Math.Max(z0, z1) + 1.25f));
        world.GetEntitiesInBounds(typeof(EntityAlive), bb, riders);
        for (int i = 0; i < riders.Count; i++)
        {
            var e = riders[i];
            if (e == null || e.AttachedToEntity != null) continue;
            if (e is EntityFallingBlock) continue;
            if (e is EntityPlayer) continue;
            if (!path.InReturnCavity(e.position)) continue;
            Vector3 stand;
            if (!NearestLanding(world, path, e.position, out stand)) continue;
            e.SetPosition(stand, true);
            var v = e as EntityVehicle;
            if (v != null) v.PhysicsResetAndSleep();
        }
    }

    bool NearestLanding(World world, HSEscalatorPath path, Vector3 feet, out Vector3 stand)
    {
        float col;
        int lane;
        bool low = true;
        if (path.TryWorldCol(feet.x, feet.z, out col, out lane))
            low = col < (path.Length - 1) * 0.5f;
        else if (Bound != null)
            low = Bound.Direction < 0;
        if (!HSEscalatorWorld.TryLandingStand(world, path, low, feet, out stand))
            return false;
        int push = low ? -path.RunSign : path.RunSign;
        if (path.RunAxis == 0) stand.x += push * 0.4f;
        else stand.z += push * 0.4f;
        return true;
    }

    bool OnTread(HSEscalatorPath path, Vector3 pos)
    {
        float col;
        int lane;
        if (!path.TryWorldCol(pos.x, pos.z, out col, out lane)) return false;
        float a = path.BeltHeight(col - 0.5f) * 0.5f;
        float b = path.BeltHeight(col + 0.5f) * 0.5f;
        return pos.y >= Math.Min(a, b) - 0.25f && pos.y <= Math.Max(a, b) + 0.3f;
    }

    Vector3 BeltTravel(HSEscalatorPath path, float extra)
    {
        if (Bound == null || path == null) return Vector3.zero;
        float dist = Bound.Direction * (Bound.Speed + extra) * Time.deltaTime;
        if (path.RunAxis == 0) return new Vector3(path.RunSign * dist, 0f, 0f);
        return new Vector3(0f, 0f, path.RunSign * dist);
    }

    bool LeavingOntoLanding(World world, HSEscalatorPath path, Vector3 feet, HSEscalatorSlotPose pose)
    {
        int fx = Mathf.FloorToInt(feet.x);
        int fz = Mathf.FloorToInt(feet.z);
        int lanes = Math.Max(1, path.Width);
        for (int w = 0; w < lanes; w++)
        {
            int lx, lz, hx, hz;
            path.OutsideLanding(true, w, out lx, out lz);
            path.OutsideLanding(false, w, out hx, out hz);
            if ((fx == lx && fz == lz) || (fx == hx && fz == hz)) return true;
        }
        return false;
    }
}
