using MentalDesk.Tui.Theming;

namespace Swatch;

// Derived, not picked per theme, so a theme added later gets them too.
internal static class SampleSchemes
{
    public const string Added = "Sample.Added";
    public const string Removed = "Sample.Removed";

    public static IReadOnlyList<string> All { get; } = [Added, Removed];

    private const double ReadableContrast = 4.5;

    private static IDisposable? _registration;

    public static void Register() => _registration ??= Themes.AddAppSchemes(Derive);

    public static string Label(string scheme) => scheme[(scheme.IndexOf('.') + 1)..];

    private static IReadOnlyDictionary<string, Scheme> Derive(string theme, IReadOnlyDictionary<string, Scheme> shared)
    {
        var text = shared[SchemeNames.Base].Editable;
        var removed = shared[SchemeNames.Error].Normal.Background;
        var added = new Color(removed.G, removed.R, removed.B);
        return new Dictionary<string, Scheme>
        {
            [Added] = Line(shared[SchemeNames.Base], text, added),
            [Removed] = Line(shared[SchemeNames.Base], text, removed),
        };
    }

    private static Scheme Line(Scheme scheme, Attribute text, Color tint)
    {
        var line = new Attribute(text.Foreground, Readable(tint, text), text.Style);
        return scheme with { Normal = line, Editable = line };
    }

    // Moves the tint towards the editor's own background until the text on it is readable.
    private static Color Readable(Color tint, Attribute text)
    {
        for (var step = 0; step <= 10; step++)
        {
            var background = Mix(tint, text.Background, step / 10.0);
            if (Contrast(text.Foreground, background) >= ReadableContrast)
                return background;
        }
        return text.Background;
    }

    private static Color Mix(Color from, Color to, double amount) => new(
        (int)Math.Round(from.R + (to.R - from.R) * amount),
        (int)Math.Round(from.G + (to.G - from.G) * amount),
        (int)Math.Round(from.B + (to.B - from.B) * amount));

    public static double Contrast(Color a, Color b)
    {
        var (lighter, darker) = (Luminance(a), Luminance(b)) is var (x, y) && x > y ? (x, y) : (y, x);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color colour) =>
        0.2126 * Linear(colour.R) + 0.7152 * Linear(colour.G) + 0.0722 * Linear(colour.B);

    private static double Linear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
