using System;
using System.Collections.Generic;

public class ConsoleCmdHSEscalator : ConsoleCmdAbstract
{
    public override string[] getCommands()
    {
        return new[] { "hsescalator" };
    }

    public override string getDescription()
    {
        return "HSEscalator admin: leftover setup and debug. Players use the Escalator Setup Tool (hold E).";
    }

    public override int DefaultPermissionLevel { get { return 1000; } }

    public override string getHelp()
    {
        return
            "Admin only. Players: hold the Escalator Setup Tool and hold E.\n" +
            "hsescalator new               - start a new escalator\n" +
            "hsescalator end1 | end2       - mark opposite corners of the step deck\n" +
            "hsescalator drive             - register the aimed Escalator Drive\n" +
            "hsescalator list | select     - list / aim to edit that one\n" +
            "hsescalator jog | reverse     - preview motion / flip direction\n" +
            "hsescalator forget            - put the half-blocks back and clear this one\n" +
            "hsescalator speed <0.2-3>     - belt speed\n" +
            "hsescalator status | debug    - status / verbose log\n" +
            "hsescalator testpath          - run layout math checks";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        try
        {
            var sub = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "status";
            var arg = _params != null && _params.Count > 1 ? _params[1] : "";
            var world = GameManager.Instance.World;
            EntityPlayerLocal player = null;
            if (world != null)
            {
                player = world.GetPrimaryPlayer();
                if (player == null)
                {
                    var locals = world.GetLocalPlayers();
                    if (locals != null && locals.Count > 0) player = locals[0] as EntityPlayerLocal;
                }
            }
            Out(HSEscalatorSetup.Execute(sub, arg, player));
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Command failed", e);
            Out("HSEscalator command failed: " + e.Message);
        }
    }

    static void Out(string s)
    {
        SdtdConsole.Instance.Output(s);
    }
}
