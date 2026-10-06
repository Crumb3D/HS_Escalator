using System;

public static class HSEscalatorSetup
{
    public static bool HasForcedAim;
    public static Vector3i ForcedAim;

    public static void Tell(EntityPlayerLocal player, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (player != null) GameManager.ShowTooltip(player, FitTooltip(msg));
        else SdtdConsole.Instance.Output(msg);
    }

    // 7DTD tooltips are one HUD line; anything longer runs off both sides of the screen.
    public static string FitTooltip(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return msg;
        const int max = 38;
        var raw = msg.Replace("\r\n", "\n").Split('\n');
        var lines = new System.Collections.Generic.List<string>();
        for (int i = 0; i < raw.Length; i++)
        {
            var words = raw[i].Split(' ');
            var cur = "";
            for (int w = 0; w < words.Length; w++)
            {
                if (words[w].Length == 0) continue;
                if (cur.Length == 0) cur = words[w];
                else if (cur.Length + 1 + words[w].Length <= max) cur += " " + words[w];
                else
                {
                    lines.Add(cur);
                    cur = words[w];
                }
            }
            if (cur.Length > 0) lines.Add(cur);
        }
        if (lines.Count > 3) lines.RemoveRange(3, lines.Count - 3);
        return string.Join("\n", lines.ToArray());
    }

    public static string Execute(string sub, string arg, EntityPlayerLocal player)
    {
        if (HSEscalatorNet.IsRemoteClient && sub != "status" && sub != "list")
        {
            if ((sub == "maxdeck" || sub == "cap") && string.IsNullOrEmpty(arg))
                return "Deck cap is " + HSEscalatorSettings.Describe() + " (from the server).";
            return HSEscalatorNet.SendSetup(sub, arg, player);
        }
        return Run(sub, arg, player);
    }

