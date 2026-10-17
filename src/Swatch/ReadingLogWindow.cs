using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Finding;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Shell;

namespace Swatch;

// A log that gains a line a second, to try the find bar on.
internal sealed class ReadingLogWindow : AppWindow
{
    public const string Back = "log.back";

    public static readonly FocusRegion LogRegion = new("Reading a log", new CommandScope("Reading a log"));

    private static readonly string[] Messages =
    [
        "build started",
        "restore failed: 503, retry in 5s",
        "retry 1 of 3",
        "restore ok",
        "compile MentalDesk.Tui",
        "compile Swatch",
        "test: Clipboard.Osc52 retry on timeout",
        "test: 412 passed, 0 failed",
        "publish swatch with native AOT",
        "upload artifact: connection reset, retrying",
        "upload ok",
        "build finished in 2m 41s",
    ];

    private static readonly DateTime Start = new(2026, 1, 1, 11, 2, 14);

    private readonly TimeSpan _every;
    private object? _timer;
    private int _written;

    public ReadingLogWindow(AppShell shell, TimeSpan? every = null) : base(Register(shell))
    {
        Shell = shell;
        _every = every ?? TimeSpan.FromSeconds(1);
        Log = new Terminal.Gui.Editor.Editor { Width = Dim.Fill(), Height = Dim.Fill(), ReadOnly = true };
        Log.Text = string.Join('\n', Enumerable.Range(0, 120).Select(_ => NextLine()));
        var pane = Panes.Create(LogRegion.Name, Log, x: 0, width: Dim.Fill());
        Body.Add(pane);
        Panes.Track(shell, LogRegion, pane, Log);
        shell.FindIn(LogRegion, new EditorFindTarget(Log));
        Initialized += (_, _) =>
        {
            shell.Focus.Focus(LogRegion);
            Log.CaretOffset = Log.Document!.GetLineByNumber(Log.Document.LineCount).Offset;
            _timer = App?.AddTimeout(_every, () =>
            {
                Append(NextLine());
                return true;
            });
        };
    }

    public AppShell Shell { get; }

    public Terminal.Gui.Editor.Editor Log { get; }

    // Follows the tail while the caret is on the last line, unless the find bar is holding a match in place.
    public void Append(string line)
    {
        var document = Log.Document!;
        var follow = !Shell.FindBar.HoldsPlace && !Log.HasSelection
            && document.GetLineByOffset(Log.CaretOffset).LineNumber == document.LineCount;
        document.Insert(document.TextLength, "\n" + line);
        if (follow)
            Log.CaretOffset = document.GetLineByNumber(document.LineCount).Offset;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _timer is not null)
            App?.RemoveTimeout(_timer);
        base.Dispose(disposing);
    }

    private string NextLine()
    {
        var at = Start.AddSeconds(_written * 7 % 3 + _written * 4);
        return $"{at:HH:mm:ss}  {Messages[_written++ % Messages.Length]}";
    }

    private static AppShell Register(AppShell shell)
    {
        shell.Commands.Register(Back, "Back to Swatch", () => shell.App.RequestStop());
        shell.Keys.Bind("Esc", Back);
        shell.UseMenu(
            new MenuSpec("_Edit", [new MenuEntry(ShellCommands.Find, "_Find")]),
            new MenuSpec("_Help", [
                new MenuEntry(ShellCommands.ShowKeys, "Show _keys"),
                new MenuEntry(ShellCommands.ShowCommands, "Show all _commands"),
                new MenuEntry(Back, "_Back to Swatch"),
            ]));
        return shell;
    }
}
