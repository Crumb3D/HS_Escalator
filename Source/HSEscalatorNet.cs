using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

public static class HSEscalatorNet
{
    public const byte Config = 1;
    public const byte Setup = 2;
    public const byte State = 3;
    public const byte Tip = 4;
    public const byte Reverse = 5;

    public static bool IsAuthority
    {
        get
        {
            try
            {
                if (GameManager.IsDedicatedServer) return true;
                var cm = ConnectionManager.Instance;
                if (cm != null) return cm.IsServer;
            }
            catch { }
            return true;
        }
    }

    public static bool IsRemoteClient
    {
        get
        {
            try
            {
                var cm = ConnectionManager.Instance;
                return cm != null && cm.IsClient && !cm.IsServer;
            }
            catch { }
            return false;
        }
    }

    static Type pkgType;

    public static void RegisterPackage()
    {
        try
        {
            var t = HSGameVersion.Is33
                ? HSGameApi.NetPackageType33("NetPackageHSEscalator", typeof(NetPackageHSEscalatorCore))
                : HSGameApi.NetPackageType32("NetPackageHSEscalator", typeof(NetPackageHSEscalatorCore));
            pkgType = t;
            var f = typeof(NetPackageManager).GetField("knownPackageTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null) return;
            var dict = f.GetValue(null) as IDictionary;
            if (dict == null) return;
            var args = f.FieldType.GetGenericArguments();
            if (args != null && args.Length >= 1 && args[0] == typeof(string))
            {
                if (!dict.Contains(t.Name)) dict[t.Name] = t;
            }
            else if (args != null && args.Length >= 1 && args[0] == typeof(Type))
            {
                if (!dict.Contains(t)) dict[t] = t.Name;
            }
            else if (!dict.Contains(t.Name)) dict[t.Name] = t;
            HSEscalatorDebug.Verbose("Registered NetPackageHSEscalator");
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Net package register failed", e);
        }
    }

    static NetPackageHSEscalatorCore Pkg()
    {
        if (pkgType == null) RegisterPackage();
        return (NetPackageHSEscalatorCore)HSGameApi.GetNetPackage(pkgType);
    }

    static void ToServer(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null) return;
        cm.SendToClientsOrServer(pkg);
    }

    static void ToClients(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer) return;
        cm.SendPackage(pkg);
    }

    static void ToClient(ClientInfo ci, NetPackage pkg)
    {
        if (ci != null) ci.SendPackage(pkg);
        else ToClients(pkg);
    }

    public static string SendSetup(string sub, string arg, EntityPlayerLocal player)
    {
        Vector3i aim = Vector3i.zero;
        bool hasAim = HSEscalatorSetup.AimedBlock(player, out aim) == null;
        ToServer(Pkg().SetupCmd(Setup, sub ?? "", arg ?? "", "", 0, 0f, false, hasAim, aim));
        return null;
    }

    public static void SendReverse(Vector3i pos)
    {
        SendDriveCmd(pos, "reverse");
    }

    public static void SendDriveCmd(Vector3i pos, string cmd)
    {
        ToServer(Pkg().SetupCmd(Reverse, cmd ?? "", "", "", 0, 0f, false, true, pos));
    }

    public static void BroadcastConfig()
    {
        if (!IsAuthority) return;
        try
        {
            ToClients(Pkg().SetupCmd(Config, HSEscalatorConfig.ToSyncJson(), "", "", 0, 0f, false, false, Vector3i.zero));
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Broadcast escalator list failed", e);
        }
    }

    public static void SendConfigTo(ClientInfo ci)
    {
        if (!IsAuthority || ci == null) return;
        try
        {
            ToClient(ci, Pkg().SetupCmd(Config, HSEscalatorConfig.ToSyncJson(), "", "", 0, 0f, false, false, Vector3i.zero));
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Send escalator list failed", e);
        }
    }

    public static void BroadcastState(HSEscalatorConfigData d)
    {
        if (!IsAuthority || d == null) return;
        ToClients(Pkg().SetupCmd(State, d.StopReason ?? "", "", d.EscalatorId ?? "", d.Direction, d.Phase, d.Running, false, Vector3i.zero));
    }

    public static void SendStateTo(ClientInfo ci, HSEscalatorConfigData d)
    {
        if (!IsAuthority || ci == null || d == null) return;
        ToClient(ci, Pkg().SetupCmd(State, d.StopReason ?? "", "", d.EscalatorId ?? "", d.Direction, d.Phase, d.Running, false, Vector3i.zero));
    }

    public static void ReplyTip(ClientInfo ci, string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (ci != null)
        {
            ToClient(ci, Pkg().SetupCmd(Tip, msg, "", "", 0, 0f, false, false, Vector3i.zero));
            return;
        }
        TellLocal(msg);
    }

    public static void TellLocal(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var world = GameManager.Instance != null ? GameManager.Instance.World : null;
            var locals = world != null ? world.GetLocalPlayers() : null;
            if (locals == null) return;
            for (int i = 0; i < locals.Count; i++)
            {
                var p = locals[i] as EntityPlayerLocal;
                if (p != null) GameManager.ShowTooltip(p, HSEscalatorSetup.FitTooltip(msg));
            }
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Local tip failed: " + e.Message);
        }
    }

    public static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!IsAuthority || data.ClientInfo == null) return;
        SendConfigTo(data.ClientInfo);
        foreach (var d in HSEscalatorConfig.Escalators)
            SendStateTo(data.ClientInfo, d);
    }
}

