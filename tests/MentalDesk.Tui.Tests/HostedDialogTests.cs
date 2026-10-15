using System.Drawing;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class HostedDialogTests : StaticConfigurationTest
{
    private static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, CtrlEnter.Key);
    private static readonly ConfirmAction DontSave = new("Don't save", ButtonKind.Danger, Key.D);

    private readonly Workbench _host = new();
    private readonly ConfirmDialog _dialog = new("Unsaved changes", ["Save changes to 'Program.cs' before closing?"], [Save, DontSave]);
    private readonly List<string> _log = [];

    public override void Dispose()
    {
        _dialog.Dispose();
        _host.Dispose();
        base.Dispose();
    }

    private void Show() => _host.Show(_dialog, () => _log.Add(_dialog.SuperView is null ? $"ran {_dialog.Chosen?.Label ?? "nothing"}" : "ran while shown"));

    private bool Shown() => _dialog.SuperView is not null && _dialog.CancelButton.HasFocus;

    private bool StillRunning(ref int ticks) => ++ticks > 5 && _host.Window.IsRunning;

    [Theory]
    [InlineData("D", "Don't save", true)]
    [InlineData("Ctrl+Enter", "Save", true)]
    [InlineData("Esc", null, false)]
    public void A_key_chooses_closes_once_and_runs_the_action_after_the_dialog_has_gone(string key, string? chosen, bool confirmed)
    {
        var ticks = 0;
        _host.Run(
            Show,
            Shown,
            () => _host.Press(Key.TryParse(key, out var parsed) ? parsed : throw new ArgumentException(key)),
            () => StillRunning(ref ticks));

        Assert.Equal(chosen, _dialog.Chosen?.Label);
        Assert.Equal(confirmed, _dialog.Confirmed);
        Assert.Equal([$"ran {chosen ?? "nothing"}"], _log);
        Assert.Same(_host.AppKeys, _host.Scopes.Top);
    }

    [Theory]
    [InlineData("Don't save", "Don't save", true)]
    [InlineData("Esc Cancel", null, false)]
    public void A_button_closes_it_once(string button, string? chosen, bool confirmed)
    {
        var ticks = 0;
        _host.Run(
            Show,
            Shown,
            () => { _dialog.ButtonRow.Single(b => b.Text.Replace("_", "").Trim() == button).InvokeCommand(Command.Accept); },
            () => StillRunning(ref ticks));

        Assert.Equal(chosen, _dialog.Chosen?.Label);
        Assert.Equal(confirmed, _dialog.Confirmed);
        Assert.Equal([$"ran {chosen ?? "nothing"}"], _log);
    }

    [Fact]
    public void Quit_cancels_it_without_stopping_the_app()
    {
        var ticks = 0;
        _host.Run(
            Show,
            Shown,
            () => { _dialog.InvokeCommand(Command.Quit); },
            () => StillRunning(ref ticks));

        Assert.False(_dialog.Confirmed);
        Assert.Equal(["ran nothing"], _log);
    }

    [Fact]
    public void Enter_on_a_focused_button_does_nothing()
    {
        var ticks = 0;
        _host.Run(
            Show,
            Shown,
            () => _host.Press(Key.CursorLeft),
            () => _dialog.ButtonFor(DontSave).HasFocus,
            () => _host.Press(Key.Enter),
            () => ++ticks > 5,
            () => _dialog.SuperView is not null && _log.Count == 0,
            () => _host.Press(Key.Esc),
            () => _log.Count == 1);

        Assert.Null(_dialog.Chosen);
    }

    [Fact]
    public void It_looks_as_it_does_under_AppShell()
    {
        string[] rows = [];
        _host.Run(
            Show,
            Shown,
            () => { rows = _host.Screen(_dialog); },
            () => _host.Press(Key.Esc));

        Assert.Equal("Save changes to 'Program.cs' before closing?", rows[0].Trim());
        Assert.Equal("Ctrl+Enter Save     Don't save     Esc Cancel", rows[2].Trim());
        Assert.IsType<Label>(_dialog.SubViews.First(view => view.Text == _dialog.Lines[0]));
        Assert.Equal([SchemeNames.ButtonPrimary, SchemeNames.ButtonDanger, SchemeNames.ButtonSecondary], _dialog.ButtonRow.Select(button => button.SchemeName));
    }

    [Fact]
    public void F1_asks_the_host_to_show_the_dialogs_keys()
    {
        KeySheet? sheet = null;
        _dialog.KeysRequested += (_, keys) => sheet = keys;

        Assert.Equal(KeyResult.Consumed, _dialog.Scope.Handle(Key.F1));
        Assert.Equal("Unsaved changes", sheet!.Here!.Heading);
        Assert.Contains(sheet.Here.Groups.Single().Rows, row => row is { Keys: "D", Label: "Don't save" });
    }

    [Fact]
    public void Unhandled_F1_is_left_to_the_host()
    {
        Assert.Equal(KeyResult.Pass, _dialog.Scope.Handle(Key.F1));
    }

    // An app that isn't built on AppShell: its own scope stack, and it shows a dialog by adding it, not running it.
    private sealed class Workbench : IDisposable
    {
        private const int MaxIterations = 1000;

        public Workbench()
        {
            App = Application.Create();
            App.Init(driverName: DriverRegistry.Names.ANSI);
            App.Keyboard.KeyDown += OnKeyDown;
            Scopes.Push(AppKeys);
        }

        public IApplication App { get; }

        public InputScopes Scopes { get; } = new();

        public IInputScope AppKeys { get; } = new PassScope();

        public Window Window { get; } = new();

        public void Show(AppDialog dialog, Action then)
        {
            dialog.Closed += Closed;
            Window.Add(dialog);
            Scopes.Push(dialog.Scope);
            dialog.SetFocus();

            void Closed(object? sender, EventArgs e)
            {
                dialog.Closed -= Closed;
                Scopes.Pop(dialog.Scope);
                Window.Remove(dialog);
                then();
            }
        }

        public void Press(Key key) => App.InjectKey(key);

        public string[] Screen(View view)
        {
            var origin = view.ViewportToScreen(Point.Empty);
            var contents = App.Driver!.Contents!;
            return [.. Enumerable.Range(0, view.Viewport.Height).Select(row =>
                string.Concat(Enumerable.Range(origin.X, view.Viewport.Width).Select(col => contents[origin.Y + row, col].Grapheme)))];
        }

        public void Run(params Delegate[] steps)
        {
            var queue = new Queue<Delegate>(steps);
            var iterations = 0;
            App.Iteration += Step;
            App.Run(Window);
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
                    case Action act:
                        queue.Dequeue();
                        act();
                        break;
                }
            }
        }

        public void Dispose()
        {
            App.Keyboard.KeyDown -= OnKeyDown;
            Window.Dispose();
            App.Dispose();
        }

        private void OnKeyDown(object? sender, Key key)
        {
            if (!key.Handled && Scopes.Handle(CtrlEnter.FromWindows(key, OperatingSystem.IsWindows())) != KeyResult.Pass)
                key.Handled = true;
        }

        private sealed class PassScope : IInputScope
        {
            public KeyResult Handle(Key key) => KeyResult.Pass;
        }
    }
}
