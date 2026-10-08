using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Tests;

public class NestedShellTests : StaticConfigurationTest
{
    private static readonly FocusRegion Main = new("Main", new CommandScope("Main"));

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    [Fact]
    public void A_nested_shell_has_the_keys_and_the_outer_one_gets_its_theme_focus_and_hints_back()
    {
        var outer = _host.Shell;
        var outerRan = 0;
        outer.Commands.Register("outer.run", "Run", () => outerRan++);
        outer.Keys.Bind("Ctrl+X", "outer.run");
        outer.HintsFor = _ => [new Hint("outer.run", "run")];
        using var window = new AppWindow(outer);
        outer.Focus.Register(Main, () => window.Body.SetFocus(), view => view == window.Body);

        using var inner = new AppShell(_host.App, outer.Cursor);
        inner.Commands.Register("inner.back", "Back", () => _host.App.RequestStop());
        inner.Keys.Bind("Esc", "inner.back");
        using var innerWindow = new AppWindow(inner);
        var themeInside = "";

        _host.Run(window,
            () => outer.Focus.Focus(Main),
            () => outer.RunNested(inner, innerWindow, Themes.Daylight),
            () => _host.App.TopRunnableView == innerWindow,
            () =>
            {
                themeInside = Themes.Current;
                _host.App.InjectKey(Key.X.WithCtrl);
                _host.App.InjectKey(Key.Esc);
            },
            () => _host.App.TopRunnableView == window);

        Assert.Equal(Themes.Daylight, themeInside);
        Assert.Equal(0, outerRan);
        Assert.Equal(Themes.Default, Themes.Current);
        Assert.Equal(Main, outer.Focus.Region);
        Assert.Equal($"Ctrl+X run{HintRow.Separator}F1 keys", outer.StatusBar.Says);
    }
}
