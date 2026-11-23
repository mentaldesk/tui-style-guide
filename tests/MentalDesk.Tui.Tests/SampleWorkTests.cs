using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using MentalDesk.Tui.Working;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class SampleWorkTests : StaticConfigurationTest
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

    // Each step of a find sets the next on the UI thread, so the clock moves one step per poll.
    private Func<bool> StepUntil(Func<bool> done) => () =>
    {
        if (done()) return true;
        _clock.Advance(SampleWindow.FindStep);
        return false;
    };

    [Fact]
    public void Find_in_files_counts_up_in_the_files_pane_and_Esc_there_stops_it_keeping_what_it_found()
    {
        string running = "";
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.FindInFiles),
            () => _clock.Advance(Work.Moment),
            () => sample.FilesLine.Spinning,
            StepUntil(() => sample.Searched >= 9 * SampleWindow.FilesPerStep),
            () =>
            {
                running = sample.FilesLine.Says;
                Assert.Equal(SampleWindow.FilesRegion, sample.Shell.Focus.Region);
                _host.App.InjectKey(Key.Esc);
            },
            () => !sample.FilesLine.Spinning,
            () =>
            {
                Assert.Equal("Stopped  •  333 files searched", sample.FilesLine.Says);
                Assert.Equal(["App.cs"], sample.Tree.Objects!.Select(node => node.Text));
                _host.App.InjectKey(Key.Esc);
            },
        ]);

        Assert.Equal("Finding… 333 files  •  Esc stop", running);
    }

    [Fact]
    public void Find_in_files_left_to_finish_leaves_its_result_and_no_spinner()
    {
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.FindInFiles),
            () => _clock.Advance(Work.Moment),
            () => sample.FilesLine.Spinning,
            StepUntil(() => sample.FilesLine.Current?.Running == false),
            () =>
            {
                Assert.False(sample.FilesLine.Visible);
                Assert.Equal(7, sample.Tree.Objects!.Count());
                _host.App.InjectKey(Key.Esc);
            },
        ]);
    }

    [Fact]
    public void Publish_stays_on_the_status_bar_through_keys_and_ends_in_its_reply()
    {
        var says = new List<string>();
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.Publish),
            () => _clock.Advance(Work.Moment),
            () => sample.Shell.StatusBar.Spinning,
            () =>
            {
                _clock.Advance(AppStatusBar.Lifetime);
                _host.App.InjectKey(Key.CursorDown);
            },
            () =>
            {
                says.Add(sample.Shell.StatusBar.Says);
                sample.Shell.Commands.Execute(SampleWindow.Publish);
                says.Add(sample.Shell.StatusBar.Says);
                sample.Shell.Commands.Execute(SampleWindow.SaveFile);
                says.Add(sample.Shell.StatusBar.Says);
                sample.Shell.StatusBar.KeyPressed();
                says.Add(sample.Shell.StatusBar.Says);
                _clock.Advance(SampleWindow.PublishTime);
            },
            () => sample.Shell.StatusBar.Message == "Published",
            () =>
            {
                Assert.False(sample.Shell.StatusBar.Spinning);
                _host.App.InjectKey(Key.Esc);
            },
        ]);

        Assert.Equal(["Publishing…", "Already publishing", "A demo: this would have saved the file", "Publishing…"], says);
    }

    [Fact]
    public void A_publish_that_fails_ends_in_an_error_that_counts_as_unread()
    {
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.PublishFailing),
            () => _clock.Advance(SampleWindow.PublishTime),
            () => sample.Shell.StatusBar.Message is not null,
            () =>
            {
                Assert.Equal(Severity.Error, sample.Shell.StatusBar.MessageSeverity);
                Assert.Equal(1, sample.Shell.StatusBar.UnreadErrors);
                _host.App.InjectKey(Key.Esc);
            },
        ]);
    }

    [Fact]
    public void Esc_with_no_find_running_doesnt_stop_publish()
    {
        Work? work = null;
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () =>
            {
                sample.Shell.Commands.Execute(SampleWindow.Publish);
                work = sample.Shell.StatusBar.Working;
                _clock.Advance(Work.Moment);
            },
            () => sample.Shell.StatusBar.Working is not null,
            () =>
            {
                work = sample.Shell.StatusBar.Working;
                Assert.DoesNotContain("Esc stop", sample.Shell.StatusBar.Says);
                Assert.False(sample.Shell.Commands.IsEnabled(SampleWindow.StopFinding));
                Assert.False(work!.Stop());
                _host.App.InjectKey(Key.Esc);
            },
        ]);

        Assert.True(work!.Running);
    }
}
