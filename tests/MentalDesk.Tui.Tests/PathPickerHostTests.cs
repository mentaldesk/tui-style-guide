using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Paths;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class PathPickerHostTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mentaldesk-paths-{Guid.NewGuid():N}");

    public PathPickerHostTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "deep", "deeper", "deepest"));
        File.WriteAllText(Path.Combine(_root, "deep", "deeper", "deepest", "notes.md"), string.Empty);
    }

    public override void Dispose()
    {
        _host.Dispose();
        Directory.Delete(_root, recursive: true);
        base.Dispose();
    }

    private PathPicker? Picker => _host.App.TopRunnableView as PathPicker;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Type(string text) => Press([.. text.Select(c => new Key(c))]);

    [Fact]
    public void The_search_runs_in_the_background_saying_so_until_it_is_done()
    {
        using var window = new SwatchWindow(_host.Shell);
        using var picker = new PathPicker("Open", PathKind.File, _root, _root);
        var state = "";

        _host.Run(window,
            () => picker.Run(_host.Shell),
            () => Picker is not null,
            () => Type("note"),
            () => picker.FilterField.Text == "note",
            () => !picker.IsSearching && picker.Labels.Count == 1,
            () =>
            {
                state = picker.State;
                Press(Key.Enter);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("", state);
        Assert.Equal("~/deep/deeper/deepest/notes.md", picker.PickedPath!.Replace('\\', '/'));
    }

    [Fact]
    public void Replacing_a_file_asks_through_a_confirm()
    {
        using var window = new SwatchWindow(_host.Shell);
        using var picker = new PathPicker("Save copy as", PathKind.NewFile, Path.Combine(_root, "deep", "deeper", "deepest"), _root);
        string[] lines = [];

        _host.Run(window,
            () => picker.Run(_host.Shell),
            () => Picker is not null,
            () => Type("notes.md"),
            () => picker.FilterField.Text == "notes.md",
            () => Press(Key.Enter.WithCtrl),
            () => _host.App.TopRunnableView is ConfirmDialog,
            () =>
            {
                lines = [.. ((ConfirmDialog)_host.App.TopRunnableView!).Lines];
                Press(Key.Enter.WithCtrl);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("~/deep/deeper/deepest/notes.md is already there.", lines[0].Replace('\\', '/'));
        Assert.True(picker.Confirmed);
    }

    [Theory]
    [InlineData(SampleWindow.Open, "Open", "opened")]
    [InlineData(SampleWindow.OpenFolder, "Open folder", "opened")]
    [InlineData(SampleWindow.SaveCopy, "Save copy as", "saved a copy as")]
    public void The_sample_File_menu_picks_a_path_and_says_what_it_picked(string command, string title, string verb)
    {
        using var window = new SwatchWindow(_host.Shell);
        using var shell = new AppShell(_host.App, _host.Shell.Cursor);
        using var sample = new SampleWindow(shell, Themes.Current, TimeSpan.FromMilliseconds(20));
        var target = command == SampleWindow.OpenFolder
            ? Path.Combine(_root, "deep")
            : Path.Combine(_root, command == SampleWindow.Open ? "deep/deeper/deepest/notes.md" : "new.md");
        var shownTitle = "";
        var said = "";

        _host.Run(window,
            () => _host.Shell.RunNested(shell, sample, Themes.Current),
            () => _host.App.TopRunnableView is SampleWindow,
            () => { shell.Commands.Execute(command); },
            () => Picker is not null,
            () =>
            {
                shownTitle = Picker!.Title;
                Type(target);
            },
            () => Picker!.FilterField.Text == target,
            () => Press(command == SampleWindow.SaveCopy ? Key.Enter.WithCtrl : Key.Enter),
            () => command != SampleWindow.OpenFolder || Picker!.Folder == target,
            () => Press(command == SampleWindow.OpenFolder ? [Key.Enter.WithCtrl] : []),
            () => shell.StatusBar.Message is not null,
            () =>
            {
                said = shell.StatusBar.Message!;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(title, shownTitle);
        Assert.Equal($"A demo: this would have {verb} {HomePath.Show(Path.GetFullPath(target), HomePath.Default)}", said);
    }

    [Fact]
    public void Esc_closes_it_with_nothing_said()
    {
        using var window = new SwatchWindow(_host.Shell);
        using var shell = new AppShell(_host.App, _host.Shell.Cursor);
        using var sample = new SampleWindow(shell, Themes.Current, TimeSpan.FromMilliseconds(20));
        string? said = "";

        _host.Run(window,
            () => _host.Shell.RunNested(shell, sample, Themes.Current),
            () => _host.App.TopRunnableView is SampleWindow,
            () => { shell.Commands.Execute(SampleWindow.Open); },
            () => Picker is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SampleWindow,
            () =>
            {
                said = shell.StatusBar.Message;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Null(said);
    }
}
