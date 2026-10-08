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
    private const string NothingId = "dialog.nothing";
    private const string PreviousId = "dialog.previous";
    private const string NextId = "dialog.next";

    private readonly Dictionary<ConfirmAction, Button> _buttons = [];

    public ConfirmDialog(
        string title, IReadOnlyList<string> lines, IReadOnlyList<ConfirmAction> actions,
        ConfirmAction? enter = null, ConfirmAction? focus = null)
        : base(title, DialogWidth(title, lines, actions, enter), contentRows: lines.Count + 3)
    {
        if (actions.Count == 0) throw new ArgumentException("A confirm needs at least one action.", nameof(actions));
        if (enter is not null && !actions.Contains(enter))
            throw new ArgumentException("Enter must be bound to one of the actions.", nameof(enter));
        if (enter?.Kind == ButtonKind.Danger)
            throw new ArgumentException("Enter is never bound to a Danger action.", nameof(enter));

        Lines = lines;
        for (var i = 0; i < lines.Count; i++)
            Add(new Label { X = 1, Y = i, Text = lines[i] });

        Commands.Register(NothingId, "Nothing", () => { });
        Commands.Register(PreviousId, "Previous button", () => Move(-1));
        Commands.Register(NextId, "Next button", () => Move(+1));
        // A focused Button presses on Enter, so Enter is always the dialog's: unbound, it does nothing.
        Keys.Bind([Key.Enter], NothingId)
            .Bind([Key.CursorLeft], PreviousId)
            .Bind([Key.CursorRight], NextId);

        var hints = new List<(string, Action?)>();
        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var id = $"dialog.action{i}";
            Commands.Register(id, action.Label, () => Choose(action));
            Keys.Bind([action.Key], id);
            if (action == enter)
                Keys.Bind([Key.Enter], id);
            var button = Button(action);
            button.IsDefault = action == enter;
            button.Accepting += (_, e) => Press(e, () => Choose(action));
            _buttons[action] = button;
            hints.Add(($"{HintKey(action, enter)} {action.Label.ToLowerInvariant()}", () => Choose(action)));
        }
        CancelButton = AppButton.Secondary("Cancel");
        CancelButton.Accepting += (_, e) => Press(e, Cancel);
        hints.Add(("Esc cancel", Cancel));
        Hints.Show(hints);

        ButtonRow = [.. actions.Select(action => _buttons[action]), CancelButton];
        for (var i = ButtonRow.Count - 1; i >= 0; i--)
        {
            var button = ButtonRow[i];
            button.Y = lines.Count + 1;
            button.X = i == ButtonRow.Count - 1 ? Pos.AnchorEnd() - 1 : Pos.Left(ButtonRow[i + 1]) - Shown(button.Text).Length - Gap.Length;
        }
        Add([.. ButtonRow]);
        var focused = focus is null ? CancelButton : _buttons[focus];
        Initialized += (_, _) => focused.SetFocus();
    }

    public IReadOnlyList<string> Lines { get; }

    public IReadOnlyList<Button> ButtonRow { get; }

    public Button CancelButton { get; }

    public ConfirmAction? Chosen { get; private set; }

    public string HintText => Hints.Says;

    public Button ButtonFor(ConfirmAction action) => _buttons[action];

    // A letter in the label is its underlined hotkey; any other key is written on the button.
    private static Button Button(ConfirmAction action) => action.Kind switch
    {
        ButtonKind.Primary => AppButton.Primary(Caption(action)),
        ButtonKind.Danger => AppButton.Danger(Caption(action)),
        _ => AppButton.Secondary(Caption(action)),
    };

    private static string Caption(ConfirmAction action)
    {
        var name = KeyName(action.Key);
        var at = name.Length == 1 && char.IsLetter(name[0])
            ? action.Label.IndexOf(name, StringComparison.OrdinalIgnoreCase)
            : -1;
        return at < 0 ? $"{action.Label}  {name}" : action.Label.Insert(at, "_");
    }

    private static string HintKey(ConfirmAction action, ConfirmAction? enter) => action == enter ? "Enter" : KeyName(action.Key);

    private static string KeyName(Key key) => key == Key.Delete ? "Del" : KeyChord.Display(key);

    private static string Shown(string text) => text.Replace("_", string.Empty, StringComparison.Ordinal);

    private static int DialogWidth(string title, IReadOnlyList<string> lines, IReadOnlyList<ConfirmAction> actions, ConfirmAction? enter)
    {
        var buttons = string.Join(Gap, actions.Select(action => $" {Shown(Caption(action))} ").Append(" Cancel "));
        var hints = string.Join(Chrome.HintRow.Separator,
            actions.Select(action => $"{HintKey(action, enter)} {action.Label}").Append("Esc cancel"));
        int[] widths = [title.Length + 4, .. lines.Select(line => line.Length), buttons.Length, hints.Length];
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

    private void Move(int by)
    {
        var at = ButtonRow.ToList().FindIndex(button => button.HasFocus);
        ButtonRow[Math.Clamp(at < 0 ? 0 : at + by, 0, ButtonRow.Count - 1)].SetFocus();
    }
}
