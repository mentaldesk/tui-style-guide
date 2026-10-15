using System.ComponentModel;
using System.Diagnostics;
using MentalDesk.Tui.Copying;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Links;

public sealed record LinkOutcome(string Message, Severity Severity);

/// <summary>Opens a link in the default browser, or copies it when the session came in over SSH. Use it instead of <c>Link.OpenUrl</c>.</summary>
public sealed class LinkOpener
{
    private static readonly string[] Schemes = ["http", "https", "mailto"];
    private static readonly string[] SshVariables = ["SSH_CONNECTION", "SSH_CLIENT", "SSH_TTY"];

    private readonly IClipboard? _clipboard;
    private readonly IBrowser _browser;
    private readonly Func<string, string?> _environment;

    public LinkOpener(IClipboard? clipboard)
        : this(clipboard, SystemBrowser.ThisMachine, Environment.GetEnvironmentVariable)
    {
    }

    internal LinkOpener(IClipboard? clipboard, IBrowser browser, Func<string, string?> environment)
    {
        _clipboard = clipboard;
        _browser = browser;
        _environment = environment;
    }

    public LinkOutcome Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !Schemes.Contains(uri.Scheme))
            return new("Couldn't open the link: only web and mail links open", Severity.Error);

        if (SshVariables.Any(name => !string.IsNullOrEmpty(_environment(name))))
        {
            var copied = _clipboard is TerminalClipboard terminal ? terminal.Write(url) : VerifiedClipboard.Write(_clipboard, url);
            return copied is CopyOutcome.Failed failed
                ? new(failed.Message, failed.Severity)
                : new($"Copied {url} (no browser over SSH)", Severity.Info);
        }

        return _browser.Open(url) is { } failure
            ? new($"Couldn't open the link: {failure}", Severity.Error)
            : new($"Opened {url}", Severity.Info);
    }
}

internal interface IBrowser
{
    /// <summary>Why it couldn't open <paramref name="url"/>, or null.</summary>
    string? Open(string url);
}

internal sealed class SystemBrowser(string? program) : IBrowser
{
    private const int FileNotFound = 2;

    public static SystemBrowser ThisMachine { get; } =
        new(OperatingSystem.IsWindows() ? null : OperatingSystem.IsMacOS() ? "open" : "xdg-open");

    public string? Open(string url)
    {
        try
        {
            var process = Process.Start(StartInfo(url));
            if (process is null || program is null) return null;
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return null;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == FileNotFound && program is not null)
        {
            return $"{program} not found";
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return e.Message;
        }
    }

    // Redirected so nothing the opener prints lands on the screen.
    internal ProcessStartInfo StartInfo(string url) => program is null
        ? new ProcessStartInfo(url) { UseShellExecute = true }
        : new ProcessStartInfo(program, [url])
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
}
