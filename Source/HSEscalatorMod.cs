using System;
using System.IO;
using System.Reflection;
using HarmonyLib;

public class HSEscalatorMod : IModApi
{
    public static string ModPath;
    public static string UserDataPath;

    public void InitMod(Mod _modInstance)
    {
        ModPath = _modInstance.Path;
        try
        {
            UserDataPath = Path.Combine(GameIO.GetUserGameDataDir(), "HSEscalator");
            Directory.CreateDirectory(UserDataPath);
            HSEscalatorConfig.EvacuateRuntimeFilesFromModFolder();
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Could not move escalator save out of the mod folder", e);
        }
        HSEscalatorDebug.Info("Init v1.0.0 - " + HSEscalatorPath.SelfTest() + "; setup tool hold E; admin: hsescalator");
        HSEscalatorNet.RegisterPackage();
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
        ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(HSEscalatorNet.OnPlayerSpawned);
        try
        {
            new Harmony("HSEscalator").PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Harmony patch failed (setup tool hold E will not open)", e);
        }
    }

    static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        try
        {
            HSEscalatorConfig.Load();
            HSEscalatorController.EnsureCreated();
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Start failed", e);
        }
    }

    static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        try
        {
            HSEscalatorController.OnWorldShuttingDown();
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Shutdown handling failed", e);
        }
    }
}
