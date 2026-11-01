namespace MentalDesk.Tui.Shell;

// OSC 0 sets the tab's title; CSI 22;0t and 23;0t push and pop the terminal's own, as Vim does.
public sealed class TerminalTitle(Action<string> write)
{
    public const string Separator = " — ";

    private string _app = string.Empty;
    private string? _place;
    private int _waiting;
    private int _unseenErrors;
    private bool _showing;

    public static TerminalTitle ForConsole() => new(sequence =>
    {
        Console.Out.Write(sequence);
        Console.Out.Flush();
    });

    public string App
    {
        get => _app;
        set => Set(ref _app, value);
    }

    // What it's open on: a folder's or a team's name, never a full path or the current file.
    public string? Place
    {
        get => _place;
        set => Set(ref _place, value);
    }

    public int Waiting
    {
        get => _waiting;
        set => Set(ref _waiting, value);
    }

    public int UnseenErrors
    {
        get => _unseenErrors;
        set => Set(ref _unseenErrors, value);
    }

    public string Text => Compose(App, Place, Waiting, UnseenErrors);

    public string? Shown { get; private set; }

    public static string Compose(string app, string? place = null, int waiting = 0, int unseenErrors = 0)
    {
        List<string> parts = [];
        if (unseenErrors > 0) parts.Add($"⚠ {unseenErrors}");
        if (waiting > 0) parts.Add($"({waiting})");
        var name = string.IsNullOrEmpty(place) ? app : string.IsNullOrEmpty(app) ? place : $"{place}{Separator}{app}";
        if (name.Length > 0) parts.Add(name);
        return string.Join(' ', parts);
    }

    // Stops Terminal.Gui writing each modal Runnable's Title to the tab over this one, until disposed.
    public static IDisposable TakeOver(IApplication app)
    {
        app.SessionBegun += StopRunnableWriting;
        return new Releaser(() => app.SessionBegun -= StopRunnableWriting);
    }

    public void Save() => write("\x1b[22;0t");

    public void Show()
    {
        _showing = true;
        Shown = null;
        Write();
    }

    public void Hold() => _showing = false;

    public void Restore()
    {
        Hold();
        Shown = null;
        write("\x1b[23;0t");
    }

    private static void StopRunnableWriting(object? sender, SessionTokenEventArgs e)
    {
        if (e.State.Runnable is View view)
            view.Border.Settings &= ~BorderSettings.TerminalTitle;
    }

    private sealed class Releaser(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Write();
    }

    private void Write()
    {
        var text = Text;
        if (!_showing || text.Length == 0 || text == Shown) return;
        write($"\x1b]0;{new string([.. text.Where(c => !char.IsControl(c))])}\x07");
        Shown = text;
    }
}
