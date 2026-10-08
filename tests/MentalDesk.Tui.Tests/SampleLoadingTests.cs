using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class SampleLoadingTests : StaticConfigurationTest
{
    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private void Run(Func<SampleWindow, Delegate[]> steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        using var shell = new AppShell(_host.App, _host.Shell.Cursor);
        using var sample = new SampleWindow(shell, Themes.Current, TimeSpan.FromMilliseconds(20));
        _host.Run(window, [() => _host.Shell.RunNested(shell, sample, Themes.Current), .. steps(sample)]);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    [Fact]
    public void The_files_pane_loads_then_shows_the_files()
    {
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () =>
            {
                Assert.Equal("notes.md", sample.Tree.SelectedObject?.Text);
                Press(Key.Esc);
            },
        ]);
    }

    [Fact]
    public void The_view_menu_shows_empty_and_failed_in_the_pane_and_Ctrl_R_retries()
    {
        var says = new List<string>();

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.LoadNoFiles),
            () => sample.Files.State == LoadState.Empty,
            () => says.Add(sample.Files.Says),
            () => sample.Shell.Commands.Execute(SampleWindow.LoadFailing),
            () => sample.Files.State == LoadState.Failed,
            () => says.Add(sample.Files.Says),
            () => Press(Key.R.WithCtrl),
            () => sample.Files.State == LoadState.Loaded,
            () => Press(Key.Esc),
        ]);

        Assert.Equal(["No files", "Couldn't list files"], says);
    }

    [Fact]
    public void A_reload_that_fails_keeps_the_files_and_says_so_on_the_status_bar()
    {
        var said = "";

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.LoadFailing),
            () => sample.Shell.StatusBar.Message is not null,
            () =>
            {
                said = sample.Shell.StatusBar.Message!;
                Assert.Equal(LoadState.Loaded, sample.Files.State);
                Press(Key.Esc);
            },
        ]);

        Assert.Equal("Couldn't list files, so these are the files from the last load", said);
    }
}
