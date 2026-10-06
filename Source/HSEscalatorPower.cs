using System;

public static class HSEscalatorPower
{
    public static bool IsDrivePowered(HSEscalatorConfigData d, out string problem)
    {
        if (d == null || d.DriveCount < 1)
        {
            problem = "needs an Escalator Panel registered beside an end";
            return false;
        }
        try
        {
            var world = GameManager.Instance.World;
            if (world == null)
            {
                problem = "no world";
                return false;
            }
            int missing = 0;
            int unpowered = 0;
            int ok = 0;
            if (d.HasDrive) CheckOne(world, d.DrivePos, ref missing, ref unpowered, ref ok);
            if (d.HasDrive2) CheckOne(world, d.Drive2Pos, ref missing, ref unpowered, ref ok);
            if (ok > 0)
            {
                problem = null;
                return true;
            }
            if (unpowered > 0)
            {
                problem = "no power: wire a generator or battery bank to an Escalator Panel";
                return false;
            }
            problem = "panel is missing (re-register it)";
            return false;
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Power check failed", e);
            problem = "power check failed";
            return false;
        }
    }

    static void CheckOne(World world, Vector3i pos, ref int missing, ref int unpowered, ref int ok)
    {
        if (!(world.GetBlock(pos).Block is BlockHSEscalatorDrive))
        {
            missing++;
            return;
        }
        var te = world.GetTileEntity(pos) as TileEntityPowered;
        if (te != null && te.IsPowered)
        {
            ok++;
            return;
        }
        if (PowerManager.HasInstance)
        {
            var item = PowerManager.Instance.GetPowerItemByWorldPos(pos);
            if (item != null && item.IsPowered)
            {
                ok++;
                return;
            }
        }
        unpowered++;
    }
}
