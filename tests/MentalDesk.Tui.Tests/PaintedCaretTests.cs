using System.Drawing;
using System.Reflection;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class PaintedCaretTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private readonly PlaceholderField _line = new() { Width = 20 };
    private readonly MultiLineField _box = new() { Y = 2, Width = 22, Height = 5 };

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    public static TheoryData<string> Bundled => [.. Themes.Bundled];

    private void Run(params Delegate[] steps)
    {
        using var window = new AppWindow(_host.Shell);
        window.Add(_line, _box);
        _host.Run(window, [() => _line.SetFocus(), () => _line.HasFocus, .. steps]);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Type(string text)
    {
        foreach (var c in text)
            _host.App.InjectKey(new Key(c));
    }

    private static Attribute Block()
    {
        var cursor = SchemeManager.GetScheme(SchemeNames.Cursor).Normal;
        return new Attribute(cursor.Background, cursor.Foreground);
    }

    private Cell CellAt(View view, int col, int row)
    {
        var screen = view.ViewportToScreen(new Point(col, row));
        return _host.App.Driver!.Contents![screen.Y, screen.X];
    }

    private bool CaretAt(View view, int col, int row, string grapheme) =>
        CellAt(view, col, row) is var cell && cell.Attribute == Block() && cell.Grapheme == grapheme;

    private int Carets(View view) =>
        Enumerable.Range(0, view.Viewport.Height)
            .SelectMany(row => Enumerable.Range(0, view.Viewport.Width).Select(col => CellAt(view, col, row)))
            .Count(cell => cell.Attribute == Block());

    [Theory]
    [MemberData(nameof(Bundled))]
    public void A_one_line_field_paints_a_block_over_the_character_at_the_insertion_point_in_the_themes_caret_colours(string theme)
    {
        Themes.Apply(theme);
        Run(
            () => Type("abc"),
            () => _line.Text == "abc",
            () => Press(Key.CursorLeft),
            () => CaretAt(_line, 2, 0, "c"));
    }

    [Theory]
    [MemberData(nameof(Bundled))]
    public void A_multi_line_field_paints_a_block_over_the_character_at_the_insertion_point_in_the_themes_caret_colours(string theme)
    {
        Themes.Apply(theme);
        Run(
            () => _box.SetFocus(),
            () => _box.HasFocus,
            () => Type("abc"),
            () => _box.Text == "abc",
            () => Press(Key.CursorLeft),
            () => CaretAt(_box, 2, 0, "c"));
    }

    [Fact]
    public void The_caret_sits_after_the_last_character_at_the_end_of_a_line()
    {
        Run(
            () => Type("ab"),
            () => CaretAt(_line, 2, 0, " "),
            () => _box.SetFocus(),
            () => Type("cd"),
            () => Press(Key.Enter),
            () => Type("e"),
            () => Press(Key.CursorUp, Key.End),
            () => CaretAt(_box, 2, 0, " "));
    }

    [Fact]
    public void The_caret_follows_every_move_without_waiting_for_a_keystroke()
    {
        Run(
            () => Type("one two"),
            () => _line.Text == "one two",
            () => Press(Key.Home),
            () => CaretAt(_line, 0, 0, "o"),
            () => Press(Key.CursorRight.WithCtrl),
            () => CaretAt(_line, 3, 0, " ") || CaretAt(_line, 4, 0, "t"),
            () => Press(Key.End),
            () => CaretAt(_line, 7, 0, " "),
            () => { _line.InsertionPoint = 1; },
            () => CaretAt(_line, 1, 0, "n"),
            () => _box.SetFocus(),
            () => Type("one two"),
            () => _box.Text == "one two",
            () => Press(Key.Home),
            () => CaretAt(_box, 0, 0, "o"),
            () => Press(Key.CursorRight),
            () => CaretAt(_box, 1, 0, "n"),
            () => { _box.CaretOffset = 5; },
            () => CaretAt(_box, 5, 0, "w"));
    }

    private void RunFocusedBeforeTheAppRuns(View field, params Delegate[] steps)
    {
        using var window = new AppWindow(_host.Shell);
        window.Add(_line, _box);
        field.SetFocus();
        _host.Run(window, [() => field.HasFocus, .. steps]);
    }

    [Fact]
    public void A_one_line_field_focused_before_the_app_runs_follows_every_move_without_waiting_for_a_keystroke()
    {
        RunFocusedBeforeTheAppRuns(_line,
            () => Type("one two"),
            () => _line.Text == "one two",
            () => Press(Key.CursorLeft),
            () => CaretAt(_line, 6, 0, "o"),
            () => Press(Key.Home),
            () => CaretAt(_line, 0, 0, "o"),
            () => Press(Key.CursorRight),
            () => CaretAt(_line, 1, 0, "n"),
            () => Press(Key.End),
            () => CaretAt(_line, 7, 0, " "),
            () => { _line.InsertionPoint = 1; },
            () => CaretAt(_line, 1, 0, "n"));
    }

    [Fact]
    public void A_multi_line_field_focused_before_the_app_runs_follows_every_move_without_waiting_for_a_keystroke()
    {
        RunFocusedBeforeTheAppRuns(_box,
            () => Type("one two"),
            () => _box.Text == "one two",
            () => Press(Key.CursorLeft),
            () => CaretAt(_box, 6, 0, "o"),
            () => { _box.CaretOffset = 1; },
            () => CaretAt(_box, 1, 0, "n"));
    }

    private static void MoveFrameworkCursor(View field) =>
        field.Cursor = field.Cursor with { Position = new Point(1, 1) };

    [Fact]
    public void A_field_focused_before_the_app_runs_stops_following_once_it_loses_focus()
    {
        var draws = 0;
        RunFocusedBeforeTheAppRuns(_line,
            () => Type("ab"),
            () => CaretAt(_line, 2, 0, " "),
            () => _box.SetFocus(),
            () => Carets(_line) == 0 && !_line.NeedsDraw,
            () =>
            {
                _line.DrawComplete += (_, _) => draws++;
                MoveFrameworkCursor(_line);
            },
            () => { },
            () => { },
            () => { });

        Assert.Equal(0, draws);
    }

    [Fact]
    public void A_field_focused_before_the_app_runs_stops_following_once_it_is_disposed()
    {
        var before = false;
        RunFocusedBeforeTheAppRuns(_line,
            () => Type("ab"),
            () => CaretAt(_line, 2, 0, " "),
            () =>
            {
                before = CaretListening();
                _line.SuperView!.Remove(_line);
                _line.Dispose();
            });

        Assert.True(before);
        Assert.False(CaretListening());
    }

    private bool CaretListening()
    {
        var iteration = _host.App.GetType().GetField(nameof(IApplication.Iteration), BindingFlags.Instance | BindingFlags.NonPublic)!;
        return iteration.GetValue(_host.App) is Delegate listeners
            && listeners.GetInvocationList().Any(listener => listener.Target is PaintedCaret);
    }

    [Fact]
    public void Only_the_focused_field_shows_a_caret_and_the_terminal_cursor_stays_hidden()
    {
        var counts = new List<(int Line, int Box, bool TerminalCursor)>();
        Run(
            () => Type("ab"),
            () => Carets(_line) == 1,
            () => counts.Add((Carets(_line), Carets(_box), _host.App.Driver!.GetCursor().IsVisible)),
            () => _box.SetFocus(),
            () => Type("cd"),
            () => Carets(_box) == 1 && Carets(_line) == 0,
            () => counts.Add((Carets(_line), Carets(_box), _host.App.Driver!.GetCursor().IsVisible)));

        Assert.Equal([(1, 0, false), (0, 1, false)], counts);
        Assert.Equal(CursorStyle.Hidden, _line.Cursor.Style);
        Assert.Equal(CursorStyle.Hidden, _box.Cursor.Style);
    }

    [Fact]
    public void Switching_theme_recolours_the_caret()
    {
        Themes.Apply(Themes.Bundled[0]);
        Run(
            () => Type("ab"),
            () => CaretAt(_line, 2, 0, " "),
            () => { _host.Shell.ApplyTheme(Themes.Bundled[1]); },
            () => CaretAt(_line, 2, 0, " "));
    }

    [Fact]
    public void At_the_end_of_a_full_row_the_caret_sits_where_the_next_character_will_go()
    {
        var full = new string('x', 20);
        Run(
            () => _box.SetFocus(),
            () => _box.HasFocus,
            () => Type(full),
            () => _box.Text == full,
            () => CaretAt(_box, 0, 1, " ") && Carets(_box) == 1,
            () => Type("y"),
            () => CaretAt(_box, 1, 1, " ") && Carets(_box) == 1,
            () => Press(Key.Backspace),
            () => CaretAt(_box, 0, 1, " ") && Carets(_box) == 1);
    }

    [Fact]
    public void A_caret_moving_between_the_ends_of_full_rows_follows_at_once()
    {
        var full = new string('x', 20);
        Run(
            () => _box.SetFocus(),
            () => _box.HasFocus,
            () => { _box.Text = full + "\n" + full; _box.CaretOffset = _box.Text.Length; },
            () => CaretAt(_box, 0, 2, " ") && Carets(_box) == 1,
            () => { _box.CaretOffset = full.Length; },
            () => CaretAt(_box, 0, 1, "x") && Carets(_box) == 1);
    }

    [Fact]
    public void Filling_the_last_row_scrolls_so_the_caret_stays_in_the_box()
    {
        Run(
            () => _box.SetFocus(),
            () => _box.HasFocus && _box.Viewport.Height == 3,
            () => Type(new string('x', 60)),
            () => _box.Text.Length == 60,
            () => Carets(_box) == 1 && _box.Cursor.Position is null,
            () => Type("y"),
            () => Carets(_box) == 1 && _box.Text.Length == 61);
    }

    [Fact]
    public void Text_long_enough_to_bring_the_scroll_bar_keeps_the_caret_in_the_box()
    {
        Run(
            () => _box.SetFocus(),
            () => _box.HasFocus,
            () => { _box.Text = new string('x', 300); _box.CaretOffset = 300; },
            () => _box.Viewport.Width == 19 && Carets(_box) == 1);
    }

    [Fact]
    public void The_wheel_still_scrolls_away_from_the_caret()
    {
        var top = -1;
        Run(
            () => _box.SetFocus(),
            () => _box.HasFocus,
            () => { _box.Text = new string('x', 300); _box.CaretOffset = 300; },
            () => Carets(_box) == 1,
            () => { top = _box.Viewport.Y; _box.Viewport = _box.Viewport with { Y = 0 }; },
            () => _box.Viewport.Y == 0 && Carets(_box) == 0);

        Assert.True(top > 0);
    }

    [Fact]
    public void A_field_hanging_off_the_top_left_of_the_screen_draws_without_a_caret_there()
    {
        _line.X = -3;
        _line.Y = -1;
        Run(
            () => Type("ab"),
            () => _line.Text == "ab");
    }
}
