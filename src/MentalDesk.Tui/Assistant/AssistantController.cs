using System.Text.Json.Nodes;

namespace MentalDesk.Tui.Assistant;

// Runs the agent in a folder: starts it on the first need, reopens the folder's last conversation, sends questions and
// keeps the Conversation in step. Reads are allowed; anything else is put to the user. Changed is raised on the UI thread.
public sealed class AssistantController : IAgentRequests, IDisposable
{
    private readonly IAgentLauncher _launcher;
    private readonly AssistantSessions? _sessions;
    private readonly Action<Action> _post;
    private AcpClient? _client;
    private Task? _starting;
    private bool _newSession;
    private string? _root;

    // post runs an action on the UI thread.
    public AssistantController(IAgentLauncher launcher, AssistantSessions? sessions, Action<Action> post, Conversation conversation)
    {
        Conversation = conversation;
        _launcher = launcher;
        _sessions = sessions;
        _post = post;
    }

    public Conversation Conversation { get; }

    public AgentSpec? Agent { get; private set; }

    public string? Root => _root;

    public bool IsAnswering { get; private set; }

    // Starting, or reopening the last conversation.
    public bool IsStarting { get; private set; }

    public bool IsRunning => _client is not null && !IsStarting;

    // Sent as _meta when a session opens, for options such as a system prompt.
    public JsonObject? SessionMeta { get; set; }

    // A file's lines as the app has them, such as an editor's unsaved text; null reads the disk.
    public Func<string, IReadOnlyList<string>?> OpenBuffer { get; set; } = _ => null;

    // Whether the agent may make a tool call that isn't a read, once.
    public Func<PermissionRequest, Task<bool>> AskPermission { get; set; } = _ => Task.FromResult(false);

    public event EventHandler? Changed;

    // From the next question on, stopping the one running if it's different.
    public void Configure(AgentSpec? agent)
    {
        if (agent == Agent) return;
        Agent = agent;
        Conversation.AgentName = agent?.Name ?? "Agent";
        StopAgent();
        Conversation.Clear();
        Raise();
    }

    public void OpenFolder(string root)
    {
        if (root == _root) return;
        _root = root;
        StopAgent();
        Conversation.Clear();
        Raise();
    }

    // Starts the agent and reopens the last conversation, unless it's running already.
    public Task EnsureStartedAsync()
    {
        if (_client is not null && _starting is not null) return _starting;
        if (Agent is null || _root is null) return Task.CompletedTask;
        var client = new AcpClient(Agent, _root, _launcher, this) { SessionMeta = SessionMeta };
        _client = client;
        client.Updated += update => _post(() =>
        {
            if (!ReferenceEquals(client, _client)) return;
            Conversation.Apply(update);
            Raise();
        });
        _ = client.Exited.ContinueWith(_ => _post(() => OnExited(client)), TaskScheduler.Default);
        IsStarting = true;
        Raise();
        return _starting = StartAsync(client);
    }

    private async Task StartAsync(AcpClient client)
    {
        var agent = client.Agent;
        try
        {
            await client.StartAsync().ConfigureAwait(false);
            var last = _newSession ? null : _sessions?.Load(client.Root, agent.CommandLine);
            var restored = last is not null && await client.LoadSessionAsync(last).ConfigureAwait(false);
            if (!restored) await client.NewSessionAsync().ConfigureAwait(false);
            _sessions?.Save(client.Root, agent.CommandLine, client.SessionId);
            _post(() =>
            {
                _newSession = false;
                if (last is not null && !restored) Conversation.AddNote("The last conversation couldn't be restored: " +
                    $"{agent.Name} can't reopen one. This is a new conversation.");
            });
        }
        catch (Exception e) when (e is AgentMissingException or JsonRpcException or IOException)
        {
            _post(() =>
            {
                if (!ReferenceEquals(client, _client)) return;
                Fail(client, e);
            });
            throw;
        }
        finally
        {
            _post(() =>
            {
                if (ReferenceEquals(client, _client)) IsStarting = false;
                Raise();
            });
        }
    }

    private void Fail(AcpClient client, Exception e)
    {
        StopAgent();
        var agent = client.Agent;
        switch (e)
        {
            case AgentMissingException missing:
                Conversation.AddNote($"{missing.Program} isn't on PATH, so {agent.Name} can't start.");
                break;
            case JsonRpcException refused:
                Refused(agent, refused);
                break;
            default:
                Conversation.AddNote($"{agent.Name} exited before it was ready. Ask again to restart it.");
                break;
        }
    }

