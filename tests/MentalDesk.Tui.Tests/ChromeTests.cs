using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Theming;

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

    private static readonly CommandScope Themes = new("Themes");
    private static readonly CommandScope Roles = new("Roles");

    [Fact]
    public void A_menu_item_shows_a_key_that_works_wherever_its_command_runs()
    {
        var commands = new CommandRegistry()
            .Register("try", "Try", () => { })
            .Register("delete", "Delete", () => { }, Themes);
        var keys = new Keymap(commands)
            .Bind("Ctrl+T T", "try")
            .Bind("Enter", "try", new CommandScope("Preview"))
            .Bind("Ctrl+T D", "delete");
        var menu = new AppMenu(commands, keys, [new MenuSpec("_Theme", ["try", "delete"])]);

        Assert.Equal(["Ctrl+T T", "Ctrl+T D"], menu.Items.Select(entry => entry.Item.KeyView.Text));
    }

    [Fact]
    public void A_menu_dims_what_can_t_run_where_the_keys_are()
    {
        var scope = Roles;
        var commands = new CommandRegistry()
            .Register("try", "Try", () => { })
            .Register("delete", "Delete", () => { }, Themes)
            .Register("export", "Export", () => { }, isEnabled: () => false);
        var keys = new Keymap(commands) { FocusedScope = () => scope };
        var menu = new AppMenu(commands, keys, [new MenuSpec("_Theme", ["try", "delete", "export"])]);

        menu.Refresh();
        var inRoles = menu.Items.Select(entry => menu.IsDimmed(entry.Id)).ToList();
        scope = Themes;
        menu.Refresh();

        Assert.Equal([false, true, true], inRoles);
        Assert.Equal([false, false, true], menu.Items.Select(entry => menu.IsDimmed(entry.Id)));
    }

    [Fact]
    public void A_dimmed_menu_item_dims_its_key_with_it()
    {
        using var menu = new View { SchemeName = SchemeNames.Menu };
        var item = new CommandMenuItem { Title = "_Delete", Key = Key.F2 };
        menu.Add(item);
        var live = item.KeyView.GetAttributeForRole(VisualRole.Normal);

        item.Dimmed = true;

        Assert.Equal(menu.GetAttributeForRole(VisualRole.HotNormal), live);
        Assert.Equal(item.GetAttributeForRole(VisualRole.Disabled), item.KeyView.GetAttributeForRole(VisualRole.Normal));
    }

    [Fact]
    public void Of_two_opposite_actions_the_menu_shows_the_one_that_can_run_in_their_shared_place()
    {
        bool? expanded = true;
        var commands = new CommandRegistry()
            .Register("collapse", "Collapse", () => { }, isEnabled: () => expanded == true)
            .Register("expand", "Expand", () => { }, isEnabled: () => expanded == false)
            .Register("reload", "Reload", () => { });
        var menu = new AppMenu(commands, new Keymap(commands),
        [
            new MenuSpec("_Folders", [new MenuEntry("collapse", "_Collapse") { Opposite = new("expand", "_Expand") }]),
            new MenuSpec("_View", ["reload"]),
        ]);
        using var bar = menu.Bar;
        var seen = new List<string>();

        foreach (var state in new bool?[] { true, false, null })
        {
            expanded = state;
            menu.Refresh();
            menu.ShowAvailable();
            var title = menu.Menus[0].PopoverMenu!.Root!.SubViews.OfType<MenuItem>().Single().Title;
            seen.Add($"{string.Join(" ", menu.Shown.Select(item => item.Title))}: {title}{(menu.IsDimmed("collapse") && title == "_Collapse" ? ", dimmed" : "")}");
        }

        Assert.Equal(["_Folders _View: _Collapse", "_Folders _View: _Expand", "_View: _Collapse, dimmed"], seen);
    }

    [Fact]
    public void A_menu_with_nothing_that_can_run_here_leaves_the_bar_until_something_can()
    {
        var scope = Themes;
        var commands = new CommandRegistry()
            .Register("quit", "Quit", () => { })
            .Register("copy", "Copy colours", () => { }, Roles)
            .Register("keys", "Show keys", () => { });
        var keys = new Keymap(commands) { FocusedScope = () => scope };
        var menu = new AppMenu(commands, keys,
            [new MenuSpec("_File", ["quit"]), new MenuSpec("_Edit", ["copy"]), new MenuSpec("_Help", ["keys"])]);
        using var bar = menu.Bar;

        menu.ShowAvailable();
        var inThemes = menu.Shown.Select(item => item.Title).ToList();
        scope = Roles;
        menu.ShowAvailable();

        Assert.Equal(["_File", "_Help"], inThemes);
        Assert.Equal(["_File", "_Edit", "_Help"], menu.Shown.Select(item => item.Title));
    }
}
