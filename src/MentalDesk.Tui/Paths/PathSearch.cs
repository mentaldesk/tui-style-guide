namespace MentalDesk.Tui.Paths;

public sealed record PathEntry(string FullPath, string Relative, bool IsFolder)
{
    public string Name => System.IO.Path.GetFileName(FullPath);
}

public static class PathSearch
{
    public const int Cap = 10_000;

    public static readonly IReadOnlySet<string> Skipped =
        new HashSet<string>([".git", ".hg", ".svn", "node_modules", "bin", "obj"], StringComparer.OrdinalIgnoreCase);

    public static bool IsHidden(string name) => name.StartsWith('.');

    // Breadth first, so a capped search keeps the shallowest. Folders it can't read, and links to folders, aren't entered.
    public static IEnumerable<PathEntry> Below(string root, bool hidden, CancellationToken cancel = default)
    {
        var folders = new Queue<(string Path, string Relative)>();
        folders.Enqueue((root, string.Empty));
        while (folders.Count > 0)
        {
            cancel.ThrowIfCancellationRequested();
            var (folder, relative) = folders.Dequeue();
            if (!TryList(folder, out var entries)) continue;
            foreach (var info in entries)
            {
                if (Skipped.Contains(info.Name) || !hidden && IsHidden(info.Name)) continue;
                var isFolder = info is DirectoryInfo;
                var path = relative.Length == 0 ? info.Name : $"{relative}/{info.Name}";
                yield return new PathEntry(info.FullName, path, isFolder);
                if (isFolder && info.LinkTarget is null)
                    folders.Enqueue((info.FullName, path));
            }
        }
    }

    public static IReadOnlyList<FileSystemInfo> List(string folder) =>
        [.. new DirectoryInfo(folder).EnumerateFileSystemInfos()
            .OrderBy(info => info is DirectoryInfo ? 0 : 1)
            .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)];

    public static string? WhyUnreadable(Exception e) => e switch
    {
        UnauthorizedAccessException => "You don't have permission to read this folder",
        DirectoryNotFoundException => "This folder isn't there any more",
        IOException => e.Message,
        _ => null,
    };

    private static bool TryList(string folder, out IReadOnlyList<FileSystemInfo> entries)
    {
        try
        {
            entries = List(folder);
            return true;
        }
        catch (Exception e) when (WhyUnreadable(e) is not null)
        {
            entries = [];
            return false;
        }
    }
}
