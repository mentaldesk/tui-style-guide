using System.Text;
using Microsoft.Extensions.Configuration;
using Terminal.Gui.Configuration;

namespace MentalDesk.Tui.Theming;

public delegate IReadOnlyDictionary<string, Scheme> AppSchemes(string theme, IReadOnlyDictionary<string, Scheme> shared);

public static class Themes
{
    public const string Midnight = "Midnight";
    public const string Daylight = "Daylight";
    public const string TurboPascal = "Turbo Pascal";
    public const string ModernBorland = "Modern Borland";

    public const string Default = Midnight;

    public static IReadOnlyList<string> Bundled { get; } = [Midnight, Daylight, TurboPascal, ModernBorland];

    public static string BundledConfig { get; } = ReadBundledConfig();

    private static readonly Lazy<IConfiguration> BundledConfiguration = new(() =>
        new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(BundledConfig))).Build());

    private static readonly List<AppSchemes> Registered = [];

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
        SchemesIn(TuiConfigurationBuilder.Shared.Configuration, theme);

    // Runs for every theme applied from now on; an app scheme may not take a shared scheme's name.
    public static IDisposable AddAppSchemes(AppSchemes derive)
    {
        foreach (var theme in Bundled)
            Derive(derive, theme, SchemesIn(BundledConfiguration.Value, theme));
        if (Registered.Count == 0)
            ThemeManager.ThemeChanged += OnThemeChanged;
        Registered.Add(derive);
        Publish();
        return new Registration(derive);
    }

    public static IReadOnlyDictionary<string, Scheme> AppSchemesOf(string theme)
    {
        var shared = SchemesOf(theme);
        return shared.Count == 0
            ? new Dictionary<string, Scheme>()
            : Registered.SelectMany(derive => Derive(derive, theme, shared)).ToDictionary();
    }

    private static IReadOnlyDictionary<string, Scheme> Derive(AppSchemes derive, string theme, IReadOnlyDictionary<string, Scheme> shared)
    {
        var schemes = derive(theme, shared);
        if (schemes.Keys.FirstOrDefault(IsShared) is { } name)
            throw new ArgumentException($"\"{name}\" is a shared scheme, so an app can't add its own by that name.", nameof(derive));
        return schemes;
    }

    private static bool IsShared(string name) =>
        SchemeNames.All.Concat(Enum.GetNames<Terminal.Gui.Drawing.Schemes>()).Contains(name, StringComparer.OrdinalIgnoreCase);

    private static void OnThemeChanged(object? sender, EventArgs e) => Publish();

    private static void Publish()
    {
        foreach (var (name, scheme) in AppSchemesOf(Current))
            SchemeManager.AddScheme(name, scheme);
    }

    private static IReadOnlyDictionary<string, Scheme> SchemesIn(IConfiguration configuration, string theme) =>
        configuration.GetSection($"Themes:{theme}:Schemes").GetChildren().ToDictionary(scheme => scheme.Key, ReadScheme);

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

    private sealed class Registration(AppSchemes derive) : IDisposable
    {
        public void Dispose()
        {
            var shared = SchemesOf(Current);
            var names = shared.Count == 0 ? [] : derive(Current, shared).Keys;
            if (!Registered.Remove(derive)) return;
            if (Registered.Count == 0)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            foreach (var name in names.Where(name => SchemeManager.TryGetScheme(name, out _)))
                SchemeManager.RemoveScheme(name);
        }
    }
}
