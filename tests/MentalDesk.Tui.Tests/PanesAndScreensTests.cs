using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Shell;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class PanesAndScreensTests : StaticConfigurationTest
{
    private readonly Host _host = new();

    public override void Dispose()
    {
        Application.SetDefaultKeyBinding(Command.NextTabGroup, new PlatformKeyBinding { All = [Key.F6] });
        _host.Dispose();
        base.Dispose();
    }

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private SampleWindow? Sample => _host.App.TopRunnableView as SampleWindow;

    private string SampleFocusWord => Sample?.Shell.StatusBar.FocusWord ?? "";

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    [Fact]
    public void F6_cycles_Swatch_s_panes_in_reading_order_and_Shift_F6_goes_back()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.F6),
            () => FocusWord == "Roles",
            () => Press(Key.F6),
            () => FocusWord == "Preview",
            () => Press(Key.F6),
            () => FocusWord == "Themes",
            () => Press(Key.F6.WithShift),
            () => FocusWord == "Preview",
            () => Press(Key.F6.WithShift),
            () => FocusWord == "Roles");
    }

    [Fact]
    public void Swatch_s_old_pane_chords_do_nothing_and_Go_to_scheme_still_works()
    {
        using var window = new SwatchWindow(_host.Shell);
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.G.WithCtrl, Key.R, Key.G.WithCtrl, Key.P),
            () => ++ticks > 5,
            () => FocusWord == "Themes",
            () => Press(Key.G.WithCtrl, Key.S),
            () => _host.App.TopRunnableView is PickerDialog<SwatchWindow.Selection>,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Swatch_s_Go_menu_lists_the_panes_then_a_separator_then_Scheme()
    {
        using var window = new SwatchWindow(_host.Shell);

        var go = _host.Shell.Menu!.Layout.Single(spec => spec.Title == "_Go");

        Assert.Equal([ShellCommands.NextPane, ShellCommands.PreviousPane, null, SwatchCommands.GoToScheme], go.Items.Select(item => item?.CommandId));
        Assert.Equal(["F6", "Shift+F6"], Keys(ShellCommands.NextPane, ShellCommands.PreviousPane));
    }

    [Fact]
    public void Help_and_the_palette_list_the_pane_commands_under_Moving_around()
    {
        using var window = new SwatchWindow(_host.Shell);
        KeySheet? sheet = null;
        List<string> listed = [];

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.F1),
            () => _host.App.TopRunnableView is KeysDialog,
            () =>
            {
                sheet = ((KeysDialog)_host.App.TopRunnableView!).Sheet;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow,
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () =>
            {
                listed = [.. ((CommandPalette)_host.App.TopRunnableView!).Shown.Select(row => row.Label)];
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        var moving = sheet!.Everywhere!.Groups.Single(group => group.Heading == "Moving around");
        Assert.Equal(["F6 Next pane", "Shift+F6 Previous pane", "Ctrl+G S Go to scheme…"], moving.Rows.Select(row => $"{row.Keys} {row.Label}"));
        Assert.Contains("Next pane", listed);
        Assert.Contains("Previous pane", listed);
    }

    [Fact]
    public void A_rebound_next_pane_key_cycles_panes_and_the_menu_and_help_show_it()
    {
        Application.SetDefaultKeyBinding(Command.NextTabGroup, new PlatformKeyBinding { All = [Key.F7] });
        using var window = new SwatchWindow(_host.Shell);
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.F6),
            () => ++ticks > 5,
            () => FocusWord == "Themes",
            () => Press(Key.F7),
            () => FocusWord == "Roles");

        Assert.Equal(["F7"], Keys(ShellCommands.NextPane));
        _host.Shell.Menu!.Refresh();
        Assert.Equal("F7", _host.Shell.Menu.Items.First(item => item.Id == ShellCommands.NextPane).Item.KeyView.Text);
    }

    [Fact]
    public void Tab_from_a_pane_s_last_control_goes_on_to_the_next_pane()
    {
        using var window = new AppWindow(_host.Shell);
        var first = new TextField { Width = 10 };
        var last = new TextField { Y = 1, Width = 10 };
        var next = new TextField { Width = 10 };
        var left = Pane(_host.Shell, "Left", 0, first, last);
        var right = Pane(_host.Shell, "Right", 20, next);
        window.Body.Add(left, right);
        window.Initialized += (_, _) => first.SetFocus();

        _host.Run(window,
            () => FocusWord == "Left",
            () => Press(Key.Tab),
            () => last.HasFocus,
            () => FocusWord == "Left",
            () => Press(Key.Tab),
            () => next.HasFocus && FocusWord == "Right");
    }

    [Fact]
    public void A_pane_that_can_t_take_focus_is_skipped()
    {
        using var window = new AppWindow(_host.Shell);
        var a = new TextField { Width = 10 };
        var b = new TextField { Width = 10 };
        var c = new TextField { Width = 10 };
        var middle = Pane(_host.Shell, "Middle", 20, b);
        window.Body.Add(Pane(_host.Shell, "First", 0, a), middle, Pane(_host.Shell, "Last", 40, c));
        middle.Enabled = false;
        window.Initialized += (_, _) => a.SetFocus();

        _host.Run(window,
            () => FocusWord == "First",
            () => Press(Key.F6),
            () => FocusWord == "Last",
            () => Press(Key.F6.WithShift),
            () => FocusWord == "First");
    }

    [Fact]
    public void F6_cycles_the_sample_s_files_and_editor()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => SampleFocusWord == "Editor",
            () => Press(Key.F6),
            () => SampleFocusWord == "Files",
            () => Press(Key.F6),
            () => SampleFocusWord == "Editor",
            () => Press(Key.F6.WithShift),
            () => SampleFocusWord == "Files",
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Ctrl_G_and_a_letter_switches_the_sample_s_screen_and_Go_checks_the_current_one()
    {
        using var window = new SwatchWindow(_host.Shell);
        var checkedOnLog = new List<string>();
        var filesShownOnLog = true;
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => SampleFocusWord == "Editor",
            () => Press(Key.G.WithCtrl, Key.L),
            () => SampleFocusWord == "Log",
            () =>
            {
                Sample!.Shell.Menu!.Refresh();
                checkedOnLog = [.. Sample.Shell.Menu.Items.Select(item => item.Item.Title).Where(title => title.StartsWith('●'))];
                filesShownOnLog = Shown(Sample.Tree);
            },
            () => Press(Key.F6),
            () => ++ticks > 5,
            () => SampleFocusWord == "Log",
            () => Press(Key.G.WithCtrl, Key.F),
            () => SampleFocusWord == "Editor" && Sample!.Shell.Screen == SampleWindow.FilesScreen,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Contains("● _Log", checkedOnLog);
        Assert.DoesNotContain(checkedOnLog, title => title.Contains("Files", StringComparison.Ordinal));
        Assert.False(filesShownOnLog);
    }

    [Fact]
    public void The_sample_s_Go_menu_lists_the_panes_then_its_screens_and_help_and_the_palette_list_both_screens()
    {
        using var window = new SwatchWindow(_host.Shell);
        KeySheet? sheet = null;
        List<string> listed = [];
        IReadOnlyList<MenuEntry?> go = [];

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => SampleFocusWord == "Editor",
            () => { go = Sample!.Shell.Menu!.Layout.Single(spec => spec.Title == "_Go").Items; },
            () => Press(Key.F1),
            () => _host.App.TopRunnableView is KeysDialog,
            () =>
            {
                sheet = ((KeysDialog)_host.App.TopRunnableView!).Sheet;
                Press(Key.Esc);
            },
            () => Sample is not null,
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () =>
            {
                listed = [.. ((CommandPalette)_host.App.TopRunnableView!).Shown.Select(row => row.Label)];
                Press(Key.Esc);
            },
            () => Sample is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(
            [ShellCommands.NextPane, ShellCommands.PreviousPane, null, SampleWindow.FilesScreen.CommandId, SampleWindow.LogScreen.CommandId],
            go.Select(item => item?.CommandId));
        var moving = sheet!.Everywhere!.Groups.Single(group => group.Heading == "Moving around");
        Assert.Equal(
            ["F6 Next pane", "Shift+F6 Previous pane", "Ctrl+G F Go to Files", "Ctrl+G L Go to Log"],
            moving.Rows.Select(row => $"{row.Keys} {row.Label}"));
        Assert.Contains("Go to Files", listed);
        Assert.Contains("Go to Log", listed);
    }

    [Fact]
    public void A_screen_letter_that_clashes_with_another_Ctrl_G_key_throws_when_it_is_added()
    {
        _host.Shell.Commands.Register("go.somewhere", "Go somewhere", () => { });
        _host.Shell.Keys.Bind("Ctrl+G L", "go.somewhere");

        var clash = Assert.Throws<ArgumentException>(() =>
            _host.Shell.AddScreen(new AppScreen("Log", 'L'), new View(), new FocusRegion("Log", CommandScope.Global)));

        Assert.Contains("Ctrl+G L", clash.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_screens_can_t_share_a_letter()
    {
        _host.Shell.AddScreen(new AppScreen("Files", 'F'), new View(), new FocusRegion("Files", CommandScope.Global));

        Assert.Throws<ArgumentException>(() =>
            _host.Shell.AddScreen(new AppScreen("Find", 'F'), new View(), new FocusRegion("Find", CommandScope.Global)));
    }

    private IEnumerable<string> Keys(params string[] ids) =>
        ids.Select(id => _host.Shell.Keys.For(id).Single().Display);

    private static FrameView Pane(AppShell shell, string name, int x, params View[] controls)
    {
        var pane = new FrameView { Title = name, X = x, Width = 20, Height = 5, CanFocus = true, TabStop = TabBehavior.TabStop };
        pane.Add(controls);
        shell.Focus.Register(new FocusRegion(name, CommandScope.Global), () => controls[0].SetFocus(), view => Within(view as View, pane));
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
