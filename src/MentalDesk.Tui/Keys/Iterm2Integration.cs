using System.Text.Json;
using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Keys;

/// <summary>An iTerm2 Dynamic Profile, which iTerm2 switches a session to while the app's process runs in it.</summary>
public sealed class Iterm2Integration : TerminalIntegration
{
    internal const string VersionKey = "MentalDeskKeysVersion";

    internal Iterm2Integration(MacKeys keys, TerminalEnvironment environment) : base(keys, environment)
    {
    }

    public override string Terminal => "iTerm2";

    public override bool IsHere => IsMacOS && Variable("TERM_PROGRAM") == "iTerm.app";

    public override string Path => Home("Library", "Application Support", "iTerm2", "DynamicProfiles", $"{Keys.FileName}.json");

    internal override int Version => 1;

    // iTerm2 takes any unique string; the same one on every install replaces the profile rather than adding one.
    internal string Guid => $"mentaldesk.mackeys.{Keys.FileName}";

    // iTerm2 matches the process name case-sensitively, and Homebrew lower-cases it.
    internal IEnumerable<string> BoundHosts => new[] { Keys.App, Keys.FileName }.Distinct().Select(name => $"&{name}*");

    internal override string Contents
    {
        get
        {
            var map = string.Join(",\n", MacKeyMap.All
                .Where(key => key.Iterm2 is not null)
                .Select(key => $"        \"{key.Iterm2}\" : {Action(key.Bytes)}"));
            var hosts = string.Join(", ", BoundHosts.Select(host => $"\"{host}\""));
            return $$"""
                {
                  "Profiles" : [
                    {
                      "{{VersionKey}}" : {{Version}},
                      "Name" : "{{Keys.App}}",
                      "Guid" : "{{Guid}}",
                      "Bound Hosts" : [{{hosts}}],
                      "Rewritable" : true,
                      "Keyboard Map" : {
                {{map}}
                      }
                    }
                  ]
                }

                """;
        }
    }

    internal override int? InstalledVersion(string text)
    {
        try
        {
            using var json = JsonDocument.Parse(text);
            return json.RootElement.TryGetProperty("Profiles", out var profiles) && profiles.ValueKind == JsonValueKind.Array
                && profiles.GetArrayLength() > 0 && profiles[0].TryGetProperty(VersionKey, out var version)
                && version.TryGetInt32(out var number)
                    ? number
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Action 10 sends Esc then the text; 11 sends the hex code.
    private static string Action(string bytes) => bytes.StartsWith('\e')
        ? $"{{ \"Action\" : 10, \"Text\" : \"{bytes[1..]}\" }}"
        : $"{{ \"Action\" : 11, \"Text\" : \"0x{(int)bytes[0]:x2}\" }}";
}
