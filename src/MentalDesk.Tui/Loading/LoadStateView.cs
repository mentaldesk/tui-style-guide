using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Loading;

public enum LoadState
{
    Loading,

    Empty,

    Failed,

    Loaded,
}

// The content stays focused in every state: the state is drawn over it, so Tab order never changes.
public sealed class LoadStateView : View
{
    private readonly View _overlay = new() { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = false };
    private readonly View _middle = new()
    {
        X = Pos.Center(), Y = Pos.Center(), Width = Dim.Auto(), Height = Dim.Auto(), CanFocus = false,
    };
    private readonly SpinnerView _spinner = Spinner();
    private readonly SpinnerView _reloadSpinner = Spinner();
    private readonly Label _message = new() { X = Pos.Center(), Y = 0 };
    private readonly string _loading;
    private View? _next;
    private SpinnerView? _waiting;
    private object? _delay;
    private bool _dimmed;

    public LoadStateView(View content, string loading)
    {
        Content = content;
        _loading = loading;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        _spinner.Y = 0;
        _message.GettingAttributeForRole += (_, e) =>
        {
            if (!_dimmed || e.Role != VisualRole.Normal) return;
            e.Result = _message.GetAttributeForRole(VisualRole.ReadOnly);
            e.Handled = true;
        };
        _middle.Add(_spinner, _message);
        _overlay.Add(_middle);
        Add(content, _overlay);
        ShowLoading();
    }

    public View Content { get; }

    public LoadState State { get; private set; }

    public bool Reloading { get; private set; }

    public string Says => _overlay.Visible && _middle.Visible ? _message.Text : string.Empty;

    public TimeSpan SpinnerDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    internal Label Message => _message;

    internal View? NextStep => _next;

    internal SpinnerView CentreSpinner => _spinner;

    internal SpinnerView ReloadSpinner => _reloadSpinner;

    public event EventHandler<string>? ReloadFailed;

    public void ShowLoading()
    {
        StopSpinning();
        if (State == LoadState.Loaded)
        {
            Reloading = true;
            WaitThenSpin(_reloadSpinner);
            return;
        }
        State = LoadState.Loading;
        Show(_loading, dimmed: false, error: false, next: null);
        _message.X = Pos.Right(_spinner) + 1;
        _middle.Visible = false;
        WaitThenSpin(_spinner);
    }

    public void ShowContent()
    {
        StopSpinning();
        State = LoadState.Loaded;
        _overlay.Visible = false;
        SetNeedsDraw();
    }

    public void ShowEmpty(string message, (string Text, Action Run)? next = null)
    {
        StopSpinning();
        State = LoadState.Empty;
        Show(message, dimmed: true, error: false, next);
    }

    public void ShowFailed(string message, (string Text, Action Run) retry)
    {
        var reloading = Reloading;
        StopSpinning();
        if (reloading)
        {
            ReloadFailed?.Invoke(this, message);
            return;
        }
        State = LoadState.Failed;
        Show(message, dimmed: false, error: true, retry);
    }

    public override void EndInit()
    {
        base.EndInit();
        if (SuperView?.Border is { Thickness.Top: > 0 } border && border.GetOrCreateView() is { } frame)
        {
            _reloadSpinner.X = Pos.AnchorEnd() - 2;
            frame.Add(_reloadSpinner);
        }
        else
        {
            _reloadSpinner.X = Pos.AnchorEnd();
            Add(_reloadSpinner);
        }
        if (_waiting is { } spinner && _delay is null)
            WaitThenSpin(spinner);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopSpinning();
            _reloadSpinner.SuperView?.Remove(_reloadSpinner);
            _reloadSpinner.Dispose();
        }
        base.Dispose(disposing);
    }

    private static SpinnerView Spinner() => new()
    {
        Style = new SpinnerStyle.Dots(), SyncWithTerminal = true, Visible = false, CanFocus = false,
    };

    private void Show(string message, bool dimmed, bool error, (string Text, Action Run)? next)
    {
        _dimmed = dimmed;
        _message.Text = message;
        _message.X = Pos.Center();
        _message.SchemeName = error ? SchemeNames.Error : null;
        if (_next is not null)
        {
            _middle.Remove(_next);
            _next.Dispose();
            _next = null;
        }
        if (next is var (text, run))
        {
            _next = HintRow.Button(text, run);
            _next.X = Pos.Center();
            _next.Y = 1;
            _middle.Add(_next);
        }
        _overlay.Visible = true;
        SetNeedsLayout();
        SetNeedsDraw();
    }

    // A load shorter than the delay shows nothing, so it doesn't flicker.
    private void WaitThenSpin(SpinnerView spinner)
    {
        _waiting = spinner;
        if (App is { Initialized: true } app)
            _delay = app.AddTimeout(SpinnerDelay, Spin);
    }

    private bool Spin()
    {
        _delay = null;
        if (_waiting is { } spinner)
        {
            spinner.Visible = true;
            spinner.AutoSpin = true;
            _middle.Visible = true;
            spinner.SuperView?.SetNeedsLayout();
        }
        _waiting = null;
        return false;
    }

    private void StopSpinning()
    {
        if (_delay is not null)
            App?.RemoveTimeout(_delay);
        _delay = null;
        _waiting = null;
        _middle.Visible = true;
        Reloading = false;
        foreach (var spinner in new[] { _spinner, _reloadSpinner })
        {
            if (spinner.AutoSpin) spinner.AutoSpin = false;
            spinner.Visible = false;
        }
        SetNeedsDraw();
    }
}
