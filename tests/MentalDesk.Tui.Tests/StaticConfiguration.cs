using Terminal.Gui.Configuration;

namespace MentalDesk.Tui.Tests;

[CollectionDefinition("StaticConfiguration", DisableParallelization = true)]
public sealed class StaticConfigurationCollection;

// Application and ThemeManager are process-wide, and Terminal.Gui's renderer throws if a parallel test changes the theme.
[Collection("StaticConfiguration")]
public abstract class StaticConfigurationTest : IDisposable
{
    private readonly string _theme;

    protected StaticConfigurationTest()
    {
        MentalDesk.Tui.Theming.Themes.Load();
        _theme = ThemeManager.Theme;
    }

    public virtual void Dispose()
    {
        MentalDesk.Tui.Theming.Themes.Apply(_theme);
        GC.SuppressFinalize(this);
    }
}
