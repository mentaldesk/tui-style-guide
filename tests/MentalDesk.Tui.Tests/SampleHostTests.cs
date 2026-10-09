using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class SampleHostTests : StaticConfigurationTest
{
    private static readonly Key[] DownToDaylight = [.. Enumerable.Repeat(Key.CursorDown, SchemeNames.All.Count + 1)];

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private SampleWindow? Sample => _host.App.TopRunnableView as SampleWindow;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    public static TheoryData<string> WaysToOpen => new() { "chord", "menu", "enter", "click" };

    [Theory]
    [MemberData(nameof(WaysToOpen))]
    public void Try_it_opens_the_sample_in_the_selected_theme_and_Esc_brings_Swatch_back_as_it_was(string way)
    {
        using var window = new SwatchWindow(_host.Shell);
        var focusBefore = "";
        var saysBefore = "";
        var themeInside = "";

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(way is "enter" or "click" ? [Key.G.WithCtrl, Key.P] : []),
            () => way is not ("enter" or "click") || FocusWord == "Preview",
            () =>
            {
                focusBefore = FocusWord;
                saysBefore = _host.Shell.StatusBar.Says;
                Open(window, way);
            },
            () => Sample is not null,
            () =>
            {
                themeInside = Themes.Current;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(Themes.Daylight, themeInside);
        Assert.Equal(Themes.Midnight, Themes.Current);
        Assert.Equal(focusBefore, FocusWord);
        Assert.Equal(saysBefore, _host.Shell.StatusBar.Says);
        Assert.Equal(Themes.Daylight, window.Showing.Theme);
    }

    private void Open(SwatchWindow window, string way)
    {
        switch (way)
        {
            case "chord":
                Press(Key.T.WithCtrl, Key.T);
                break;
            case "menu":
                Press(Key.F10, Key.CursorRight, Key.CursorRight, Key.T);
                break;
            case "enter":
                Press(Key.Enter);
                break;
            case "click":
                var at = window.Preview.FrameToScreen().Location + new System.Drawing.Size(1, 1);
                _host.App.InjectSequence(InputInjectionExtensions.LeftButtonClick(at));
                break;
        }
    }

    [Fact]
    public void The_sample_s_status_bar_offers_only_F1_keys_whichever_pane_has_focus()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => Sample?.Shell.StatusBar.FocusWord == "Editor",
            () => Sample!.Shell.StatusBar.Says == "F1 keys",
            () => Press(Key.Tab.WithShift),
            () => Sample!.Shell.StatusBar.FocusWord == "Files",
            () => Sample!.Shell.StatusBar.Says == "F1 keys",
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Esc_closes_an_open_menu_without_leaving_the_sample()
    {
        using var window = new SwatchWindow(_host.Shell);
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => Sample is not null,
            () => Press(Key.F10),
            () => Sample!.Shell.Menu!.IsOpen,
            () => Press(Key.Esc),
            () => !Sample!.Shell.Menu!.IsOpen,
            () => ++ticks > 5,
            () => Sample is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Esc_closes_a_dialog_without_leaving_the_sample()
    {
        using var window = new SwatchWindow(_host.Shell);
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => Sample is not null,
            () => Press(Key.S.WithCtrl),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () => Press(Key.Esc),
            () => Sample is not null,
            () => ++ticks > 5,
            () => Sample is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void The_theme_menu_restyles_the_sample_and_marks_the_current_theme()
    {
        using var window = new SwatchWindow(_host.Shell);
        SampleWindow? sample = null;
        var marked = new List<string>();

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => (sample = Sample) is not null,
            () => Press(Key.F10, Key.CursorRight, Key.CursorRight, Key.CursorRight),
            () => sample!.Shell.Menu!.Menus.Single(menu => menu.Title == "_Theme").PopoverMenuOpen,
            () => Press(Key.D),
            () => Themes.Current == Themes.Daylight,
            () =>
            {
                sample!.Shell.Menu!.Refresh();
                marked.AddRange(sample.Shell.Menu.Items
                    .Where(entry => entry.Item.Title.StartsWith('●')).Select(entry => entry.Item.Title));
            },
            () => Sample == sample,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(["● _Daylight"], marked);
        Assert.Equal(Themes.Daylight, sample!.Shell.StatusBar.State);
        Assert.Equal(Themes.Midnight, Themes.Current);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("delete")]
    public void Saving_or_deleting_changes_nothing_and_says_it_would_have(string action)
    {
        using var window = new SwatchWindow(_host.Shell);
        var config = Themes.BundledConfig;
        var said = "";

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => Sample is not null,
            () => Press(action == "save" ? [Key.S.WithCtrl] : [Key.Tab.WithShift, Key.Delete]),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () => Press(action == "save" ? Key.Enter : Key.Delete),
            () => Sample?.Shell.StatusBar.Message is not null,
            () =>
            {
                said = Sample!.Shell.StatusBar.Message!;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(action == "save" ? "A demo: this would have saved \"Solar\"" : "A demo: this would have deleted \"Midnight\"", said);
        Assert.Equal(config, Themes.BundledConfig);
        Assert.Equal(Themes.Bundled, Themes.Names);
        Assert.Equal(Themes.Midnight, Themes.Current);
        Assert.Null(_host.Shell.StatusBar.Message);
    }

    [Fact]
    public void Every_sample_menu_item_has_a_hot_letter_of_its_own()
    {
        using var shell = new Shell.AppShell(_host.App, _host.Shell.Cursor);
        using var sample = new SampleWindow(shell, Themes.Current);

        var menus = shell.Menu!.Menus;
        Assert.Equal(["_File", "_Edit", "_View", "_Theme", "_Help"], menus.Select(menu => menu.Title));
        foreach (var menu in menus)
        {
            var items = menu.PopoverMenu!.Root!.SubViews.OfType<MenuItem>().ToList();
            Assert.Equal(items.Count, items.Select(item => item.HotKey).Distinct().Count());
        }
        shell.Menu.Refresh();
        Assert.True(shell.Menu.IsDimmed(SampleWindow.Export));
    }

    [Fact]
    public void The_sample_s_edit_menu_is_on_the_bar_only_while_the_editor_has_focus()
    {
        using var window = new SwatchWindow(_host.Shell);
        var openedFromFiles = true;
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => Sample?.Shell.StatusBar.FocusWord == "Editor",
            () => Titles().SequenceEqual(["_File", "_Edit", "_View", "_Theme", "_Help"]),
            () => Press(Key.Tab.WithShift),
            () => Sample!.Shell.StatusBar.FocusWord == "Files",
            () => Titles().SequenceEqual(["_File", "_View", "_Theme", "_Help"]),
            () => Press(Key.E.WithAlt),
            () => ++ticks > 5,
            () => { openedFromFiles = Sample!.Shell.Menu!.IsOpen; },
            () => Press(Key.Tab),
            () => Sample!.Shell.StatusBar.FocusWord == "Editor",
            () => Titles().Contains("_Edit"),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.False(openedFromFiles);

        IEnumerable<string> Titles() => Sample!.Shell.Menu!.Shown.Select(menu => menu.Title);
    }
}
