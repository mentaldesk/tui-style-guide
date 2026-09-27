using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Tests;

public class TerminalCursorTests
{
    private readonly List<string> _written = [];

    [Fact]
    public void Colours_the_cursor_with_OSC_12()
    {
        var cursor = new TerminalCursor(_written.Add);

        cursor.Colour(new Color(0x1F, 0x23, 0x28));

        Assert.Equal(["\x1b]12;#1F2328\x07"], _written);
        Assert.Equal(new Color(0x1F, 0x23, 0x28), cursor.Asked);
    }

    [Fact]
    public void Gives_the_terminal_its_own_colour_back_only_if_it_was_changed()
    {
        var cursor = new TerminalCursor(_written.Add);
        cursor.Restore();
        cursor.Colour(new Color(0xE6, 0xE9, 0xEF));
        cursor.Restore();

        Assert.Equal(["\x1b]12;#E6E9EF\x07", "\x1b]112\x07"], _written);
    }

    [Fact]
    public void With_no_terminal_to_ask_the_report_says_there_was_no_answer()
    {
        var cursor = new TerminalCursor(_written.Add);
        var answered = false;

        cursor.Query(driver: null, () => answered = true);

        Assert.True(answered);
        Assert.Equal(CursorReport.NoAnswer, cursor.Report);
    }
}
