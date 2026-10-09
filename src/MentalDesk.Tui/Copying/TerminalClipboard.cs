using System.Text;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Copying;

/// <summary>Sends every copy through the terminal (OSC 52) as well as to the platform clipboard, so it reaches the user's machine over SSH.</summary>
public sealed class TerminalClipboard : IClipboard
{
    // tmux drops a sequence longer than 1 MiB, and the base64 of this many bytes stays under it.
    public const int MaxBytes = 750_000;

    public const string TooLarge = "too large to send through the terminal (the limit is 750 KB)";

    private readonly Action<string> _write;
    private readonly ClipboardTools _tools;

    internal TerminalClipboard(IClipboard? platform, Action<string> write, ClipboardTools tools)
    {
        Platform = platform;
        _write = write;
        _tools = tools;
    }

    public event EventHandler<CopyOutcome>? Copied;

    public IClipboard? Platform { get; }

    public bool IsSupported => true;

    public CopyOutcome Copy(string text)
    {
        var outcome = VerifiedClipboard.Write(this, text, _tools);
        Copied?.Invoke(this, outcome);
        return outcome;
    }

    /// <summary>False when the text is too large to send.</summary>
    public bool Send(string text)
    {
        if (Sequence(text) is not { } sequence) return false;
        _write(sequence);
        return true;
    }

    internal static string? Sequence(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return bytes.Length > MaxBytes ? null : $"\e]52;c;{Convert.ToBase64String(bytes)}\a";
    }

    // Terminal.Gui's in-process clipboard only holds what this app copied, so the system's is asked first.
    public string Paste()
    {
        var platform = Read(Platform);
        if (platform is { Length: > 0 } && Platform is not FakeClipboard) return platform;
        return _tools.Read() ?? platform ?? string.Empty;
    }

    public string GetClipboardData() => Paste();

    public void SetClipboardData(string text) => Copy(text);

    public bool TryGetClipboardData(out string result)
    {
        result = Paste();
        return true;
    }

    public bool TrySetClipboardData(string text) => Copy(text) is CopyOutcome.Copied;

    private static string? Read(IClipboard? clipboard)
    {
        try
        {
            return clipboard?.GetClipboardData();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
