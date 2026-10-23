using MentalDesk.Tui.Assistant;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;

namespace Swatch;

internal sealed class SwatchWindow : AppWindow
{
    public const string GuideUrl = "https://github.com/mentaldesk/tui-style-guide";

    public static readonly FocusRegion ThemesRegion = new("Themes", new CommandScope("Themes"));
    public static readonly FocusRegion RolesRegion = new("Roles", new CommandScope("Roles"));
    public static readonly FocusRegion PreviewRegion = new("Preview", new CommandScope("Preview"));
    public static readonly FocusRegion AssistantRegion = new("Assistant", new CommandScope("Assistant"));

    private const int RolesWidth = 50;
    private const int AssistantWidth = 48;

    private readonly FrameView _themesPane;
    private readonly FrameView _rolesPane;
    private readonly FrameView _previewPane;
    private readonly TreeView _themes = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly ListView _roles = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly PreviewView _preview;
    private readonly AssistantController _assistantController;
    private bool _showHexValues;

    public SwatchWindow(AppShell shell, IAgentLauncher? agents = null) : base(Register(shell))
    {
        Settings = new SwatchSettings(shell.Settings);
        Themes.Apply(Settings.Theme);
        _showHexValues = Settings.ShowHexValues;
        _preview = new PreviewView(shell.Icons);
        _themesPane = Panes.Create("Themes", _themes, x: 0, width: 24);
        _themesPane.SchemeName = SchemeNames.Sidebar;
        _rolesPane = Panes.Create("Roles", _roles, x: Pos.Right(_themesPane), width: RolesWidth);
        _previewPane = Panes.Create("Preview", _preview, x: Pos.Right(_rolesPane), width: Dim.Fill());
        _assistantController = new AssistantController(agents ?? new DemoAgent(), sessions: null, shell.App.Invoke, new Conversation());
        _assistantController.Configure(DemoAgent.Spec);
        _assistantController.OpenFolder(Environment.CurrentDirectory);
        Assistant = new AssistantPane(_assistantController)
        {
            X = Pos.AnchorEnd(AssistantWidth), Width = AssistantWidth, Height = Dim.Fill(), Visible = false, Place = "Swatch",
        };
        Body.Add(_themesPane, _rolesPane, _previewPane, Assistant);

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
        Assistant.Attach(shell, AssistantRegion);
        _preview.MouseEvent += (_, mouse) =>
        {
            if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked)) return;
            mouse.Handled = true;
            shell.Commands.Execute(SwatchCommands.TryTheme);
        };
        shell.StatusBar.State = Themes.Current;
        Show(new Selection(Themes.Current, SchemeNames.Base));
        KeyOverrides = KeyOverrides.InSection(shell.Settings, SwatchSettings.Section, shell.Commands, shell.Keys);
        KeyOverrides.Load();
        Initialized += (_, _) => shell.Focus.Focus(ThemesRegion);
    }

    public View Preview => _preview;

    public AssistantPane Assistant { get; }

    public Selection Showing { get; private set; } = new(Themes.Default, SchemeNames.Base);

    public IEnumerable<string> ThemeNames => _themes.Objects?.Select(node => node.Text) ?? [];

    public SwatchSettings Settings { get; }

    public KeyOverrides KeyOverrides { get; }

    public bool ShowHexValues
    {
        get => _showHexValues;
        set
        {
            if (value == _showHexValues) return;
            _showHexValues = value;
            var selected = _roles.SelectedItem;
            ShowRoles();
            _roles.SelectedItem = selected;
        }
    }

    private static AppShell Register(AppShell shell)
    {
        shell.Commands
            .Register(SwatchCommands.GoToScheme, "Go to scheme…", () => GoToScheme(shell))
            .Register(SwatchCommands.UseTheme, "Use this theme for Swatch", () => UseTheme(shell), ThemesRegion.Scope,
                isEnabled: () => shell.App.TopRunnableView is SwatchWindow window && window.Showing.Theme != Themes.Current)
            .Register(SwatchCommands.TryTheme, "Try this theme", () => TryTheme(shell))
            .Register(SwatchCommands.DeleteTheme, "Delete this theme", () => DeleteTheme(shell), ThemesRegion.Scope)
            .Register(SwatchCommands.RemoveTheme, "Remove this theme", () => RemoveTheme(shell), ThemesRegion.Scope)
            .Register(SwatchCommands.CloseTheme, "Close this theme", () => CloseTheme(shell), ThemesRegion.Scope)
            .Register(SwatchCommands.CopyColours, "Copy colours", () => CopyColours(shell), RolesRegion.Scope)
            .Register(SwatchCommands.ShowLoadingStates, "Show loading states", () => ShowLoadingStates(shell))
            .Register(SwatchCommands.PickIssue, "Pick an issue", () => PickIssue(shell))
            .Register(SwatchCommands.WriteComment, "Write a comment", () => WriteComment(shell))
            .Register(SwatchCommands.ReadLog, "Read a log", () => ReadLog(shell))
            .Register(SwatchCommands.ShowSettings, "Settings…", () => ShowSettings(shell))
            .Register(SwatchCommands.OpenGuide, "Open the guide on GitHub", () => shell.OpenLink(GuideUrl))
            .Register(SwatchCommands.ToggleAssistant, "Assistant", () => ToggleAssistant(shell));
        shell.Keys
            .Bind("Ctrl+G S", SwatchCommands.GoToScheme)
            .Bind("Ctrl+T U", SwatchCommands.UseTheme)
            .Bind("Ctrl+T T", SwatchCommands.TryTheme)
            .Bind("Enter", SwatchCommands.TryTheme, PreviewRegion.Scope)
            .Bind("Ctrl+T D", SwatchCommands.DeleteTheme)
            .Bind("Ctrl+T R", SwatchCommands.RemoveTheme)
            .Bind("Ctrl+T C", SwatchCommands.CloseTheme)
            .Bind("Ctrl+C", SwatchCommands.CopyColours);
        shell.UseMenu(
            new MenuSpec("_File", [new MenuEntry(SwatchCommands.ShowSettings, "_Settings…"), null, ShellCommands.Quit]),
            new MenuSpec("_Edit", [new MenuEntry(SwatchCommands.CopyColours, "_Copy colours"), new MenuEntry(ShellCommands.Find, "_Find")]),
            shell.GoMenu(new MenuEntry(SwatchCommands.GoToScheme, "_Scheme…")),
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
                new MenuEntry(ShellCommands.ShowMessages, "Messages…"),
                null,
                new MenuEntry(SwatchCommands.ShowLoadingStates, "_Loading states…"),
                new MenuEntry(SwatchCommands.ToggleAssistant, "_Assistant"),
                new MenuEntry(SwatchCommands.PickIssue, "_Picking an issue…"),
                new MenuEntry(SwatchCommands.WriteComment, "_Writing a comment…"),
                new MenuEntry(SwatchCommands.ReadLog, "_Reading a log…"),
                new MenuEntry(SwatchCommands.OpenGuide, "Open the _guide on GitHub"),
                new MenuEntry(ShellCommands.ShowDiagnostics, "Show _diagnostics"),
            ]));
        return shell;
    }

    // Down the right of the window in Preview's place, taking from Roles on a narrow screen; choosing it again closes it.
    private static void ToggleAssistant(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        var pane = window.Assistant;
        pane.Visible = !pane.Visible;
        window._previewPane.Visible = !pane.Visible;
        window._rolesPane.Width = pane.Visible ? Dim.Fill(AssistantWidth) : RolesWidth;
        if (!pane.Visible)
        {
            shell.Focus.Focus(PreviewRegion);
            return;
        }
        shell.Focus.Focus(AssistantRegion);
        _ = window._assistantController.EnsureStartedAsync().ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
    }

    private static void UseTheme(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        var applied = shell.ApplyTheme(window.Showing.Theme);
        window.Settings.Theme = applied;
        shell.StatusBar.State = applied;
        shell.ShowMessage($"Swatch is now in {applied}");
    }

    private static void GoToScheme(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        Selection[] pairs = [.. Themes.Names.SelectMany(theme => SchemeNames.All.Select(scheme => new Selection(theme, scheme)))];
        using var picker = new PickerDialog<Selection>("Go to scheme", width: 76, contentRows: 18, pairs, pair => pair.Display, verb: "Go");
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
        using var sampleShell = new AppShell(shell.App, shell.Cursor, shell.Icons);
        using var sample = new SampleWindow(sampleShell, window.Showing.Theme);
        shell.RunNested(sampleShell, sample, window.Showing.Theme);
    }

    private static void CopyColours(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window || window._roles.Source is not RoleSource roles) return;
        shell.Copy(roles.Describe(window._roles.SelectedItem ?? 0));
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

    private static void PickIssue(AppShell shell)
    {
        using var picker = new IssuePicker();
        picker.Run(shell);
        if (picker.Chosen is { } issue)
            shell.ShowMessage($"A demo: Swatch has no issues, so #{issue.Number} didn't open");
    }

    private static void WriteComment(AppShell shell)
    {
        using var dialog = new CommentDialog();
        dialog.Run(shell);
        if (dialog.Confirmed)
            shell.ShowMessage("A demo: Swatch has nowhere to post it, so the comment went nowhere");
    }

    private static void ReadLog(AppShell shell)
    {
        using var logShell = new AppShell(shell.App, shell.Cursor, shell.Icons);
        using var log = new ReadingLogWindow(logShell);
        shell.RunNested(logShell, log);
    }

    private static void ShowSettings(AppShell shell)
    {
        if (shell.App.TopRunnableView is not SwatchWindow window) return;
        List<SettingsPage> pages =
        [
            new AppearancePage(shell, window.Settings),
            new SwatchesPage(shell, window.Settings, show => window.ShowHexValues = show),
            new KeysPage(shell, window.KeyOverrides),
        ];
        if (shell.MacKeys is { } keys)
            pages.Add(new MacKeysPage(shell, keys));
        using var dialog = new SettingsDialog(pages);
        dialog.Run(shell);
        shell.StatusBar.State = Themes.Current;
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
        ShowRoles();
        _roles.SelectedItem = 0;
        _rolesPane.Title = selection.Display;
        _previewPane.Title = $"Preview › {selection.Theme}";
        _preview.Show(selection.Theme);
        Assistant.NextContext = selection.Display;
    }

    private void ShowRoles()
    {
        if (Themes.SchemesOf(Showing.Theme).TryGetValue(Showing.Scheme, out var scheme))
            _roles.Source = new RoleSource(scheme, _showHexValues);
    }

    internal sealed record Selection(string Theme, string Scheme)
    {
        public string Display => $"{Theme} › {Scheme}";
    }
}
