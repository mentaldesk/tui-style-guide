using System.Text.Json;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Tests;

public class MacKeysTests : StaticConfigurationTest
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), $"mentaldesk-{Guid.NewGuid():N}");
    private readonly List<string> _written = [];

    public override void Dispose()
    {
        if (Directory.Exists(_home))
            Directory.Delete(_home, recursive: true);
        base.Dispose();
    }

    private MacKeys Keys(string app = "Swatch", params (string, string)[] terminal) =>
        new(app, FakeTerminal.With(_home, terminal.Length > 0 ? terminal : [FakeTerminal.Iterm2]), _written.Add);

    private static TerminalIntegration In(MacKeys keys, string terminal) => keys.Integrations.Single(i => i.Terminal == terminal);

    public static TheoryData<string, string> Paths => new()
    {
        { "iTerm2", "Library/Application Support/iTerm2/DynamicProfiles/swatch.json" },
        { "kitty", ".config/kitty/swatch.conf" },
        { "WezTerm", ".config/wezterm/swatch.lua" },
    };

    public static TheoryData<string> Terminals => ["iTerm2", "kitty", "WezTerm"];

    [Theory]
    [MemberData(nameof(Paths))]
    public void Installing_writes_a_file_under_the_apps_name(string terminal, string path)
    {
        var integration = In(Keys(), terminal);

        integration.Install();

        Assert.Equal(Path.Combine([_home, .. path.Split('/')]), integration.Path);
        Assert.True(File.Exists(integration.Path));
    }

    [Theory]
    [MemberData(nameof(Terminals))]
    public void It_is_not_installed_then_installed_then_removed(string terminal)
    {
        var integration = In(Keys(), terminal);
        var states = new List<MacKeysState> { integration.State };

        integration.Install();
        states.Add(integration.State);
        integration.Install();
        states.Add(integration.State);
        integration.Remove();
        states.Add(integration.State);
        integration.Remove();

        Assert.Equal([MacKeysState.NotInstalled, MacKeysState.Installed, MacKeysState.Installed, MacKeysState.NotInstalled], states);
        Assert.False(File.Exists(integration.Path));
    }

    [Theory]
    [InlineData("iTerm2", """{ "Profiles": [ { "MentalDeskKeysVersion": 0 } ] }""")]
    [InlineData("iTerm2", """{ "Profiles": [ { "TuiCodeIntegrationVersion": 4 } ] }""")]
    [InlineData("iTerm2", "not json")]
    [InlineData("kitty", "# MentalDeskKeysVersion: 0\n")]
    [InlineData("kitty", "map cmd+c send_text all \\x03\n")]
    [InlineData("WezTerm", "-- MentalDeskKeysVersion: 0\n")]
    public void An_older_or_unreadable_file_is_out_of_date_and_installing_updates_it(string terminal, string old)
    {
        var integration = In(Keys(), terminal);
        Directory.CreateDirectory(Path.GetDirectoryName(integration.Path)!);
        File.WriteAllText(integration.Path, old);

        var before = integration.State;
        integration.Install();

        Assert.Equal(MacKeysState.OutOfDate, before);
        Assert.Equal(MacKeysState.Installed, integration.State);
    }

    [Theory]
    [MemberData(nameof(Terminals))]
    public void Two_apps_installs_and_removals_leave_each_other_alone(string terminal)
    {
        var swatch = In(Keys("Swatch"), terminal);
        var tuicode = In(Keys("TuiCode"), terminal);
        tuicode.Install();
        var theirs = File.ReadAllText(tuicode.Path);

        swatch.Install();
        var both = (swatch.State, tuicode.State);
        swatch.Remove();

        Assert.NotEqual(swatch.Path, tuicode.Path);
        Assert.Equal((MacKeysState.Installed, MacKeysState.Installed), both);
        Assert.Equal(theirs, File.ReadAllText(tuicode.Path));
        tuicode.Remove();
        swatch.Install();
        Assert.Equal(MacKeysState.Installed, swatch.State);
    }

    [Fact]
    public void Two_apps_have_their_own_iTerm2_profile_and_their_own_running_marker()
    {
        var swatch = Keys("Swatch");
        var tuicode = Keys("TuiCode");
        var swatchProfile = (Iterm2Integration)In(swatch, "iTerm2");
        var tuicodeProfile = (Iterm2Integration)In(tuicode, "iTerm2");

        Assert.NotEqual(swatchProfile.Guid, tuicodeProfile.Guid);
        Assert.Equal(swatchProfile.Guid, ((Iterm2Integration)In(Keys("Swatch"), "iTerm2")).Guid);
        Assert.Equal(["&Swatch*", "&swatch*"], swatchProfile.BoundHosts);
        Assert.Equal(["&TuiCode*", "&tuicode*"], tuicodeProfile.BoundHosts);
        Assert.Equal("SWATCH_ACTIVE", swatch.Variable);
        Assert.Equal("TUICODE_ACTIVE", tuicode.Variable);
        Assert.Contains("var:SWATCH_ACTIVE=1", In(swatch, "kitty").Contents, StringComparison.Ordinal);
        Assert.DoesNotContain("TUICODE", In(swatch, "kitty").Contents, StringComparison.Ordinal);
        Assert.Contains("name ~= 'SWATCH_ACTIVE'", In(swatch, "WezTerm").Contents, StringComparison.Ordinal);
        Assert.Contains("config.key_tables.swatch =", In(swatch, "WezTerm").Contents, StringComparison.Ordinal);
    }

    [Fact]
    public void The_iTerm2_profile_is_TuiCodes_key_map_with_Cmd_V_left_to_iTerm2()
    {
        var integration = In(Keys(), "iTerm2");
        integration.Install();

        using var json = JsonDocument.Parse(File.ReadAllText(integration.Path));
        var profile = json.RootElement.GetProperty("Profiles")[0];
        var map = profile.GetProperty("Keyboard Map").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

        Assert.Equal("Swatch", profile.GetProperty("Name").GetString());
        Assert.Equal(25, map.Count);
        Assert.DoesNotContain("0x76-0x100000", map.Keys);
        Assert.Equal((11, "0x19"), Binding(map["0x5a-0x120000"]));
        Assert.Equal((10, "[127;6u"), Binding(map["0x7f-0x100000"]));
        Assert.Equal((10, "[H"), Binding(map["0xf702-0x300000"]));
        Assert.Equal((11, "0x03"), Binding(map["0x63-0x100000"]));
    }

    [Fact]
    public void The_kitty_and_WezTerm_files_send_TuiCodes_bytes_for_each_key()
    {
        var keys = Keys();
        var kitty = In(keys, "kitty").Contents;
        var wezterm = In(keys, "WezTerm").Contents;

        Assert.Equal(27, kitty.Split('\n').Count(line => line.StartsWith("map ", StringComparison.Ordinal)));
        Assert.Contains(@"map --when-focus-on var:SWATCH_ACTIVE=1 cmd+backspace send_text all \x1b[127;6u", kitty, StringComparison.Ordinal);
        Assert.Contains(@"map --when-focus-on var:SWATCH_ACTIVE=1 cmd+shift+z send_text all \x19", kitty, StringComparison.Ordinal);
        Assert.Equal(27, wezterm.Split('\n').Count(line => line.TrimStart().StartsWith("{ key = ", StringComparison.Ordinal)));
        Assert.Contains(@"{ key = 'LeftArrow', mods = 'OPT', action = wezterm.action.SendString '\x1b[1;5D' },", wezterm, StringComparison.Ordinal);
        Assert.Equal("include swatch.conf", In(keys, "kitty").ConfigLine);
        Assert.Equal("require 'swatch'.apply(config)", In(keys, "WezTerm").ConfigLine);
        Assert.Null(In(keys, "iTerm2").ConfigLine);
    }

    [Theory]
    [InlineData("iTerm2", "TERM_PROGRAM", "iTerm.app")]
    [InlineData("WezTerm", "TERM_PROGRAM", "WezTerm")]
    [InlineData("kitty", "TERM", "xterm-kitty")]
    [InlineData("kitty", "KITTY_WINDOW_ID", "1")]
    public void It_finds_the_terminal_it_runs_in(string terminal, string variable, string value)
    {
        var keys = Keys("Swatch", (variable, value));

        Assert.Equal(terminal, keys.Here?.Terminal);
        Assert.Equal(terminal, keys.Terminal);
    }

    [Theory]
    [InlineData("Terminal.app", "Apple_Terminal")]
    [InlineData("ghostty", "ghostty")]
    public void Elsewhere_there_is_nothing_to_install_and_it_says_which_terminal(string name, string program)
    {
        var keys = Keys("Swatch", ("TERM_PROGRAM", program), ("KITTY_WINDOW_ID", "1"));

        Assert.Null(keys.Here);
        Assert.Equal(name, keys.Terminal);
    }

    [Fact]
    public void Off_a_Mac_no_terminal_is_supported()
    {
        var keys = new MacKeys("Swatch", FakeTerminal.With(_home), _written.Add);

        Assert.Null(keys.Here);
        Assert.False(keys.OnAMac);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tui code")]
    [InlineData("../swatch")]
    [InlineData("1swatch")]
    public void An_app_name_is_letters_and_digits(string app) =>
        Assert.Throws<ArgumentException>(() => Keys(app));

    [Fact]
    public void Activating_marks_the_app_running_until_disposed_and_off_while_suspended()
    {
        var keys = Keys();
        var during = new List<string>();

        var active = keys.Activate();
        keys.WhileSuspended(() => during.AddRange(_written));
        active.Dispose();
        active.Dispose();

        Assert.Equal(["\e]1337;SetUserVar=SWATCH_ACTIVE=MQ==\a", "\e]1337;SetUserVar=SWATCH_ACTIVE=MA==\a"], during);
        Assert.Equal([.. during, "\e]1337;SetUserVar=SWATCH_ACTIVE=MQ==\a", "\e]1337;SetUserVar=SWATCH_ACTIVE=MA==\a"], _written);
    }

    private static (int, string?) Binding(JsonElement binding) =>
        (binding.GetProperty("Action").GetInt32(), binding.GetProperty("Text").GetString());
}
