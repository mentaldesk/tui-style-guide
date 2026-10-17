using System.Text.Json.Nodes;
using MentalDesk.Tui.Keys;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class MenuKeyWidthTests : StaticConfigurationTest
{
    private static readonly string[] AsBuilt =
    [
        "┌──────────────┐",
        "│ Settings…    │",
        "├──────────────┤",
        "│ Quit  Ctrl+Q │",
        "└──────────────┘",
    ];

    private static readonly string[] SettingsRebound =
    [
        "┌─────────────────────┐",
        "│ Settings…  Ctrl+T X │",
        "├─────────────────────┤",
        "│ Quit         Ctrl+Q │",
        "└─────────────────────┘",
    ];

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private MenuBarItem File => _host.Shell.Menu!.Menus.Single(item => item.Title == "_File");

    private string[] Screen(View view)
    {
        var frame = view.FrameToScreen();
        var contents = _host.App.Driver!.Contents!;
        return [.. Enumerable.Range(frame.Y, frame.Height).Select(row =>
            string.Concat(Enumerable.Range(frame.X, frame.Width).Select(col => contents[row, col].Grapheme)))];
    }

    private Delegate[] OpenFile(Action<string[]> seen) =>
    [
        () => _host.App.InjectKey(Key.F10),
        () => File.PopoverMenuOpen,
        () => seen(Screen(File.PopoverMenu!.Root!)),
        () => _host.App.InjectKey(Key.Esc),
        () => !_host.Shell.Menu!.IsOpen,
    ];

    private void Rebind(SwatchWindow window, string overrides) =>
        window.KeyOverrides.Keep(KeyOverrides.Read(JsonNode.Parse(overrides)!.AsObject(), window.KeyOverrides.Defaults, _host.Shell.Commands));

    [Fact]
    public void A_key_saved_for_a_command_with_none_shows_in_full_after_a_restart()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_host.SettingsPath)!);
        System.IO.File.WriteAllText(_host.SettingsPath, """{"swatch":{"keys":{"swatch.settings":"Ctrl+T x"}}}""");
        using var window = new SwatchWindow(_host.Shell);
        string[] rows = [];

        _host.Run(window, [() => _host.Shell.StatusBar.FocusWord == "Themes", .. OpenFile(seen => rows = seen)]);

        Assert.Equal(SettingsRebound, rows);
    }

    [Theory]
    [MemberData(nameof(Rebindings))]
    public void A_menu_resizes_to_fit_keys_changed_after_it_was_shown(string overrides, string[] expected)
    {
        using var window = new SwatchWindow(_host.Shell);
        string[] before = [], after = [];

        _host.Run(window, [
            () => _host.Shell.StatusBar.FocusWord == "Themes",
            .. OpenFile(seen => before = seen),
            () => Rebind(window, overrides),
            .. OpenFile(seen => after = seen),
        ]);

        Assert.Equal(AsBuilt, before);
        Assert.Equal(expected, after);
    }

    public static TheoryData<string, string[]> Rebindings => new()
    {
        { """{"swatch.settings":"Ctrl+T x"}""", SettingsRebound },
        {
            """{"app.quit":"Ctrl+T Ctrl+Q"}""",
            [
                "┌─────────────────────┐",
                "│ Settings…           │",
                "├─────────────────────┤",
                "│ Quit  Ctrl+T Ctrl+Q │",
                "└─────────────────────┘",
            ]
        },
        {
            """{"app.quit":""}""",
            [
                "┌───────────┐",
                "│ Settings… │",
                "├───────────┤",
                "│ Quit      │",
                "└───────────┘",
            ]
        },
    };
}
