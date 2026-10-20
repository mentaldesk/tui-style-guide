using System.Text.Json.Nodes;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Layout;
using MentalDesk.Tui.Palette;
using Swatch;
using Point = System.Drawing.Point;

namespace MentalDesk.Tui.Tests;

public class SidePanesTests : StaticConfigurationTest
{
    private const int Row = 5;

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private string FocusWord => _host.Shell.StatusBar.FocusWord;

    private JsonObject Section => new MentalDeskSettings(_host.SettingsPath).Section(SwatchSettings.Section);

    private JsonNode? Saved(string pane) => Section[SidePanes.Section]?[pane];

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Run(SwatchWindow window, int columns, params Delegate[] steps)
    {
        _host.App.Driver!.SetScreenSize(columns, 30);
        _host.Run(window, [() => FocusWord == "Themes", .. steps]);
    }

    private void Execute(string id) => _host.Shell.Commands.Execute(id);

    private void Mouse(int x, MouseFlags flags) =>
        _host.App.InjectMouse(new Mouse { ScreenPosition = new Point(x, Row), Flags = flags });

    private void Save(JsonObject section) =>
        new MentalDeskSettings(_host.SettingsPath).SaveSection(SwatchSettings.Section, section);

    [Fact]
    public void Widen_pane_twice_from_the_palette_takes_ten_columns_from_Preview_and_Narrow_pane_gives_five_back()
    {
        using var window = new SwatchWindow(_host.Shell);
        var preview = 0;

        Run(window, 140,
            () => Press(Key.Tab),
            () => FocusWord == "Roles",
            () => { preview = window.PreviewPane.Frame.Width; },
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press([.. "widen".Select(c => new Key(c)), Key.Enter]),
            () => _host.App.TopRunnableView is SwatchWindow && window.RolesPane.Frame.Width == 55,
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press([.. "widen".Select(c => new Key(c)), Key.Enter]),
            () => _host.App.TopRunnableView is SwatchWindow && window.RolesPane.Frame.Width == 60,
            () => Assert.Equal(preview - 10, window.PreviewPane.Frame.Width),
            () => Execute(SidePanes.NarrowId),
            () => window.RolesPane.Frame.Width == 55,
            () => Assert.Equal(preview - 5, window.PreviewPane.Frame.Width));

        Assert.Equal(55, (int)Saved("Roles")!);
    }

    [Fact]
    public void The_commands_are_offered_only_while_a_side_pane_has_focus()
    {
        using var window = new SwatchWindow(_host.Shell);
        string[] ids = [SidePanes.WidenId, SidePanes.NarrowId, SidePanes.ResetId];
        bool Offered() => ids.All(id => _host.Shell.Commands.IsAvailable(id, _host.Shell.Focus.Region!.Scope));

        Run(window, 120,
            () => Assert.True(Offered()),
            () => Press(Key.Tab),
            () => FocusWord == "Roles",
            () => Assert.True(Offered()),
            () => Press(Key.Tab),
            () => FocusWord == "Preview",
            () => Assert.DoesNotContain(ids, id => _host.Shell.Commands.IsEnabled(id)),
            () => Execute(SwatchCommands.ToggleAssistant),
            () => FocusWord == "Assistant",
            () => Assert.True(Offered()));

        Assert.DoesNotContain(_host.Shell.Keys.Bindings, binding => ids.Contains(binding.CommandId));
    }

