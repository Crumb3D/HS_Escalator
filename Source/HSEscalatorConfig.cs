using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class HSEscalatorStepCell
{
    public int Col;
    public int Lane;
    public uint Raw;
    public int Damage;
    public sbyte Density;
    public long[] Tex;
}

public class HSEscalatorSupportCell
{
    public int X;
    public int Y;
    public int Z;
    public uint Raw;
    public int Damage;
    public sbyte Density;
    public long[] Tex;
}

public class HSEscalatorConfigData
{
    public string EscalatorId = "esc1";
    public int[] End1;
    public int[] End2;

    public bool HasDeck;
    public bool Captured;
    public int OriginX;
    public int OriginZ;
    public int LaneMinX;
    public int LaneMinZ;
    public int Length;
    public int Width;
    public int RunAxis;
    public int RunSign = 1;
    public int[] Heights;
    public bool IsWalkway;
    public int RiseHalf;

    public int DriveX, DriveY, DriveZ;
    public bool HasDrive;
    public int Drive2X, Drive2Y, Drive2Z;
    public bool HasDrive2;

    public float Speed = 0.6f;
    public int Direction = 1;
    public bool Running;
    public bool WantedOn = true;
    public bool RunWhenOccupied;
    public string Side = "glass";
    public float Phase;
    public string StopReason;
    public bool Debug;

    public List<HSEscalatorStepCell> Steps = new List<HSEscalatorStepCell>();
    public List<HSEscalatorSupportCell> Supports = new List<HSEscalatorSupportCell>();

    [JsonIgnore]
    public bool SideGlass
    {
        get { return !string.Equals(Side, "metal", StringComparison.OrdinalIgnoreCase); }
    }

    [JsonIgnore]
    public Vector3i DrivePos { get { return new Vector3i(DriveX, DriveY, DriveZ); } }

    [JsonIgnore]
    public Vector3i Drive2Pos { get { return new Vector3i(Drive2X, Drive2Y, Drive2Z); } }

    [JsonIgnore]
    public int DriveCount
    {
        get { return (HasDrive ? 1 : 0) + (HasDrive2 ? 1 : 0); }
    }

    public bool IsDrive(Vector3i pos)
    {
        if (HasDrive && DriveX == pos.x && DriveY == pos.y && DriveZ == pos.z) return true;
        if (HasDrive2 && Drive2X == pos.x && Drive2Y == pos.y && Drive2Z == pos.z) return true;
        return false;
    }

    public void AddDrive(Vector3i p)
    {
        if (HasDrive && DriveX == p.x && DriveY == p.y && DriveZ == p.z) return;
        if (HasDrive2 && Drive2X == p.x && Drive2Y == p.y && Drive2Z == p.z) return;
        if (!HasDrive)
        {
            DriveX = p.x;
            DriveY = p.y;
            DriveZ = p.z;
            HasDrive = true;
            return;
        }
        Drive2X = p.x;
        Drive2Y = p.y;
        Drive2Z = p.z;
        HasDrive2 = true;
    }

    public void ClearDrives()
    {
        HasDrive = false;
        HasDrive2 = false;
    }

    public HSEscalatorPath ToPath()
    {
        if (!HasDeck || Length < 3 || Heights == null || Heights.Length != Length) return null;
        return new HSEscalatorPath
        {
            Length = Length,
            Width = Math.Max(1, Width),
            RunAxis = RunAxis,
            RunSign = RunSign == 0 ? 1 : RunSign,
            OriginX = OriginX,
            OriginZ = OriginZ,
            LaneMinX = LaneMinX,
            LaneMinZ = LaneMinZ,
            Heights = Heights,
            IsWalkway = IsWalkway,
            RiseHalf = RiseHalf
        };
    }

    public bool InDeckXZ(int x, int z)
    {
        if (!HasDeck) return false;
        var path = ToPath();
        if (path == null) return false;
        for (int c = 0; c < Length; c++)
        for (int w = 0; w < Width; w++)
        {
            int px, pz;
            path.WorldXZ(c, w, out px, out pz);
            if (px == x && pz == z) return true;
        }
        return false;
    }
}

public class HSEscalatorFile
{
    public string ActiveId;
    public bool Debug;
    public int MaxDeckCells;
    public List<HSEscalatorConfigData> Escalators = new List<HSEscalatorConfigData>();
}

public static class HSEscalatorConfig
{
    public static HSEscalatorConfigData Data = new HSEscalatorConfigData();
    public static List<HSEscalatorConfigData> Escalators = new List<HSEscalatorConfigData>();
    public static string ActiveId;

