using MentalDesk.Tui.Theming;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drivers;
using Point = System.Drawing.Point;

namespace MentalDesk.Tui.Fields;

/// <summary>A field's caret, painted as a block over the character at the insertion point in the theme's Cursor scheme, with the terminal cursor hidden.</summary>
internal sealed class PaintedCaret
{
    private readonly View _field;
    private IApplication? _app;
    private Point? _drawnAt;

    public PaintedCaret(View field)
    {
        _field = field;
        Hide();
        field.HasFocusChanged += (_, focus) => Follow(focus.NewValue);
        field.Disposing += (_, _) => Follow(false);
    }

    private Attribute Block()
    {
        var colours = SchemeManager.TryGetScheme(SchemeNames.Cursor, out var cursor)
            ? cursor.Normal
            : _field.GetAttributeForRole(VisualRole.Editable);
        return new Attribute(colours.Background, colours.Foreground);
    }

    /// <summary>Call last in the field's draw, once the framework has placed its cursor.</summary>
    /// <param name="unplaced">Where the caret goes in the viewport when the framework found no cell for it.</param>
    public void Paint(Point? unplaced = null)
    {
        Hide();
        _drawnAt = _field.Cursor.Position;
        var placed = _drawnAt ?? (unplaced is { } cell ? _field.ViewportToScreen(cell) : null);
        if (!_field.HasFocus || placed is not { } screen || _field.App?.Driver?.Contents is not { } contents) return;
        var at = _field.ScreenToViewport(screen);
        if (at.X < 0 || at.Y < 0 || at.X >= _field.Viewport.Width || at.Y >= _field.Viewport.Height
            || screen.X < 0 || screen.Y < 0 || screen.Y >= contents.GetLength(0) || screen.X >= contents.GetLength(1)) return;
        var grapheme = contents[screen.Y, screen.X].Grapheme;
        _field.SetAttribute(Block());
        _field.AddStr(at.X, at.Y, string.IsNullOrEmpty(grapheme) ? " " : grapheme);
        _field.SetAttribute(_field.GetAttributeForRole(VisualRole.Normal));
    }

    private void Hide()
    {
        if (_field.Cursor.Style != CursorStyle.Hidden)
            _field.Cursor = _field.Cursor with { Style = CursorStyle.Hidden };
    }

    private void Follow(bool focused)
    {
        if (_app is not null) _app.Iteration -= OnIteration;
        _app = focused ? _field.App : null;
        if (_app is not null) _app.Iteration += OnIteration;
        _field.SetNeedsDraw();
    }

    // The framework moves its own cursor without a redraw, so a painted one has to ask for one.
    private void OnIteration(object? sender, EventArgs<IApplication?> e)
    {
        if (_field.Cursor.Position != _drawnAt) _field.SetNeedsDraw();
    }
}
