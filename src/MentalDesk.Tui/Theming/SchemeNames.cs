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

    // Not drawn with: its Normal foreground is the terminal cursor's colour.
    public const string Cursor = "Cursor";

    public static IReadOnlyList<string> All { get; } =
        [Base, Sidebar, Dialog, Menu, StatusBar, Accent, Error, Warning, Cursor];
}
