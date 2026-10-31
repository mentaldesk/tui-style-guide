using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Tests;

public class UnsavedWorkTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private readonly List<UnsavedItem> _unsaved = [];
    private readonly List<string> _saved = [];
    private readonly HashSet<ConfirmDialog> _seen = [];

    public UnsavedWorkTests()
    {
        _host.Shell.ReportUnsaved(() => _unsaved);
        _host.App.Iteration += CountPrompts;
    }

    public override void Dispose()
    {
        _host.App.Iteration -= CountPrompts;
        _host.Dispose();
        base.Dispose();
    }

    private ConfirmDialog? Prompt => _host.App.TopRunnableView as ConfirmDialog;

    private UnsavedItem File(string name) => new(name, "file", () => _saved.Add(name));

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void CountPrompts(object? sender, EventArgs e)
    {
        if (Prompt is { } prompt)
            _seen.Add(prompt);
    }

    // Leaves nothing reported, then quits, so a run that stayed open still ends.
    private void QuitForReal()
    {
        _unsaved.Clear();
        Press(Key.Q.WithCtrl);
    }

    [Fact]
    public void With_nothing_reported_Ctrl_Q_quits_at_once()
    {
        using var window = new AppWindow(_host.Shell);

        _host.Run(window, () => Press(Key.Q.WithCtrl));

        Assert.Empty(_seen);
    }

    [Fact]
    public void Terminal_Guis_own_Quit_binding_asks_too()
    {
        using var window = new AppWindow(_host.Shell);
        _unsaved.Add(File("notes.md"));

        _host.Run(window,
            () => { _host.App.Keyboard.InvokeCommandsBoundToKey(Key.Q.WithCtrl); },
            () => Prompt is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView == window,
            QuitForReal);

        Assert.Single(_seen);
    }

    [Fact]
    public void One_prompt_names_what_the_app_and_each_screen_report_and_Discard_quits()
    {
        using var window = new AppWindow(_host.Shell);
        _host.Shell.ReportUnsaved(() => [File("plan.md")]);
        _unsaved.Add(File("notes.md"));
        IReadOnlyList<string>? lines = null;

        _host.Run(window,
            () => Press(Key.Q.WithCtrl),
            () => Prompt is not null,
            () =>
            {
                lines = Prompt!.Lines;
                Press(Key.D);
            });

        Assert.Equal(["2 files have unsaved changes:", "notes.md, plan.md", "Save them before quitting?"], lines);
        Assert.Single(_seen);
        Assert.Empty(_saved);
    }

    [Fact]
    public void Save_all_saves_each_item_then_quits()
    {
        using var window = new AppWindow(_host.Shell);
        _unsaved.AddRange([File("notes.md"), File("plan.md")]);
        string[]? buttons = null;

        _host.Run(window,
            () => Press(Key.Q.WithCtrl),
            () => Prompt is not null,
            () =>
            {
                buttons = [.. Prompt!.ButtonRow.Select(button => button.Text)];
                Press(CtrlEnter.Key);
            });

        Assert.Equal([" Ctrl+Enter Save all ", " _Discard ", " Esc Cancel "], buttons!);
        Assert.Equal(["notes.md", "plan.md"], _saved);
    }

    [Fact]
    public void A_failed_save_keeps_the_app_open_and_says_which_and_why()
    {
        using var window = new AppWindow(_host.Shell);
        _unsaved.AddRange([File("notes.md"), new UnsavedItem("plan.md", "file", () => throw new IOException("the disk is full"))]);

        _host.Run(window,
            () => Press(Key.Q.WithCtrl),
            () => Prompt is not null,
            () => Press(CtrlEnter.Key),
            () => _host.App.TopRunnableView == window,
            () => _host.Shell.StatusBar.Message == "Couldn't save plan.md: the disk is full",
            QuitForReal);

        Assert.Equal(["notes.md"], _saved);
    }

    [Fact]
    public void Esc_goes_back_to_the_work_with_nothing_lost()
    {
        using var window = new AppWindow(_host.Shell);
        _unsaved.AddRange([File("notes.md"), File("plan.md")]);
        var stillUnsaved = 0;

        _host.Run(window,
            () => Press(Key.Q.WithCtrl),
            () => Prompt is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView == window,
            () => { stillUnsaved = _host.Shell.Unsaved.Count; },
            QuitForReal);

        Assert.Equal(2, stillUnsaved);
        Assert.Empty(_saved);
    }

    [Fact]
    public void A_nested_shell_asks_about_its_own_work_before_going_back()
    {
        using var window = new AppWindow(_host.Shell);
        using var inner = new AppShell(_host.App, _host.Shell.Cursor);
        inner.ReportUnsaved(() => [File("notes.md")]);
        using var innerWindow = new AppWindow(inner);
        IReadOnlyList<string>? lines = null;

        _host.Run(window,
            () => _host.Shell.RunNested(inner, innerWindow),
            () => _host.App.TopRunnableView == innerWindow,
            () => Press(Key.Q.WithCtrl),
            () => Prompt is not null,
            () =>
            {
                lines = Prompt!.Lines;
                Press(Key.D);
            },
            () => _host.App.TopRunnableView == window,
            QuitForReal);

        Assert.Equal(["notes.md has unsaved changes.", "Save it before quitting?"], lines);
        Assert.Single(_seen);
    }

    [Fact]
    public void Past_four_the_prompt_names_four_and_counts_the_rest()
    {
        using var prompt = UnsavedPrompt.Create([.. "abcdef".Select(c => File($"{c}.md"))], "quitting");

        Assert.Equal("a.md, b.md, c.md, d.md and 2 more", prompt.Lines[1]);
    }

    [Fact]
    public void Discard_is_the_only_action_when_anything_cant_be_saved()
    {
        using var prompt = UnsavedPrompt.Create([File("notes.md"), new UnsavedItem("Untitled-1", "file")], "quitting");

        Assert.Equal([" Ctrl+Enter Discard ", " Esc Cancel "], prompt.ButtonRow.Select(button => button.Text));
        Assert.Equal(
            ["2 files have unsaved changes:", "notes.md, Untitled-1", "Untitled-1 can't be saved from here.", "Quitting loses them all."],
            prompt.Lines);
    }

    [Fact]
    public void Closing_one_thing_uses_the_same_wording()
    {
        using var closing = UnsavedPrompt.Create([File("notes.md")], "closing");
        using var cantKeep = UnsavedPrompt.Create([new UnsavedItem("Untitled-1", "file")], "closing");

        Assert.Equal(UnsavedPrompt.Title, closing.Title);
        Assert.Equal(["notes.md has unsaved changes.", "Save it before closing?"], closing.Lines);
        Assert.Equal([" Ctrl+Enter Save ", " _Discard ", " Esc Cancel "], closing.ButtonRow.Select(button => button.Text));
        Assert.Equal(["Untitled-1 has unsaved changes.", "Closing loses it."], cantKeep.Lines);
    }

    [Fact]
    public void Mixed_kinds_are_counted_as_items()
    {
        using var prompt = UnsavedPrompt.Create([File("notes.md"), new UnsavedItem("your comment", "comment", () => { })], "quitting");

        Assert.Equal("2 items have unsaved changes:", prompt.Lines[0]);
    }
}
