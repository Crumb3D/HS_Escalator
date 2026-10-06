using System;
using UnityEngine.Scripting;

[Preserve]
public class ItemActionHSEscalatorTool : ItemAction
{
    public static bool IsHolding(EntityPlayerLocal player)
    {
        if (player == null || player.inventory == null) return false;
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return false;
        foreach (var a in holding.Actions)
            if (a is ItemActionHSEscalatorTool) return true;
        return false;
    }

    public static bool IsBuildBlock(WorldRayHitInfo hit)
    {
        if (hit == null || !hit.bHitValid) return false;
        var world = GameManager.Instance.World;
        if (world == null) return false;
        var bv = world.GetBlock(hit.hit.blockPos);
        if (bv.isair) return false;
        var b = bv.Block;
        if (b == null || b.shape == null || b.shape.IsTerrain()) return false;
        return true;
    }

    public static void OpenRadial(EntityPlayerLocal player)
    {
        if (player == null || player.playerUI == null || player.playerUI.xui == null) return;
        var radial = player.playerUI.xui.RadialWindow;
        if (radial == null || radial.IsOpen) return;
        radial.Open();
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return;
        foreach (var a in holding.Actions)
        {
            if (!(a is ItemActionHSEscalatorTool)) continue;
            a.SetupRadial(radial, player);
            return;
        }
    }

    public override bool HasRadial()
    {
        return true;
    }

    public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
    {
        if (!_bReleased) return;
        try
        {
            var player = _actionData != null && _actionData.invData != null ? _actionData.invData.holdingEntity as EntityPlayerLocal : null;
            if (player != null) GameManager.ShowTooltip(player, Localization.Get("hsescalatorToolHint"));
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Setup tool click failed", e);
        }
    }

    public override string CanInteract(ItemActionData _actionData)
    {
        return Localization.Get("hsescalatorToolHint");
    }

    public override void SetupRadial(XUiC_Radial radial, EntityPlayerLocal player)
    {
        try
        {
            radial.ResetRadialEntries();
            var d = HSEscalatorConfig.Data ?? new HSEscalatorConfigData();
            bool needEnd1 = d.End1 == null;
            bool needEnd2 = d.End2 == null;
            bool needDrive = d.DriveCount < 2;
            Add(radial, 0, "ui_game_symbol_assemble", Localization.Get("hsescalatorRadialNew"), false);
            Add(radial, 1, "ui_game_symbol_map", Localization.Get("hsescalatorRadialUse"), false);
            Add(radial, 2, "ui_game_symbol_map_waypoint_set", Localization.Get("hsescalatorRadialEnd1"), needEnd1);
            Add(radial, 3, "ui_game_symbol_map_cursor", Localization.Get("hsescalatorRadialEnd2"), needEnd2);
            Add(radial, 4, "ui_game_symbol_lightbulb", Localization.Get("hsescalatorRadialDrive"), needDrive);
            Add(radial, 5, "ui_game_symbol_book", Localization.Get("hsescalatorRadialJog"), false);
            Add(radial, 6, "ui_game_symbol_lock", Localization.Get("hsescalatorRadialReverse"), false);
            Add(radial, 7, "ui_game_symbol_x", Localization.Get("hsescalatorRadialForget"), false);
            radial.SetCommonData(
                default(GUI_2.UIUtils.ButtonIcon),
                HandleCommand,
                new XUiC_Radial.RadialContextHoldingSlotIndex(player.inventory.holdingItemIdx),
                -1,
                false,
                StillHolding);
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Setup tool radial failed", e);
        }
    }

    static void Add(XUiC_Radial radial, int i, string icon, string label, bool needsSetup)
    {
        radial.CreateRadialEntry(i, icon, "UIAtlas", "", label, false);
        try
        {
            if (radial == null || radial.menuItem == null || i < 0 || i >= radial.menuItem.Length) return;
            var entry = radial.menuItem[i];
            if (entry == null) return;
            var sprites = entry.GetChildrenByViewType<XUiV_Sprite>();
            if (sprites == null) return;
            for (int s = 0; s < sprites.Length; s++)
            {
                if (sprites[s] == null || sprites[s].ID != "hsliftNeedSetup") continue;
                sprites[s].IsVisible = needsSetup;
                return;
            }
        }
        catch { }
    }

    static bool StillHolding(XUiC_Radial radial, XUiC_Radial.RadialContextAbs context)
    {
        var ctx = context as XUiC_Radial.RadialContextHoldingSlotIndex;
        var player = radial != null && radial.xui != null && radial.xui.playerUI != null ? radial.xui.playerUI.entityPlayer : null;
        if (player == null || ctx == null || player.inventory.holdingItemIdx != ctx.ItemSlotIndex) return false;
        var holding = player.inventory.holdingItem;
        if (holding == null || holding.Actions == null) return false;
        foreach (var a in holding.Actions)
            if (a is ItemActionHSEscalatorTool) return true;
        return false;
    }

    static void HandleCommand(XUiC_Radial sender, int commandIndex, XUiC_Radial.RadialContextAbs context)
    {
        try
        {
            var player = sender.xui.playerUI.entityPlayer;
            string msg;
            switch (commandIndex)
            {
                case 0: msg = HSEscalatorSetup.Execute("new", "", player); break;
                case 1: msg = HSEscalatorSetup.Execute("select", "", player); break;
                case 2: msg = HSEscalatorSetup.Execute("end1", "", player); break;
                case 3: msg = HSEscalatorSetup.Execute("end2", "", player); break;
                case 4: msg = HSEscalatorSetup.Execute("drive", "", player); break;
                case 5: msg = HSEscalatorSetup.Execute("jog", "", player); break;
                case 6: msg = HSEscalatorSetup.Execute("reverse", "", player); break;
                case 7: msg = HSEscalatorSetup.Execute("forget", "", player); break;
                default: return;
            }
            HSEscalatorSetup.Tell(player, msg);
        }
        catch (Exception e)
        {
            HSEscalatorDebug.Error("Setup tool failed", e);
        }
    }
}
