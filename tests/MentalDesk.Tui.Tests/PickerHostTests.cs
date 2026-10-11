using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class PickerHostTests : StaticConfigurationTest
{
    private readonly Host _host;

    public PickerHostTests()
    {
        _host = new Host();
    }

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private CommandPalette? Palette => _host.App.TopRunnableView as CommandPalette;

    private IEnumerable<string> Shown => Palette!.Shown.Select(row => row.Label);

    private PickerDialog<SwatchWindow.Selection>? SchemePicker => _host.App.TopRunnableView as PickerDialog<SwatchWindow.Selection>;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Type(string text) => Press([.. text.Select(c => new Key(c))]);

    private void InPalette(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, [
            () => FocusWord == "Themes",
            () => Press(Key.E.WithCtrl),
            () => Palette is not null,
            .. steps,
        ]);
    }

    [Theory]
    [InlineData("gt")]
    [InlineData("GT")]
    public void Typing_humps_lists_the_matches_with_the_first_selected(string query) =>
        InPalette(
            () => Type(query),
            () => Shown.SequenceEqual(["Go to preview", "Go to roles", "Go to scheme…", "Go to themes"]),
            () => Palette!.SubViews.OfType<ListView>().Single().SelectedItem == 0,
            () => Press(Key.Esc));

    [Theory]
    [InlineData("gtt")]
    [InlineData("gotot")]
    public void More_letters_narrow_to_one(string query) =>
        InPalette(
            () => Type(query),
            () => Shown.SequenceEqual(["Go to themes"]),
            () => Press(Key.Esc));

    [Fact]
    public void Letters_inside_a_word_still_find_the_command_after_the_hump_matches() =>
        InPalette(
            () => Type("heme"),
            () => Shown.Contains("Go to themes"),
            () => Press(Key.Esc));

    [Fact]
    public void Hidden_command_ids_never_match() =>
        InPalette(
            () => Type("app"),
            () => Palette!.NoMatches,
            () => Press(Key.Esc));

    [Fact]
    public void The_key_shown_beside_a_command_matches() =>
        InPalette(
            () => Type("f12"),
            () => Shown.SequenceEqual(["Show diagnostics"]),
            () => Press(Key.Esc));

    [Fact]
    public void Nothing_matching_says_so_dims_Enter_and_Enter_does_nothing()
    {
        var ticks = 0;
        InPalette(
            () => Palette!.PickButton.Enabled,
            () => Type("xyz"),
            () => Palette!.NoMatches,
            () => !Palette!.PickButton.Enabled,
            () => Press(Key.Enter),
            () => ++ticks > 5,
            () => Palette is not null,
            () => Press(Key.Backspace, Key.Backspace, Key.Backspace),
            () => Palette!.PickButton.Enabled && !Palette.NoMatches,
            () => Press(Key.Esc));
    }

    [Fact]
    public void Navigation_keys_move_the_selection_and_leave_the_cursor_in_the_field()
    {
        var list = (ListView?)null;
        InPalette(
            () => { list = Palette!.SubViews.OfType<ListView>().Single(); },
            () => Type("gt"),
            () => list!.SelectedItem == 0,
            () => Press(Key.CursorDown, Key.CursorDown),
            () => list!.SelectedItem == 2,
            () => Palette!.SubViews.OfType<TextField>().Single().HasFocus,
            () => Press(Key.CursorUp),
            () => list!.SelectedItem == 1,
            () => Press(Key.PageDown),
            () => list!.SelectedItem == 3,
            () => Press(Key.PageUp),
            () => list!.SelectedItem == 0,
            () => Palette!.SubViews.OfType<TextField>().Single().Text == "gt",
            () => Press(Key.CursorDown, Key.Enter),
            () => _host.App.TopRunnableView is SwatchWindow && FocusWord == "Roles");
    }

    [Fact]
    public void With_the_filter_empty_commands_keep_their_usual_order() =>
        InPalette(
            () => Shown.SequenceEqual(Shown.Order(StringComparer.OrdinalIgnoreCase)),
            () => Press(Key.Esc));

    [Fact]
    public void The_scheme_picker_opens_from_the_palette_and_goes_to_the_scheme()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.E.WithCtrl),
            () => Palette is not null,
            () => Type("gts"),
            () => Shown.SequenceEqual(["Go to scheme…"]),
            () => Press(Key.Enter),
            () => SchemePicker is not null,
            () => SchemePicker!.Labels.Count == Themes.Names.Count * SchemeNames.All.Count,
            () => Type("dbd"),
            () => SchemePicker!.Labels.SequenceEqual(["Daylight › ButtonDanger"]),
            () => Press(Key.Enter),
            () => _host.App.TopRunnableView is SwatchWindow,
            () => window.Showing == new SwatchWindow.Selection(Themes.Daylight, SchemeNames.ButtonDanger),
            () => FocusWord == "Themes");
    }

    [Fact]
    public void The_scheme_picker_ranks_humps_first_then_contains_and_says_when_nothing_matches()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.G.WithCtrl, Key.S),
            () => SchemePicker is not null,
            () => Type("dd"),
            () => SchemePicker!.Labels.SequenceEqual(["Daylight › Dialog", "Daylight › ButtonDanger"]),
            () => Press(Key.Backspace, Key.Backspace),
            () => Type("anger"),
            () => SchemePicker!.Labels.All(label => label.EndsWith("ButtonDanger", StringComparison.Ordinal)),
            () => Type("xyz"),
            () => SchemePicker!.NoMatches,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow,
            () => window.Showing == new SwatchWindow.Selection(Themes.Midnight, SchemeNames.Base));
    }
}
