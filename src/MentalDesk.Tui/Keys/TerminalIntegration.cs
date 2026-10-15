using System.Text;
using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Keys;

public enum MacKeysState
{
    NotInstalled,
    Installed,
    OutOfDate,
}

/// <summary>One terminal's Mac keys for one app: a file of the app's own, which maps the keys only while the app runs.</summary>
public abstract class TerminalIntegration
{
    private readonly TerminalEnvironment _environment;

    private protected TerminalIntegration(MacKeys keys, TerminalEnvironment environment)
    {
        Keys = keys;
        _environment = environment;
    }

    public abstract string Terminal { get; }

    public abstract bool IsHere { get; }

    public abstract string Path { get; }

    /// <summary>The line the user adds to their own terminal config, which the library never edits, or null when none is needed.</summary>
    public virtual string? ConfigLine => null;

    /// <summary>The config file <see cref="ConfigLine"/> goes in, with <c>~</c> for the home folder.</summary>
    public virtual string? ConfigFile => null;

    public MacKeysState State
    {
        get
        {
            string text;
            try
            {
                if (!File.Exists(Path)) return MacKeysState.NotInstalled;
                text = File.ReadAllText(Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return MacKeysState.OutOfDate;
            }
            return InstalledVersion(text) == Version ? MacKeysState.Installed : MacKeysState.OutOfDate;
        }
    }

    internal MacKeys Keys { get; }

    internal abstract int Version { get; }

    internal abstract string Contents { get; }

    private protected bool IsMacOS => _environment.IsMacOS;

    /// <summary>Writes the file, or brings an out-of-date one up to date.</summary>
    public void Install()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, Contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Remove()
    {
        if (File.Exists(Path))
            File.Delete(Path);
    }

    internal abstract int? InstalledVersion(string text);

    private protected string? Variable(string name) => _environment.Variable(name);

    private protected string Home(params string[] parts) =>
        System.IO.Path.Combine([_environment.Folder(Environment.SpecialFolder.UserProfile), .. parts]);

    private protected static int? VersionAfter(string marker, string text)
    {
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return null;
        var end = text.IndexOf('\n', at);
        var rest = text[(at + marker.Length)..(end < 0 ? text.Length : end)].Trim();
        return int.TryParse(rest, out var version) ? version : null;
    }
}
