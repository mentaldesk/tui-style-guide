using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui;

public sealed class MentalDeskSettings(string path)
{
    private const string IconStyleKey = "iconStyle";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static MentalDeskSettings ThisUser { get; } = new(DefaultPath());

    public string Path { get; } = path;

    public IconStyle IconStyle
    {
        get => Read()?[IconStyleKey] is JsonValue value && value.TryGetValue<string>(out var name)
            && Enum.TryParse<IconStyle>(name, out var style) && Enum.IsDefined(style)
                ? style
                : IconStyle.Auto;
        set
        {
            var settings = Read() ?? [];
            settings[IconStyleKey] = value.ToString();
            Write(settings);
        }
    }

    public static string DefaultPath()
    {
        if (OperatingSystem.IsWindows())
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MentalDesk", "settings.json");
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
            ? xdg
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return System.IO.Path.Combine(config, "mentaldesk", "settings.json");
    }

    private JsonObject? Read()
    {
        try
        {
            return File.Exists(Path) ? JsonNode.Parse(File.ReadAllText(Path)) as JsonObject : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Through a temporary file, so another app starting meanwhile never reads half of it.
    private void Write(JsonObject settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = $"{Path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temporary, settings.ToJsonString(Indented));
        File.Move(temporary, Path, overwrite: true);
    }
}
