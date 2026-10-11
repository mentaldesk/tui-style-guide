using MentalDesk.Tui.Loading;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class LoadingStatesDialogTests : StaticConfigurationTest
{
    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, steps);
    }

    private LoadingStatesDialog? Dialog => _host.App.TopRunnableView as LoadingStatesDialog;

    [Fact]
    public void Help_opens_it_loading_with_a_button_for_each_key_and_no_hint_row()
    {
        Run(
            () => { _host.Shell.Commands.Execute(SwatchCommands.ShowLoadingStates); },
            () => Dialog is not null,
            () =>
            {
                Assert.Equal(LoadState.Loading, Dialog!.Rows.State);
                Assert.Equal(
                    [" R Reload ", " E Load empty ", " F Load failing ", " Esc Close "],
                    Dialog.SubViews.OfType<Button>().Select(button => button.Text));
                Assert.Empty(Dialog.SubViews.OfType<MentalDesk.Tui.Chrome.HintRow>());
                _host.App.InjectKey(Key.Esc);
            },
            () => Dialog is null);
    }

    [Fact]
    public void R_retries_a_failed_load_and_the_list_keeps_focus_throughout()
    {
        var focused = new List<bool>();
        LoadingStatesDialog? dialog = null;

        Run(() =>
        {
            dialog = new LoadingStatesDialog(_host.Shell, TimeSpan.FromMilliseconds(20));
            dialog.Run(_host.Shell);
            dialog.Dispose();
        },
            () => dialog?.Rows.State == LoadState.Loaded,
            () => _host.App.InjectKey(Key.E),
            () => dialog!.Rows.State == LoadState.Empty,
            () => focused.Add(dialog!.List.HasFocus),
            () => _host.App.InjectKey(Key.F),
            () => dialog!.Rows.State == LoadState.Failed,
            () => focused.Add(dialog!.List.HasFocus),
            () => _host.App.InjectKey(Key.R),
            () => dialog!.Rows.State == LoadState.Loaded,
            () => focused.Add(dialog!.List.HasFocus),
            () => _host.App.InjectKey(Key.Esc));

        Assert.Equal([true, true, true], focused);
    }

    [Fact]
    public void A_reload_that_fails_keeps_the_rows_and_says_so_on_the_status_bar()
    {
        LoadingStatesDialog? dialog = null;

        Run(() =>
        {
            dialog = new LoadingStatesDialog(_host.Shell, TimeSpan.FromMilliseconds(20));
            dialog.Run(_host.Shell);
            dialog.Dispose();
        },
            () => dialog?.Rows.State == LoadState.Loaded,
            () => _host.App.InjectKey(Key.F),
            () => _host.Shell.StatusBar.Message is not null,
            () =>
            {
                Assert.Equal(LoadState.Loaded, dialog!.Rows.State);
                Assert.Equal(5, dialog.List.Source!.Count);
                _host.App.InjectKey(Key.Esc);
            });
    }
}
