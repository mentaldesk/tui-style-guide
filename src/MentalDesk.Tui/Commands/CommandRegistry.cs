namespace MentalDesk.Tui.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public CommandRegistry Register(
        string id, string label, Action run, CommandScope? scope = null, Func<bool>? isEnabled = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(run);
        _entries[id] = new Entry(new AppCommand(id, label, scope ?? CommandScope.Global), run, isEnabled);
        return this;
    }

    public IEnumerable<AppCommand> All => _entries.Values.Select(entry => entry.Command);

    public AppCommand? Find(string id) => _entries.TryGetValue(id, out var entry) ? entry.Command : null;

    public CommandScope ScopeOf(string id) => Find(id)?.Scope ?? CommandScope.Global;

    public bool IsEnabled(string id) =>
        _entries.TryGetValue(id, out var entry) && entry.IsEnabled?.Invoke() != false;

    public bool Execute(string id)
    {
        if (!_entries.TryGetValue(id, out var entry) || entry.IsEnabled?.Invoke() == false)
            return false;
        entry.Run();
        return true;
    }

    private sealed record Entry(AppCommand Command, Action Run, Func<bool>? IsEnabled);
}
