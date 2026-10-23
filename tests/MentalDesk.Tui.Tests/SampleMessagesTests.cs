using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Palette;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class SampleMessagesTests : StaticConfigurationTest
{
    private const string Failure = "Couldn't list files, so these are the files from the last load";

    private readonly TestClipboard _platform = new();
    private readonly Host _host;

    public SampleMessagesTests() => _host = new Host(_platform);

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

    private MessagesDialog? Messages => _host.App.TopRunnableView as MessagesDialog;

    [Fact]
    public void A_failed_reload_survives_typing_and_counts_as_unread()
    {
        string? said = null;
        var unread = "";

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.LoadFailing),
            () => sample.Shell.StatusBar.Message is not null,
            () => Press(Key.A, Key.B, Key.C),
            () =>
            {
                said = sample.Shell.StatusBar.Message;
                unread = sample.Shell.StatusBar.UnreadButton.Text;
                Press(Key.Esc);
            },
        ]);

        Assert.Equal(Failure, said);
        Assert.Equal("⚠ 1", unread);
    }

    [Fact]
    public void A_reload_that_works_takes_the_failure_off_the_bar()
    {
        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.LoadFailing),
            () => sample.Shell.StatusBar.Message == Failure,
            () => sample.Shell.Commands.Execute(SampleWindow.ReloadFiles),
            () => sample.Shell.StatusBar.Message is null,
            () => Press(Key.Esc),
        ]);
    }

    [Fact]
    public void A_key_whose_command_says_something_keeps_its_message()
    {
        string? said = null;

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => Press(Key.S.WithCtrl),
            () => sample.Shell.StatusBar.Message is not null,
            () =>
            {
                said = sample.Shell.StatusBar.Message;
                Press(Key.Esc);
            },
        ]);

        Assert.Equal("A demo: this would have saved the file", said);
    }

    [Fact]
    public void The_keys_of_a_chord_leave_a_message_showing()
    {
        var says = new List<string>();

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => { sample.Shell.ShowMessage("Saved"); },
            () => Press(Key.G.WithCtrl),
            () => sample.Shell.Scopes.PendingChord is not null,
            () =>
            {
                says.Add(sample.Shell.StatusBar.Says);
                Press(Key.F);
            },
            () => sample.Shell.Scopes.PendingChord is null,
            () =>
            {
                says.Add(sample.Shell.StatusBar.Says);
                Press(Key.A);
            },
            () => sample.Shell.StatusBar.Message is null,
            () => Press(Key.Esc),
        ]);

        Assert.Equal(["Ctrl+G…", "Saved"], says);
    }

    [Fact]
    public void Clicking_the_unread_count_opens_messages_on_the_newest_which_Ctrl_C_copies_and_Esc_closes()
    {
        var focused = false;
        var unreadAfter = true;

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => { sample.Shell.ShowMessage("Saved"); },
            () => { sample.Shell.ShowNews(Failure, Severity.Error); },
            () => { sample.Shell.StatusBar.UnreadButton.InvokeCommand(Command.Accept); },
            () => Messages is { IsInitialized: true },
            () =>
            {
                focused = Messages!.List.HasFocus && Messages.List.SelectedItem == 0;
                unreadAfter = sample.Shell.StatusBar.UnreadButton.Visible;
                Press(Key.C.WithCtrl);
            },
            () => _platform.Text.Length > 0,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SampleWindow,
            () => Press(Key.Esc),
        ]);

        Assert.True(focused);
        Assert.False(unreadAfter);
        Assert.Matches($"^\\d\\d:\\d\\d  {Failure}$", _platform.Text);
    }

    [Fact]
    public void A_logged_warning_counts_as_unread_without_a_message_and_the_selected_file_rests_on_the_bar()
    {
        string? said = null;
        var unread = "";
        var resting = "";

        Run(sample =>
        [
            () => sample.Files.State == LoadState.Loaded,
            () => sample.Shell.Commands.Execute(SampleWindow.LogWarning),
            () =>
            {
                said = sample.Shell.StatusBar.Message;
                unread = sample.Shell.StatusBar.UnreadButton.Text;
                resting = sample.Shell.StatusBar.Says;
                Press(Key.Esc);
            },
        ]);

        Assert.Null(said);
        Assert.Equal("⚠ 1", unread);
        Assert.Equal("src/notes.md", resting);
    }

    [Fact]
    public void Messages_is_in_the_help_menu_and_the_command_palette()
    {
        var palette = new List<string>();

        Run(sample =>
        [
            () =>
            {
                var help = sample.Shell.Menu!.Layout.Single(menu => menu.Title == "_Help").Items.Select(item => item?.CommandId);
                Assert.Contains(ShellCommands.ShowMessages, help);
                using var commands = new CommandPalette(sample.Shell.Commands, sample.Shell.Keys, Commands.CommandScope.Global);
                palette.AddRange(commands.Labels);
                Press(Key.Esc);
            },
        ]);

        Assert.Contains(palette, label => label.StartsWith("Messages "));
    }
}
