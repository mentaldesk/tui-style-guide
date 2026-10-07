using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class ButtonTests : StaticConfigurationTest
{
    private static readonly string[] ButtonSchemes = [SchemeNames.ButtonPrimary, SchemeNames.ButtonDanger, SchemeNames.ButtonSecondary];

    public static TheoryData<string> Bundled => [.. Themes.Bundled];

    public static TheoryData<string, string> EveryButtonInEveryTheme =>
        [.. Themes.Bundled.SelectMany(theme => ButtonSchemes.Select(scheme => (theme, scheme)))];

    [Theory]
    [MemberData(nameof(EveryButtonInEveryTheme))]
    public void A_button_label_is_easy_to_read_whatever_its_state(string theme, string name)
    {
        var scheme = Themes.SchemesOf(theme)[name];
        foreach (var role in new[] { VisualRole.Normal, VisualRole.Focus, VisualRole.Highlight })
        {
            var attribute = scheme.GetAttributeForRole(role);
            Assert.True(Contrast(attribute.Foreground, attribute.Background) >= 4.5, $"{theme} › {name} › {role}");
        }
    }

    [Theory]
    [MemberData(nameof(EveryButtonInEveryTheme))]
    public void Focus_and_hover_each_look_different_from_rest(string theme, string name)
    {
        var scheme = Themes.SchemesOf(theme)[name];
        Assert.NotEqual(scheme.Normal.Background, scheme.Focus.Background);
        Assert.True(scheme.Focus.Style.HasFlag(TextStyle.Bold));
        Assert.NotEqual(scheme.Normal.Background, scheme.Highlight.Background);
        Assert.NotEqual(scheme.Focus.Background, scheme.Highlight.Background);
    }

    [Theory]
    [MemberData(nameof(Bundled))]
    public void Danger_is_red(string theme)
    {
        var danger = Themes.SchemesOf(theme)[SchemeNames.ButtonDanger].Normal.Background;
        Assert.True(danger.R > 2 * danger.G && danger.R > 2 * danger.B, $"{theme} › {danger}");
    }

    [Fact]
    public void Only_a_primary_button_is_the_enter_action()
    {
        Assert.True(AppButton.Primary("Save").IsDefault);
        Assert.False(AppButton.Danger("Delete").IsDefault);
        Assert.False(AppButton.Secondary("Cancel").IsDefault);
    }

    [Fact]
    public void A_button_is_a_padded_block_of_colour_without_brackets_and_only_the_focused_one_looks_focused()
    {
        using var app = Application.Create();
        app.Init(driverName: DriverRegistry.Names.ANSI);
        using var window = new Window { BorderStyle = LineStyle.None };
        Button[] buttons = [AppButton.Primary("Save"), AppButton.Danger("Delete"), AppButton.Secondary("Cancel")];
        buttons[1].X = 10;
        buttons[2].X = 20;
        window.Add(buttons);
        var token = app.Begin(window);

        buttons[2].SetFocus();
        app.LayoutAndDraw(true);

        var schemes = Themes.SchemesOf(Themes.Current);
        AssertDrawn(app, 0, " Save ", schemes[SchemeNames.ButtonPrimary].Normal);
        AssertDrawn(app, 10, " Delete ", schemes[SchemeNames.ButtonDanger].Normal);
        AssertDrawn(app, 20, " Cancel ", schemes[SchemeNames.ButtonSecondary].Focus);
        app.End(token!);
    }

    private static void AssertDrawn(IApplication app, int x, string text, Attribute attribute)
    {
        var cells = Enumerable.Range(x, text.Length).Select(col => app.Driver!.Contents![0, col]).ToArray();
        Assert.Equal(text, string.Concat(cells.Select(cell => cell.Grapheme)));
        Assert.All(cells, cell => Assert.Equal(attribute.Background, cell.Attribute!.Value.Background));
        Assert.All(cells, cell => Assert.Equal(attribute.Style.HasFlag(TextStyle.Bold), cell.Attribute!.Value.Style.HasFlag(TextStyle.Bold)));
    }

    private static double Contrast(Color a, Color b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static double Luminance(Color colour) =>
        0.2126 * Linear(colour.R) + 0.7152 * Linear(colour.G) + 0.0722 * Linear(colour.B);

    private static double Linear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
