using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class KeysHostTests : StaticConfigurationTest
{
    private static readonly Key[] DownToDaylight = [.. Enumerable.Repeat(Key.CursorDown, SchemeNames.All.Count + 1)];

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private KeysDialog? Keys => _host.App.TopRunnableView as KeysDialog;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private static IEnumerable<string> Labels(KeyColumn column) =>
        column.Groups.SelectMany(group => group.Rows).Select(row => row.Label);

    private static IEnumerable<View> Descendants(View view) =>
        view.SubViews.SelectMany(sub => Descendants(sub).Prepend(sub));

    [Fact]
    public void F1_keys_is_the_status_bar_s_only_hint_whichever_pane_has_focus()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => _host.Shell.StatusBar.Says == "F1 keys",
            () => Press(Key.G.WithCtrl, Key.R),
            () => FocusWord == "Roles",
            () => _host.Shell.StatusBar.Says == "F1 keys");
    }

    [Fact]
    public void F1_on_the_themes_pane_shows_its_theme_keys_here_and_the_rest_everywhere()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(Key.F1),
            () => Keys is not null,
            () =>
            {
                Assert.Equal("Keys", Keys!.Title);
                Assert.Equal("Here: Themes", Keys.Sheet.Here!.Heading);
                Assert.Equal(
                    ["Use this theme for Swatch", "Delete this theme", "Remove this theme", "Close this theme"],
                    Labels(Keys.Sheet.Here));
                Assert.Equal(["File", "Go", "Theme", "Help"], Keys.Sheet.Everywhere!.Groups.Select(group => group.Heading));
                Assert.Equal(" Esc Close ", Keys.CloseButton.Text);
                Assert.DoesNotContain(Descendants(Keys), view => view.CanFocus);
            },
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void F1_on_the_roles_pane_shows_copy_colours_here()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.G.WithCtrl, Key.R),
            () => FocusWord == "Roles",
            () => Press(Key.F1),
            () => Keys is not null,
            () => Keys!.Sheet.Here?.Heading == "Here: Roles" && Labels(Keys.Sheet.Here).SequenceEqual(["Copy colours"]),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Enter_and_F1_do_nothing_inside_the_dialog()
    {
        using var window = new SwatchWindow(_host.Shell);
        var opened = new List<KeysDialog>();
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.F1),
            () => Keys is not null,
            () => opened.Add(Keys!),
            () => Press(Key.Enter, Key.F1),
            () => ++ticks > 5,
            () => Keys == opened.Single(),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void F1_in_another_dialog_shows_only_that_dialog_s_keys()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press(Key.F1),
            () => Keys is not null,
            () =>
            {
                Assert.Equal("Here: Commands", Keys!.Sheet.Here!.Heading);
                Assert.Null(Keys.Sheet.Everywhere);
            },
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Show_keys_runs_from_the_palette()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press([.. "show keys".Select(c => new Key(c))]),
            () => ((CommandPalette)_host.App.TopRunnableView!).Shown.FirstOrDefault()?.Label == "Show keys",
            () => Press(Key.Enter),
            () => Keys is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void The_help_menu_lists_show_keys_with_F1()
    {
        using var window = new SwatchWindow(_host.Shell);

        var item = _host.Shell.Menu!.Items.Single(entry => entry.Id == MentalDesk.Tui.Shell.ShellCommands.ShowKeys).Item;

        Assert.Equal("F1", item.KeyView.Text);
    }

    [Fact]
    public void Use_theme_works_from_the_themes_pane_but_its_key_no_longer_does_from_roles()
    {
        using var window = new SwatchWindow(_host.Shell);
        var ticks = 0;

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight,
            () => Press(Key.G.WithCtrl, Key.R),
            () => FocusWord == "Roles",
            () => Press(Key.T.WithCtrl, Key.U),
            () => ++ticks > 5,
            () => Themes.Current != Themes.Daylight,
            () => _host.Shell.Commands.Execute(SwatchCommands.UseTheme),
            () => Themes.Current == Themes.Daylight);
    }
}
