using System.Globalization;
using System.Text;
using Terminal.Gui.Text;

namespace MentalDesk.Tui.Assistant;

public enum ChatLineKind { Blank, Heading, Text, Tool, Note, Code }

// One screen row of the chat, and who said it: null for a note.
public sealed record ChatLine(ChatLineKind Kind, string Text, ChatRole? Role = null);

// Questions, streamed answers with a line per tool call, and notes. Framework-free.
public sealed class Conversation
{
    private const int TabWidth = 4;

    // Columns left blank left of the user's turns and right of the agent's.
    public const int Gutter = 4;

    private readonly List<Entry> _entries = [];

    // Changes on every edit, so a view knows to lay out again.
    public int Version { get; private set; }

    public bool IsEmpty => _entries.Count == 0;

    public string AgentName { get; set; } = "Agent";

    // The heading for a reloaded question's attached resource; null shows none.
    public Func<string, string?> ResourceLabel { get; set; } = _ => null;

    public void Clear()
    {
        _entries.Clear();
        Version++;
    }

    public void AddQuestion(string question, string? context)
    {
        _entries.Add(new Turn(ChatRole.User) { Context = context });
        Append(ChatRole.User, question);
    }

    public void AddNote(string note)
    {
        _entries.Add(new Note(note));
        Version++;
    }

    // So a reply after a note doesn't run on from the last one.
    public void EndTurn() => _entries.Add(new Note(null));

    public void Apply(SessionUpdate update)
    {
        switch (update)
        {
            case MessageChunk chunk:
                Append(chunk.Role, chunk.Text);
                break;
            case ContextChunk { Role: ChatRole.User } context:
                TurnFor(ChatRole.User).Context ??= ResourceLabel(context.Uri);
                Version++;
                break;
            case ToolCallUpdate call:
                var turn = TurnFor(ChatRole.Agent);
                if (turn.Parts.OfType<ToolPart>().FirstOrDefault(p => p.Id == call.Id) is { } known)
                {
                    known.Title = call.Title ?? known.Title;
                    known.Status = call.Status ?? known.Status;
                }
                else turn.Parts.Add(new ToolPart(call.Id) { Title = call.Title ?? "Tool", Status = call.Status });
                Version++;
                break;
        }
    }

    private void Append(ChatRole role, string text)
    {
        var turn = TurnFor(role);
        if (turn.Parts.LastOrDefault() is not TextPart part) turn.Parts.Add(part = new TextPart());
        part.Text.Append(text);
        Version++;
    }

    private Turn TurnFor(ChatRole role)
    {
        if (_entries.LastOrDefault() is Turn turn && turn.Role == role) return turn;
        var started = new Turn(role);
        _entries.Add(started);
        return started;
    }

    public IReadOnlyList<ChatLine> Render(int width)
    {
        var lines = new List<ChatLine>();
        var turnWidth = Math.Max(width - Gutter, 1);
        foreach (var entry in _entries)
        {
            switch (entry)
            {
                case Note { Text: { } text }:
                    Gap(lines);
                    Wrap(lines, ChatLineKind.Note, text, width);
                    break;
                case Turn { Role: ChatRole.User } turn:
                    Gap(lines);
                    var asked = string.Concat(turn.Parts.OfType<TextPart>().Select(p => p.Text.ToString()));
                    var (context, question) = turn.Context is null ? QuestionText.Split(asked) : (turn.Context, asked);
                    Wrap(lines, ChatLineKind.Heading, context is null ? "You" : $"You · {context}", turnWidth, ChatRole.User);
                    foreach (var line in question.Trim('\n').Split('\n')) Wrap(lines, ChatLineKind.Text, line, turnWidth, ChatRole.User);
                    break;
                case Turn turn:
                    Gap(lines);
                    Wrap(lines, ChatLineKind.Heading, AgentName, turnWidth, ChatRole.Agent);
                    foreach (var part in turn.Parts)
                    {
                        if (part is ToolPart tool) Wrap(lines, ChatLineKind.Tool, $"▸ {tool.Title}{(tool.Status == "failed" ? " (failed)" : "")}", turnWidth, ChatRole.Agent);
                        else if (part is TextPart text) Markdown(lines, text.Text.ToString(), turnWidth);
                    }
                    break;
            }
        }
        return lines;
    }

