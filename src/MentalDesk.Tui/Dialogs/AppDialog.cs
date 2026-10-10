using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Dialogs;

public abstract class AppDialog : Dialog
{
    public const string CancelId = "dialog.cancel";

    private const string ButtonGap = "   ";

    private readonly int _contentRows;
    private AppShell? _shell;

    protected AppDialog(string title, int width, int contentRows)
    {
        _contentRows = contentRows;
        Alert = new AlertView { X = 0, Y = contentRows };
        Title = title;
        Width = width;
        Height = Dim.Func(_ => _contentRows + Alert.Lines + GetAdornmentsThickness().Vertical);
        Add(Alert);
        Keys = new Keymap(Commands);
        Commands
            .Register(CancelId, "Cancel", Cancel)
            .Register(ShellCommands.ShowKeys, "Show keys", ShowKeys);
        Keys.Bind("Esc", CancelId)
            .Bind("F1", ShellCommands.ShowKeys);
    }

    public AlertView Alert { get; }

    public bool Confirmed { get; private set; }

    protected CommandRegistry Commands { get; } = new();

    protected Keymap Keys { get; }

    protected virtual IInputScope Scope => Keys;

    public void Run(AppShell shell)
    {
        _shell = shell;
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

    public KeySheet KeysHere() => KeySheet.ForDialog(Title, Commands, Keys);

    public void ShowAlert(string message, Severity severity)
    {
        Alert.Show(message, severity, Viewport.Width);
        SetNeedsLayout();
    }

    // The key is on the label, so the button never takes focus from where the user is typing.
    protected Button CommandButton(ButtonKind kind, string commandId)
    {
        var key = Keys.For(commandId).First().Display;
        var button = AppButton.Of(kind, $"{key} {Commands.Find(commandId)!.Label}");
        button.CanFocus = false;
        button.Accepting += (_, e) =>
        {
            e.Handled = true;
            Commands.Execute(commandId);
        };
        return button;
    }

    protected void AddButtonRow(int y, params Button[] row)
    {
        for (var i = row.Length - 1; i >= 0; i--)
        {
            row[i].Y = y;
            row[i].X = i == row.Length - 1 ? Pos.AnchorEnd() - 1 : Pos.Left(row[i + 1]) - row[i].Text.Length - ButtonGap.Length;
        }
        Add(row);
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

    private void ShowKeys()
    {
        if (_shell is null) return;
        var focused = MostFocused;
        using (var keys = new KeysDialog(KeysHere(), _shell.App.Screen.Size))
            keys.Run(_shell);
        focused?.SetFocus();
    }
}
