using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class SampleSyncedTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private readonly FakeClock _clock = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private void Run(Func<SampleWindow, Delegate[]> steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        using var shell = new AppShell(_host.App, _host.Shell.Cursor);
        shell.StatusBar.Clock = _clock;
        using var sample = new SampleWindow(shell, Themes.Current, TimeSpan.FromMilliseconds(20));
        _host.Run(window, [() => _host.Shell.RunNested(shell, sample, Themes.Current), .. steps(sample)]);
    }

    private static void OnFolder(SampleWindow sample) => sample.Tree.SelectedObject = sample.Tree.Objects!.First();

    private static void OnFile(SampleWindow sample) => sample.Tree.SelectedObject = sample.Tree.Objects!.First().Children[0];

    private static string Resting(SampleWindow sample) => sample.Shell.StatusBar.RestingText;

    [Fact]
    public void Off_a_file_the_status_bar_says_when_files_synced_and_keeps_counting()
    {
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () =>
            {
                Assert.Equal("src/notes.md", Resting(sample));
                OnFolder(sample);
                Assert.Equal("Synced now", Resting(sample));
                _clock.Advance(TimeSpan.FromMinutes(1));
            },
            () => Resting(sample) == "Synced 1m ago",
            () => _clock.Advance(TimeSpan.FromMinutes(2)),
            () => Resting(sample) == "Synced 3m ago",
            () =>
            {
                OnFile(sample);
                Assert.Equal("src/notes.md", Resting(sample));
                OnFolder(sample);
                Assert.Equal("Synced 3m ago", Resting(sample));
                _host.App.InjectKey(Key.Esc);
            },
        ]);
    }

    [Fact]
    public void Reloading_starts_it_again_and_a_failed_reload_keeps_the_last_good_load()
    {
        DateTimeOffset? loaded = null;
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => _clock.Advance(TimeSpan.FromMinutes(5)),
            () => sample.Shell.Commands.Execute(SampleWindow.ReloadFiles),
            () => sample.Files.State == LoadState.Loaded && sample.LastLoaded == _clock.GetUtcNow(),
            () =>
            {
                OnFolder(sample);
                Assert.Equal("Synced now", Resting(sample));
                loaded = sample.LastLoaded;
                _clock.Advance(TimeSpan.FromMinutes(5));
                sample.Shell.Commands.Execute(SampleWindow.LoadFailing);
            },
            () => !sample.Files.Reloading,
            () =>
            {
                Assert.Equal(loaded, sample.LastLoaded);
                _clock.Advance(SampleWindow.TickEvery);
            },
            () => Resting(sample) == "Synced 5m ago",
            () => _host.App.InjectKey(Key.Esc),
        ]);
    }

    [Fact]
    public void The_view_menu_shows_the_exact_time_of_the_last_load()
    {
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () =>
            {
                Assert.Contains(sample.Shell.Menu!.Layout.Single(menu => menu.Title == "_View").Items, item => item?.CommandId == SampleWindow.ShowLastLoaded);
                _clock.Advance(TimeSpan.FromDays(1));
                sample.Shell.Commands.Execute(SampleWindow.ShowLastLoaded);
            },
            () => _host.App.TopRunnableView is LastLoadedDialog { IsInitialized: true },
            () =>
            {
                Assert.Equal("Last loaded 17 Oct 14:02", ((LastLoadedDialog)_host.App.TopRunnableView!).Text.Text);
                _host.App.InjectKey(Key.Esc);
            },
            () => _host.App.TopRunnableView is not LastLoadedDialog,
            () => _host.App.InjectKey(Key.Esc),
        ]);
    }
}
