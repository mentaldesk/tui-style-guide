using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Dialogs;

public abstract class AppDialog : Dialog
{
    public const string CancelId = "dialog.cancel";

    private const string ButtonGap = "   ";

    private readonly int _contentRows;
    private readonly ButtonsIgnoreEnter _buttonsIgnoreEnter;
    private readonly List<FieldCheck> _checks = [];

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

    // Set while Run runs it, so it can open a dialog of its own.
    protected AppShell? RunningIn { get; private set; }

    protected Keymap Keys { get; }

    public virtual IInputScope Scope => _buttonsIgnoreEnter;

    public void Run(AppShell shell)
    {
        EventHandler<KeySheet> showKeys = (_, sheet) => ShowKeys(shell, sheet);
        KeysRequested += showKeys;
        shell.Scopes.Push(Scope);
        RunningIn = shell;
        try
        {
            shell.App.Run(this);
        }
        finally
        {
            RunningIn = null;
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

    // For a no that comes back later, such as from disk or a server.
    public void Refuse(View field, string message)
    {
        var check = _checks.Find(check => check.Field == field)
            ?? throw new ArgumentException("Refuse a field registered with Check.", nameof(field));
        check.Mark(message);
        field.SetFocus();
        SayRefusals();
    }

    // A field is checked as it changes only once refused.
    protected void Check(View field, View label, Func<string?> refusal)
    {
        var check = new FieldCheck(field, label, refusal);
        _checks.Add(check);
        field.TextChanged += (_, _) => Recheck(check);
        if (field is Terminal.Gui.Editor.Editor editor)
            editor.ContentChanged += (_, _) => Recheck(check);
        field.HasFocusChanged += (_, e) =>
        {
            if (e.NewValue && _checks.Any(c => c.Refusal is not null))
                SayRefusals();
        };
    }

    // A confirm starts with this.
    protected bool Checked()
    {
        foreach (var check in _checks)
            check.Mark(check.Refuses());
        var first = _checks.Find(check => check.Refusal is not null);
        if (first is null)
            return true;
        first.Field.SetFocus();
        SayRefusals();
        return false;
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

    // Esc, the close box or a Cancel button; false keeps the dialog open.
    protected virtual bool MayCancel() => true;

    protected void Cancel()
    {
        if (!MayCancel()) return;
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

    private void Recheck(FieldCheck check)
    {
        if (check.Refusal is null) return;
        check.Mark(check.Refuses());
        SayRefusals();
    }

    private void SayRefusals()
    {
        var refused = _checks.Where(check => check.Refusal is not null).ToList();
        if (refused.Count == 0)
        {
            Alert.Clear();
            SetNeedsLayout();
            return;
        }
        var said = refused.Find(check => check.Field.HasFocus) ?? refused[0];
        ShowAlert(refused.Count == 1 ? said.Refusal! : $"{said.Refusal}  •  {refused.Count - 1} more to fix", Severity.Error);
    }

    private void ShowKeys() => KeysRequested?.Invoke(this, KeysHere());

    private void ShowKeys(AppShell shell, KeySheet sheet)
    {
        var focused = MostFocused;
        using (var keys = new KeysDialog(sheet, shell.App.Screen.Size))
            keys.Run(shell);
        focused?.SetFocus();
    }

    private sealed class FieldCheck(View field, View label, Func<string?> refuses)
    {
        private readonly string? _labelScheme = label.SchemeName;

        public View Field { get; } = field;

        public string? Refusal { get; private set; }

        public string? Refuses() => refuses();

        public void Mark(string? refusal)
        {
            Refusal = refusal;
            label.SchemeName = refusal is null ? _labelScheme : SchemeNames.Error;
        }
    }

    // A focused Button presses on Enter, and Enter never confirms a dialog.
    private sealed class ButtonsIgnoreEnter(AppDialog dialog) : IInputScope
    {
        public KeyResult Handle(Key key) =>
            key == Key.Enter && dialog.MostFocused is Button ? KeyResult.Consumed : dialog.Keys.Handle(key);

        public string? PendingChord => dialog.Keys.PendingChord;
    }
}
