using System;
using System.IO;
using Newtonsoft.Json;

public class HSEscalatorSettingsData
{
    // 0 = no cap. Host / dedicated server only.
    public int MaxDeckCells;
}

public static class HSEscalatorSettings
{
    public static int MaxDeckCells;

    static string FileName { get { return "HSEscalatorSettings.json"; } }

    static string UserFile
    {
        get
        {
            var dir = HSEscalatorMod.UserDataPath;
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, FileName);
        }
    }

    static string ModFile
    {
        get
        {
            var dir = HSEscalatorMod.ModPath;
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, FileName);
        }
    }

    static string SaveFile
    {
        get
        {
            try
            {
                var save = GameIO.GetSaveGameDir();
                if (!string.IsNullOrEmpty(save)) return Path.Combine(save, FileName);
            }
            catch { }
            return null;
        }
    }

    public static void Load()
    {
        if (HSEscalatorNet.IsRemoteClient)
        {
            HSEscalatorDebug.Info("Client: deck cap comes from the server, local file ignored");
            return;
        }
        MaxDeckCells = 0;
        TryRead(ModFile);
        TryRead(UserFile);
        TryRead(SaveFile);
        if (MaxDeckCells < 0) MaxDeckCells = 0;
        HSEscalatorDebug.Info("Deck cap " + Describe());
    }

    public static void ApplyFromServer(int cells)
    {
        if (cells < 0) cells = 0;
        MaxDeckCells = cells;
        HSEscalatorDebug.Info("Deck cap from server: " + Describe());
    }

    public static string SetMaxDeckCells(int cells)
    {
        if (HSEscalatorNet.IsRemoteClient)
            return "Deck cap is host-only. Run this on the server, or in single player.";
        if (cells < 0) cells = 0;
        MaxDeckCells = cells;
        try
        {
            var path = UserFile;
            if (string.IsNullOrEmpty(path))
                path = ModFile;
            if (string.IsNullOrEmpty(path))
                return "Could not write settings.";
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonConvert.SerializeObject(new HSEscalatorSettingsData { MaxDeckCells = MaxDeckCells }, Formatting.Indented));
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Could not save escalator settings", e);
            return "Deck cap is " + Describe() + " but the file did not save: " + e.Message;
        }
        HSEscalatorNet.BroadcastConfig();
        return "Deck cap is now " + Describe() + ". Host value overrides every client.";
    }

    public static string RejectIfOverCap(int length, int width)
    {
        if (MaxDeckCells <= 0) return null;
        int cells = length * width;
        if (cells <= MaxDeckCells) return null;
        return "Deck is " + length + "x" + width + " (" + cells + " steps). This host caps decks at " + MaxDeckCells + " cells.";
    }

    public static string Describe()
    {
        return MaxDeckCells <= 0 ? "off (no cap)" : MaxDeckCells + " step cells";
    }

    static void TryRead(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var data = JsonConvert.DeserializeObject<HSEscalatorSettingsData>(File.ReadAllText(path));
            if (data == null) return;
            MaxDeckCells = data.MaxDeckCells;
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Warn("Could not read " + path + ": " + e.Message);
        }
    }
}
