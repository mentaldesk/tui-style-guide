using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Keys;

public static class CtrlEnter
{
    public static Key Key => Key.Enter.WithCtrl;

    private const KeyCode WindowsCtrlLineFeed = KeyCode.CtrlMask | (KeyCode)0x0A;

    // The Win32 console sends Ctrl+Enter as Ctrl+LineFeed; elsewhere that's a different key.
    public static Key FromWindows(Key key, bool isWindows) =>
        isWindows && key.KeyCode == WindowsCtrlLineFeed ? Key : key;

    // Without the kitty protocol a terminal sends Ctrl+Enter as plain Enter.
    public static bool IsReported(IApplication app, bool isWindows) =>
        isWindows || app.Driver?.KittyKeyboardCapabilities is { IsSupported: true, Flags: not KittyKeyboardFlags.None };
}