public abstract class NetPackageHSEscalatorCore : NetPackage
{
    protected byte kind;
    protected string text;
    protected string arg;
    protected string id;
    protected int direction;
    protected float phase;
    protected bool running;
    protected bool hasPos;
    protected Vector3i pos;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.Both; } }

    public NetPackageHSEscalatorCore SetupCmd(byte k, string t, string a, string i, int dir, float ph, bool run, bool has, Vector3i p)
    {
        kind = k;
        text = t ?? "";
        arg = a ?? "";
        id = i ?? "";
        direction = dir;
        phase = ph;
        running = run;
        hasPos = has;
        pos = p;
        return this;
    }

    public override void read(PooledBinaryReader br)
    {
        kind = br.ReadByte();
        text = br.ReadString();
        arg = br.ReadString();
        id = br.ReadString();
        direction = br.ReadInt32();
        phase = br.ReadSingle();
        running = br.ReadBoolean();
        hasPos = br.ReadBoolean();
        pos = new Vector3i(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(kind);
        bw.Write(text ?? "");
        bw.Write(arg ?? "");
        bw.Write(id ?? "");
        bw.Write(direction);
        bw.Write(phase);
        bw.Write(running);
        bw.Write(hasPos);
        bw.Write(pos.x);
        bw.Write(pos.y);
        bw.Write(pos.z);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        try
        {
            if (world == null) return;
            switch (kind)
            {
                case HSEscalatorNet.Config:
                    if (HSEscalatorNet.IsAuthority) return;
                    HSEscalatorConfig.ApplyFromServer(text);
                    break;
                case HSEscalatorNet.Setup:
                    if (!HSEscalatorNet.IsAuthority) return;
                    HSEscalatorSetup.HasForcedAim = hasPos;
                    HSEscalatorSetup.ForcedAim = pos;
                    string result;
                    try { result = HSEscalatorSetup.Execute(text, arg, null); }
                    finally { HSEscalatorSetup.HasForcedAim = false; }
                    HSEscalatorNet.ReplyTip(Sender, result);
                    break;
                case HSEscalatorNet.State:
                    if (HSEscalatorNet.IsAuthority) return;
                    var d = HSEscalatorConfig.ById(id);
                    if (d == null) return;
                    var ctrl = HSEscalatorController.Ensure(d);
                    if (ctrl != null) ctrl.ApplyRemoteState(phase, direction, running, text);
                    break;
                case HSEscalatorNet.Tip:
                    HSEscalatorNet.TellLocal(text);
                    break;
                case HSEscalatorNet.Reverse:
                    if (!HSEscalatorNet.IsAuthority) return;
                    HSEscalatorNet.ReplyTip(Sender, HSEscalatorController.DriveCommand(pos, string.IsNullOrEmpty(text) ? "reverse" : text));
                    break;
            }
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Net package failed (" + kind + ")", e);
        }
    }

}

[HarmonyPatch(typeof(NetPackageManager), "SetupBaseMapping")]
public static class HSEscalatorNetRegister
{
    static void Postfix()
    {
        HSEscalatorNet.RegisterPackage();
    }
}
