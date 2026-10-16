namespace MentalDesk.Tui.Chrome;

// Gives each title its own letter: a letter it marks itself, else its first letter, a later word's, then any.
public static class MenuLetters
{
    public static string[] Pick(IReadOnlyList<string> titles)
    {
        var picked = titles.ToArray();
        var taken = picked.Select(Marked).OfType<char>().Select(char.ToLowerInvariant).ToHashSet();
        foreach (var candidates in new Func<string, IEnumerable<int>>[] { FirstLetter, LaterWords, AnyLetter })
        {
            for (var i = 0; i < picked.Length; i++)
            {
                var title = picked[i];
                if (Marked(title) is not null) continue;
                var at = candidates(title).FirstOrDefault(at => taken.Add(char.ToLowerInvariant(title[at])), -1);
                if (at >= 0)
                    picked[i] = title.Insert(at, "_");
            }
        }
        return picked;
    }

    private static char? Marked(string title) =>
        title.IndexOf('_') is var at and >= 0 && at + 1 < title.Length ? title[at + 1] : null;

    private static IEnumerable<int> AnyLetter(string title) =>
        Enumerable.Range(0, title.Length).Where(at => char.IsLetterOrDigit(title[at]));

    private static IEnumerable<int> FirstLetter(string title) => AnyLetter(title).Take(1);

    private static IEnumerable<int> LaterWords(string title) =>
        AnyLetter(title).Skip(1).Where(at => !char.IsLetterOrDigit(title[at - 1]));
}
