using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Dialogs;

public enum ButtonKind
{
    Primary,

    Danger,

    Secondary,
}

public sealed record ConfirmAction(string Label, ButtonKind Kind, Key Key);

public sealed class ConfirmDialog : AppDialog
{
    private const string Gap = "   ";
    private const string PreviousId = "dialog.previous";
    private const string NextId = "dialog.next";
    private const string CancelCaption = "Esc Cancel";

    private readonly Dictionary<ConfirmAction, Button> _buttons = [];
    private readonly EnterScope _scope;

    public ConfirmDialog(
        string title, IReadOnlyList<string> lines, IReadOnlyList<ConfirmAction> actions,
        ConfirmAction? focus = null, TextField? field = null)
        : base(title, DialogWidth(title, lines, actions), contentRows: lines.Count + (field is null ? 2 : 3))
    {
        if (actions.Count == 0) throw new ArgumentException("A confirm needs at least one action.", nameof(actions));
        if (actions.Any(action => action.Key == Key.Enter))
            throw new ArgumentException("Enter never confirms a dialog: Ctrl+Enter does.", nameof(actions));
        if (actions.Count(action => action.Key == CtrlEnter.Key) != 1)
            throw new ArgumentException("Ctrl+Enter is bound to exactly one action.", nameof(actions));
        if (focus is { Kind: ButtonKind.Danger })
            throw new ArgumentException("Focus never starts on a Danger button.", nameof(focus));

        Lines = lines;
        for (var i = 0; i < lines.Count; i++)
            Add(new Label { X = 1, Y = i, Text = lines[i] });
        Field = field;
        if (field is not null)
        {
            field.X = 1;
            field.Y = lines.Count;
            field.Width = Dim.Fill(1);
            Add(field);
        }
        var buttonsY = lines.Count + (field is null ? 1 : 2);

        // Off while the field has focus, so the arrows move its cursor instead.
        Commands.Register(PreviousId, "Previous button", () => Move(-1), isEnabled: OnAButton);
        Commands.Register(NextId, "Next button", () => Move(+1), isEnabled: OnAButton);
        Keys.Bind([Key.CursorLeft], PreviousId)
            .Bind([Key.CursorRight], NextId);

        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var id = $"dialog.action{i}";
            Commands.Register(id, action.Label, () => Choose(action));
            Keys.Bind([action.Key], id);
            var button = Button(action);
            button.IsDefault = false;
            button.Accepting += (_, e) => Press(e, () => Choose(action));
            _buttons[action] = button;
        }
        CancelButton = AppButton.Secondary(CancelCaption);
        CancelButton.Accepting += (_, e) => Press(e, Cancel);

        ButtonRow = [.. actions.Select(action => _buttons[action]), CancelButton];
        for (var i = ButtonRow.Count - 1; i >= 0; i--)
        {
            var button = ButtonRow[i];
            button.Y = buttonsY;
            button.X = i == ButtonRow.Count - 1 ? Pos.AnchorEnd() - 1 : Pos.Left(ButtonRow[i + 1]) - Shown(button.Text).Length - Gap.Length;
        }
        Add([.. ButtonRow]);
        View focused = focus is not null ? _buttons[focus] : (View?)field ?? CancelButton;
        Initialized += (_, _) => focused.SetFocus();
        _scope = new EnterScope(Keys);
    }

    public IReadOnlyList<string> Lines { get; }

    public IReadOnlyList<Button> ButtonRow { get; }

    public Button CancelButton { get; }

    public TextField? Field { get; }

    public ConfirmAction? Chosen { get; private set; }

    public Button ButtonFor(ConfirmAction action) => _buttons[action];

    protected override IInputScope Scope => _scope;

    // A letter in the label is its underlined hotkey; any other key is written before the label, as in a hint.
    private static Button Button(ConfirmAction action) => action.Kind switch
    {
        ButtonKind.Primary => AppButton.Primary(Caption(action)),
        ButtonKind.Danger => AppButton.Danger(Caption(action)),
        _ => AppButton.Secondary(Caption(action)),
    };

    private static string Caption(ConfirmAction action)
    {
        var name = KeyChord.Display(action.Key);
        var at = name.Length == 1 && char.IsLetter(name[0])
            ? action.Label.IndexOf(name, StringComparison.OrdinalIgnoreCase)
            : -1;
        return at < 0 ? $"{name} {action.Label}" : action.Label.Insert(at, "_");
    }

    private static string Shown(string text) => text.Replace("_", string.Empty, StringComparison.Ordinal);

    private static int DialogWidth(string title, IReadOnlyList<string> lines, IReadOnlyList<ConfirmAction> actions)
    {
        var buttons = string.Join(Gap, actions.Select(action => $" {Shown(Caption(action))} ").Append($" {CancelCaption} "));
        int[] widths = [title.Length + 4, .. lines.Select(line => line.Length), buttons.Length];
        return Math.Max(40, widths.Max() + 4);
    }

    private static void Press(CommandEventArgs e, Action choice)
    {
        e.Handled = true;
        choice();
    }

    private void Choose(ConfirmAction action)
    {
        Chosen = action;
        Accept();
    }

    private bool OnAButton() => ButtonRow.Any(button => button.HasFocus);

    private void Move(int by)
    {
        var at = ButtonRow.ToList().FindIndex(button => button.HasFocus);
        ButtonRow[Math.Clamp(at < 0 ? 0 : at + by, 0, ButtonRow.Count - 1)].SetFocus();
    }

    // A focused Button presses on Enter, so the dialog takes Enter and does nothing with it.
    private sealed class EnterScope(Keymap keys) : IInputScope
    {
        public KeyResult Handle(Key key) => key == Key.Enter ? KeyResult.Consumed : keys.Handle(key);

        public string? PendingChord => keys.PendingChord;
    }
}
