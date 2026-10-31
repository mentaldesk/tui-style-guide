using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;

namespace Swatch;

internal static class ThemeConfirms
{
    public static readonly ConfirmAction Delete = new("Delete", ButtonKind.Danger, CtrlEnter.Key);
    public static readonly ConfirmAction Remove = new("Remove", ButtonKind.Primary, CtrlEnter.Key);

    public static ConfirmDialog DeleteTheme(string theme) =>
        new("Delete theme", [$"Delete \"{theme}\"?", "This can't be undone."], [Delete]);

    public static ConfirmDialog RemoveTheme(string theme) =>
        new($"Remove {theme}?", ["Swatch stops listing this theme.", "To get it back: Theme › Restore removed."], [Remove]);

    public static ConfirmDialog CloseTheme(string theme) =>
        UnsavedPrompt.Create([new UnsavedItem($"\"{theme}\"", "theme", () => { })], "closing");
}
