using MentalDesk.Tui.Columns;

namespace MentalDesk.Tui.Tests;

public class RowLayoutTests
{
    private sealed record Spend(string What, string Runs, string Cost);

    private static readonly RowLayout<Spend> Spending = new(
        Column<Spend>.Name("What", spend => spend.What, minWidth: 8),
        Column<Spend>.Number("Runs", spend => spend.Runs),
        Column<Spend>.Number("Cost", spend => spend.Cost));

    private static readonly Spend[] Rows =
    [
        new("Pitching #42 Narrow terminals", "3", "$2.10"),
        new("Building #43 Settings screen", "11", "$9.85"),
    ];

    // What 29, Runs 4, Cost 5, with two gaps of 2.
    private const int Natural = 29 + 2 + 4 + 2 + 5;

    private static ColumnFit<Spend> Fit(int width) => Spending.Fit(Rows, width);

    private static string[] Headers(ColumnFit<Spend> fit) => [.. fit.Placed.Select(placed => placed.Column.Header)];

    [Theory]
    [InlineData(-1, "")]
    [InlineData(0, "")]
    [InlineData(1, "a")]
    [InlineData(2, "a…")]
    public void The_end_cut_guards_narrow_widths_and_never_leaves_a_lone_ellipsis(int width, string expected) =>
        Assert.Equal(expected, Truncate.End("abc", width));

    [Fact]
    public void Text_exactly_the_width_is_whole_and_one_over_loses_its_last_two_for_the_ellipsis()
    {
        Assert.Equal("abcdef", Truncate.End("abcdef", 6));
        Assert.Equal("abcd…", Truncate.End("abcdef", 5));
        Assert.Equal("…cdef", Truncate.Start("abcdef", 5));
    }

    [Fact]
    public void A_path_is_cut_at_its_start_so_the_last_folder_stays()
    {
        Assert.Equal("…/code/TuiCode/side-pane-resize", Truncate.Start("/Volumes/work/code/TuiCode/side-pane-resize", 31));
        Assert.Equal("~/code/a-team/main", Truncate.Start("~/code/a-team/main", 18));
        Assert.Equal("e", Truncate.Start("side", 1));
        Assert.Equal(string.Empty, Truncate.Start("side", 0));
    }

    [Fact]
    public void Wide_characters_count_as_two_columns_and_are_never_split()
    {
        Assert.Equal(4, Truncate.Width("日本"));
        Assert.Equal("日本", Truncate.End("日本", 4));
        Assert.Equal("日…", Truncate.End("日本語", 4));
        Assert.Equal("…", Truncate.End("日本語", 2));
        Assert.Equal("…語", Truncate.Start("日本語", 4));
        Assert.True(Truncate.Width(Truncate.End("a日本語", 4)) <= 4);
    }

    [Fact]
    public void With_room_every_column_takes_its_widest_cell_two_apart_and_numbers_align_right()
    {
        var fit = Fit(Natural);

        Assert.Equal([0, 31, 37], fit.Placed.Select(placed => placed.X));
        Assert.Equal("What                           Runs   Cost", fit.Header);
        Assert.Equal("Pitching #42 Narrow terminals     3  $2.10", fit.Row(Rows[0]));
        Assert.Equal("Building #43 Settings screen     11  $9.85", fit.Row(Rows[1]));
    }

    [Fact]
    public void The_last_column_does_not_stretch_into_spare_width()
    {
        Assert.Equal(Fit(Natural).Placed, Fit(Natural + 30).Placed);
        Assert.Equal(Natural, Fit(Natural + 30).Row(Rows[0]).Length);
    }

    [Fact]
    public void The_name_shrinks_to_its_minimum_with_an_ellipsis_before_any_column_drops()
    {
        var fit = Fit(Natural - 10);

        Assert.Equal(["What", "Runs", "Cost"], Headers(fit));
        Assert.Equal("Pitching #42 Narro…     3  $2.10", fit.Row(Rows[0]));

        var floor = Fit(8 + 2 + 4 + 2 + 5);
        Assert.Equal(["What", "Runs", "Cost"], Headers(floor));
        Assert.Equal("Pitchin…     3  $2.10", floor.Row(Rows[0]));
    }

    [Fact]
    public void Below_the_names_minimum_columns_drop_from_the_right_and_a_number_is_never_cut()
    {
        var fit = Fit(8 + 2 + 4 + 2 + 4);

        Assert.Equal(["What", "Runs"], Headers(fit));
        Assert.Equal("Pitching #42 …     3", fit.Row(Rows[0]));
    }

    [Fact]
    public void A_lone_column_has_no_header_and_is_cut_to_fit()
    {
        var fit = Fit(5);

        Assert.Equal(["What"], Headers(fit));
        Assert.False(fit.ShowsHeader);
        Assert.Null(fit.Header);
        Assert.Equal("Pitc…", fit.Row(Rows[0]));
    }

    [Fact]
    public void Widening_brings_the_columns_back_in_the_reverse_order()
    {
        var dropped = Enumerable.Range(0, Natural + 1).Select(width => Headers(Fit(width)).Length).ToList();

        Assert.Equal(dropped.Order(), dropped);
        Assert.Equal(3, dropped[^1]);
        Assert.Equal(1, dropped[0]);
    }

    [Fact]
    public void A_fixed_width_holds_whatever_the_rows_hold_and_a_name_is_cut_to_it()
    {
        var layout = new RowLayout<Spend>(
            Column<Spend>.Name("What", spend => spend.What) with { Width = 10 },
            Column<Spend>.Text("Runs", spend => spend.Runs) with { Width = 6 });

        var fit = layout.Fit(Rows, 80);

        Assert.Equal([0, 12], fit.Placed.Select(placed => placed.X));
        Assert.Equal("Pitching …  3", fit.Row(Rows[0]));
    }
}
