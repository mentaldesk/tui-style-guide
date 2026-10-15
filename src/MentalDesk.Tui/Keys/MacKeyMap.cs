namespace MentalDesk.Tui.Keys;

/// <summary>One Mac key, as each terminal names it, and the bytes it sends instead: TuiCode's map, unchanged.</summary>
internal sealed record MacKeyMapping(string Kitty, string WezTermKey, string WezTermMods, string? Iterm2, string Bytes);

internal static class MacKeyMap
{
    public static readonly MacKeyMapping[] All =
    [
        new("cmd+c", "c", "CMD", "0x63-0x100000", "\x03"),
        new("cmd+x", "x", "CMD", "0x78-0x100000", "\x18"),
        new("cmd+z", "z", "CMD", "0x7a-0x100000", "\x1a"),
        // iTerm2 keys a shifted letter by its shifted character (mentaldesk/TuiCode#46).
        new("cmd+shift+z", "z", "CMD|SHIFT", "0x5a-0x120000", "\x19"),
        new("cmd+a", "a", "CMD", "0x61-0x100000", "\x01"),
        new("cmd+backspace", "Backspace", "CMD", "0x7f-0x100000", "\e[127;6u"),
        new("opt+backspace", "Backspace", "OPT", "0x7f-0x80000", "\e[127;5u"),
        new("cmd+left", "LeftArrow", "CMD", "0xf702-0x300000", "\e[H"),
        new("cmd+right", "RightArrow", "CMD", "0xf703-0x300000", "\e[F"),
        new("cmd+shift+left", "LeftArrow", "CMD|SHIFT", "0xf702-0x320000", "\e[1;2H"),
        new("cmd+shift+right", "RightArrow", "CMD|SHIFT", "0xf703-0x320000", "\e[1;2F"),
        new("opt+left", "LeftArrow", "OPT", "0xf702-0x280000", "\e[1;5D"),
        new("opt+right", "RightArrow", "OPT", "0xf703-0x280000", "\e[1;5C"),
        new("opt+shift+left", "LeftArrow", "OPT|SHIFT", "0xf702-0x2a0000", "\e[1;6D"),
        new("opt+shift+right", "RightArrow", "OPT|SHIFT", "0xf703-0x2a0000", "\e[1;6C"),
        new("cmd+up", "UpArrow", "CMD", "0xf700-0x300000", "\e[1;5H"),
        new("cmd+down", "DownArrow", "CMD", "0xf701-0x300000", "\e[1;5F"),
        new("cmd+shift+up", "UpArrow", "CMD|SHIFT", "0xf700-0x320000", "\e[1;6H"),
        new("cmd+shift+down", "DownArrow", "CMD|SHIFT", "0xf701-0x320000", "\e[1;6F"),
        new("page_up", "PageUp", "NONE", "0xf72c-0x200000", "\e[5~"),
        new("page_down", "PageDown", "NONE", "0xf72d-0x200000", "\e[6~"),
        new("shift+page_up", "PageUp", "SHIFT", "0xf72c-0x220000", "\e[5;2~"),
        new("shift+page_down", "PageDown", "SHIFT", "0xf72d-0x220000", "\e[6;2~"),
        new("ctrl+page_up", "PageUp", "CTRL", null, "\e[5;5~"),
        new("ctrl+page_down", "PageDown", "CTRL", null, "\e[6;5~"),
        new("opt+delete", "Delete", "OPT", "0xf728-0x200000", "\x04"),
        new("cmd+delete", "Delete", "CMD", "0xf728-0x280000", "\e[3;5~"),
    ];

    /// <summary>The bytes as kitty and Lua spell them: <c>\x1b[H</c>.</summary>
    public static string Escaped(string bytes) =>
        string.Concat(bytes.Select(c => c < ' ' || c == '\x7f' ? $"\\x{(int)c:x2}" : c.ToString()));
}
