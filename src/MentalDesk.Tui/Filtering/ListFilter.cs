namespace MentalDesk.Tui.Filtering;

public static class ListFilter
{
    public static IReadOnlyList<T> Apply<T>(string query, IReadOnlyList<T> rows, Func<T, string> text)
    {
        if (query.Length == 0) return rows;
        return [.. rows
            .Select(row => (Row: row, Score: Score(query, text(row))))
            .Where(match => match.Score is not null)
            .OrderBy(match => match.Score)
            .Select(match => match.Row)];
    }

    // CamelHumps scores are small, so a contains-only match ranks after all of them.
    public static int? Score(string query, string text)
    {
        if (query.Contains('/')) return CamelHumps.ScorePath(query, text.Split('/'));
        return CamelHumps.Score(query, text)
            ?? (text.Contains(query, StringComparison.OrdinalIgnoreCase) ? int.MaxValue : null);
    }
}
