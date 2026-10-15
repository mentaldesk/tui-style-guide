using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Dialogs;

public abstract class AppDialog : Dialog
{
    public const string CancelId = "dialog.cancel";

    private const string ButtonGap = "   ";

    private readonly int _contentRows;
    private readonly ButtonsIgnoreEnter _buttonsIgnoreEnter;

    protected AppDialog(string title, int width, int contentRows)
    {
        _contentRows = contentRows;
        Alert = new AlertView { X = 0, Y = contentRows };
        Title = title;
        Width = width;
        Height = Dim.Func(_ => _contentRows + Alert.Lines + GetAdornmentsThickness().Vertical);
        Add(Alert);
        Keys = new Keymap(Commands);
        _buttonsIgnoreEnter = new ButtonsIgnoreEnter(this);
        Commands
            .Register(CancelId, "Cancel", Cancel)
            .Register(ShellCommands.ShowKeys, "Show keys", ShowKeys, isEnabled: () => KeysRequested is not null);
        Keys.Bind("Esc", CancelId)
            .Bind("F1", ShellCommands.ShowKeys);
        AddCommand(Command.Quit, () =>
        {
            Cancel();
            return true;
        });
    }

    // However it closed, with Chosen and Confirmed already set.
    public event EventHandler? Closed;

    // Without a handler, F1 is left to the host.
    public event EventHandler<KeySheet>? KeysRequested;

    public AlertView Alert { get; }

    public bool Confirmed { get; private set; }

    protected CommandRegistry Commands { get; } = new();

    protected Keymap Keys { get; }

    public virtual IInputScope Scope => _buttonsIgnoreEnter;

    public void Run(AppShell shell)
    {
        EventHandler<KeySheet> showKeys = (_, sheet) => ShowKeys(shell, sheet);
        KeysRequested += showKeys;
        shell.Scopes.Push(Scope);
        try
        {
            shell.App.Run(this);
        }
        finally
        {
            shell.Scopes.Pop(Scope);
            KeysRequested -= showKeys;
        }
        RaiseClosed();
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

    protected void AddButtonRow(Pos y, params Button[] row)
    {
        for (var i = row.Length - 1; i >= 0; i--)
        {
            row[i].Y = y;
            row[i].X = i == row.Length - 1 ? Pos.AnchorEnd() - 1 : Pos.Left(row[i + 1]) - row[i].Text.Length - ButtonGap.Length;
        }
        Add(row);
    }

    // However it closed: Esc, a button, or the close box.
    protected virtual void OnClosed()
    {
    }

    protected void Accept()
    {
        Confirmed = true;
        Close();
    }

    protected void Cancel()
    {
        Confirmed = false;
        Close();
    }

    // Run raises Closed once its loop ends; a hosted dialog never stops the host's loop.
    private void Close()
    {
        if (IsRunning)
            RequestStop();
        else
            RaiseClosed();
    }

    private void RaiseClosed()
    {
        OnClosed();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowKeys() => KeysRequested?.Invoke(this, KeysHere());

    private void ShowKeys(AppShell shell, KeySheet sheet)
    {
        var focused = MostFocused;
        using (var keys = new KeysDialog(sheet, shell.App.Screen.Size))
            keys.Run(shell);
        focused?.SetFocus();
    }

    // A focused Button presses on Enter, and Enter never confirms a dialog.
    private sealed class ButtonsIgnoreEnter(AppDialog dialog) : IInputScope
    {
        public KeyResult Handle(Key key) =>
            key == Key.Enter && dialog.MostFocused is Button ? KeyResult.Consumed : dialog.Keys.Handle(key);

        public string? PendingChord => dialog.Keys.PendingChord;
    }
}
