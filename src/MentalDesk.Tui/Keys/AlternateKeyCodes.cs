using System.Text.RegularExpressions;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Keys;

// Terminal.Gui 2.5 drops a CSI ~ key whose code carries kitty alternate keys, as iTerm2 sends Shift+F6: ESC[17:0:0;2~.
internal static partial class AlternateKeyCodes
{
    private static readonly CsiKeyPattern Csi = new();

    public static void Install(IInputProcessor input)
    {
        input.AnsiSequenceSwallowed -= OnSwallowed;
        input.AnsiSequenceSwallowed += OnSwallowed;
    }

    public static Key? Parse(string sequence) =>
        Sequence().Match(sequence) is { Success: true } match
            ? Csi.GetKey($"\e[{match.Groups["code"].Value}{match.Groups["modifiers"].Value}~")
            : null;

    private static void OnSwallowed(object? sender, string sequence)
    {
        if (sender is not IInputProcessor input || Parse(sequence) is not { } key) return;
        if (key.EventType == KeyEventType.Release)
            input.RaiseKeyUpEvent(key);
        else
            input.RaiseKeyDownEvent(key);
    }

    [GeneratedRegex(@"^\e\[(?<code>[1-6]|1[1-57-9]|2[0134])(?::\d*)+(?<modifiers>;\d+(?::\d+)*)?~$")]
    private static partial Regex Sequence();
}
