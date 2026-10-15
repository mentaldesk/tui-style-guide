using System.Text.RegularExpressions;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Keys;

public sealed record MacKeyRow(string Keys, string Does);

/// <summary>Cmd and Option keys for one app, through an integration installed in iTerm2, kitty or WezTerm under the app's own name.</summary>
public sealed partial class MacKeys
{
    public static readonly IReadOnlyList<MacKeyRow> Rows =
    [
        new("Cmd+← / Cmd+→", "Start / end of the line"),
        new("Cmd+↑ / Cmd+↓", "Start / end of the text"),
        new("Option+← / Option+→", "Previous / next word"),
        new("Any of those with Shift", "Select as it moves"),
        new("Cmd+⌫ / Option+⌫", "Delete to the start of the line / the previous word"),
        new("Cmd+A  C  X  Z", "Select all, copy, cut, undo"),
    ];

    private readonly TerminalEnvironment _environment;
    private readonly Action<string> _write;

    /// <param name="app">The app's process name, such as <c>Swatch</c>: letters and digits only.</param>
    public MacKeys(string app) : this(app, TerminalEnvironment.ThisMachine, WriteToConsole)
    {
    }

    internal MacKeys(string app, TerminalEnvironment environment, Action<string> write)
    {
        if (!AppName().IsMatch(app))
            throw new ArgumentException("An app's name is a letter then letters and digits.", nameof(app));
        App = app;
        _environment = environment;
        _write = write;
        BindDeleteToLineStart();
        Integrations = [new Iterm2Integration(this, environment), new WezTermIntegration(this, environment), new KittyIntegration(this, environment)];
    }

    public string App { get; }

    public IReadOnlyList<TerminalIntegration> Integrations { get; }

    /// <summary>This terminal's integration, or null where none can be installed.</summary>
    public TerminalIntegration? Here => Integrations.FirstOrDefault(integration => integration.IsHere);

    public bool OnAMac => _environment.IsMacOS;

    /// <summary>The terminal's name, supported or not.</summary>
    public string Terminal => Here?.Terminal ?? _environment.Variable("TERM_PROGRAM") switch
    {
        "Apple_Terminal" => "Terminal.app",
        { Length: > 0 } other => other,
        _ => "Unknown",
    };

    internal string FileName => App.ToLowerInvariant();

    internal string Variable => $"{App.ToUpperInvariant()}_ACTIVE";

    internal string Active => UserVariable("1");

    internal string Inactive => UserVariable("0");

    /// <summary>Turns the app's keys on in kitty and WezTerm until disposed. <c>AppShell</c> does it for its apps.</summary>
    public IDisposable Activate()
    {
        _write(Active);
        return new Activation(this);
    }

    /// <summary>Turns the keys off while the app is suspended, so the shell gets the terminal's own.</summary>
    public void WhileSuspended(Action suspend)
    {
        _write(Inactive);
        try
        {
            suspend();
        }
        finally
        {
            _write(Active);
        }
    }

    // Cmd+⌫ arrives as Ctrl+Shift+Backspace, which TextField leaves unbound; MultiLineField binds it itself.
    private static void BindDeleteToLineStart()
    {
        var bindings = TextField.DefaultKeyBindings;
        if (bindings is null) return;
        var key = MultiLineField.DeleteToLineStart;
        var existing = bindings.GetValueOrDefault(Command.CutToStartOfLine);
        if (existing?.All?.Contains(key) == true) return;
        bindings[Command.CutToStartOfLine] = (existing ?? new PlatformKeyBinding()) with { All = [.. existing?.All ?? [], key] };
    }

    // OSC 1337 SetUserVar, which kitty and WezTerm key the mappings off; other terminals ignore it.
    private string UserVariable(string value) =>
        $"\e]1337;SetUserVar={Variable}={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))}\a";

    private static void WriteToConsole(string sequence)
    {
        Console.Out.Write(sequence);
        Console.Out.Flush();
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex AppName();

    private sealed class Activation(MacKeys keys) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            keys._write(keys.Inactive);
        }
    }
}
