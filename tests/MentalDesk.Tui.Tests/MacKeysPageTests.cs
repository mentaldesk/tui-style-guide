using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Settings;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class MacKeysPageTests : StaticConfigurationTest
{
    private const string Active = "\e]1337;SetUserVar=SWATCH_ACTIVE=MQ==\a";
    private const string Inactive = "\e]1337;SetUserVar=SWATCH_ACTIVE=MA==\a";

    private Host? _host;

    public override void Dispose()
    {
        _host?.Dispose();
        base.Dispose();
    }

    private Host Host => _host ?? throw new InvalidOperationException("No host yet.");

    private SettingsDialog? Dialog => Host.App.TopRunnableView as SettingsDialog;

    private MacKeysPage Page => (MacKeysPage)Dialog!.Page!;

    private TerminalIntegration? Here => Host.Shell.MacKeys!.Here;

    public static TheoryData<string> Terminals => ["iTerm2", "kitty", "WezTerm"];

    private static (string, string) Variable(string terminal) => terminal switch
    {
        "iTerm2" => FakeTerminal.Iterm2,
        "kitty" => FakeTerminal.Kitty,
        "WezTerm" => FakeTerminal.WezTerm,
        _ => FakeTerminal.AppleTerminal,
    };

    private void In(string terminal) => _host = new Host(terminal: Variable(terminal));

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            Host.App.InjectKey(key);
    }

    private Delegate[] OpenPage() =>
    [
        () => { Host.Shell.Commands.Execute(SwatchCommands.ShowSettings); },
        () => Dialog?.IsInitialized == true,
        () => Dialog!.Open(3),
        () => Page.TurnedOn.HasFocus || !Page.TurnedOn.Enabled,
    ];

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(Host.Shell);
        Host.Run(window, steps);
    }

    [Theory]
    [MemberData(nameof(Terminals))]
    public void It_names_the_terminal_and_says_not_installed_with_the_box_clear(string terminal)
    {
        In(terminal);
        string[]? shown = null;
        Run([
            .. OpenPage(),
            () =>
            {
                shown = [Dialog!.PageList.Source!.ToList()[3]!.ToString()!, Page.State.Text, Page.TurnedOn.Text, Page.TurnedOn.Value.ToString(), Page.Note];
                Press(Key.Esc);
            },
            () => Dialog is null,
        ]);

        Assert.Equal(["Mac keys", "Not installed", "Cmd and Option keys in Swatch", "UnChecked", "Only Swatch is affected; your shell and other apps are unchanged."], shown!);
        Assert.Equal(terminal, Here!.Terminal);
    }

    [Theory]
    [MemberData(nameof(Terminals))]
    public void Ticking_it_writes_nothing_until_Ctrl_Enter_and_Esc_leaves_the_terminal_as_it_was(string terminal)
    {
        In(terminal);
        var marked = "";
        Run([
            .. OpenPage(),
            () => Press(Key.Space),
            () => Page.IsChanged,
            () =>
            {
                marked = Dialog!.PageList.Source!.ToList()[3]!.ToString()!;
                Press(Key.Esc);
            },
            () => Dialog is null,
        ]);

        Assert.Equal("Mac keys •", marked);
        Assert.Equal(MacKeysState.NotInstalled, Here!.State);
        Assert.False(Directory.Exists(Host.Home));
    }

    [Theory]
    [MemberData(nameof(Terminals))]
    public void Ctrl_Enter_with_it_ticked_installs_and_reopening_says_installed(string terminal)
    {
        In(terminal);
        string[]? reopened = null;
        var copyShown = false;
        Run([
            .. OpenPage(),
            () => Press(Key.Space),
            () => Page.IsChanged,
            () => Press(Key.Enter.WithCtrl),
            () => Dialog is null,
            .. OpenPage(),
            () =>
            {
                reopened = [Page.State.Text, Page.TurnedOn.Value.ToString(), Page.Note];
                copyShown = Page.CopyButton.Visible;
                Press(Key.Esc);
            },
            () => Dialog is null,
        ]);

        Assert.Equal(MacKeysState.Installed, Here!.State);
        Assert.Equal("Installed", reopened![0]);
        Assert.Equal("Checked", reopened[1]);
        Assert.Equal(Here.ConfigLine is not null, copyShown);
        Assert.Equal(
            Here.ConfigFile is { } file ? $"Add this line to {file}, then reopen Swatch:" : "Only Swatch is affected; your shell and other apps are unchanged.",
            reopened[2]);
    }

    [Theory]
    [MemberData(nameof(Terminals))]
    public void Ctrl_Enter_with_it_cleared_removes_it(string terminal)
    {
        In(terminal);
        Here!.Install();
        Run([
            .. OpenPage(),
            () => Page.TurnedOn.Value == CheckState.Checked && !Page.IsChanged,
            () => Press(Key.Space),
            () => Page.IsChanged,
            () => Press(Key.Enter.WithCtrl),
            () => Dialog is null,
        ]);

        Assert.Equal(MacKeysState.NotInstalled, Here.State);
        Assert.False(File.Exists(Here.Path));
    }

    [Theory]
    [MemberData(nameof(Terminals))]
    public void An_out_of_date_copy_is_ticked_and_Ctrl_Enter_updates_it(string terminal)
    {
        In(terminal);
        Directory.CreateDirectory(Path.GetDirectoryName(Here!.Path)!);
        File.WriteAllText(Here.Path, "written by an older Swatch");
        var shown = "";
        Run([
            .. OpenPage(),
            () =>
            {
                shown = Page.State.Text;
                Assert.Equal(CheckState.Checked, Page.TurnedOn.Value);
                Assert.True(Page.IsChanged);
                Press(Key.Enter.WithCtrl);
            },
            () => Dialog is null,
        ]);

        Assert.Equal("Out of date · saving updates it", shown);
        Assert.Equal(MacKeysState.Installed, Here.State);
    }

    [Theory]
    [InlineData("kitty", "include swatch.conf")]
    [InlineData("WezTerm", "require 'swatch'.apply(config)")]
    public void Copy_copies_the_line_to_add_to_the_terminals_config(string terminal, string line)
    {
        In(terminal);
        Here!.Install();
        var said = "";
        Run([
            .. OpenPage(),
            () => Press(Key.C),
            () => Host.App.Clipboard?.GetClipboardData() == line,
            () =>
            {
                said = Dialog!.Alert.Message;
                Press(Key.Esc);
            },
            () => Dialog is null,
        ]);

        Assert.StartsWith("Copied 1 line", said, StringComparison.Ordinal);
    }

    [Fact]
    public void In_Terminal_app_it_cant_be_installed_and_says_which_keys_work_without_it()
    {
        In("Terminal.app");
        string[]? shown = null;
        var enabled = true;
        Run([
            .. OpenPage(),
            () => Press(Key.Space, Key.Enter.WithCtrl),
            () => Dialog is null,
            .. OpenPage(),
            () =>
            {
                shown = [Page.State.Text, Page.TurnedOn.Value.ToString(), Page.Note];
                enabled = Page.TurnedOn.Enabled;
                Press(Key.Esc);
            },
            () => Dialog is null,
        ]);

        Assert.Null(Here);
        Assert.False(enabled);
        Assert.Equal(["Can't be installed here", "UnChecked"], shown![..2]);
        Assert.StartsWith("Terminal.app can't change keys for one app only.", shown[2], StringComparison.Ordinal);
        Assert.Contains("Option+←/→ moves a word, fn+←/→ goes to the start and end of the line.", shown[2], StringComparison.Ordinal);
        Assert.False(Directory.Exists(Host.Home));
    }

    [Fact]
    public void It_lists_the_keys_the_mapping_gives()
    {
        In("kitty");
        var text = new List<string>();
        Run([
            .. OpenPage(),
            () =>
            {
                text.AddRange(Page.SubViews.SelectMany(view => view.SubViews.Append(view)).OfType<Label>().Select(label => label.Text));
                Press(Key.Esc);
            },
            () => Dialog is null,
        ]);

        foreach (var row in MacKeys.Rows)
        {
            Assert.Contains(row.Keys, text);
            Assert.Contains(row.Does, text);
        }
    }

    [Fact]
    public void Swatch_is_marked_running_while_it_runs_and_not_after_and_the_sample_app_leaves_it_alone()
    {
        In("kitty");
        var during = new List<string>();
        Run(
            () => { Host.Shell.Commands.Execute(SwatchCommands.TryTheme); },
            () => Host.App.TopRunnableView is SampleWindow,
            () =>
            {
                during.AddRange(Host.MacKeysWritten);
                Press(Key.Esc);
            },
            () => Host.App.TopRunnableView is SwatchWindow);

        Assert.Equal([Active], during);
        Assert.Equal([Active, Inactive], Host.MacKeysWritten);
    }
}
