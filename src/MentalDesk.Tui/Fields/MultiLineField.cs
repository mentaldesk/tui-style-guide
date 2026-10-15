using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.Rendering;

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

    // Option+←/→ on a Mac: Alt+←/→ from iTerm2 and Ghostty, Esc b/Esc f (Alt+B/Alt+F) from Terminal.app.
    private static readonly (Key Key, Command Command)[] MacWordKeys =
    [
        (Key.CursorLeft.WithAlt, Command.WordLeft), (Key.CursorRight.WithAlt, Command.WordRight),
        (Key.CursorLeft.WithAlt.WithShift, Command.WordLeftExtend), (Key.CursorRight.WithAlt.WithShift, Command.WordRightExtend),
        (Key.B.WithAlt, Command.WordLeft), (Key.F.WithAlt, Command.WordRight),
        (Key.B.WithAlt.WithShift, Command.WordLeftExtend), (Key.F.WithAlt.WithShift, Command.WordRightExtend),
    ];

    internal static readonly Key DeleteToLineStart = Key.Backspace.WithCtrl.WithShift;

    private readonly FocusBorder _border;
    private string? _copyFailure;

    public MultiLineField()
    {
        BorderStyle = LineStyle.Single;
        WordWrap = true;
        ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        foreach (var key in EditorOnlyKeys)
            KeyBindings.Remove(key);
        foreach (var (key, command) in MacWordKeys)
            KeyBindings.ReplaceCommands(key, command);
        KeyBindings.Add(DeleteToLineStart, Command.CutToStartOfLine);
        AddCommand(Command.LeftStart, () => MoveTo(VisibleRow().Start, extend: false));
        AddCommand(Command.RightEnd, () => MoveTo(VisibleRow().End, extend: false));
        AddCommand(Command.LeftStartExtend, () => MoveTo(VisibleRow().Start, extend: true));
        AddCommand(Command.RightEndExtend, () => MoveTo(VisibleRow().End, extend: true));
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

    // A row ends before the space it wraps at: the offset after that space is drawn on the next row.
    private (int Start, int End) VisibleRow()
    {
        var line = Document!.GetLineByOffset(CaretOffset);
        var text = Document.GetText(line);
        var segments = WordWrapStrategy.ComputeSegments(text, Viewport.Width > 0 ? Viewport.Width : 80, IndentationSize);
        var index = segments.Count - 1;
        while (index > 0 && segments[index].StartOffset > CaretOffset - line.Offset)
            index--;
        var row = segments[index];
        var end = row.StartOffset + row.Length;
        if (index < segments.Count - 1)
        {
            var trimmed = row.StartOffset + text[row.StartOffset..end].TrimEnd().Length;
            end = trimmed < end ? trimmed : end - 1;
        }
        return (line.Offset + row.StartOffset, line.Offset + Math.Max(row.StartOffset, end));
    }

    private bool MoveTo(int offset, bool extend)
    {
        if (!extend)
        {
            ClearSelection();
            CaretOffset = offset;
            return true;
        }
        // Editor keeps its selection anchor private, so step there with its own extending moves.
        var forward = offset > CaretOffset;
        while (forward ? CaretOffset < offset : CaretOffset > offset)
        {
            var before = CaretOffset;
            InvokeCommand(forward ? Command.RightExtend : Command.LeftExtend);
            if (CaretOffset == before) break;
        }
        return true;
    }

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
