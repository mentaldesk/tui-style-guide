using System.Text.Json.Nodes;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class ComingBackTests : StaticConfigurationTest
{
    private const string App = "test";

    private static readonly AppScreen First = new("First", 'F');
    private static readonly AppScreen Second = new("Second", 'S');

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private SampleWindow? Sample => _host.App.TopRunnableView as SampleWindow;

    private string SampleFocusWord => Sample?.Shell.StatusBar.FocusWord ?? "";

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private MenuBarItem Go => Sample!.Shell.Menu!.Menus.Single(item => item.Title == "_Go");

    private bool TodoSelectedInFiles() =>
        SampleFocusWord == "Files" && Sample!.Tree.HasFocus && Sample.Tree.SelectedObject?.Text == "todo.md";

    [Fact]
    public void Going_back_to_the_sample_s_Files_screen_puts_focus_back_on_the_row_it_was_on()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => SampleFocusWord == "Editor" && Sample!.Tree.SelectedObject is not null,
            () => Press(Key.F6, Key.CursorDown),
            TodoSelectedInFiles,
            () => Press(Key.G.WithCtrl, Key.L),
            () => SampleFocusWord == "Log",
            () => Press(Key.G.WithCtrl, Key.F),
            () => Sample!.Shell.Screen == SampleWindow.FilesScreen && TodoSelectedInFiles(),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Choosing_Files_from_the_Go_menu_goes_back_where_its_key_does()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => SampleFocusWord == "Editor" && Sample!.Tree.SelectedObject is not null,
            () => Press(Key.F6, Key.CursorDown),
            TodoSelectedInFiles,
            () => Press(Key.G.WithCtrl, Key.L),
            () => SampleFocusWord == "Log",
            () => Press(Key.G.WithAlt),
            () => Go.PopoverMenuOpen,
            () => Press(Key.F),
            () => !Sample!.Shell.Menu!.IsOpen,
            () => Sample!.Shell.Screen == SampleWindow.FilesScreen && TodoSelectedInFiles(),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void The_first_visit_to_a_screen_lands_where_work_starts()
    {
        var (window, controls) = TwoScreens(_host.Shell);
        using var _ = window;
        window.Initialized += (_, _) => _host.Shell.Reopen(App, controls.FirstStart);

        _host.Run(window,
            () => FocusWord == "Main",
            () => controls.Other.SetFocus(),
            () => FocusWord == "Other",
            () => Press(Key.G.WithCtrl, Key.S),
            () => FocusWord == "Log" && controls.Log.HasFocus,
            () => Press(Key.G.WithCtrl, Key.F),
            () => FocusWord == "Other" && controls.Other.HasFocus);
    }

    [Fact]
    public void A_control_gone_since_it_had_focus_falls_back_to_where_work_starts()
    {
        var (window, controls) = TwoScreens(_host.Shell);
        using var _ = window;
        window.Initialized += (_, _) => _host.Shell.Reopen(App, controls.FirstStart);

        _host.Run(window,
            () => FocusWord == "Main",
            () => controls.Other.SetFocus(),
            () => FocusWord == "Other",
            () => Press(Key.G.WithCtrl, Key.S),
            () => FocusWord == "Log",
            () => { controls.Other.SuperView!.Remove(controls.Other); },
            () => Press(Key.G.WithCtrl, Key.F),
            () => FocusWord == "Main" && controls.Main.HasFocus);
    }

    [Fact]
    public void Swatch_saves_the_pane_on_quit_and_reopens_on_it_beside_its_other_settings()
    {
        SaveSection(SwatchSettings.Section, new JsonObject
        {
            ["theme"] = "Midnight",
            [KeyOverrides.Section] = new JsonObject { [SwatchCommands.ShowLoadingStates] = "F5" },
        });
        using (var window = new SwatchWindow(_host.Shell))
        {
            _host.Run(window,
                () => FocusWord == "Themes",
                () => Press(Key.F6),
                () => FocusWord == "Roles",
                () => Press(Key.Q.WithCtrl));
        }

        var section = Section(SwatchSettings.Section);
        Assert.Equal("Roles", section["place"]?["pane"]?.GetValue<string>());
        Assert.Equal("Midnight", section["theme"]?.GetValue<string>());
        Assert.Equal("F5", section[KeyOverrides.Section]?[SwatchCommands.ShowLoadingStates]?.GetValue<string>());

        using var restarted = Restarted();
        using var reopened = new SwatchWindow(restarted.Shell);
        restarted.Run(reopened, () => restarted.Shell.StatusBar.FocusWord == "Roles");
    }

    [Fact]
    public void Swatch_reopens_with_the_Assistant_closed_and_focus_where_work_starts()
    {
        using (var window = new SwatchWindow(_host.Shell))
        {
            _host.Run(window,
                () => FocusWord == "Themes",
                () => _host.Shell.Commands.Execute(SwatchCommands.ToggleAssistant),
                () => FocusWord == "Assistant",
                () => Press(Key.Q.WithCtrl));
        }
        Assert.Equal("Assistant", Section(SwatchSettings.Section)["place"]?["pane"]?.GetValue<string>());

        using var restarted = Restarted();
        using var reopened = new SwatchWindow(restarted.Shell);
        var ticks = 0;
        restarted.Run(reopened,
            () => ++ticks > 5,
            () => restarted.Shell.StatusBar.FocusWord == "Themes" && !reopened.Assistant.Visible);
    }

    [Fact]
    public void An_app_reopens_on_the_screen_and_pane_it_was_left_on()
    {
        SaveSection(App, new JsonObject { ["place"] = new JsonObject { ["screen"] = "Second", ["pane"] = "Log" } });
        var (window, controls) = TwoScreens(_host.Shell);
        using var _ = window;
        window.Initialized += (_, _) => _host.Shell.Reopen(App, controls.FirstStart);

        _host.Run(window,
            () => _host.Shell.Screen == Second && FocusWord == "Log" && controls.Log.HasFocus,
            () => !Shown(controls.Other),
            () => Press(Key.G.WithCtrl, Key.F),
            () => FocusWord == "Main",
            () => Press(Key.Q.WithCtrl));

        Assert.Equal("First", Section(App)["place"]?["screen"]?.GetValue<string>());
        Assert.Equal("Main", Section(App)["place"]?["pane"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("""{ "screen": "Gone", "pane": "Log" }""")]
    [InlineData("""{ "screen": 3 }""")]
    [InlineData("""{ "pane": "Nowhere" }""")]
    [InlineData("\"First\"")]
    public void A_saved_place_that_no_longer_exists_opens_as_it_always_has(string place)
    {
        SaveSection(App, new JsonObject { ["place"] = JsonNode.Parse(place) });
        var (window, controls) = TwoScreens(_host.Shell);
        using var _ = window;
        window.Initialized += (_, _) => _host.Shell.Reopen(App, controls.FirstStart);

        _host.Run(window, () => _host.Shell.Screen == First && FocusWord == "Main" && controls.Main.HasFocus);
    }

    private Host Restarted()
    {
        var restarted = new Host();
        Directory.CreateDirectory(Path.GetDirectoryName(restarted.SettingsPath)!);
        File.Copy(_host.SettingsPath, restarted.SettingsPath);
        return restarted;
    }

    private JsonObject Section(string app) => new MentalDeskSettings(_host.SettingsPath).Section(app);

    private void SaveSection(string app, JsonObject section) => new MentalDeskSettings(_host.SettingsPath).SaveSection(app, section);

    private sealed record Controls(TextField Main, TextField Other, TextField Log, FocusRegion FirstStart);

    private static (AppWindow, Controls) TwoScreens(AppShell shell)
    {
        var window = new AppWindow(shell);
        var main = new TextField { Width = 10 };
        var other = new TextField { Width = 10 };
        var log = new TextField { Width = 10 };
        var first = Screen(Pane(shell, "Main", 0, main), Pane(shell, "Other", 20, other));
        var second = Screen(Pane(shell, "Log", 0, log));
        window.Body.Add(first, second);
        var start = shell.Focus.Regions.Single(region => region.Name == "Main");
        shell.AddScreen(First, first, start).AddScreen(Second, second, shell.Focus.Regions.Single(region => region.Name == "Log"));
        return (window, new Controls(main, other, log, start));
    }

    private static View Screen(params View[] panes)
    {
        var screen = new View { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        screen.Add(panes);
        return screen;
    }

    private static FrameView Pane(AppShell shell, string name, int x, View control)
    {
        var pane = new FrameView { Title = name, X = x, Width = 20, Height = 5, CanFocus = true, TabStop = TabBehavior.TabStop };
        pane.Add(control);
        shell.Focus.Register(new FocusRegion(name, CommandScope.Global), () => control.SetFocus(), view => Within(view as View, pane));
        return pane;
    }

    private static bool Shown(View? view)
    {
        for (; view is not null; view = view.SuperView)
            if (!view.Visible) return false;
        return true;
    }

    private static bool Within(View? view, View pane)
    {
        for (; view is not null; view = view.SuperView)
            if (view == pane) return true;
        return false;
    }
}
