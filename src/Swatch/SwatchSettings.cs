using System.Text.Json.Nodes;
using MentalDesk.Tui;
using MentalDesk.Tui.Theming;

namespace Swatch;

internal sealed class SwatchSettings(MentalDeskSettings file)
{
    public const bool DefaultShowHexValues = true;

    private const string Section = "swatch";
    private const string ThemeKey = "theme";
    private const string ShowHexValuesKey = "showHexValues";

    public string Theme
    {
        get => Read(ThemeKey) is JsonValue value && value.TryGetValue<string>(out var theme) ? theme : Themes.Default;
        set => Write(ThemeKey, value);
    }

    public bool ShowHexValues
    {
        get => Read(ShowHexValuesKey) is JsonValue value && value.TryGetValue<bool>(out var show) ? show : DefaultShowHexValues;
        set => Write(ShowHexValuesKey, value);
    }

    private JsonNode? Read(string key) => file.Section(Section)[key];

    private void Write(string key, JsonNode value)
    {
        var section = file.Section(Section);
        section[key] = value;
        file.SaveSection(Section, section);
    }
}
