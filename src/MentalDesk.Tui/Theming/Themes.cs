using Microsoft.Extensions.Configuration;
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
    public static IReadOnlyList<string> Names
    {
        get
        {
            var available = ThemeManager.GetThemeNames();
            return [.. Bundled.Where(available.Contains)];
        }
    }

    public static void Load(string? theme = null)
    {
        TuiConfigurationBuilder.Shared.RuntimeConfig = BundledConfig;
        TuiConfigurationBuilder.Shared.ApplyToStaticFacades();
        Apply(theme ?? Default);
    }

    public static string Apply(string theme)
    {
        ThemeManager.Theme = Names.Contains(theme) ? theme : Default;
        return ThemeManager.Theme;
    }

    public static IReadOnlyDictionary<string, Scheme> SchemesOf(string theme) =>
        TuiConfigurationBuilder.Shared.Configuration.GetSection($"Themes:{theme}:Schemes").GetChildren()
            .ToDictionary(scheme => scheme.Key, ReadScheme);

    public static Color? CursorColour(string theme) =>
        SchemesOf(theme).TryGetValue(SchemeNames.Cursor, out var cursor) ? cursor.Normal.Foreground : null;

    // Terminal.Gui's own scheme reader is internal.
    private static Scheme ReadScheme(IConfigurationSection section) =>
        section.GetChildren().Aggregate(new Scheme(), (scheme, role) =>
        {
            var attribute = new Attribute(role["Foreground"] ?? "", role["Background"] ?? "", role["Style"]);
            return role.Key switch
            {
                "Normal" => scheme with { Normal = attribute },
                "HotNormal" => scheme with { HotNormal = attribute },
                "Focus" => scheme with { Focus = attribute },
                "HotFocus" => scheme with { HotFocus = attribute },
                "Active" => scheme with { Active = attribute },
                "HotActive" => scheme with { HotActive = attribute },
                "Highlight" => scheme with { Highlight = attribute },
                "Editable" => scheme with { Editable = attribute },
                "ReadOnly" => scheme with { ReadOnly = attribute },
                "Disabled" => scheme with { Disabled = attribute },
                _ => scheme,
            };
        });

    private static string ReadBundledConfig()
    {
        using var stream = typeof(Themes).Assembly.GetManifestResourceStream("MentalDesk.Tui.themes.json")
            ?? throw new InvalidOperationException("The bundled themes are missing.");
        return new StreamReader(stream).ReadToEnd();
    }
}
