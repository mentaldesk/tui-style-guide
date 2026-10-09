using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class SampleLoadingTests : StaticConfigurationTest
{
    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private void Run(Func<SampleWindow, Delegate[]> steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        using var shell = new AppShell(_host.App, _host.Shell.Cursor);
        using var sample = new SampleWindow(shell, Themes.Current, TimeSpan.FromMilliseconds(20));
        _host.Run(window, [() => _host.Shell.RunNested(shell, sample, Themes.Current), .. steps(sample)]);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    [Fact]
    public void The_files_pane_loads_then_shows_the_files()
    {
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () =>
            {
                Assert.Equal("notes.md", sample.Tree.SelectedObject?.Text);
                Press(Key.Esc);
            },
        ]);
    }

    [Fact]
    public void The_view_menu_offers_collapse_or_expand_whichever_the_folders_need()
    {
        var offered = new List<string>();

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => offered.Add(Folders(sample)),
            () => Press(Key.F10, Key.CursorRight, Key.CursorRight),
            () => sample.Shell.Menu!.Menus.Single(menu => menu.Title == "_View").PopoverMenuOpen,
            () => Press(Key.C),
            () => sample.Tree.Objects!.All(node => !sample.Tree.IsExpanded(node)),
            () => offered.Add(Folders(sample)),
            () => sample.Shell.Commands.Execute(SampleWindow.LoadNoFiles),
            () => sample.Files.State == LoadState.Empty,
            () =>
            {
                offered.Add(Folders(sample));
                Press(Key.Esc);
            },
        ]);

        Assert.Equal(["_Collapse folders", "_Expand folders", "_Collapse folders, dimmed"], offered);
    }

    private static string Folders(SampleWindow sample)
    {
        var menu = sample.Shell.Menu!;
        menu.Refresh();
        var item = menu.Menus.Single(view => view.Title == "_View").PopoverMenu!.Root!.SubViews.OfType<MenuItem>().Last();
        var id = menu.Items.Single(entry => entry.Item == item).Id;
        return menu.IsDimmed(id) ? $"{item.Title}, dimmed" : item.Title;
    }

    [Fact]
    public void The_view_menu_shows_empty_and_failed_in_the_pane_and_Ctrl_R_retries()
    {
        var says = new List<string>();

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.LoadNoFiles),
            () => sample.Files.State == LoadState.Empty,
            () => says.Add(sample.Files.Says),
            () => sample.Shell.Commands.Execute(SampleWindow.LoadFailing),
            () => sample.Files.State == LoadState.Failed,
            () => says.Add(sample.Files.Says),
            () => Press(Key.R.WithCtrl),
            () => sample.Files.State == LoadState.Loaded,
            () => Press(Key.Esc),
        ]);

        Assert.Equal(["No files", "Couldn't list files"], says);
    }

    [Fact]
    public void A_reload_that_fails_keeps_the_files_and_says_so_on_the_status_bar()
    {
        var said = "";

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.LoadFailing),
            () => sample.Shell.StatusBar.Message is not null,
            () =>
            {
                said = sample.Shell.StatusBar.Message!;
                Assert.Equal(LoadState.Loaded, sample.Files.State);
                Press(Key.Esc);
            },
        ]);

        Assert.Equal("Couldn't list files, so these are the files from the last load", said);
    }
}
