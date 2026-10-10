using System.Diagnostics;

namespace MentalDesk.Tui.Icons;

internal sealed class TerminalEnvironment
{
    public static TerminalEnvironment ThisMachine { get; } = new()
    {
        Variable = Environment.GetEnvironmentVariable,
        Folder = Environment.GetFolderPath,
        ReadFile = ReadIfThere,
        PlistToXml = Plutil,
        IsWindows = OperatingSystem.IsWindows(),
        IsMacOS = OperatingSystem.IsMacOS(),
    };

    public required Func<string, string?> Variable { get; init; }

    public required Func<Environment.SpecialFolder, string> Folder { get; init; }

    public required Func<string, string?> ReadFile { get; init; }

    public required Func<string, string?> PlistToXml { get; init; }

    public bool IsWindows { get; init; }

    public bool IsMacOS { get; init; }

    private static string? ReadIfThere(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // iTerm2 saves its preferences as a binary plist.
    private static string? Plutil(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo("plutil", ["-convert", "xml1", "-o", "-", path])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(2000) || process.ExitCode != 0) return null;
            return output.Result;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
