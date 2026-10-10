using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Diagnostics;

public sealed class DiagnosticsDialog : AppDialog
{
    private const int FieldX = 18;

    private readonly TerminalCursor _cursor;
    private readonly Label _cursorColour;
    private readonly Label _keyName;
    private readonly Label _keyHex;
    private readonly Label _keyBase;
    private readonly Label _keyRune;
    private readonly RecordingScope _scope;

    public DiagnosticsDialog(IApplication app, TerminalCursor cursor, string theme, IconSettings icons)
        : base("Diagnostics", width: 72, contentRows: 14)
    {
        _cursor = cursor;
        var y = 0;
        Row("Driver", app.Driver?.GetName() ?? "unknown");
        Row("Kitty keyboard", Kitty(app));
        Row("Terminal", Terminal());
        Row("Theme", theme);
        _cursorColour = Row("Cursor colour", string.Empty);
        var iconsRow = Row("Icons", Icons(icons));
        iconsRow.Height = 2;
        IconsLine = iconsRow.Text;
        y++;
        y++;
        Add(new Label { X = 1, Y = y++, Text = "Last key" });
        _keyName = Row("  Name", "—");
        _keyHex = Row("  Hex", "—");
        _keyBase = Row("  Base", "—");
        _keyRune = Row("  Rune", "—");

        Commands.Register("diagnostics.close", "Close", Cancel);
        Keys.Bind("Esc", "diagnostics.close");
        ShowHints(Hint.Note("Press any key to see it here"), new Hint("diagnostics.close", "close"));
        _scope = new RecordingScope(this);

        ShowCursor();
        cursor.Query(app.Driver, () => app.Invoke(ShowCursor));

        Label Row(string heading, string value)
        {
            Add(new Label { X = 1, Y = y, Text = heading });
            var field = new Label { X = FieldX, Y = y++, Width = Dim.Fill(1), Text = value };
            Add(field);
            return field;
        }
    }

    public string CursorLine => _cursorColour.Text;

    public string IconsLine { get; }

    protected override IInputScope Scope => _scope;

    public void ShowKey(Key key)
    {
        var raw = (uint)key.KeyCode;
        var modifiers = (uint)(KeyCode.CtrlMask | KeyCode.AltMask | KeyCode.ShiftMask);
        _keyName.Text = key.ToString();
        _keyHex.Text = $"0x{raw:X8}";
        _keyBase.Text = $"0x{raw & ~modifiers:X8}";
        _keyRune.Text = key.AsRune.Value != 0 ? $"U+{key.AsRune.Value:X4}  {key.AsGrapheme}" : "—";
    }

    private void ShowCursor()
    {
        var asked = _cursor.Asked is { } wanted ? TerminalCursor.Hex(wanted) : "nothing";
        var reported = (_cursor.Report, _cursor.Reported) switch
        {
            (CursorReport.Answered, { } shown) => TerminalCursor.Hex(shown),
            (CursorReport.Waiting, _) => "asking…",
            _ => "no answer",
        };
        var verdict = (_cursor.Asked, _cursor.Reported) switch
        {
            ({ } a, { } r) => a == r ? "  ✓" : "  ✗",
            _ => string.Empty,
        };
        _cursorColour.Text = $"asked {asked}{HintRow.Separator}terminal reports {reported}{verdict}";
    }

    private static string Icons(IconSettings icons) => icons.Style == IconStyle.Auto
        ? $"{IconSettings.Name(icons.InUse)} (Auto){HintRow.Separator}{icons.Detected.Reason}"
        : $"{IconSettings.Name(icons.InUse)} (chosen){HintRow.Separator}Auto would pick {IconSettings.Name(icons.AutoChoice)}: {icons.Detected.Reason}";

    private static string Kitty(IApplication app) =>
        app.Driver?.KittyKeyboardCapabilities is not { } kitty ? "no answer"
        : kitty.IsSupported ? $"yes ({kitty.Flags})"
        : "not supported";

    private static string Terminal()
    {
        var program = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var version = Environment.GetEnvironmentVariable("TERM_PROGRAM_VERSION");
        var term = Environment.GetEnvironmentVariable("TERM");
        return program is { Length: > 0 } ? $"{program} {version}".TrimEnd() : term ?? "unknown";
    }

    private sealed class RecordingScope(DiagnosticsDialog dialog) : IInputScope
    {
        public KeyResult Handle(Key key)
        {
            dialog.ShowKey(key);
            dialog.Keys.Handle(key);
            return KeyResult.Consumed;
        }
    }
}
