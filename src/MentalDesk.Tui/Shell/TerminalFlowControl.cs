using System.Diagnostics;

namespace MentalDesk.Tui.Shell;

// XON/XOFF flow control eats Ctrl+S and Ctrl+Q before the app sees them.
public sealed class TerminalFlowControl : IDisposable
{
    private readonly string? _saved;

    public TerminalFlowControl()
    {
        if (OperatingSystem.IsWindows() || !Environment.UserInteractive) return;
        try
        {
            _saved = Stty("-g");
            Stty("-ixon -ixoff");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _saved = null;
        }
    }

    public void Dispose()
    {
        if (_saved is null) return;
        try
        {
            Stty(_saved);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static string Stty(string arguments)
    {
        using var stty = Process.Start(new ProcessStartInfo("stty", arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Couldn't start stty.");
        var output = stty.StandardOutput.ReadToEnd();
        stty.WaitForExit();
        return output.Trim();
    }
}
