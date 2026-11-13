using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.Rendering;
using Point = System.Drawing.Point;

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
    private readonly PaintedCaret _caret;
    private string? _copyFailure;
    private IApplication? _pasteSource;
    private (int Caret, int Width, int Length) _keptInView;

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
        KeyBindings.Add(Key.K.WithCtrl, Command.CutToEndOfLine);
        AddCommand(Command.CutToEndOfLine, CutToEndOfLine);
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
        _caret = new PaintedCaret(this);
        CaretChanged += (_, _) => SetNeedsDraw();
    }

    public string Placeholder { get; set; } = string.Empty;

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var caret = CaretRow();
        KeepInView(caret);
        base.OnDrawingContent(context);
        if (Text.Length == 0 && Placeholder.Length > 0)
        {
            SetAttribute(GetAttributeForRole(VisualRole.ReadOnly));
            foreach (var (segment, row) in Segments(Placeholder).Take(Viewport.Height).Select((segment, row) => (segment, row)))
            {
                var line = Placeholder.Substring(segment.StartOffset, segment.Length).TrimEnd();
                Move(0, row);
                AddStr(line.Length > Viewport.Width ? line[..Viewport.Width] : line);
            }
            SetAttribute(GetAttributeForRole(VisualRole.Normal));
        }
        _caret.Paint(caret.PastRow ? new Point(0, caret.Row - Viewport.Y) : null);
        return true;
    }

    protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView)
    {
        base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);
        _border.Show(newHasFocus);
        if (!newHasFocus || App is not { } app || app == _pasteSource) return;
        if (_pasteSource is not null) _pasteSource.Paste -= OnTerminalPaste;
        _pasteSource = app;
        app.Paste += OnTerminalPaste;
    }

    // Editor reads a bracketed paste from the clipboard rather than the terminal, so it's taken before Editor sees it.
    private void OnTerminalPaste(object? sender, PasteEventArgs e)
    {
        if (e.Handled || App?.Navigation?.GetFocused() != this) return;
        e.Handled = true;
        if (ReadOnly) return;
        var text = e.Text.ReplaceLineEndings("\n");
        using (Document!.RunUpdate())
        {
            if (HasSelection) ReplaceSelection(text);
            else Document.Insert(CaretOffset, text);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _pasteSource is not null) _pasteSource.Paste -= OnTerminalPaste;
        base.Dispose(disposing);
    }

    // Editor's deletes nothing when the clipboard refuses the text.
    private bool? CutToEndOfLine()
    {
        if (ReadOnly) return true;
        var line = Document!.GetLineByOffset(CaretOffset);
        var end = line.Offset + line.Length;
        var (start, length) = CaretOffset < end ? (CaretOffset, end - CaretOffset) : (end, line.DelimiterLength);
        if (length == 0) return true;
        App?.Clipboard?.TrySetClipboardData(Document.GetText(start, length));
        Document.Remove(start, length);
        return true;
    }

    private IReadOnlyList<WrapSegment> Segments(string text) =>
        WordWrapStrategy.ComputeSegments(text, Viewport.Width > 0 ? Viewport.Width : 80, IndentationSize);

    private static int SegmentAt(IReadOnlyList<WrapSegment> segments, int offset)
    {
        var index = segments.Count - 1;
        while (index > 0 && segments[index].StartOffset > offset)
            index--;
        return index;
    }

    // Editor has no cell for a caret past the end of a full row, so it goes where the next character will: the start of the row below.
    private (int Row, bool PastRow) CaretRow()
    {
        var line = Document!.GetLineByOffset(CaretOffset);
        var text = Document.GetText(line);
        var segments = Segments(text);
        var inLine = CaretOffset - line.Offset;
        var index = SegmentAt(segments, inLine);
        var row = Document.Lines.TakeWhile(other => other != line).Sum(other => Segments(Document.GetText(other)).Count) + index;
        var past = inLine > segments[index].StartOffset + segments[index].Length
            || (inLine == text.Length && Segments(text + "x").Count > segments.Count);
        return past ? (row + 1, true) : (row, false);
    }

    // Only when the caret, the text or the width changes, so the wheel can still scroll away from the caret.
    private void KeepInView((int Row, bool PastRow) caret)
    {
        var state = (CaretOffset, Viewport.Width, Document!.TextLength);
        if (!HasFocus || Viewport.Height == 0 || state == _keptInView) return;
        _keptInView = state;
        var top = Math.Clamp(Viewport.Y, Math.Max(0, caret.Row - Viewport.Height + 1), caret.Row);
        // That row can be past Editor's content: a row below the last, or a scroll bar has just narrowed the text.
        if (top + Viewport.Height > GetContentSize().Height) ViewportSettings |= ViewportSettingsFlags.AllowYPlusHeightGreaterThanContentHeight;
        else ViewportSettings &= ~ViewportSettingsFlags.AllowYPlusHeightGreaterThanContentHeight;
        if (top != Viewport.Y) Viewport = Viewport with { Y = top };
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
        var segments = Segments(text);
        var index = SegmentAt(segments, CaretOffset - line.Offset);
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
