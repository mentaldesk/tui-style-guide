using System.Globalization;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Diagnostics;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Links;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Theming;
using Terminal.Gui;

namespace MentalDesk.Tui.Shell;

public sealed class AppShell : IDisposable
{
    private static readonly Command[] LineDeletes = [Command.CutToEndOfLine, Command.CutToStartOfLine];

    private static readonly (Command Framework, string Id)[] PaneKeys =
        [(Command.NextTabGroup, ShellCommands.NextPane), (Command.PreviousTabGroup, ShellCommands.PreviousPane)];

    private readonly List<(AppScreen Screen, View View, FocusRegion Start)> _screens = [];

    private bool _nested;
    private bool _deleting;

    public AppShell(IApplication app, TerminalCursor cursor, IconSettings? icons = null)
        : this(app, cursor, ClipboardTools.ThisMachine, icons)
    {
    }

    internal AppShell(IApplication app, TerminalCursor cursor, ClipboardTools clipboardTools, IconSettings? icons = null)
    {
        App = app;
        Cursor = cursor;
        Icons = icons ?? IconSettings.ForThisUser();
        Clipboard = InstallClipboard(app, clipboardTools);
        if (app.Driver is { } driver)
            AlternateKeyCodes.Install(driver.GetInputProcessor());
        Links = new LinkOpener(Clipboard);
        Focus = new FocusTracker(FocusedView);
        Keys = new Keymap(Commands) { FocusedScope = () => Focus.Region?.Scope ?? CommandScope.Global };
        Scopes.Push(Keys);

        MoveQuitOffEsc();
        Commands
            .Register(ShellCommands.Quit, "Quit", () => App.RequestStop())
            .Register(ShellCommands.ShowCommands, "Show all commands", ShowCommands)
            .Register(ShellCommands.ShowMenu, "Open the menu", () => Menu?.Open(), isEnabled: () => Menu is not null)
            .Register(ShellCommands.ShowDiagnostics, "Show diagnostics", ShowDiagnostics)
            .Register(ShellCommands.ShowKeys, "Show keys", ShowKeys)
            .Register(ShellCommands.NextPane, "Next pane", () => Focus.Next())
            .Register(ShellCommands.PreviousPane, "Previous pane", () => Focus.Previous());
        Keys.Bind("Ctrl+Q", ShellCommands.Quit)
            .Bind("Ctrl+E", ShellCommands.ShowCommands)
            .Bind("F10", ShellCommands.ShowMenu)
            .Bind("F12", ShellCommands.ShowDiagnostics)
            .Bind("F1", ShellCommands.ShowKeys);
        BindPaneKeys();
        StatusBar.Hints.Show([("Press F1 for help", () => Commands.Execute(ShellCommands.ShowKeys))]);

        Focus.RegionChanged += (_, region) => StatusBar.SetFocusWord(region.Name);
        Scopes.ChordChanged += (_, chord) => StatusBar.SetChord(chord);
        App.Keyboard.KeyDown += OnKeyDown;
        App.Iteration += OnIteration;
        Clipboard.Copied += OnCopied;
        Application.DefaultKeyBindingsChanged += BindPaneKeys;
    }

    public IApplication App { get; }

    public CommandRegistry Commands { get; } = new();

    public Keymap Keys { get; }

    public InputScopes Scopes { get; } = new();

    public FocusTracker Focus { get; }

    public AppStatusBar StatusBar { get; } = new();

    public AppMenu? Menu { get; private set; }

    public AppScreen? Screen { get; private set; }

    public MacKeys? MacKeys { get; private set; }

    public TerminalCursor Cursor { get; }

    public TerminalClipboard Clipboard { get; }

    public IconSettings Icons { get; }

    public MentalDeskSettings Settings => Icons.File;

    internal LinkOpener Links { get; set; }

    internal bool IsWindows { get; init; } = OperatingSystem.IsWindows();

    public AppMenu UseMenu(params MenuSpec[] layout) => Menu = new AppMenu(Commands, Keys, layout);

    public MenuSpec GoMenu(params MenuEntry?[] places) =>
        new("_Go", [ShellCommands.NextPane, ShellCommands.PreviousPane, null, .. places]) { KeysHeading = "Moving around" };

    public MenuEntry GoTo(AppScreen screen) => new(screen.CommandId, screen.Name, () => Screen == screen);

    // Bind the app's own Ctrl+G keys first, so a clash throws here.
    public AppShell AddScreen(AppScreen screen, View view, FocusRegion start)
    {
        var chord = KeyChord.Parse(screen.Chord);
        if (Keys.CheckConflict(chord, CommandScope.Global) is { } conflict)
            throw new ArgumentException($"{screen.Chord} for {screen.Name} clashes with a key already bound ({conflict}).", nameof(screen));
        Commands.Register(screen.CommandId, $"Go to {screen.Name}", () => ShowScreen(screen));
        Keys.Bind(chord, screen.CommandId);
        _screens.Add((screen, view, start));
        if (Screen is null)
            Screen = screen;
        else
            view.Visible = false;
        return this;
    }

    public void ShowScreen(AppScreen screen)
    {
        var (_, _, start) = _screens.Single(entry => entry.Screen == screen);
        foreach (var entry in _screens)
            entry.View.Visible = entry.Screen == screen;
        Screen = screen;
        Focus.Focus(start);
    }

    // Marks the app active in the terminal while Run runs.
    public MacKeys UseMacKeys(MacKeys keys) => MacKeys = keys;

