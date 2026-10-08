using System.Collections.ObjectModel;
using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Tests;

public class LoadStateViewTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private readonly ListView _list = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private readonly LoadStateView _view;
    private readonly Window _window = new();

    public LoadStateViewTests()
    {
        _view = new LoadStateView(_list, "Loading rows…");
        var pane = new FrameView { Title = "Rows", Width = Dim.Fill(), Height = Dim.Fill(), BorderStyle = LineStyle.Single };
        pane.Add(_view);
        _window.Add(pane);
        _window.Initialized += (_, _) => _list.SetFocus();
    }

    public override void Dispose()
    {
        _window.Dispose();
        _host.Dispose();
        base.Dispose();
    }

    private static (string, Action) Retry(Action run) => ("R retry", run);

    private void Rows(params string[] rows) => _list.SetSource(new ObservableCollection<string>(rows));

    [Fact]
    public void Loading_then_content_shows_the_content()
    {
        _host.Run(_window,
            () => Assert.Equal(LoadState.Loading, _view.State),
            () =>
            {
                Rows("one");
                _view.ShowContent();
            },
            () =>
            {
                Assert.Equal(LoadState.Loaded, _view.State);
                Assert.Equal(string.Empty, _view.Says);
            });
    }

    [Fact]
    public void Loading_then_empty_says_what_is_not_there_and_the_next_step()
    {
        var ran = false;

        _host.Run(_window,
            () => _view.ShowEmpty("No open pull requests", ("N new branch", () => ran = true)),
            () => { _view.NextStep!.InvokeCommand(Command.Accept); });

        Assert.Equal(LoadState.Empty, _view.State);
        Assert.Equal("No open pull requests", _view.Says);
        Assert.Equal("N new branch", _view.NextStep!.Text);
        Assert.True(ran);
    }

    [Fact]
    public void Failed_then_retry_loads_again_and_shows_the_content()
    {
        _host.Run(_window,
            () => _view.ShowFailed("Couldn't reach github.com: timed out", Retry(_view.ShowLoading)),
            () => Assert.Equal(LoadState.Failed, _view.State),
            () => { _view.NextStep!.InvokeCommand(Command.Accept); },
            () => Assert.Equal(LoadState.Loading, _view.State),
            () =>
            {
                Rows("one");
                _view.ShowContent();
            });

        Assert.Equal(LoadState.Loaded, _view.State);
        Assert.Equal(string.Empty, _view.Says);
    }

    [Fact]
    public void A_reload_keeps_the_content_and_spins_in_the_border()
    {
        _view.SpinnerDelay = TimeSpan.Zero;
        var during = string.Empty;

        _host.Run(_window,
            () =>
            {
                Rows("one");
                _view.ShowContent();
            },
            () => _view.ShowLoading(),
            () => _view.ReloadSpinner.Visible,
            () =>
            {
                Assert.Equal(LoadState.Loaded, _view.State);
                Assert.True(_view.Reloading);
                Assert.False(_view.CentreSpinner.Visible);
                Assert.IsAssignableFrom<AdornmentView>(_view.ReloadSpinner.SuperView);
                during = _view.Says;
                _view.ShowContent();
            });

        Assert.Equal(string.Empty, during);
        Assert.False(_view.Reloading);
        Assert.False(_view.ReloadSpinner.Visible);
    }

    [Fact]
    public void A_reload_that_fails_keeps_the_content_and_reports_it()
    {
        string? reported = null;
        _view.ReloadFailed += (_, message) => reported = message;

        _host.Run(_window,
            () =>
            {
                Rows("one");
                _view.ShowContent();
            },
            () => _view.ShowLoading(),
            () => _view.ShowFailed("Couldn't reach github.com", Retry(_view.ShowLoading)));

        Assert.Equal(LoadState.Loaded, _view.State);
        Assert.Equal(string.Empty, _view.Says);
        Assert.Equal("Couldn't reach github.com", reported);
    }

    [Fact]
    public void A_load_shorter_than_the_delay_shows_no_spinner()
    {
        _view.SpinnerDelay = TimeSpan.FromMilliseconds(500);
        var ticks = 0;
        var spun = false;

        _host.Run(_window,
            () =>
            {
                spun |= _view.CentreSpinner.Visible || _view.Says.Length > 0;
                return ++ticks > 3;
            },
            () =>
            {
                Rows("one");
                _view.ShowContent();
            });

        Assert.False(spun);
    }

    [Fact]
    public void A_load_longer_than_the_delay_shows_the_spinner_and_what_is_loading()
    {
        _view.SpinnerDelay = TimeSpan.FromMilliseconds(20);

        _host.Run(_window,
            () => _view.CentreSpinner.Visible,
            () => Assert.Equal("Loading rows…", _view.Says));
    }

    [Fact]
    public void The_content_keeps_focus_in_every_state()
    {
        var focused = new List<bool>();
        void Check() => focused.Add(_list.HasFocus);

        _host.Run(_window,
            () => _list.HasFocus,
            () => _view.ShowEmpty("No rows"),
            Check,
            () => _view.ShowFailed("Couldn't load rows", Retry(_view.ShowLoading)),
            Check,
            () => _view.ShowLoading(),
            Check,
            () =>
            {
                Rows("one");
                _view.ShowContent();
            },
            Check);

        Assert.All(focused, Assert.True);
    }

    [Fact]
    public void Empty_is_dimmed_and_failed_is_in_the_Error_scheme()
    {
        Attribute empty = default, dimmed = default, failed = default, error = default;

        _host.Run(_window,
            () => _view.ShowEmpty("No rows"),
            () =>
            {
                empty = _view.Message.GetAttributeForRole(VisualRole.Normal);
                dimmed = _view.Message.GetAttributeForRole(VisualRole.ReadOnly);
            },
            () => _view.ShowFailed("Couldn't load rows", Retry(_view.ShowLoading)),
            () =>
            {
                failed = _view.Message.GetAttributeForRole(VisualRole.Normal);
                error = Themes.SchemesOf(Themes.Current)[SchemeNames.Error].Normal;
            });

        Assert.Equal(dimmed, empty);
        Assert.Equal(SchemeNames.Error, _view.Message.SchemeName);
        Assert.Equal(error.Foreground, failed.Foreground);
        Assert.Equal(error.Background, failed.Background);
    }

    [Fact]
    public void The_terminal_progress_indicator_is_on_while_loading_and_off_afterwards()
    {
        _view.SpinnerDelay = TimeSpan.Zero;
        var during = false;

        _host.Run(_window,
            () => _view.CentreSpinner.Visible,
            () =>
            {
                during = _view.CentreSpinner is { SyncWithTerminal: true, AutoSpin: true };
                Rows("one");
                _view.ShowContent();
            });

        Assert.True(during);
        Assert.False(_view.CentreSpinner.AutoSpin);
        Assert.False(_view.ReloadSpinner.AutoSpin);
    }
}
