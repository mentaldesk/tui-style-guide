namespace MentalDesk.Tui.Theming;

public static class SchemeNames
{
    public const string Base = "Base";
    public const string Sidebar = "Sidebar";
    public const string Dialog = "Dialog";
    public const string Menu = "Menu";
    public const string StatusBar = "StatusBar";
    public const string Accent = "Accent";
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string ButtonPrimary = "ButtonPrimary";
    public const string ButtonDanger = "ButtonDanger";
    public const string ButtonSecondary = "ButtonSecondary";

    // Normal's foreground is the caret's colour, and its background the colour of the character under it.
    public const string Cursor = "Cursor";

    public static IReadOnlyList<string> All { get; } =
        [Base, Sidebar, Dialog, Menu, StatusBar, Accent, Error, Warning, ButtonPrimary, ButtonDanger, ButtonSecondary, Cursor];
}
