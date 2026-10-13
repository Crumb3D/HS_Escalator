using UnityEngine.Scripting;

// Right of the crosshair while the setup tool is in hand: the escalator being edited, its steps,
// panels, power, speed and state. Hidden the rest of the time.
[Preserve]
public class HSEscalatorHud : XUiController
{
    string info = "";
    bool show;
    float nextText;

    public override void Update(float _dt)
    {
        base.Update(_dt);
        var player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer as EntityPlayerLocal : null;
        bool holding = ItemActionHSEscalatorTool.IsHolding(player);
        string next = info;
        if (!holding) next = "";
        else if (!show || UnityEngine.Time.unscaledTime >= nextText)
        {
            nextText = UnityEngine.Time.unscaledTime + 0.25f;
            next = HSEscalatorConfig.HoldingHud();
        }
        if (holding != show || next != info)
        {
            show = holding;
            info = next;
            IsDirty = true;
        }
        if (!IsDirty) return;
        RefreshBindings();
        IsDirty = false;
    }

    public override bool GetBindingValueInternal(ref string _value, string _bindingName)
    {
        if (_bindingName == "info") { _value = info ?? ""; return true; }
        if (_bindingName == "show") { _value = show ? "true" : "false"; return true; }
        return base.GetBindingValueInternal(ref _value, _bindingName);
    }
}
