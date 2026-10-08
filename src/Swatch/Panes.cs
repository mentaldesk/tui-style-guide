using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Shell;

namespace Swatch;

internal static class Panes
{
    public static FrameView Create(string title, View content, Pos x, Dim width)
    {
        var pane = new FrameView
        {
            Title = title, X = x, Width = width, Height = Dim.Fill(), CanFocus = true, BorderStyle = LineStyle.Single,
            TabStop = TabBehavior.TabStop,
        };
        pane.Add(content);
        return pane;
    }

    public static void Track(AppShell shell, FocusRegion region, FrameView pane, View content)
    {
        var border = new FocusBorder(pane);
        shell.Focus.Register(region, () => content.SetFocus(), view => Within(view as View, pane));
        shell.Focus.RegionChanged += (_, focused) => border.Show(focused == region);
    }

    private static bool Within(View? view, View pane)
    {
        for (; view is not null; view = view.SuperView)
            if (view == pane) return true;
        return false;
    }
}
