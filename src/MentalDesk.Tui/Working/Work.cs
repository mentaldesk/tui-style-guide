using MentalDesk.Tui.Chrome;

namespace MentalDesk.Tui.Working;

public enum WorkState
{
    Running,

    Done,

    Failed,

    Stopped,
}

public sealed class Work
{
    public static readonly TimeSpan Moment = TimeSpan.FromMilliseconds(250);

    private readonly (string Text, Action Run)? _stop;
    private ITimer? _delay;

    internal Work(string text, (string Text, Action Run)? stop, string? already)
    {
        Text = text;
        _stop = stop;
        Already = already;
    }

    public event EventHandler? Changed;

    public WorkState State { get; private set; }

    public bool Running => State == WorkState.Running;

    // False until it has taken longer than a moment, so fast work doesn't flicker.
    public bool Showing { get; private set; }

    public string Text { get; private set; }

    public string? Outcome { get; private set; }

    public string? StopHint => _stop?.Text;

    public string? Already { get; }

    public string Line => Running
        ? StopHint is { } stop ? $"{Text}{HintRow.Separator}{stop}" : Text
        : Outcome ?? string.Empty;

    public void Report(string text)
    {
        if (!Running) return;
        Text = text;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // The app's stop calls Stopped, saying what was done.
    public bool Stop()
    {
        if (!Running || _stop is not { } stop) return false;
        stop.Run();
        return true;
    }

    public void Done(string? reply = null) => Finish(WorkState.Done, reply);

    public void Failed(string error) => Finish(WorkState.Failed, error);

    public void Stopped(string text) => Finish(WorkState.Stopped, text);

    internal void ShowAfterMoment(TimeProvider clock, Action<Action> post) =>
        _delay = clock.CreateTimer(_ => post(Show), null, Moment, System.Threading.Timeout.InfiniteTimeSpan);

    private void Show()
    {
        if (!Running || Showing) return;
        Showing = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Finish(WorkState state, string? outcome)
    {
        if (!Running) return;
        _delay?.Dispose();
        _delay = null;
        State = state;
        Outcome = outcome;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
