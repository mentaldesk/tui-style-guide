using MentalDesk.Tui.Columns;
using Swatch;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class ColumnTableTests : StaticConfigurationTest
{
    private readonly IApplication _app = Application.Create().Init(DriverRegistry.Names.ANSI);

    public override void Dispose()
    {
        _app.Dispose();
        base.Dispose();
    }

    private static readonly Times Times = new(new FakeClock());

    private static readonly RowLayout<RowsWithColumnsDialog.Spend> SpendColumns = RowsWithColumnsDialog.SpendColumns(Times);

    private static IReadOnlyList<RowsWithColumnsDialog.Spend> Spending =>
    [
        new("Pitching #42 Narrow terminals", 3, 41, 2.10m, Times.Clock.GetUtcNow()),
        new("Building #43 Settings screen", 11, 187, 9.85m, Times.Clock.GetUtcNow().AddDays(-10)),
    ];

    private ColumnTable<RowsWithColumnsDialog.Spend> Table(int width)
    {
        _app.Driver!.SetScreenSize(width, 5);
        var table = new ColumnTable<RowsWithColumnsDialog.Spend>(SpendColumns, Spending)
        {
            App = _app, Width = width, Height = 5,
        };
        table.BeginInit();
        table.EndInit();
        table.Layout();
        var driver = _app.Driver!;
        driver.ClearContents();
        driver.Clip = new Region(driver.Screen);
        table.SetNeedsDraw();
        table.Draw();
        return table;
    }

    private string Line(int row, int width) =>
        string.Concat(Enumerable.Range(0, width).Select(col => _app.Driver!.Contents![row, col].Grapheme)).TrimEnd();

    [Theory]
    [InlineData(80)]
    [InlineData(40)]
    [InlineData(24)]
    [InlineData(12)]
    public void It_draws_the_same_columns_as_a_list_laid_out_from_the_same_description(int width)
    {
        using var table = Table(width);
        var fit = SpendColumns.Fit(Spending, width);

        var lines = fit.ShowsHeader ? [fit.Header!, .. Spending.Select(fit.Row)] : Spending.Select(fit.Row).ToArray();
        for (var i = 0; i < lines.Length; i++)
            Assert.Equal(lines[i], Line(i, width));
    }

    [Fact]
    public void Narrow_it_cuts_the_name_with_an_ellipsis_and_drops_the_last_column_rather_than_cut_a_number()
    {
        using var table = Table(34);

        Assert.Equal("What                 Runs  Minutes", Line(0, 34));
        Assert.Equal("Pitching #42 Narro…     3       41", Line(1, 34));
    }

    [Fact]
    public void Narrowing_drops_last_run_before_cost_and_never_cuts_an_age()
    {
        Assert.Equal(["What", "Runs", "Minutes", "Cost", "Last run"], SpendColumns.Fit(Spending, 46).Placed.Select(placed => placed.Column.Header));
        Assert.EndsWith("   7 Oct", SpendColumns.Fit(Spending, 46).Row(Spending[1]), StringComparison.Ordinal);
        Assert.Equal(["What", "Runs", "Minutes", "Cost"], SpendColumns.Fit(Spending, 45).Placed.Select(placed => placed.Column.Header));
    }

    [Fact]
    public void Its_header_is_dimmed_with_no_underline_and_it_has_no_grid_lines()
    {
        using var table = Table(80);

        Assert.False(table.Style.ShowHorizontalHeaderUnderline);
        Assert.False(table.Style.ShowHorizontalHeaderOverline);
        Assert.False(table.Style.ShowVerticalCellLines);
        Assert.False(table.Style.ShowVerticalHeaderLines);
        Assert.False(table.Style.ExpandLastColumn);
        Assert.Equal(table.GetAttributeForRole(VisualRole.Disabled).Foreground, _app.Driver!.Contents![0, 0].Attribute!.Value.Foreground);
        Assert.NotEqual(table.GetAttributeForRole(VisualRole.Disabled).Foreground, _app.Driver.Contents[2, 0].Attribute!.Value.Foreground);
    }

    [Fact]
    public void Each_column_takes_its_alignment_minimum_and_ellipsis_from_the_description()
    {
        using var table = Table(80);

        var what = table.Style.GetColumnStyleIfAny(0)!;
        var cost = table.Style.GetColumnStyleIfAny(3)!;
        Assert.Equal(Alignment.Start, what.Alignment);
        Assert.Equal(Alignment.End, cost.Alignment);
        Assert.Equal(14, what.MinAcceptableWidth);
        Assert.Equal("…", what.TruncationIndicator);
        Assert.Equal("…", cost.TruncationIndicator);
    }
}
