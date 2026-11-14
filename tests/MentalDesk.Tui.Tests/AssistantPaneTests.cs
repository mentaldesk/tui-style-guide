using MentalDesk.Tui.Assistant;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using Swatch;

namespace MentalDesk.Tui.Tests;

// Swatch's Assistant pane through the shell, against a fake agent.
public class AssistantPaneTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private readonly FakeAgent _agent = new();

    public AssistantPaneTests() => _host.App.Driver!.SetScreenSize(120, 30);

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private static string Chat(AssistantPane pane) => string.Join('\n', pane.Chat.Lines.Select(l => l.Text));

    private void Type(string text)
    {
        foreach (var c in text) _host.App.InjectKey(new Key(c));
    }

    private void Open() => _host.Shell.Commands.Execute(SwatchCommands.ToggleAssistant);

    [Fact]
    public void Choosing_Assistant_opens_it_in_Preview_s_place_with_the_question_box_focused_and_again_closes_it()
    {
        using var window = new SwatchWindow(_host.Shell, _agent);
        var opened = (Pane: false, Preview: true);

        _host.Run(window,
            () => FocusWord == "Themes",
            Open,
            () => window.Assistant.InputHasFocus,
            () => { opened = (window.Assistant.Visible, window.Preview.SuperView!.Visible); },
            Open,
            () => FocusWord == "Preview");

        Assert.Equal((true, false), opened);
        Assert.False(window.Assistant.Visible);
    }

    [Fact]
    public void A_question_goes_out_about_the_selection_and_its_answer_streams_in_under_it()
    {
        using var window = new SwatchWindow(_host.Shell, _agent);
        _agent.OnPrompt = async (agent, _) =>
        {
            agent.Say("Because ");
            agent.Tool("t", "Read themes.json", "completed");
            agent.Say("it reads well.");
            await Task.Yield();
        };

        _host.Run(window,
            () => FocusWord == "Themes",
            Open,
            () => window.Assistant.InputHasFocus,
            () => Type("Why?"),
            () => _host.App.InjectKey(Key.Enter),
            () => Chat(window.Assistant).Contains("reads well") && !window.Assistant.Controller.IsAnswering);

        Assert.Equal($"You · {window.Showing.Display}\nWhy?\n\nDemo agent\nBecause\n▸ Read themes.json\nit reads well.", Chat(window.Assistant));
        Assert.Equal("", window.Assistant.Question);
        Assert.StartsWith("● Demo agent · Swatch", window.Assistant.HeaderText);
    }

    [Fact]
    public void Esc_stops_the_answer_and_keeps_what_arrived()
    {
        using var window = new SwatchWindow(_host.Shell, _agent);
        string? whileAnswering = null;
        _agent.OnPrompt = async (agent, _) =>
        {
            agent.Say("Partial");
            await agent.Cancelled.Task;
        };

        _host.Run(window,
            Open,
            () => window.Assistant.InputHasFocus,
            () => Type("Long?"),
            () => _host.App.InjectKey(Key.Enter),
            () => Chat(window.Assistant).Contains("Partial"),
            () => { whileAnswering = window.Assistant.HeaderText; },
            () => _host.App.InjectKey(Key.Esc),
            () => Chat(window.Assistant).EndsWith("Stopped."));

        Assert.EndsWith("answering…", whileAnswering);
        Assert.DoesNotContain("answering", window.Assistant.HeaderText);
        Assert.True(window.Assistant.Visible);
    }

    [Fact]
    public void Ctrl_L_starts_a_new_conversation()
    {
        using var window = new SwatchWindow(_host.Shell, _agent);

        _host.Run(window,
            Open,
            () => window.Assistant.InputHasFocus,
            () => Type("One"),
            () => _host.App.InjectKey(Key.Enter),
            () => Chat(window.Assistant).Contains("Hello"),
            () => _host.App.InjectKey(Key.L.WithCtrl),
            () => Chat(window.Assistant) == "",
            () => Type("Two"),
            () => _host.App.InjectKey(Key.Enter),
            () => Chat(window.Assistant).Contains("Hello"));

        Assert.StartsWith("You · ", Chat(window.Assistant));
        Assert.EndsWith("Two\n\nDemo agent\nHello", Chat(window.Assistant));
        Assert.Equal(2, _agent.Received("session/new").Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_command_the_agent_wants_to_run_is_asked_about_with_Deny_focused(bool allow)
    {
        using var window = new SwatchWindow(_host.Shell, _agent);
        string? answer = "unset";
        (string? Title, string? Focused, string[] Buttons) shown = (null, null, []);
        _agent.OnPrompt = async (agent, _) =>
        {
            answer = await agent.AskPermission("execute", "git log", "git log --oneline -5");
            agent.Say("Carrying on.");
        };

        _host.Run(window,
            Open,
            () => window.Assistant.InputHasFocus,
            () => Type("Go"),
            () => _host.App.InjectKey(Key.Enter),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () =>
            {
                var dialog = (ConfirmDialog)_host.App.TopRunnableView!;
                shown = (dialog.Title, dialog.ButtonRow.SingleOrDefault(b => b.HasFocus)?.Text.Trim(), [.. dialog.ButtonRow.Select(b => b.Text.Trim())]);
            },
            () => _host.App.InjectKey(allow ? CtrlEnter.Key : Key.Esc),
            () => Chat(window.Assistant).Contains("Carrying on."));

        Assert.Equal(("Demo agent wants to run a command", "Esc Deny"), (shown.Title, shown.Focused));
        Assert.Equal(["Ctrl+Enter Allow once", "Esc Deny"], shown.Buttons);
        Assert.Equal(allow ? "allow" : "reject", answer);
    }

    [Fact]
    public void Enter_on_Deny_does_nothing_and_the_command_waits()
    {
        using var window = new SwatchWindow(_host.Shell, _agent);
        string? answer = "unset";
        var ticks = 0;
        _agent.OnPrompt = async (agent, _) => answer = await agent.AskPermission("execute", "rm", "rm -rf build");

        _host.Run(window,
            Open,
            () => window.Assistant.InputHasFocus,
            () => Type("Go"),
            () => _host.App.InjectKey(Key.Enter),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () => _host.App.InjectKey(Key.Enter),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView is ConfirmDialog && answer == "unset",
            () => _host.App.InjectKey(Key.Esc),
            () => answer == "reject");
    }

    [Fact]
    public void F1_in_the_pane_lists_its_keys()
    {
        using var window = new SwatchWindow(_host.Shell, _agent);
        KeySheet? sheet = null;

        _host.Run(window,
            Open,
            () => FocusWord == "Assistant",
            () => _host.App.InjectKey(Key.F1),
            () => _host.App.TopRunnableView is KeysDialog,
            () => { sheet = ((KeysDialog)_host.App.TopRunnableView!).Sheet; },
            () => _host.App.InjectKey(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("Assistant", sheet!.Here!.Heading);
        // Esc only stops an answer, so it's listed while one is coming.
        Assert.Equal(
            [("Enter", "Send question"), ("Ctrl+L", "Start a new conversation"), ("Ctrl+T T", "Show tool calls in full")],
            sheet.Here.Groups.SelectMany(g => g.Rows).Select(r => (r.Keys, r.Label)));
    }

    [Fact]
    public void Ctrl_T_T_in_the_pane_shows_every_call_and_again_folds_them_where_elsewhere_it_tries_the_theme()
    {
        using var window = new SwatchWindow(_host.Shell, _agent);
        string? inFull = null;
        _agent.OnPrompt = async (agent, _) =>
        {
            agent.Tool("a", "Read a.cs", "completed");
            agent.Tool("b", "Read b.cs", "completed");
            agent.Say("Done.");
            await Task.Yield();
        };
        void Chord()
        {
            _host.App.InjectKey(Key.T.WithCtrl);
            _host.App.InjectKey(Key.T);
        }

        _host.Run(window,
            Open,
            () => window.Assistant.InputHasFocus,
            () => Type("Why?"),
            () => _host.App.InjectKey(Key.Enter),
            () => Chat(window.Assistant).EndsWith("Done.") && !window.Assistant.Controller.IsAnswering,
            Chord,
            () => Chat(window.Assistant).Contains("▸ Read a.cs"),
            () => { inFull = Chat(window.Assistant); },
            Chord,
            () => !Chat(window.Assistant).Contains("▸ Read a.cs"));

        Assert.EndsWith("▸ Read a.cs\n▸ Read b.cs\nDone.", inFull);
        Assert.EndsWith("Demo agent\n▸ Read b.cs (+1)\nDone.", Chat(window.Assistant));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Toggling_keeps_the_row_at_the_top_in_view(bool startInFull)
    {
        var conversation = new Conversation { AgentName = "Claude Code", StartsInFull = startInFull };
        conversation.Clear();
        conversation.Apply(new MessageChunk(ChatRole.Agent, string.Join('\n', Enumerable.Range(1, 10).Select(i => $"before {i}"))));
        foreach (var call in Enumerable.Range(1, 5)) conversation.Apply(new ToolCallUpdate($"{call}", $"Read {call}.cs", "completed"));
        conversation.Apply(new MessageChunk(ChatRole.Agent, string.Join('\n', Enumerable.Range(1, 40).Select(i => $"after {i}"))));
        var chat = new ChatView(conversation) { Width = 40, Height = 10 };
        chat.SetRelativeLayout(new System.Drawing.Size(40, 10));
        var rows = chat.Lines.Select(l => l.Text).ToList();
        chat.Viewport = chat.Viewport with { Y = rows.IndexOf("after 3") };

        chat.ShowToolCallsInFull(!startInFull);

        Assert.Equal("after 3", chat.Lines[chat.Viewport.Y].Text);
    }

    [Fact]
    public void Toggling_at_the_end_stays_at_the_end()
    {
        var conversation = new Conversation { AgentName = "Claude Code" };
        foreach (var call in Enumerable.Range(1, 5)) conversation.Apply(new ToolCallUpdate($"{call}", $"Read {call}.cs", "completed"));
        conversation.Apply(new MessageChunk(ChatRole.Agent, string.Join('\n', Enumerable.Range(1, 40).Select(i => $"after {i}"))));
        var chat = new ChatView(conversation) { Width = 40, Height = 10 };
        chat.SetRelativeLayout(new System.Drawing.Size(40, 10));
        chat.Viewport = chat.Viewport with { Y = chat.Lines.Count - 10 };

        chat.ShowToolCallsInFull(true);

        Assert.True(chat.AtBottom);
    }

    [Fact]
    public void Folding_with_a_folded_away_call_at_the_top_lands_on_the_row_that_stands_for_it()
    {
        var conversation = new Conversation { AgentName = "Claude Code", ToolCallsInFull = true };
        foreach (var call in Enumerable.Range(1, 5)) conversation.Apply(new ToolCallUpdate($"{call}", $"Read {call}.cs", "completed"));
        conversation.Apply(new MessageChunk(ChatRole.Agent, string.Join('\n', Enumerable.Range(1, 40).Select(i => $"after {i}"))));
        var chat = new ChatView(conversation) { Width = 40, Height = 10 };
        chat.SetRelativeLayout(new System.Drawing.Size(40, 10));
        chat.Viewport = chat.Viewport with { Y = 2 };

        chat.ShowToolCallsInFull(false);

        Assert.Equal("▸ Read 5.cs (+4)", chat.Lines[chat.Viewport.Y].Text);
    }

    [Fact]
    public void An_agent_that_isn_t_installed_says_so_in_the_pane()
    {
        _agent.Missing = true;
        using var window = new SwatchWindow(_host.Shell, _agent);

        _host.Run(window,
            Open,
            () => Chat(window.Assistant).Contains("isn't on PATH"));

        Assert.Equal("demo isn't on PATH, so Demo agent can't start.", Chat(window.Assistant).Replace('\n', ' '));
    }
}
