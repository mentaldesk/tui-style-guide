using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Diagnostics;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Theming;
using Swatch;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Terminal.Gui.Input;

namespace MentalDesk.Tui.Tests;

public class SwatchHostTests : StaticConfigurationTest
{
    private static readonly Key[] DownToDaylight = [.. Enumerable.Repeat(Key.CursorDown, SchemeNames.All.Count + 1)];

    // Built after the base class has loaded the themes, as an app does.
    private readonly Host _host;

    public SwatchHostTests()
    {
        _host = new Host();
    }

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    [Fact]
    public void Opens_with_the_themes_focused_and_their_hints_showing()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => _host.Shell.StatusBar.Says.StartsWith("Ctrl+T U use theme", StringComparison.Ordinal));
    }

    [Fact]
    public void A_chord_shows_while_it_is_typed_then_moves_focus()
    {
        using var window = new SwatchWindow(_host.Shell);
        var during = "";

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.G.WithCtrl),
            () => _host.Shell.StatusBar.Says == "Ctrl+G…",
            () => { during = _host.Shell.StatusBar.Says; },
            () => Press(Key.R),
            () => FocusWord == "Roles");

        Assert.Equal("Ctrl+G…", during);
    }

    [Fact]
    public void Tab_moves_between_panes_and_the_focus_word_follows()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.Tab),
            () => FocusWord == "Roles",
            () => Press(Key.Tab.WithShift),
            () => FocusWord == "Themes");
    }

    [Fact]
    public void The_palette_runs_what_you_pick_after_it_closes()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press([.. "preview".Select(c => new Key(c))]),
            () => ((CommandPalette)_host.App.TopRunnableView!).Labels.SequenceEqual(["Go to preview"]),
            () => Press(Key.Enter),
            () => _host.App.TopRunnableView is SwatchWindow && FocusWord == "Preview");
    }

    [Fact]
    public void Esc_closes_the_palette_and_gives_the_app_its_keys_back()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow,
            () => Press(Key.G.WithCtrl, Key.P),
            () => FocusWord == "Preview");
    }

    [Fact]
    public void Esc_on_the_main_screen_does_nothing()
    {
        using var window = new SwatchWindow(_host.Shell);
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.Esc),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Esc_still_does_nothing_after_the_theme_changes()
    {
        using var window = new SwatchWindow(_host.Shell);
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(Key.T.WithCtrl, Key.U),
            () => _host.Shell.StatusBar.Message is not null,
            () => Press(Key.Esc),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void F10_opens_the_menu_and_Esc_closes_it()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.F10),
            () => _host.Shell.Menu!.IsOpen,
            () => Press(Key.Esc),
            () => !_host.Shell.Menu!.IsOpen);
    }

    [Fact]
    public void Every_menu_item_has_a_hot_letter_of_its_own()
    {
        using var window = new SwatchWindow(_host.Shell);

        var menus = _host.Shell.Menu!.Menus;
        Assert.Equal(menus.Count, menus.Select(menu => menu.HotKey).Distinct().Count());
        foreach (var menu in menus)
        {
            var items = menu.PopoverMenu!.Root!.SubViews.OfType<MenuItem>().ToList();
            Assert.Equal(items.Count, items.Select(item => item.HotKey).Distinct().Count());
        }
    }

    [Fact]
    public void Using_a_theme_switches_the_app_and_sends_the_terminal_its_cursor_colour()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(Key.T.WithCtrl, Key.U),
            () => Themes.Current == Themes.Daylight,
            () => _host.Shell.StatusBar.Message == "Swatch is now in Daylight",
            () => Press(Key.CursorUp),
            () => _host.Shell.StatusBar.Message is null);

        Assert.Equal(["\x1b]12;#1F2328\x07", "\x1b]112\x07"], _host.Written[^2..]);
    }

    [Fact]
    public void The_menu_acts_on_the_selected_theme_while_it_has_focus()
    {
        using var window = new SwatchWindow(_host.Shell);
        var menu = _host.Shell.Menu!;
        var themeMenu = menu.Menus.Single(item => item.Title == "_Theme");
        var useTheme = menu.Items.Single(entry => entry.Id == SwatchCommands.UseTheme).Item;
        var enabledWhileOpen = false;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(Key.F10, Key.CursorRight, Key.CursorRight),
            () => themeMenu.PopoverMenuOpen,
            () =>
            {
                menu.Refresh();
                enabledWhileOpen = useTheme.Enabled;
            },
            () => Press(Key.U),
            () => Themes.Current == Themes.Daylight,
            () => menu.Refresh());

        Assert.True(enabledWhileOpen);
        Assert.False(useTheme.Enabled);
    }

    [Fact]
    public void Diagnostics_shows_the_cursor_colour_asked_for_and_each_key_pressed()
    {
        using var window = new SwatchWindow(_host.Shell);
        DiagnosticsDialog? diagnostics = null;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.F12),
            () => (diagnostics = _host.App.TopRunnableView as DiagnosticsDialog) is not null,
            () => diagnostics!.CursorLine.StartsWith("asked #E6E9EF", StringComparison.Ordinal),
            () => Press(Key.X.WithCtrl),
            () => diagnostics!.SubViews.Any(view => view.Text == "Ctrl+X"),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Delete_opens_on_the_selected_theme_with_focus_on_cancel()
    {
        using var window = new SwatchWindow(_host.Shell);
        ConfirmDialog? dialog = null;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(Key.T.WithCtrl, Key.D),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => dialog!.CancelButton.HasFocus,
            () => dialog!.HintText == "Del delete  •  Esc cancel",
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("Delete theme", dialog!.Title);
        Assert.Equal(["Delete \"Daylight\"?", "This can't be undone."], dialog.Lines);
    }

    [Fact]
    public void Delete_is_in_the_palette()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press([.. "delete".Select(c => new Key(c))]),
            () => ((CommandPalette)_host.App.TopRunnableView!).Labels.SequenceEqual(["Delete this theme"]),
            () => Press(Key.Enter),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Theory]
    [MemberData(nameof(KeysThatDoNotDelete))]
    public void Esc_cancels_and_Enter_does_nothing_whichever_button_is_focused(Key move, Key key)
    {
        using var window = new SwatchWindow(_host.Shell);
        ConfirmDialog? dialog = null;
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.D),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => dialog!.CancelButton.HasFocus,
            () => Press(move, key),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView is SwatchWindow == (key == Key.Esc),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Null(dialog!.Chosen);
        Assert.Null(_host.Shell.StatusBar.Message);
    }

    public static TheoryData<Key, Key> KeysThatDoNotDelete => new()
    {
        { Key.CursorRight, Key.Enter },
        { Key.CursorLeft, Key.Enter },
        { Key.CursorRight, Key.Esc },
        { Key.CursorLeft, Key.Esc },
    };

    [Theory]
    [MemberData(nameof(WaysToDelete))]
    public void Del_or_arrowing_to_Delete_and_Space_deletes_and_says_the_themes_are_built_in(Key[] keys)
    {
        using var window = new SwatchWindow(_host.Shell);
        ConfirmDialog? dialog = null;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.D),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => dialog!.CancelButton.HasFocus,
            () => Press(keys),
            () => _host.App.TopRunnableView is SwatchWindow,
            () => _host.Shell.StatusBar.Message == "Swatch's themes are built in, so nothing was deleted");

        Assert.Equal(ThemeConfirms.Delete, dialog!.Chosen);
        Assert.Equal(Themes.Names, window.ThemeNames);
    }

    public static TheoryData<Key[]> WaysToDelete => new() { new[] { Key.Delete }, new[] { Key.CursorLeft, Key.Space }, new[] { Key.Tab, Key.Space } };

    [Theory]
    [MemberData(nameof(WaysToRemove))]
    public void Remove_takes_Enter_R_or_Space_on_its_button(Key[] keys)
    {
        using var window = new SwatchWindow(_host.Shell);
        ConfirmDialog? dialog = null;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.R),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => dialog!.CancelButton.HasFocus,
            () => dialog!.HintText == "Enter remove  •  Esc cancel",
            () => Press(keys),
            () => _host.App.TopRunnableView is SwatchWindow,
            () => _host.Shell.StatusBar.Message == "A demo: Swatch's themes are built in, so nothing was removed");

        Assert.Equal(ThemeConfirms.Remove, dialog!.Chosen);
        Assert.Equal($"Remove {Themes.Names[0]}?", dialog.Title);
    }

    public static TheoryData<Key[]> WaysToRemove => new() { new[] { Key.Enter }, new[] { Key.R }, new[] { Key.R.WithShift }, new[] { Key.CursorLeft, Key.Space } };

    [Theory]
    [MemberData(nameof(WaysToClose))]
    public void Close_takes_S_D_or_Space_and_Enter_does_nothing(Key[] keys, string? said)
    {
        using var window = new SwatchWindow(_host.Shell);
        ConfirmDialog? dialog = null;
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.C),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => dialog!.CancelButton.HasFocus,
            () => dialog!.HintText == "S save  •  D don't save  •  Esc cancel",
            () => Press(keys),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView is SwatchWindow == (said is not null),
            () => _host.Shell.StatusBar.Message == said,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

    }

    public static TheoryData<Key[], string?> WaysToClose => new()
    {
        { [Key.S], "A demo: Swatch has no unsaved changes, so nothing was saved" },
        { [Key.D], "A demo: Swatch has no unsaved changes, so nothing was lost" },
        { [Key.CursorLeft, Key.CursorLeft, Key.Space], "A demo: Swatch has no unsaved changes, so nothing was saved" },
        { [Key.CursorLeft, Key.Space], "A demo: Swatch has no unsaved changes, so nothing was lost" },
        { [Key.Enter], null },
        { [Key.CursorLeft, Key.Enter], null },
    };

    [Fact]
    public void Hovering_a_button_lightens_it_without_moving_focus()
    {
        using var window = new SwatchWindow(_host.Shell);
        var danger = Themes.SchemesOf(Themes.Current)[SchemeNames.ButtonDanger];
        ConfirmDialog? dialog = null;
        var over = Point.Empty;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.D),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => dialog!.CancelButton.HasFocus,
            () =>
            {
                over = dialog!.ButtonFor(ThemeConfirms.Delete).FrameToScreen().Location;
                Move(over);
            },
            () => Background(over) == danger.Highlight.Background,
            () => dialog!.CancelButton.HasFocus,
            () => Move(dialog!.FrameToScreen().Location + new Size(2, 1)),
            () => Background(over) == danger.Normal.Background,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Color Background(Point at) => _host.App.Driver!.Contents![at.Y, at.X].Attribute!.Value.Background;
    }

    [Fact]
    public void The_dialog_takes_the_colours_of_the_theme_in_use()
    {
        using var window = new SwatchWindow(_host.Shell);
        var cancel = Themes.SchemesOf(Themes.Daylight)[SchemeNames.ButtonSecondary];
        ConfirmDialog? dialog = null;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(Key.T.WithCtrl, Key.U),
            () => Themes.Current == Themes.Daylight,
            () => Press(Key.T.WithCtrl, Key.D),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => dialog!.CancelButton.HasFocus,
            () =>
            {
                var at = dialog!.CancelButton.FrameToScreen().Location;
                return _host.App.Driver!.Contents![at.Y, at.X].Attribute!.Value.Background == cancel.Focus.Background;
            },
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    private void Move(Point to) =>
        _host.App.InjectMouse(new Mouse { ScreenPosition = to, Flags = MouseFlags.PositionReport });
}
