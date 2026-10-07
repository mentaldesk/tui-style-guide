using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;

namespace Swatch;

internal sealed class SwatchWindow : AppWindow
{
    public static readonly FocusRegion ThemesRegion = new("Themes", new CommandScope("Themes"));
    public static readonly FocusRegion RolesRegion = new("Roles", new CommandScope("Roles"));
    public static readonly FocusRegion PreviewRegion = new("Preview", new CommandScope("Preview"));

    private readonly AppShell _shell;
    private readonly FrameView _themesPane;
    private readonly FrameView _rolesPane;
    private readonly FrameView _previewPane;
    private readonly TreeView _themes = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly ListView _roles = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly PreviewView _preview = new();

    public SwatchWindow(AppShell shell) : base(Register(shell))
    {
        _shell = shell;
        _themesPane = Pane("Themes", _themes, x: 0, width: 24);
        _themesPane.SchemeName = SchemeNames.Sidebar;
        _rolesPane = Pane("Roles", _roles, x: Pos.Right(_themesPane), width: 50);
        _previewPane = Pane("Preview", _preview, x: Pos.Right(_rolesPane), width: Dim.Fill());
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

        Track(ThemesRegion, _themesPane, _themes);
        Track(RolesRegion, _rolesPane, _roles);
        Track(PreviewRegion, _previewPane, _preview);
        shell.HintsFor = Hints;
        shell.StatusBar.State = Themes.Current;
        Show(new Selection(Themes.Current, SchemeNames.Base));
        Initialized += (_, _) => shell.Focus.Focus(ThemesRegion);
    }

    public Selection Showing { get; private set; } = new(Themes.Default, SchemeNames.Base);

    public IEnumerable<string> ThemeNames => _themes.Objects.Select(node => node.Text);

    private static AppShell Register(AppShell shell)
    {
        shell.Commands
            .Register(SwatchCommands.GoToThemes, "Go to themes", () => shell.Focus.Focus(ThemesRegion))
            .Register(SwatchCommands.GoToRoles, "Go to roles", () => shell.Focus.Focus(RolesRegion))
            .Register(SwatchCommands.GoToPreview, "Go to preview", () => shell.Focus.Focus(PreviewRegion))
            .Register(SwatchCommands.UseTheme, "Use this theme for Swatch", () => UseTheme(shell),
                isEnabled: () => shell.App.TopRunnableView is SwatchWindow window && window.Showing.Theme != Themes.Current)
            .Register(SwatchCommands.DeleteTheme, "Delete this theme", () => DeleteTheme(shell));
        shell.Keys
            .Bind("Ctrl+G T", SwatchCommands.GoToThemes)
            .Bind("Ctrl+G R", SwatchCommands.GoToRoles)
            .Bind("Ctrl+G P", SwatchCommands.GoToPreview)
            .Bind("Ctrl+T U", SwatchCommands.UseTheme)
            .Bind("Ctrl+T D", SwatchCommands.DeleteTheme);
        shell.UseMenu(
            new MenuSpec("_File", [ShellCommands.Quit]),
            new MenuSpec("_Go", [
                new MenuEntry(SwatchCommands.GoToThemes, "_Themes"),
                new MenuEntry(SwatchCommands.GoToRoles, "_Roles"),
                new MenuEntry(SwatchCommands.GoToPreview, "_Preview"),
            ]),
            new MenuSpec("_Theme", [
                new MenuEntry(SwatchCommands.UseTheme, "_Use for Swatch"),
                new MenuEntry(SwatchCommands.DeleteTheme, "_Delete…"),
            ]),
            new MenuSpec("_Help", [
                new MenuEntry(ShellCommands.ShowCommands, "Show all _commands"),
                new MenuEntry(ShellCommands.ShowMenu, "Open the _menu"),
                null,
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

    private static void DeleteTheme(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        using var dialog = new DeleteThemeDialog(window.Showing.Theme);
        dialog.Run(shell);
        if (dialog.Confirmed)
            shell.ShowMessage("Swatch's themes are built in, so nothing was deleted");
    }

    private static IEnumerable<Hint> Hints(FocusRegion? region)
    {
        if (region == ThemesRegion)
            yield return new Hint(SwatchCommands.UseTheme, "use theme");
        yield return new Hint(ShellCommands.ShowCommands, "commands");
        yield return new Hint(ShellCommands.ShowMenu, "menu");
        yield return new Hint(ShellCommands.Quit, "quit");
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
        _rolesPane.Title = $"{selection.Theme} › {selection.Scheme}";
        _previewPane.Title = $"Preview › {selection.Theme}";
        _preview.Show(selection.Theme);
    }

    private void Track(FocusRegion region, FrameView pane, View content)
    {
        var border = new FocusBorder(pane);
        _shell.Focus.Register(region, () => content.SetFocus(), view => Within(view as View, pane));
        _shell.Focus.RegionChanged += (_, focused) => border.Show(focused == region);
    }

    private static bool Within(View? view, View pane)
    {
        for (; view is not null; view = view.SuperView)
            if (view == pane) return true;
        return false;
    }

    private static FrameView Pane(string title, View content, Pos x, Dim width)
    {
        var pane = new FrameView
        {
            Title = title, X = x, Width = width, Height = Dim.Fill(), CanFocus = true, BorderStyle = LineStyle.Single,
            TabStop = TabBehavior.TabStop,
        };
        pane.Add(content);
        return pane;
    }

    internal sealed record Selection(string Theme, string Scheme);
}
