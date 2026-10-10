using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Tests;

public class TerminalFontDetectionTests
{
    private const string Home = "/home/me";
    private const string ITerm2Plist = $"{Home}/Library/Preferences/com.googlecode.iterm2.plist";
    private const string VsCodeSettings = $"{Home}/Library/Application Support/Code/User/settings.json";
    private const string WindowsTerminalSettings = "/local/Packages/Microsoft.WindowsTerminal_8wekyb3d8bbwe/LocalState/settings.json";
    private const string AlacrittyToml = $"{Home}/.config/alacritty/alacritty.toml";

    private readonly Dictionary<string, string> _variables = [];
    private readonly Dictionary<string, string> _files = [];

    [Theory]
    [InlineData("JetBrainsMono Nerd Font")]
    [InlineData("JetBrainsMonoNerdFontMono-Regular")]
    [InlineData("CaskaydiaCove NF")]
    [InlineData("JetBrainsMonoNFM-Regular")]
    [InlineData("MesloLGS-NF-Regular")]
    [InlineData("'Hack Nerd Font', monospace")]
    public void Nerd_font_names_are_recognised(string font) =>
        Assert.True(TerminalFontDetection.IsNerdFont(font));

    [Theory]
    [InlineData("Menlo-Regular")]
    [InlineData("Cascadia Mono")]
    [InlineData("SF Mono")]
    [InlineData("CONFIGURED")]
    public void Other_fonts_are_not(string font) =>
        Assert.False(TerminalFontDetection.IsNerdFont(font));

    [Theory]
    [InlineData("TERM_PROGRAM", "ghostty", "Ghostty")]
    [InlineData("TERM_PROGRAM", "WezTerm", "WezTerm")]
    [InlineData("TERM", "xterm-kitty", "kitty")]
    [InlineData("KITTY_WINDOW_ID", "1", "kitty")]
    public void Terminals_that_bundle_the_symbols_have_them(string variable, string value, string terminal)
    {
        _variables[variable] = value;

        Assert.Equal(new FontDetection(true, $"{terminal} bundles the Nerd Font symbols"), Detect());
    }

    [Theory]
    [InlineData("tmux", "WEZTERM_PANE", "WezTerm")]
    [InlineData("screen", "GHOSTTY_RESOURCES_DIR", "Ghostty")]
    [InlineData(null, "KITTY_WINDOW_ID", "kitty")]
    public void A_terminal_is_still_recognised_inside_a_multiplexer(string? program, string marker, string terminal)
    {
        if (program is not null) _variables["TERM_PROGRAM"] = program;
        _variables[marker] = "0";

        Assert.Equal(new FontDetection(true, $"{terminal} bundles the Nerd Font symbols"), Detect());
    }

    [Fact]
    public void ITerm2_is_still_read_inside_tmux()
    {
        _variables["TERM_PROGRAM"] = "tmux";
        _variables["ITERM_SESSION_ID"] = "w0t0p0";
        _variables["ITERM_PROFILE"] = "Work";

        Assert.Equal(new FontDetection(true, "iTerm2 uses JetBrainsMonoNFM-Regular"), Detect(ITerm2Preferences));
    }

    [Fact]
    public void An_unrecognised_terminal_is_assumed_not_to_have_them() =>
        Assert.Equal(new FontDetection(false, "couldn't tell which font the terminal uses"), Detect());

    [Theory]
    [InlineData("Work", true, "iTerm2 uses JetBrainsMonoNFM-Regular")]
    [InlineData(null, false, "iTerm2 uses Menlo-Regular, which isn't a Nerd Font")]
    public void ITerm2_uses_the_sessions_profile_or_else_the_default(string? profile, bool nerdFont, string reason)
    {
        _variables["TERM_PROGRAM"] = "iTerm.app";
        if (profile is not null) _variables["ITERM_PROFILE"] = profile;

        Assert.Equal(new FontDetection(nerdFont, reason), Detect(ITerm2Preferences));
    }

    [Fact]
    public void ITerm2s_non_ascii_font_counts_when_it_is_enabled()
    {
        _variables["TERM_PROGRAM"] = "iTerm.app";
        _variables["ITERM_PROFILE"] = "Split";

        Assert.Equal(new FontDetection(true, "iTerm2 uses HackNF-Regular"), Detect(ITerm2Preferences));
    }

    [Fact]
    public void An_unreadable_iterm2_plist_is_not_a_nerd_font()
    {
        _variables["TERM_PROGRAM"] = "iTerm.app";

        Assert.Equal(new FontDetection(false, "couldn't read iTerm2's font"), Detect(_ => null));
    }

