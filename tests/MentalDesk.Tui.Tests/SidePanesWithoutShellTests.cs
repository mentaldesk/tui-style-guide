using System.Drawing;
using System.Text.Json.Nodes;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Layout;

namespace MentalDesk.Tui.Tests;

public class SidePanesWithoutShellTests : StaticConfigurationTest
{
    private static readonly FocusRegion Body = new("Body", new CommandScope("Body"));
    private static readonly FocusRegion History = new("History", new CommandScope("History"));

    private readonly View _row = new();
    private readonly View _body = new();
    private readonly View _history = new();
    private readonly List<string> _said = [];
    private FocusRegion? _focused = History;
    private JsonObject? _file;

    private SidePanes Panes(int columns = 120)
    {
        var panes = new SidePanes(_row, () => _focused, _said.Add, () => _file, written => _file = written)
            .AddMain(_body)
            .AddSide(_history, History, 48);
        _row.Add(_body, _history);
        Lay(columns);
        return panes;
    }

    private void Lay(int columns) => Lay(_row, columns);

    private static void Lay(View row, int columns)
    {
        row.Width = columns;
        row.Height = 10;
        row.Layout(new Size(columns, 10));
    }

    public override void Dispose()
    {
        _row.Dispose();
        base.Dispose();
    }

    [Fact]
    public void Widen_and_Narrow_resize_the_focused_pane_and_write_its_width_through_the_app()
    {
        var panes = Panes();

        panes.Widen();
        Lay(120);

        Assert.Equal(53, _history.Frame.Width);
        Assert.Equal(120 - 53, _body.Frame.Width);
        Assert.Equal(53, (int)_file!["History"]!);

        panes.Narrow();
        panes.Narrow();
        Lay(120);

        Assert.Equal(43, _history.Frame.Width);
    }

    [Fact]
    public void A_width_the_app_kept_is_used_and_one_too_narrow_is_ignored()
    {
        _file = new JsonObject { ["History"] = 30 };
        Panes();
        Assert.Equal(30, _history.Frame.Width);

        _file = new JsonObject { ["History"] = 3 };
        using var row = new View();
        using var body = new View();
        using var history = new View();
        new SidePanes(row, () => _focused, _said.Add, () => _file, written => _file = written).AddMain(body).AddSide(history, History, 48);
        row.Add(body, history);
        Lay(row, 120);
        Assert.Equal(48, history.Frame.Width);
    }

    [Fact]
    public void At_a_limit_it_says_so_through_the_app()
    {
        var panes = Panes();

        for (var i = 0; i < 20; i++)
            panes.Widen();
        Lay(120);

        Assert.Equal(SidePanes.MinimumMainWidth, _body.Frame.Width);
        Assert.Equal("History is as wide as it can be", _said[^1]);
    }

    [Fact]
    public void Reset_forgets_the_width()
    {
        _file = new JsonObject { ["History"] = 30 };
        var panes = Panes();

        panes.Reset();
        Lay(120);

        Assert.Equal(48, _history.Frame.Width);
        Assert.Empty(_file!);
    }

    [Fact]
    public void It_can_resize_only_while_the_app_says_a_side_pane_has_focus()
    {
        var panes = Panes();
        Assert.True(panes.CanResize);

        _focused = Body;
        Assert.False(panes.CanResize);

        _focused = null;
        Assert.False(panes.CanResize);
        panes.Widen();
        Assert.Null(_file);
        Assert.Empty(_said);
    }
}
