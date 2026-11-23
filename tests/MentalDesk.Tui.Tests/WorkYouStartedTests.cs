using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Working;

namespace MentalDesk.Tui.Tests;

// A bare AppStatusBar and WorkLine, as an app without AppShell has them.
public class WorkYouStartedTests : StaticConfigurationTest
{
    private readonly FakeClock _clock = new();
    private readonly AppStatusBar _bar;
    private readonly WorkLine _line;

    public WorkYouStartedTests()
    {
        _bar = new AppStatusBar { Clock = _clock };
        _line = new WorkLine { Clock = _clock };
    }

    public override void Dispose()
    {
        _bar.Dispose();
        _line.Dispose();
        base.Dispose();
    }

    private Work Approving()
    {
        var work = _bar.ShowWork("Approving #12…")!;
        _clock.Advance(Work.Moment);
        return work;
    }

    [Fact]
    public void Work_on_the_bar_shows_only_after_a_moment()
    {
        _bar.ShowWork("Approving #12…");

        _clock.Advance(Work.Moment - TimeSpan.FromMilliseconds(1));
        var before = (_bar.Says, _bar.Spinning);
        _clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Equal(("", false), before);
        Assert.Equal(("Approving #12…", true), (_bar.Says, _bar.Spinning));
    }

    [Fact]
    public void Work_finished_within_a_moment_never_shows()
    {
        var work = _bar.ShowWork("Approving #12…")!;

        work.Done();
        _clock.Advance(Work.Moment);

        Assert.Equal(("", false), (_bar.Says, _bar.Spinning));
    }

    [Fact]
    public void Work_finished_within_a_moment_leaves_the_showing_error_alone()
    {
        _bar.ShowNews("Couldn't load files", Severity.Error);

        var work = _bar.ShowWork("Approving #12…")!;
        work.Done();
        _clock.Advance(Work.Moment);

        Assert.Equal(("Couldn't load files", Severity.Error), (_bar.Message, _bar.MessageSeverity));
    }

    [Fact]
    public void Work_replaces_the_older_message_once_it_shows()
    {
        _bar.ShowNews("Couldn't load files", Severity.Error);

        _bar.ShowWork("Approving #12…");
        var before = _bar.Says;
        _clock.Advance(Work.Moment);

        Assert.Equal("Couldn't load files", before);
        Assert.Equal(("Approving #12…", true), (_bar.Says, _bar.Spinning));
    }

    [Fact]
    public void Work_on_the_bar_outlives_the_lifetime_and_keys_then_its_reply_replaces_it()
    {
        var work = Approving();

        _clock.Advance(AppStatusBar.Lifetime * 2);
        _bar.KeyPressed();
        work.Report("Approving #12… 2 of 3");
        var running = _bar.Says;
        work.Done("#12 approved");

        Assert.Equal("Approving #12… 2 of 3", running);
        Assert.Equal("#12 approved", _bar.Message);
        Assert.False(_bar.Spinning);
    }

    [Fact]
    public void Work_that_fails_ends_in_error_news_that_counts_as_unread()
    {
        var work = Approving();

        work.Failed("Couldn't approve #12");

        Assert.Equal(("Couldn't approve #12", Severity.Error), (_bar.Message, _bar.MessageSeverity));
        Assert.Equal(1, _bar.UnreadErrors);
    }

    [Fact]
    public void A_newer_message_shows_over_running_work_which_comes_back_after_it()
    {
        Approving();

        _bar.ShowMessage("Copied 3 lines");
        var over = (_bar.Says, _bar.Spinning);
        _bar.KeyPressed();

        Assert.Equal(("Copied 3 lines", false), over);
        Assert.Equal(("Approving #12…", true), (_bar.Says, _bar.Spinning));
    }

    [Fact]
    public void A_second_start_of_the_same_work_is_refused_and_says_so()
    {
        var first = _bar.ShowWork("Publishing…", already: "Already publishing")!;
        _clock.Advance(Work.Moment);

        var second = _bar.ShowWork("Publishing…", already: "Already publishing");
        var said = _bar.Message;
        _bar.KeyPressed();

        Assert.Null(second);
        Assert.Equal("Already publishing", said);
        Assert.Equal("Publishing…", _bar.Says);
        first.Done("Published");
        Assert.NotNull(_bar.ShowWork("Publishing…", already: "Already publishing"));
    }

    [Fact]
    public void Work_with_no_stop_offered_ignores_a_stop()
    {
        var work = Approving();

        Assert.False(work.Stop());
        Assert.True(work.Running);
        Assert.Equal("Approving #12…", _bar.Says);
    }

    [Fact]
    public void A_pane_line_says_its_stop_key_and_stopping_keeps_the_count()
    {
        var searched = 0;
        Work? work = null;
        work = _line.Start("Finding… 0 files", ("Esc stop", () => work!.Stopped($"Stopped{HintRow.Separator}{searched:N0} files searched")));
        _clock.Advance(Work.Moment);
        searched = 1234;
        work.Report($"Finding… {searched:N0} files");
        var running = (_line.Says, _line.Spinning, work.Line);

        Assert.True(_line.Stop());

        Assert.Equal(("Finding… 1,234 files  •  Esc stop", true, "Finding… 1,234 files  •  Esc stop"), running);
        Assert.Equal(("Stopped  •  1,234 files searched", false), (_line.Says, _line.Spinning));
        Assert.Equal(WorkState.Stopped, work.State);
    }

    [Fact]
    public void A_pane_line_shows_nothing_for_work_done_within_a_moment()
    {
        var work = _line.Start("Finding… 0 files");

        work.Done();
        _clock.Advance(Work.Moment);

        Assert.False(_line.Visible);
    }
}
