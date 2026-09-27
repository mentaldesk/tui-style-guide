using Terminal.Gui.Configuration;

namespace MentalDesk.Tui.Theming;

public static class Themes
{
    public const string Midnight = "Midnight";
    public const string Daylight = "Daylight";
    public const string TurboPascal = "Turbo Pascal";
    public const string ModernBorland = "Modern Borland";

    public const string Default = Midnight;

    public static IReadOnlyList<string> Bundled { get; } = [Midnight, Daylight, TurboPascal, ModernBorland];

    public static string BundledConfig { get; } = ReadBundledConfig();

    public static string Current => ThemeManager.Theme;

    // Terminal.Gui's own themes are hidden: they have no cursor colour.
    public static IReadOnlyList<string> Names =>
        [.. Bundled.Where(name => ThemeManager.Themes?.ContainsKey(name) == true)];

    // ~/.tui and TUI_CONFIG are left out: an app keeps its own settings.
    public static void Load(string? theme = null)
    {
        ConfigurationManager.RuntimeConfig = BundledConfig;
        ConfigurationManager.Enable(ConfigLocations.HardCoded | ConfigLocations.LibraryResources | ConfigLocations.Runtime);
        Apply(theme ?? Default);
    }

    public static string Apply(string theme)
    {
        ThemeManager.Theme = Names.Contains(theme) ? theme : Default;
        ConfigurationManager.Apply();
        return ThemeManager.Theme;
    }

    public static IReadOnlyDictionary<string, Scheme> SchemesOf(string theme) =>
        ThemeManager.Themes?.TryGetValue(theme, out var scope) == true
        && scope.TryGetValue("Schemes", out var property)
        && property.PropertyValue is Dictionary<string, Scheme?> schemes
            ? schemes.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!)
            : new Dictionary<string, Scheme>();

    public static Color? CursorColour(string theme) =>
        SchemesOf(theme).TryGetValue(SchemeNames.Cursor, out var cursor) ? cursor.Normal.Foreground : null;

    private static string ReadBundledConfig()
    {
        using var stream = typeof(Themes).Assembly.GetManifestResourceStream("MentalDesk.Tui.themes.json")
            ?? throw new InvalidOperationException("The bundled themes are missing.");
        return new StreamReader(stream).ReadToEnd();
    }
}
