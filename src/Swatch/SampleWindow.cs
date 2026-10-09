using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Configuration;

namespace Swatch;

// The painted preview, live: every state here is Terminal.Gui's own.
internal sealed class SampleWindow : AppWindow
{
    public const string SaveTheme = "sample.saveTheme";
    public const string DeleteTheme = "sample.deleteTheme";
    public const string Export = "sample.export";
    public const string Cut = "sample.cut";
    public const string Copy = "sample.copy";
    public const string Paste = "sample.paste";
    public const string Back = "sample.back";
    public const string ReloadFiles = "sample.reloadFiles";
    public const string LoadNoFiles = "sample.loadNoFiles";
    public const string LoadFailing = "sample.loadFailing";
    public const string CollapseFolders = "sample.collapseFolders";
    public const string ExpandFolders = "sample.expandFolders";

    public static readonly FocusRegion FilesRegion = new("Files", new CommandScope("Files"));
    public static readonly FocusRegion EditorRegion = new("Editor", new CommandScope("Editor"));

    public static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, Key.Enter);

    private readonly TreeView _files = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly SampleEditor _editor = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly TreeNode _src;
    private object? _load;

    public SampleWindow(AppShell shell, string theme, TimeSpan? loadTime = null) : base(Register(shell))
    {
        Shell = shell;
        LoadTime = loadTime ?? TimeSpan.FromSeconds(1.5);
        Files = new LoadStateView(_files, "Loading files…");
        Files.ReloadFailed += (_, message) =>
            shell.ShowMessage($"{message}, so these are the files from the last load", Severity.Error);
        var filesPane = Panes.Create("Files", Files, x: 0, width: 24);
        filesPane.SchemeName = SchemeNames.Sidebar;
        var editorPane = Panes.Create("Editor", _editor, x: Pos.Right(filesPane), width: Dim.Fill());
        Body.Add(filesPane, editorPane);

        var todo = new TreeNode { Text = "todo.md" };
        _src = new TreeNode { Text = "src", Children = [new TreeNode { Text = "notes.md" }, todo] };
        _files.ColorGetter = node => node == todo && _files.GetScheme() is { } scheme
            ? scheme with { Normal = scheme.Disabled, Active = scheme.Disabled }
            : null;

        Panes.Track(shell, FilesRegion, filesPane, _files);
        Panes.Track(shell, EditorRegion, editorPane, _editor);
        shell.Commands
            .Register(Cut, "Cut", () => _editor.Cut(), EditorRegion.Scope)
            .Register(Copy, "Copy", () => _editor.Copy(), EditorRegion.Scope)
            .Register(Paste, "Paste", () => _editor.Paste(), EditorRegion.Scope)
            .Register(ReloadFiles, "Reload files", () => Load(Outcome.Files))
            .Register(LoadNoFiles, "Load no files", () => Load(Outcome.Empty))
            .Register(LoadFailing, "Load files, failing", () => Load(Outcome.Failure))
            .Register(CollapseFolders, "Collapse folders", () => _files.Collapse(_src), isEnabled: () => Listed && _files.IsExpanded(_src))
            .Register(ExpandFolders, "Expand folders", () => _files.Expand(_src), isEnabled: () => Listed && !_files.IsExpanded(_src));
        shell.Keys.Bind("Ctrl+R", ReloadFiles);
        shell.StatusBar.State = theme;
        Initialized += (_, _) =>
        {
            shell.Focus.Focus(EditorRegion);
            Load(Outcome.Files);
        };
    }

    private enum Outcome
    {
        Files,

        Empty,

        Failure,
    }

    public AppShell Shell { get; }

    public LoadStateView Files { get; }

    public TreeView Tree => _files;

    public TimeSpan LoadTime { get; }

    private bool Listed => _files.Objects?.Contains(_src) == true;

    protected override void Dispose(bool disposing)
    {
        if (disposing && _load is not null) App?.RemoveTimeout(_load);
        base.Dispose(disposing);
    }

    private void Load(Outcome outcome)
    {
        if (_load is not null) App?.RemoveTimeout(_load);
        Files.ShowLoading();
        _load = App?.AddTimeout(LoadTime, () =>
        {
            _load = null;
            Finish(outcome);
            return false;
        });
    }

    private void Finish(Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Files:
                _files.ClearObjects();
                _files.AddObject(_src);
                _files.Expand(_src);
                _files.SelectedObject ??= _src.Children[0];
                Files.ShowContent();
                break;
            case Outcome.Empty:
                _files.ClearObjects();
                Files.ShowEmpty("No files", Step(ReloadFiles, "reload"));
                break;
            case Outcome.Failure:
                Files.ShowFailed("Couldn't list files", Step(ReloadFiles, "retry")!.Value);
                break;
        }
    }

    private (string, Action)? Step(string id, string verb) =>
        new Hint(id, verb).Resolve(Shell.Commands, Shell.Keys) is (var text, { } run) ? (text, run) : null;

    private static AppShell Register(AppShell shell)
    {
        shell.Commands
            .Register(SaveTheme, "Save theme…", () => SaveAs(shell))
            .Register(DeleteTheme, "Delete theme…", () => Delete(shell), FilesRegion.Scope)
            .Register(Export, "Export", () => { }, isEnabled: () => false)
            .Register(Back, "Back to Swatch", () => shell.App.RequestStop());
        foreach (var theme in Themes.Names)
            shell.Commands.Register(ThemeCommand(theme), $"Theme: {theme}", () => shell.StatusBar.State = shell.ApplyTheme(theme));
        shell.Keys
            .Bind("Ctrl+S", SaveTheme)
            .Bind("Delete", DeleteTheme)
            .Bind("Esc", Back);
        var hotLetters = new HashSet<char>();
        shell.UseMenu(
            new MenuSpec("_File", [
                new MenuEntry(SaveTheme, "_Save theme…"),
                new MenuEntry(DeleteTheme, "_Delete…"),
                null,
                new MenuEntry(Export, "_Export"),
            ]),
            new MenuSpec("_Edit", [
                new MenuEntry(Cut, "Cu_t"),
                new MenuEntry(Copy, "_Copy"),
                new MenuEntry(Paste, "_Paste"),
            ]),
            new MenuSpec("_View", [
                new MenuEntry(ReloadFiles, "_Reload files"),
                new MenuEntry(LoadNoFiles, "Load _no files"),
                new MenuEntry(LoadFailing, "Load files, _failing"),
                null,
                new MenuEntry(CollapseFolders, "_Collapse folders") { Opposite = new(ExpandFolders, "_Expand folders") },
            ]),
            new MenuSpec("_Theme", [.. Themes.Names.Select(theme =>
                new MenuEntry(ThemeCommand(theme), Hot(theme, hotLetters), () => Themes.Current == theme))]),
            new MenuSpec("_Help", [
                new MenuEntry(ShellCommands.ShowCommands, "Show all _commands"),
                new MenuEntry(Back, "_Back to Swatch"),
            ]));
        return shell;
    }

    public static string ThemeCommand(string theme) => $"sample.theme.{theme}";

    // Midnight and Modern Borland would otherwise share M.
    private static string Hot(string title, HashSet<char> taken)
    {
        var at = Enumerable.Range(0, title.Length)
            .Where(i => i == 0 || title[i - 1] == ' ')
            .FirstOrDefault(i => taken.Add(char.ToLowerInvariant(title[i])), -1);
        return at < 0 ? title : title.Insert(at, "_");
    }

    private static void SaveAs(AppShell shell)
    {
        using var dialog = new ConfirmDialog(
            "Save theme", [$"Save a copy of \"{Themes.Current}\" as"], [Save],
            field: new TextField { Text = "Solar" });
        dialog.Initialized += (_, _) => dialog.ShowAlert("Couldn't write themes.json", Severity.Error);
        dialog.Run(shell);
        if (dialog.Chosen is not null)
            shell.ShowMessage($"A demo: this would have saved \"{dialog.Field!.Text}\"");
    }

    private static void Delete(AppShell shell)
    {
        using var dialog = ThemeConfirms.DeleteTheme(Themes.Current);
        dialog.Run(shell);
        if (dialog.Chosen is not null)
            shell.ShowMessage($"A demo: this would have deleted \"{Themes.Current}\"");
    }

#pragma warning disable CS0618 // Obsolete in Terminal.Gui 2.5, but still the stock editor apps use.
    private sealed class SampleEditor : TextView
#pragma warning restore CS0618
    {
        public SampleEditor()
        {
            Text = "Hello, world\nread only\n⚠ changed on disk";
            TabKeyAddsTab = false;
        }

        protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
        {
            switch (idxRow)
            {
                case 1:
                    SetAttribute(GetAttributeForRole(VisualRole.ReadOnly));
                    break;
                case 2:
                    SetAttribute(SchemeManager.GetScheme(SchemeNames.Warning).Normal);
                    break;
                default:
                    base.OnDrawNormalColor(line, idxCol, idxRow);
                    break;
            }
        }
    }
}
