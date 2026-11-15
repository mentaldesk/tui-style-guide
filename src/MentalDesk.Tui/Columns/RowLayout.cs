namespace MentalDesk.Tui.Columns;

// One description of a list's columns, laid out for a width: a ListView's rows or a ColumnTable.
public sealed class RowLayout<T>(params IReadOnlyList<Column<T>> columns)
{
    public const int Gap = 2;

    public IReadOnlyList<Column<T>> Columns { get; } = columns;

    // A list whose rows explain themselves leaves the header out, and its widths don't count it.
    public bool HasHeader { get; init; } = true;

    // The name shrinks to its minimum, then paths, then the right-hand columns drop, last first.
    public ColumnFit<T> Fit(IEnumerable<T> rows, int width)
    {
        var all = rows.ToList();
        var natural = Columns.Select((column, i) => Natural(column, all)).ToArray();
        var kept = Enumerable.Range(0, Columns.Count).ToList();
        while (true)
        {
            var widths = kept.ToDictionary(i => i, i => natural[i]);
            var over = Total(widths) - width;
            foreach (var i in Shrinking(kept))
            {
                if (over <= 0) break;
                var floor = Math.Min(natural[i], Columns[i].MinWidth);
                var by = Math.Min(over, natural[i] - floor);
                widths[i] -= by;
                over -= by;
            }
            if (over <= 0 || kept.Count == 1)
                return new ColumnFit<T>(this, Place(kept, widths, width), width);
            kept.Remove(Droppable(kept));
        }
    }

    private int Natural(Column<T> column, List<T> rows)
    {
        if (column.Shrinks && column.Width is { } fixedWidth) return fixedWidth;
        var widths = rows.Select(row => Truncate.Width(column.Value(row)));
        if (HasHeader) widths = widths.Append(Truncate.Width(column.Header));
        return Math.Max(widths.DefaultIfEmpty(0).Max(), column.Width ?? 0);
    }

    private static int Total(Dictionary<int, int> widths) => widths.Values.Sum() + Gap * (widths.Count - 1);

    private IEnumerable<int> Shrinking(List<int> kept) =>
        kept.Where(i => Columns[i].Kind == ColumnKind.Name)
            .Concat(kept.Where(i => Columns[i].Kind == ColumnKind.Path).Reverse());

    private int Droppable(List<int> kept) =>
        kept.LastOrDefault(i => Columns[i].Kind != ColumnKind.Name, kept[^1]);

    // A lone column that still doesn't fit is cut to the width.
    private List<PlacedColumn<T>> Place(List<int> kept, Dictionary<int, int> widths, int width)
    {
        var placed = new List<PlacedColumn<T>>();
        var x = 0;
        foreach (var i in kept)
        {
            var w = kept.Count == 1 ? Math.Max(0, Math.Min(widths[i], width)) : widths[i];
            placed.Add(new PlacedColumn<T>(Columns[i], i, x, w));
            x += w + Gap;
        }
        return placed;
    }
}

public sealed record PlacedColumn<T>(Column<T> Column, int Index, int X, int Width)
{
    public string Cell(string text) => Truncate.Pad(Column.Fit(text, Width), Width, Column.AlignsRight);
}

public sealed class ColumnFit<T>
{
    internal ColumnFit(RowLayout<T> layout, IReadOnlyList<PlacedColumn<T>> placed, int width)
    {
        Layout = layout;
        Placed = placed;
        Width = width;
    }

    public RowLayout<T> Layout { get; }

    // The width it was fitted to.
    public int Width { get; }

    public IReadOnlyList<PlacedColumn<T>> Placed { get; }

    // A list of one column has no header.
    public bool ShowsHeader => Layout.HasHeader && Placed.Count > 1;

    public string? Header => ShowsHeader ? Format([.. Layout.Columns.Select(column => column.Header)]) : null;

    public string Row(T row) => Format([.. Layout.Columns.Select(column => column.Value(row))]);

    // One cell per column of the layout, dropped ones included; the last column doesn't stretch.
    public string Format(IReadOnlyList<string> cells) =>
        string.Join(new string(' ', RowLayout<T>.Gap), Placed.Select(placed => placed.Cell(cells[placed.Index]))).TrimEnd();
}
