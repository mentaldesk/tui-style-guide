using System.Text;
using MentalDesk.Tui.Copying;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class TerminalClipboardTests
{
    [Theory]
    [InlineData("hello")]
    [InlineData("one\ntwo\r\nthree\n")]
    [InlineData("café — 日本語 🙂")]
    public void Sequence_is_OSC_52_with_the_base64_of_the_UTF8_text(string text)
    {
        var sequence = TerminalClipboard.Sequence(text)!;

        Assert.StartsWith("\e]52;c;", sequence);
        Assert.EndsWith("\a", sequence);
        Assert.Equal(text, Encoding.UTF8.GetString(Convert.FromBase64String(sequence[7..^1])));
    }

    [Fact]
    public void Sequence_for_hello_is_the_known_one() =>
        Assert.Equal("\e]52;c;aGVsbG8=\a", TerminalClipboard.Sequence("hello"));

    [Fact]
    public void Send_takes_text_up_to_the_limit_in_bytes()
    {
        var sent = new List<string>();
        var clipboard = new TerminalClipboard(null, sent.Add, ClipboardProgram.Missing);

        Assert.True(clipboard.Send(new string('x', TerminalClipboard.MaxBytes)));
        Assert.False(clipboard.Send(new string('é', TerminalClipboard.MaxBytes / 2 + 1)));
        Assert.Single(sent);
    }

    [Fact]
    public void Setting_the_clipboard_sends_the_text_and_stores_it_on_the_platform_clipboard()
    {
        var sent = new List<string>();
        var platform = new TestClipboard();
        var clipboard = new TerminalClipboard(platform, sent.Add, ClipboardProgram.Missing);

        clipboard.SetClipboardData("a");

        Assert.Equal("a", platform.Text);
        Assert.Equal("a", clipboard.GetClipboardData());
        Assert.Equal([TerminalClipboard.Sequence("a")!], sent);
    }

    [Fact]
    public void Setting_the_clipboard_sends_the_text_even_when_the_platform_clipboard_throws()
    {
        var sent = new List<string>();
        var clipboard = new TerminalClipboard(new TestClipboard { Throw = new NotSupportedException() }, sent.Add, ClipboardProgram.Missing);

        Assert.True(clipboard.TrySetClipboardData("a"));
        Assert.Single(sent);
    }

    [Fact]
    public void Every_copy_says_what_it_did()
    {
        var clipboard = new TerminalClipboard(new TestClipboard(), _ => { }, ClipboardProgram.Missing);
        CopyOutcome? said = null;
        clipboard.Copied += (_, outcome) => said = outcome;

        clipboard.SetClipboardData("a");

        Assert.Equal(new CopyOutcome.Copied(1, 1), said);
    }

    [Theory]
    [InlineData(1, 18, false, "Copied 1 line  •  18 characters")]
    [InlineData(3, 1, false, "Copied 3 lines  •  1 character")]
    [InlineData(1, 18, true, "Copied 1 line  •  18 characters through the terminal")]
    [InlineData(2, 1234, false, "Copied 2 lines  •  1,234 characters")]
    public void A_copy_that_landed_is_reported_as_information(int lines, int characters, bool throughTerminal, string message)
    {
        var copied = new CopyOutcome.Copied(lines, characters, throughTerminal);

        Assert.Equal(message, copied.Message);
        Assert.Equal(Severity.Info, copied.Severity);
    }

    [Fact]
    public void A_failed_copy_is_reported_as_an_error_with_its_reason()
    {
        var failed = new CopyOutcome.Failed(TerminalClipboard.TooLarge);

        Assert.Equal("Copy failed: too large to send through the terminal (the limit is 750 KB)", failed.Message);
        Assert.Equal(Severity.Error, failed.Severity);
    }

    [Fact]
    public void Paste_reads_the_platform_clipboard_even_when_it_says_it_is_unsupported()
    {
        var program = new ClipboardProgram();
        var clipboard = new TerminalClipboard(new TestClipboard { Text = "from another app" }, _ => { }, ClipboardTools.For(true, false, false, program));

        Assert.True(clipboard.TryGetClipboardData(out var pasted));
        Assert.Equal("from another app", pasted);
        Assert.Empty(program.Ran);
    }

    [Fact]
    public void Paste_goes_through_the_platform_program_when_the_platform_clipboard_throws()
    {
        var program = new ClipboardProgram { Answer = command => command[0] == "pbpaste" ? new ToolRun.Exited(0, "from another app", "") : null };
        var clipboard = new TerminalClipboard(new TestClipboard { ThrowOnRead = new NotSupportedException() }, _ => { },
            ClipboardTools.For(true, false, false, program));

        Assert.Equal("from another app", clipboard.GetClipboardData());
    }

    [Fact]
    public void Paste_asks_the_platform_program_before_Terminal_Guis_in_process_clipboard()
    {
        var inProcess = new FakeClipboard();
        inProcess.SetClipboardData("copied here");
        var program = new ClipboardProgram { Answer = command => command[0] == "xclip" ? new ToolRun.Exited(0, "copied elsewhere", "") : null };
        var clipboard = new TerminalClipboard(inProcess, _ => { }, ClipboardTools.For(false, false, false, program));

        Assert.Equal("copied elsewhere", clipboard.GetClipboardData());
    }

    [Fact]
    public void Paste_falls_back_to_Terminal_Guis_in_process_clipboard_when_there_is_no_program()
    {
        var inProcess = new FakeClipboard();
        inProcess.SetClipboardData("copied here");
        var clipboard = new TerminalClipboard(inProcess, _ => { }, ClipboardProgram.Missing);

        Assert.Equal("copied here", clipboard.GetClipboardData());
    }
}
