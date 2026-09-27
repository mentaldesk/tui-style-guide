namespace MentalDesk.Tui.Keys;

public sealed class InputScopes
{
    private readonly Stack<IInputScope> _stack = new();
    private string? _pendingChord;

    public event EventHandler<string?>? ChordChanged;

    public IInputScope? Top => _stack.TryPeek(out var top) ? top : null;

    public string? PendingChord => _pendingChord;

    public void Push(IInputScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _stack.Push(scope);
        SyncChord();
    }

    public void Pop(IInputScope scope)
    {
        if (!ReferenceEquals(Top, scope))
            throw new InvalidOperationException("Input scopes must be popped in the reverse order they were pushed.");
        _stack.Pop();
        SyncChord();
    }

    public KeyResult Handle(Key key)
    {
        var result = Top?.Handle(key) ?? KeyResult.Pass;
        SyncChord();
        return result;
    }

    private void SyncChord()
    {
        var chord = Top?.PendingChord;
        if (chord == _pendingChord) return;
        _pendingChord = chord;
        ChordChanged?.Invoke(this, chord);
    }
}
