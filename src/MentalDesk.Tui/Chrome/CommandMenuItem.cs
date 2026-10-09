namespace MentalDesk.Tui.Chrome;

// Dimmed rather than disabled: Terminal.Gui's arrows skip a disabled item, so the menu would change shape.
internal sealed class CommandMenuItem : MenuItem
{
    // Terminal.Gui colours the key from the menu rather than the item, so it's dimmed here.
    public CommandMenuItem() =>
        KeyView.GettingAttributeForRole += (_, args) =>
        {
            if (!Dimmed || args.Role != VisualRole.Normal) return;
            args.Result = GetAttributeForRole(VisualRole.Normal);
            args.Handled = true;
        };

    public bool Dimmed
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            SetNeedsDraw();
        }
    }

    protected override bool OnGettingAttributeForRole(in VisualRole role, ref Attribute currentAttribute)
    {
        if (!Dimmed) return base.OnGettingAttributeForRole(role, ref currentAttribute);
        var scheme = GetScheme();
        var disabled = scheme.GetAttributeForRole(VisualRole.Disabled, App?.Driver?.DefaultAttribute);
        currentAttribute = HasFocus
            ? disabled with { Background = scheme.GetAttributeForRole(VisualRole.Focus, App?.Driver?.DefaultAttribute).Background }
            : disabled;
        return true;
    }

    protected override bool OnActivating(CommandEventArgs args) => Dimmed || base.OnActivating(args);

    protected override bool OnAccepting(CommandEventArgs args) => Dimmed || base.OnAccepting(args);
}
