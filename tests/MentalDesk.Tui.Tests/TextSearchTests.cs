using MentalDesk.Tui.Finding;

namespace MentalDesk.Tui.Tests;

public class TextSearchTests
{
    private static readonly IReadOnlyList<TextMatch> Matches = TextSearch.FindAll(["a Retry", "none", "retry retry"], "RETRY");

    [Fact]
    public void Finds_every_match_in_reading_order_whatever_its_case() =>
        Assert.Equal([new TextMatch(0, 2, 5), new TextMatch(2, 0, 5), new TextMatch(2, 6, 5)], Matches);

    [Fact]
    public void An_empty_query_finds_nothing() => Assert.Empty(TextSearch.FindAll(["retry"], ""));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 1, 2)]
    [InlineData(2, 7, 0)]
    public void At_or_after_a_place_wraps_to_the_first(int row, int column, int index) =>
        Assert.Equal(index, TextSearch.IndexAtOrAfter(Matches, (row, column)));

    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(2, 6, 0)]
    public void After_a_match_wraps_to_the_first(int row, int column, int index) =>
        Assert.Equal(index, TextSearch.IndexAfter(Matches, (row, column)));

    [Theory]
    [InlineData(2, 6, 1)]
    [InlineData(0, 2, 2)]
    public void Before_a_match_wraps_to_the_last(int row, int column, int index) =>
        Assert.Equal(index, TextSearch.IndexBefore(Matches, (row, column)));
}
