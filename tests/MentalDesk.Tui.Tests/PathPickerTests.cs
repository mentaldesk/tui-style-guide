using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Paths;

namespace MentalDesk.Tui.Tests;

public class PathPickerTests : StaticConfigurationTest
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), $"mentaldesk-paths-{Guid.NewGuid():N}");

    public PathPickerTests()
    {
        Make(
            "code/tui-style-guide/src/Swatch/Program.cs",
            "code/tui-style-guide/README.md",
            "code/swatch/themes/old/solar.json",
            "code/swatch/notes.md",
            "code/sentry-swift/",
            "code/.github/workflows/ci.yml",
            "code/.hidden.md",
            "code/app/node_modules/swatch-lib/",
            "code/app/bin/Swatch.dll",
            "code/app/obj/swatch.json",
            "code/app/.git/swatch",
            "code/app/.hg/swatch",
            "code/app/.svn/swatch",
            "code/b.md",
            "code/a.md");
    }

    public override void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        base.Dispose();
    }

    private string Code => Path.Combine(_home, "code");

    private string At(string relative) => Path.Combine(_home, relative.Replace('/', Path.DirectorySeparatorChar));

    private void Make(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (path.EndsWith('/'))
            {
                Directory.CreateDirectory(At(path));
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(At(path))!);
            File.WriteAllText(At(path), string.Empty);
        }
    }

    private PathPicker Picker(PathKind kind = PathKind.File, string start = "code", int cap = PathSearch.Cap) =>
        new("Open", kind, At(start), _home, cap);

    private static void Press(PathPicker picker, params Key[] keys)
    {
        foreach (var key in keys)
            Assert.Equal(KeyResult.Consumed, picker.Scope.Handle(key));
    }

    private static void Type(PathPicker picker, string text) => picker.FilterField.Text = text;

    [Fact]
    public void It_starts_in_the_folder_with_home_as_a_tilde_and_lists_up_folders_then_files_without_hidden_ones()
    {
        using var picker = Picker();

        Assert.Equal("~/code", picker.Where.Replace('\\', '/'));
        Assert.Equal(["../", "app/", "sentry-swift/", "swatch/", "tui-style-guide/", "a.md", "b.md"], picker.Labels);
        Assert.Equal(0, picker.Shown.ToList().IndexOf(picker.Selected!));
    }

    [Fact]
    public void Enter_on_a_folder_goes_in_and_on_up_goes_back_selecting_where_it_came_from()
    {
        using var picker = Picker();
        Type(picker, "swa");
        Assert.Equal("swatch/", picker.Selected!.Label);

        Press(picker, Key.Enter);
        Assert.Equal(At("code/swatch"), picker.Folder);
        Assert.Equal("", picker.FilterField.Text);
        Assert.Equal(["../", "themes/", "notes.md"], picker.Labels);
        Assert.Equal("../", picker.Selected!.Label);

        Press(picker, Key.Enter);
        Assert.Equal(Code, picker.Folder);
        Assert.Equal("swatch/", picker.Selected!.Label);
    }

    [Fact]
    public void Backspace_in_an_empty_filter_goes_up_and_otherwise_edits_the_filter()
    {
        using var picker = Picker(start: "code/swatch/themes");
        Type(picker, "o");
        Assert.Equal(KeyResult.Pass, picker.Scope.Handle(Key.Backspace));

        Type(picker, "");
        Press(picker, Key.Backspace);

        Assert.Equal(At("code/swatch"), picker.Folder);
        Assert.Equal("themes/", picker.Selected!.Label);
    }

    [Fact]
    public void The_root_has_no_way_up()
    {
        using var picker = new PathPicker("Open", PathKind.Folder, Path.GetPathRoot(_home)!, _home);

        Assert.DoesNotContain("../", picker.Labels);
        Assert.Equal(KeyResult.Pass, picker.Scope.Handle(Key.Backspace));
    }

    [Fact]
    public void Typing_finds_files_and_folders_below_with_their_path_from_here_shallower_first()
    {
        using var picker = Picker();
        Type(picker, "sw");

        Assert.Equal(["swatch/", "tui-style-guide/src/Swatch/", "sentry-swift/"], picker.Labels.Take(3));

        Type(picker, "prog");
        Assert.Equal(["tui-style-guide/src/Swatch/Program.cs"], picker.Labels);
    }

    [Fact]
    public void A_slash_matches_folder_by_folder()
    {
        using var picker = Picker();
        Type(picker, "src/sw");

        Assert.Equal(["tui-style-guide/src/Swatch/"], picker.Labels);
    }

    [Fact]
    public void Version_control_dependency_and_build_folders_are_skipped()
    {
        using var picker = Picker(start: "code/app");
        Type(picker, "swatch");

        Assert.True(picker.NoMatches);
        Type(picker, ".swatch");
        Assert.True(picker.NoMatches);
    }

    [Fact]
    public void Hidden_entries_show_only_for_a_filter_starting_with_a_dot()
    {
        using var picker = Picker();
        Type(picker, "gith");
        Assert.True(picker.NoMatches);

        Type(picker, ".gith");
        Assert.Equal([".github/"], picker.Labels);
        Type(picker, ".hid");
        Assert.Equal([".hidden.md"], picker.Labels);
    }

    [Fact]
    public void A_search_stops_at_the_cap_keeping_the_shallowest_and_says_so()
    {
        using var picker = Picker(cap: 6);
        Type(picker, "s");

        Assert.Equal("First 6 entries", picker.SearchStatus);
        Assert.DoesNotContain(picker.Labels, label => label.Count(c => c == '/') > 1);
    }

    [Fact]
    public void A_search_that_finishes_says_nothing()
    {
        using var picker = Picker();
        Type(picker, "s");

        Assert.Equal("", picker.SearchStatus);
        Assert.False(picker.IsSearching);
    }

    [Fact]
    public void Enter_on_a_folder_found_below_goes_into_it()
    {
        using var picker = Picker();
        Type(picker, "src/sw");
        Press(picker, Key.Enter);

        Assert.Equal(At("code/tui-style-guide/src/Swatch"), picker.Folder);
    }

    [Fact]
    public void Enter_on_a_file_anywhere_picks_it_with_home_as_a_tilde()
    {
        using var picker = Picker();
        Type(picker, "prog");
        Press(picker, Key.Enter);

        Assert.True(picker.Confirmed);
        Assert.Equal("~/code/tui-style-guide/src/Swatch/Program.cs", picker.PickedPath!.Replace('\\', '/'));
    }

    [Fact]
    public void A_typed_home_path_to_a_folder_goes_there()
    {
        using var picker = Picker();
        Type(picker, "~/code/swatch/themes");
        Press(picker, Key.Enter);

        Assert.Equal(At("code/swatch/themes"), picker.Folder);
        Assert.False(picker.Confirmed);
    }

    [Fact]
    public void A_typed_absolute_path_to_a_file_picks_it()
    {
        using var picker = Picker();
        Type(picker, At("code/swatch/notes.md"));
        Press(picker, Key.Enter);

        Assert.Equal("~/code/swatch/notes.md", picker.PickedPath!.Replace('\\', '/'));
    }

    [Fact]
    public void A_typed_absolute_path_to_a_folder_goes_there()
    {
        using var picker = Picker(start: "code/swatch");
        Type(picker, At("code/app"));
        Press(picker, Key.Enter);

        Assert.Equal(At("code/app"), picker.Folder);
    }

    [Fact]
    public void Open_folder_lists_only_folders_and_Ctrl_Enter_uses_the_one_you_are_in()
    {
        using var picker = Picker(PathKind.Folder);
        Assert.Equal(["../", "app/", "sentry-swift/", "swatch/", "tui-style-guide/"], picker.Labels);
        Assert.Equal([" Enter Open ", " Ctrl+Enter Use this folder ", " Esc Cancel "],
            picker.SubViews.OfType<Button>().Where(button => button.SuperView == picker).Select(button => button.Text));

        Type(picker, "swa");
        Press(picker, Key.Enter);
        Assert.False(picker.Confirmed);
        Press(picker, CtrlEnter.Key);

        Assert.True(picker.Confirmed);
        Assert.Equal("~/code/swatch", picker.PickedPath!.Replace('\\', '/'));
    }

    [Fact]
    public void A_new_name_is_offered_first_and_dim_until_a_file_of_that_name_is_here()
    {
        using var picker = Picker(PathKind.NewFile, start: "code/swatch");
        Assert.False(picker.SaveHereButton!.Enabled);
        Type(picker, "solar.json");

        Assert.Equal(["New file: solar.json", "themes/old/solar.json"], picker.Labels);
        Assert.True(picker.SaveHereButton.Enabled);

        Type(picker, "notes.md");
        Assert.Equal(["notes.md"], picker.Labels);
    }

    [Fact]
    public void Ctrl_Enter_saves_the_new_name_here()
    {
        using var picker = Picker(PathKind.NewFile, start: "code/swatch");
        Type(picker, "solar.json");
        Press(picker, CtrlEnter.Key);

        Assert.Equal("~/code/swatch/solar.json", picker.PickedPath!.Replace('\\', '/'));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Naming_a_file_that_is_there_asks_to_replace_it(bool replace)
    {
        using var picker = Picker(PathKind.NewFile, start: "code/swatch");
        var asked = new List<string>();
        picker.ConfirmReplace = path =>
        {
            asked.Add(path.Replace('\\', '/'));
            return replace;
        };
        Type(picker, "notes.md");
        Press(picker, CtrlEnter.Key);

        Assert.Equal(["~/code/swatch/notes.md"], asked);
        Assert.Equal(replace, picker.Confirmed);
    }

    [Fact]
    public void Enter_on_a_file_that_is_there_asks_to_replace_it()
    {
        using var picker = Picker(PathKind.NewFile, start: "code/swatch");
        var asked = 0;
        picker.ConfirmReplace = _ => ++asked > 0;
        Type(picker, "old/sol");
        Press(picker, Key.Enter);

        Assert.Equal(1, asked);
        Assert.Equal("~/code/swatch/themes/old/solar.json", picker.PickedPath!.Replace('\\', '/'));
    }

    [Fact]
    public void A_folder_that_cannot_be_read_says_why_and_up_still_works()
    {
        if (OperatingSystem.IsWindows()) return;
        Make("code/locked/");
        File.SetUnixFileMode(At("code/locked"), UnixFileMode.None);
        try
        {
            using var picker = Picker(start: "code/locked");

            Assert.Equal(["../"], picker.Labels);
            Assert.Equal("You don't have permission to read this folder", picker.Note);
            Press(picker, Key.Enter);
            Assert.Equal(Code, picker.Folder);
            Assert.Null(picker.Note);
        }
        finally
        {
            File.SetUnixFileMode(At("code/locked"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void Esc_picks_nothing()
    {
        using var picker = Picker();
        Press(picker, Key.Esc);

        Assert.False(picker.Confirmed);
        Assert.Null(picker.PickedPath);
    }

    [Fact]
    public void A_picker_replaces_its_rows_keeping_the_filter_and_selecting_what_it_is_asked_to()
    {
        using var picker = new PickerDialog<string>("Pick", 40, 10, ["alpha", "beta"], row => row, "Pick");
        picker.FilterField.Text = "a";

        picker.Replace(["gamma", "delta", "alpaca"], select: "delta");

        Assert.Equal("a", picker.FilterField.Text);
        Assert.Equal(["alpaca", "gamma", "delta"], picker.Labels);
        Assert.Equal("delta", picker.Selected);

        picker.Replace(["omicron"], select: "delta");
        Assert.True(picker.NoMatches);
        Assert.Null(picker.Selected);
    }
}
