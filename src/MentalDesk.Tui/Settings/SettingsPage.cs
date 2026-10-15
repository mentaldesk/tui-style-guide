using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Settings;

internal sealed record PageAction(string Id, string Label, Key Key, Action Run);

// Every change is staged: it may preview on screen, but nothing is saved until Keep.
public abstract class SettingsPage : View
{
    private readonly List<PageAction> _actions = [];

    protected SettingsPage(string title)
    {
        Title = title;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
    }

    public event EventHandler? Changed;

    public abstract bool IsChanged { get; }

    internal IReadOnlyList<PageAction> Actions => _actions;

    public abstract void Keep();

    public abstract void Revert();

    public static string DefaultNote(string value) => $"(default: {value})";

    public static Label Dimmed(Label label)
    {
        label.GettingAttributeForRole += (_, e) =>
        {
            if (e.Role != VisualRole.Normal) return;
            e.Result = label.GetAttributeForRole(VisualRole.ReadOnly);
            e.Handled = true;
        };
        return label;
    }

    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    // Its key works only while this page is shown, so it never takes focus.
    protected Button ActionButton(string id, string label, Key key, Action run)
    {
        var button = AppButton.Secondary($"{KeyChord.Display(key)} {label}");
        button.CanFocus = false;
        button.Accepting += (_, e) =>
        {
            e.Handled = true;
            run();
        };
        _actions.Add(new PageAction(id, label, key, run));
        return button;
    }
}
