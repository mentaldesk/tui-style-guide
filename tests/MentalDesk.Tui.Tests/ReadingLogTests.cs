using MentalDesk.Tui.Finding;
using MentalDesk.Tui.Shell;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class ReadingLogTests : StaticConfigurationTest
{
    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Type(string text) => Press([.. text.Select(c => new Key(c))]);

    private static int CaretLine(ReadingLogWindow window) => window.Log.Document!.GetLineByOffset(window.Log.CaretOffset).LineNumber;

    private static int LastLine(ReadingLogWindow window) => window.Log.Document!.LineCount;

    [Fact]
    public void Help_Reading_a_log_opens_a_read_only_log_scrolled_to_its_tail_and_Esc_goes_back()
    {
        using var window = new SwatchWindow(_host.Shell);
        ReadingLogWindow? log = null;
        var readOnly = false;
        var atTail = false;
        var focusWord = "";

        _host.Run(window,
            () => _host.Shell.StatusBar.FocusWord == "Themes",
            () => { _host.Shell.Commands.Execute(SwatchCommands.ReadLog); },
            () => (log = _host.App.TopRunnableView as ReadingLogWindow) is { } open && open.Log.HasFocus,
            () =>
            {
                readOnly = log!.Log.ReadOnly;
                atTail = CaretLine(log) == LastLine(log);
                focusWord = log.Shell.StatusBar.FocusWord;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.True(readOnly);
        Assert.True(atTail);
        Assert.Equal("Reading a log", focusWord);
    }

    [Fact]
    public void Lines_that_match_raise_the_count_while_the_found_line_stays_put()
    {
        using var shell = new AppShell(_host.App, _host.Shell.Cursor, _host.Shell.Icons);
        using var window = new ReadingLogWindow(shell, TimeSpan.FromHours(1));
        var found = 0;
        var caret = 0;

        _host.Run(window,
            () => window.Log.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => shell.FindBar.QueryField.HasFocus,
            () => Type("retry"),
            () => shell.FindBar.Current >= 0,
            () =>
            {
                found = shell.FindBar.Matches.Count;
                caret = window.Log.CaretOffset;
                window.Append("11:30:00  retry 2 of 3");
                window.Append("11:30:01  build finished");
            });

        Assert.Equal(found + 1, shell.FindBar.Matches.Count);
        Assert.Equal(caret, window.Log.CaretOffset);
        Assert.True(shell.FindBar.HoldsPlace);
        Assert.EndsWith($"of {found + 1}", shell.FindBar.Status);
    }

    [Fact]
    public void The_log_stops_following_its_tail_while_the_bar_holds_a_match_on_its_last_line()
    {
        using var shell = new AppShell(_host.App, _host.Shell.Cursor, _host.Shell.Icons);
        using var window = new ReadingLogWindow(shell, TimeSpan.FromHours(1));
        var found = 0;

        _host.Run(window,
            () => window.Log.HasFocus,
            () => window.Append("11:30:00  newest retry"),
            () => CaretLine(window) == LastLine(window),
            () => Press(Key.F.WithCtrl),
            () => shell.FindBar.QueryField.HasFocus,
            () => Type("newest"),
            () => shell.FindBar.Status == "1 of 1",
            () =>
            {
                found = CaretLine(window);
                window.Append("11:30:01  build started");
            });

        Assert.Equal(found, CaretLine(window));
        Assert.Equal(found + 1, LastLine(window));
    }

    [Fact]
    public void The_log_follows_its_tail_again_once_the_bar_closes()
    {
        using var shell = new AppShell(_host.App, _host.Shell.Cursor, _host.Shell.Icons);
        using var window = new ReadingLogWindow(shell, TimeSpan.FromHours(1));

        _host.Run(window,
            () => window.Log.HasFocus,
            () => window.Append("11:30:00  newest retry"),
            () => CaretLine(window) == LastLine(window),
            () => Press(Key.F.WithCtrl),
            () => shell.FindBar.QueryField.HasFocus,
            () => Type("newest"),
            () => shell.FindBar.HoldsPlace,
            () => Press(Key.Esc),
            () => !shell.FindBar.IsOpen && window.Log.HasFocus,
            () => window.Append("11:30:01  build started"));

        Assert.Equal(LastLine(window), CaretLine(window));
    }

    [Fact]
    public void Find_is_dimmed_in_Swatch_s_panes_which_have_nothing_to_search()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window, () => _host.Shell.StatusBar.FocusWord == "Themes");

        foreach (var region in new[] { SwatchWindow.ThemesRegion, SwatchWindow.RolesRegion, SwatchWindow.PreviewRegion })
            Assert.False(_host.Shell.Commands.IsAvailable(ShellCommands.Find, region.Scope));
    }
}
