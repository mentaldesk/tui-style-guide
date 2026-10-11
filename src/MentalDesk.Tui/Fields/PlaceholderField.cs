namespace MentalDesk.Tui.Fields;

public sealed class PlaceholderField : TextField
{
    public string Placeholder { get; set; } = string.Empty;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        if (Text.Length == 0 && Placeholder.Length > 0)
        {
            SetAttribute(GetAttributeForRole(VisualRole.ReadOnly));
            Move(0, 0);
            AddStr(Placeholder.Length > Viewport.Width ? Placeholder[..Viewport.Width] : Placeholder);
            SetAttribute(GetAttributeForRole(VisualRole.Normal));
        }
        return true;
    }
}
