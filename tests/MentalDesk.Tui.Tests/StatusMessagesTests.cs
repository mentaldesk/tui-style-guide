using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Tests;

// A bare AppStatusBar, as an app without AppShell has it.
public class StatusMessagesTests : StaticConfigurationTest
{
    private static readonly TimeSpan JustUnder = AppStatusBar.Lifetime - TimeSpan.FromMilliseconds(1);

    private readonly FakeClock _clock = new();
    private readonly AppStatusBar _bar;

    public StatusMessagesTests() => _bar = new AppStatusBar { Clock = _clock };

    public override void Dispose()
    {
        _bar.Dispose();
        base.Dispose();
    }

    [Fact]
    public void A_reply_clears_at_the_next_key()
    {
        _bar.ShowMessage("Saved");

        _bar.KeyPressed();

        Assert.Null(_bar.Message);
    }

    [Fact]
    public void A_reply_clears_after_five_seconds_with_no_key()
    {
        _bar.ShowMessage("Saved");

        _clock.Advance(JustUnder);
        var before = _bar.Message;
        _clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Equal("Saved", before);
        Assert.Null(_bar.Message);
    }

    [Fact]
    public void An_error_reply_does_not_time_out_but_clears_at_the_next_key()
    {
        _bar.ShowMessage("Couldn't open the link", Severity.Error);

        _clock.Advance(TimeSpan.FromMinutes(1));
        var after = _bar.Message;
        _bar.KeyPressed();

        Assert.Equal("Couldn't open the link", after);
        Assert.Null(_bar.Message);
    }

    [Theory]
    [InlineData(Severity.Info)]
    [InlineData(Severity.Error)]
    public void News_survives_keys_for_its_first_five_seconds(Severity severity)
    {
        _bar.ShowNews("notes.md changed on disk", severity);

        _bar.KeyPressed();
        _clock.Advance(JustUnder);
        _bar.KeyPressed();

        Assert.Equal("notes.md changed on disk", _bar.Message);
    }

    [Fact]
    public void Info_news_then_clears_on_its_own()
    {
        _bar.ShowNews("notes.md changed on disk");

        _clock.Advance(AppStatusBar.Lifetime);

        Assert.Null(_bar.Message);
    }

    [Fact]
    public void Error_news_then_clears_at_the_next_key_and_never_on_a_timer()
    {
        _bar.ShowNews("Couldn't list files", Severity.Error);

        _clock.Advance(TimeSpan.FromMinutes(1));
        var after = _bar.Message;
        _bar.KeyPressed();

        Assert.Equal("Couldn't list files", after);
        Assert.Null(_bar.Message);
    }

    [Fact]
    public void Only_error_news_counts_as_unread()
    {
        _bar.ShowMessage("Saved");
        _bar.ShowMessage("Couldn't open the link", Severity.Error);
        _bar.ShowNews("notes.md changed on disk");
        var none = (_bar.UnreadErrors, _bar.UnreadButton.Visible);

        _bar.ShowNews("Couldn't list files", Severity.Error);
        _bar.ShowNews("Couldn't list files", Severity.Error);

        Assert.Equal((0, false), none);
        Assert.Equal(2, _bar.UnreadErrors);
        Assert.True(_bar.UnreadButton.Visible);
        Assert.Equal("⚠ 2", _bar.UnreadButton.Text);
        Assert.Equal(SchemeNames.Error, _bar.UnreadButton.SchemeName);
    }

    [Fact]
    public void Opening_messages_resets_the_count_and_hides_it()
    {
        _bar.ShowNews("Couldn't list files", Severity.Error);

        using var dialog = new MessagesDialog(_bar);

        Assert.Equal(0, _bar.UnreadErrors);
        Assert.False(_bar.UnreadButton.Visible);
    }

    [Fact]
    public void The_unread_count_sits_just_before_the_state_with_a_separator()
    {
        _bar.State = "Daylight";
        _bar.Hints.Show([("Press F1 for help", () => { })]);
        _bar.ShowNews("Couldn't list files", Severity.Error);

        _bar.Layout(new System.Drawing.Size(120, 1));

        var right = _bar.SubViews.Last();
        var says = string.Concat(right.SubViews.Where(view => view.Visible).OrderBy(view => view.Frame.X).Select(view => view is HintRow row ? row.Says : view.Text));
        Assert.Equal("⚠ 1  •  Daylight  •  Press F1 for help", says);
    }