    public string ApplyTheme(string theme)
    {
        var applied = Themes.Apply(theme);
        if (Themes.CursorColour(applied) is { } colour)
            Cursor.Colour(colour);
        App.TopRunnableView?.SetNeedsDraw();
        return applied;
    }

    public void ShowMessage(string message, Severity severity = Severity.Info) => StatusBar.ShowMessage(message, severity);

    public CopyOutcome Copy(string text) => Clipboard.Copy(text);

    public LinkOutcome OpenLink(string url)
    {
        var outcome = Links.Open(url);
        ShowMessage(outcome.Message, outcome.Severity);
        return outcome;
    }

    public void ShowCommands()
    {
        using var palette = new CommandPalette(Commands, Keys, Focus.Region?.Scope ?? CommandScope.Global);
        palette.Run(this);
        if (palette.Chosen is { } row)
            Commands.Execute(row.Id);
    }

    public void ShowKeys()
    {
        var sheet = KeySheet.For(Commands, Keys, Focus.Region?.Name, Focus.Region?.Scope ?? CommandScope.Global, Menu?.Layout ?? []);
        using var dialog = new KeysDialog(sheet, App.Screen.Size);
        dialog.Run(this);
    }

    public void ShowDiagnostics()
    {
        using var diagnostics = new DiagnosticsDialog(App, Cursor, Themes.Current, Icons, IsWindows);
        diagnostics.Run(this);
    }

    public void Run(IRunnable window)
    {
        ApplyTheme(Themes.Current);
        using var active = MacKeys?.Activate();
        try
        {
            App.Run(window);
        }
        finally
        {
            Cursor.Restore();
        }
    }

    // Keys go to inner until it closes; then this shell's theme and focus come back.
    public void RunNested(AppShell inner, IRunnable window, string? theme = null)
    {
        var outerTheme = Themes.Current;
        var region = Focus.Region;
        _nested = true;
        try
        {
            if (theme is not null)
                Themes.Apply(theme);
            inner.Run(window);
        }
        finally
        {
            _nested = false;
            ApplyTheme(outerTheme);
            if (region is not null)
                Focus.Focus(region);
        }
    }

    public void Dispose()
    {
        App.Keyboard.KeyDown -= OnKeyDown;
        App.Iteration -= OnIteration;
        Clipboard.Copied -= OnCopied;
        Application.DefaultKeyBindingsChanged -= BindPaneKeys;
    }

    private void BindPaneKeys(object? sender = null, EventArgs? e = null)
    {
        foreach (var (framework, id) in PaneKeys)
        {
            foreach (var binding in Keys.For(id).ToList())
                Keys.Unbind(binding.Chord, binding.Scope);
            foreach (var key in Application.GetDefaultKeys(framework))
                Keys.Bind([key], id);
        }
    }

    // A nested shell shares the clipboard its outer shell installed.
    private static TerminalClipboard InstallClipboard(IApplication app, ClipboardTools tools)
    {
        if (app.Driver is not { } driver) return new TerminalClipboard(null, _ => { }, tools);
        if (driver.Clipboard is not TerminalClipboard installed)
            driver.Clipboard = installed = new TerminalClipboard(driver.Clipboard, driver.WriteRaw, tools);
        return installed;
    }

    private void OnCopied(object? sender, CopyOutcome outcome)
    {
        if (!_nested && !_deleting)
            ShowMessage(outcome.Message, outcome.Severity);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        // An open menu owns the keyboard; its own letters would otherwise run commands too.
        if (_nested || key.Handled || App.Popovers?.GetActivePopover() is not null) return;
        Focus.Reconcile();
        StatusBar.ClearMessage();
        if (Scopes.Handle(CtrlEnter.FromWindows(key, IsWindows)) != KeyResult.Pass || DeleteToLineEdge(key))
            key.Handled = true;
    }

    // Terminal.Gui puts what these delete on the clipboard, which would otherwise report it as copied.
    private bool DeleteToLineEdge(Key key)
    {
        if (FocusedView() is not View view || !view.KeyBindings.TryGet(key, out var binding)
            || !binding.Commands.Any(LineDeletes.Contains)) return false;
        var before = Length(view);
        _deleting = true;
        try
        {
            view.InvokeCommands(binding.Commands, binding);
        }
        finally
        {
            _deleting = false;
        }
        var deleted = before - Length(view);
        if (deleted > 0)
            ShowMessage($"{deleted:N0} character{(deleted == 1 ? "" : "s")} deleted");
        return true;
    }

    // TextView's Text hides View.Text, which goes stale once it's edited.
    private static int Length(View view) =>
#pragma warning disable CS0618
        new StringInfo(view is TextView editor ? editor.Text : view.Text).LengthInTextElements;
#pragma warning restore CS0618

    private void OnIteration(object? sender, EventArgs e)
    {
        if (_nested) return;
        Focus.Reconcile();
        Menu?.ShowAvailable();
    }

    private object? FocusedView()
    {
        // GetFocused can return an ancestor of the view actually holding the keys.
        var view = App.Navigation?.GetFocused();
        while (view?.MostFocused is { } inner && inner != view)
            view = inner;
        return view;
    }

    // Esc never quits. Removing Terminal.Gui's Quit binding breaks its popover menus, so it's moved instead.
    private static void MoveQuitOffEsc() =>
        Application.SetDefaultKeyBinding(Command.Quit, new PlatformKeyBinding { All = [Key.Q.WithCtrl] });
}
