using System.Globalization;
using System.Text.Json.Nodes;
using MentalDesk.Tui.Commands;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Keys;

// The keys a user changed, by command: "Ctrl+A", "" for none, or a list where a key outside the command's home is { "key", "where" }.
public sealed class KeyOverrides
{
    public const string Section = "keys";

    private const string KeyField = "key";
    private const string WhereField = "where";
    private const string KeycodeMark = "#";

    private readonly CommandRegistry _commands;
    private readonly Keymap _keys;
    private readonly Func<JsonObject?> _read;
    private readonly Action<JsonObject> _write;

    public KeyOverrides(CommandRegistry commands, Keymap keys, Func<JsonObject?> read, Action<JsonObject> write)
    {
        _commands = commands;
        _keys = keys;
        _read = read;
        _write = write;
        Defaults = Current;
    }

    public IReadOnlyList<KeyBinding> Defaults { get; }

    public IReadOnlyList<KeyBinding> Current =>
        [.. _keys.Bindings.Where(binding => _commands.Find(binding.CommandId) is not null)];

    // Create it once every default key is bound, so it can tell what the user changed.
    public static KeyOverrides InSection(MentalDeskSettings file, string app, CommandRegistry commands, Keymap keys) =>
        new(commands, keys, () => file.Section(app)[Section] as JsonObject, overrides =>
        {
            var section = file.Section(app);
            section[Section] = overrides;
            file.SaveSection(app, section);
        });

    public void Load()
    {
        if (_read() is { } overrides)
            Apply(Read(overrides, Defaults, _commands));
    }

    public void Keep(IReadOnlyList<KeyBinding> bindings)
    {
        _write(Write(Defaults, bindings, _commands));
        Apply(bindings);
    }

    // An entry that doesn't parse is skipped whole, so that command keeps its default keys.
    public static IReadOnlyList<KeyBinding> Read(JsonObject overrides, IReadOnlyList<KeyBinding> defaults, CommandRegistry commands)
    {
        var bindings = defaults.ToList();
        foreach (var (id, value) in overrides)
        {
            if (commands.Find(id) is not { } command || Entry(value, command) is not { } keys) continue;
            bindings.RemoveAll(binding => binding.CommandId == id);
            bindings.AddRange(keys.Distinct(SameKey.Instance));
        }
        return bindings;
    }

    public static JsonObject Write(IReadOnlyList<KeyBinding> defaults, IReadOnlyList<KeyBinding> bindings, CommandRegistry commands)
    {
        var overrides = new JsonObject();
        foreach (var command in commands.All)
        {
            var mine = bindings.Where(binding => binding.CommandId == command.Id).ToList();
            var before = defaults.Where(binding => binding.CommandId == command.Id).ToList();
            if (mine.ToHashSet(SameKey.Instance).SetEquals(before)) continue;
            overrides[command.Id] = mine switch
            {
                [] => string.Empty,
                [var only] when only.Scope == command.Scope => Name(Keymap.Normalize(only.Chord)),
                _ => new JsonArray([.. mine.Select(binding => binding.Scope == command.Scope
                    ? (JsonNode)Name(Keymap.Normalize(binding.Chord))
                    : new JsonObject { [KeyField] = Name(Keymap.Normalize(binding.Chord)), [WhereField] = binding.Scope.Name })]),
            };
        }
        return overrides;
    }

    public static string Name(IReadOnlyList<Key> chord) => string.Join(' ', chord.Select(Name));

    // A key whose name doesn't parse back to it is written as its keycode.
    public static string Name(Key key) =>
        Key.TryParse(key.ToString(), out var parsed) && parsed == key
            ? key.ToString()
            : KeycodeMark + ((uint)key.KeyCode).ToString(CultureInfo.InvariantCulture);

    public static IReadOnlyList<Key>? Parse(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var chord = new List<Key>();
        foreach (var part in parts)
        {
            if (part.Length > 1 && part.StartsWith(KeycodeMark, StringComparison.Ordinal)
                && uint.TryParse(part.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var code))
                chord.Add(new Key((KeyCode)code));
            else if (Key.TryParse(part, out var key) && key != Key.Empty)
                chord.Add(key);
            else
                return null;
        }
        return chord;
    }

    private static List<KeyBinding>? Entry(JsonNode? value, AppCommand command)
    {
        switch (value)
        {
            case JsonValue text when text.TryGetValue<string>(out var name):
                if (name.Length == 0) return [];
                return Parse(name) is { } chord ? [new KeyBinding(chord, command.Id, command.Scope)] : null;
            case JsonArray list:
                var keys = new List<KeyBinding>();
                foreach (var item in list)
                {
                    if (Binding(item, command) is not { } binding) return null;
                    keys.Add(binding);
                }
                return keys;
            default:
                return null;
        }
    }

    private static KeyBinding? Binding(JsonNode? item, AppCommand command)
    {
        string? name = null;
        var scope = command.Scope;
        if (item is JsonValue text && text.TryGetValue<string>(out var plain))
            name = plain;
        else if (item is JsonObject entry && entry[KeyField] is JsonValue key && key.TryGetValue<string>(out var keyName))
        {
            name = keyName;
            if (entry[WhereField] is JsonValue where && where.TryGetValue<string>(out var whereName) && whereName.Length > 0)
                scope = new CommandScope(whereName);
        }
        // A command that only runs in one region can't have keys anywhere else.
        if (command.Scope != CommandScope.Global && scope != command.Scope) return null;
        return name is not null && Parse(name) is { } chord ? new KeyBinding(chord, command.Id, scope) : null;
    }

    private void Apply(IReadOnlyList<KeyBinding> bindings)
    {
        foreach (var binding in Current)
            _keys.Unbind(binding.Chord, binding.Scope);
        foreach (var binding in bindings)
            _keys.Bind(binding.Chord, binding.CommandId, binding.Scope);
    }

    internal sealed class SameKey : IEqualityComparer<KeyBinding>
    {
        public static SameKey Instance { get; } = new();

        public bool Equals(KeyBinding? x, KeyBinding? y) =>
            x is not null && y is not null && x.CommandId == y.CommandId && x.Scope == y.Scope
            && KeyChord.Canonical(Keymap.Normalize(x.Chord)) == KeyChord.Canonical(Keymap.Normalize(y.Chord));

        public int GetHashCode(KeyBinding binding) =>
            HashCode.Combine(binding.CommandId, binding.Scope, KeyChord.Canonical(Keymap.Normalize(binding.Chord)));
    }
}
