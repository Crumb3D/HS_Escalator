# HS_Escalator — game calls (from Source)

Not extracted from the game DLL. This is `HS_Escalator/Source`. Unity / System / Harmony / our types skipped.

3.2 vs 3.3: `7DaysRef/HS_Escalator_3.2_vs_3.3.md`

## Inherits
- `Block`
- `BlockPowered`
- `ConsoleCmdAbstract`
- `IModApi`
- `ItemAction`
- `NetPackage`

## HarmonyPatch / typeof game types
- `Entity`
- `EntityAlive`
- `NetPackageManager`
- `PlayerMoveController`
- `string`
- `TextureFullArray`
- `XUiC_InteractionPrompt`

## Type.Method (static / nested)
- `Block.GetBlockName`
- `Block.GetBlockValue`
- `Block.GetLocalizedBlockName`
- `Bound.ToPath`
- `Escalators.Add`
- `Escalators.AddRange`
- `Escalators.Clear`
- `Escalators.Contains`
- `Escalators.Find`
- `Escalators.FindIndex`
- `FieldType.GetGenericArguments`
- `GameIO.GetSaveGameDir`
- `GameIO.GetUserGameDataDir`
- `GameManager.ShowTooltip`
- `GameStartDone.RegisterHandler`
- `Heights.Clone`
- `Instance.GetPowerItemByWorldPos`
- `Instance.Output`
- `ItemClass.GetForId`
- `Localization.Get`
- `Log.Error`
- `Log.Out`
- `Log.Warning`
- `Phase.ToString`
- `PlayerSpawnedInWorld.RegisterHandler`
- `Steps.Clear`
- `StopReason.IndexOf`
- `Supports.Add`
- `Supports.Clear`
- `Text.IndexOf`
- `Text.StringBuilder`
- `Versioning.TargetFrameworkAttribute`
- `WorldShuttingDown.RegisterHandler`
- `XUiC_InteractionPrompt.SetText`
- `XUiC_Radial.RadialContextHoldingSlotIndex`

## new
- `new BlockActivationCommand`
- `new BlockChangeInfo`
- `new BlockValue`
- `new Color32`
- `new string`
- `new Vector3i`

## override (must still exist on 3.3 base)
- `CanInteract`
- `Execute`
- `ExecuteAction`
- `GetActivationText`
- `GetBlockActivationCommands`
- `getCommands`
- `getDescription`
- `getHelp`
- `GetLength`
- `HasBlockActivationCommands`
- `HasRadial`
- `OnBlockActivated`
- `OnBlockDamaged`
- `OnBlockDestroyedBy`
- `OnBlockDestroyedByExplosion`
- `ProcessPackage`
- `read`
- `SetupRadial`
- `write`

## By file (line)
### HSEscalatorBelt.cs
- `113: ItemClass.GetForId`
- `271: new Vector3i`

### HSEscalatorCommands.cs
- `4: inherits ConsoleCmdAbstract`
- `6: override getCommands`
- `11: override getDescription`
- `18: override getHelp`
- `34: override Execute`
- `62: Instance.Output`

### HSEscalatorConfig.cs
- `65: new Vector3i`
- `68: new Vector3i`
- `162: GameIO.GetSaveGameDir`
- `233: Escalators.Add`
- `257: Escalators.Add`
- `271: Escalators.AddRange`
- `279: Escalators.Clear`
- `281: Escalators.Add`
- `304: StopReason.IndexOf`
- `305: StopReason.IndexOf`
- `306: StopReason.IndexOf`
- `307: StopReason.IndexOf`
- `317: Escalators.FindIndex`
- `319: Escalators.Contains`
- `319: Escalators.Add`
- `344: Escalators.Find`
- `352: Escalators.Add`

### HSEscalatorController.cs
- `76: Phase.ToString`
- `111: Localization.Get`
- `168: Bound.ToPath`
- `259: Bound.ToPath`
- `317: Bound.ToPath`
- `340: Bound.ToPath`
- `370: Localization.Get`
- `376: Bound.ToPath`

### HSEscalatorDebug.cs
- `11: Log.Out`
- `16: Log.Out`
- `21: Log.Warning`
- `26: Log.Error`

### HSEscalatorMod.cs
- `6: inherits IModApi`
- `18: GameIO.GetUserGameDataDir`
- `29: GameStartDone.RegisterHandler`
- `30: WorldShuttingDown.RegisterHandler`
- `31: PlayerSpawnedInWorld.RegisterHandler`

