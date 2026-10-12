using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class SwatchSettingsTests : StaticConfigurationTest
{
    private readonly Host _host = new(detected: new FontDetection(false, "couldn't tell which font the terminal uses"));

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private IconSettings Icons => _host.Shell.Icons;

    private SettingsDialog? Dialog => _host.App.TopRunnableView as SettingsDialog;

    private AppearancePage Appearance => (AppearancePage)Dialog!.Pages[0];

    private SwatchesPage Swatches => (SwatchesPage)Dialog!.Pages[1];

    private SwatchSettings Saved => new(new MentalDeskSettings(_host.SettingsPath));

    private IEnumerable<string> Names => Dialog!.PageList.Source!.ToList().Cast<string>();

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        Run(window, steps);
    }

    private void Run(SwatchWindow window, params Delegate[] steps) =>
        _host.Run(window, [
            () => { _host.Shell.Commands.Execute(SwatchCommands.ShowSettings); },
            () => Dialog?.IsInitialized == true && Appearance.Theme.HasFocus,
            .. steps,
        ]);

    [Fact]
    public void It_is_on_the_File_menu_and_in_the_palette_and_Help_has_no_Icons()
    {
        using var window = new SwatchWindow(_host.Shell);
        var layout = _host.Shell.Menu!.Layout;

        _host.Run(window,
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press([.. "settings".Select(c => new Key(c))]),
            () => ((CommandPalette)_host.App.TopRunnableView!).Shown.Select(row => row.Label).SequenceEqual(["Settings…"]),
            () => Press(Key.Enter),
            () => Dialog is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Contains(layout.Single(menu => menu.Title == "_File").Items, entry => entry?.CommandId == SwatchCommands.ShowSettings);
        Assert.DoesNotContain(layout.Single(menu => menu.Title == "_Help").Items, entry => entry?.Title?.Contains("Icons", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(_host.Shell.Commands.All, command => command.Label.StartsWith("Icons", StringComparison.Ordinal));
    }

    [Fact]
    public void Appearance_lists_the_themes_and_icon_styles_with_autos_reason()
    {
        Run(() =>
        {
            Assert.Equal(["Appearance", "Swatches"], Names);
            Assert.Equal(Themes.Names, Appearance.ThemeRows);
            Assert.Equal(0, Appearance.Theme.SelectedItem);
            Assert.Equal(0, Appearance.Icons.Value);
            Assert.Contains(Appearance.SubViews, view => view.Text == "Auto chose Plain: couldn't tell which font the terminal uses");
            Press(Key.Esc);
        });
    }

    [Fact]
    public void Moving_through_the_themes_previews_each_and_marks_it_changed_from_the_default()
    {
        Run(
            () => Press(Key.CursorDown),
            () => Themes.Current == Themes.Daylight,
            () =>
            {
                Assert.Equal(["Midnight", "Daylight  (default: Midnight)", "Turbo Pascal", "Modern Borland"], Appearance.ThemeRows);
                Assert.Equal(["Appearance •", "Swatches"], Names);
            },
            () => Press(Key.CursorUp),
            () => Themes.Current == Themes.Midnight,
            () =>
            {
                Assert.Equal(Themes.Names, Appearance.ThemeRows);
                Assert.Equal(["Appearance", "Swatches"], Names);
                Press(Key.Esc);
            });
    }

    [Fact]
    public void Enter_on_the_theme_list_neither_keeps_nor_closes()
    {
        var ticks = 0;
        Run(
            () => Press(Key.CursorDown, Key.Enter),
            () => ++ticks > 5,
            () =>
            {
                Assert.Same(Dialog, _host.App.TopRunnableView);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(Themes.Midnight, Themes.Current);
        Assert.False(File.Exists(_host.SettingsPath));
    }

    [Fact]
    public void The_icon_style_previews_at_once_and_says_when_it_isnt_the_default()
    {
        Run(
            () => Press(Key.Tab),
            () => Appearance.Icons.HasFocus,
            () => Press(Key.CursorRight),
            () => Icons.InUse == IconStyle.NerdFont && Appearance.IconsDefault == "(default: Auto)",
            () => Press(Key.CursorLeft),
            () => Icons.Style == IconStyle.Auto && Appearance.IconsDefault == "",
            () => Press(Key.Esc));
    }

    [Fact]
    public void Ctrl_Enter_keeps_every_change_for_the_next_start()
    {
        Run(
            () => Press(Key.CursorDown, Key.Tab, Key.CursorRight, Key.CursorRight),
            () => Themes.Current == Themes.Daylight && Icons.Style == IconStyle.Plain,
            () => Press(Key.PageDown.WithCtrl),
            () => Swatches.ShowHexValues.HasFocus,
            () => Press(Key.Space),
            () => Names.SequenceEqual(["Appearance •", "Swatches •"]),
            () => Press(Key.Enter.WithCtrl),
            () => _host.App.TopRunnableView is SwatchWindow window && !window.ShowHexValues);

        Assert.Equal(Themes.Daylight, Themes.Current);
        Assert.Equal(Themes.Daylight, Saved.Theme);
        Assert.False(Saved.ShowHexValues);
        Assert.Equal(IconStyle.Plain, new MentalDeskSettings(_host.SettingsPath).IconStyle);

        Themes.Apply(Themes.Midnight);
        using var restarted = new SwatchWindow(_host.Shell);
        Run(restarted, () =>
        {
            Assert.Equal(Themes.Daylight, Themes.Current);
            Assert.False(restarted.ShowHexValues);
            Assert.Equal(1, Appearance.Theme.SelectedItem);
            Assert.Equal(["Appearance", "Swatches"], Names);
            Press(Key.Esc);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Esc_or_the_close_box_puts_back_the_saved_theme_icons_and_hex_values(bool esc)
    {
        Run(
            () => Press(Key.CursorDown, Key.CursorDown, Key.Tab, Key.CursorRight),
            () => Themes.Current == Themes.TurboPascal && Icons.Style == IconStyle.NerdFont,
            () => Press(Key.PageDown.WithCtrl),
            () => Swatches.ShowHexValues.HasFocus,
            () => Press(Key.Space),
            () => Swatches.HexDefault == "(default: on)",
            () =>
            {
                if (esc) Press(Key.Esc);
                else Dialog!.RequestStop();
            },
            () => _host.App.TopRunnableView is SwatchWindow window && window.ShowHexValues);

        Assert.Equal(Themes.Midnight, Themes.Current);
        Assert.Equal(IconStyle.Auto, Icons.Style);
        Assert.False(File.Exists(_host.SettingsPath));
    }

    [Fact]
    public void A_confirmed_reset_changes_the_page_and_Esc_on_Settings_undoes_it()
    {
        Saved.ShowHexValues = false;
        using var window = new SwatchWindow(_host.Shell);
        ConfirmDialog? confirm = null;

        Run(window,
            () => Press(Key.PageDown.WithCtrl),
            () => Swatches.ShowHexValues.HasFocus,
            () =>
            {
                Assert.Equal("(default: on)", Swatches.HexDefault);
                Assert.Equal(" R Reset to defaults… ", Swatches.ResetButton.Text);
            },
            () => Press(Key.R),
            () => (confirm = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => Press(Key.Esc),
            () => Dialog is not null,
            () =>
            {
                Assert.Equal(CheckState.UnChecked, Swatches.ShowHexValues.Value);
                Assert.Equal(["Appearance", "Swatches"], Names);
            },
            () => Press(Key.R),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () => Press(Key.Enter.WithCtrl),
            () => Dialog is not null && Swatches.ShowHexValues.Value == CheckState.Checked,
            () =>
            {
                Assert.Equal(["Appearance", "Swatches •"], Names);
                Assert.Equal("", Swatches.HexDefault);
                Assert.True(window.ShowHexValues);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.False(window.ShowHexValues);
        Assert.False(Saved.ShowHexValues);
    }

    [Fact]
    public void A_confirmed_reset_is_kept_by_Ctrl_Enter()
    {
        Saved.ShowHexValues = false;
        using var window = new SwatchWindow(_host.Shell);

        Run(window,
            () => Press(Key.PageDown.WithCtrl),
            () => Swatches.ShowHexValues.HasFocus,
            () => Press(Key.R),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () => Press(Key.Enter.WithCtrl),
            () => Dialog is not null && Swatches.ShowHexValues.Value == CheckState.Checked,
            () => Press(Key.Enter.WithCtrl),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.True(window.ShowHexValues);
        Assert.True(Saved.ShowHexValues);
    }
}
