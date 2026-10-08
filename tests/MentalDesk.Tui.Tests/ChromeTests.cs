using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Dialogs;

namespace MentalDesk.Tui.Tests;

public class ChromeTests : StaticConfigurationTest
{
    [Fact]
    public void The_status_bar_names_the_focused_region_first()
    {
        using var bar = new AppStatusBar();

        bar.SetFocusWord("Roles");

        Assert.Equal("Roles", bar.FocusWord);
    }

    [Fact]
    public void A_chord_in_flight_takes_the_hints_place_then_gives_it_back()
    {
        using var bar = new AppStatusBar();
        bar.Hints.Show([("Ctrl+E commands", () => { })]);

        bar.SetChord("Ctrl+G");
        var during = bar.Says;
        bar.SetChord(null);

        Assert.Equal("Ctrl+G…", during);
        Assert.Equal("Ctrl+E commands", bar.Says);
    }

    [Fact]
    public void A_message_replaces_the_hints_until_it_is_cleared()
    {
        using var bar = new AppStatusBar();
        bar.Hints.Show([("Ctrl+E commands", () => { }), ("Ctrl+Q quit", () => { })]);

        bar.ShowMessage("Couldn't read theme", Severity.Error);
        var during = bar.Says;
        bar.ClearMessage();

        Assert.Equal("Couldn't read theme", during);
        Assert.Equal($"Ctrl+E commands{HintRow.Separator}Ctrl+Q quit", bar.Says);
    }

    [Fact]
    public void A_hint_is_clickable_and_runs_its_command()
    {
        using var row = new HintRow();
        var ran = false;
        row.Show([("Enter run", () => ran = true)]);

        row.SubViews.OfType<Button>().Single().InvokeCommand(Command.Accept);

        Assert.True(ran);
    }

    [Fact]
    public void A_message_block_takes_no_rows_until_there_is_something_to_say()
    {
        using var alert = new AlertView();
        Assert.Equal(0, alert.Lines);

        alert.Show("Couldn't write themes.json: the folder is read-only, so nothing was saved", Severity.Error, width: 30);

        Assert.True(alert.Lines > 1);
        Assert.True(alert.Visible);
        alert.Clear();
        Assert.Equal(0, alert.Lines);
        Assert.False(alert.Visible);
    }

    [Fact]
    public void A_checked_menu_entry_is_marked_and_follows_its_state_on_refresh()
    {
        var current = "a";
        var commands = new CommandRegistry().Register("a", "A", () => { }).Register("b", "B", () => { });
        var menu = new AppMenu(commands, new Keymap(commands),
            [new MenuSpec("_Pick", [new MenuEntry("a", "_A", () => current == "a"), new MenuEntry("b", "_B", () => current == "b")])]);
        var before = menu.Items.Select(entry => entry.Item.Title).ToList();

        current = "b";
        menu.Refresh();

        Assert.Equal(["● _A", "  _B"], before);
        Assert.Equal(["  _A", "● _B"], menu.Items.Select(entry => entry.Item.Title));
    }

    [Fact]
    public void A_menu_item_shows_only_a_key_that_works_everywhere()
    {
        var commands = new CommandRegistry().Register("try", "Try", () => { });
        var keys = new Keymap(commands).Bind("Ctrl+T T", "try").Bind("Enter", "try", new CommandScope("Preview"));
        var menu = new AppMenu(commands, keys, [new MenuSpec("_Theme", ["try"])]);

        Assert.Equal("Ctrl+T T", menu.Items.Single().Item.KeyView.Text);
    }
}