    private void Refused(AgentSpec agent, JsonRpcException e)
    {
        Conversation.AddNote($"{agent.Name}: {e.Message}");
        if (agent.SignIn is { } signIn && IsSignIn(e))
            Conversation.AddNote($"Sign in by running `{signIn}` in a terminal, then ask again.");
    }

    // ACP's auth_required error, or an agent that says so in its own words.
    private static bool IsSignIn(JsonRpcException e) =>
        e.Code == -32000 || e.Message.Contains("auth", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("log in", StringComparison.OrdinalIgnoreCase);

    // The question goes out as prompt, or as its text with the context ahead of it.
    public void Ask(string question, string? context, IReadOnlyList<JsonNode>? prompt = null)
    {
        if (IsAnswering || Agent is null) return;
        Conversation.AddQuestion(question, context);
        IsAnswering = true;
        Raise();
        _ = AskAsync(prompt ?? [new JsonObject { ["type"] = "text", ["text"] = QuestionText.Compose(context, question) }]);
    }

    private async Task AskAsync(IReadOnlyList<JsonNode> prompt)
    {
        try
        {
            await EnsureStartedAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            _post(Answered);
            return;
        }
        var client = _client;
        if (client is null)
        {
            _post(Answered);
            return;
        }
        try
        {
            var reason = await client.PromptAsync(prompt).ConfigureAwait(false);
            _post(() =>
            {
                if (reason == "cancelled") Conversation.AddNote("Stopped.");
                else if (reason is "refusal" or "max_tokens") Conversation.AddNote($"{client.Agent.Name} stopped: {reason.Replace('_', ' ')}.");
                Answered();
            });
        }
        catch (JsonRpcException e)
        {
            _post(() =>
            {
                Refused(client.Agent, e);
                Answered();
            });
        }
        catch (IOException)
        {
            _post(Answered);
        }
    }

    private void Answered()
    {
        IsAnswering = false;
        Conversation.EndTurn();
        Raise();
    }

    // What arrived stays.
    public void Stop()
    {
        if (IsAnswering) _client?.Cancel();
    }

    // Empties the chat; the next question starts a new conversation.
    public void Clear()
    {
        Stop();
        StopAgent();
        _newSession = true;
        if (_root is not null && Agent is not null) _sessions?.Save(_root, Agent.CommandLine, null);
        Conversation.Clear();
        Raise();
    }

    private void OnExited(AcpClient client)
    {
        if (!ReferenceEquals(client, _client)) return;
        _client = null;
        _starting = null;
        IsStarting = false;
        IsAnswering = false;
        Conversation.AddNote($"{client.Agent.Name} exited. Your next question starts it again.");
        Raise();
    }

    private void StopAgent()
    {
        var client = _client;
        _client = null;
        _starting = null;
        IsStarting = false;
        IsAnswering = false;
        client?.Dispose();
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    public async Task<string> ReadTextFileAsync(string path, int? line, int? limit)
    {
        var open = new TaskCompletionSource<IReadOnlyList<string>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _post(() => open.TrySetResult(OpenBuffer(path)));
        IReadOnlyList<string> lines = await open.Task.ConfigureAwait(false) ?? ReadDisk(path);
        var first = Math.Max((line ?? 1) - 1, 0);
        var taken = lines.Skip(first);
        if (limit is { } count) taken = taken.Take(count);
        return string.Join('\n', taken);
    }

    private static string[] ReadDisk(string path)
    {
        try
        {
            return File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new JsonRpcException(-32002, $"Couldn't read {path}: {e.Message}");
        }
    }

    public async Task<string?> RequestPermissionAsync(PermissionRequest request)
    {
        if (request.IsRead) return request.Option("allow_once") ?? request.Option("allow_always");
        var asked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _post(() => AskPermission(request).ContinueWith(t => asked.TrySetResult(t is { IsCompletedSuccessfully: true, Result: true }), TaskScheduler.Default));
        var allowed = await asked.Task.ConfigureAwait(false);
        return allowed
            ? request.Option("allow_once") ?? request.Option("allow_always")
            : request.Option("reject_once") ?? request.Option("reject_always");
    }

    public void WriteRefused(string path) => _post(() =>
    {
        Conversation.AddNote($"{Agent?.Name ?? "The agent"} can't write {System.IO.Path.GetFileName(path)} directly: it has to ask first.");
        Raise();
    });

    public void Dispose() => StopAgent();
}
