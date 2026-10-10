using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MentalDesk.Tui.Icons;

// No terminal reports its font, so this is a best guess.
public static partial class TerminalFontDetection
{
    public static FontDetection Detect() => Detect(TerminalEnvironment.ThisMachine);

    internal static FontDetection Detect(TerminalEnvironment env)
    {
        var termProgram = env.Variable("TERM_PROGRAM");
        bool Is(string program, string marker) =>
            string.Equals(termProgram, program, StringComparison.OrdinalIgnoreCase)
            // Terminal multiplexers replace TERM_PROGRAM, but these survive.
            || (termProgram is null or "tmux" or "screen" && env.Variable(marker) is not null);

        if (string.Equals(termProgram, "vscode", StringComparison.Ordinal))
            return FromFonts("VS Code", VsCodeFonts(env));
        if (Is("ghostty", "GHOSTTY_RESOURCES_DIR"))
            return Bundled("Ghostty");
        if (Is("WezTerm", "WEZTERM_PANE"))
            return Bundled("WezTerm");
        if (env.Variable("TERM") == "xterm-kitty" || Is("kitty", "KITTY_WINDOW_ID"))
            return Bundled("kitty");
        if (Is("iTerm.app", "ITERM_SESSION_ID"))
            return FromFonts("iTerm2", Iterm2Fonts(env));
        if (env.Variable("WT_SESSION") is not null)
            return FromFonts("Windows Terminal", WindowsTerminalFonts(env));
        if (env.Variable("TERM") == "alacritty" || Is("alacritty", "ALACRITTY_WINDOW_ID"))
            return FromFonts("Alacritty", AlacrittyFonts(env));

        return new FontDetection(false, "couldn't tell which font the terminal uses");
    }

    // Family names say "Nerd Font"; short and PostScript names end in NF, NFM or NFP.
    public static bool IsNerdFont(string font) => NerdFontName().IsMatch(font);

    [GeneratedRegex(@"(?i:nerd ?font)|NF[MP]?\b")]
    private static partial Regex NerdFontName();

    private static FontDetection Bundled(string terminal) => new(true, $"{terminal} bundles the Nerd Font symbols");

    private static FontDetection FromFonts(string terminal, IReadOnlyList<string>? fonts)
    {
        if (fonts is null or [])
            return new FontDetection(false, $"couldn't read {terminal}'s font");
        if (fonts.FirstOrDefault(IsNerdFont) is { } nerd)
            return new FontDetection(true, $"{terminal} uses {nerd}");
        return new FontDetection(false, $"{terminal} uses {fonts[0]}, which isn't a Nerd Font");
    }

    private static List<string>? Iterm2Fonts(TerminalEnvironment env)
    {
        var path = Path.Combine(env.Folder(Environment.SpecialFolder.UserProfile), "Library", "Preferences", "com.googlecode.iterm2.plist");
        if (env.PlistToXml(path) is not { } xml) return null;

        Dictionary<string, XElement> preferences;
        try
        {
            preferences = PlistDict(XDocument.Parse(xml).Root?.Element("dict"));
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var profiles = preferences.GetValueOrDefault("New Bookmarks")?.Elements("dict").Select(PlistDict).ToList() ?? [];
        var name = env.Variable("ITERM_PROFILE");
        var defaultGuid = preferences.GetValueOrDefault("Default Bookmark Guid")?.Value;
        var profile = profiles.FirstOrDefault(p => name is not null && p.GetValueOrDefault("Name")?.Value == name)
            ?? profiles.FirstOrDefault(p => p.GetValueOrDefault("Guid")?.Value == defaultGuid);
        if (profile is null) return null;

        // Stored as "<PostScript name> <size>".
        static string Font(string value) => value.LastIndexOf(' ') is > 0 and var space ? value[..space] : value;

        var fonts = new List<string>();
        if (profile.GetValueOrDefault("Normal Font")?.Value is { } normal) fonts.Add(Font(normal));
        // Glyphs outside ASCII, the icons included, come from this font when it's enabled.
        if (profile.GetValueOrDefault("Use Non-ASCII Font")?.Name == "true" && profile.GetValueOrDefault("Non Ascii Font")?.Value is { } nonAscii)
            fonts.Add(Font(nonAscii));
        return fonts;
    }

    private static Dictionary<string, XElement> PlistDict(XElement? dict)
    {
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        var children = dict?.Elements().ToList() ?? [];
        for (var i = 0; i + 1 < children.Count; i += 2)
        {
            if (children[i].Name == "key")
                result[children[i].Value] = children[i + 1];
        }
        return result;
    }

    private static List<string>? WindowsTerminalFonts(TerminalEnvironment env)
    {
        var local = env.Folder(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        [
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(local, "Microsoft", "Windows Terminal", "settings.json"),
        ];
        if (ParseJson(candidates.Select(env.ReadFile).FirstOrDefault(text => text is not null))?["profiles"] is not { } profiles)
            return null;

        static string? Face(JsonNode? profile) => String(profile?["font"]?["face"]) ?? String(profile?["fontFace"]);

        var list = profiles as JsonArray ?? profiles["list"] as JsonArray;
        var id = env.Variable("WT_PROFILE_ID");
        var profile = list?.FirstOrDefault(p => id is not null && string.Equals(String(p?["guid"]), id, StringComparison.OrdinalIgnoreCase));
        var face = Face(profile) ?? Face((profiles as JsonObject)?["defaults"]) ?? "Cascadia Mono";
        return [face];
    }

    private static List<string> VsCodeFonts(TerminalEnvironment env)
    {
        var home = env.Folder(Environment.SpecialFolder.UserProfile);
        var user = env.IsWindows ? Path.Combine(env.Folder(Environment.SpecialFolder.ApplicationData), "Code", "User")
            : env.IsMacOS ? Path.Combine(home, "Library", "Application Support", "Code", "User")
            : Path.Combine(home, ".config", "Code", "User");
        var settings = ParseJson(env.ReadFile(Path.Combine(user, "settings.json")));
        // A CSS font list, and the terminal falls back through it glyph by glyph, so any Nerd Font in it counts.
        var family = String(settings?["terminal.integrated.fontFamily"]) ?? String(settings?["editor.fontFamily"]);
        return family is null ? ["the default font"] : [family];
    }

    private static List<string> AlacrittyFonts(TerminalEnvironment env)
    {
        var home = env.Folder(Environment.SpecialFolder.UserProfile);
        var xdg = env.Variable("XDG_CONFIG_HOME") is { Length: > 0 } x ? x : Path.Combine(home, ".config");
        string[] candidates =
        [
            Path.Combine(xdg, "alacritty", "alacritty.toml"),
            Path.Combine(env.Folder(Environment.SpecialFolder.ApplicationData), "alacritty", "alacritty.toml"),
            Path.Combine(home, ".alacritty.toml"),
        ];
        if (candidates.Select(env.ReadFile).FirstOrDefault(text => text is not null) is not { } toml) return ["the default font"];

        var families = TomlFamily().Matches(toml).Select(m => m.Groups[1].Value).ToList();
        return families.Count > 0 ? families : ["the default font"];
    }

    [GeneratedRegex("""^\s*family\s*=\s*["']([^"']+)["']""", RegexOptions.Multiline)]
    private static partial Regex TomlFamily();

    private static JsonNode? ParseJson(string? text)
    {
        if (text is null) return null;
        try
        {
            return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? String(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;
}
