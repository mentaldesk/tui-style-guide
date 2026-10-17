using System.Text.Json.Nodes;
using MentalDesk.Tui.Assistant;

namespace Swatch;

// Swatch has nothing for a real agent to look at, so its assistant is this: it streams a canned answer, shows a tool
// call, and asks to run a command, which the user can allow or deny.
internal sealed class DemoAgent : IAgentLauncher
{
    public static readonly AgentSpec Spec = new("demo", "Demo agent", "demo", [], null);

    public const string Command = "dotnet --version";

    private static readonly TimeSpan WordDelay = TimeSpan.FromMilliseconds(60);

    public TimeSpan Delay { get; init; } = WordDelay;

    public IAgentProcess Launch(string program, IReadOnlyList<string> arguments, string directory)
    {
        var agent = new InProcessAgent();
        var cancelled = new CancellationTokenSource();
        agent.Connection.NotificationReceived += (method, _) =>
        {
            if (method == "session/cancel") cancelled.Cancel();
        };
        agent.Connection.AsyncRequestHandler = async (method, parameters) =>
        {
            switch (method)
            {
                case "initialize":
                    return new JsonObject { ["protocolVersion"] = AcpClient.ProtocolVersion, ["agentCapabilities"] = new JsonObject() };
                case "session/new":
                    return new JsonObject { ["sessionId"] = "demo" };
                case "session/prompt":
                    cancelled = new CancellationTokenSource();
                    var text = parameters?["prompt"]?[0]?["text"]?.GetValue<string>() ?? "";
                    var stopped = await Answer(agent.Connection, QuestionText.Split(text).Context, cancelled.Token);
                    return new JsonObject { ["stopReason"] = stopped ? "cancelled" : "end_turn" };
                default:
                    throw new JsonRpcException(JsonRpcConnection.MethodNotFound, method);
            }
        };
        agent.Connection.Start();
        return agent;
    }

    // True when it was stopped.
    private async Task<bool> Answer(JsonRpcConnection connection, string? context, CancellationToken cancelled)
    {
        Update(connection, new JsonObject
        {
            ["sessionUpdate"] = "tool_call", ["toolCallId"] = "look", ["title"] = $"Looked at {context ?? "the screen"}",
            ["status"] = "completed",
        });
        if (await Say(connection, "This is Swatch's demo agent, so it can't really answer. In TuiCode and a-team the pane runs " +
            "Claude Code, which reads what you asked about and answers here. Next it asks before it runs a command.", cancelled))
            return true;
        var answer = await connection.RequestAsync("session/request_permission", new JsonObject
        {
            ["sessionId"] = "demo",
            ["toolCall"] = new JsonObject { ["toolCallId"] = "run", ["title"] = Command, ["kind"] = "execute", ["rawInput"] = new JsonObject { ["command"] = Command } },
            ["options"] = new JsonArray(
                new JsonObject { ["optionId"] = "allow", ["name"] = "Allow", ["kind"] = "allow_once" },
                new JsonObject { ["optionId"] = "reject", ["name"] = "Reject", ["kind"] = "reject_once" }),
        });
        var allowed = answer?["outcome"]?["optionId"]?.GetValue<string>() == "allow";
        return await Say(connection, allowed ? "\n\nYou allowed it, but this is a demo, so nothing ran." : "\n\nYou denied it, so it didn't run.", cancelled);
    }

    private async Task<bool> Say(JsonRpcConnection connection, string text, CancellationToken cancelled)
    {
        foreach (var word in text.Split(' '))
        {
            if (cancelled.IsCancellationRequested) return true;
            Update(connection, new JsonObject
            {
                ["sessionUpdate"] = "agent_message_chunk", ["content"] = new JsonObject { ["type"] = "text", ["text"] = word + " " },
            });
            try
            {
                await Task.Delay(Delay, cancelled);
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }
        return false;
    }

    private static void Update(JsonRpcConnection connection, JsonObject update) =>
        connection.Notify("session/update", new JsonObject { ["sessionId"] = "demo", ["update"] = update });
}
