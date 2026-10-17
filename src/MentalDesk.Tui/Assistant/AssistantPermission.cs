using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Assistant;

// Before the agent runs a command, edits a file or uses any other tool that isn't a read. Deny has focus.
public static class AssistantPermission
{
    public static readonly ConfirmAction AllowOnce = new("Allow once", ButtonKind.Primary, CtrlEnter.Key);

    private const int LineWidth = 64;

    public static string TitleFor(PermissionRequest request, string agent) =>
        $"{agent} wants to {(request.IsCommand ? "run a command" : request.IsEdit ? "edit a file" : "use a tool")}";

    public static ConfirmDialog Dialog(PermissionRequest request, string agent) =>
        new(TitleFor(request, agent), Lines(request.Command ?? request.Title), [AllowOnce], cancel: "Deny");

    public static bool Ask(AppShell shell, PermissionRequest request, string agent)
    {
        using var dialog = Dialog(request, agent);
        dialog.Run(shell);
        return dialog.Chosen == AllowOnce;
    }

    // A long command breaks into rows rather than widening the dialog past the screen.
    private static List<string> Lines(string text)
    {
        var lines = new List<string>();
        foreach (var line in text.Split('\n'))
            for (var at = 0; at < Math.Max(line.Length, 1); at += LineWidth)
                lines.Add(line.Substring(at, Math.Min(LineWidth, line.Length - at)));
        return lines;
    }
}
