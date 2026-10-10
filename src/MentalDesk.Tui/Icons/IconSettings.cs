namespace MentalDesk.Tui.Icons;

public sealed class IconSettings
{
    private readonly MentalDeskSettings _file;
    private readonly Lazy<FontDetection> _detected;

    public IconSettings(MentalDeskSettings file, Func<FontDetection> detect)
    {
        _file = file;
        _detected = new Lazy<FontDetection>(detect);
        Saved = Style = file.IconStyle;
    }

    public event EventHandler? Changed;

    public IconStyle Saved { get; private set; }

    public IconStyle Style { get; private set; }

    public FontDetection Detected => _detected.Value;

    public IconStyle AutoChoice => Detected.NerdFont ? IconStyle.NerdFont : IconStyle.Plain;

    public IconStyle InUse => Style == IconStyle.Auto ? AutoChoice : Style;

    public string AutoReason => $"Auto chose {Name(AutoChoice)}: {Detected.Reason}";

    public static IconSettings ForThisUser() => new(MentalDeskSettings.ThisUser, TerminalFontDetection.Detect);

    public static string Name(IconStyle style) => style == IconStyle.NerdFont ? "Nerd Font" : style.ToString();

    public string Glyph(Icon icon) => InUse == IconStyle.NerdFont ? icon.NerdFont : icon.Plain;

    public void Preview(IconStyle style)
    {
        if (style == Style) return;
        Style = style;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Keep()
    {
        _file.IconStyle = Style;
        Saved = Style;
    }

    public void Revert() => Preview(Saved);
}
