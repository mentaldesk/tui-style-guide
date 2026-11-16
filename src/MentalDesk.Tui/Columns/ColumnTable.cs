namespace MentalDesk.Tui.Columns;

// A TableView in the house look: a dimmed header in the rows' own columns, no lines, laid out by RowLayout.
public class ColumnTable<T> : TableView
{
    private IReadOnlyList<T> _rows;
    private int _fitted;

    public ColumnTable(RowLayout<T> layout, IReadOnlyList<T> rows)
    {
        RowLayout = layout;
        _rows = rows;
        Fit = layout.Fit(rows, 0);
        FullRowSelect = true;
        MaxCellWidth = int.MaxValue;
        Style.ShowHorizontalHeaderOverline = false;
        Style.ShowHorizontalHeaderUnderline = false;
        Style.ShowHorizontalBottomLine = false;
        Style.ShowVerticalCellLines = false;
        Style.ShowVerticalHeaderLines = false;
        Style.ShowVerticalCellLineForFirstColumn = false;
        Style.ShowVerticalCellLineForLastColumn = false;
        Style.ExpandLastColumn = false;
        Style.InvertSelectedCellFirstCharacter = false;
        for (var i = 0; i < layout.Columns.Count; i++)
        {
            var column = layout.Columns[i];
            var style = Style.GetOrCreateColumnStyle(i);
            style.Alignment = column.AlignsRight ? Alignment.End : Alignment.Start;
            style.MinAcceptableWidth = column.Shrinks ? column.MinWidth : Truncate.Width(column.Header);
            style.TruncationIndicator = Truncate.Ellipsis;
            var index = i;
            style.RepresentationGetter = value => CellText(index, (string)value);
            style.HeaderColorGetter = _ => DimHeader();
        }
        Table = new Source(this);
        Refit(0);
    }

    public RowLayout<T> RowLayout { get; }

    public ColumnFit<T> Fit { get; private set; }

    public IReadOnlyList<T> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            Refit(Viewport.Width);
            Update();
        }
    }

    public T? SelectedRow => Value?.SelectedCell is { Y: var y } && y >= 0 && y < _rows.Count ? _rows[y] : default;

    protected override void OnViewportChanged(DrawEventArgs e)
    {
        if (Viewport.Width != _fitted)
            Refit(Viewport.Width);
        base.OnViewportChanged(e);
    }

    // Laid out before it has a viewport, it fits itself to the width it's drawn at.
    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (Viewport.Width != _fitted)
        {
            Refit(Viewport.Width);
            RefreshContentSize();
        }
        return base.OnDrawingContent(context);
    }

    // TableView keeps one column after each cell for its separator; the space after the cell makes the second.
    private void Refit(int width)
    {
        _fitted = width;
        Fit = RowLayout.Fit(_rows, width);
        for (var i = 0; i < RowLayout.Columns.Count; i++)
        {
            var style = Style.GetOrCreateColumnStyle(i);
            var placed = Placed(i);
            style.Visible = placed is not null;
            style.MinWidth = style.MaxWidth = (placed?.Width ?? 0) + 1;
        }
        Style.ShowHeaders = Fit.ShowsHeader;
        SetNeedsDraw();
    }

    private PlacedColumn<T>? Placed(int index) => Fit.Placed.FirstOrDefault(placed => placed.Index == index);

    private string CellText(int index, string text) => Placed(index) is { } placed ? placed.Cell(text) + " " : string.Empty;

    private Scheme DimHeader()
    {
        var dim = GetAttributeForRole(VisualRole.Disabled);
        var normal = GetAttributeForRole(VisualRole.Normal);
        var header = new Attribute(dim.Foreground, normal.Background);
        return new Scheme(GetScheme()) { Normal = header, Focus = header };
    }

    private sealed class Source(ColumnTable<T> table) : ITableSource
    {
        public object this[int row, int col] => table.RowLayout.Columns[col].Value(table._rows[row]);

        public string[] ColumnNames => [.. table.RowLayout.Columns.Select((column, i) => table.CellText(i, column.Header))];

        public int Columns => table.RowLayout.Columns.Count;

        public int Rows => table._rows.Count;
    }
}