    static string Run(string sub, string arg, EntityPlayerLocal player)
    {
        var d = HSEscalatorConfig.Data;
        if (d == null) return "No escalator selected.";
        switch (sub)
        {
            case "status":
                var c = HSEscalatorController.Of(d);
                return c != null ? c.StatusText() : HSEscalatorConfig.Summary();
            case "debug":
                d.Debug = !d.Debug;
                HSEscalatorDebug.Enabled = d.Debug;
                HSEscalatorConfig.Save();
                return "HSEscalator debug " + (d.Debug ? "ON" : "OFF");
            case "list":
                return HSEscalatorConfig.ListAll();
            case "new":
            {
                var created = HSEscalatorConfig.NewEscalator();
                return "Started " + created.EscalatorId + ". Set End 1 and End 2 on the steps.";
            }
            case "select":
            {
                Vector3i p;
                var err = AimedBlock(player, out p);
                if (err != null) return err;
                return HSEscalatorConfig.SelectNearest(p);
            }
            case "end1":
            case "end2":
                return SetEnd(sub == "end1", player);
            case "drive":
                return RegisterDrive(arg, player);
            case "jog":
            {
                var ctrl = HSEscalatorController.Ensure(d);
                return ctrl != null ? ctrl.Jog() : "No controller.";
            }
            case "reverse":
            {
                var ctrl = HSEscalatorController.Ensure(d);
                return ctrl != null ? ctrl.Reverse() : "No controller.";
            }
            case "forget":
            case "delete":
                return Forget(d);
            case "testpath":
                return HSEscalatorPath.SelfTest();
            case "speed":
            {
                float s;
                if (!float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out s) || s < 0.2f || s > 3f)
                    return "Usage: hsescalator speed <0.2 - 3>";
                d.Speed = s;
                HSEscalatorConfig.Save();
                return "Speed = " + s + " blocks/sec";
            }
            case "maxdeck":
            case "cap":
            {
                if (string.IsNullOrEmpty(arg))
                    return "Deck cap is " + HSEscalatorSettings.Describe() + ". Usage: hsescalator maxdeck <cells>   (0 = no cap)";
                int n;
                if (!int.TryParse(arg, out n))
                    return "Usage: hsescalator maxdeck <cells>   (0 = no cap)";
                return HSEscalatorSettings.SetMaxDeckCells(n);
            }
        }
        return "Unknown command.";
    }

    static string SetEnd(bool first, EntityPlayerLocal player)
    {
        var d = HSEscalatorConfig.Data;
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        BindAimed(p);
        d = HSEscalatorConfig.Data;
        var world = GameManager.Instance.World;
        HSEscalatorHalf half;
        if (world == null || !HSEscalatorWorld.TryReadHalf(world, p, out half))
            return "Aim at a half-block step. " + (world != null ? HSEscalatorWorld.DisplayName(world.GetBlock(p)) : "No block") + " at " + p + " is not a step.";
        if (d.Captured)
        {
            var ctrl = HSEscalatorController.Of(d);
            if (ctrl != null) ctrl.RebuildBelt();
            HSEscalatorWorld.Restore(world, d);
        }
        var c = new[] { p.x, p.y, p.z };
        if (first) d.End1 = c; else d.End2 = c;
        d.HasDeck = false;
        d.Captured = false;
        HSEscalatorConfig.Save();
        if (d.End1 == null || d.End2 == null)
            return (first ? "End 1 set." : "End 2 set.") + " Mark the other end.";
        return FinishDeck(world, d);
    }

    static string FinishDeck(World world, HSEscalatorConfigData d)
    {
        HSEscalatorPath path;
        var err = HSEscalatorWorld.ScanDeck(world, d, out path);
        if (err != null)
        {
            d.HasDeck = false;
            return err;
        }
        err = HSEscalatorWorld.Capture(world, d, path);
        if (err != null)
        {
            d.HasDeck = false;
            return "Could not take the steps: " + err;
        }
        var ctrl = HSEscalatorController.Ensure(d);
        if (ctrl != null) ctrl.RebuildBelt();
        HSEscalatorConfig.Save();
        return (d.IsWalkway ? "Walkway " : "Escalator ") + d.Length + "x" + d.Width
            + (d.DriveCount > 0 ? " ready." : ". Register the Panel.");
    }

    static string RegisterDrive(string arg, EntityPlayerLocal player)
    {
        var d = HSEscalatorConfig.Data;
        if (arg == "clear")
        {
            d.ClearDrives();
            HSEscalatorConfig.Save();
            return "Panels forgotten.";
        }
        Vector3i p;
        var err = AimedBlock(player, out p);
        if (err != null) return err;
        BindAimed(p);
        d = HSEscalatorConfig.Data;
        var world = GameManager.Instance.World;
        var bv = world.GetBlock(p);
        p = BlockHSEscalatorDrive.ParentPos(p, bv);
        if (!(world.GetBlock(p).Block is BlockHSEscalatorDrive))
            return "Aim at an Escalator Panel. That block is " + HSEscalatorWorld.DisplayName(bv) + ".";
        if (d.HasDeck && d.InDeckXZ(p.x, p.z))
            return "The panel sits on a step. Place it beside an end, not on the deck.";
        if (d.IsDrive(p))
        {
            HSEscalatorConfig.Save();
            return "That panel is already registered.";
        }
        if (d.DriveCount >= 2 && !d.IsDrive(p))
        {
            d.Drive2X = p.x;
            d.Drive2Y = p.y;
            d.Drive2Z = p.z;
            d.HasDrive2 = true;
        }
        else
            d.AddDrive(p);
        d.StopReason = null;
        HSEscalatorConfig.Save();
        string power;
        bool on = HSEscalatorPower.IsDrivePowered(d, out power);
        return "Panel " + d.DriveCount + " registered at " + p + ". " + (on ? "Powered — it will run." : power);
    }

    static string Forget(HSEscalatorConfigData d)
    {
        var world = GameManager.Instance != null ? GameManager.Instance.World : null;
        var ctrl = HSEscalatorController.Of(d);
        if (ctrl != null) ctrl.RebuildBelt();
        if (world != null) HSEscalatorWorld.Restore(world, d);
        d.End1 = d.End2 = null;
        d.HasDeck = false;
        d.Captured = false;
        d.ClearDrives();
        d.Steps.Clear();
        d.Running = false;
        d.StopReason = null;
        HSEscalatorConfig.Save();
        return "Forgot " + d.EscalatorId + ". Original half-blocks were put back.";
    }

    static void BindAimed(Vector3i p)
    {
        var d = HSEscalatorConfig.At(p);
        if (d != null) HSEscalatorConfig.Use(d);
    }

    public static string AimedBlock(EntityPlayerLocal player, out Vector3i pos)
    {
        pos = Vector3i.zero;
        if (HasForcedAim)
        {
            pos = ForcedAim;
            return null;
        }
        var world = GameManager.Instance.World;
        if (player == null) player = world != null ? world.GetPrimaryPlayer() : null;
        if (player == null) return "No local player.";
        var hit = player.HitInfo;
        if (hit == null || !hit.bHitValid) return "Aim at a block first.";
        pos = hit.hit.blockPos;
        if (world.GetBlock(pos).isair) return "Aim at a block first.";
        return null;
    }
}
