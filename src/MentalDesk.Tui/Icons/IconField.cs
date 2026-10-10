using Terminal.Gui.Text;

namespace MentalDesk.Tui.Icons;

public static class IconField
{
    public const int Width = 2;

    public static string Pad(string glyph) => glyph + new string(' ', Math.Max(0, Width - glyph.GetColumns()));

    // The row's hot colour over the row's own background, so a selected row still reads as selected.
    public static Attribute Colour(Scheme scheme, Attribute row)
    {
        if (row == scheme.Disabled) return row;
        var hot = row.Background == scheme.Focus.Background ? scheme.HotFocus
            : row.Background == scheme.Active.Background ? scheme.HotActive
            : scheme.HotNormal;
        return new Attribute(hot.Foreground, row.Background, row.Style);
    }

    public static void Draw(View view, int x, int y, string glyph, Scheme scheme, Attribute row)
    {
        view.Move(x, y);
        view.SetAttribute(Colour(scheme, row));
        view.AddStr(glyph);
        view.SetAttribute(row);
        view.AddStr(new string(' ', Math.Max(0, Width - glyph.GetColumns())));
    }

    // At draw time, so the tree's type-to-jump still matches the bare name.
    public static bool Prepend<T>(DrawTreeViewLineEventArgs<T> e, string glyph) where T : class
    {
        // Negative when scrolled sideways past the start of the text.
        var at = e.IndexOfModelText;
        if (e.Cells is not { } cells || at < 0 || at >= cells.Count) return false;

        var row = cells[at].Attribute ?? default;
        var icon = e.Tree?.GetScheme() is { } scheme ? Colour(scheme, row) : row;
        List<Cell> field = [new Cell { Grapheme = glyph, Attribute = icon }];
        for (var column = glyph.GetColumns(); column < Width; column++)
            field.Add(new Cell { Grapheme = " ", Attribute = row });
        cells.InsertRange(at, field);
        return true;
    }
}
