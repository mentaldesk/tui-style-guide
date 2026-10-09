using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Shell;
using Swatch;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class CopyHostTests : StaticConfigurationTest
{
    private Host? _host;

    public override void Dispose()
    {
        _host?.Dispose();
        base.Dispose();
    }

    private Host Start(IClipboard? platform = null) => _host = new Host(platform);

    private string FocusWord => _host!.Shell.StatusBar.FocusWord;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host!.App.InjectKey(key);
    }

    [Fact]
    public void Installs_the_terminal_clipboard_as_the_app_s()
    {
        var platform = new FakeClipboard();
        var host = Start(platform);

        Assert.Same(host.Shell.Clipboard, host.App.Clipboard);
        Assert.Same(platform, host.Shell.Clipboard.Platform);
    }

    [Fact]
    public void A_nested_shell_shares_the_clipboard()
    {
        var host = Start();
        using var inner = new AppShell(host.App, host.Shell.Cursor);

        Assert.Same(host.Shell.Clipboard, inner.Clipboard);
    }

    [Fact]
    public void Ctrl_C_in_roles_copies_the_selected_role_s_colours()
    {
        var platform = new TestClipboard();
        var host = Start(platform);
        using var window = new SwatchWindow(host.Shell);

        host.Run(window,
            () => FocusWord == "Themes",
            () => Press(Key.G.WithCtrl, Key.R),
            () => FocusWord == "Roles",
            () => Press(Key.CursorDown, Key.CursorDown, Key.C.WithCtrl),
            () => host.Shell.StatusBar.Message is not null);

        Assert.Matches("^#[0-9A-F]{6} on #[0-9A-F]{6}$", platform.Text);
        Assert.Equal("Copied 1 line  •  18 characters", host.Shell.StatusBar.Message);
        Assert.Equal(Severity.Info, host.Shell.StatusBar.MessageSeverity);
    }

    [Fact]
    public void With_no_clipboard_on_this_machine_the_copy_goes_through_the_terminal()
    {
        var host = Start(new FakeClipboard());
        using var window = new SwatchWindow(host.Shell);

        host.Run(window,
            () => FocusWord == "Themes",
            () => { host.Shell.Commands.Execute(SwatchCommands.CopyColours); },
            () => host.Shell.StatusBar.Message is not null);

        Assert.Equal("Copied 1 line  •  18 characters through the terminal", host.Shell.StatusBar.Message);
        Assert.Equal(Severity.Info, host.Shell.StatusBar.MessageSeverity);
    }

    [Fact]
    public void The_edit_menu_has_copy_colours_with_its_key()
    {
        var host = Start();
        using var window = new SwatchWindow(host.Shell);
        var menu = host.Shell.Menu!;

        Assert.Equal(["_File", "_Edit", "_Go", "_Theme", "_Help"], menu.Menus.Select(item => item.Title));
        var copy = menu.Items.Single(entry => entry.Id == SwatchCommands.CopyColours).Item;
        Assert.Equal("_Copy colours", copy.Title);
        Assert.Equal("Ctrl+C", copy.KeyView.Text);
    }

    [Fact]
    public void A_copy_that_lands_nowhere_says_why_as_an_error()
    {
        var host = Start(new TestClipboard { Transform = _ => null });
        using var window = new SwatchWindow(host.Shell);

        host.Run(window,
            () => FocusWord == "Themes",
            () => { host.Shell.Copy(new string('x', TerminalClipboard.MaxBytes + 1)); },
            () => host.Shell.StatusBar.Message is not null);

        Assert.Equal("Copy failed: too large to send through the terminal (the limit is 750 KB)", host.Shell.StatusBar.Message);
        Assert.Equal(Severity.Error, host.Shell.StatusBar.MessageSeverity);
    }

    [Fact]
    public void A_text_field_copies_and_pastes_through_the_installed_clipboard()
    {
        var platform = new TestClipboard();
        var host = Start(platform);
        var field = new TextField { Width = 20, Text = "hello" };
        var target = new TextField { Y = 1, Width = 20 };
        using var window = new Window();
        window.Add(field, target);

        host.Run(window,
            () => field.HasFocus,
            () =>
            {
                field.SelectAll();
                Press(Key.C.WithCtrl);
            },
            () => platform.Text == "hello",
            () => host.Shell.StatusBar.Message == "Copied 1 line  •  5 characters",
            () =>
            {
                platform.Text = "from another app";
                target.SetFocus();
                Press(Key.V.WithCtrl);
            },
            () => target.Text == "from another app");
    }

    [Fact]
    public void A_text_field_pastes_when_Terminal_Gui_says_the_clipboard_is_unsupported()
    {
        var host = Start(new FakeClipboard(isSupportedAlwaysFalse: true));
        var field = new TextField { Width = 20 };
        using var window = new Window();
        window.Add(field);

        host.Run(window,
            () => field.HasFocus,
            () =>
            {
                host.Shell.Copy("pasted");
                Press(Key.V.WithCtrl);
            },
            () => field.Text == "pasted");
    }
}
