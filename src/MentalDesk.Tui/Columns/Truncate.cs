using Terminal.Gui.Text;

namespace MentalDesk.Tui.Columns;

// Widths are terminal columns, so a wide character counts as two.
public static class Truncate
{
    public const string Ellipsis = "…";

    public static int Width(string text) => text.GetColumns();

    // Too narrow for a character and the ellipsis, it clips instead of showing a lone ….
    public static string End(string text, int width)
    {
        if (width <= 0) return string.Empty;
        if (Width(text) <= width) return text;
        return width == 1 ? Take(GraphemeHelper.GetGraphemes(text), 1) : Take(GraphemeHelper.GetGraphemes(text), width - 1) + Ellipsis;
    }

    // A path keeps its end, where the file or folder name is.
    public static string Start(string text, int width)
    {
        if (width <= 0) return string.Empty;
        if (Width(text) <= width) return text;
        var reversed = GraphemeHelper.GetGraphemes(text).Reverse();
        return width == 1 ? Take(reversed, 1, reverse: true) : Ellipsis + Take(reversed, width - 1, reverse: true);
    }

    public static string Pad(string text, int width, bool right)
    {
        var padding = new string(' ', Math.Max(0, width - Width(text)));
        return right ? padding + text : text + padding;
    }

    private static string Take(IEnumerable<string> graphemes, int width, bool reverse = false)
    {
        var taken = new List<string>();
        var used = 0;
        foreach (var grapheme in graphemes)
        {
            var w = Width(grapheme);
            if (used + w > width) break;
            taken.Add(grapheme);
            used += w;
        }
        if (reverse) taken.Reverse();
        return string.Concat(taken);
    }
}
