using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Diagnostics;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Theming;
using Terminal.Gui;

namespace MentalDesk.Tui.Shell;

public sealed class AppShell : IDisposable
{
    private bool _nested;

    public AppShell(IApplication app, TerminalCursor cursor) : this(app, cursor, ClipboardTools.ThisMachine)
    {
    }

    internal AppShell(IApplication app, TerminalCursor cursor, ClipboardTools clipboardTools)
    {
        App = app;
        Cursor = cursor;
        Clipboard = InstallClipboard(app, clipboardTools);
        Focus = new FocusTracker(FocusedView);
        Keys = new Keymap(Commands) { FocusedScope = () => Focus.Region?.Scope ?? CommandScope.Global };
        Scopes.Push(Keys);

        MoveQuitOffEsc();
        Commands
            .Register(ShellCommands.Quit, "Quit", () => App.RequestStop())
            .Register(ShellCommands.ShowCommands, "Show all commands", ShowCommands)
            .Register(ShellCommands.ShowMenu, "Open the menu", () => Menu?.Open(), isEnabled: () => Menu is not null)
            .Register(ShellCommands.ShowDiagnostics, "Show diagnostics", ShowDiagnostics)
            .Register(ShellCommands.ShowKeys, "Show keys", ShowKeys);
        Keys.Bind("Ctrl+Q", ShellCommands.Quit)
            .Bind("Ctrl+E", ShellCommands.ShowCommands)
            .Bind("F10", ShellCommands.ShowMenu)
            .Bind("F12", ShellCommands.ShowDiagnostics)
            .Bind("F1", ShellCommands.ShowKeys);
        StatusBar.Hints.Show([new Hint(ShellCommands.ShowKeys, "keys")], Commands, Keys);

        Focus.RegionChanged += (_, region) => StatusBar.SetFocusWord(region.Name);
        Scopes.ChordChanged += (_, chord) => StatusBar.SetChord(chord);
        App.Keyboard.KeyDown += OnKeyDown;
        App.Iteration += OnIteration;
        Clipboard.Copied += OnCopied;
    }

    public IApplication App { get; }

    public CommandRegistry Commands { get; } = new();

    public Keymap Keys { get; }

    public InputScopes Scopes { get; } = new();

    public FocusTracker Focus { get; }

    public AppStatusBar StatusBar { get; } = new();

    public AppMenu? Menu { get; private set; }

    public TerminalCursor Cursor { get; }

    public TerminalClipboard Clipboard { get; }

    public AppMenu UseMenu(params MenuSpec[] layout) => Menu = new AppMenu(Commands, Keys, layout);

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
        using var diagnostics = new DiagnosticsDialog(App, Cursor, Themes.Current);
        diagnostics.Run(this);
    }

    public void Run(IRunnable window)
    {
        ApplyTheme(Themes.Current);
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
        if (!_nested)
            ShowMessage(outcome.Message, outcome.Severity);
    }

    private void OnKeyDown(object? sender, Key key)
    {
        // An open menu owns the keyboard; its own letters would otherwise run commands too.
        if (_nested || key.Handled || App.Popovers?.GetActivePopover() is not null) return;
        Focus.Reconcile();
        StatusBar.ClearMessage();
        if (Scopes.Handle(key) != KeyResult.Pass)
            key.Handled = true;
    }

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
