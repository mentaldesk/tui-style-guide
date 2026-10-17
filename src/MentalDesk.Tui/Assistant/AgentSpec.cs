namespace MentalDesk.Tui.Assistant;

// An agent run over ACP, and the command that signs in to it.
public sealed record AgentSpec(string Id, string Name, string Program, IReadOnlyList<string> Arguments, string? SignIn)
{
    // From the ACP registry's launch command.
    public static readonly AgentSpec ClaudeCode = new(
        "claude", "Claude Code", "npx", ["-y", "@agentclientprotocol/claude-agent-acp"], "claude auth login");

    public string CommandLine => string.Join(' ', [Program, .. Arguments]);
}
