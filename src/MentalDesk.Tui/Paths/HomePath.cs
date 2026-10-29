namespace MentalDesk.Tui.Paths;

// A path as the user reads and types it: `~` for home.
public static class HomePath
{
    public static string Default => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Show(string path, string home)
    {
        home = Path.TrimEndingDirectorySeparator(home);
        if (home.Length == 0) return path;
        if (string.Equals(path, home, PathComparison)) return "~";
        return path.StartsWith(home + Path.DirectorySeparatorChar, PathComparison)
            ? "~" + path[home.Length..]
            : path;
    }

    public static bool IsTyped(string text) => IsHome(text) || Path.IsPathRooted(text);

    public static string Expand(string text, string home) =>
        text == "~" ? home : IsHome(text) ? Path.Join(home, text[2..]) : text;

    private static bool IsHome(string text) =>
        text == "~" || text.StartsWith("~/", StringComparison.Ordinal)
        || text.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
