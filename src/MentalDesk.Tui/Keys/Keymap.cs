using MentalDesk.Tui.Commands;

namespace MentalDesk.Tui.Keys;

public sealed class Keymap(CommandRegistry commands) : IInputScope
{
    private readonly Dictionary<CommandScope, Node> _roots = [];
    private readonly List<Key> _typed = [];
    private Node? _current;

    public Func<CommandScope> FocusedScope { get; set; } = () => CommandScope.Global;

    public string? PendingChord { get; private set; }

    public IEnumerable<KeyBinding> Bindings =>
        _roots.SelectMany(root => Walk(root.Key, root.Value, []));

    public IEnumerable<KeyBinding> For(string commandId) =>
        Bindings.Where(binding => binding.CommandId == commandId).OrderBy(binding => binding.Chord.Count);

    public Keymap Bind(string sequence, string commandId) => Bind(KeyChord.Parse(sequence), commandId);

    public Keymap Bind(IReadOnlyList<Key> chord, string commandId)
    {
        if (chord.Count == 0) throw new ArgumentException("A chord needs at least one key.", nameof(chord));
        ArgumentException.ThrowIfNullOrEmpty(commandId);
        var node = Root(commands.ScopeOf(commandId));
        foreach (var key in chord.Select(Normalize))
        {
            if (!node.Children.TryGetValue(key, out var child))
                node.Children[key] = child = new Node();
            node = child;
        }
        node.CommandId = commandId;
        return this;
    }

    public bool Unbind(IReadOnlyList<Key> chord, CommandScope scope)
    {
        if (!_roots.TryGetValue(scope, out var node)) return false;
        var path = new List<(Node Parent, Key Key, Node Child)>();
        foreach (var key in chord.Select(Normalize))
        {
            if (!node.Children.TryGetValue(key, out var child)) return false;
            path.Add((node, key, child));
            node = child;
        }
        if (node.CommandId is null) return false;
        node.CommandId = null;
        for (var i = path.Count - 1; i >= 0; i--)
        {
            var (parent, key, child) = path[i];
            if (child.CommandId is not null || child.Children.Count > 0) break;
            parent.Children.Remove(key);
        }
        return true;
    }

    public KeyConflict? CheckConflict(IReadOnlyList<Key> chord, CommandScope scope)
    {
        if (!_roots.TryGetValue(scope, out var node)) return null;
        for (var i = 0; i < chord.Count; i++)
        {
            if (!node.Children.TryGetValue(Normalize(chord[i]), out var child)) return null;
            if (child.CommandId is not null && i < chord.Count - 1) return KeyConflict.ExtensionOfExisting;
            node = child;
        }
        return node.Children.Count > 0 ? KeyConflict.PrefixOfExisting
            : node.CommandId is not null ? KeyConflict.ExactMatch
            : null;
    }

    public KeyResult Handle(Key key)
    {
        var normalized = Normalize(key);
        if (_current is not null)
            return Continue(normalized);
        var focused = FocusedScope();
        if (focused != CommandScope.Global && Start(focused, normalized) is { } result)
            return result;
        return Start(CommandScope.Global, normalized) ?? KeyResult.Pass;
    }

    private KeyResult? Start(CommandScope scope, Key key)
    {
        if (!_roots.TryGetValue(scope, out var root) || !root.Children.TryGetValue(key, out var next))
            return null;
        if (next.CommandId is null)
            return Descend(key, next);
        return commands.Execute(next.CommandId) ? KeyResult.Consumed : null;
    }

    private KeyResult Continue(Key key)
    {
        if (key == Key.Esc || !_current!.Children.TryGetValue(key, out var next))
        {
            Reset();
            return KeyResult.Consumed;
        }
        if (next.CommandId is null)
            return Descend(key, next);
        Reset();
        commands.Execute(next.CommandId);
        return KeyResult.Consumed;
    }

    private KeyResult Descend(Key key, Node next)
    {
        _typed.Add(key);
        _current = next;
        PendingChord = KeyChord.Display(_typed);
        return KeyResult.ChordInProgress;
    }

    private void Reset()
    {
        _current = null;
        _typed.Clear();
        PendingChord = null;
    }

    private Node Root(CommandScope scope)
    {
        if (!_roots.TryGetValue(scope, out var root))
            _roots[scope] = root = new Node();
        return root;
    }

    // Terminal.Gui reports Shift+x as a different key from x; a chord step written "X" should match either.
    private static Key Normalize(Key key) =>
        key is { IsShift: true, IsCtrl: false, IsAlt: false } && char.IsLetter((char)key.AsRune.Value)
            ? new Key(char.ToLowerInvariant((char)key.AsRune.Value))
            : key;

    private static IEnumerable<KeyBinding> Walk(CommandScope scope, Node node, List<Key> path)
    {
        if (node.CommandId is not null)
            yield return new KeyBinding([.. path], node.CommandId, scope);
        foreach (var (key, child) in node.Children)
        {
            path.Add(key);
            foreach (var binding in Walk(scope, child, path))
                yield return binding;
            path.RemoveAt(path.Count - 1);
        }
    }

    private sealed class Node
    {
        public string? CommandId;
        public Dictionary<Key, Node> Children { get; } = [];
    }
}
