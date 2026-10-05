using System;

public class BlockHSEscalatorDrive : BlockPowered
{
    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var esc = HSEscalatorConfig.DriveOwner(pos);
            if (esc == null) return new BlockActivationCommand[0];
            HSEscalatorConfig.Use(esc);
            string dir = esc.Direction < 0 ? "hsescalatorForward" : "hsescalatorReverse";
            string run = esc.WantedOn ? "hsescalatorStop" : "hsescalatorStart";
            string mode = esc.RunWhenOccupied ? "hsescalatorRunAlways" : "hsescalatorRunOccupied";
            return new[]
            {
                new BlockActivationCommand(dir, "electric_switch", true, false, null),
                new BlockActivationCommand(run, "lightbulb", true, false, null),
                new BlockActivationCommand(mode, "map", true, false, null)
            };
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Panel commands failed", e);
            return new BlockActivationCommand[0];
        }
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var esc = HSEscalatorConfig.DriveOwner(pos);
            if (esc == null) return Localization.Get("hsescalatorUnregistered");
            if (!string.IsNullOrEmpty(esc.StopReason))
                return string.Format(Localization.Get("hsescalatorStopped"), esc.StopReason);
            string problem;
            if (!HSEscalatorPower.IsDrivePowered(esc, out problem))
                return string.Format(Localization.Get("hsescalatorNotReady"), problem);
            return Localization.Get("hsescalatorPanelHint");
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Panel text failed", e);
            return "";
        }
    }

    public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return true;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            string cmd = CommandToNet(_commandName);
            if (string.IsNullOrEmpty(cmd)) return true;
            if (HSEscalatorNet.IsRemoteClient)
            {
                HSEscalatorNet.SendDriveCmd(pos, cmd);
                return true;
            }
            var msg = HSEscalatorController.DriveCommand(pos, cmd);
            if (_player != null && !string.IsNullOrEmpty(msg))
                GameManager.ShowTooltip(_player, HSEscalatorSetup.FitTooltip(msg));
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Panel press failed", e);
        }
        return true;
    }

    static string CommandToNet(string name)
    {
        if (name == "hsescalatorForward") return "forward";
        if (name == "hsescalatorReverse") return "reverse";
        if (name == "hsescalatorStart") return "start";
        if (name == "hsescalatorStop") return "stop";
        if (name == "hsescalatorRunAlways") return "always";
        if (name == "hsescalatorRunOccupied") return "occupied";
        return null;
    }

    public static Vector3i ParentPos(Vector3i pos, BlockValue bv)
    {
        try
        {
            if (bv.ischild && bv.Block != null)
                return bv.Block.multiBlockPos.GetParentPos(pos, bv);
        }
        catch { }
        return pos;
    }
}
