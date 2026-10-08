using MentalDesk.Tui.Filtering;

namespace MentalDesk.Tui.Tests;

public class ListFilterTests
{
    private static IReadOnlyList<string> Apply(string query, params string[] rows) => ListFilter.Apply(query, rows, row => row);

    [Theory]
    [InlineData("iew")]
    [InlineData("IEW")]
    [InlineData("ammarPick")]
    public void A_row_containing_the_letters_matches_whatever_the_case(string query) =>
        Assert.Equal(["GrammarPickerView.cs"], Apply(query, "GrammarPickerView.cs", "Readme.md"));

    [Fact]
    public void Contains_matches_rank_after_every_camel_humps_match() =>
        Assert.Equal(["GoToEnd", "Tango", "Mongoose"], Apply("go", "Tango", "Mongoose", "GoToEnd"));

    [Fact]
    public void A_prefix_ranks_first_then_fewest_hump_jumps() =>
        Assert.Equal(["GotItRight", "AGoTo", "GreenOakTree"], Apply("got", "GreenOakTree", "AGoTo", "GotItRight"));

    [Fact]
    public void Ties_keep_the_list_order() =>
        Assert.Equal(["Go to themes", "Go to roles", "Go to preview"], Apply("gt", "Go to themes", "Go to roles", "Go to preview"));

    [Fact]
    public void An_empty_query_keeps_every_row_in_order() =>
        Assert.Equal(["b", "a", "c"], Apply("", "b", "a", "c"));

    [Fact]
    public void A_query_with_a_slash_matches_path_segments_by_camel_humps_only()
    {
        Assert.Equal(["src/Navigation/OpenView.cs"], Apply("nav/ov", "src/Navigation/OpenView.cs", "docs/OpenView.cs"));
        Assert.Empty(Apply("ion/OpenView", "src/Navigation/OpenView.cs"));
    }
}
