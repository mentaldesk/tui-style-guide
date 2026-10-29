using System.Drawing;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Theming;
using Swatch;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class MultiLineFieldTests : StaticConfigurationTest
{
    private Host? _host;

    public override void Dispose()
    {
        _host?.Dispose();
        base.Dispose();
    }

    private Host Host => _host ??= new Host();

    private CommentDialog? Dialog => Host.App.TopRunnableView as CommentDialog;

    private MultiLineField Field => Dialog!.Body;

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(Host.Shell);
        Host.Run(window, [
            () => { Host.Shell.Commands.Execute(SwatchCommands.WriteComment); },
            () => Dialog?.IsInitialized == true && Field.HasFocus,
            .. steps,
        ]);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            Host.App.InjectKey(key);
    }

    private void Type(string text)
    {
        foreach (var c in text)
            Host.App.InjectKey(new Key(c));
    }

    private string Row(View view, int row)
    {
        var origin = view.ViewportToScreen(Point.Empty);
        var contents = Host.App.Driver!.Contents!;
        return string.Concat(Enumerable.Range(origin.X, view.Viewport.Width).Select(col => contents[origin.Y + row, col].Grapheme)).TrimEnd();
    }

    private Attribute? BorderCorner(View view)
    {
        var corner = view.FrameToScreen().Location;
        return Host.App.Driver!.Contents![corner.Y, corner.X].Attribute;
    }

    [Fact]
    public void Help_opens_it_with_the_field_focused_its_keys_on_the_buttons_and_no_hints()
    {
        string[]? buttons = null;
        var hintRows = -1;
        Run(() =>
        {
            buttons = [Dialog!.PostButton.Text, Dialog.CancelButton.Text];
            hintRows = Dialog.SubViews.OfType<MentalDesk.Tui.Chrome.HintRow>().Count();
            Press(Key.Esc);
        }, () => Dialog is null);

        Assert.Equal([" Ctrl+Enter Post ", " Esc Cancel "], buttons!);
        Assert.Equal(0, hintRows);
    }

    [Fact]
    public void Clicking_Post_posts()
    {
        Run(
            () => Type("a"),
            () => Field.Text == "a",
            () =>
            {
                Dialog!.TitleField.Text = "Title";
                Dialog.PostButton.InvokeCommand(Command.Accept);
            },
            () => Dialog is null);

        Assert.Equal("A demo: Swatch has nowhere to post it, so the comment went nowhere", Host.Shell.StatusBar.Message);
    }

    [Fact]
    public void The_placeholder_shows_while_empty_and_goes_on_the_first_keystroke()
    {
        var rows = new List<string>();
        Run(
            () => rows.Add(Row(Field, 0)),
            () => Type("a"),
            () => Field.Text == "a",
            () => rows.Add(Row(Field, 0)),
            () => Press(Key.Backspace),
            () => Field.Text.Length == 0,
            () => rows.Add(Row(Field, 0)),
            () => Press(Key.Esc));

        Assert.Equal(["Leave a comment…", "a", "Leave a comment…"], rows);
    }

    [Fact]
    public void Enter_starts_a_new_line_and_Ctrl_Enter_posts()
    {
        string? text = null;
        Run(
            () => Type("a"),
            () => Press(Key.Enter),
            () => Type("b"),
            () => Field.Text.Length == 3,
            () =>
            {
                text = Field.Text;
                Dialog!.TitleField.Text = "Title";
                Press(Key.Enter.WithCtrl);
            },
            () => Dialog is null);

        Assert.Equal("a\nb", text!.ReplaceLineEndings("\n"));
        Assert.Equal("A demo: Swatch has nowhere to post it, so the comment went nowhere", Host.Shell.StatusBar.Message);
    }

    [Fact]
    public void Enter_on_a_button_presses_nothing()
    {
        var ticks = 0;
        Run(
            () => Type("a"),
            () => Press(Key.Tab),
            () => Dialog!.PostButton.HasFocus,
            () => Press(Key.Enter),
            () => ++ticks > 5,
            () => Dialog is not null,
            () => Press(Key.Tab),
            () => Dialog!.CancelButton.HasFocus,
            () => Press(Key.Enter),
            () => ++ticks > 10,
            () => Dialog is not null,
            () => Press(Key.Esc),
            () => Dialog is null);

        Assert.Null(Host.Shell.StatusBar.Message);
    }

    [Fact]
    public void Tab_and_Shift_Tab_move_focus_and_never_type_a_tab()
    {
        var focus = new List<string>();
        Run(
            () => Press(Key.Tab),
            () => Dialog!.PostButton.HasFocus,
            () => focus.Add("post"),
            () => Press(Key.Tab, Key.Tab),
            () => Dialog!.TitleField.HasFocus,
            () => focus.Add("title"),
            () => Press(Key.Tab),
            () => Field.HasFocus,
            () => focus.Add("field"),
            () => Press(Key.Tab.WithShift),
            () => Dialog!.TitleField.HasFocus,
            () =>
            {
                focus.Add("title");
                Assert.DoesNotContain('\t', Field.Text);
                Press(Key.Esc);
            });

        Assert.Equal(["post", "title", "field", "title"], focus);
    }

    [Fact]
    public void Esc_cancels_even_with_text_selected()
    {
        Run(
            () => Type("hello"),
            () => Field.Text == "hello",
            () => Press(Key.CursorLeft.WithShift, Key.CursorLeft.WithShift),
            () => Field.HasSelection,
            () => Press(Key.Esc),
            () => Dialog is null);

        Assert.Null(Host.Shell.StatusBar.Message);
    }

    [Fact]
    public void The_scroll_bar_shows_only_while_the_text_is_taller_than_the_field()
    {
        var shown = new List<bool>();
        Run(
            () => shown.Add(Field.VerticalScrollBar.Visible),
            () => { Field.Text = string.Join('\n', Enumerable.Range(1, 10)); },
            () => Field.VerticalScrollBar.Visible,
            () => shown.Add(true),
            () => { Field.Text = "fits"; },
            () => !Field.VerticalScrollBar.Visible,
            () =>
            {
                shown.Add(false);
                Press(Key.Esc);
            });

        Assert.Equal([false, true, false], shown);
    }

    [Fact]
    public void A_long_paragraph_wraps_instead_of_scrolling_sideways()
    {
        var words = string.Join(' ', Enumerable.Repeat("word", 30));
        Run(
            () => { Field.Text = words; },
            () => Row(Field, 1).Length > 0,
            () =>
            {
                Assert.Equal(0, Field.Viewport.X);
                Assert.False(Field.HorizontalScrollBar.Visible);
                Assert.StartsWith("word word", Row(Field, 1));
                Press(Key.Esc);
            });
    }

    [Fact]
    public void The_border_takes_the_focus_colour_only_while_the_field_has_focus()
    {
        var corners = new List<bool>();
        Run(
            () => { corners.Add(BorderCorner(Field) == Field.GetAttributeForRole(VisualRole.Focus)); },
            () => Press(Key.Tab.WithShift),
            () => Dialog!.TitleField.HasFocus,
            () => { },
            () =>
            {
                corners.Add(BorderCorner(Field) == Field.GetAttributeForRole(VisualRole.Normal));
                Press(Key.Esc);
            });

        Assert.Equal([true, true], corners);
    }

    [Fact]
    public void The_caret_is_a_bar()
    {
        CursorStyle? style = null;
        Run(() =>
        {
            style = Field.Cursor.Style;
            Press(Key.Esc);
        });

        Assert.Equal(CursorStyle.SteadyBar, style);
    }

    [Fact]
    public void Ctrl_Z_undoes_and_Ctrl_Y_redoes()
    {
        var texts = new List<string>();
        Run(
            () => Type("hi"),
            () => Field.Text == "hi",
            () => Press(Key.Z.WithCtrl),
            () => Field.Text != "hi",
            () => Press(Key.Y.WithCtrl),
            () => Field.Text == "hi",
            () =>
            {
                texts.Add(Field.Text);
                Press(Key.Esc);
            });

        Assert.Equal(["hi"], texts);
    }

    [Fact]
    public void Ctrl_arrows_move_by_word_and_Shift_arrows_select()
    {
        string? selected = null;
        Run(
            () => Type("one two"),
            () => Field.Text == "one two",
            () => Press(Key.CursorLeft.WithCtrl, Key.CursorRight.WithShift, Key.CursorRight.WithShift),
            () => Field.HasSelection,
            () =>
            {
                selected = Field.SelectedText;
                Press(Key.Esc);
            });

        Assert.Equal("tw", selected);
    }

    private const string Paragraph =
        "The quick brown fox jumps over the lazy dog and keeps running through the long grass beyond the river, " +
        "past the mill, over the bridge and up the hill until the sun goes down behind the far woods.";

    private record Landing(int Caret, string Selected);

    // From each start offset, presses the keys and records where the caret lands and what is selected.
    private List<Landing> Landings(string text, IEnumerable<(int Start, Key[] Keys)> cases, Action<string[]>? rows = null)
    {
        var landings = new List<Landing>();
        var steps = new List<Delegate> { () => { Field.Text = text; }, () => Row(Field, 0).Length > 0 };
        if (rows is not null)
            steps.Add(() => rows(Enumerable.Range(0, Field.Viewport.Height).Select(row => Row(Field, row)).ToArray()));
        foreach (var (start, keys) in cases)
        {
            steps.Add(() =>
            {
                Field.ClearSelection();
                Field.CaretOffset = start;
                Press(keys);
            });
            steps.Add(() => landings.Add(new Landing(Field.CaretOffset, Field.SelectedText)));
        }
        steps.Add(() => Press(Key.Esc));
        Run([.. steps]);
        return landings;
    }

    private (int Start, int End)[] VisibleRows()
    {
        string[] rows = [];
        Landings(Paragraph, [], seen => rows = seen.Where(row => row.Length > 0).ToArray());
        Assert.True(rows.Length >= 3, "the paragraph wraps over at least three rows");
        var at = 0;
        return rows.Select(row =>
        {
            var start = Paragraph.IndexOf(row, at, StringComparison.Ordinal);
            at = start + row.Length;
            return (start, at);
        }).ToArray();
    }

    [Fact]
    public void Home_and_End_go_to_the_start_and_end_of_the_row_you_see()
    {
        var rows = VisibleRows();
        var cases = new List<(int, Key[])>();
        var expected = new List<Landing>();
        foreach (var (start, end) in rows)
        foreach (var from in new[] { start, (start + end) / 2, end })
        {
            cases.Add((from, [Key.End]));
            expected.Add(new Landing(end, ""));
            cases.Add((from, [Key.End, Key.End]));
            expected.Add(new Landing(end, ""));
            cases.Add((from, [Key.Home]));
            expected.Add(new Landing(start, ""));
        }

        Assert.Equal(expected, Landings(Paragraph, cases));
    }

    [Fact]
    public void Shift_Home_and_Shift_End_select_to_the_start_and_end_of_the_row()
    {
        var rows = VisibleRows();
        var cases = new List<(int, Key[])>();
        var expected = new List<Landing>();
        foreach (var (start, end) in rows)
        {
            var middle = (start + end) / 2;
            cases.Add((middle, [Key.End.WithShift]));
            expected.Add(new Landing(end, Paragraph[middle..end]));
            cases.Add((middle, [Key.Home.WithShift]));
            expected.Add(new Landing(start, Paragraph[start..middle]));
            cases.Add((middle, [Key.End.WithShift, Key.Home.WithShift]));
            expected.Add(new Landing(start, Paragraph[start..middle]));
        }

        Assert.Equal(expected, Landings(Paragraph, cases));
    }

    [Fact]
    public void Ctrl_Home_and_Ctrl_End_go_to_the_start_and_end_of_the_text()
    {
        var middle = VisibleRows()[1].Start + 3;

        Assert.Equal(
            [
                new Landing(Paragraph.Length, ""),
                new Landing(0, ""),
                new Landing(Paragraph.Length, Paragraph[middle..]),
                new Landing(0, Paragraph[..middle]),
            ],
            Landings(Paragraph, [
                (middle, [Key.End.WithCtrl]),
                (middle, [Key.Home.WithCtrl]),
                (middle, [Key.End.WithCtrl.WithShift]),
                (middle, [Key.Home.WithCtrl.WithShift]),
            ]));
    }

    [Fact]
    public void Home_and_End_stay_put_on_an_empty_line()
    {
        Assert.Equal(
            [new Landing(4, ""), new Landing(4, "")],
            Landings("one\n\ntwo", [(4, [Key.Home]), (4, [Key.End])]));
    }

    [Fact]
    public void Option_arrows_and_Alt_B_and_F_move_by_word_like_Ctrl_arrows()
    {
        var jumps = Paragraph.IndexOf("jumps", StringComparison.Ordinal);
        var over = Paragraph.IndexOf(" over", StringComparison.Ordinal);
        var the = Paragraph.IndexOf("the lazy", StringComparison.Ordinal);
        Key[][] right = [[Key.CursorRight.WithCtrl], [Key.CursorRight.WithAlt], [Key.F.WithAlt]];
        Key[][] left = [[Key.CursorLeft.WithCtrl], [Key.CursorLeft.WithAlt], [Key.B.WithAlt]];
        Key[][] selectRight = [[Key.CursorRight.WithCtrl.WithShift], [Key.CursorRight.WithAlt.WithShift], [Key.F.WithAlt.WithShift]];
        Key[][] selectLeft = [[Key.CursorLeft.WithCtrl.WithShift], [Key.CursorLeft.WithAlt.WithShift], [Key.B.WithAlt.WithShift]];

        var landings = Landings(Paragraph, [
            .. right.Select(keys => (over, keys.Concat(keys).ToArray())),
            .. left.Select(keys => (over, keys)),
            .. selectRight.Select(keys => (over, keys)),
            .. selectLeft.Select(keys => (over, keys)),
        ]);

        Assert.Equal(
            [
                .. Enumerable.Repeat(landings[0], 3),
                .. Enumerable.Repeat(new Landing(jumps, ""), 3),
                .. Enumerable.Repeat(landings[6], 3),
                .. Enumerable.Repeat(new Landing(jumps, "jumps"), 3),
            ],
            landings);
        Assert.Equal(the, landings[0].Caret);
        Assert.Equal(Paragraph[over..landings[6].Caret], landings[6].Selected);
        Assert.True(landings[6].Caret > over);
    }

    [Fact]
    public void Ctrl_C_copies_the_selection_and_says_so_on_the_status_bar()
    {
        var platform = new TestClipboard();
        _host = new Host(platform);
        string? said = null;
        Run(
            () => Type("hello"),
            () => Field.Text == "hello",
            () => Press(Key.A.WithCtrl, Key.C.WithCtrl),
            () => (said = Host.Shell.StatusBar.Message) is not null,
            () => Press(Key.Esc));

        Assert.Equal("hello", platform.Text);
        Assert.Equal("Copied 1 line  •  5 characters", said);
    }

    [Fact]
    public void Ctrl_X_cuts_and_Ctrl_V_pastes_what_another_app_copied()
    {
        var platform = new TestClipboard();
        _host = new Host(platform);
        string? cut = null;
        Run(
            () => Type("hello"),
            () => Field.Text == "hello",
            () => Press(Key.A.WithCtrl, Key.X.WithCtrl),
            () => Field.Text.Length == 0,
            () =>
            {
                cut = platform.Text;
                platform.Text = "from elsewhere";
                Press(Key.V.WithCtrl);
            },
            () => Field.Text == "from elsewhere",
            () => Press(Key.Esc));

        Assert.Equal("hello", cut);
    }

    [Fact]
    public void A_copy_that_lands_nowhere_says_so_in_the_dialog_and_keeps_the_selection()
    {
        Run(
            () => Type("hello"),
            () => Field.Text == "hello",
            () => { Host.App.Driver!.Clipboard = new TestClipboard { Throw = new IOException("no clipboard tool found") }; },
            () => Press(Key.A.WithCtrl, Key.C.WithCtrl),
            () => Dialog!.Alert.Visible,
            () =>
            {
                Assert.Equal("Copy failed: no clipboard tool found (TestClipboard)", Dialog!.Alert.Message);
                Assert.Equal(SchemeNames.Error, Dialog.Alert.SchemeName);
                Assert.Equal("hello", Field.SelectedText);
                Press(Key.X.WithCtrl);
            },
            () =>
            {
                Assert.Equal("hello", Field.Text);
                Press(Key.Esc);
            });
    }
}
