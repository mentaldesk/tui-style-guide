using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Dialogs;

// Save throws to say why it failed; without one, the item can't be kept from the prompt.
public sealed record UnsavedItem(string Name, string Kind, Action? Save = null);

public static class UnsavedPrompt
{
    public const string Title = "Unsaved changes";

    private const int Named = 4;

    public static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, CtrlEnter.Key);
    public static readonly ConfirmAction SaveAll = new("Save all", ButtonKind.Primary, CtrlEnter.Key);
    public static readonly ConfirmAction Discard = new("Discard", ButtonKind.Danger, Key.D);
    public static readonly ConfirmAction DiscardOnly = new("Discard", ButtonKind.Danger, CtrlEnter.Key);

    // Leaving is what the user is doing: "quitting", "closing".
    public static ConfirmDialog Create(IReadOnlyList<UnsavedItem> items, string leaving)
    {
        if (items.Count == 0) throw new ArgumentException("Ask only when something would be lost.", nameof(items));
        var them = items.Count == 1 ? "it" : "them";
        List<string> lines = items.Count == 1
            ? [$"{items[0].Name} has unsaved changes."]
            : [$"{items.Count} {Plural(items)} have unsaved changes:", Names(items)];
        var unsavable = items.Where(item => item.Save is null).ToList();
        if (unsavable.Count == 0)
            return new ConfirmDialog(Title, [.. lines, $"Save {them} before {leaving}?"], [items.Count == 1 ? Save : SaveAll, Discard]);
        if (items.Count > 1)
            lines.Add($"{Names(unsavable)} can't be saved from here.");
        lines.Add($"{char.ToUpperInvariant(leaving[0])}{leaving[1..]} loses {(items.Count == 1 ? "it" : "them all")}.");
        return new ConfirmDialog(Title, lines, [DiscardOnly]);
    }

    // True when it's fine to go: nothing to lose, all of it saved, or discarded.
    public static bool Leave(AppShell shell, IReadOnlyList<UnsavedItem> items, string leaving)
    {
        if (items.Count == 0) return true;
        using var prompt = Create(items, leaving);
        prompt.Run(shell);
        if (prompt.Chosen is null) return false;
        if (prompt.Chosen.Kind == ButtonKind.Danger) return true;
        foreach (var item in items)
        {
            try
            {
                item.Save!();
            }
            catch (Exception e)
            {
                shell.ShowMessage($"Couldn't save {item.Name}: {e.Message}", Severity.Error);
                return false;
            }
        }
        return true;
    }

    public static string Names(IReadOnlyList<UnsavedItem> items)
    {
        var names = string.Join(", ", items.Take(Named).Select(item => item.Name));
        return items.Count > Named ? $"{names} and {items.Count - Named} more" : names;
    }

    private static string Plural(IReadOnlyList<UnsavedItem> items) =>
        items.Select(item => item.Kind).Distinct().Count() == 1 ? $"{items[0].Kind}s" : "items";
}