    public static string RuntimeDir
    {
        get
        {
            try
            {
                var save = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(save)) return save;
            }
            catch { }
            return string.IsNullOrEmpty(HSEscalatorMod.UserDataPath) ? "." : HSEscalatorMod.UserDataPath;
        }
    }

    static string FilePath { get { return Path.Combine(RuntimeDir, "HSEscalator.json"); } }

    public static void EvacuateRuntimeFilesFromModFolder()
    {
        var mod = HSEscalatorMod.ModPath;
        var dest = HSEscalatorMod.UserDataPath;
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(dest) || !Directory.Exists(mod)) return;
        Directory.CreateDirectory(dest);
        var from = Path.Combine(mod, "HSEscalator.json");
        var to = Path.Combine(dest, "HSEscalator.json");
        if (!File.Exists(from)) return;
        try
        {
            if (!File.Exists(to)) File.Copy(from, to);
            File.Delete(from);
            HSEscalatorDebug.Info("Moved HSEscalator.json out of Mods so server and clients keep the same folder.");
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Could not move save out of the mod folder", e);
        }
    }

    static void AdoptPendingSaveIfNeeded()
    {
        var pending = string.IsNullOrEmpty(HSEscalatorMod.UserDataPath) ? null : Path.Combine(HSEscalatorMod.UserDataPath, "HSEscalator.json");
        if (string.IsNullOrEmpty(pending) || !File.Exists(pending)) return;
        if (File.Exists(FilePath)) return;
        var dir = RuntimeDir;
        if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(HSEscalatorMod.UserDataPath), StringComparison.OrdinalIgnoreCase)) return;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.Copy(pending, FilePath);
        HSEscalatorDebug.Info("Copied escalator list into this world save.");
    }

    public static void Use(HSEscalatorConfigData d)
    {
        if (d == null) return;
        Data = d;
        ActiveId = d.EscalatorId;
    }

    public static string ToSyncJson()
    {
        var file = new HSEscalatorFile
        {
            ActiveId = ActiveId,
            Debug = HSEscalatorDebug.Enabled,
            MaxDeckCells = HSEscalatorSettings.MaxDeckCells,
            Escalators = Escalators
        };
        return JsonConvert.SerializeObject(file);
    }

    public static void ApplyFromServer(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var file = JsonConvert.DeserializeObject<HSEscalatorFile>(json, settings) ?? new HSEscalatorFile();
            Escalators = file.Escalators != null ? file.Escalators : new List<HSEscalatorConfigData>();
            ActiveId = file.ActiveId;
            if (Escalators.Count == 0) Escalators.Add(new HSEscalatorConfigData());
            foreach (var d in Escalators) Normalize(d);
            Use(ById(ActiveId) ?? Escalators[0]);
            HSEscalatorDebug.Enabled = file.Debug;
            HSEscalatorSettings.ApplyFromServer(file.MaxDeckCells);
            foreach (var d in Escalators)
            {
                if (d.Debug) HSEscalatorDebug.Enabled = true;
                var ctrl = HSEscalatorController.Ensure(d);
                if (ctrl != null) ctrl.RebuildBelt();
            }
            HSEscalatorDebug.Info("Got " + Escalators.Count + " escalator(s) from server. Editing " + Data.EscalatorId);
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Could not apply escalator list from server", e);
        }
    }

    public static void Load()
    {
        if (HSEscalatorNet.IsRemoteClient)
        {
            Escalators = new List<HSEscalatorConfigData>();
            Escalators.Add(new HSEscalatorConfigData());
            Use(Escalators[0]);
            HSEscalatorDebug.Info("Client: waiting for the server escalator list");
            return;
        }
        Escalators = new List<HSEscalatorConfigData>();
        try
        {
            AdoptPendingSaveIfNeeded();
            if (File.Exists(FilePath))
            {
                var raw = File.ReadAllText(FilePath);
                var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
                var file = JsonConvert.DeserializeObject<HSEscalatorFile>(raw, settings) ?? new HSEscalatorFile();
                if (file.Escalators != null) Escalators.AddRange(file.Escalators);
                ActiveId = file.ActiveId;
                HSEscalatorDebug.Enabled = file.Debug;
            }
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Config load failed, using defaults", e);
            Escalators.Clear();
        }
        if (Escalators.Count == 0) Escalators.Add(new HSEscalatorConfigData());
        foreach (var d in Escalators) Normalize(d);
        Use(ById(ActiveId) ?? Escalators[0]);
        foreach (var d in Escalators)
            if (d.Debug) HSEscalatorDebug.Enabled = true;
        Save();
        HSEscalatorDebug.Info("Config loaded: " + Escalators.Count + " escalator(s), active " + Data.EscalatorId);
    }

    static void Normalize(HSEscalatorConfigData d)
    {
        if (d == null) return;
        if (d.Steps == null) d.Steps = new List<HSEscalatorStepCell>();
        if (d.Supports == null) d.Supports = new List<HSEscalatorSupportCell>();
        if (string.IsNullOrEmpty(d.EscalatorId)) d.EscalatorId = "esc1";
        if (d.Speed < 0.15f || d.Speed > 4f) d.Speed = 0.6f;
        if (d.Direction != -1 && d.Direction != 1) d.Direction = 1;
        if (d.Width < 1) d.Width = 1;
        if (d.RunSign == 0) d.RunSign = 1;
        d.Side = string.Equals(d.Side, "metal", StringComparison.OrdinalIgnoreCase) ? "metal" : "glass";
        if (d.Heights != null && d.Heights.Length != d.Length) d.HasDeck = false;
        if (d.HasDeck && !d.Captured && d.Steps != null && d.Steps.Count > 0)
            d.Captured = true;
        if (!string.IsNullOrEmpty(d.StopReason)
            && (d.StopReason.IndexOf("comb", StringComparison.OrdinalIgnoreCase) >= 0
                || d.StopReason.IndexOf("someone", StringComparison.OrdinalIgnoreCase) >= 0
                || d.StopReason.IndexOf("folding", StringComparison.OrdinalIgnoreCase) >= 0
                || d.StopReason.IndexOf("landing", StringComparison.OrdinalIgnoreCase) >= 0))
            d.StopReason = null;
    }

    public static void Save()
    {
        try
        {
            if (Data != null)
            {
                var i = Escalators.FindIndex(e => e.EscalatorId == Data.EscalatorId);
                if (i >= 0) Escalators[i] = Data;
                else if (!Escalators.Contains(Data)) Escalators.Add(Data);
                ActiveId = Data.EscalatorId;
            }
            if (HSEscalatorNet.IsRemoteClient) return;
            var dir = RuntimeDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var file = new HSEscalatorFile
            {
                ActiveId = ActiveId,
                Debug = HSEscalatorDebug.Enabled,
                MaxDeckCells = HSEscalatorSettings.MaxDeckCells,
                Escalators = Escalators
            };
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            HSEscalatorNet.BroadcastConfig();
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Config save failed", e);
        }
    }

    public static HSEscalatorConfigData ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Escalators.Find(e => string.Equals(e.EscalatorId, id, StringComparison.OrdinalIgnoreCase));
    }

    public static HSEscalatorConfigData NewEscalator()
    {
        int n = 1;
        while (ById("esc" + n) != null) n++;
        var d = new HSEscalatorConfigData { EscalatorId = "esc" + n };
        Escalators.Add(d);
        Use(d);
        HSEscalatorController.Ensure(d);
        Save();
        return d;
    }

    public static HSEscalatorConfigData DriveOwner(Vector3i pos)
    {
        foreach (var d in Escalators)
            if (d.IsDrive(pos)) return d;
        return null;
    }

    public static HSEscalatorConfigData At(Vector3i pos)
    {
        var drive = DriveOwner(pos);
        if (drive != null) return drive;
        foreach (var d in Escalators)
            if (d.InDeckXZ(pos.x, pos.z)) return d;
        return null;
    }

    public static string SelectNearest(Vector3i pos)
    {
        var d = At(pos);
        if (d == null) return "No escalator at this block. Start one: New Escalator, then Set End 1 and Set End 2 on the step deck.";
        Use(d);
        Save();
        return "Now editing " + d.EscalatorId + ". " + Summary(d);
    }

    public static string ListAll()
    {
        if (Escalators.Count == 0) return "No escalators.";
        var parts = new List<string>();
        foreach (var d in Escalators)
        {
            var mark = d.EscalatorId == ActiveId ? "* " : "  ";
            parts.Add(mark + Summary(d));
        }
        return "Escalators:\n" + string.Join("\n", parts.ToArray());
    }

    public static string Summary(HSEscalatorConfigData d)
    {
        if (d == null) return "none";
        if (!d.HasDeck)
            return d.EscalatorId + ": no steps yet" + (d.DriveCount > 0 ? " (panel ok)" : " (no panel)");
        var kind = d.IsWalkway ? "walkway" : ("rise " + d.RiseHalf);
        return d.EscalatorId + ": " + d.Length + "x" + d.Width + " " + kind
            + (d.DriveCount > 0 ? "" : " (no panel)")
            + (d.Running ? " running" : "");
    }

    public static string Summary()
    {
        return Summary(Data);
    }
}
