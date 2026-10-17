using MentalDesk.Tui.Assistant;

namespace MentalDesk.Tui.Tests;

public class ConversationTests
{
    private static Conversation New() => new() { AgentName = "Claude Code" };

    private static IEnumerable<(ChatLineKind, string)> Rows(Conversation conversation, int width = 40 + Conversation.Gutter) =>
        conversation.Render(width).Select(l => (l.Kind, l.Text));

    [Fact]
    public void A_question_and_its_streamed_answer_read_under_their_headings()
    {
        var chat = New();
        chat.AddQuestion("Why?", "on #499 in Work");
        chat.Apply(new MessageChunk(ChatRole.Agent, "Because "));
        chat.Apply(new MessageChunk(ChatRole.Agent, "it does."));

        Assert.Equal(
        [
            (ChatLineKind.Heading, "You · on #499 in Work"), (ChatLineKind.Text, "Why?"), (ChatLineKind.Blank, ""),
            (ChatLineKind.Heading, "Claude Code"), (ChatLineKind.Text, "Because it does."),
        ], Rows(chat));
    }

    [Fact]
    public void A_tool_call_is_one_line_updated_in_place()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("t", "Read", "pending"));
        chat.Apply(new ToolCallUpdate("t", "Read board.sh", "completed"));
        chat.Apply(new ToolCallUpdate("u", "Searched \"blocked\"", "failed"));

        Assert.Equal(
        [
            (ChatLineKind.Heading, "Claude Code"),
            (ChatLineKind.Tool, "▸ Read board.sh"),
            (ChatLineKind.Tool, "▸ Searched \"blocked\" (failed)"),
        ], Rows(chat));
    }

    [Fact]
    public void Long_text_wraps_at_the_width()
    {
        var chat = New();
        chat.Apply(new MessageChunk(ChatRole.Agent, "one two three four five"));

        Assert.Equal(["Claude Code", "one two", "three four", "five"], Rows(chat, 12 + Conversation.Gutter).Select(r => r.Item2));
    }

    [Fact]
    public void A_fenced_block_is_code_rows_that_keep_their_indent_and_break_anywhere()
    {
        var chat = New();
        chat.Apply(new MessageChunk(ChatRole.Agent, "See:\n```csharp\nvar x = 1;\n\tReturn();\n```\nDone."));

        Assert.Equal(
        [
            (ChatLineKind.Heading, "Claude Code"), (ChatLineKind.Text, "See:"),
            (ChatLineKind.Code, "var x = 1;"), (ChatLineKind.Code, "    Return();"), (ChatLineKind.Text, "Done."),
        ], Rows(chat));
        Assert.Equal(["abcd", "efgh", "ij"], Rows(Answer("```\nabcdefghij\n```"), 4 + Conversation.Gutter).Where(r => r.Item1 == ChatLineKind.Code).Select(r => r.Item2));
    }

    [Fact]
    public void Each_side_leaves_a_gutter_and_says_whose_rows_they_are()
    {
        var chat = New();
        chat.AddQuestion("aaaaa bbbbb", null);
        chat.Apply(new MessageChunk(ChatRole.Agent, "ccccc ddddd"));
        chat.AddNote("Stopped.");

        var lines = chat.Render(11 + Conversation.Gutter);

        Assert.Equal(
        [
            ("You", ChatRole.User), ("aaaaa bbbbb", ChatRole.User), ("", null),
            ("Claude Code", ChatRole.Agent), ("ccccc ddddd", ChatRole.Agent), ("", null),
            ("Stopped.", null),
        ], lines.Select(l => (l.Text, l.Role)));
    }

    [Fact]
    public void A_reloaded_question_takes_its_heading_from_the_context_its_text_carries()
    {
        var chat = New();
        chat.Apply(new MessageChunk(ChatRole.User, QuestionText.Compose("on #499 in Work", "Why so long?")));

        Assert.Equal([(ChatLineKind.Heading, "You · on #499 in Work"), (ChatLineKind.Text, "Why so long?")], Rows(chat));
    }

    [Fact]
    public void A_reloaded_question_with_a_resource_takes_its_heading_from_the_app()
    {
        var chat = New();
        chat.ResourceLabel = uri => uri.EndsWith("#L41-58", StringComparison.Ordinal) ? "lines 41–58 of a.cs" : null;
        chat.Apply(new MessageChunk(ChatRole.User, "Why?"));
        chat.Apply(new ContextChunk(ChatRole.User, "file:///work/a.cs#L41-58"));

        Assert.Equal("You · lines 41–58 of a.cs", Rows(chat).First().Item2);
    }

    [Theory]
    [InlineData("Why?", null, "Why?")]
    [InlineData("[on #499 in Work]\n\nWhy?", "on #499 in Work", "Why?")]
    [InlineData("[not a context] Why?", null, "[not a context] Why?")]
    [InlineData("[two\nlines]\n\nWhy?", null, "[two\nlines]\n\nWhy?")]
    public void Question_text_splits_off_only_a_context_it_composed(string text, string? context, string question) =>
        Assert.Equal((context, question), QuestionText.Split(text));

    [Fact]
    public void A_note_stands_apart_and_an_answer_after_it_gets_its_own_heading()
    {
        var chat = New();
        chat.Apply(new MessageChunk(ChatRole.Agent, "Partial"));
        chat.AddNote("Stopped.");
        chat.Apply(new MessageChunk(ChatRole.Agent, "More"));

        Assert.Equal(["Claude Code", "Partial", "", "Stopped.", "", "Claude Code", "More"], Rows(chat).Select(r => r.Item2));
    }

    private static Conversation Answer(string text)
    {
        var chat = New();
        chat.Apply(new MessageChunk(ChatRole.Agent, text));
        return chat;
    }
}
