using System.Globalization;
using System.Text.Json.Nodes;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Diagnostics;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Finding;
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
    private const string PlaceKey = "place";
    private const string ScreenKey = "screen";
    private const string PaneKey = "pane";

    private static readonly Command[] LineDeletes = [Command.CutToEndOfLine, Command.CutToStartOfLine];

    private static readonly (Command Framework, string Id)[] PaneKeys =
        [(Command.NextTabGroup, ShellCommands.NextPane), (Command.PreviousTabGroup, ShellCommands.PreviousPane)];

    private static readonly (Key Arrow, Command Command, NavigationDirection Direction)[] Arrows =
    [
        (Key.CursorUp, Command.Up, NavigationDirection.Backward), (Key.CursorLeft, Command.Left, NavigationDirection.Backward),
        (Key.CursorDown, Command.Down, NavigationDirection.Forward), (Key.CursorRight, Command.Right, NavigationDirection.Forward),
    ];

    private readonly List<(AppScreen Screen, View View, FocusRegion Start)> _screens = [];
    private readonly Dictionary<FocusRegion, IFindTarget> _findTargets = [];
    private readonly Dictionary<AppScreen, View> _leftOn = [];
    private readonly List<Func<IEnumerable<UnsavedItem>>> _unsaved = [];

    private readonly ArrowFallback _arrowFallback;
    private bool _nested;
    private bool _deleting;
    private IInputScope? _findScope;
    private string? _placeSection;

    public AppShell(IApplication app, TerminalCursor cursor, IconSettings? icons = null)
        : this(app, cursor, ClipboardTools.ThisMachine, icons)
    {
    }

    internal AppShell(IApplication app, TerminalCursor cursor, ClipboardTools clipboardTools, IconSettings? icons = null)
    {
        App = app;
        Cursor = cursor;
        Icons = icons ?? IconSettings.ForThisUser();
        Clipboard = TerminalClipboard.Install(app, clipboardTools);
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
            .Register(ShellCommands.ShowMessages, "Messages", ShowMessages)
            .Register(ShellCommands.NextPane, "Next pane", () => Focus.Next())
            .Register(ShellCommands.PreviousPane, "Previous pane", () => Focus.Previous())
            .Register(ShellCommands.Find, "Find", Find, isEnabled: () => FindTarget() is not null);
        Keys.Bind("Ctrl+Q", ShellCommands.Quit)
            .Bind("Ctrl+E", ShellCommands.ShowCommands)
            .Bind("F10", ShellCommands.ShowMenu)
            .Bind("F12", ShellCommands.ShowDiagnostics)
            .Bind("F1", ShellCommands.ShowKeys)
            .Bind("Ctrl+F", ShellCommands.Find);
        BindPaneKeys();
        _arrowFallback = new ArrowFallback(MoveFocusOutsidePanes);
        BindArrows();
        StatusBar.Hints.Show([("Press F1 for help", () => Commands.Execute(ShellCommands.ShowKeys))]);

        Focus.RegionChanged += (_, region) => StatusBar.SetFocusWord(region.Name);
        Scopes.ChordChanged += (_, chord) => StatusBar.SetChord(chord);
        StatusBar.MessagesRequested += OnMessagesRequested;
        App.Keyboard.KeyDown += OnKeyDown;
        App.Iteration += OnIteration;
        Clipboard.Copied += OnCopied;
        FindBar.KeysRequested += OnFindKeysRequested;
        FindBar.Closed += OnFindClosed;
        Application.DefaultKeyBindingsChanged += BindPaneKeys;
        Application.DefaultKeyBindingsChanged += BindArrows;
    }

    public IApplication App { get; }

    public CommandRegistry Commands { get; } = new();

    public Keymap Keys { get; }

    public InputScopes Scopes { get; } = new();

    public FocusTracker Focus { get; }

    public AppStatusBar StatusBar { get; } = new();

    public AppMenu? Menu { get; private set; }

    public FindBar FindBar { get; } = new();

    public AppScreen? Screen { get; private set; }

    public MacKeys? MacKeys { get; private set; }

    public TerminalCursor Cursor { get; }

    public TerminalClipboard Clipboard { get; }

    public IconSettings Icons { get; }

    public IReadOnlyList<UnsavedItem> Unsaved => [.. _unsaved.SelectMany(report => report())];

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

    // Back to the control that had focus there last; to start on the first visit.
    public void ShowScreen(AppScreen screen)
    {
        RememberFocus();
        var (_, view, start) = _screens.Single(entry => entry.Screen == screen);
        Switch(screen);
        var left = _leftOn.GetValueOrDefault(screen);
        if (left is not null && Shown(left, view) && (left.SetFocus() || left.HasFocus))
            Focus.Reconcile();
        else
            Focus.Focus(start);
    }

    // Opens on the screen and pane saved in the app's settings section, and saves them there when Run ends.
    public void Reopen(string app, FocusRegion start)
    {
        _placeSection = app;
        var place = Settings.Section(app)[PlaceKey] as JsonObject;
        if (Text(place?[ScreenKey]) is { } screenName && _screens.Count > 0)
        {
            if (_screens.FirstOrDefault(entry => entry.Screen.Name == screenName) is not { Screen: not null } saved)
            {
                Focus.Focus(start);
                return;
            }
            Switch(saved.Screen);
            start = saved.Start;
        }
        if (Text(place?[PaneKey]) is not { } paneName || Focus.Regions.FirstOrDefault(region => region.Name == paneName) is not { } pane
            || !Focus.Focus(pane))
            Focus.Focus(start);
    }

    public AppShell ReportUnsaved(Func<IEnumerable<UnsavedItem>> unsaved)
    {
        _unsaved.Add(unsaved);
        return this;
    }

    public AppShell FindIn(FocusRegion region, IFindTarget target)
    {
        _findTargets[region] = target;
        return this;
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

    public StatusMessage ShowMessage(string message, Severity severity = Severity.Info) => StatusBar.ShowMessage(message, severity);

    public StatusMessage ShowNews(string message, Severity severity = Severity.Info) => StatusBar.ShowNews(message, severity);

    public StatusMessage Record(string message, Severity severity = Severity.Info) => StatusBar.Record(message, severity);

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

    public void ShowKeys() => ShowKeys(KeySheet.For(Commands, Keys, Focus.Region?.Name, Focus.Region?.Scope ?? CommandScope.Global, Menu?.Layout ?? []));

    private void ShowKeys(KeySheet sheet)
    {
        using var dialog = new KeysDialog(sheet, App.Screen.Size);
        dialog.Run(this);
    }

    public void ShowMessages()
    {
        using var dialog = new MessagesDialog(StatusBar);
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
        window.IsRunningChanging += OnStopping;
        try
        {
            App.Run(window);
        }
        finally
        {
            window.IsRunningChanging -= OnStopping;
            Cursor.Restore();
            if (_placeSection is not null)
                SavePlace(_placeSection);
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
            BindArrows();
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
        StatusBar.MessagesRequested -= OnMessagesRequested;
        FindBar.KeysRequested -= OnFindKeysRequested;
        FindBar.Closed -= OnFindClosed;
        if (!FindBar.IsOpen)
            FindBar.Dispose();
        Application.DefaultKeyBindingsChanged -= BindPaneKeys;
        Application.DefaultKeyBindingsChanged -= BindArrows;
        _arrowFallback.Dispose();
    }

    // Terminal.Gui moves focus on an arrow no view used; out of a pane, only F6 and Tab should.
    private void BindArrows(object? sender = null, EventArgs? e = null)
    {
        foreach (var (arrow, command, _) in Arrows)
        {
            App.Keyboard.KeyBindings.Remove(arrow);
            App.Keyboard.KeyBindings.Add(arrow, new Terminal.Gui.Input.KeyBinding([command], _arrowFallback));
        }
    }

    private bool MoveFocusOutsidePanes(NavigationDirection direction) =>
        Focus.Owns(Focus.Region, FocusedView()) || App.Navigation!.AdvanceFocus(direction, TabBehavior.TabStop);

    private sealed class ArrowFallback : View
    {
        public ArrowFallback(Func<NavigationDirection, bool> move)
        {
            foreach (var (_, command, direction) in Arrows)
                AddCommand(command, () => move(direction));
        }
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

    private void Switch(AppScreen screen)
    {
        foreach (var entry in _screens)
            entry.View.Visible = entry.Screen == screen;
        Screen = screen;
    }

    private void SavePlace(string app)
    {
        var place = new JsonObject();
        if (Screen is not null) place[ScreenKey] = Screen.Name;
        if (Focus.Region is not null) place[PaneKey] = Focus.Region.Name;
        var section = Settings.Section(app);
        section[PlaceKey] = place;
        Settings.SaveSection(app, section);
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private void RememberFocus()
    {
        if (Screen is null || FocusedView() is not View focused) return;
        var (_, view, _) = _screens.Single(entry => entry.Screen == Screen);
        if (Shown(focused, view))
            _leftOn[Screen] = focused;
    }

    private static bool Shown(View? view, View screen)
    {
        for (; view is not null; view = view.SuperView)
        {
            if (!view.Visible || !view.Enabled) return false;
            if (view == screen) return true;
        }
        return false;
    }

    private IFindTarget? FindTarget() => Focus.Region is { } region ? _findTargets.GetValueOrDefault(region) : null;

    private void Find()
    {
        if (FindTarget() is not { } target) return;
        if (_findScope is null)
            Scopes.Push(_findScope = FindBar.ScopeOver(Keys));
        FindBar.Open(target);
    }

    private void OnFindClosed(object? sender, EventArgs e)
    {
        if (_findScope is null) return;
        Scopes.Pop(_findScope);
        _findScope = null;
    }

    private void OnFindKeysRequested(object? sender, KeyColumn column)
    {
        var focused = FindBar.MostFocused;
        ShowKeys(KeySheet.For(Commands, Keys, null, CommandScope.Global, Menu?.Layout ?? []) with { Here = column });
        focused?.SetFocus();
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
        RememberFocus();
        var shown = StatusBar.Current;
        var chording = Scopes.PendingChord is not null;
        if (Scopes.Handle(CtrlEnter.FromWindows(key, IsWindows)) != KeyResult.Pass || DeleteToLineEdge(key))
            key.Handled = true;
        // Counted after handling, so a key whose command says something doesn't take its own message.
        if (!chording && Scopes.PendingChord is null && StatusBar.Current == shown)
            StatusBar.KeyPressed();
    }

    // Every way out ends here, Terminal.Gui's own Quit binding included.
    private void OnStopping(object? sender, CancelEventArgs<bool> e)
    {
        if (!e.NewValue && !UnsavedPrompt.Leave(this, Unsaved, "quitting"))
            e.Cancel = true;
    }

    private void OnMessagesRequested(object? sender, EventArgs e) => Commands.Execute(ShellCommands.ShowMessages);

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
        RememberFocus();
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
