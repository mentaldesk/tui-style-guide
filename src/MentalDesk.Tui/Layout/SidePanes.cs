using System.Text.Json.Nodes;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Layout;

// Panes side by side: side panes the user resizes, and the main pane, which takes what's left.
public sealed class SidePanes
{
    public const string WidenId = "app.widenPane";
    public const string NarrowId = "app.narrowPane";
    public const string ResetId = "app.resetPaneWidth";
    public const int Step = 5;
    public const int MinimumWidth = 15;
    public const int MinimumMainWidth = 40;
    public const string Section = "paneWidths";

    private readonly AppShell _shell;
    private readonly View _row;
    private readonly string _app;
    private readonly List<Pane> _panes = [];
    private Pane? _main;

    // Widths are kept in the app's settings section.
    public SidePanes(AppShell shell, View row, string app)
    {
        _shell = shell;
        _row = row;
        _app = app;
        shell.Commands
            .Register(WidenId, "Widen pane", () => Resize(Step), isEnabled: () => Focused() is not null)
            .Register(NarrowId, "Narrow pane", () => Resize(-Step), isEnabled: () => Focused() is not null)
            .Register(ResetId, "Reset pane width", Reset, isEnabled: () => Focused() is not null);
    }

    public View? Main
    {
        get => _main?.View;
        set
        {
            _main = _panes.Single(pane => pane.View == value);
            Arrange();
        }
    }

    // In reading order, left to right.
    public SidePanes AddMain(View view)
    {
        Place(new Pane(view, null, 0));
        _main ??= _panes[^1];
        Arrange();
        return this;
    }

    public SidePanes AddSide(View view, FocusRegion region, int defaultWidth)
    {
        Place(new Pane(view, region, defaultWidth) { Chosen = Saved(region) ?? defaultWidth });
        Arrange();
        return this;
    }

    private void Place(Pane pane)
    {
        _panes.Add(pane);
        pane.View.X = Pos.Func(_ => Widths()[pane].X);
        pane.View.Width = Dim.Func(_ => Widths()[pane].Width);
        pane.View.SubViewsLaidOut += (_, _) => Dragged(pane);
        pane.View.VisibleChanged += (_, _) => Arrange();
    }

    private Dictionary<Pane, (int X, int Width)> Widths()
    {
        var shown = _panes.Where(pane => pane.View.Visible).ToList();
        var main = shown.Contains(_main!) ? _main : null;
        var widths = shown.ToDictionary(pane => pane, pane => pane == main ? 0 : pane.Chosen);
        var room = _row.Viewport.Width;
        GiveWay(widths, main, room - (main is null ? 0 : MinimumMainWidth), MinimumWidth);
        GiveWay(widths, main, room, 0);
        if (main is not null)
            widths[main] = Math.Max(0, room - widths.Values.Sum());
        var layout = new Dictionary<Pane, (int, int)>();
        var x = 0;
        foreach (var pane in _panes)
        {
            var width = widths.GetValueOrDefault(pane);
            layout[pane] = (x, width);
            x += width;
        }
        return layout;
    }

    // The widest side pane gives a column at a time, so a narrow one stays readable.
    private static void GiveWay(Dictionary<Pane, int> widths, Pane? main, int room, int floor)
    {
        while (widths.Values.Sum() > room
            && widths.Where(entry => entry.Key != main && entry.Value > floor).OrderByDescending(entry => entry.Value).FirstOrDefault() is { Key: { } widest })
            widths[widest]--;
    }

    // A side pane's inner border is the one facing the main pane.
    private void Arrange()
    {
        var mainAt = _main is null ? _panes.Count : _panes.IndexOf(_main);
        for (var i = 0; i < _panes.Count; i++)
        {
            _panes[i].View.Arrangement = i == mainAt || _panes[i].Region is null ? ViewArrangement.Fixed
                : i < mainAt ? ViewArrangement.RightResizable : ViewArrangement.LeftResizable;
        }
        _row.SetNeedsLayout();
    }

    // Terminal.Gui's drag sets the frame, which replaces the pane's X and Width with absolute ones, then lays it out.
    private void Dragged(Pane pane)
    {
        if (pane.View.Width is not DimAbsolute) return;
        var width = Math.Clamp(pane.View.Frame.Width, MinimumWidth, Math.Max(MinimumWidth, Widest(pane)));
        pane.View.X = Pos.Func(_ => Widths()[pane].X);
        pane.View.Width = Dim.Func(_ => Widths()[pane].Width);
        Choose(pane, width);
    }

    private int Widest(Pane pane)
    {
        var layout = Widths();
        var others = layout.Where(entry => entry.Key != pane && entry.Key != _main).Sum(entry => entry.Value.Width);
        return _row.Viewport.Width - others - (_main?.View.Visible == true ? MinimumMainWidth : 0);
    }

    private void Resize(int step)
    {
        if (Focused() is not { } pane) return;
        var shown = Widths()[pane].Width;
        var width = step > 0 ? Math.Min(shown + step, Math.Max(shown, Widest(pane))) : Math.Max(shown + step, Math.Min(shown, MinimumWidth));
        if (width == shown)
        {
            _shell.ShowMessage($"{pane.Region!.Name} is as {(step > 0 ? "wide" : "narrow")} as it can be");
            return;
        }
        Choose(pane, width);
    }

    private void Reset()
    {
        if (Focused() is not { } pane) return;
        Choose(pane, pane.Default);
    }

    private void Choose(Pane pane, int width)
    {
        pane.Chosen = width;
        _row.SetNeedsLayout();
        var section = _shell.Settings.Section(_app);
        var widths = section[Section] as JsonObject ?? [];
        if (width == pane.Default)
            widths.Remove(pane.Region!.Name);
        else
            widths[pane.Region!.Name] = width;
        section[Section] = widths;
        _shell.Settings.SaveSection(_app, section);
    }

    private int? Saved(FocusRegion region) =>
        _shell.Settings.Section(_app)[Section]?[region.Name] is JsonValue value && value.TryGetValue<int>(out var width)
            && width >= MinimumWidth
            ? width
            : null;

    private Pane? Focused() =>
        _panes.FirstOrDefault(pane => pane.Region is not null && pane.Region == _shell.Focus.Region && pane != _main && pane.View.Visible);

    private sealed class Pane(View view, FocusRegion? region, int defaultWidth)
    {
        public View View { get; } = view;

        public FocusRegion? Region { get; } = region;

        public int Default { get; } = defaultWidth;

        public int Chosen { get; set; } = defaultWidth;
    }
}
