using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Finding;
using MentalDesk.Tui.Keys;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

// a-team's dashboard has its own key stack and no AppShell.
public class HostedFindBarTests : StaticConfigurationTest
{
    private const int MaxIterations = 1000;

    private readonly IApplication _app = Application.Create();
    private readonly InputScopes _scopes = new();
    private readonly CommandRegistry _commands = new();
    private readonly Keymap _keys;
    private readonly Window _window = new();
    private readonly Terminal.Gui.Editor.Editor _editor = new() { Width = Dim.Fill(), Height = Dim.Fill(), Text = "one retry\ntwo\nthree retry" };
    private readonly FindBar _bar = new();
    private readonly EditorFindTarget _target;
    private readonly List<KeyColumn> _requested = [];
    private IInputScope? _barScope;

    public HostedFindBarTests()
    {
        _app.Init(driverName: DriverRegistry.Names.ANSI);
        _app.Keyboard.KeyDown += OnKeyDown;
        var pane = new FrameView { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        pane.Add(_editor);
        _window.Add(pane);
        _target = new EditorFindTarget(_editor);
        _keys = new Keymap(_commands);
        _commands.Register("find", "Find", Find);
        _keys.Bind("Ctrl+F", "find");
        _scopes.Push(_keys);
        _bar.KeysRequested += (_, column) => _requested.Add(column);
        _bar.Closed += (_, _) =>
        {
            _scopes.Pop(_barScope!);
            _barScope = null;
        };
    }

    public override void Dispose()
    {
        _app.Keyboard.KeyDown -= OnKeyDown;
        if (!_bar.IsOpen)
            _bar.Dispose();
        _window.Dispose();
        _app.Dispose();
        base.Dispose();
    }

    private void Find()
    {
        if (_barScope is null)
            _scopes.Push(_barScope = _bar.ScopeOver(_keys));
        _bar.Open(_target);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        if (!key.Handled && _scopes.Handle(key) != KeyResult.Pass)
            key.Handled = true;
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _app.InjectKey(key);
    }

    [Fact]
    public void A_host_with_its_own_key_stack_binds_Ctrl_F_pushes_the_bar_s_scope_and_gets_F1()
    {
        IInputScope? top = null;

        Run(
            () => { _editor.SetFocus(); },
            () => _editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => _bar.QueryField.HasFocus,
            () =>
            {
                top = _scopes.Top;
                Press([.. "retry".Select(c => new Key(c))]);
            },
            () => _bar.Status == "1 of 2",
            () => Press(Key.Enter),
            () => _bar.Status == "2 of 2",
            () => Press(Key.F1),
            () => _requested.Count == 1,
            () => Press(Key.Esc),
            () => !_bar.IsOpen && _editor.HasFocus);

        Assert.NotSame(_keys, top);
        Assert.Same(_keys, _scopes.Top);
        Assert.Equal(FindBar.Heading, _requested.Single().Heading);
        Assert.Equal(["Enter", "Shift+Enter", "Esc"], _requested.Single().Groups.Single().Rows.Select(row => row.Keys));
    }

    private void Run(params Delegate[] steps)
    {
        var queue = new Queue<Delegate>(steps);
        var iterations = 0;
        _app.Iteration += Step;
        _app.Run(_window);
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
