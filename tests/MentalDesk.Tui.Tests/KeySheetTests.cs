using System.Drawing;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Tests;

public class KeySheetTests : StaticConfigurationTest
{
    private static readonly CommandScope Themes = new("Themes");
    private static readonly CommandScope Roles = new("Roles");

    private static readonly MenuSpec[] Menu =
    [
        new("_File", ["quit"]),
        new("_Go", ["go.themes", "go.roles"]),
        new("_Help", ["keys"]),
    ];

    private readonly CommandRegistry _commands = new();
    private readonly Keymap _keys;
    private bool _deleteEnabled = true;

    public KeySheetTests()
    {
        _commands
            .Register("quit", "Quit", () => { })
            .Register("go.themes", "Go to themes", () => { })
            .Register("go.roles", "Go to roles", () => { })
            .Register("keys", "Show keys", () => { })
            .Register("diagnostics", "Show diagnostics", () => { })
            .Register("unbound", "Never bound", () => { })
            .Register("delete", "Delete this theme", () => { }, Themes, isEnabled: () => _deleteEnabled)
            .Register("use", "Use this theme", () => { }, Themes);
        _keys = new Keymap(_commands)
            .Bind("Ctrl+Q", "quit")
            .Bind("Ctrl+G T", "go.themes")
            .Bind("Ctrl+G R", "go.roles")
            .Bind("F1", "keys")
            .Bind("F12", "diagnostics")
            .Bind("Ctrl+T U", "use")
            .Bind("Ctrl+T D", "delete");
    }

    private KeySheet Sheet(CommandScope scope) => KeySheet.For(_commands, _keys, scope.Name, scope, Menu);

    private static IEnumerable<string> Rows(KeyColumn column) =>
        column.Groups.SelectMany(group => group.Rows.Select(row => $"{group.Heading}: {row.Keys} {row.Label}"));

    [Fact]
    public void An_opposite_action_s_key_is_under_its_menu_heading()
    {
        _commands
            .Register("collapse", "Collapse folders", () => { }, isEnabled: () => false)
            .Register("expand", "Expand folders", () => { });
        _keys.Bind("Ctrl+K C", "collapse").Bind("Ctrl+K E", "expand");
        MenuSpec[] menu = [new("_View", [new MenuEntry("collapse") { Opposite = new("expand") }])];

        var sheet = KeySheet.For(_commands, _keys, Themes.Name, Themes, menu);

        Assert.Contains("View: Ctrl+K E Expand folders", Rows(sheet.Everywhere));
    }

    [Fact]
    public void A_region_binding_is_here_and_a_global_one_is_everywhere_under_its_menu_heading()
    {
        var sheet = Sheet(Themes);

        Assert.Equal("Here: Themes", sheet.Here!.Heading);
        Assert.Equal([": Ctrl+T U Use this theme", ": Ctrl+T D Delete this theme"], Rows(sheet.Here));
        Assert.Equal(
            [
                "File: Ctrl+Q Quit",
                "Go: Ctrl+G T Go to themes",
                "Go: Ctrl+G R Go to roles",
                "Help: F1 Show keys",
                "Other: F12 Show diagnostics",
            ],
            Rows(sheet.Everywhere));
    }

    [Fact]
    public void Disabled_and_unbound_commands_are_left_out()
    {
        _deleteEnabled = false;

        var sheet = Sheet(Themes);

        Assert.Equal([": Ctrl+T U Use this theme"], Rows(sheet.Here!));
        Assert.DoesNotContain(Rows(sheet.Everywhere), row => row.Contains("Never bound", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rebinding_shows_up_with_no_other_change()
    {
        _keys.Unbind(KeyChord.Parse("F12"), CommandScope.Global);
        _keys.Bind("Ctrl+D", "diagnostics");

        Assert.Contains("Other: Ctrl+D Show diagnostics", Rows(Sheet(Themes).Everywhere));
    }

    [Fact]
    public void A_region_with_no_bindings_of_its_own_has_no_here()
    {
        Assert.Null(Sheet(Roles).Here);
        Assert.Null(Sheet(CommandScope.Global).Here);
    }

    [Fact]
    public void The_columns_sit_side_by_side_when_they_fit_and_stack_when_they_do_not()
    {
        using var wide = new KeysDialog(Sheet(Themes), new Size(120, 30));
        using var narrow = new KeysDialog(Sheet(Themes), new Size(40, 30));

        Assert.False(wide.Stacked);
        Assert.True(narrow.Stacked);
        Assert.True(narrow.Body.GetContentSize().Width <= 40);
    }

    [Fact]
    public void A_sheet_taller_than_the_screen_scrolls()
    {
        using var dialog = new KeysDialog(Sheet(Themes), new Size(120, 12));

        Assert.True(dialog.Body.ViewportSettings.HasFlag(ViewportSettingsFlags.HasVerticalScrollBar));
        Assert.True(dialog.Body.Height is DimAbsolute { Size: var rows } && dialog.Body.GetContentSize().Height > rows);
    }
}
