using MentalDesk.Tui.Theming;
using MentalDesk.Tui.Working;

namespace MentalDesk.Tui.Chrome;

// Not Terminal.Gui's StatusBar: that draws a border between items and paints in the Menu scheme.
public sealed class AppStatusBar : View
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(5);

    private readonly Label _focus = new() { X = 1, Y = 0 };
    private readonly Label _state = new() { X = 0, Y = 0 };
    private readonly Button _unread;
    private readonly Label _unreadGap = new() { Y = 0, Text = HintRow.Separator, Visible = false };
    private readonly HintRow _hints;
    private readonly View _right = new() { X = Pos.AnchorEnd() - 1, Y = 0, Width = Dim.Auto(), Height = 1, CanFocus = false };
    private readonly List<StatusMessage> _history = [];
    private readonly List<Work> _work = [];
    private readonly Dictionary<Work, StatusMessage?> _covering = [];
    private readonly SpinnerView _spinner = new()
    {
        Y = 0, Style = new SpinnerStyle.Dots(), SyncWithTerminal = true, Visible = false, CanFocus = false,
    };
    private string _stateText = string.Empty;
    private string _restingText = string.Empty;
    private readonly Label _message;
    private string? _chord;
    private StatusMessage? _shown;
    private ITimer? _timer;

    public AppStatusBar()
    {
        Y = Pos.AnchorEnd(1);
        Width = Dim.Fill();
        Height = 1;
        CanFocus = false;
        SchemeName = SchemeNames.StatusBar;
        _unread = HintRow.Button(string.Empty, () => MessagesRequested?.Invoke(this, EventArgs.Empty));
        _unread.SchemeName = SchemeNames.Error;
        _unread.Visible = false;
        _unreadGap.X = Pos.Right(_unread);
        _hints = new HintRow { X = Pos.Right(_state), Width = Dim.Auto() };
        _right.Add(_unread, _unreadGap, _state, _hints);
        _spinner.X = Pos.Right(_focus);
        _message = new Label { X = Pos.Right(_focus), Width = Dim.Fill(2, _right), Visible = false };
        Add(_focus, _spinner, _message, _right);
    }

    // Clicking ⚠ n.
    public event EventHandler? MessagesRequested;

    public event EventHandler? UnreadErrorsChanged;

    public TimeProvider Clock { get; set; } = TimeProvider.System;

    public string FocusWord { get; private set; } = string.Empty;

    public StatusMessage? Current => _shown;

    public string? Message => _shown?.Text;

    public Severity? MessageSeverity => _shown?.Severity;

    public IReadOnlyList<StatusMessage> History => _history;

    public Work? Working => _work.LastOrDefault(work => work.Showing);

    public bool Spinning => _spinner.Visible;

    public int UnreadErrors { get; private set; }

    public Button UnreadButton => _unread;

    public string Says => _message.Visible ? _message.Text : _hints.Says;

    public string State
    {
        get => _stateText;
        set
        {
            _stateText = value;
            _state.Text = value.Length == 0 ? string.Empty : $"{value}{HintRow.Separator}";
        }
    }

    public HintRow Hints => _hints;

    // Shown in the message's place while there's no message or chord, like the active file's path.
    public string RestingText
    {
        get => _restingText;
        set
        {
            _restingText = value;
            Refresh();
        }
    }

    public void SetFocusWord(string word)
    {
        FocusWord = word;
        ShowFocusWord();
    }

    private void ShowFocusWord() =>
        _focus.Text = FocusWord.Length > 0 && _message.Visible ? $"{FocusWord}{HintRow.Separator}" : FocusWord;

    public StatusMessage ShowMessage(string text, Severity severity = Severity.Info) => Show(text, severity, MessageKind.Reply);

    public StatusMessage ShowNews(string text, Severity severity = Severity.Info) => Show(text, severity, MessageKind.News);

    // Null, saying `already`, while work started with the same `already` still runs.
    public Work? ShowWork(string text, (string Text, Action Run)? stop = null, string? already = null)
    {
        if (already is not null && _work.Exists(work => work.Already == already))
        {
            ShowMessage(already);
            return null;
        }
        var work = new Work(text, stop, already);
        work.Changed += OnWorkChanged;
        _work.Add(work);
        _covering[work] = _shown;
        work.ShowAfterMoment(Clock, Post);
        return work;
    }

    // For what the app logs rather than shows: it goes in Messages, and an Error counts in ⚠ n.
    public StatusMessage Record(string text, Severity severity = Severity.Info)
    {
        var message = new StatusMessage(text, severity, MessageKind.News, Clock.GetLocalNow());
        Add(message);
        return message;
    }

    public void Withdraw(StatusMessage message)
    {
        if (_shown == message)
            ClearMessage();
    }

    public void ClearMessage()
    {
        if (_shown is null) return;
        StopTimer();
        _shown = null;
        Refresh();
    }

    // Call before handling each key; AppShell does, skipping the keys of a chord.
    public void KeyPressed()
    {
        if (_shown is { } shown && (shown.Kind == MessageKind.Reply || Clock.GetUtcNow() - shown.At >= Lifetime))
            ClearMessage();
    }

    public void MarkRead()
    {
        UnreadErrors = 0;
        ShowUnread();
    }

    public void SetChord(string? chord)
    {
        _chord = chord;
        Refresh();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            StopTimer();
        base.Dispose(disposing);
    }

    private StatusMessage Show(string text, Severity severity, MessageKind kind)
    {
        var message = new StatusMessage(text, severity, kind, Clock.GetLocalNow());
        Add(message);
        StopTimer();
        _shown = message;
        if (severity == Severity.Info)
            _timer = Clock.CreateTimer(_ => Expire(message), null, Lifetime, System.Threading.Timeout.InfiniteTimeSpan);
        Refresh();
        return message;
    }

    private void Add(StatusMessage message)
    {
        _history.Add(message);
        if (!message.CountsAsUnread) return;
        UnreadErrors++;
        ShowUnread();
    }

    private void Expire(StatusMessage message) => Post(() => Withdraw(message));

    private void Post(Action action)
    {
        if (App is { } app)
            app.Invoke(action);
        else
            action();
    }

    private void OnWorkChanged(object? sender, EventArgs e)
    {
        var work = (Work)sender!;
        if (work.Running)
        {
            if (work.Showing && _covering.Remove(work, out var older) && older is not null && _shown == older)
                ClearMessage();
            Refresh();
            return;
        }
        work.Changed -= OnWorkChanged;
        _work.Remove(work);
        _covering.Remove(work);
        switch (work.State)
        {
            case WorkState.Failed:
                ShowNews(work.Outcome!, Severity.Error);
                break;
            case WorkState.Done or WorkState.Stopped when work.Outcome is { } outcome:
                ShowMessage(outcome);
                break;
            default:
                Refresh();
                break;
        }
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Spin(bool on)
    {
        if (_spinner.Visible == on) return;
        _spinner.Visible = on;
        if (App is not null) _spinner.AutoSpin = on;
        _message.X = on ? Pos.Right(_spinner) + 1 : Pos.Right(_focus);
        SetNeedsLayout();
    }

    private void ShowUnread()
    {
        var any = UnreadErrors > 0;
        _unread.Text = any ? $"⚠ {UnreadErrors}" : string.Empty;
        _unread.Visible = any;
        _unreadGap.Visible = any;
        _state.X = any ? Pos.Right(_unreadGap) : 0;
        _right.SetNeedsLayout();
        SetNeedsDraw();
        UnreadErrorsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        var working = _chord is null && _shown is null ? Working : null;
        if (_chord is not null)
        {
            _message.Text = $"{_chord}…";
            _message.SchemeName = null;
        }
        else if (_shown is { } shown)
        {
            _message.Text = shown.Text;
            _message.SchemeName = shown.Severity == Severity.Error ? SchemeNames.Error : null;
        }
        else if (working is not null)
        {
            _message.Text = working.Line;
            _message.SchemeName = null;
        }
        else
        {
            _message.Text = _restingText;
            _message.SchemeName = null;
        }
        Spin(working is not null);
        _message.Visible = _chord is not null || _shown is not null || working is not null || _restingText.Length > 0;
        ShowFocusWord();
        SetNeedsDraw();
    }
}
