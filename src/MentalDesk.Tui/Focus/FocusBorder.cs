namespace MentalDesk.Tui.Focus;

// Terminal.Gui draws borders in Normal whatever has focus. Swap the role, not the scheme, which a theme change would undo.
public sealed class FocusBorder
{
    private readonly View _pane;

    public FocusBorder(View pane)
    {
        _pane = pane;
        if (pane.Border?.GetOrCreateView() is not { } border) return;
        border.GettingAttributeForRole += (_, e) =>
        {
            if (!Focused) return;
            var role = e.Role switch
            {
                VisualRole.Normal => VisualRole.Focus,
                VisualRole.HotNormal => VisualRole.HotFocus,
                _ => e.Role,
            };
            if (role == e.Role) return;
            e.Result = _pane.GetAttributeForRole(role);
            e.Handled = true;
        };
    }

    public bool Focused { get; private set; }

    public void Show(bool focused)
    {
        if (focused == Focused) return;
        Focused = focused;
        _pane.SetNeedsDraw();
    }
}
