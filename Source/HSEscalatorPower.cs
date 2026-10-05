using System;

public static class HSEscalatorPower
{
    public static bool IsDrivePowered(HSEscalatorConfigData d, out string problem)
    {
        if (d == null || !d.HasDrive)
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
            var pos = d.DrivePos;
            if (!(world.GetBlock(pos).Block is BlockHSEscalatorDrive))
            {
                problem = "panel at " + pos + " is missing (re-register it)";
                return false;
            }
            var te = world.GetTileEntity(pos) as TileEntityPowered;
            if (te != null && te.IsPowered)
            {
                problem = null;
                return true;
            }
            if (PowerManager.HasInstance)
            {
                var item = PowerManager.Instance.GetPowerItemByWorldPos(pos);
                if (item != null && item.IsPowered)
                {
                    problem = null;
                    return true;
                }
            }
            problem = "no power: wire a generator or battery bank to this Escalator Panel";
            return false;
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Power check failed", e);
            problem = "power check failed";
            return false;
        }
    }
}
