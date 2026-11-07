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

    private static IEnumerable<string> Tools(Conversation chat, int width = 40 + Conversation.Gutter) =>
        Rows(chat, width).Where(r => r.Item1 == ChatLineKind.Tool).Select(r => r.Item2);

    [Fact]
    public void A_run_of_calls_folds_into_its_newest_with_a_count_of_the_rest()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", "Read b.cs", "completed"));
        chat.Apply(new ToolCallUpdate("c", "grep -rn Search src", "pending"));
        chat.Apply(new MessageChunk(ChatRole.Agent, "Yes."));

        Assert.Equal(
        [
            (ChatLineKind.Heading, "Claude Code"),
            (ChatLineKind.Tool, "▸ grep -rn Search src (+2)"),
            (ChatLineKind.Text, "Yes."),
        ], Rows(chat));
    }

    [Fact]
    public void A_new_call_updates_the_folded_row_rather_than_adding_one()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "pending"));
        var before = chat.Render(44).Count;
        chat.Apply(new ToolCallUpdate("b", "Read b.cs", "pending"));
        chat.Apply(new ToolCallUpdate("c", "Read c.cs", "pending"));
        chat.Apply(new ToolCallUpdate("c", "Read c.cs in full", "completed"));
        chat.Apply(new ToolCallUpdate("a", "Read a.cs in full", "completed"));

        Assert.Equal(before, chat.Render(44).Count);
        Assert.Equal(["▸ Read c.cs in full (+2)"], Tools(chat));
    }

    [Fact]
    public void Text_between_calls_gives_each_run_its_own_row_so_the_answer_reads_in_order()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", "Read b.cs", "completed"));
        chat.Apply(new MessageChunk(ChatRole.Agent, "First."));
        chat.Apply(new ToolCallUpdate("c", "Read c.cs", "completed"));
        chat.Apply(new ToolCallUpdate("d", "Read d.cs", "completed"));
        chat.Apply(new ToolCallUpdate("e", "Read e.cs", "completed"));
        chat.Apply(new MessageChunk(ChatRole.Agent, "\n\nSecond."));
        chat.Apply(new ToolCallUpdate("f", "Read f.cs", "completed"));

        Assert.Equal(
        [
            (ChatLineKind.Heading, "Claude Code"),
            (ChatLineKind.Tool, "▸ Read b.cs (+1)"),
            (ChatLineKind.Text, "First."),
            (ChatLineKind.Tool, "▸ Read e.cs (+2)"),
            (ChatLineKind.Text, "Second."),
            (ChatLineKind.Tool, "▸ Read f.cs"),
        ], Rows(chat));
    }

    [Fact]
    public void A_failed_call_keeps_its_own_row_and_is_not_counted()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", "dotnet test", "failed"));
        chat.Apply(new ToolCallUpdate("c", "Read c.cs", "completed"));
        chat.Apply(new ToolCallUpdate("d", "Read d.cs", "completed"));

        Assert.Equal(["▸ dotnet test (failed)", "▸ Read d.cs (+2)"], Tools(chat));
    }

    [Fact]
    public void A_newest_call_that_goes_on_to_fail_hands_the_folded_row_to_the_one_before()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", "Read b.cs", "pending"));
        chat.Apply(new ToolCallUpdate("b", null, "failed"));

        Assert.Equal(["▸ Read a.cs", "▸ Read b.cs (failed)"], Tools(chat));
    }

    [Fact]
    public void Each_answer_folds_its_own_calls()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", "Read b.cs", "completed"));
        chat.EndTurn();
        chat.AddQuestion("And?", null);
        chat.Apply(new ToolCallUpdate("c", "Read c.cs", "completed"));

        Assert.Equal(["▸ Read b.cs (+1)", "▸ Read c.cs"], Tools(chat));
    }

    [Theory]
    [InlineData("Read src/Services/RecipeBook.cs", "▸ Read src/Services/Re… (+1)")]
    [InlineData("Read src/a.cs", "▸ Read src/a.cs (+1)")]
    [InlineData("two\nlines", "▸ two lines (+1)")]
    [InlineData("Read 日本語日本語日本語日本語日本語.cs", "▸ Read 日本語日本語日… (+1)")]
    public void A_folded_row_is_clipped_to_one_row_keeping_its_count(string title, string row)
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read x.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", title, "completed"));

        Assert.Equal([row], Tools(chat, 28 + Conversation.Gutter));
    }

    [Fact]
    public void A_failed_row_is_clipped_keeping_failed()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "dotnet test tests/Tui.Tests --filter Conversation", "failed"));

        Assert.Equal(["▸ dotnet test tests/… (failed)"], Tools(chat, 30 + Conversation.Gutter));
    }

    [Fact]
    public void In_full_every_call_has_its_row_and_a_long_one_wraps()
    {
        var chat = New();
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", "Read b.cs", "failed"));
        chat.Apply(new ToolCallUpdate("c", "Read a very long path name", "completed"));
        var version = chat.Version;

        chat.ToolCallsInFull = true;

        Assert.NotEqual(version, chat.Version);
        Assert.Equal(["▸ Read a.cs", "▸ Read b.cs (failed)", "▸ Read a very long path", "name"], Tools(chat, 24 + Conversation.Gutter));
    }

    [Fact]
    public void Clearing_starts_the_next_conversation_the_way_the_app_says()
    {
        var chat = New();
        Assert.False(chat.ToolCallsInFull);

        chat.StartsInFull = true;
        chat.Clear();
        Assert.True(chat.ToolCallsInFull);

        chat.ToolCallsInFull = false;
        chat.Clear();
        Assert.True(chat.ToolCallsInFull);
    }

    [Fact]
    public void A_reloaded_answer_folds_as_a_live_one_does()
    {
        var chat = New();
        chat.Apply(new MessageChunk(ChatRole.User, "Why?"));
        chat.Apply(new ToolCallUpdate("a", "Read a.cs", "completed"));
        chat.Apply(new ToolCallUpdate("b", "Read b.cs", "completed"));
        chat.Apply(new MessageChunk(ChatRole.Agent, "Because."));

        Assert.Equal(
        [
            (ChatLineKind.Heading, "You"), (ChatLineKind.Text, "Why?"), (ChatLineKind.Blank, ""),
            (ChatLineKind.Heading, "Claude Code"), (ChatLineKind.Tool, "▸ Read b.cs (+1)"), (ChatLineKind.Text, "Because."),
        ], Rows(chat));
    }

    private static Conversation Answer(string text)
    {
        var chat = New();
        chat.Apply(new MessageChunk(ChatRole.Agent, text));
        return chat;
    }
}
