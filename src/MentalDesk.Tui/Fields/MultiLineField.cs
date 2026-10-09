using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Fields;

/// <summary>Multi-line free text on Terminal.Gui.Editor: wraps, and leaves Tab, Esc and Ctrl+Enter to the dialog.</summary>
public sealed class MultiLineField : Terminal.Gui.Editor.Editor
{
    // Indent, folding, find and replace, and extra carets: none of them belong in a comment box.
    private static readonly Key[] EditorOnlyKeys =
    [
        Key.Tab, Key.Tab.WithShift, Key.M.WithCtrl, Key.F.WithCtrl, Key.H.WithCtrl, Key.F3, Key.F3.WithShift,
        Key.CursorUp.WithCtrl.WithAlt, Key.CursorDown.WithCtrl.WithAlt,
    ];

    private readonly FocusBorder _border;
    private string? _copyFailure;

    public MultiLineField()
    {
        BorderStyle = LineStyle.Single;
        WordWrap = true;
        ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        foreach (var key in EditorOnlyKeys)
            KeyBindings.Remove(key);
        AddCommand(Command.Copy, () =>
        {
            CopySelection();
            return true;
        });
        AddCommand(Command.Cut, () =>
        {
            if (!ReadOnly && CopySelection())
                ReplaceSelection(string.Empty);
            return true;
        });
        _border = new FocusBorder(this);
    }

    public string Placeholder { get; set; } = string.Empty;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);
        if (Text.Length == 0 && Placeholder.Length > 0)
        {
            SetAttribute(GetAttributeForRole(VisualRole.ReadOnly));
            Move(0, 0);
            AddStr(Placeholder.Length > Viewport.Width ? Placeholder[..Viewport.Width] : Placeholder);
            SetAttribute(GetAttributeForRole(VisualRole.Normal));
        }
        if (Cursor.Position is not null && !OverwriteMode)
            Cursor = Cursor with { Style = CursorStyle.SteadyBar };
        return true;
    }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
        _border.Show(newHasFocus);
    }

    protected override bool OnKeyDownNotHandled(Key key) =>
        !(key.IsCtrl && key.IsShift && key.IsAlt) && base.OnKeyDownNotHandled(key);

    protected override bool OnMouseEvent(Mouse mouse) =>
        mouse.Flags.HasFlag(MouseFlags.Ctrl) || mouse.Flags.HasFlag(MouseFlags.Alt) || base.OnMouseEvent(mouse);

    // A failure goes in the dialog's message block rather than the status bar, and the selection stays.
    private bool CopySelection()
    {
        if (!HasSelection) return false;
        var terminal = App?.Clipboard as TerminalClipboard;
        var outcome = terminal?.Write(SelectedText) ?? VerifiedClipboard.Write(App?.Clipboard, SelectedText);
        var dialog = Dialog();
        if (outcome is CopyOutcome.Failed && dialog is not null)
        {
            _copyFailure = outcome.Message;
            dialog.ShowAlert(outcome.Message, outcome.Severity);
            return false;
        }
        if (dialog is not null && _copyFailure is not null && dialog.Alert.Message == _copyFailure)
            dialog.ShowAlert(string.Empty, Severity.Info);
        _copyFailure = null;
        terminal?.Announce(outcome);
        return outcome is CopyOutcome.Copied;
    }

    private AppDialog? Dialog()
    {
        for (var view = SuperView; view is not null; view = view.SuperView)
            if (view is AppDialog dialog) return dialog;
        return null;
    }
}
