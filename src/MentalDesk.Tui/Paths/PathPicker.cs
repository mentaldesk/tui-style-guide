using System.Diagnostics;
using System.Globalization;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Filtering;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Paths;

public enum PathKind
{
    File,

    Folder,

    NewFile,
}

public enum PathRowKind
{
    Up,

    Folder,

    File,

    NewFile,
}

public sealed record PathRow(PathRowKind Kind, string FullPath, string Label);

public sealed class PathPicker : PickerDialog<PathRow>
{
    public const string UpId = "path.up";
    public const string UseFolderId = "path.useFolder";
    public const string SaveHereId = "path.saveHere";
    public const string Searching = "Searching…";

    public static readonly ConfirmAction ReplaceAction = new("Replace", ButtonKind.Danger, CtrlEnter.Key);

    private static readonly TimeSpan BatchEvery = TimeSpan.FromMilliseconds(100);

    private readonly PathKind _kind;
    private readonly Label _where;
    private readonly Label _state;
    private readonly bool _ready;
    private Search? _search;

    // useFolder names the Ctrl+Enter button that picks the folder you're in; a file picker has none without it.
    public PathPicker(
        string title, PathKind kind, string start, string? home = null, int searchCap = PathSearch.Cap, string? useFolder = null)
        : base(title, width: 76, contentRows: 18, [], row => row.Label, verb: "Open", display: null, linesAbove: 1,
            kind == PathKind.File ? ButtonKind.Primary : ButtonKind.Secondary)
    {
        if (kind == PathKind.NewFile && useFolder is not null)
            throw new ArgumentException("A new file's picker has no folder to use.", nameof(useFolder));
        _kind = kind;
        Home = Path.TrimEndingDirectorySeparator(home ?? HomePath.Default);
        SearchCap = searchCap;
        _where = new Label { X = 1, Y = 0, Width = Dim.Fill(1), Enabled = false };
        _state = new Label { X = 1, Y = 2, Width = Dim.Fill(1), Enabled = false };
        Add(_where, _state);

        Commands.Register(UpId, "Up a folder", () => GoUp(), isEnabled: () => FilterField.Text.Length == 0 && Parent(Folder) is not null);
        Keys.Bind("Backspace", UpId);
        switch (kind)
        {
            case PathKind.File when useFolder is null:
                AddButtonRow(17, PickButton, CancelButton);
                break;
            case PathKind.File:
                AddButtonRow(17, PickButton, UseFolderButton(useFolder, ButtonKind.Secondary), CancelButton);
                break;
            case PathKind.Folder:
                AddButtonRow(17, PickButton, UseFolderButton(useFolder ?? "Use this folder", ButtonKind.Primary), CancelButton);
                break;
            case PathKind.NewFile:
                Commands.Register(SaveHereId, "Save here", SaveHere, isEnabled: () => Named() is not null);
                Keys.Bind([CtrlEnter.Key], SaveHereId);
                SaveHereButton = CommandButton(ButtonKind.Primary, SaveHereId);
                AddButtonRow(17, SaveHereButton, CancelButton);
                FilterField.TextChanged += (_, _) => SaveHereButton.Enabled = Named() is not null;
                SaveHereButton.Enabled = false;
                break;
        }
        _ready = true;
        GoTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(start)));
    }

    public string Home { get; }

    public int SearchCap { get; }

    public string Folder { get; private set; } = string.Empty;

    public string Where => _where.Text;

    public string SearchStatus => _state.Text;

    public bool IsSearching => _search is { Done: false };

    public Button? SaveHereButton { get; }

    public string? PickedPath => Chosen is null ? null : HomePath.Show(Chosen.FullPath, Home);

    public Func<string, bool>? ConfirmReplace { get; set; }

    protected override IReadOnlyList<PathRow> Match(string query, IReadOnlyList<PathRow> rows)
    {
        if (!_ready || query.Length == 0) return rows;
        if (HomePath.IsTyped(query)) return Typed(query);

        var hidden = PathSearch.IsHidden(query);
        if (_search is not { } search || search.Folder != Folder || search.Hidden != hidden)
            search = Start(hidden);
        var bySegment = query.Contains('/');
        List<PathRow> matched = [.. search.Found
            .Where(entry => entry.IsFolder || _kind != PathKind.Folder)
            .Select(entry => (Entry: entry, Score: ListFilter.Score(query, bySegment ? entry.Relative : entry.Name)))
            .Where(match => match.Score is not null)
            .OrderBy(match => match.Score)
            .Select(match => match.Entry.IsFolder
                ? new PathRow(PathRowKind.Folder, match.Entry.FullPath, match.Entry.Relative + "/")
                : new PathRow(PathRowKind.File, match.Entry.FullPath, match.Entry.Relative))];
        if (Named() is { } named && !File.Exists(named) && !Directory.Exists(named))
            matched.Insert(0, new PathRow(PathRowKind.NewFile, named, $"New file: {query}"));
        return matched;
    }

    protected override bool IsDim(PathRow row) => row.Kind == PathRowKind.NewFile;

    protected override void PickSelected()
    {
        if (Selected is not { } row) return;
        switch (row.Kind)
        {
            case PathRowKind.Up:
                GoUp();
                break;
            case PathRowKind.Folder:
                GoTo(row.FullPath);
                break;
            case PathRowKind.File when _kind == PathKind.NewFile:
                if (Replacing(row.FullPath)) Done(row);
                break;
            case PathRowKind.File:
            case PathRowKind.NewFile:
                Done(row);
                break;
        }
    }

    protected override void OnClosed()
    {
        _search?.Cancel();
        base.OnClosed();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _search?.Cancel();
        base.Dispose(disposing);
    }

    private Button UseFolderButton(string label, ButtonKind kind)
    {
        Commands.Register(UseFolderId, label, () => Done(new PathRow(PathRowKind.Folder, Folder, Folder)));
        Keys.Bind([CtrlEnter.Key], UseFolderId);
        return CommandButton(kind, UseFolderId);
    }

    private static string? Parent(string folder) => Path.GetDirectoryName(folder);

    private static PathRow FolderRow(string path) =>
        new(PathRowKind.Folder, path, Path.GetFileName(path) + "/");

    private void GoUp()
    {
        if (Parent(Folder) is { } parent) GoTo(parent, select: FolderRow(Folder));
    }

    private void GoTo(string folder, PathRow? select = null)
    {
        _search?.Cancel();
        _search = null;
        Folder = folder;
        _where.Text = HomePath.Show(folder, Home);
        _state.Text = string.Empty;
        List<PathRow> rows = [];
        if (Parent(folder) is { } parent) rows.Add(new PathRow(PathRowKind.Up, parent, "../"));
        string? note = null;
        try
        {
            foreach (var info in PathSearch.List(folder))
            {
                if (PathSearch.IsHidden(info.Name)) continue;
                if (info is DirectoryInfo)
                    rows.Add(FolderRow(info.FullName));
                else if (_kind != PathKind.Folder)
                    rows.Add(new PathRow(PathRowKind.File, info.FullName, info.Name));
            }
        }
        catch (Exception e) when (PathSearch.WhyUnreadable(e) is { } why)
        {
            note = why;
        }
        FilterField.Text = string.Empty;
        ShowNote(note);
        Replace(rows, select);
    }

    private IReadOnlyList<PathRow> Typed(string query)
    {
        var path = Path.GetFullPath(HomePath.Expand(query, Home));
        if (Directory.Exists(path))
            return [new PathRow(PathRowKind.Folder, Path.TrimEndingDirectorySeparator(path), query)];
        if (File.Exists(path) && _kind != PathKind.Folder)
            return [new PathRow(PathRowKind.File, path, query)];
        if (_kind == PathKind.NewFile && Named() is { } named && !File.Exists(named))
            return [new PathRow(PathRowKind.NewFile, named, $"New file: {query}")];
        return [];
    }

    private string? Named()
    {
        if (_kind != PathKind.NewFile || FilterField.Text is not { Length: > 0 } text) return null;
        var path = Path.GetFullPath(HomePath.IsTyped(text) ? HomePath.Expand(text, Home) : Path.Join(Folder, text));
        return Directory.Exists(Parent(path)) && !Directory.Exists(path) && !Path.EndsInDirectorySeparator(text) ? path : null;
    }

    private void SaveHere()
    {
        if (Named() is not { } path) return;
        if (!File.Exists(path) || Replacing(path))
            Done(new PathRow(PathRowKind.NewFile, path, path));
    }

    private bool Replacing(string path)
    {
        var shown = HomePath.Show(path, Home);
        if (ConfirmReplace is { } ask) return ask(shown);
        if (RunningIn is not { } shell) return false;
        using var confirm = new ConfirmDialog(
            "Replace file", [$"{shown} is already there.", "Replace it? This can't be undone."], [ReplaceAction]);
        confirm.Run(shell);
        return confirm.Chosen is not null;
    }

    private void Done(PathRow row)
    {
        _search?.Cancel();
        Pick(row);
    }

    private Search Start(bool hidden)
    {
        _search?.Cancel();
        var search = new Search(Folder, hidden);
        _search = search;
        _state.Text = Searching;
        Action<Action> post = App is { } app ? app.Invoke : action => action();
        void Changed()
        {
            if (_search != search) return;
            _state.Text = search.Done ? search.Capped ? $"First {SearchCap.ToString("N0", CultureInfo.InvariantCulture)} entries" : string.Empty : Searching;
            Refresh();
        }
        if (App is null)
            search.Run(SearchCap, post, Changed);
        else
            _ = Task.Run(() => search.Run(SearchCap, post, Changed));
        return search;
    }

    // Found, Done and Capped are only touched on the UI thread, through post.
    private sealed class Search(string folder, bool hidden)
    {
        private readonly CancellationTokenSource _cancel = new();

        public string Folder => folder;

        public bool Hidden => hidden;

        public List<PathEntry> Found { get; } = [];

        public bool Done { get; private set; }

        public bool Capped { get; private set; }

        public void Cancel() => _cancel.Cancel();

        public void Run(int cap, Action<Action> post, Action changed)
        {
            var token = _cancel.Token;
            var batch = new List<PathEntry>();
            var since = Stopwatch.StartNew();
            var capped = false;
            var count = 0;
            try
            {
                foreach (var entry in PathSearch.Below(folder, hidden, token))
                {
                    if (count == cap)
                    {
                        capped = true;
                        break;
                    }
                    batch.Add(entry);
                    count++;
                    if (since.Elapsed < BatchEvery) continue;
                    Post(final: false);
                    since.Restart();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            Post(final: true);

            void Post(bool final)
            {
                PathEntry[] found = [.. batch];
                batch.Clear();
                post(() =>
                {
                    if (token.IsCancellationRequested) return;
                    Found.AddRange(found);
                    if (final)
                    {
                        Done = true;
                        Capped = capped;
                    }
                    changed();
                });
            }
        }
    }
}
