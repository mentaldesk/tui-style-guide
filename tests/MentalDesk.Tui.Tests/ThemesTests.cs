using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Tests;

public class ThemesTests : StaticConfigurationTest
{
    public static TheoryData<string> Bundled => [.. Themes.Bundled];

    [Theory]
    [MemberData(nameof(Bundled))]
    public void Every_theme_defines_every_scheme(string theme)
    {
        Assert.Equal(SchemeNames.All.Order(), Themes.SchemesOf(theme).Keys.Where(SchemeNames.All.Contains).Order());
    }

    [Theory]
    [MemberData(nameof(Bundled))]
    public void Hot_letters_are_underlined_wherever_they_can_be_drawn(string theme)
    {
        foreach (var (name, scheme) in Themes.SchemesOf(theme).Where(pair => pair.Key is not (SchemeNames.StatusBar or SchemeNames.Cursor)))
            Assert.True(scheme.HotNormal.Style.HasFlag(TextStyle.Underline), $"{theme} › {name} › HotNormal");
    }

    [Theory]
    [MemberData(nameof(Bundled))]
    public void The_status_bar_stands_apart_from_every_region_above_it(string theme)
    {
        var schemes = Themes.SchemesOf(theme);
        var status = schemes[SchemeNames.StatusBar].Normal.Background;
        Assert.NotEqual(schemes[SchemeNames.Base].Normal.Background, status);
        Assert.NotEqual(schemes[SchemeNames.Sidebar].Normal.Background, status);
    }

    [Theory]
    [MemberData(nameof(Bundled))]
    public void The_cursor_is_a_different_colour_from_the_background_it_sits_on(string theme)
    {
        var cursor = Themes.CursorColour(theme);
        Assert.NotNull(cursor);
        Assert.NotEqual(Themes.SchemesOf(theme)[SchemeNames.Base].Editable.Background, cursor);
    }

    [Fact]
    public void An_unknown_theme_falls_back_to_the_default()
    {
        Assert.Equal(Themes.Default, Themes.Apply("Solarized"));
    }
}
