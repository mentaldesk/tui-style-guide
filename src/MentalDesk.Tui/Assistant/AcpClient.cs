using System.Reflection;
using System.Text.Json.Nodes;

namespace MentalDesk.Tui.Assistant;

// What the agent can do, from its answer to initialize.
public sealed record AgentInfo(bool LoadSession, bool EmbeddedContext);

public sealed record PermissionOption(string Id, string Name, string Kind);

// A session/request_permission: the tool call the agent wants to make, and the answers it accepts.
public sealed record PermissionRequest(string Title, string? Kind, string? Command, IReadOnlyList<PermissionOption> Options)
{
    public bool IsEdit => Kind is "edit" or "delete" or "move";

    public bool IsRead => Kind is "read" or "search" or "think";

    public bool IsCommand => Command is not null || Kind == "execute";

    // The first option of this kind (allow_once, reject_once…), or null.
    public string? Option(string kind) => Options.FirstOrDefault(o => o.Kind == kind)?.Id;

    public static PermissionRequest Parse(JsonNode? parameters)
    {
        var call = parameters?["toolCall"];
        var input = call?["rawInput"];
        var command = SessionUpdate.StringOf(input?["command"])
            ?? (input?["command"] is JsonArray words ? string.Join(' ', words.Select(SessionUpdate.StringOf)) : null);
        var options = (parameters?["options"] as JsonArray ?? [])
            .Select(o => new PermissionOption(SessionUpdate.StringOf(o?["optionId"]) ?? "", SessionUpdate.StringOf(o?["name"]) ?? "", SessionUpdate.StringOf(o?["kind"]) ?? ""))
            .Where(o => o.Id.Length > 0)
            .ToList();
        return new PermissionRequest(SessionUpdate.StringOf(call?["title"]) ?? "a tool", SessionUpdate.StringOf(call?["kind"]), command, options);
    }
}

// Each is answered off the reading thread, so one may wait on the user.
public interface IAgentRequests
{
    Task<string> ReadTextFileAsync(string path, int? line, int? limit);

    // The option chosen; null to cancel.
    Task<string?> RequestPermissionAsync(PermissionRequest request);

    void WriteRefused(string path);
}

public sealed class AgentMissingException(string program) : Exception($"{program} isn't on PATH")
{
    public string Program { get; } = program;
}

// One agent over the Agent Client Protocol: the app opens a session in a folder, sends prompts and answers the agent's requests.
public sealed class AcpClient : IDisposable
{
    public const int ProtocolVersion = 1;

    private readonly IAgentLauncher _launcher;
    private readonly IAgentRequests _requests;
    private IAgentProcess? _process;
    private JsonRpcConnection? _connection;
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _disposed;

    public AcpClient(AgentSpec agent, string root, IAgentLauncher launcher, IAgentRequests requests)
    {
        Agent = agent;
        Root = root;
        _launcher = launcher;
        _requests = requests;
    }

    public AgentSpec Agent { get; }
    public string Root { get; }
    public AgentInfo? Info { get; private set; }
    public string? SessionId { get; private set; }

    // Sent as _meta on session/new and session/load, for options an agent reads there.
    public JsonObject? SessionMeta { get; init; }

    // Switched to once a session opens, if the agent offers it, such as Claude Code's "auto".
    public string? Mode { get; init; }

    public Task Exited => _exited.Task;

    // Raised on the reading thread.
    public event Action<SessionUpdate>? Updated;

