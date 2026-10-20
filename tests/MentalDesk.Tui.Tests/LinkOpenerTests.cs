using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Links;
using MentalDesk.Tui.Shell;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class LinkOpenerTests
{
    private const string Url = "https://github.com/mentaldesk/tui-style-guide";

    [Fact]
    public void Opens_a_link_locally()
    {
        var browser = new TestBrowser();
        var opener = new LinkOpener(new TestClipboard(), browser, _ => null);

        var outcome = opener.Open(Url);

        Assert.Equal([Url], browser.Opened);
        Assert.Equal(new LinkOutcome($"Opened {Url}", Severity.Info), outcome);
    }

    [Theory]
    [InlineData("SSH_CONNECTION")]
    [InlineData("SSH_CLIENT")]
    [InlineData("SSH_TTY")]
    public void Copies_the_link_over_SSH(string variable)
    {
        var browser = new TestBrowser();
        var clipboard = new TestClipboard();
        var opener = new LinkOpener(clipboard, browser, name => name == variable ? "10.0.0.1 22" : null);

        var outcome = opener.Open(Url);

        Assert.Empty(browser.Opened);
        Assert.Equal(Url, clipboard.Text);
        Assert.Equal(new LinkOutcome($"Copied {Url} (no browser over SSH)", Severity.Info), outcome);
    }

    [Fact]
    public void A_failed_copy_over_SSH_says_why()
    {
        var clipboard = new TestClipboard { Throw = new InvalidOperationException("no display") };
        var opener = new LinkOpener(clipboard, new TestBrowser(), name => name == "SSH_TTY" ? "/dev/ttys001" : null);

        var outcome = opener.Open(Url);

        Assert.Equal(new LinkOutcome("Copy failed: no display (TestClipboard)", Severity.Error), outcome);
    }

    [Fact]
    public void A_missing_opener_is_an_error()
    {
        var opener = new LinkOpener(new TestClipboard(), new SystemBrowser("mentaldesk-no-such-opener"), _ => null);

        var outcome = opener.Open(Url);

        Assert.Equal(new LinkOutcome("Couldn't open the link: mentaldesk-no-such-opener not found", Severity.Error), outcome);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/etc/passwd")]
    [InlineData("README.md")]
    public void Only_web_and_mail_links_open(string url)
    {
        var browser = new TestBrowser();
        var clipboard = new TestClipboard();
        var opener = new LinkOpener(clipboard, browser, _ => "over SSH");

        var outcome = opener.Open(url);

        Assert.Empty(browser.Opened);
        Assert.Empty(clipboard.Text);
        Assert.Equal(new LinkOutcome("Couldn't open the link: only web and mail links open", Severity.Error), outcome);
    }

    [Fact]
    public void A_mail_link_opens()
    {
        var browser = new TestBrowser();

        new LinkOpener(null, browser, _ => null).Open("mailto:someone@example.com");

        Assert.Equal(["mailto:someone@example.com"], browser.Opened);
    }

    [Fact]
    public void The_opener_cannot_write_to_the_terminal()
    {
        var start = new SystemBrowser("xdg-open").StartInfo(Url);

        Assert.True(start.RedirectStandardInput);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
        Assert.Equal([Url], start.ArgumentList);
    }
}

public class OpenLinkHostTests : StaticConfigurationTest
{
    private Host? _host;

    public override void Dispose()
    {
        _host?.Dispose();
        base.Dispose();
    }

    [Fact]
    public void Open_the_guide_on_GitHub_says_on_the_status_bar_that_it_opened()
    {
        var host = _host = new Host();
        var browser = new TestBrowser();
        host.Shell.Links = new LinkOpener(host.Shell.Clipboard, browser, _ => null);
        using var window = new SwatchWindow(host.Shell);

        host.Run(window,
            () => host.Shell.Commands.Execute(SwatchCommands.OpenGuide),
            () => host.Shell.StatusBar.Message is not null);

        Assert.Equal([SwatchWindow.GuideUrl], browser.Opened);
        Assert.Equal($"Opened {SwatchWindow.GuideUrl}", host.Shell.StatusBar.Message);
        Assert.Equal(Severity.Info, host.Shell.StatusBar.MessageSeverity);
        Assert.Equal(MessageKind.Reply, host.Shell.StatusBar.Current!.Kind);
    }

    [Fact]
    public void Over_SSH_the_shell_copies_the_link_and_says_so()
    {
        var platform = new TestClipboard();
        var host = _host = new Host(platform);
        var browser = new TestBrowser();
        host.Shell.Links = new LinkOpener(host.Shell.Clipboard, browser, name => name == "SSH_CONNECTION" ? "10.0.0.1 22" : null);
        using var window = new SwatchWindow(host.Shell);

        host.Run(window,
            () => { host.Shell.OpenLink(SwatchWindow.GuideUrl); },
            () => host.Shell.StatusBar.Message is not null);

        Assert.Empty(browser.Opened);
        Assert.Equal(SwatchWindow.GuideUrl, platform.Text);
        Assert.Equal($"Copied {SwatchWindow.GuideUrl} (no browser over SSH)", host.Shell.StatusBar.Message);
    }

    [Fact]
    public void The_guide_is_in_the_Help_menu_just_before_diagnostics()
    {
        var host = _host = new Host();
        using var window = new SwatchWindow(host.Shell);

        var help = host.Shell.Menu!.Layout.Single(menu => menu.Title == "_Help").Items
            .Select(item => item?.CommandId).ToList();

        Assert.Equal(help.IndexOf(SwatchCommands.ReadLog) + 1, help.IndexOf(SwatchCommands.OpenGuide));
        Assert.Equal(help.IndexOf(SwatchCommands.OpenGuide) + 1, help.IndexOf(ShellCommands.ShowDiagnostics));
    }
}

internal sealed class TestBrowser : IBrowser
{
    public List<string> Opened { get; } = [];

    public string? Open(string url)
    {
        Opened.Add(url);
        return null;
    }
}