    [Theory]
    [InlineData("{61c54bbd-c2c6-5271-96e7-009a87ff44bf}", true, "Windows Terminal uses CaskaydiaCove NF")]
    [InlineData("{00000000-0000-0000-0000-000000000000}", false, "Windows Terminal uses Cascadia Code, which isn't a Nerd Font")]
    public void Windows_terminal_uses_the_sessions_profile_font_or_else_the_defaults(string profileId, bool nerdFont, string reason)
    {
        _variables["WT_SESSION"] = "x";
        _variables["WT_PROFILE_ID"] = profileId;
        _files[WindowsTerminalSettings] = """
            {
                // Comments and trailing commas are allowed.
                "profiles": {
                    "defaults": { "font": { "face": "Cascadia Code" } },
                    "list": [
                        { "guid": "{61C54BBD-C2C6-5271-96E7-009A87FF44BF}", "font": { "face": "CaskaydiaCove NF" } },
                    ],
                },
            }
            """;

        Assert.Equal(new FontDetection(nerdFont, reason), Detect());
    }

    [Fact]
    public void Windows_terminal_without_settings_is_unreadable()
    {
        _variables["WT_SESSION"] = "x";

        Assert.Equal(new FontDetection(false, "couldn't read Windows Terminal's font"), Detect());
    }

    [Fact]
    public void Vs_code_falls_back_through_its_font_list()
    {
        _variables["TERM_PROGRAM"] = "vscode";
        _variables["ITERM_SESSION_ID"] = "leaked from the shell that launched VS Code";
        _files[VsCodeSettings] = """{ "terminal.integrated.fontFamily": "Menlo, 'Symbols Nerd Font Mono'" }""";

        Assert.Equal(new FontDetection(true, "VS Code uses Menlo, 'Symbols Nerd Font Mono'"), Detect());
    }

    [Fact]
    public void Vs_code_without_a_nerd_font_says_which_font_it_uses()
    {
        _variables["TERM_PROGRAM"] = "vscode";

        Assert.Equal(new FontDetection(false, "VS Code uses the default font, which isn't a Nerd Font"), Detect());
    }

    [Theory]
    [InlineData("Hack Nerd Font Mono", true, "Alacritty uses Hack Nerd Font Mono")]
    [InlineData("Menlo", false, "Alacritty uses Menlo, which isn't a Nerd Font")]
    public void Alacritty_reads_the_font_family_from_its_toml(string family, bool nerdFont, string reason)
    {
        _variables["TERM"] = "alacritty";
        _files[AlacrittyToml] = $"""
            [font.normal]
            family = "{family}"
            """;

        Assert.Equal(new FontDetection(nerdFont, reason), Detect());
    }

    private FontDetection Detect(Func<string, string?>? plistToXml = null) =>
        TerminalFontDetection.Detect(new TerminalEnvironment
        {
            Variable = name => _variables.GetValueOrDefault(name),
            Folder = folder => folder switch
            {
                Environment.SpecialFolder.UserProfile => Home,
                Environment.SpecialFolder.LocalApplicationData => "/local",
                _ => "/roaming",
            },
            ReadFile = path => _files.GetValueOrDefault(path.Replace('\\', '/')),
            PlistToXml = plistToXml ?? (_ => null),
            IsMacOS = true,
        });

    private static string? ITerm2Preferences(string path) => path.Replace('\\', '/') != ITerm2Plist ? null : """
        <?xml version="1.0" encoding="UTF-8"?>
        <plist version="1.0">
        <dict>
            <key>Default Bookmark Guid</key>
            <string>A</string>
            <key>New Bookmarks</key>
            <array>
                <dict>
                    <key>Guid</key><string>A</string>
                    <key>Name</key><string>Default</string>
                    <key>Normal Font</key><string>Menlo-Regular 12</string>
                    <key>Use Non-ASCII Font</key><false/>
                    <key>Non Ascii Font</key><string>HackNF-Regular 12</string>
                </dict>
                <dict>
                    <key>Guid</key><string>B</string>
                    <key>Name</key><string>Work</string>
                    <key>Normal Font</key><string>JetBrainsMonoNFM-Regular 13</string>
                </dict>
                <dict>
                    <key>Guid</key><string>C</string>
                    <key>Name</key><string>Split</string>
                    <key>Normal Font</key><string>Menlo-Regular 12</string>
                    <key>Use Non-ASCII Font</key><true/>
                    <key>Non Ascii Font</key><string>HackNF-Regular 12</string>
                </dict>
            </array>
        </dict>
        </plist>
        """;
}
