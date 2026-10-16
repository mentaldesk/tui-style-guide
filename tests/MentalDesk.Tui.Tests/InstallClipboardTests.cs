using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class InstallClipboardTests : StaticConfigurationTest
{
    private const int MaxIterations = 1000;

    private readonly IApplication _app = Application.Create();
    private readonly List<CopyOutcome> _copied = [];

    public override void Dispose()
    {
        _app.Dispose();
        base.Dispose();
    }

    private IClipboard Init(IClipboard? platform = null)
    {
        _app.Init(driverName: DriverRegistry.Names.ANSI);
        return _app.Driver!.Clipboard = platform ?? new FakeClipboard();
    }

    private TerminalClipboard Install()
    {
        var clipboard = TerminalClipboard.Install(_app, ClipboardProgram.Missing);
        clipboard.Copied += (_, outcome) => _copied.Add(outcome);
        return clipboard;
    }

    private AppShell Shell() => new(_app, new TerminalCursor(_ => { }), ClipboardProgram.Missing,
        new IconSettings(new MentalDeskSettings(Path.Combine(Path.GetTempPath(), $"mentaldesk-{Guid.NewGuid():N}", "settings.json")),
            () => new FontDetection(false, "a test terminal")));

    [Fact]
    public void Install_makes_the_app_s_clipboard_wrap_the_previous_one()
    {
        var platform = Init();

        var clipboard = Install();

        Assert.Same(clipboard, _app.Driver!.Clipboard);
        Assert.Same(platform, clipboard.Platform);
    }

    [Fact]
    public void Installing_twice_leaves_one_clipboard()
    {
        var platform = Init();

        var first = TerminalClipboard.Install(_app, ClipboardProgram.Missing);
        var second = TerminalClipboard.Install(_app, ClipboardProgram.Missing);

        Assert.Same(first, second);
        Assert.Same(first, _app.Driver!.Clipboard);
        Assert.Same(platform, first.Platform);
    }

    [Fact]
    public void An_AppShell_started_after_Install_shares_its_clipboard()
    {
        Init();
        var installed = TerminalClipboard.Install(_app, ClipboardProgram.Missing);

        using var shell = Shell();

        Assert.Same(installed, shell.Clipboard);
        Assert.Same(installed, _app.Driver!.Clipboard);
    }

    [Fact]
    public void Install_after_an_AppShell_returns_the_shell_s_clipboard()
    {
        Init();
        using var shell = Shell();

        Assert.Same(shell.Clipboard, TerminalClipboard.Install(_app, ClipboardProgram.Missing));
        Assert.Same(shell.Clipboard, _app.Driver!.Clipboard);
    }

    [Fact]
    public void With_no_driver_it_still_says_what_was_copied()
    {
        var clipboard = Install();

        clipboard.Copy("hello");

        Assert.Null(clipboard.Platform);
        Assert.Equal([new CopyOutcome.Copied(1, 5, ThroughTerminal: true)], _copied);
    }

    [Theory]
    [InlineData("Ctrl+C", "hello")]
    [InlineData("Ctrl+X", "")]
    public void A_text_field_s_copy_goes_through_the_terminal(string key, string left)
    {
        Init();
        Install();
        var field = new TextField { Width = 20, Text = "hello" };

        Run(field,
            () => field.HasFocus,
            () =>
            {
                field.SelectAll();
                _app.InjectKey(Key.TryParse(key, out var parsed) ? parsed : throw new ArgumentException(key));
            },
            () => _copied.Count > 0);

        Assert.Equal([new CopyOutcome.Copied(1, 5, ThroughTerminal: true)], _copied);
        Assert.Equal(left, field.Text);
    }

    [Fact]
    public void A_text_view_s_copy_goes_through_the_terminal()
    {
        Init();
        Install();
#pragma warning disable CS0618
        var view = new TextView { Width = 20, Height = 3, Text = "one\ntwo" };
#pragma warning restore CS0618

        Run(view,
            () => view.HasFocus,
            () =>
            {
                view.SelectAll();
                _app.InjectKey(Key.C.WithCtrl);
            },
            () => _copied.Count > 0);

        Assert.Equal([new CopyOutcome.Copied(2, 6, ThroughTerminal: true)], _copied);
    }

    [Fact]
    public void The_app_s_own_copy_says_when_it_is_too_large()
    {
        Init(new TestClipboard { Transform = _ => null });

        var outcome = Install().Copy(new string('x', TerminalClipboard.MaxBytes + 1));

        Assert.Equal(new CopyOutcome.Failed(TerminalClipboard.TooLarge), outcome);
        Assert.Equal("Copy failed: too large to send through the terminal (the limit is 750 KB)", outcome.Message);
        Assert.Equal(Severity.Error, outcome.Severity);
        Assert.Equal([outcome], _copied);
    }

    private void Run(View content, params Delegate[] steps)
    {
        using var window = new Window();
        window.Add(content);
        var queue = new Queue<Delegate>(steps);
        var iterations = 0;
        _app.Iteration += Step;
        _app.Run(window);
        _app.Iteration -= Step;
        Assert.True(queue.Count == 0, $"{queue.Count} step(s) never completed");

        void Step(object? sender, EventArgs e)
        {
            if (queue.Count == 0 || ++iterations > MaxIterations)
            {
                _app.RequestStop();
                return;
            }
            switch (queue.Peek())
            {
                case Func<bool> poll:
                    if (poll()) queue.Dequeue();
                    break;
                case Action act:
                    queue.Dequeue();
                    act();
                    break;
            }
        }
    }
}
