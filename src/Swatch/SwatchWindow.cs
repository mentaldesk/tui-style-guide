using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;

namespace Swatch;

internal sealed class SwatchWindow : AppWindow
{
    public static readonly FocusRegion ThemesRegion = new("Themes", new CommandScope("Themes"));
    public static readonly FocusRegion RolesRegion = new("Roles", new CommandScope("Roles"));
    public static readonly FocusRegion PreviewRegion = new("Preview", new CommandScope("Preview"));

    private readonly FrameView _themesPane;
    private readonly FrameView _rolesPane;
    private readonly FrameView _previewPane;
    private readonly TreeView _themes = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly ListView _roles = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly PreviewView _preview = new();

    public SwatchWindow(AppShell shell) : base(Register(shell))
    {
        _themesPane = Panes.Create("Themes", _themes, x: 0, width: 24);
        _themesPane.SchemeName = SchemeNames.Sidebar;
        _rolesPane = Panes.Create("Roles", _roles, x: Pos.Right(_themesPane), width: 50);
        _previewPane = Panes.Create("Preview", _preview, x: Pos.Right(_rolesPane), width: Dim.Fill());
        Body.Add(_themesPane, _rolesPane, _previewPane);

        foreach (var theme in Themes.Names)
        {
            var node = new TreeNode { Text = theme, Tag = new Selection(theme, SchemeNames.Base) };
            foreach (var scheme in SchemeNames.All)
                node.Children.Add(new TreeNode { Text = scheme, Tag = new Selection(theme, scheme) });
            _themes.AddObject(node);
        }
        _themes.SelectionChanged += (_, e) =>
        {
            if (e.NewValue?.Tag is Selection selection) Show(selection);
        };
        _themes.SelectedObject = _themes.Objects?.FirstOrDefault();
        _themes.Expand();

        Panes.Track(shell, ThemesRegion, _themesPane, _themes);
        Panes.Track(shell, RolesRegion, _rolesPane, _roles);
        Panes.Track(shell, PreviewRegion, _previewPane, _preview);
        _preview.MouseEvent += (_, mouse) =>
        {
            if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked)) return;
            mouse.Handled = true;
            shell.Commands.Execute(SwatchCommands.TryTheme);
        };
        shell.StatusBar.State = Themes.Current;
        Show(new Selection(Themes.Current, SchemeNames.Base));
        Initialized += (_, _) => shell.Focus.Focus(ThemesRegion);
    }

    public View Preview => _preview;

    public Selection Showing { get; private set; } = new(Themes.Default, SchemeNames.Base);

    public IEnumerable<string> ThemeNames => _themes.Objects?.Select(node => node.Text) ?? [];

    private static AppShell Register(AppShell shell)
    {
        shell.Commands
            .Register(SwatchCommands.GoToThemes, "Go to themes", () => shell.Focus.Focus(ThemesRegion))
            .Register(SwatchCommands.GoToRoles, "Go to roles", () => shell.Focus.Focus(RolesRegion))
            .Register(SwatchCommands.GoToPreview, "Go to preview", () => shell.Focus.Focus(PreviewRegion))
            .Register(SwatchCommands.GoToScheme, "Go to scheme…", () => GoToScheme(shell))
            .Register(SwatchCommands.UseTheme, "Use this theme for Swatch", () => UseTheme(shell),
                isEnabled: () => shell.App.TopRunnableView is SwatchWindow window && window.Showing.Theme != Themes.Current)
            .Register(SwatchCommands.TryTheme, "Try this theme", () => TryTheme(shell))
            .Register(SwatchCommands.DeleteTheme, "Delete this theme", () => DeleteTheme(shell))
            .Register(SwatchCommands.RemoveTheme, "Remove this theme", () => RemoveTheme(shell))
            .Register(SwatchCommands.CloseTheme, "Close this theme", () => CloseTheme(shell))
            .Register(SwatchCommands.ShowLoadingStates, "Show loading states", () => ShowLoadingStates(shell));
        shell.Keys
            .Bind("Ctrl+G T", SwatchCommands.GoToThemes)
            .Bind("Ctrl+G R", SwatchCommands.GoToRoles)
            .Bind("Ctrl+G P", SwatchCommands.GoToPreview)
            .Bind("Ctrl+G S", SwatchCommands.GoToScheme)
            .Bind("Ctrl+T U", SwatchCommands.UseTheme, ThemesRegion.Scope)
            .Bind("Ctrl+T T", SwatchCommands.TryTheme)
            .Bind("Enter", SwatchCommands.TryTheme, PreviewRegion.Scope)
            .Bind("Ctrl+T D", SwatchCommands.DeleteTheme, ThemesRegion.Scope)
            .Bind("Ctrl+T R", SwatchCommands.RemoveTheme, ThemesRegion.Scope)
            .Bind("Ctrl+T C", SwatchCommands.CloseTheme, ThemesRegion.Scope);
        shell.UseMenu(
            new MenuSpec("_File", [ShellCommands.Quit]),
            new MenuSpec("_Go", [
                new MenuEntry(SwatchCommands.GoToThemes, "_Themes"),
                new MenuEntry(SwatchCommands.GoToRoles, "_Roles"),
                new MenuEntry(SwatchCommands.GoToPreview, "_Preview"),
                new MenuEntry(SwatchCommands.GoToScheme, "_Scheme…"),
            ]),
            new MenuSpec("_Theme", [
                new MenuEntry(SwatchCommands.TryTheme, "_Try it"),
                new MenuEntry(SwatchCommands.UseTheme, "_Use for Swatch"),
                new MenuEntry(SwatchCommands.DeleteTheme, "_Delete…"),
                new MenuEntry(SwatchCommands.RemoveTheme, "_Remove…"),
                new MenuEntry(SwatchCommands.CloseTheme, "_Close…"),
            ]),
            new MenuSpec("_Help", [
                new MenuEntry(ShellCommands.ShowKeys, "Show _keys"),
                new MenuEntry(ShellCommands.ShowCommands, "Show all _commands"),
                new MenuEntry(ShellCommands.ShowMenu, "Open the _menu"),
                null,
                new MenuEntry(SwatchCommands.ShowLoadingStates, "_Loading states…"),
                new MenuEntry(ShellCommands.ShowDiagnostics, "Show _diagnostics"),
            ]));
        return shell;
    }

    private static void UseTheme(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        var applied = shell.ApplyTheme(window.Showing.Theme);
        shell.StatusBar.State = applied;
        shell.ShowMessage($"Swatch is now in {applied}");
    }

    private static void GoToScheme(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        Selection[] pairs = [.. Themes.Names.SelectMany(theme => SchemeNames.All.Select(scheme => new Selection(theme, scheme)))];
        using var picker = new PickerDialog<Selection>("Go to scheme", width: 76, contentRows: 18, pairs, pair => pair.Display, verb: "go");
        picker.Run(shell);
        if (picker.Chosen is not { } chosen) return;
        var theme = window._themes.Objects?.FirstOrDefault(node => node.Text == chosen.Theme);
        if (theme?.Children.FirstOrDefault(node => Equals(((TreeNode)node).Tag, chosen)) is not { } scheme) return;
        window._themes.Expand(theme);
        window._themes.SelectedObject = scheme;
        window._themes.EnsureVisible(scheme);
        shell.Focus.Focus(ThemesRegion);
    }

    private static void TryTheme(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        using var sampleShell = new AppShell(shell.App, shell.Cursor);
        using var sample = new SampleWindow(sampleShell, window.Showing.Theme);
        shell.RunNested(sampleShell, sample, window.Showing.Theme);
    }

    private static void DeleteTheme(AppShell shell) =>
        Confirm(shell, ThemeConfirms.DeleteTheme, _ => "Swatch's themes are built in, so nothing was deleted");

    private static void RemoveTheme(AppShell shell) =>
        Confirm(shell, ThemeConfirms.RemoveTheme, _ => "A demo: Swatch's themes are built in, so nothing was removed");

    private static void CloseTheme(AppShell shell) =>
        Confirm(shell, ThemeConfirms.CloseTheme, chosen => chosen == ThemeConfirms.Save
            ? "A demo: Swatch has no unsaved changes, so nothing was saved"
            : "A demo: Swatch has no unsaved changes, so nothing was lost");

    private static void ShowLoadingStates(AppShell shell)
    {
        using var dialog = new LoadingStatesDialog(shell);
        dialog.Run(shell);
    }

    private static void Confirm(AppShell shell, Func<string, ConfirmDialog> open, Func<ConfirmAction, string> said)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        using var dialog = open(window.Showing.Theme);
        dialog.Run(shell);
        if (dialog.Chosen is { } chosen)
            shell.ShowMessage(said(chosen));
    }

    private void Show(Selection selection)
    {
        Showing = selection;
        var schemes = Themes.SchemesOf(selection.Theme);
        if (schemes.TryGetValue(selection.Scheme, out var scheme))
        {
            _roles.Source = new RoleSource(scheme);
            _roles.SelectedItem = 0;
        }
        _rolesPane.Title = selection.Display;
        _previewPane.Title = $"Preview › {selection.Theme}";
        _preview.Show(selection.Theme);
    }

    internal sealed record Selection(string Theme, string Scheme)
    {
        public string Display => $"{Theme} › {Scheme}";
    }
}
