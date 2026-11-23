using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Working;

// Takes no room while there's nothing to say.
public sealed class WorkLine : View
{
    private readonly SpinnerView _spinner = new()
    {
        X = 0, Y = 0, Style = new SpinnerStyle.Dots(), SyncWithTerminal = true, Visible = false, CanFocus = false,
    };
    private readonly Label _label = new() { X = 0, Y = 0, Width = Dim.Fill() };
    private bool _error;

    public WorkLine()
    {
        X = 0;
        Y = Pos.AnchorEnd();
        Width = Dim.Fill();
        Height = 1;
        CanFocus = false;
        Visible = false;
        _label.GettingAttributeForRole += (_, e) =>
        {
            if (_error || e.Role != VisualRole.Normal) return;
            e.Result = _label.GetAttributeForRole(VisualRole.ReadOnly);
            e.Handled = true;
        };
        Add(_spinner, _label);
    }

    public TimeProvider Clock { get; set; } = TimeProvider.System;

    public Work? Current { get; private set; }

    public bool Spinning => _spinner.Visible;

    public string Says => Visible ? _label.Text : string.Empty;

    public Work Start(string text, (string Text, Action Run)? stop = null)
    {
        if (Current is { Running: true } running)
        {
            running.Changed -= OnChanged;
            running.Done();
        }
        var work = new Work(text, stop, null);
        work.Changed += OnChanged;
        Current = work;
        Show(string.Empty, spinning: false, error: false);
        work.ShowAfterMoment(Clock, Post);
        return work;
    }

    public bool Stop() => Current?.Stop() == true;

    private void OnChanged(object? sender, EventArgs e)
    {
        var work = (Work)sender!;
        if (work.Running)
            Show(work.Showing ? work.Line : string.Empty, spinning: work.Showing, error: false);
        else
        {
            work.Changed -= OnChanged;
            Show(work.Line, spinning: false, error: work.State == WorkState.Failed);
        }
    }

    private void Show(string text, bool spinning, bool error)
    {
        _error = error;
        _label.Text = text;
        _label.SchemeName = error ? SchemeNames.Error : null;
        _label.X = spinning ? Pos.Right(_spinner) + 1 : 0;
        _spinner.Visible = spinning;
        if (App is not null && _spinner.AutoSpin != spinning) _spinner.AutoSpin = spinning;
        Visible = text.Length > 0;
        SuperView?.SetNeedsLayout();
        SetNeedsLayout();
        SetNeedsDraw();
    }

    private void Post(Action action)
    {
        if (App is { } app)
            app.Invoke(action);
        else
            action();
    }
}
