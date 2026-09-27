using MentalDesk.Tui.Theming;
using Terminal.Gui.Text;

namespace MentalDesk.Tui.Dialogs;

// A Label that outgrows its row draws over what's below it, so the dialog grows by Lines instead.
public sealed class AlertView : View
{
    private const string Indent = " ";

    public AlertView()
    {
        Width = Dim.Fill();
        Height = 0;
        Visible = false;
        CanFocus = false;
    }

    public string Message { get; private set; } = string.Empty;

    public int Lines { get; private set; }

    public void Show(string message, Severity severity, int width)
    {
        Message = message;
        SchemeName = severity == Severity.Error ? SchemeNames.Error : SchemeNames.Accent;
        var wrapped = message.Length == 0
            ? []
            : TextFormatter.Format(
                message, Math.Max(1, width - (Indent.Length * 2)), Alignment.Start, wordWrap: true,
                preserveTrailingSpaces: false, tabWidth: 4, TextDirection.LeftRight_TopBottom, multiLine: false,
                textFormatter: null, preserveTabs: false);
        Lines = wrapped.Count;
        Text = string.Join('\n', wrapped.Select(line => Indent + line));
        Height = Lines;
        Visible = Lines > 0;
    }

    public void Clear() => Show(string.Empty, Severity.Info, 1);
}
