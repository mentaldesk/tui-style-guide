using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Dialogs;

public abstract class AppDialog : Dialog
{
    public const string CancelId = "dialog.cancel";

    private readonly int _contentRows;

    protected AppDialog(string title, int width, int contentRows)
    {
        _contentRows = contentRows;
        Hints = new HintRow { X = 1, Y = contentRows, Width = Dim.Fill(1) };
        Alert = new AlertView { X = 0, Y = contentRows + 1 };
        Title = title;
        Width = width;
        Height = Dim.Func(_ => _contentRows + 1 + Alert.Lines + GetAdornmentsThickness().Vertical);
        Add(Hints, Alert);
        Keys = new Keymap(Commands);
        Commands.Register(CancelId, "Cancel", Cancel);
        Keys.Bind("Esc", CancelId);
    }

    public HintRow Hints { get; }

    public AlertView Alert { get; }

    public bool Confirmed { get; private set; }

    protected CommandRegistry Commands { get; } = new();

    protected Keymap Keys { get; }

    protected virtual IInputScope Scope => Keys;

    public void Run(AppShell shell)
    {
        shell.Scopes.Push(Scope);
        try
        {
            shell.App.Run(this);
        }
        finally
        {
            shell.Scopes.Pop(Scope);
        }
    }

    protected void ShowHints(params Hint[] hints) => Hints.Show(hints, Commands, Keys);

    public void ShowAlert(string message, Severity severity)
    {
        Alert.Show(message, severity, Viewport.Width);
        SetNeedsLayout();
    }

    protected void Accept()
    {
        Confirmed = true;
        RequestStop();
    }

    protected void Cancel()
    {
        Confirmed = false;
        RequestStop();
    }
}
