using System.Globalization;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Theming;

// OSC 12 sets the cursor colour, OSC 112 restores it, and OSC 12;? reads it back (mentaldesk/TuiCode#243).
public sealed class TerminalCursor(Action<string> write)
{
    public Color? Asked { get; private set; }

    public Color? Reported { get; private set; }

    public CursorReport Report { get; private set; } = CursorReport.NotAsked;

    public static TerminalCursor ForConsole() => new(sequence =>
    {
        Console.Out.Write(sequence);
        Console.Out.Flush();
    });

    public void Colour(Color colour)
    {
        write($"\x1b]12;{Hex(colour)}\x07");
        Asked = colour;
    }

    public void Restore()
    {
        if (Asked is null) return;
        write("\x1b]112\x07");
        Asked = null;
    }

    public void Query(IDriver? driver, Action? answered = null)
    {
        if (driver is null or { IsLegacyConsole: true })
        {
            Report = CursorReport.NoAnswer;
            answered?.Invoke();
            return;
        }
        Report = CursorReport.Waiting;
        driver.QueueAnsiRequest(new AnsiEscapeSequenceRequest
        {
            Request = $"{EscSeqUtils.OSC}12;?{EscSeqUtils.ST}",
            Terminator = EscSeqUtils.ST,
            Value = "12",
            ResponseReceived = response =>
            {
                Reported = EscSeqUtils.TryParseOscColorResponse(response, out var colour) ? colour : null;
                Report = Reported is null ? CursorReport.NoAnswer : CursorReport.Answered;
                answered?.Invoke();
            },
            Abandoned = () =>
            {
                Report = CursorReport.NoAnswer;
                answered?.Invoke();
            },
        });
    }

    public static string Hex(Color colour) =>
        string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}");
}

public enum CursorReport
{
    NotAsked,
    Waiting,
    NoAnswer,
    Answered,
}
