namespace MentalDesk.Tui.Columns;

public enum ColumnKind
{
    // Names the row; it gives way first, cut at its end.
    Name,

    // Gives way after the name, cut at its start so the last folder stays.
    Path,

    // A key, an id or a word: never cut, so its column drops instead.
    Text,

    // Right-aligned and never cut: a cut number reads as a different number.
    Number,
}

public sealed record Column<T>(string Header, ColumnKind Kind, Func<T, string> Value)
{
    // How far a Name or Path shrinks before columns drop.
    public int MinWidth { get; init; }

    // The width it takes when there's room, whatever its rows hold; a Name or Path is cut to it.
    public int? Width { get; init; }

    public bool AlignsRight => Kind == ColumnKind.Number;

    public bool Shrinks => Kind is ColumnKind.Name or ColumnKind.Path;

    public static Column<T> Name(string header, Func<T, string> value, int minWidth = 8) =>
        new(header, ColumnKind.Name, value) { MinWidth = minWidth };

    public static Column<T> Path(string header, Func<T, string> value, int minWidth = 12) =>
        new(header, ColumnKind.Path, value) { MinWidth = minWidth };

    public static Column<T> Text(string header, Func<T, string> value) => new(header, ColumnKind.Text, value);

    public static Column<T> Number(string header, Func<T, string> value) => new(header, ColumnKind.Number, value);

    public string Fit(string text, int width) => Kind == ColumnKind.Path ? Truncate.Start(text, width) : Truncate.End(text, width);
}
