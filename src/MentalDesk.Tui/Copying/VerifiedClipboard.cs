using System.Globalization;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Copying;

public abstract record CopyOutcome
{
    public abstract string Message { get; }

    public abstract Severity Severity { get; }

    /// <param name="ThroughTerminal">Only the terminal took it: the platform clipboard didn't, or there isn't one.</param>
    public sealed record Copied(int Lines, int Characters, bool ThroughTerminal = false) : CopyOutcome
    {
        public override string Message =>
            $"Copied {Count(Lines, "line")}  •  {Count(Characters, "character")}{(ThroughTerminal ? " through the terminal" : "")}";

        public override Severity Severity => Severity.Info;

        private static string Count(int n, string noun) => $"{n:N0} {noun}{(n == 1 ? "" : "s")}";
    }

    public sealed record Failed(string Reason) : CopyOutcome
    {
        public override string Message => $"Copy failed: {Reason}";

        public override Severity Severity => Severity.Error;
    }
}

internal static class VerifiedClipboard
{
    // Checked by reading back, not IClipboard.IsSupported, which is false on macOS while the clipboard works.
    public static CopyOutcome Write(IClipboard? clipboard, string text, ClipboardTools? fallback = null)
    {
        var terminal = clipboard as TerminalClipboard;
        var platform = terminal is null ? clipboard : terminal.Platform;
        bool? sent = terminal?.Send(text);

        var failed = TryWrite(platform, text);
        // Terminal.Gui's clipboard on Linux is in-process, so only this app can paste from it.
        var heldOnlyInApp = failed is null && platform is FakeClipboard;
        if (failed is null && !heldOnlyInApp) return Copied(text);
        if (fallback is not null)
        {
            failed = fallback.Write(text);
            if (failed is null) return Copied(text);
        }

        return sent switch
        {
            true => Copied(text, throughTerminal: true),
            false => new CopyOutcome.Failed(TerminalClipboard.TooLarge),
            null when heldOnlyInApp => Copied(text),
            null => failed!,
        };
    }

    private static CopyOutcome.Copied Copied(string text, bool throughTerminal = false)
    {
        var lines = Lines(text);
        return new CopyOutcome.Copied(lines.Length, lines.Sum(line => new StringInfo(line).LengthInTextElements), throughTerminal);
    }

    private static CopyOutcome.Failed? TryWrite(IClipboard? clipboard, string text)
    {
        if (clipboard is null) return new CopyOutcome.Failed("there is no clipboard");
        string? readBack;
        try
        {
            clipboard.SetClipboardData(text);
            readBack = clipboard.GetClipboardData();
        }
        catch (Exception e)
        {
            return new CopyOutcome.Failed($"{FirstLine(e.Message)} ({Name(clipboard)})");
        }
        return readBack?.ReplaceLineEndings("\n") == text.ReplaceLineEndings("\n")
            ? null
            : new CopyOutcome.Failed($"the clipboard didn't take the text ({Name(clipboard)})");
    }

    private static string[] Lines(string text)
    {
        text = text.ReplaceLineEndings("\n");
        return (text.EndsWith('\n') ? text[..^1] : text).Split('\n');
    }

    private static string FirstLine(string message) =>
        message.Split('\n', 2)[0].Trim() is { Length: > 0 } line ? line : "the clipboard failed";

    private static string Name(IClipboard clipboard) => clipboard.GetType().Name switch
    {
        "MacOSXClipboard" => "macOS clipboard",
        "WindowsClipboard" => "Windows clipboard",
        "WSLClipboard" => "WSL clipboard",
        "FakeClipboard" => "the app's own clipboard",
        var name => name,
    };
}
