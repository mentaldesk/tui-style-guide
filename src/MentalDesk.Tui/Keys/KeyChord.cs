using System.Globalization;

namespace MentalDesk.Tui.Keys;

// Canonical (raw keycodes) is a chord's identity. Display is lossy (Ctrl+Alt+Shift++), so never parse it back.
public static class KeyChord
{
    private static readonly (string Name, string Symbol)[] Arrows =
    [
        ("CursorUp", "↑"),
        ("CursorDown", "↓"),
        ("CursorLeft", "←"),
        ("CursorRight", "→"),
    ];

    public static string Canonical(IReadOnlyList<Key> chord) =>
        string.Join(' ', chord.Select(key => ((uint)key.KeyCode).ToString(CultureInfo.InvariantCulture)));

    public static string Display(IReadOnlyList<Key> chord) => string.Join(' ', chord.Select(Display));

    public static string Display(Key key)
    {
        // Chord steps match either case, so show the letter as a keycap does.
        var text = key.ToString();
        if (text.Length == 1 && char.IsLetter(text[0]))
            return text.ToUpperInvariant();
        foreach (var (name, symbol) in Arrows)
        {
            if (text == name) return symbol;
            if (text.EndsWith($"+{name}", StringComparison.Ordinal))
                return string.Concat(text.AsSpan(0, text.Length - name.Length), symbol);
        }
        return text;
    }

    public static IReadOnlyList<Key> Parse(string sequence)
    {
        var parts = sequence.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) throw new ArgumentException("A chord needs at least one key.", nameof(sequence));
        return [.. parts.Select(part => Key.TryParse(part, out var key)
            ? key
            : throw new ArgumentException($"Invalid key '{part}' in '{sequence}'.", nameof(sequence)))];
    }
}
