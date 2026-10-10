using System.Drawing;
using System.Reflection;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Diagnostics;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Palette;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class DialogKeysTests : StaticConfigurationTest
{
    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private KeysDialog? Keys => _host.App.TopRunnableView as KeysDialog;

    private CommandPalette? Palette => _host.App.TopRunnableView as CommandPalette;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, [() => _host.Shell.StatusBar.FocusWord == "Themes", .. steps]);
    }

    private static IEnumerable<string> Rows(KeySheet sheet) =>
        sheet.Here!.Groups.SelectMany(group => group.Rows).Select(row => $"{row.Keys} {row.Label}");

    private string ScreenRow(View view, int row)
    {
        var origin = view.ViewportToScreen(Point.Empty);
        var contents = _host.App.Driver!.Contents!;
        return string.Concat(Enumerable.Range(origin.X, view.Viewport.Width).Select(col => contents[origin.Y + row, col].Grapheme)).TrimEnd();
    }

    [Fact]
    public void F1_in_the_palette_lists_its_keys_under_its_title_and_nothing_from_the_app()
    {
        KeySheet? sheet = null;
        Run(
            () => Press(Key.E.WithCtrl),
            () => Palette is not null,
            () => Press(Key.F1),
            () => Keys is not null,
            () => { sheet = Keys!.Sheet; },
            () => Press(Key.Esc),
            () => Palette is not null,
            () => Press(Key.Esc));

        Assert.Equal("Here: Commands", sheet!.Here!.Heading);
        Assert.Null(sheet.Everywhere);
        Assert.Equal(["Enter Run", "↑ Previous", "↓ Next", "PageUp Page up", "PageDown Page down", "Esc Cancel"], Rows(sheet));
    }

    [Fact]
    public void F1_in_a_confirm_lists_its_buttons_keys_and_closing_the_sheet_puts_focus_back()
    {
        ConfirmDialog? dialog = null;
        KeySheet? sheet = null;
        Run(
            () => Press(Key.T.WithCtrl, Key.C),
            () => (dialog = _host.App.TopRunnableView as ConfirmDialog) is not null,
            () => Press(Key.CursorLeft),
            () => dialog!.ButtonFor(ThemeConfirms.DontSave).HasFocus,
            () => Press(Key.F1),
            () => Keys is not null,
            () => { sheet = Keys!.Sheet; },
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView == dialog,
            () => dialog!.ButtonFor(ThemeConfirms.DontSave).HasFocus,
            () => Press(Key.Esc));

        Assert.Equal("Here: Unsaved changes", sheet!.Here!.Heading);
        Assert.Equal(["← Previous button", "→ Next button", "S Save", "D Don't save", "Esc Cancel"], Rows(sheet));
    }

    [Fact]
    public void F1_in_the_keys_dialog_opened_from_a_dialog_does_nothing()
    {
        var opened = new List<KeysDialog>();
        var ticks = 0;
        Run(
            () => Press(Key.F12),
            () => _host.App.TopRunnableView is DiagnosticsDialog,
            () => Press(Key.F1),
            () => Keys is not null,
            () => opened.Add(Keys!),
            () => Press(Key.F1),
            () => ++ticks > 5,
            () => Keys == opened.Single(),
            () => Assert.Equal(["Esc Close"], Rows(Keys!.Sheet)),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is DiagnosticsDialog,
            () => Press(Key.Esc));
    }

    [Fact]
    public void A_dialog_with_no_code_of_its_own_gets_F1()
    {
        KeySheet? sheet = null;
        Run(
            () =>
            {
                using var bare = new BareDialog();
                bare.Run(_host.Shell);
            },
            () => _host.App.TopRunnableView is BareDialog,
            () => Press(Key.F1),
            () => Keys is not null,
            () => { sheet = Keys!.Sheet; },
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is BareDialog,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("Here: Bare", sheet!.Here!.Heading);
        Assert.Equal(["Esc Cancel"], Rows(sheet));
    }

    [Fact]
    public void AppDialog_has_no_hint_row()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.DoesNotContain(typeof(AppDialog).GetProperties(all), property => property.PropertyType == typeof(HintRow));
        Assert.DoesNotContain(typeof(AppDialog).GetMethods(all), method => method.GetParameters().Any(p => p.ParameterType == typeof(Hint[])));
    }

    [Fact]
    public void The_palette_has_Run_and_Cancel_buttons_that_do_what_their_keys_do()
    {
        Run(
            () => Press(Key.E.WithCtrl),
            () => Palette is not null,
            () =>
            {
                Assert.Equal(" Enter Run ", Palette!.PickButton.Text);
                Assert.Equal(" Esc Cancel ", Palette.CancelButton.Text);
                Assert.Empty(Palette.SubViews.OfType<HintRow>());
            },
            () => { Palette!.CancelButton.InvokeCommand(Command.Accept); },
            () => _host.App.TopRunnableView is SwatchWindow,
            () => Press(Key.E.WithCtrl),
            () => Palette is not null,
            () => Press([.. "go to roles".Select(c => new Key(c))]),
            () => Palette!.Shown.FirstOrDefault()?.Label == "Go to roles",
            () => { Palette!.PickButton.InvokeCommand(Command.Accept); },
            () => _host.App.TopRunnableView is SwatchWindow && _host.Shell.StatusBar.FocusWord == "Roles");
    }

    [Fact]
    public void The_scheme_picker_has_a_Go_button()
    {
        Run(
            () => Press(Key.G.WithCtrl, Key.S),
            () => _host.App.TopRunnableView is PickerDialog<SwatchWindow.Selection>,
            () =>
            {
                var picker = (PickerDialog<SwatchWindow.Selection>)_host.App.TopRunnableView!;
                Assert.Equal(" Enter Go ", picker.PickButton.Text);
                Assert.Equal(" Esc Cancel ", picker.CancelButton.Text);
                Press(Key.Esc);
            });
    }

    [Fact]
    public void The_empty_filter_shows_a_dim_placeholder_that_typing_replaces()
    {
        Run(
            () => Press(Key.E.WithCtrl),
            () => Palette is not null,
            () => ScreenRow(Palette!.FilterField, 0) == "Type to filter",
            () => Press(Key.G),
            () => ScreenRow(Palette!.FilterField, 0) == "g",
            () => Press(Key.Backspace),
            () => ScreenRow(Palette!.FilterField, 0) == "Type to filter",
            () => Press(Key.Esc));
    }

    [Fact]
    public void Diagnostics_says_to_press_a_key_in_its_body_and_closes_from_its_button()
    {
        Run(
            () => Press(Key.F12),
            () => _host.App.TopRunnableView is DiagnosticsDialog,
            () =>
            {
                var diagnostics = (DiagnosticsDialog)_host.App.TopRunnableView!;
                Assert.Contains(diagnostics.SubViews, view => view.Text == "Press any key to see it here");
                Assert.Equal(" Esc Close ", diagnostics.CloseButton.Text);
                Assert.Empty(diagnostics.SubViews.OfType<HintRow>());
                diagnostics.CloseButton.InvokeCommand(Command.Accept);
            },
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void The_keys_dialog_closes_from_its_button()
    {
        Run(
            () => Press(Key.F1),
            () => Keys is not null,
            () => { Keys!.CloseButton.InvokeCommand(Command.Accept); },
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void Each_loading_states_button_does_what_its_key_does()
    {
        LoadingStatesDialog? dialog = null;
        Button ButtonFor(string text) => dialog!.SubViews.OfType<Button>().Single(button => button.Text == text);

        Run(
            () =>
            {
                dialog = new LoadingStatesDialog(_host.Shell, TimeSpan.FromMilliseconds(20));
                dialog.Run(_host.Shell);
                dialog.Dispose();
            },
            () => dialog?.Rows.State == LoadState.Loaded,
            () => { ButtonFor(" E Load empty ").InvokeCommand(Command.Accept); },
            () => dialog!.Rows.State == LoadState.Empty,
            () => { ButtonFor(" F Load failing ").InvokeCommand(Command.Accept); },
            () => dialog!.Rows.State == LoadState.Failed,
            () => { ButtonFor(" R Reload ").InvokeCommand(Command.Accept); },
            () => dialog!.Rows.State == LoadState.Loaded,
            () => { ButtonFor(" Esc Close ").InvokeCommand(Command.Accept); },
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    private sealed class BareDialog() : AppDialog("Bare", width: 40, contentRows: 3);
}
