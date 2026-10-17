namespace MentalDesk.Tui.Finding;

public readonly record struct TextMatch(int Row, int Column, int Length)
{
    public int End => Column + Length;
}

public static class TextSearch
{
    public static IReadOnlyList<TextMatch> FindAll(IEnumerable<string> lines, string query)
    {
        var matches = new List<TextMatch>();
        if (query.Length == 0) return matches;
        var row = 0;
        foreach (var line in lines)
        {
            for (var at = line.IndexOf(query, StringComparison.OrdinalIgnoreCase); at >= 0;
                 at = line.IndexOf(query, at + query.Length, StringComparison.OrdinalIgnoreCase))
                matches.Add(new TextMatch(row, at, query.Length));
            row++;
        }
        return matches;
    }

    internal static int IndexAtOrAfter(IReadOnlyList<TextMatch> matches, (int Row, int Column) at)
    {
        for (var i = 0; i < matches.Count; i++)
            if (Compare(matches[i], at) >= 0) return i;
        return matches.Count > 0 ? 0 : -1;
    }

    internal static int IndexAfter(IReadOnlyList<TextMatch> matches, (int Row, int Column) at)
    {
        for (var i = 0; i < matches.Count; i++)
            if (Compare(matches[i], at) > 0) return i;
        return matches.Count > 0 ? 0 : -1;
    }

    internal static int IndexBefore(IReadOnlyList<TextMatch> matches, (int Row, int Column) at)
    {
        for (var i = matches.Count - 1; i >= 0; i--)
            if (Compare(matches[i], at) < 0) return i;
        return matches.Count - 1;
    }

    private static int Compare(TextMatch match, (int Row, int Column) at) =>
        match.Row != at.Row ? match.Row.CompareTo(at.Row) : match.Column.CompareTo(at.Column);
}
