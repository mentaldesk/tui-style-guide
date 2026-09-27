namespace MentalDesk.Tui.Focus;

// The one record of which region has the keys. Terminal.Gui moves focus on its own, so Reconcile re-reads it.
public sealed class FocusTracker(Func<object?> focusedView)
{
    private sealed record Target(FocusRegion Region, Func<bool> Move, Func<object?, bool> Owns);

    private readonly List<Target> _targets = [];

    public FocusRegion? Region { get; private set; }

    public FocusRegion? Unreachable { get; private set; }

    public IReadOnlyList<FocusRegion> Regions => [.. _targets.Select(target => target.Region)];

    public event EventHandler<FocusRegion>? RegionChanged;

    public void Register(FocusRegion region, Func<bool> move, Func<object?, bool> owns) =>
        _targets.Add(new Target(region, move, owns));

    public bool Focus(FocusRegion region)
    {
        if (_targets.FirstOrDefault(target => target.Region == region) is not { } target) return false;
        // SetFocus reports false when the view already has focus, which is a move that landed.
        if (!target.Move() && !target.Owns(focusedView()))
        {
            Unreachable = region;
            return false;
        }
        Unreachable = null;
        Record(region);
        return true;
    }

    // Call every iteration and before every key: a click moves focus in between.
    public void Reconcile()
    {
        var focused = focusedView();
        if (_targets.FirstOrDefault(target => target.Region == Region) is { } current && current.Owns(focused))
            return;
        if (_targets.FirstOrDefault(target => target.Owns(focused)) is { } owner)
            Record(owner.Region);
    }

    private void Record(FocusRegion region)
    {
        if (region == Region) return;
        Region = region;
        RegionChanged?.Invoke(this, region);
    }
}
