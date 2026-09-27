using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

internal sealed class Host : IDisposable
{
    private const int MaxIterations = 1000;

    public Host()
    {
        App = Application.Create();
        App.Init(driverName: DriverRegistry.Names.ANSI);
        Shell = new AppShell(App, new TerminalCursor(Written.Add));
    }

    public IApplication App { get; }

    public AppShell Shell { get; }

    public List<string> Written { get; } = [];

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
            var done = queue.Peek() switch
            {
                Func<bool> poll => poll(),
                Action act => Do(act),
                _ => throw new InvalidOperationException("A step is an Action or a Func<bool>."),
            };
            if (done) queue.Dequeue();
        }

        static bool Do(Action act)
        {
            act();
            return true;
        }
    }

    public void Dispose()
    {
        Shell.Dispose();
        App.Dispose();
    }
}
