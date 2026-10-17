using System.Text;
using System.Text.Json.Nodes;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Focus;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Assistant;

// A pane to ask an agent about what's on screen: which agent and how it's doing, the chat, and a box for the next question.
// It takes its own keys, Enter to send, Esc to stop and Ctrl+L to start again, so it works in an app without AppShell.
public sealed class AssistantPane : View
{
    public const string SendId = "assistant.send";
    public const string StopId = "assistant.stop";
    public const string ClearId = "assistant.clear";

    private const int InputHeight = 4;
    private static readonly Rune NoHotKey = (Rune)0xffff;

    private readonly Label _header;
    private readonly Label _context;
    private readonly MultiLineField _input;
    private string? _place;
    private string? _nextContext;

    public AssistantPane(AssistantController controller)
    {
        Controller = controller;
        Title = "Assistant";
        CanFocus = true;
        TabStop = TabBehavior.TabStop;
        BorderStyle = LineStyle.Single;
        _header = new Label { X = 0, Y = 0, Width = Dim.Fill(), Height = 1, HotKeySpecifier = NoHotKey, Text = "" };
        Chat = new ChatView(controller.Conversation) { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill(InputHeight + 1) };
        _context = new Label { X = 0, Y = Pos.AnchorEnd(InputHeight + 1), Width = Dim.Fill(), Height = 1, HotKeySpecifier = NoHotKey, Text = "" };
        _input = new MultiLineField { X = 0, Y = Pos.AnchorEnd(InputHeight), Width = Dim.Fill(), Height = InputHeight, Placeholder = "Ask a question" };
        // GetAttributeForRole isn't virtual, so the line is styled through its event; Handled makes the result stick.
        _context.GettingAttributeForRole += Faint;
        Add(_header, Chat, _context, _input);
        _input.KeyDown += OnKey;
        Chat.KeyDown += OnKey;
        controller.Changed += (_, _) => Refresh();
        Refresh();
    }

    public AssistantController Controller { get; }

    public ChatView Chat { get; }

    public string HeaderText => _header.Text;

    // Builds the prompt for a question and its context; null sends the question with the context ahead of it.
    public Func<string, string?, IReadOnlyList<JsonNode>?> Prompt { get; set; } = (_, _) => null;

    // Where the agent is working, shown after its name.
    public string? Place
    {
        get => _place;
        set
        {
            _place = value;
            Refresh();
        }
    }

    // What the next question is about, shown above the box and in its heading.
    public string? NextContext
    {
        get => _nextContext;
        set
        {
            _nextContext = value;
            _context.Text = value ?? "";
        }
    }

    public string Question
    {
        get => _input.Text.TrimEnd('\n', '\r');
        set => _input.Text = value;
    }

    public bool InputHasFocus => _input.HasFocus;

    public bool FocusInput() => _input.SetFocus();

    public void Send()
    {
        var question = Question.Trim();
        if (question.Length == 0 || Controller.IsAnswering) return;
        Controller.Ask(question, NextContext, Prompt(question, NextContext));
        Question = "";
    }

    public void Stop() => Controller.Stop();

    public void Clear() => Controller.Clear();

    // For an AppShell app: the pane's commands under its region in the keymap, so F1 lists them.
    public void Register(CommandRegistry commands, Keymap keys, CommandScope scope)
    {
        commands
            .Register(SendId, "Send question", Send, scope)
            .Register(StopId, "Stop answering", Stop, scope, isEnabled: () => Controller.IsAnswering)
            .Register(ClearId, "Start a new conversation", Clear, scope);
        keys.Bind("Enter", SendId, scope)
            .Bind("Esc", StopId, scope)
            .Bind("Ctrl+L", ClearId, scope);
    }

    // Registers the pane's region and commands with the shell, and asks the user there before the agent acts.
    public void Attach(AppShell shell, FocusRegion region)
    {
        Register(shell.Commands, shell.Keys, region.Scope);
        var border = new FocusBorder(this);
        shell.Focus.Register(region, () => Visible && FocusInput(), view => Within(view as View));
        shell.Focus.RegionChanged += (_, focused) => border.Show(focused == region);
        Controller.AskPermission = request => Task.FromResult(AssistantPermission.Ask(shell, request, Controller.Agent?.Name ?? "The agent"));
    }

    private bool Within(View? view)
    {
        for (; view is not null; view = view.SuperView)
            if (view == this) return true;
        return false;
    }

    private void OnKey(object? sender, Key key)
    {
        if (key == Key.Enter && sender == _input) Send();
        else if (key == Key.Esc && Controller.IsAnswering) Stop();
        else if (key == Key.L.WithCtrl) Clear();
        else return;
        key.Handled = true;
    }

    private void Refresh()
    {
        var agent = Controller.Agent;
        var state = agent is null ? "No agent"
            : $"{(Controller.IsStarting ? "◌" : "●")} {agent.Name}{(Place is null ? "" : $" · {Place}")}{(Controller.IsAnswering ? " · answering…" : "")}";
        _header.Text = state;
        Chat.Refresh();
    }

    private void Faint(object? sender, VisualRoleEventArgs e)
    {
        var attribute = e.Result ?? GetAttributeForRole(e.Role);
        e.Result = attribute with { Style = attribute.Style | TextStyle.Faint };
        e.Handled = true;
    }
}