    private static void Gap(List<ChatLine> lines)
    {
        if (lines.Count > 0) lines.Add(new ChatLine(ChatLineKind.Blank, ""));
    }

    // Text between ``` fences is code: its rows break anywhere and keep their spaces.
    private static void Markdown(List<ChatLine> lines, string text, int width)
    {
        var code = false;
        foreach (var raw in text.TrimStart('\n').Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                code = !code;
                continue;
            }
            if (code) Wrap(lines, ChatLineKind.Code, line.Replace("\t", new string(' ', TabWidth)), width, ChatRole.Agent, words: false);
            else Wrap(lines, ChatLineKind.Text, line, width, ChatRole.Agent);
        }
    }

    private static void Wrap(List<ChatLine> lines, ChatLineKind kind, string text, int width, ChatRole? role = null, bool words = true)
    {
        var graphemes = Graphemes(text);
        var starts = RowStarts(graphemes, width, words);
        for (var row = 0; row < starts.Count; row++)
        {
            var end = row + 1 < starts.Count ? starts[row + 1] : graphemes.Count;
            var piece = string.Concat(graphemes.Skip(starts[row]).Take(end - starts[row]));
            lines.Add(new ChatLine(kind, words ? piece.TrimEnd() : piece, role));
        }
    }

    // Breaks after the last space that fits, or mid-word when there's none; spaces at a break may run past the edge.
    private static List<int> RowStarts(IReadOnlyList<string> graphemes, int width, bool words)
    {
        width = Math.Max(width, 1);
        var starts = new List<int> { 0 };
        var (start, column, lastSpace) = (0, 0, -1);
        for (var i = 0; i < graphemes.Count; i++)
        {
            var grapheme = graphemes[i];
            var cells = Math.Max(grapheme.GetColumns(), 1);
            if (column + cells > width && i > start && !(words && grapheme == " "))
            {
                var afterSpace = words && lastSpace >= start ? lastSpace + 1 : i;
                var carried = Columns(graphemes, afterSpace, i);
                (start, column) = afterSpace < i && carried + cells <= width ? (afterSpace, carried) : (i, 0);
                starts.Add(start);
                lastSpace = -1;
            }
            column += cells;
            if (grapheme == " ") lastSpace = i;
        }
        return starts;
    }

    private static int Columns(IReadOnlyList<string> graphemes, int from, int to)
    {
        var columns = 0;
        for (var i = from; i < to; i++) columns += Math.Max(graphemes[i].GetColumns(), 1);
        return columns;
    }

    private static List<string> Graphemes(string text)
    {
        var result = new List<string>(text.Length);
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) result.Add(enumerator.GetTextElement());
        return result;
    }

    private abstract class Entry;

    private sealed class Note(string? text) : Entry
    {
        public string? Text { get; } = text;
    }

    private sealed class Turn(ChatRole role) : Entry
    {
        public ChatRole Role { get; } = role;
        public string? Context { get; set; }
        public List<object> Parts { get; } = [];
    }

    private sealed class TextPart
    {
        public StringBuilder Text { get; } = new();
    }

    private sealed class ToolPart(string id)
    {
        public string Id { get; } = id;
        public string Title { get; set; } = "";
        public string? Status { get; set; }
    }
}

// How a question's context travels in its text, so a reloaded conversation can show it again.
public static class QuestionText
{
    public static string Compose(string? context, string question) => context is null ? question : $"[{context}]\n\n{question}";

    public static (string? Context, string Question) Split(string text)
    {
        if (!text.StartsWith('[')) return (null, text);
        var end = text.IndexOf("]\n\n", StringComparison.Ordinal);
        return end < 2 || text.AsSpan(1, end - 1).Contains('\n') ? (null, text) : (text[1..end], text[(end + 3)..]);
    }
}