    [Fact]
    public void Withdrawn_news_comes_off_the_bar()
    {
        var failure = _bar.ShowNews("Couldn't list files", Severity.Error);

        _bar.Withdraw(failure);

        Assert.Null(_bar.Message);
    }

    [Fact]
    public void Withdrawing_news_no_longer_showing_leaves_the_bar_alone()
    {
        var failure = _bar.ShowNews("Couldn't list files", Severity.Error);
        _bar.ShowMessage("Saved");

        _bar.Withdraw(failure);

        Assert.Equal("Saved", _bar.Message);
    }

    public static TheoryData<MessageKind, Severity, MessageKind, Severity> Pairings()
    {
        var data = new TheoryData<MessageKind, Severity, MessageKind, Severity>();
        foreach (var first in Enum.GetValues<MessageKind>())
        foreach (var firstSeverity in Enum.GetValues<Severity>())
        foreach (var second in Enum.GetValues<MessageKind>())
        foreach (var secondSeverity in Enum.GetValues<Severity>())
            data.Add(first, firstSeverity, second, secondSeverity);
        return data;
    }

    [Theory]
    [MemberData(nameof(Pairings))]
    public void The_latest_message_wins(MessageKind first, Severity firstSeverity, MessageKind second, Severity secondSeverity)
    {
        Show("first", first, firstSeverity);

        Show("second", second, secondSeverity);

        Assert.Equal("second", _bar.Message);
        Assert.Equal(secondSeverity, _bar.MessageSeverity);
        Assert.Equal(second, _bar.Current!.Kind);
    }

    [Fact]
    public void A_replaced_message_s_timer_does_not_clear_the_new_one()
    {
        _bar.ShowMessage("Saved");
        _clock.Advance(TimeSpan.FromSeconds(3));
        _bar.ShowMessage("Copied");

        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal("Copied", _bar.Message);
    }

    [Fact]
    public void A_pending_chord_shows_over_a_message_which_comes_back_with_its_lifetime_unaffected()
    {
        _bar.ShowMessage("Saved");

        _bar.SetChord("Ctrl+G");
        var during = _bar.Says;
        _bar.SetChord(null);
        var after = _bar.Says;
        _clock.Advance(AppStatusBar.Lifetime);

        Assert.Equal("Ctrl+G…", during);
        Assert.Equal("Saved", after);
        Assert.Null(_bar.Message);
    }

    [Fact]
    public void Every_message_is_kept_with_its_time()
    {
        _bar.ShowMessage("Saved");
        _clock.Advance(TimeSpan.FromMinutes(1));
        _bar.ShowNews("Couldn't list files", Severity.Error);

        Assert.Equal(["14:02  Saved", "14:03  Couldn't list files"], _bar.History.Select(message => message.ToString()));
    }

    [Fact]
    public void Messages_lists_newest_first_with_errors_in_the_error_scheme()
    {
        _bar.ShowMessage("Swatch is now in Daylight");
        _clock.Advance(TimeSpan.FromMinutes(3));
        _bar.ShowMessage("Copied 3 lines  •  142 characters");
        _clock.Advance(TimeSpan.FromMinutes(1));
        _bar.ShowNews("Couldn't list files, so these are the files from the last load", Severity.Error);

        using var dialog = new MessagesDialog(_bar);

        Assert.Equal(
            ["14:06  Couldn't list files, so these are the files from the last load", "14:05  Copied 3 lines  •  142 characters", "14:02  Swatch is now in Daylight"],
            dialog.List.Source!.ToList().Cast<string>());
        Assert.Equal(0, dialog.List.SelectedItem);
        Assert.Equal(Terminal.Gui.Configuration.SchemeManager.GetScheme(SchemeNames.Error).Normal, dialog.RowAttribute(0));
        Assert.Null(dialog.RowAttribute(1));
        Assert.False(dialog.Empty.Visible);
    }

    [Fact]
    public void Messages_with_nothing_yet_says_so_dimmed()
    {
        using var dialog = new MessagesDialog(_bar);

        Assert.True(dialog.Empty.Visible);
        Assert.False(dialog.Empty.Enabled);
        Assert.Equal("No messages yet", dialog.Empty.Text);
        Assert.False(dialog.List.Visible);
        Assert.False(dialog.CopyButton.Enabled);
    }

    private void Show(string text, MessageKind kind, Severity severity)
    {
        if (kind == MessageKind.Reply)
            _bar.ShowMessage(text, severity);
        else
            _bar.ShowNews(text, severity);
    }
}
