using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Focus;

namespace MentalDesk.Tui.Tests;

public class FocusTrackerTests
{
    private static readonly FocusRegion Left = new("Left", CommandScope.Global);
    private static readonly FocusRegion Right = new("Right", CommandScope.Global);

    private object? _focused;
    private readonly FocusTracker _focus;

    public FocusTrackerTests()
    {
        _focus = new FocusTracker(() => _focused);
        _focus.Register(Left, () => Move("left"), view => view is "left");
        _focus.Register(Right, () => Move("right"), view => view is "right");
    }

    private bool Move(string view)
    {
        _focused = view;
        return true;
    }

    [Fact]
    public void A_move_that_lands_is_recorded()
    {
        var changes = new List<FocusRegion>();
        _focus.RegionChanged += (_, region) => changes.Add(region);

        Assert.True(_focus.Focus(Right));

        Assert.Equal(Right, _focus.Region);
        Assert.Equal([Right], changes);
    }

    [Fact]
    public void A_move_that_doesnt_land_leaves_the_region_and_says_where_it_couldnt_go()
    {
        var focus = new FocusTracker(() => null);
        focus.Register(Left, () => false, _ => false);

        Assert.False(focus.Focus(Left));

        Assert.Null(focus.Region);
        Assert.Equal(Left, focus.Unreachable);
    }

    [Fact]
    public void Reconcile_follows_a_move_the_framework_made_on_its_own()
    {
        _focus.Focus(Left);
        _focused = "right";

        _focus.Reconcile();

        Assert.Equal(Right, _focus.Region);
    }

    [Fact]
    public void Reconcile_keeps_the_region_while_focus_is_somewhere_no_region_owns()
    {
        _focus.Focus(Left);
        _focused = "a dialog";

        _focus.Reconcile();

        Assert.Equal(Left, _focus.Region);
    }
}
