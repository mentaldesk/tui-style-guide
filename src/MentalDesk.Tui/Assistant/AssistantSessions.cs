using System.Text.Json;
using System.Text.Json.Nodes;

namespace MentalDesk.Tui.Assistant;

// The last conversation in each folder, as the agent's session id: the agent keeps the history itself.
// A JSON array, most recently used folder first.
public sealed class AssistantSessions(string path)
{
    public const int MaxFolders = 30;

    // ~/.tui/<app>.assistant.json
    public static AssistantSessions ForApp(string app) =>
        new(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tui", $"{app}.assistant.json"));

    public string Path { get; } = path;

    public string? Load(string folder, string agent) =>
        Read().FirstOrDefault(e => Text(e, "Folder") == folder && Text(e, "Agent") == agent) is { } entry ? Text(entry, "Session") : null;

    // Null forgets the folder's session.
    public void Save(string folder, string agent, string? session)
    {
        var root = new JsonArray();
        if (session is not null)
            root.Add((JsonNode)new JsonObject { ["Folder"] = folder, ["Agent"] = agent, ["Session"] = session });
        foreach (var entry in Read().Where(e => Text(e, "Folder") != folder).Take(MaxFolders - root.Count))
            root.Add((JsonNode)entry.DeepClone());
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        // Losing it only costs the next start its last conversation.
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private List<JsonObject> Read()
    {
        try
        {
            if (!File.Exists(Path)) return [];
            return JsonNode.Parse(File.ReadAllText(Path)) is JsonArray entries ? [.. entries.OfType<JsonObject>()] : [];
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string? Text(JsonObject entry, string key) =>
        entry[key] is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
}
