using System.Text.Json.Nodes;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Shell;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class KeysPageTests : StaticConfigurationTest
{
    private readonly Host _host = new(isWindows: false);

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private SettingsDialog? Dialog => _host.App.TopRunnableView as SettingsDialog;

    private KeysPage Page => Dialog!.Pages.OfType<KeysPage>().Single();

    private IEnumerable<string> Names => Dialog!.PageList.Source!.ToList().Cast<string>();

    private ConfirmDialog? Confirm => _host.App.TopRunnableView as ConfirmDialog;

    private JsonObject? Written =>
        new MentalDeskSettings(_host.SettingsPath).Section(SwatchSettings.Section)[KeyOverrides.Section] as JsonObject;

    private List<string> KeysOf(string id) => [.. _host.Shell.Keys.For(id).Select(binding => binding.Display)];

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Type(string text) => Press([.. text.Select(c => new Key(c))]);

    private string Row(int i) => Page.Rows[i];

    private static string Squeeze(string row) => string.Join(' ', row.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private void Save(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_host.SettingsPath)!);
        File.WriteAllText(_host.SettingsPath, json);
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        Run(window, steps);
    }

    private void Run(SwatchWindow window, params Delegate[] steps) =>
        _host.Run(window, [
            () => { _host.Shell.Commands.Execute(SwatchCommands.ShowSettings); },
            () => Dialog?.IsInitialized == true,
            () => Dialog!.Open(2),
            () => Page.Filter.HasFocus,
            .. steps,
        ]);

    private Delegate[] RecordFor(string filter) =>
    [
        () => Type(filter),
        () => Page.Filter.Text == filter,
        () => Press(Key.Enter),
        () => Page.IsRecording,
    ];

    [Fact]
    public void It_lists_every_command_one_row_per_key_under_a_header_with_where_each_works()
    {
        Run(() =>
        {
            Assert.Equal(["Appearance", "Swatches", "Keys", "Mac keys"], Names);
            Assert.Equal("Command Keys Where", Squeeze(Page.Header));
            var rows = Page.Rows.Select(Squeeze).ToList();
            Assert.Contains("Try this theme Ctrl+T T Everywhere", rows);
            Assert.Contains("Try this theme Enter Preview", rows);
            Assert.Contains("Copy colours Ctrl+C Roles", rows);
            Assert.Contains("Write a comment Everywhere", rows);
            Assert.Equal(_host.Shell.Commands.All.Count() + 1, rows.Count);
            Assert.Equal(KeysPage.BrowsingHint, Page.Hint);
            Assert.Equal(" Ctrl+R Reset to defaults… ", Page.ResetButton.Text);
            Assert.DoesNotContain(Page.Rows, row => row.Contains("default", StringComparison.Ordinal));
            Press(Key.Esc);
        });
    }

    [Theory]
    [InlineData("comment", "Write a comment")]
    [InlineData("ctrl+t d", "Delete this theme")]
    [InlineData("roles", "Copy colours")]
    public void The_filter_matches_the_command_its_keys_and_where(string filter, string first)
    {
        Run(
            () => Type(filter),
            () => Page.Filter.Text == filter,
            () =>
            {
                Assert.StartsWith(first, Row(0), StringComparison.Ordinal);
                Press(Key.Esc);
            });
    }

    [Fact]
    public void Recording_a_chord_adds_it_and_Keep_makes_it_work_at_once_and_writes_only_that()
    {
        Run([
            .. RecordFor("comment"),
            () =>
            {
                Assert.Equal(KeysPage.RecordingHint, Page.Hint);
                Assert.Contains(KeysPage.Recording, Row(0), StringComparison.Ordinal);
                Press(Key.T.WithCtrl);
            },
            () => Row(0).Contains($"{KeysPage.Recording} Ctrl+T", StringComparison.Ordinal),
            () => Press(Key.X),
            () => Row(0).Contains($"{KeysPage.Recording} Ctrl+T X", StringComparison.Ordinal),
            () => Press(Key.Enter),
            () => !Page.IsRecording,
            () =>
            {
                Assert.Equal("Write a comment Ctrl+T X Everywhere (default: none)", Squeeze(Row(0)));
                Assert.Equal(["Appearance", "Swatches", "Keys •", "Mac keys"], Names);
                Assert.Empty(KeysOf(SwatchCommands.WriteComment));
                Press(Key.Enter.WithCtrl);
            },
            () => _host.App.TopRunnableView is SwatchWindow,
            () => Press(Key.T.WithCtrl, Key.X),
            () => _host.App.TopRunnableView is CommentDialog,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow,
        ]);

        Assert.Equal(["Ctrl+T X"], KeysOf(SwatchCommands.WriteComment));
        Assert.Equal("""{"swatch.writeComment":"Ctrl+T x"}""", Written!.ToJsonString(Json.Readable));
    }

    [Fact]
    public void A_kept_key_shows_in_the_menu_the_palette_and_Help_and_is_back_after_a_restart()
    {
        KeySheet? sheet = null;
        Run([
            .. RecordFor("loading"),
            () => Press(Key.F5),
            () => Press(Key.Enter),
            () => !Page.IsRecording,
            () => Press(Key.Enter.WithCtrl),
            () => _host.App.TopRunnableView is SwatchWindow,
            () =>
            {
                _host.Shell.Menu!.Refresh();
                var item = _host.Shell.Menu.Items.Single(entry => entry.Id == SwatchCommands.ShowLoadingStates).Item;
                Assert.Equal("F5", item.KeyView.Text);
                Press(Key.F1);
            },
            () => _host.App.TopRunnableView is KeysDialog keys && (sheet = keys.Sheet) is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow,
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () =>
            {
                var palette = (CommandPalette)_host.App.TopRunnableView!;
                Assert.Contains(palette.Shown, row => row.Label == "Show loading states" && row.Keys == "F5");
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow,
        ]);

        Assert.Contains(sheet!.Everywhere!.Groups.SelectMany(group => group.Rows), row => row is { Keys: "F5", Label: "Show loading states" });

        using var restarted = new Host(isWindows: false);
        Directory.CreateDirectory(Path.GetDirectoryName(restarted.SettingsPath)!);
        File.Copy(_host.SettingsPath, restarted.SettingsPath);
        using var window = new SwatchWindow(restarted.Shell);
        Assert.Equal(["F5"], restarted.Shell.Keys.For(SwatchCommands.ShowLoadingStates).Select(binding => binding.Display));
    }

    [Fact]
    public void A_second_key_joins_the_commands_others_and_Delete_removes_the_selected_one()
    {
        Run([
            .. RecordFor("try this"),
            () => Press(Key.F5, Key.Enter),
            () => !Page.IsRecording,
            () =>
            {
                Assert.Equal(["Ctrl+T T", "F5"], Page.Staged.Where(binding => binding.CommandId == SwatchCommands.TryTheme && binding.Scope == Commands.CommandScope.Global).Select(binding => binding.Display).Order());
                Assert.Contains(Page.Rows, row => Squeeze(row) == "Try this theme Enter Preview");
                Assert.Contains(Page.Rows, row => Squeeze(row) == "Try this theme F5 Everywhere (default: Ctrl+T T, Enter)");
            },
            () =>
            {
                if (Squeeze(Page.Rows[Page.List.SelectedItem ?? 0]).StartsWith("Try this theme Ctrl+T T", StringComparison.Ordinal)) return true;
                Press(Key.CursorDown);
                return false;
            },
            () => Press(Key.Delete),
            () => Page.Rows.Count(row => row.StartsWith("Try this theme", StringComparison.Ordinal)) == 2,
            () =>
            {
                Assert.Contains("(default: Ctrl+T T, Enter)", Page.Rows.First(row => row.StartsWith("Try this theme", StringComparison.Ordinal)), StringComparison.Ordinal);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow,
        ]);

        Assert.Equal(["Ctrl+T T", "Enter"], KeysOf(SwatchCommands.TryTheme));
    }

    [Fact]
    public void Esc_while_recording_stops_and_changes_nothing()
    {
        Run([
            .. RecordFor("comment"),
            () => Press(Key.T.WithCtrl, Key.X, Key.Esc),
            () => !Page.IsRecording,
            () =>
            {
                Assert.Equal("Write a comment Everywhere", Squeeze(Row(0)));
                Assert.False(Page.IsChanged);
                Assert.Same(Dialog, _host.App.TopRunnableView);
                Assert.Equal(KeysPage.BrowsingHint, Page.Hint);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow,
        ]);
    }

    public static TheoryData<string, string> FixedKeys() => new()
    {
        { "Enter", "Enter is kept: it takes the keys" },
        { "Ctrl+Enter", "Ctrl+Enter is kept: it confirms" },
        { "Tab", "Tab is kept: it moves focus" },
        { "Shift+Tab", "Shift+Tab is kept: it moves focus" },
        { "F1", "F1 is kept: it opens Help" },
        { "F6", "F6 is kept: it moves between panes" },
        { "Shift+F6", "Shift+F6 is kept: it moves between panes" },
    };

    [Theory]
    [MemberData(nameof(FixedKeys))]
    public void A_key_the_guide_fixes_is_refused_on_the_row_and_recording_carries_on(string name, string reason)
    {
        Assert.True(Key.TryParse(name, out var key));
        Run([
            .. RecordFor("comment"),
            () => Press(key),
            () => Row(0).Contains(reason, StringComparison.Ordinal),
            () =>
            {
                Assert.True(Page.IsRecording);
                Assert.Same(Dialog, _host.App.TopRunnableView);
                Press(Key.F5);
            },
            () => Row(0).Contains($"{KeysPage.Recording} F5", StringComparison.Ordinal) && !Row(0).Contains(reason, StringComparison.Ordinal),
            () => Press(Key.Enter),
            () => !Page.IsRecording,
            () =>
            {
                Assert.Equal(["F5"], Page.Staged.Where(binding => binding.CommandId == SwatchCommands.WriteComment).Select(binding => binding.Display));
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow,
        ]);
    }

    [Fact]
    public void Esc_can_never_be_recorded_because_it_stops_recording()
    {
        Run([
            .. RecordFor("comment"),
            () => Press(Key.Esc),
            () => !Page.IsRecording,
            () =>
            {
                Assert.Same(Dialog, _host.App.TopRunnableView);
                Assert.DoesNotContain(Page.Staged, binding => binding.CommandId == SwatchCommands.WriteComment);
                Press(Key.Esc);
            },
        ]);
    }

    [Fact]
    public void A_fixed_key_cant_be_deleted()
    {
        Run(
            () => Type("show keys"),
            () => Squeeze(Row(0)) == "Show keys F1 Everywhere",
            () => Press(Key.Delete),
            () => Row(0).Contains("F1 is kept: it opens Help", StringComparison.Ordinal),
            () =>
            {
                Assert.False(Page.IsChanged);
                Press(Key.Esc);
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_chord_another_command_has_in_the_same_place_asks_to_replace_it(bool replace)
    {
        ConfirmDialog? confirm = null;
        Run([
            .. RecordFor("close this"),
            () => Press(Key.T.WithCtrl, Key.D, Key.Enter),
            () => (confirm = Confirm) is not null,
            () => Press(replace ? Key.Enter.WithCtrl : Key.Esc),
            () => Dialog is not null && !Page.IsRecording,
            () =>
            {
                var delete = Page.Staged.Where(binding => binding.CommandId == SwatchCommands.DeleteTheme).Select(binding => binding.Display);
                var close = Page.Staged.Where(binding => binding.CommandId == SwatchCommands.CloseTheme).Select(binding => binding.Display).Order();
                Assert.Equal(replace ? [] : ["Ctrl+T D"], delete);
                Assert.Equal(replace ? ["Ctrl+T C", "Ctrl+T D"] : ["Ctrl+T C"], close);
                Assert.Equal(replace, Page.IsChanged);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow,
        ]);

        Assert.Equal("Replace key?", confirm!.Title);
        Assert.Equal(["Ctrl+T D already runs Delete this theme in Themes.", "Give it to Close this theme instead?"], confirm.Lines);
        Assert.Equal(" Ctrl+Enter Replace ", confirm.ButtonFor(KeysPage.ReplaceAction).Text);
    }

    [Fact]
    public void A_chord_that_starts_another_is_refused_and_recording_carries_on_from_it()
    {
        Run([
            .. RecordFor("comment"),
            () => Press(Key.T.WithCtrl, Key.Enter),
            () => Row(0).Contains("Ctrl+T starts Ctrl+T T, which runs Try this theme", StringComparison.Ordinal),
            () =>
            {
                Assert.True(Page.IsRecording);
                Press(Key.X, Key.Enter);
            },
            () => !Page.IsRecording,
            () =>
            {
                Assert.Equal(["Ctrl+T X"], Page.Staged.Where(binding => binding.CommandId == SwatchCommands.WriteComment).Select(binding => binding.Display));
                Press(Key.Esc);
            },
        ]);
    }

    [Fact]
    public void A_chord_that_goes_on_from_another_is_refused_naming_it()
    {
        Run([
            .. RecordFor("comment"),
            () => Press(Key.Q.WithCtrl, Key.X, Key.Enter),
            () => Row(0).Contains("Ctrl+Q X goes on from Ctrl+Q, which runs Quit", StringComparison.Ordinal),
            () =>
            {
                Assert.True(Page.IsRecording);
                Assert.False(Page.IsChanged);
                Press(Key.Esc);
            },
            () => !Page.IsRecording,
            () => Press(Key.Esc),
        ]);
    }

    [Theory]
    [InlineData("close this", "Ctrl+Q", new[] { "Ctrl+Q runs Quit everywhere.", "In Themes it would run Close this theme instead." })]
    [InlineData("comment", "Ctrl+C", new[] { "Ctrl+C runs Copy colours in Roles.", "Everywhere else it would run Write a comment." })]
    public void A_region_key_hiding_one_everywhere_asks_first_saying_which_wins_where(string filter, string chord, string[] lines)
    {
        ConfirmDialog? confirm = null;
        Run([
            .. RecordFor(filter),
            () => Press([.. KeyChord.Parse(chord), Key.Enter]),
            () => (confirm = Confirm) is not null,
            () => Press(Key.Enter.WithCtrl),
            () => Dialog is not null && !Page.IsRecording,
            () =>
            {
                Assert.True(Page.IsChanged);
                Assert.Contains(Page.Rows, row => row.Contains(chord, StringComparison.Ordinal) && row.Contains("(default:", StringComparison.Ordinal));
                Press(Key.Esc);
            },
        ]);

        Assert.Equal("Hide a key?", confirm!.Title);
        Assert.Equal(lines, confirm.Lines);
    }

    [Fact]
    public void Reset_asks_then_stages_the_defaults_and_Esc_on_Settings_puts_the_kept_keys_back()
    {
        Save("""{ "swatch": { "keys": { "swatch.writeComment": "F5", "app.quit": "" } } }""");
        Run(
            () =>
            {
                Assert.Equal(["F5"], KeysOf(SwatchCommands.WriteComment));
                Assert.Contains(Page.Rows, row => Squeeze(row) == "Quit Everywhere (default: Ctrl+Q)");
                Press(Key.R.WithCtrl);
            },
            () => Confirm is not null,
            () => Press(Key.Enter.WithCtrl),
            () => Dialog is not null && Page.IsChanged,
            () =>
            {
                Assert.Contains(Page.Rows, row => Squeeze(row) == "Quit Ctrl+Q Everywhere");
                Assert.DoesNotContain(Page.Rows, row => row.Contains("default", StringComparison.Ordinal));
                Assert.Equal(["Appearance", "Swatches", "Keys •", "Mac keys"], Names);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(["F5"], KeysOf(SwatchCommands.WriteComment));
        Assert.Empty(KeysOf(ShellCommands.Quit));
    }

    [Fact]
    public void A_kept_reset_leaves_nothing_in_the_file()
    {
        Save("""{ "swatch": { "keys": { "swatch.writeComment": "F5" } } }""");
        Run(
            () => Press(Key.R.WithCtrl),
            () => Confirm is not null,
            () => Press(Key.Enter.WithCtrl),
            () => Dialog is not null && Page.IsChanged,
            () => Press(Key.Enter.WithCtrl),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("{}", Written!.ToJsonString(Json.Readable));
        Assert.Empty(KeysOf(SwatchCommands.WriteComment));
    }
}
