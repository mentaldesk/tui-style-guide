using MentalDesk.Tui.Dialogs;

namespace Swatch;

internal static class ThemeConfirms
{
    public static readonly ConfirmAction Delete = new("Delete", ButtonKind.Danger, Key.Delete);
    public static readonly ConfirmAction Remove = new("Remove", ButtonKind.Primary, Key.Enter);
    public static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, Key.S);
    public static readonly ConfirmAction DontSave = new("Don't save", ButtonKind.Danger, Key.D);

    public static ConfirmDialog DeleteTheme(string theme) =>
        new("Delete theme", [$"Delete \"{theme}\"?", "This can't be undone."], [Delete]);

    public static ConfirmDialog RemoveTheme(string theme) =>
        new($"Remove {theme}?", ["Swatch stops listing this theme.", "To get it back: Theme › Restore removed."], [Remove]);

    public static ConfirmDialog CloseTheme(string theme) =>
        new("Unsaved changes", [$"Save changes to \"{theme}\" before closing?", "Your changes are lost if you don't save."],
            [Save, DontSave]);
}
