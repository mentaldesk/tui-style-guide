using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

internal sealed class Host : IDisposable
{
    private const int MaxIterations = 1000;

    // Tests never reach the machine's real clipboard or settings.
    public Host(IClipboard? platform = null, FontDetection? detected = null)
    {
        SettingsPath = Path.Combine(Path.GetTempPath(), $"mentaldesk-{Guid.NewGuid():N}", "settings.json");
        var icons = new IconSettings(new MentalDeskSettings(SettingsPath), () => detected ?? new FontDetection(false, "a test terminal"));
        App = Application.Create();
        App.Init(driverName: DriverRegistry.Names.ANSI);
        App.Driver!.Clipboard = platform ?? new FakeClipboard();
        Shell = new AppShell(App, new TerminalCursor(Written.Add), ClipboardProgram.Missing, icons);
    }

    public IApplication App { get; }

    public AppShell Shell { get; }

    public List<string> Written { get; } = [];

    public string SettingsPath { get; }

    // Each step gets its own iteration, so injected keys are processed in between. A Func<bool> step is polled until true.
    public void Run(IRunnable window, params Delegate[] steps)
    {
        var queue = new Queue<Delegate>(steps);
        var iterations = 0;
        App.Iteration += Step;
        Shell.Run(window);
        App.Iteration -= Step;
        Assert.True(queue.Count == 0, $"{queue.Count} step(s) never completed");

        void Step(object? sender, EventArgs e)
        {
            if (queue.Count == 0 || ++iterations > MaxIterations)
            {
                App.RequestStop();
                return;
            }
            switch (queue.Peek())
            {
                case Func<bool> poll:
                    if (poll()) queue.Dequeue();
                    break;
                // Dequeued first: a key that opens a dialog runs its nested loop, and this step, before returning.
                case Action act:
                    queue.Dequeue();
                    act();
                    break;
                default:
                    throw new InvalidOperationException("A step is an Action or a Func<bool>.");
            }
        }
    }

    public void Dispose()
    {
        Shell.Dispose();
        App.Dispose();
        if (Directory.Exists(Path.GetDirectoryName(SettingsPath)))
            Directory.Delete(Path.GetDirectoryName(SettingsPath)!, recursive: true);
    }
}
