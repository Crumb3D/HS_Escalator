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
            return new[] { new BlockActivationCommand("hsescalatorReverse", "electric_switch", true, false, null) };
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Drive commands failed", e);
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
            if (esc.Running) return Localization.Get("hsescalatorReverse");
            if (!string.IsNullOrEmpty(esc.StopReason))
                return string.Format(Localization.Get("hsescalatorStopped"), esc.StopReason);
            string problem;
            if (!HSEscalatorPower.IsDrivePowered(esc, out problem))
                return string.Format(Localization.Get("hsescalatorNotReady"), problem);
            return Localization.Get("hsescalatorReverse");
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Drive text failed", e);
            return "";
        }
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        try
        {
            var pos = ParentPos(_blockPos, _blockValue);
            var msg = HSEscalatorController.ReverseDrive(pos);
            if (_player != null && !string.IsNullOrEmpty(msg))
                GameManager.ShowTooltip(_player, msg);
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Drive press failed", e);
        }
        return true;
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
