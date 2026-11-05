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

    private readonly View _row;
    private readonly Func<FocusRegion?> _focused;
    private readonly Action<string> _say;
    private readonly Func<JsonObject?> _read;
    private readonly Action<JsonObject> _write;
    private readonly List<Pane> _panes = [];
    private Pane? _main;
    private IMouse? _mouse;
    private Pane? _dragging;
    private bool _clickAfterDrag;

    // Widths are kept in the app's settings section.
    public SidePanes(AppShell shell, View row, string app)
        : this(row, () => shell.Focus.Region, message => shell.ShowMessage(message),
            () => shell.Settings.Section(app)[Section] as JsonObject,
            widths =>
            {
                var section = shell.Settings.Section(app);
                section[Section] = widths;
                shell.Settings.SaveSection(app, section);
            })
    {
        shell.Commands
            .Register(WidenId, "Widen pane", Widen, isEnabled: () => CanResize)
            .Register(NarrowId, "Narrow pane", Narrow, isEnabled: () => CanResize)
            .Register(ResetId, "Reset pane width", Reset, isEnabled: () => CanResize);
    }

    // For an app without an AppShell: it registers Widen, Narrow and Reset itself, and keeps the widths where it likes.
    public SidePanes(View row, Func<FocusRegion?> focused, Action<string> say, Func<JsonObject?> read, Action<JsonObject> write)
    {
        _row = row;
        _focused = focused;
        _say = say;
        _read = read;
        _write = write;
        // A view has no App until it's in a running one.
        _row.SubViewsLaidOut += (_, _) => FollowMouse();
        _row.Disposing += (_, _) =>
        {
            if (_mouse is not null) _mouse.MouseEvent -= OnMouse;
        };
    }

    public bool CanResize => Focused() is not null;

    public View? Main
    {
        get => _main?.View;
        set
        {
            _main = _panes.Single(pane => pane.View == value);
            _row.SetNeedsLayout();
        }
    }

    // In reading order, left to right.
    public SidePanes AddMain(View view)
    {
        Place(new Pane(view, null, 0));
        _main ??= _panes[^1];
        _row.SetNeedsLayout();
        return this;
    }

    public SidePanes AddSide(View view, FocusRegion region, int defaultWidth)
    {
        Place(new Pane(view, region, defaultWidth) { Chosen = Saved(region) ?? defaultWidth });
        _row.SetNeedsLayout();
        return this;
    }

    private void Place(Pane pane)
    {
        _panes.Add(pane);
        pane.View.X = Pos.Func(_ => Widths()[pane].X);
        pane.View.Width = Dim.Func(_ => Widths()[pane].Width);
        pane.View.VisibleChanged += (_, _) => _row.SetNeedsLayout();
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

    private void FollowMouse()
    {
        if (_mouse is not null || _row.App is not { } app) return;
        _mouse = app.Mouse;
        _mouse.MouseEvent += OnMouse;
    }

    // Terminal.Gui's own drag takes focus and shows its arrange buttons, so the drag is handled before any view sees the mouse.
    private void OnMouse(object? sender, Mouse mouse)
    {
        // Terminal.Gui follows a release with a click where the pointer is, which would move focus there.
        if (_clickAfterDrag && mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            _clickAfterDrag = false;
            mouse.Handled = true;
            return;
        }
        _clickAfterDrag = false;

        if (_dragging is { } pane)
        {
            if (mouse.Flags.HasFlag(MouseFlags.LeftButtonReleased))
            {
                _dragging = null;
                _clickAfterDrag = true;
                Choose(pane, DraggedWidth(pane, mouse.ScreenPosition.X));
            }
            else if (mouse.Flags.HasFlag(MouseFlags.LeftButtonPressed))
            {
                pane.Chosen = DraggedWidth(pane, mouse.ScreenPosition.X);
                _row.SetNeedsLayout();
            }
            else
            {
                // The button went up out of sight, so the next press isn't part of this drag.
                _dragging = null;
                Choose(pane, pane.Chosen);
                return;
            }
            mouse.Handled = true;
            return;
        }

        if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonPressed)) return;
        _dragging = _panes.FirstOrDefault(side => OnInnerBorder(side, mouse));
        mouse.Handled = _dragging is not null;
    }

    // Terminal.Gui reports the deepest view under the pointer, so a dialog over the border isn't the pane's own border.
    private bool OnInnerBorder(Pane pane, Mouse mouse)
    {
        if (pane.Region is null || pane == _main || !pane.View.Visible) return false;
        if (mouse.View is not AdornmentView { Adornment: { } adornment } || !ReferenceEquals(adornment.Parent, pane.View)) return false;
        var frame = pane.View.FrameToScreen();
        return mouse.ScreenPosition.X == (FacesRight(pane) ? frame.Right - 1 : frame.Left);
    }

    private bool FacesRight(Pane pane) => _main is null || _panes.IndexOf(pane) < _panes.IndexOf(_main);

    private int DraggedWidth(Pane pane, int column)
    {
        var frame = pane.View.FrameToScreen();
        var width = FacesRight(pane) ? column - frame.Left + 1 : frame.Right - column;
        return Math.Clamp(width, MinimumWidth, Math.Max(MinimumWidth, Widest(pane)));
    }

    private int Widest(Pane pane)
    {
        var layout = Widths();
        var others = layout.Where(entry => entry.Key != pane && entry.Key != _main).Sum(entry => entry.Value.Width);
        return _row.Viewport.Width - others - (_main?.View.Visible == true ? MinimumMainWidth : 0);
    }

    public void Widen() => Resize(Step);

    public void Narrow() => Resize(-Step);

    public void Reset()
    {
        if (Focused() is not { } pane) return;
        Choose(pane, pane.Default);
    }

    // A width chosen outside the panes, such as on a settings page. A narrow terminal shrinks what's drawn, not this.
    public void SetWidth(View view, int width) =>
        Choose(_panes.Single(pane => pane.View == view && pane.Region is not null), Math.Max(MinimumWidth, width));

    private void Resize(int step)
    {
        if (Focused() is not { } pane) return;
        var shown = Widths()[pane].Width;
        var width = step > 0 ? Math.Min(shown + step, Math.Max(shown, Widest(pane))) : Math.Max(shown + step, Math.Min(shown, MinimumWidth));
        if (width == shown)
        {
            _say($"{pane.Region!.Name} is as {(step > 0 ? "wide" : "narrow")} as it can be");
            return;
        }
        Choose(pane, width);
    }

    private void Choose(Pane pane, int width)
    {
        pane.Chosen = width;
        _row.SetNeedsLayout();
        var widths = _read()?.DeepClone() as JsonObject ?? [];
        if (width == pane.Default)
            widths.Remove(pane.Region!.Name);
        else
            widths[pane.Region!.Name] = width;
        _write(widths);
    }

    private int? Saved(FocusRegion region) =>
        _read()?[region.Name] is JsonValue value && value.TryGetValue<int>(out var width)
            && width >= MinimumWidth
            ? width
            : null;

    private Pane? Focused() =>
        _panes.FirstOrDefault(pane => pane.Region is not null && pane.Region == _focused() && pane != _main && pane.View.Visible);

    private sealed class Pane(View view, FocusRegion? region, int defaultWidth)
    {
        public View View { get; } = view;

        public FocusRegion? Region { get; } = region;

        public int Default { get; } = defaultWidth;

        public int Chosen { get; set; } = defaultWidth;
    }
}