    [Fact]
    public void A_key_given_on_the_Keys_page_widens_the_focused_pane()
    {
        Save(new JsonObject { [KeyOverrides.Section] = new JsonObject { [SidePanes.WidenId] = "Ctrl+W" } });
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 140,
            () => Press(Key.W.WithCtrl),
            () => window.ThemesPane.Frame.Width == 29);
    }

    [Fact]
    public void A_side_pane_stops_at_15_columns_and_Preview_at_40()
    {
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 120,
            () =>
            {
                for (var i = 0; i < 5; i++) Execute(SidePanes.NarrowId);
            },
            () => window.ThemesPane.Frame.Width == SidePanes.MinimumWidth,
            () => Assert.Equal("Themes is as narrow as it can be", _host.Shell.StatusBar.Says),
            () =>
            {
                for (var i = 0; i < 20; i++) Execute(SidePanes.WidenId);
            },
            () => window.PreviewPane.Frame.Width == SidePanes.MinimumMainWidth,
            () => Assert.Equal(120 - 50 - 40, window.ThemesPane.Frame.Width),
            () => Assert.Equal("Themes is as wide as it can be", _host.Shell.StatusBar.Says));
    }

    [Fact]
    public void Dragging_the_right_border_of_Themes_resizes_it_and_Preview_takes_what_is_left()
    {
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 140,
            () => Mouse(23, MouseFlags.LeftButtonPressed),
            () => Mouse(33, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport),
            () => window.ThemesPane.Frame.Width == 34 && window.RolesPane.Frame.X == 34,
            () => Assert.Equal(140 - 34 - 50, window.PreviewPane.Frame.Width),
            () => Mouse(33, MouseFlags.LeftButtonReleased));

        Assert.Equal(34, (int)Saved("Themes")!);
    }

    [Fact]
    public void A_drag_stops_at_the_limit_rather_than_jumping()
    {
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 120,
            () => Mouse(23, MouseFlags.LeftButtonPressed),
            () => Mouse(100, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport),
            () => window.PreviewPane.Frame.Width == SidePanes.MinimumMainWidth && window.ThemesPane.Frame.Width == 30,
            () => Mouse(3, MouseFlags.LeftButtonPressed | MouseFlags.PositionReport),
            () => window.ThemesPane.Frame.Width == SidePanes.MinimumWidth && window.RolesPane.Frame.X == SidePanes.MinimumWidth,
            () => Mouse(3, MouseFlags.LeftButtonReleased));

        Assert.Equal(SidePanes.MinimumWidth, (int)Saved("Themes")!);
    }

    [Fact]
    public void The_chosen_width_is_there_after_a_restart()
    {
        using (var first = new SwatchWindow(_host.Shell))
            Run(first, 140, () => Execute(SidePanes.WidenId), () => first.ThemesPane.Frame.Width == 29);

        using var restarted = new SwatchWindow(_host.Shell);
        Run(restarted, 140, () => restarted.ThemesPane.Frame.Width == 29);
    }

    [Fact]
    public void A_narrow_terminal_shrinks_the_side_panes_first_and_keeps_what_was_chosen()
    {
        Save(new JsonObject { [SidePanes.Section] = new JsonObject { ["Roles"] = 60 } });
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 90,
            () => window.PreviewPane.Frame.Width == SidePanes.MinimumMainWidth,
            () => Assert.Equal(50, window.ThemesPane.Frame.Width + window.RolesPane.Frame.Width),
            () => Assert.Equal(60, (int)Saved("Roles")!),
            () => _host.App.Driver!.SetScreenSize(160, 30),
            () => window.RolesPane.Frame.Width == 60,
            () => Assert.Equal(24, window.ThemesPane.Frame.Width),
            () => Assert.Equal(76, window.PreviewPane.Frame.Width));
    }

    [Fact]
    public void Reset_pane_width_puts_back_the_default_and_forgets_the_choice()
    {
        Save(new JsonObject { [SidePanes.Section] = new JsonObject { ["Themes"] = 40, ["Roles"] = 30 } });
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 140,
            () => window.ThemesPane.Frame.Width == 40,
            () => Execute(SidePanes.ResetId),
            () => window.ThemesPane.Frame.Width == 24);

        Assert.Null(Saved("Themes"));
        Assert.Equal(30, (int)Saved("Roles")!);
    }

    [Fact]
    public void The_Assistant_reopens_at_the_width_it_was_given()
    {
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 140,
            () => Execute(SwatchCommands.ToggleAssistant),
            () => FocusWord == "Assistant" && window.Assistant.Frame.Width == 48,
            () => Execute(SidePanes.NarrowId),
            () => window.Assistant.Frame.Width == 43,
            () => Assert.Equal(140 - 43, window.Assistant.Frame.X),
            () => Assert.Equal(140 - 24 - 43, window.RolesPane.Frame.Width),
            () => Execute(SwatchCommands.ToggleAssistant),
            () => FocusWord == "Preview" && window.RolesPane.Frame.Width == 50,
            () => Execute(SwatchCommands.ToggleAssistant),
            () => window.Assistant.Visible && window.Assistant.Frame.Width == 43);
    }

    [Fact]
    public void Roles_is_not_resizable_while_it_takes_what_the_Assistant_leaves()
    {
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 140,
            () => Execute(SwatchCommands.ToggleAssistant),
            () => FocusWord == "Assistant",
            () => _host.Shell.Focus.Focus(SwatchWindow.RolesRegion),
            () => FocusWord == "Roles",
            () => Assert.False(_host.Shell.Commands.IsEnabled(SidePanes.WidenId)),
            () => Assert.Equal(ViewArrangement.Fixed, window.RolesPane.Arrangement),
            () => Assert.Equal(ViewArrangement.LeftResizable, window.Assistant.Arrangement));
    }

    [Fact]
    public void A_saved_width_below_the_limit_or_not_a_number_or_for_a_pane_that_is_gone_is_ignored()
    {
        Save(new JsonObject { [SidePanes.Section] = new JsonObject { ["Themes"] = 5, ["Roles"] = "wide", ["Gone"] = 30 } });
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 140,
            () => window.ThemesPane.Frame.Width == 24 && window.RolesPane.Frame.Width == 50);
    }

    [Fact]
    public void Saving_a_width_keeps_the_app_s_other_settings_and_rebound_keys()
    {
        Save(new JsonObject
        {
            ["theme"] = "Daylight",
            [KeyOverrides.Section] = new JsonObject { [SwatchCommands.CopyColours] = "Ctrl+K" },
        });
        using var window = new SwatchWindow(_host.Shell);

        Run(window, 140, () => Execute(SidePanes.WidenId), () => window.ThemesPane.Frame.Width == 29);

        Assert.Equal("Daylight", (string)Section["theme"]!);
        Assert.Equal("Ctrl+K", (string)Section[KeyOverrides.Section]![SwatchCommands.CopyColours]!);
        Assert.Equal(29, (int)Saved("Themes")!);
    }
}
