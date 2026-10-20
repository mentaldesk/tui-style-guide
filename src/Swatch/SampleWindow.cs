using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Paths;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui;
using MentalDesk.Tui.Theming;
using System.Collections.ObjectModel;
using Terminal.Gui.Configuration;

namespace Swatch;

// The painted preview, live: every state here is Terminal.Gui's own.
internal sealed class SampleWindow : AppWindow
{
    public const string Open = "sample.open";
    public const string OpenFolder = "sample.openFolder";
    public const string SaveCopy = "sample.saveCopy";
    public const string SaveFile = "sample.save";
    public const string SaveTheme = "sample.saveTheme";
    public const string Settings = "sample.settings";
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
    public static readonly FocusRegion LogRegion = new("Log", new CommandScope("Log"));

    public static readonly AppScreen FilesScreen = new("Files", 'F');
    public static readonly AppScreen LogScreen = new("Log", 'L');

    public static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, CtrlEnter.Key);

    private readonly TreeView _files = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly SampleEditor _editor = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly ListView _log = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly TreeNode _src;
    private object? _load;
    private StatusMessage? _failure;

    public SampleWindow(AppShell shell, string theme, TimeSpan? loadTime = null) : base(Register(shell))
    {
        Shell = shell;
        LoadTime = loadTime ?? TimeSpan.FromSeconds(1.5);
        Files = new LoadStateView(_files, "Loading files…");
        Files.ReloadFailed += (_, message) =>
            _failure = shell.ShowNews($"{message}, so these are the files from the last load", Severity.Error);
        var filesPane = Panes.Create("Files", Files, x: 0, width: 24);
        filesPane.SchemeName = SchemeNames.Sidebar;
        var editorPane = Panes.Create("Editor", _editor, x: Pos.Right(filesPane), width: Dim.Fill());
        var logPane = Panes.Create("Log", _log, x: 0, width: Dim.Fill());
        _log.SetSource(new ObservableCollection<string>(["Opened the sample", "Listed the files in src"]));
        var filesScreen = Screen(filesPane, editorPane);
        var logScreen = Screen(logPane);
        Body.Add(filesScreen, logScreen);

        var todo = new TreeNode { Text = "todo.md" };
        _src = new TreeNode { Text = "src", Children = [new TreeNode { Text = "notes.md" }, todo] };
        _files.DrawLine += (_, e) => IconField.Prepend(e, shell.Icons.Glyph(e.Model == _src ? SwatchIcons.Folder : SwatchIcons.File));
        shell.Icons.Changed += OnIconsChanged;
        _files.ColorGetter = node => node == todo && _files.GetScheme() is { } scheme
            ? scheme with { Normal = scheme.Disabled, Active = scheme.Disabled }
            : null;

        Panes.Track(shell, FilesRegion, filesPane, _files);
        Panes.Track(shell, EditorRegion, editorPane, _editor);
        Panes.Track(shell, LogRegion, logPane, _log);
        shell.AddScreen(FilesScreen, filesScreen, EditorRegion).AddScreen(LogScreen, logScreen, LogRegion);
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
        if (disposing)
        {
            if (_load is not null) App?.RemoveTimeout(_load);
            Shell.Icons.Changed -= OnIconsChanged;
        }
        base.Dispose(disposing);
    }

    private static View Screen(params View[] panes)
    {
        var screen = new View { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        screen.Add(panes);
        return screen;
    }

    private void OnIconsChanged(object? sender, EventArgs e) => _files.SetNeedsDraw();

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
                if (_failure is not null) Shell.StatusBar.Withdraw(_failure);
                _failure = null;
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
            .Register(Open, "Open…", () => PickPath(shell, "Open", PathKind.File, "opened"))
            .Register(OpenFolder, "Open folder…", () => PickPath(shell, "Open folder", PathKind.Folder, "opened"))
            .Register(SaveCopy, "Save copy as…", () => PickPath(shell, "Save copy as", PathKind.NewFile, "saved a copy as"))
            .Register(SaveFile, "Save", () => shell.ShowMessage("A demo: this would have saved the file"))
            .Register(SaveTheme, "Save theme as…", () => SaveAs(shell))
            .Register(Settings, "Settings…", () => shell.ShowMessage("A demo: this would have opened the settings"))
            .Register(DeleteTheme, "Delete theme…", () => Delete(shell), FilesRegion.Scope)
            .Register(Export, "Export", () => { }, isEnabled: () => false)
            .Register(Back, "Back to Swatch", () => shell.App.RequestStop());
        foreach (var theme in Themes.Names)
            shell.Commands.Register(ThemeCommand(theme), $"Theme: {theme}", () => shell.StatusBar.State = shell.ApplyTheme(theme));
        shell.Keys
            .Bind("Ctrl+O", Open)
            .Bind("Ctrl+S", SaveFile)
            .Bind("Delete", DeleteTheme)
            .Bind("Esc", Back);
        shell.UseMenu(
            new MenuSpec("_File", [
                new MenuEntry(Open, "Open…"),
                new MenuEntry(OpenFolder, "Open folder…"),
                new MenuEntry(SaveFile, "Save"),
                new MenuEntry(SaveTheme, "Save as…"),
                new MenuEntry(SaveCopy, "Save copy as…"),
                new MenuEntry(Settings, "Settings…"),
                new MenuEntry(DeleteTheme, "Delete…"),
                null,
                new MenuEntry(Export, "Export"),
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
            shell.GoMenu(shell.GoTo(FilesScreen), shell.GoTo(LogScreen)),
            new MenuSpec("_Theme", [.. Themes.Names.Select(theme =>
                new MenuEntry(ThemeCommand(theme), theme, () => Themes.Current == theme))]),
            new MenuSpec("_Help", [
                new MenuEntry(ShellCommands.ShowCommands, "Show all _commands"),
                new MenuEntry(ShellCommands.ShowMessages, "Messages…"),
                new MenuEntry(Back, "_Back to Swatch"),
            ]));
        return shell;
    }

    public static string ThemeCommand(string theme) => $"sample.theme.{theme}";

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

    private static void PickPath(AppShell shell, string title, PathKind kind, string verb)
    {
        using var picker = new PathPicker(title, kind, Environment.CurrentDirectory);
        picker.Run(shell);
        if (picker.PickedPath is { } path)
            shell.ShowMessage($"A demo: this would have {verb} {path}");
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
