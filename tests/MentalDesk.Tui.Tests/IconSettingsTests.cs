using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Tests;

public sealed class IconSettingsTests : IDisposable
{
    private static readonly Icon Folder = new("\U000F024B", "▸");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"mentaldesk-{Guid.NewGuid():N}");

    private string SettingsPath => Path.Combine(_folder, "mentaldesk", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Theory]
    [InlineData(IconStyle.Auto)]
    [InlineData(IconStyle.NerdFont)]
    [InlineData(IconStyle.Plain)]
    public void A_kept_style_reads_back(IconStyle style)
    {
        new MentalDeskSettings(SettingsPath).IconStyle = style;

        Assert.Equal(style, new MentalDeskSettings(SettingsPath).IconStyle);
    }

    [Fact]
    public void A_missing_file_is_auto() =>
        Assert.Equal(IconStyle.Auto, new MentalDeskSettings(SettingsPath).IconStyle);

    [Theory]
    [InlineData("not json {")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "iconStyle": "Emoji" }""")]
    [InlineData("""{ "iconStyle": "7" }""")]
    [InlineData("""{ "iconStyle": 1 }""")]
    public void A_corrupt_or_unknown_value_is_auto(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, content);

        Assert.Equal(IconStyle.Auto, new MentalDeskSettings(SettingsPath).IconStyle);
    }

    [Fact]
    public void Keeping_the_style_leaves_other_settings_alone()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "keys": { "quit": "Ctrl+Q" } }""");

        new MentalDeskSettings(SettingsPath).IconStyle = IconStyle.Plain;

        Assert.Contains("Ctrl+Q", File.ReadAllText(SettingsPath));
        Assert.Equal(IconStyle.Plain, new MentalDeskSettings(SettingsPath).IconStyle);
    }

    [Theory]
    [InlineData(true, IconStyle.NerdFont, "\U000F024B")]
    [InlineData(false, IconStyle.Plain, "▸")]
    public void Auto_draws_what_the_terminal_was_detected_to_have(bool nerdFont, IconStyle inUse, string glyph)
    {
        var icons = Settings(new FontDetection(nerdFont, "a reason"));

        Assert.Equal(inUse, icons.InUse);
        Assert.Equal(glyph, icons.Glyph(Folder));
        Assert.Equal($"Auto chose {IconSettings.Name(inUse)}: a reason", icons.AutoReason);
    }

    [Theory]
    [InlineData(IconStyle.NerdFont, "\U000F024B")]
    [InlineData(IconStyle.Plain, "▸")]
    public void A_chosen_style_overrides_detection(IconStyle style, string glyph)
    {
        new MentalDeskSettings(SettingsPath).IconStyle = style;
        var detected = false;

        var icons = new IconSettings(new MentalDeskSettings(SettingsPath), () =>
        {
            detected = true;
            return new FontDetection(style != IconStyle.NerdFont, "a reason");
        });

        Assert.Equal(style, icons.InUse);
        Assert.Equal(glyph, icons.Glyph(Folder));
        Assert.False(detected);
    }

    [Fact]
    public void A_preview_is_drawn_but_not_kept_until_keep()
    {
        var icons = Settings(new FontDetection(false, "a reason"));
        var changes = 0;
        icons.Changed += (_, _) => changes++;

        icons.Preview(IconStyle.NerdFont);

        Assert.Equal(1, changes);
        Assert.Equal(IconStyle.NerdFont, icons.InUse);
        Assert.Equal(IconStyle.Auto, new MentalDeskSettings(SettingsPath).IconStyle);

        icons.Keep();

        Assert.Equal(IconStyle.NerdFont, new MentalDeskSettings(SettingsPath).IconStyle);
    }

    [Fact]
    public void Reverting_puts_the_saved_style_back()
    {
        var icons = Settings(new FontDetection(false, "a reason"));
        icons.Preview(IconStyle.NerdFont);

        icons.Revert();

        Assert.Equal(IconStyle.Auto, icons.Style);
        Assert.Equal(IconStyle.Plain, icons.InUse);
    }

    private IconSettings Settings(FontDetection detection) =>
        new(new MentalDeskSettings(SettingsPath), () => detection);
}
