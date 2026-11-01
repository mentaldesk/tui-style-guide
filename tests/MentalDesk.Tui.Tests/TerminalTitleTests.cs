using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Tests;

public class TerminalTitleTests
{
    private const string Push = "\x1b[22;0t";
    private const string Pop = "\x1b[23;0t";

    private readonly List<string> _written = [];

    private static string Osc(string title) => $"\x1b]0;{title}\x07";

    [Theory]
    [InlineData("Swatch", "Midnight", 0, 0, "Midnight — Swatch")]
    [InlineData("Swatch", null, 0, 0, "Swatch")]
    [InlineData("Swatch sample", "src", 0, 1, "⚠ 1 src — Swatch sample")]
    [InlineData("a-team", "tui", 3, 0, "(3) tui — a-team")]
    [InlineData("Swatch sample", "src", 3, 1, "⚠ 1 (3) src — Swatch sample")]
    public void Says_what_is_waiting_then_what_it_is_open_on_then_the_app(string app, string? place, int waiting, int unseen, string title) =>
        Assert.Equal(title, TerminalTitle.Compose(app, place, waiting, unseen));

    [Fact]
    public void Works_on_its_own_saving_the_terminals_title_first_and_giving_it_back()
    {
        var title = new TerminalTitle(_written.Add) { App = "Swatch", Place = "Midnight" };

        title.Save();
        title.Show();
        title.Restore();

        Assert.Equal([Push, Osc("Midnight — Swatch"), Pop], _written);
    }

    [Fact]
    public void Is_rewritten_only_when_what_it_says_changes()
    {
        var title = new TerminalTitle(_written.Add) { App = "Swatch sample", Place = "src" };
        title.Show();

        title.Place = "src";
        title.UnseenErrors = 1;
        title.UnseenErrors = 1;
        title.Waiting = 3;
        title.UnseenErrors = 0;
        title.Waiting = 0;

        Assert.Equal(
        [
            Osc("src — Swatch sample"), Osc("⚠ 1 src — Swatch sample"), Osc("⚠ 1 (3) src — Swatch sample"),
            Osc("(3) src — Swatch sample"), Osc("src — Swatch sample"),
        ], _written);
    }

    [Fact]
    public void Writes_nothing_until_shown_or_while_held()
    {
        var title = new TerminalTitle(_written.Add) { App = "Swatch" };
        title.Place = "Midnight";
        title.Show();
        title.Hold();
        title.Place = "Daylight";
        title.Show();

        Assert.Equal([Osc("Midnight — Swatch"), Osc("Daylight — Swatch")], _written);
    }

    [Fact]
    public void Leaves_out_control_characters()
    {
        var title = new TerminalTitle(_written.Add) { App = "Swatch", Place = "a\x1b]0;b\x07" };
        title.Show();

        Assert.Equal([Osc("a]0;b — Swatch")], _written);
    }
}