### HSEscalatorNet.cs
- `52: FieldType.GetGenericArguments`
- `173: GameManager.ShowTooltip`
- `191: inherits NetPackage`
- `219: override read`
- `229: new Vector3i`
- `232: override write`
- `248: override ProcessPackage`
- `290: override GetLength`
- `296: HarmonyPatch NetPackageManager`

### HSEscalatorPaint.cs
- `175: Text.StringBuilder`

### HSEscalatorPower.cs
- `86: Instance.GetPowerItemByWorldPos`

### HSEscalatorRail.cs
- `63: new Vector3i`
- `391: new Vector3i`
- `393: Block.GetBlockName`
- `648: new Color32`

### HSEscalatorSettings.cs
- `41: GameIO.GetSaveGameDir`

### HSEscalatorSetup.cs
- `11: GameManager.ShowTooltip`
- `12: Instance.Output`
- `227: Steps.Clear`

### HSEscalatorWorld.cs
- `118: new string`
- `125: Block.GetLocalizedBlockName`
- `129: Block.GetBlockName`
- `207: new Vector3i`
- `208: new Vector3i`
- `246: new Vector3i`
- `253: new Vector3i`
- `278: new Vector3i`
- `302: new Vector3i`
- `312: new Vector3i`
- `340: new Vector3i`
- `353: new BlockChangeInfo`
- `367: Heights.Clone`
- `387: new Vector3i`
- `390: new BlockValue`
- `391: new BlockChangeInfo`
- `405: Block.GetBlockValue`
- `406: Block.GetBlockName`
- `421: new Vector3i`
- `432: Supports.Add`
- `438: new BlockChangeInfo`
- `451: Supports.Clear`
- `454: Block.GetBlockValue`
- `458: new Vector3i`
- `462: new BlockChangeInfo`
- `462: new BlockValue`
- `465: Supports.Clear`
- `491: new Vector3i`
- `495: new BlockChangeInfo`
- `511: new Vector3i`
- `519: new Vector3i`
- `524: new Vector3i`
- `553: new Vector3i`
- `584: new Vector3i`
- `625: new BlockValue`

### HSGameVersion.cs
- `20: Log.Error`
- `25: Log.Error`
- `31: Log.Out`

### ItemActionHSEscalatorTool.cs
- `5: inherits ItemAction`
- `45: override HasRadial`
- `50: override ExecuteAction`
- `56: GameManager.ShowTooltip`
- `56: Localization.Get`
- `64: override CanInteract`
- `66: Localization.Get`
- `69: override SetupRadial`
- `78: Localization.Get`
- `79: Localization.Get`
- `80: Localization.Get`
- `81: Localization.Get`
- `82: Localization.Get`
- `83: Localization.Get`
- `84: Localization.Get`
- `85: Localization.Get`
- `89: XUiC_Radial.RadialContextHoldingSlotIndex`

### Blocks\BlockHSEscalatorDrive.cs
- `3: inherits BlockPowered`
- `5: override HasBlockActivationCommands`
- `10: override GetBlockActivationCommands`
- `23: new BlockActivationCommand`
- `24: new BlockActivationCommand`
- `25: new BlockActivationCommand`
- `35: override GetActivationText`
- `41: Localization.Get`
- `43: Localization.Get`
- `46: Localization.Get`
- `47: Localization.Get`
- `56: override OnBlockActivated`
- `61: override OnBlockActivated`
- `75: GameManager.ShowTooltip`

### Blocks\BlockHSEscalatorHide.cs
- `1: inherits Block`
- `3: override OnBlockDamaged`
- `8: override OnBlockDestroyedBy`
- `13: override OnBlockDestroyedByExplosion`

### obj\Release\.NETFramework,Version=v4.8.AssemblyAttributes.cs
- `4: Versioning.TargetFrameworkAttribute`

### Patches\HSEscalatorToolPatch.cs
- `4: HarmonyPatch PlayerMoveController`
- `23: XUiC_InteractionPrompt.SetText`
- `23: Localization.Get`
- `53: XUiC_InteractionPrompt.SetText`
- `62: Localization.Get`
- `79: HarmonyPatch XUiC_InteractionPrompt`
- `86: Localization.Get`
- `88: Text.IndexOf`
- `94: XUiC_InteractionPrompt.SetText`


