using System.Text.Json.Nodes;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;
using Terminal.Gui.Configuration;

namespace MentalDesk.Tui.Tests;

public class AppSchemesTests : StaticConfigurationTest
{
    private const string Line = "Test.Line";

    private readonly IDisposable _registration = Themes.AddAppSchemes(Derive);

    public static TheoryData<string> Bundled => [.. Themes.Bundled];

    public override void Dispose()
    {
        _registration.Dispose();
        Themes.Load();
        base.Dispose();
    }

    private static IReadOnlyDictionary<string, Scheme> Derive(string theme, IReadOnlyDictionary<string, Scheme> shared) =>
        new Dictionary<string, Scheme> { [Line] = shared[SchemeNames.Base] with { Normal = shared[SchemeNames.Error].Normal } };

    private static Scheme Expected(string theme) => Derive(theme, Themes.SchemesOf(theme))[Line];

    [Fact]
    public void An_apps_schemes_are_there_in_every_theme_and_change_with_it()
    {
        Themes.Load();
        foreach (var theme in Themes.Bundled)
        {
            Themes.Apply(theme);
            Assert.Equal(Expected(theme), SchemeManager.GetScheme(Line));
        }
        Assert.Equal(Themes.Bundled.Count, Themes.Bundled.Select(theme => Expected(theme).Normal).Distinct().Count());
    }

    [Fact]
    public void An_app_scheme_named_like_a_shared_one_is_refused_when_registered()
    {
        var error = Assert.Throws<ArgumentException>(() => Themes.AddAppSchemes((_, shared) =>
            new Dictionary<string, Scheme> { ["warning"] = shared[SchemeNames.Base] }));
        Assert.Contains("\"warning\"", error.Message);
    }

    [Fact]
    public void A_theme_added_after_the_app_registered_gets_its_schemes()
    {
        var config = JsonNode.Parse(Themes.BundledConfig)!;
        var solar = config["Themes"]![Themes.Midnight]!.DeepClone();
        solar["Schemes"]![SchemeNames.Error]!["Normal"]!["Background"] = "#204060";
        config["Themes"]!["Solar"] = solar;
        TuiConfigurationBuilder.Shared.RuntimeConfig = config.ToJsonString();
        TuiConfigurationBuilder.Shared.ApplyToStaticFacades();

        ThemeManager.Theme = "Solar";

        Assert.Equal("Solar", Themes.Current);
        Assert.Equal(Expected("Solar"), SchemeManager.GetScheme(Line));
        Assert.NotEqual(Expected(Themes.Midnight), SchemeManager.GetScheme(Line));
    }

    [Fact]
    public void Trying_a_theme_and_coming_back_leaves_the_apps_schemes_in_the_outer_theme()
    {
        using var host = new Host();
        using var window = new AppWindow(host.Shell);
        using var inner = new AppShell(host.App, host.Shell.Cursor);
        inner.Commands.Register("inner.back", "Back", () => host.App.RequestStop());
        inner.Keys.Bind("Esc", "inner.back");
        using var innerWindow = new AppWindow(inner);
        Scheme? inside = null;

        host.Run(window,
            () => host.Shell.RunNested(inner, innerWindow, Themes.Daylight),
            () => host.App.TopRunnableView == innerWindow,
            () =>
            {
                inside = SchemeManager.GetScheme(Line);
                host.App.InjectKey(Key.Esc);
            },
            () => host.App.TopRunnableView == window);

        Assert.Equal(Expected(Themes.Daylight), inside);
        Assert.Equal(Expected(Themes.Default), SchemeManager.GetScheme(Line));
    }

    [Theory]
    [MemberData(nameof(Bundled))]
    public void Registering_app_schemes_leaves_the_shared_ones_as_they_were(string theme)
    {
        Themes.Apply(theme);
        foreach (var (name, scheme) in Themes.SchemesOf(theme))
            Assert.Equal(scheme, SchemeManager.GetScheme(name));
    }

    [Theory]
    [MemberData(nameof(Bundled))]
    public void The_sample_apps_added_and_removed_lines_are_readable_and_tell_apart(string theme)
    {
        var schemes = Themes.AppSchemesOf(theme);
        var text = Themes.SchemesOf(theme)[SchemeNames.Base].Editable;
        var added = schemes[SampleSchemes.Added].Normal;
        var removed = schemes[SampleSchemes.Removed].Normal;

        Assert.True(SampleSchemes.Contrast(added.Foreground, added.Background) >= 4.5, $"{theme} › Added");
        Assert.True(SampleSchemes.Contrast(removed.Foreground, removed.Background) >= 4.5, $"{theme} › Removed");
        Assert.Equal(3, new[] { text.Background, added.Background, removed.Background }.Distinct().Count());
    }
}
