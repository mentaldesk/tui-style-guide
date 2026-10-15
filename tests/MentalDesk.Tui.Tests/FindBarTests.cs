using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Finding;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class FindBarTests : StaticConfigurationTest
{
    private const string Content = "alpha Retry\nbeta\nretry gamma retry\ndelta";

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private FindBar Bar => _host.Shell.FindBar;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Type(string text) => Press([.. text.Select(c => new Key(c))]);

    private bool Says(string status) => Bar.Status == status;

    [Fact]
    public void Typing_highlights_every_match_whatever_its_case_and_selects_the_first()
    {
        using var window = new Notes(_host.Shell);

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.QueryField.HasFocus,
            () => Type("RETRY"),
            () => Says("1 of 3"));

        Assert.NotNull(window.Editor.SearchStrategy);
        Assert.Equal([new TextMatch(0, 6, 5), new TextMatch(2, 0, 5), new TextMatch(2, 12, 5)], Bar.Matches);
        Assert.Equal("Retry", window.Editor.SelectedText);
    }

    [Fact]
    public void A_query_with_no_matches_says_so_and_an_empty_one_clears_the_highlights()
    {
        using var window = new Notes(_host.Shell);
        var noResults = false;

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.QueryField.HasFocus,
            () => Type("zzz"),
            () => Says("No results"),
            () =>
            {
                noResults = window.Editor.SearchStrategy is null && !window.Editor.HasSelection;
                Press(Key.Backspace, Key.Backspace, Key.Backspace);
            },
            () => Bar.Query.Length == 0);

        Assert.True(noResults);
        Assert.Equal("", Bar.Status);
        Assert.Empty(Bar.Matches);
        Assert.Null(window.Editor.SearchStrategy);
    }

    [Fact]
    public void Enter_and_Shift_Enter_step_through_the_matches_wrapping_at_both_ends()
    {
        using var window = new Notes(_host.Shell);
        List<string> said = [];

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.QueryField.HasFocus,
            () => Type("retry"),
            () => Says("1 of 3"),
            () => Press(Key.Enter),
            () => Says("2 of 3"),
            () => Press(Key.Enter),
            () => Says("3 of 3"),
            () => Press(Key.Enter),
            () => Says("1 of 3"),
            () => Press(Key.Enter.WithShift),
            () => Says("3 of 3"),
            () =>
            {
                said.Add(window.Editor.SelectedText);
                Press(Key.Enter.WithShift);
            },
            () => Says("2 of 3"));

        Assert.Equal(["retry"], said);
        Assert.Equal(new TextMatch(2, 0, 5), Bar.Matches[Bar.Current]);
    }

    [Theory]
    [InlineData(23, 5, "gamma")]
    [InlineData(6, 8, "")]
    public void A_single_line_selection_starts_the_bar_with_it_and_a_multi_line_one_does_not(int start, int length, string query)
    {
        using var window = new Notes(_host.Shell);

        _host.Run(window,
            () => window.Editor.HasFocus,
            () =>
            {
                window.Editor.SelectRange(start, length);
                Press(Key.F.WithCtrl);
            },
            () => Bar.IsOpen);

        Assert.Equal(query, Bar.Query);
    }

    [Fact]
    public void Esc_closes_the_bar_leaving_the_view_at_the_match_with_focus_in_it()
    {
        using var window = new Notes(_host.Shell);
        var caretAtMatch = 0;

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.QueryField.HasFocus,
            () => Type("gamma"),
            () => Says("1 of 1"),
            () =>
            {
                caretAtMatch = window.Editor.CaretOffset;
                Press(Key.Esc);
            },
            () => !Bar.IsOpen && window.Editor.HasFocus);

        Assert.Equal(caretAtMatch, window.Editor.CaretOffset);
        Assert.Null(window.Editor.SearchStrategy);
        Assert.Null(Bar.SuperView);
        Assert.Same(_host.Shell.Keys, _host.Shell.Scopes.Top);
        Assert.Equal("Notes", _host.Shell.StatusBar.FocusWord);
    }

    [Fact]
    public void Esc_from_the_text_closes_the_bar_too()
    {
        using var window = new Notes(_host.Shell);

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.QueryField.HasFocus,
            () => window.Editor.SetFocus(),
            () => window.Editor.HasFocus,
            () => Press(Key.Esc),
            () => !Bar.IsOpen);
    }

    [Fact]
    public void The_replace_row_is_hidden_unless_asked_for()
    {
        using var window = new Notes(_host.Shell);

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.IsOpen);

        Assert.False(Bar.ReplaceVisible);
        Assert.False(Bar.ReplacementField.Visible);
        Assert.Equal(2, Bar.Frame.Height);
    }

    [Fact]
    public void Enter_in_the_replacement_replaces_the_current_match_and_goes_to_the_next()
    {
        using var window = new Notes(_host.Shell);

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.IsOpen,
            () => Bar.Open(window.Target, replace: true),
            () => Bar.QueryField.HasFocus,
            () => Type("retry"),
            () => Says("1 of 3"),
            () => Press(Key.Tab),
            () => Bar.ReplacementField.HasFocus,
            () => Type("again"),
            () => Press(Key.Enter),
            () => Says("1 of 2"));

        Assert.True(Bar.ReplaceVisible);
        Assert.Equal("alpha again\nbeta\nretry gamma retry\ndelta", window.Editor.Text);
        Assert.Equal(new TextMatch(2, 0, 5), Bar.Matches[Bar.Current]);
    }

    [Fact]
    public void Ctrl_Enter_replaces_every_match()
    {
        using var window = new Notes(_host.Shell);

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.IsOpen,
            () => Bar.Open(window.Target, replace: true),
            () => Bar.QueryField.HasFocus,
            () => Type("retry"),
            () => Says("1 of 3"),
            () => { Bar.Replacement = "again"; },
            () => Press(CtrlEnter.Key),
            () => Says("Replaced 3"));

        Assert.Equal("alpha again\nbeta\nagain gamma again\ndelta", window.Editor.Text);
    }

    [Fact]
    public void Find_is_available_only_where_the_focused_region_has_something_to_search()
    {
        using var window = new Notes(_host.Shell);
        var inNotes = false;
        var ticks = 0;

        _host.Run(window,
            () => window.Editor.HasFocus,
            () =>
            {
                inNotes = _host.Shell.Commands.IsAvailable(ShellCommands.Find, Notes.NotesRegion.Scope);
                _host.Shell.Focus.Focus(Notes.ListRegion);
            },
            () => _host.Shell.StatusBar.FocusWord == "List",
            () => Press(Key.F.WithCtrl),
            () => ++ticks > 5);

        Assert.True(inNotes);
        Assert.False(_host.Shell.Commands.IsAvailable(ShellCommands.Find, Notes.ListRegion.Scope));
        Assert.False(Bar.IsOpen);
        Assert.Equal("Ctrl+F", _host.Shell.Keys.For(ShellCommands.Find).Single().Display);
    }

    [Fact]
    public void F1_in_the_bar_lists_its_keys_beside_Everywhere()
    {
        using var window = new Notes(_host.Shell);
        KeySheet? sheet = null;

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.QueryField.HasFocus,
            () => Press(Key.F1),
            () => _host.App.TopRunnableView is KeysDialog,
            () =>
            {
                sheet = ((KeysDialog)_host.App.TopRunnableView!).Sheet;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is Notes && Bar.QueryField.HasFocus);

        Assert.Equal("Find bar", sheet!.Here!.Heading);
        Assert.Equal(
            [new KeyRow("Enter", "Next match"), new KeyRow("Shift+Enter", "Previous match"), new KeyRow("Esc", "Close")],
            sheet.Here.Groups.Single().Rows);
        Assert.Equal("Everywhere", sheet.Everywhere!.Heading);
        Assert.True(Bar.IsOpen);
    }

    [Fact]
    public void The_bar_s_keys_with_the_replace_row_name_what_Enter_does_in_the_replacement()
    {
        using var window = new Notes(_host.Shell);
        KeyColumn? column = null;

        _host.Run(window,
            () => window.Editor.HasFocus,
            () => Press(Key.F.WithCtrl),
            () => Bar.IsOpen,
            () => Bar.Open(window.Target, replace: true),
            () => Bar.QueryField.HasFocus,
            () => Press(Key.Tab),
            () => Bar.ReplacementField.HasFocus,
            () => { column = Bar.KeysHere(); });

        Assert.Equal(
            ["Replace and go to next", "Previous match", "Replace all", "Other field", "Close"],
            column!.Groups.Single().Rows.Select(row => row.Label));
    }

    private sealed class Notes : AppWindow
    {
        public static readonly FocusRegion NotesRegion = new("Notes", new CommandScope("Notes"));
        public static readonly FocusRegion ListRegion = new("List", new CommandScope("List"));

        public Notes(AppShell shell) : base(shell)
        {
            Editor = new Terminal.Gui.Editor.Editor { Width = Dim.Fill(), Height = Dim.Fill(), Text = Content };
            Target = new EditorFindTarget(Editor);
            var notes = Panes.Create("Notes", Editor, x: 0, width: 40);
            var list = new ListView { Width = Dim.Fill(), Height = Dim.Fill() };
            var listPane = Panes.Create("List", list, x: Pos.Right(notes), width: Dim.Fill());
            Body.Add(notes, listPane);
            Panes.Track(shell, NotesRegion, notes, Editor);
            Panes.Track(shell, ListRegion, listPane, list);
            shell.FindIn(NotesRegion, Target);
            Initialized += (_, _) => shell.Focus.Focus(NotesRegion);
        }

        public Terminal.Gui.Editor.Editor Editor { get; }

        public EditorFindTarget Target { get; }
    }
}
