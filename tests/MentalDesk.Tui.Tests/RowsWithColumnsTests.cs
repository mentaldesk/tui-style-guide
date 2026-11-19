using MentalDesk.Tui.Columns;
using MentalDesk.Tui.Palette;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class RowsWithColumnsTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private readonly FakeClock _clock = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private RowsWithColumnsDialog? Dialog => _host.App.TopRunnableView as RowsWithColumnsDialog;

    private CommandPalette? Palette => _host.App.TopRunnableView as CommandPalette;

    // On Windows the driver polls the console and undoes SetScreenSize, so each test sizes its dialog instead.
    private void Run(params Delegate[] steps)
    {
        _host.Shell.StatusBar.Clock = _clock;
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, [() => _host.Shell.StatusBar.FocusWord == "Themes", .. steps]);
    }

    [Fact]
    public void It_is_on_the_help_menu_beside_loading_states()
    {
        Run(() =>
        {
            var help = _host.Shell.Menu!.Layout.Single(menu => menu.Title == "_Help").Items.Select(item => item?.CommandId).ToList();
            Assert.Equal(help.IndexOf(SwatchCommands.ShowLoadingStates) + 1, help.IndexOf(SwatchCommands.ShowRowsWithColumns));
        });
    }

    [Fact]
    public void Wide_the_table_and_the_picker_show_every_column_with_the_path_cut_at_its_start()
    {
        Run(
            () => { _host.Shell.Commands.Execute(SwatchCommands.ShowRowsWithColumns); },
            () => Dialog is { List.Viewport.Width: > 0 } && Dialog.Rows.Count == 3,
            () => { Dialog!.Width = RowsWithColumnsDialog.MaxWidth; },
            () => Dialog!.Frame.Width == RowsWithColumnsDialog.MaxWidth,
            () =>
            {
                Assert.Equal(["What", "Runs", "Minutes", "Cost", "Last run"], Dialog!.Table.Fit.Placed.Select(placed => placed.Column.Header));
                Assert.Equal(
                    ["     now", "      3h", "      2d", "   7 Oct"],
                    Dialog.Table.Rows.Select(row => Dialog.Table.Fit.Row(row)[^8..]));
                Assert.Equal("Name             Where", Dialog.Header.Text);
                Assert.Equal("tui-style-guide  ~/code/tui-style-guide/main", Dialog.Rows[0]);
                Assert.Equal("TuiCode          …/code/TuiCode/side-pane-resize", Dialog.Rows[2]);
                Assert.True(Dialog.Table.HasFocus);
                _host.App.InjectKey(Key.Esc);
            },
            () => Dialog is null);
    }

    [Fact]
    public void Narrow_the_names_shrink_with_an_ellipsis_and_the_right_hand_columns_drop()
    {
        Run(
            () => { _host.Shell.Commands.Execute(SwatchCommands.ShowRowsWithColumns); },
            () => Dialog is { List.Viewport.Width: > 0 },
            () => { Dialog!.Width = 48; },
            () => Dialog!.Frame.Width == 48,
            () =>
            {
                Assert.Equal(["What", "Runs"], Dialog!.Table.Fit.Placed.Select(placed => placed.Column.Header));
                Assert.EndsWith("…", Dialog.Table.Fit.Row(Dialog.Table.Rows[3]).Split("  ")[0], StringComparison.Ordinal);
                Assert.Equal(string.Empty, Dialog.Header.Text);
                Assert.Equal(["tui-style-guide", "a-team", "TuiCode"], Dialog.Rows);
                _host.App.InjectKey(Key.Esc);
            },
            () => Dialog is null);
    }

    [Fact]
    public void On_a_narrow_terminal_the_palette_cuts_a_command_with_an_ellipsis_and_keeps_its_keys_whole()
    {
        Run(
            () => _host.App.InjectKey(Key.E.WithCtrl),
            () => Palette is not null,
            () => { Palette!.Width = 28; },
            () => Palette!.Frame.Width == 28,
            () =>
            {
                var row = Palette!.Labels[Palette.Shown.ToList().FindIndex(row => row.Label == "Delete this theme")];
                Assert.StartsWith("Delete this", row, StringComparison.Ordinal);
                Assert.Contains("…", row, StringComparison.Ordinal);
                Assert.EndsWith("Ctrl+T D", row, StringComparison.Ordinal);
                _host.App.InjectKey(Key.Esc);
            },
            () => Palette is null);
    }
}