    /// <exception cref="AgentMissingException">The program isn't installed.</exception>
    /// <exception cref="JsonRpcException">The agent refused.</exception>
    /// <exception cref="IOException">The agent exited.</exception>
    public async Task StartAsync()
    {
        var process = _launcher.Launch(Agent.Program, Agent.Arguments, Root) ?? throw new AgentMissingException(Agent.Program);
        var connection = new JsonRpcConnection(process.Output, process.Input)
        {
            AsyncRequestHandler = AnswerAsync,
        };
        connection.NotificationReceived += OnNotification;
        _process = process;
        _connection = connection;
        connection.Start();
        _ = Task.WhenAny(process.Exited, connection.Completion).ContinueWith(_ => _exited.TrySetResult(), TaskScheduler.Default);

        var app = Assembly.GetEntryAssembly()?.GetName();
        var answer = await connection.RequestAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["clientCapabilities"] = new JsonObject
            {
                ["fs"] = new JsonObject { ["readTextFile"] = true, ["writeTextFile"] = false },
                ["terminal"] = false,
            },
            ["clientInfo"] = new JsonObject { ["name"] = app?.Name ?? "MentalDesk.Tui", ["version"] = app?.Version?.ToString() ?? "0" },
        }).ConfigureAwait(false);
        var capabilities = answer?["agentCapabilities"];
        Info = new AgentInfo(
            capabilities?["loadSession"] is JsonValue load && load.TryGetValue<bool>(out var canLoad) && canLoad,
            capabilities?["promptCapabilities"]?["embeddedContext"] is JsonValue embedded && embedded.TryGetValue<bool>(out var canEmbed) && canEmbed);
    }

    public async Task NewSessionAsync()
    {
        var answer = await Connection.RequestAsync("session/new", SessionParams()).ConfigureAwait(false);
        SessionId = SessionUpdate.StringOf(answer?["sessionId"]) ?? throw new JsonRpcException(JsonRpcConnection.InternalError, "The agent didn't open a session.");
        await SwitchModeAsync(answer).ConfigureAwait(false);
    }

    // Its history arrives as updates first. False when the agent can't reopen one.
    public async Task<bool> LoadSessionAsync(string sessionId)
    {
        if (Info is not { LoadSession: true }) return false;
        SessionId = sessionId;
        var parameters = SessionParams();
        parameters["sessionId"] = sessionId;
        JsonNode? answer;
        try
        {
            answer = await Connection.RequestAsync("session/load", parameters).ConfigureAwait(false);
        }
        catch (JsonRpcException)
        {
            SessionId = null;
            return false;
        }
        await SwitchModeAsync(answer).ConfigureAwait(false);
        return true;
    }

    private async Task SwitchModeAsync(JsonNode? session)
    {
        if (Mode is null || session?["modes"] is not { } modes || SessionUpdate.StringOf(modes["currentModeId"]) == Mode) return;
        if (modes["availableModes"] is not JsonArray available || !available.Any(mode => SessionUpdate.StringOf(mode?["id"]) == Mode)) return;
        await Connection.RequestAsync("session/set_mode", new JsonObject { ["sessionId"] = SessionId, ["modeId"] = Mode }).ConfigureAwait(false);
    }

    private JsonObject SessionParams()
    {
        var parameters = new JsonObject { ["cwd"] = Root, ["mcpServers"] = new JsonArray() };
        if (SessionMeta is not null) parameters["_meta"] = SessionMeta.DeepClone();
        return parameters;
    }

    // Completes with the stop reason once the answer is over.
    public async Task<string?> PromptAsync(IEnumerable<JsonNode> blocks)
    {
        var answer = await Connection.RequestAsync("session/prompt", new JsonObject
        {
            ["sessionId"] = SessionId,
            ["prompt"] = new JsonArray([.. blocks]),
        }).ConfigureAwait(false);
        return SessionUpdate.StringOf(answer?["stopReason"]);
    }

    // The prompt then ends as cancelled.
    public void Cancel()
    {
        if (SessionId is { } id) _connection?.Notify("session/cancel", new JsonObject { ["sessionId"] = id });
    }

    private JsonRpcConnection Connection => _connection ?? throw new InvalidOperationException("The agent hasn't started.");

    private void OnNotification(string method, JsonNode? parameters)
    {
        if (method != "session/update" || SessionUpdate.StringOf(parameters?["sessionId"]) != SessionId) return;
        if (SessionUpdate.Parse(parameters?["update"]) is { } update) Updated?.Invoke(update);
    }

    private async Task<JsonNode?> AnswerAsync(string method, JsonNode? parameters)
    {
        switch (method)
        {
            case "fs/read_text_file":
                var path = SessionUpdate.StringOf(parameters?["path"]) ?? throw new JsonRpcException(-32602, "No path");
                var text = await _requests.ReadTextFileAsync(path, Int(parameters!["line"]), Int(parameters["limit"])).ConfigureAwait(false);
                return new JsonObject { ["content"] = text };
            case "fs/write_text_file":
                _requests.WriteRefused(SessionUpdate.StringOf(parameters?["path"]) ?? "");
                throw new JsonRpcException(-32000, "This app doesn't let agents write files.");
            case "session/request_permission":
                var chosen = await _requests.RequestPermissionAsync(PermissionRequest.Parse(parameters)).ConfigureAwait(false);
                return new JsonObject
                {
                    ["outcome"] = chosen is null
                        ? new JsonObject { ["outcome"] = "cancelled" }
                        : new JsonObject { ["outcome"] = "selected", ["optionId"] = chosen },
                };
            default:
                throw new JsonRpcException(JsonRpcConnection.MethodNotFound, $"Method not found: {method}");
        }
    }

    private static int? Int(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _process?.Kill();
        _connection?.Dispose();
        _process?.Dispose();
    }
}
