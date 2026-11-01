using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class TerminalTitleHostTests : StaticConfigurationTest
{
    private const string Push = "\x1b[22;0t";
    private const string Pop = "\x1b[23;0t";
    private const string Osc = "\x1b]0;";

    private static readonly Key[] DownToDaylight = [.. Enumerable.Repeat(Key.CursorDown, SchemeNames.All.Count + SampleSchemes.All.Count + 2)];

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private IEnumerable<string> Titles => _host.Written
        .Where(written => written.StartsWith(Osc) || written is Push or Pop)
        .Select(written => written.StartsWith(Osc) ? written[Osc.Length..^1] : written);

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    [Fact]
    public void Swatch_names_the_theme_being_edited_and_gives_the_terminal_its_title_back()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => _host.Shell.StatusBar.FocusWord == "Themes",
            () => Press(DownToDaylight),
            () => window.Showing.Theme == Themes.Daylight);

        Assert.Equal([Push, "Midnight — Swatch", "Daylight — Swatch", Pop], Titles);
    }

    [Fact]
    public void Gives_the_terminal_its_title_back_when_Run_throws()
    {
        using var window = new AppWindow(_host.Shell);
        _host.Shell.Title.App = "Swatch";

        Assert.Throws<InvalidOperationException>(() => _host.Run(window, new Action(() => throw new InvalidOperationException())));

        Assert.Equal([Push, "Swatch", Pop], Titles);
    }

    [Fact]
    public void The_sample_app_shows_its_own_title_with_what_is_waiting_then_Swatch_has_its_own_back()
    {
        using var window = new SwatchWindow(_host.Shell);
        using var shell = new AppShell(_host.App, _host.Shell.Cursor);
        using var sample = new SampleWindow(shell, Themes.Current, TimeSpan.FromMilliseconds(20));
        var dialogWritesItsOwn = true;

        _host.Run(window,
            () => _host.Shell.RunNested(shell, sample, Themes.Current),
            () => sample.Files.State == Loading.LoadState.Loaded,
            () => shell.Commands.Execute(SampleWindow.LoadFailing),
            () => shell.StatusBar.UnreadErrors == 1,
            () => shell.Commands.Execute(SampleWindow.ThreeWaiting),
            () => { shell.Commands.Execute(ShellCommands.ShowMessages); },
            () => _host.App.TopRunnableView is Chrome.MessagesDialog { IsInitialized: true },
            () =>
            {
                dialogWritesItsOwn = _host.App.TopRunnableView!.Border.Settings.HasFlag(BorderSettings.TerminalTitle);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SampleWindow,
            () => shell.Commands.Execute(SampleWindow.ThreeWaiting),
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(
        [
            Push, "Midnight — Swatch",
            Push, "src — Swatch sample", "⚠ 1 src — Swatch sample", "⚠ 1 (3) src — Swatch sample",
            "(3) src — Swatch sample", "src — Swatch sample", Pop,
            "Midnight — Swatch", Pop,
        ], Titles);
        Assert.False(dialogWritesItsOwn);
    }
}
