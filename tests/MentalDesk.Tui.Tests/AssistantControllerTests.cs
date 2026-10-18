using System.Text.Json.Nodes;
using MentalDesk.Tui.Assistant;

namespace MentalDesk.Tui.Tests;

// The ACP client and the controller against a fake agent over in-memory pipes.
public sealed class AssistantControllerTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"mentaldesk-{Guid.NewGuid():N}");
    private readonly FakeAgent _agent = new();
    private readonly AssistantSessions _sessions;
    private readonly AssistantController _controller;
    private readonly Lock _gate = new();

    public AssistantControllerTests()
    {
        Directory.CreateDirectory(Root);
        _sessions = new AssistantSessions(Path.Combine(_dir, "home", "Test.assistant.json"));
        _controller = New();
    }

    private string Root => Path.Combine(_dir, "work");

    private AssistantController New()
    {
        var controller = new AssistantController(_agent, _sessions, action => { lock (_gate) action(); }, new Conversation());
        controller.Configure(AgentSpec.ClaudeCode);
        controller.OpenFolder(Root);
        return controller;
    }

    public void Dispose()
    {
        _controller.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            lock (_gate)
                if (condition()) return;
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static string Chat(AssistantController controller) =>
        string.Join('\n', controller.Conversation.Render(60).Select(l => l.Text));

    [Fact]
    public async Task Starting_runs_the_agent_in_the_folder_and_opens_a_session_there()
    {
        await _controller.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var (program, arguments, directory) = _agent.Launches.Single();
        Assert.Equal(("npx", "-y @agentclientprotocol/claude-agent-acp", Root), (program, string.Join(' ', arguments), directory));
        var initialize = _agent.Received("initialize").Single()!;
        Assert.True(initialize["clientCapabilities"]!["fs"]!["readTextFile"]!.GetValue<bool>());
        Assert.False(initialize["clientCapabilities"]!["fs"]!["writeTextFile"]!.GetValue<bool>());
        Assert.Equal(Root, _agent.Received("session/new").Single()!["cwd"]!.GetValue<string>());
    }

    [Fact]
    public async Task Session_meta_goes_out_with_the_new_session()
    {
        _controller.SessionMeta = new JsonObject { ["systemPrompt"] = new JsonObject { ["append"] = "You know a-team." } };

        await _controller.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Equal("You know a-team.", _agent.Received("session/new").Single()!["_meta"]!["systemPrompt"]!["append"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_session_switches_to_the_mode_asked_for_when_the_agent_offers_it()
    {
        _agent.Modes.AddRange(["default", "auto"]);
        _controller.SessionMode = "auto";

        await _controller.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var switched = _agent.Received("session/set_mode").Single()!;
        Assert.Equal(("session-1", "auto"), (switched["sessionId"]!.GetValue<string>(), switched["modeId"]!.GetValue<string>()));
    }

    [Fact]
    public async Task A_mode_the_agent_does_not_offer_is_not_asked_for()
    {
        _agent.Modes.AddRange(["default", "plan"]);
        _controller.SessionMode = "auto";

        await _controller.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Empty(_agent.Received("session/set_mode"));
    }

    [Fact]
    public async Task A_reopened_conversation_switches_to_the_mode_too()
    {
        _controller.Ask("One", null);
        await Until(() => !_controller.IsAnswering);
        _agent.Modes.AddRange(["default", "auto"]);
        using var reopened = New();
        reopened.SessionMode = "auto";

        await reopened.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Single(_agent.Received("session/load"));
        Assert.Equal("auto", _agent.Received("session/set_mode").Single()!["modeId"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_question_goes_out_with_its_context_ahead_of_it_and_the_answer_streams_in_with_a_line_per_tool_call()
    {
        _agent.OnPrompt = async (agent, _) =>
        {
            agent.Say("It waits ");
            agent.Tool("t1", "Read the board history");
            agent.Say("on review.");
            await Task.Yield();
        };

        _controller.Ask("Why so long?", "on #499 in Work");
        await Until(() => !_controller.IsAnswering);

        var prompt = _agent.Received("session/prompt").Single()!["prompt"]!.AsArray();
        Assert.Equal("[on #499 in Work]\n\nWhy so long?", prompt.Single()!["text"]!.GetValue<string>());
        Assert.Equal("You · on #499 in Work\nWhy so long?\n\nClaude Code\nIt waits\n▸ Read the board history\non review.", Chat(_controller));
    }

    [Fact]
    public async Task An_app_can_build_the_prompt_itself()
    {
        JsonNode[] prompt = [new JsonObject { ["type"] = "text", ["text"] = "Why? (and much more)" }];

        _controller.Ask("Why?", "on #1", prompt);
        await Until(() => !_controller.IsAnswering);

        Assert.Equal("Why? (and much more)", _agent.Received("session/prompt").Single()!["prompt"]![0]!["text"]!.GetValue<string>());
        Assert.StartsWith("You · on #1\nWhy?", Chat(_controller));
    }

    [Fact]
    public async Task A_follow_up_goes_to_the_same_session()
    {
        _controller.Ask("One", null);
        await Until(() => !_controller.IsAnswering);
        _controller.Ask("Two", null);
        await Until(() => !_controller.IsAnswering);

        var sessions = _agent.Received("session/prompt").Select(p => p!["sessionId"]!.GetValue<string>()).ToList();
        Assert.Equal(2, sessions.Count);
        Assert.Equal(sessions[0], sessions[1]);
        Assert.Single(_agent.Received("session/new"));
    }

    [Fact]
    public async Task Stopping_cancels_the_session_and_keeps_what_arrived()
    {
        _agent.OnPrompt = async (agent, _) =>
        {
            agent.Say("Partial");
            await agent.Cancelled.Task;
        };
        _controller.Ask("Long?", null);
        await Until(() => Chat(_controller).Contains("Partial"));

        _controller.Stop();
        await Until(() => !_controller.IsAnswering);

        Assert.Single(_agent.Received("session/cancel"));
        Assert.EndsWith("Claude Code\nPartial\n\nStopped.", Chat(_controller));
    }

    [Fact]
    public async Task Clearing_empties_the_chat_and_the_next_question_starts_a_new_session()
    {
        _controller.Ask("One", null);
        await Until(() => !_controller.IsAnswering);

        _controller.Clear();
        Assert.True(_controller.Conversation.IsEmpty);
        _controller.Ask("Two", null);
        await Until(() => !_controller.IsAnswering);

        var sessions = _agent.Received("session/prompt").Select(p => p!["sessionId"]!.GetValue<string>()).ToList();
        Assert.NotEqual(sessions[0], sessions[1]);
        Assert.Empty(_agent.Received("session/load"));
    }

    [Fact]
    public async Task Starting_again_reloads_the_last_conversation_with_each_question_s_context()
    {
        _controller.Ask("One", "on #2");
        await Until(() => !_controller.IsAnswering);
        var session = _agent.SessionId;
        _agent.History.AddRange([(true, QuestionText.Compose("on #2", "One")), (false, "Hello")]);

        using var reopened = New();
        await reopened.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await Until(() => Chat(reopened).Contains("Hello"));

        Assert.Equal(session, _agent.Received("session/load").Single()!["sessionId"]!.GetValue<string>());
        Assert.Equal("You · on #2\nOne\n\nClaude Code\nHello", Chat(reopened));
    }

    [Fact]
    public async Task An_agent_that_cant_reload_starts_empty_and_says_so()
    {
        _controller.Ask("One", null);
        await Until(() => !_controller.IsAnswering);
        _agent.LoadSession = false;

        using var reopened = New();
        await reopened.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await Until(() => Chat(reopened).Length > 0);

        Assert.Empty(_agent.Received("session/load"));
        Assert.StartsWith("The last conversation couldn't be restored", Chat(reopened));
    }

    [Fact]
    public async Task Each_folder_has_its_own_last_conversation()
    {
        _controller.Ask("One", null);
        await Until(() => !_controller.IsAnswering);

        _controller.OpenFolder(Path.Combine(_dir, "other"));
        await _controller.EnsureStartedAsync().WaitAsync(Timeout, TestContext.Current.CancellationToken);

        Assert.Empty(_agent.Received("session/load"));
        Assert.Equal(2, _agent.Received("session/new").Count());
    }

    [Fact]
    public async Task Reading_a_file_the_app_has_open_gets_its_text_and_otherwise_the_disk()
    {
        File.WriteAllText(Path.Combine(Root, "Open.cs"), "on disk");
        File.WriteAllText(Path.Combine(Root, "Closed.cs"), "one\r\ntwo\r\nthree\r\nfour");
        _controller.OpenBuffer = path => path.EndsWith("Open.cs", StringComparison.Ordinal) ? ["edited", "lines"] : null;
        string? open = null, closed = null;
        _agent.OnPrompt = async (agent, _) =>
        {
            open = (await agent.Ask("fs/read_text_file", new JsonObject { ["path"] = Path.Combine(Root, "Open.cs") }))!["content"]!.GetValue<string>();
            closed = (await agent.Ask("fs/read_text_file", new JsonObject { ["path"] = Path.Combine(Root, "Closed.cs"), ["line"] = 2, ["limit"] = 2 }))!["content"]!.GetValue<string>();
        };

        _controller.Ask("Read", null);
        await Until(() => !_controller.IsAnswering);

        Assert.Equal(("edited\nlines", "two\nthree"), (open, closed));
    }

    [Fact]
    public async Task Writing_a_file_directly_is_refused_and_the_chat_says_so()
    {
        var path = Path.Combine(Root, "A.cs");
        File.WriteAllText(path, "before");
        JsonRpcException? refused = null;
        _agent.OnPrompt = async (agent, _) =>
        {
            refused = await Assert.ThrowsAsync<JsonRpcException>(() => agent.Ask("fs/write_text_file", new JsonObject { ["path"] = path, ["content"] = "after" }));
        };

        _controller.Ask("Edit", null);
        await Until(() => !_controller.IsAnswering);

        Assert.NotNull(refused);
        Assert.Equal("before", File.ReadAllText(path));
        Assert.Contains("Claude Code can't write A.cs directly", Chat(_controller));
    }

    [Fact]
    public async Task A_read_is_allowed_without_asking_and_a_command_or_an_edit_is_put_to_the_user()
    {
        var asked = new List<PermissionRequest>();
        _controller.AskPermission = request =>
        {
            asked.Add(request);
            return Task.FromResult(request.IsEdit);
        };
        string? read = "unset", edit = "unset", command = "unset";
        _agent.OnPrompt = async (agent, _) =>
        {
            read = await agent.AskPermission("read", "Read A.cs");
            edit = await agent.AskPermission("edit", "Edit A.cs");
            command = await agent.AskPermission("execute", "git log", "git log --oneline -5");
            agent.Say("Carrying on.");
        };

        _controller.Ask("Go", null);
        await Until(() => !_controller.IsAnswering);

        Assert.Equal(("allow", "allow", "reject"), (read, edit, command));
        Assert.Equal(["Edit A.cs", "git log --oneline -5"], asked.Select(r => r.Command ?? r.Title));
        Assert.EndsWith("Carrying on.", Chat(_controller));
    }

    [Fact]
    public async Task An_agent_that_isnt_signed_in_shows_its_own_message_and_the_command_to_sign_in()
    {
        _agent.NewSessionError = (-32000, "Authentication required");

        _controller.Ask("Hi", null);
        await Until(() => !_controller.IsAnswering);

        Assert.Contains("Claude Code: Authentication required", Chat(_controller));
        Assert.Contains("Sign in by running `claude auth login` in a terminal", Chat(_controller));
    }

    [Fact]
    public async Task An_agent_that_exits_mid_conversation_says_so_and_the_next_question_starts_it_again()
    {
        _agent.OnPrompt = (agent, _) =>
        {
            agent.Current.Exit();
            return Task.CompletedTask;
        };
        _controller.Ask("One", null);
        await Until(() => Chat(_controller).Contains("exited"));
        await Until(() => !_controller.IsAnswering);

        _agent.OnPrompt = (agent, _) => { agent.Say("Back"); return Task.CompletedTask; };
        _controller.Ask("Two", null);
        await Until(() => Chat(_controller).Contains("Back"));

        Assert.Equal(2, _agent.Launches.Count);
        Assert.Contains("Claude Code exited. Your next question starts it again.", Chat(_controller));
    }

    [Fact]
    public async Task A_missing_agent_says_its_program_isnt_on_PATH()
    {
        _agent.Missing = true;

        _controller.Ask("Hi", null);
        await Until(() => !_controller.IsAnswering);

        Assert.Contains("npx isn't on PATH, so Claude Code can't start.", Chat(_controller));
    }
}
