using System.Drawing;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Tests;

public class SettingsDialogTests : StaticConfigurationTest
{
    private readonly Host _host = new(isWindows: false);
    private readonly TestPage _first = new("First");
    private readonly TestPage _second = new("Second");
    private readonly TestPage _third = new("Third");
    private readonly SettingsDialog _dialog;

    public SettingsDialogTests()
    {
        _dialog = new SettingsDialog([_first, _second, _third]);
    }

    public override void Dispose()
    {
        _dialog.Dispose();
        _host.Dispose();
        base.Dispose();
    }

    private IEnumerable<string> Names => _dialog.PageList.Source!.ToList().Cast<string>();

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new AppWindow(_host.Shell);
        _host.Run(window, [
            () => _dialog.Run(_host.Shell),
            () => _dialog.IsInitialized && _first.One.HasFocus,
            .. steps,
        ]);
    }

    [Fact]
    public void An_apps_own_pages_are_listed_with_the_first_open_and_its_first_control_focused()
    {
        Run(() =>
        {
            Assert.Equal("Settings", _dialog.Title);
            Assert.Equal(["First", "Second", "Third"], Names);
            Assert.Same(_first, _dialog.Page);
            Assert.True(_first.Visible);
            Assert.False(_second.Visible);
            Press(Key.Esc);
        });
    }

    [Fact]
    public void Ctrl_PgDn_and_Ctrl_PgUp_change_page_and_wrap_at_both_ends()
    {
        Run(
            () => Press(Key.PageDown.WithCtrl),
            () => _second.One.HasFocus,
            () => Press(Key.PageDown.WithCtrl),
            () => _third.One.HasFocus,
            () => Press(Key.PageDown.WithCtrl),
            () => _first.One.HasFocus && _dialog.PageList.SelectedItem == 0,
            () => Press(Key.PageUp.WithCtrl),
            () => _third.One.HasFocus && _dialog.PageList.SelectedItem == 2,
            () => Press(Key.Esc));
    }

    [Fact]
    public void Clicking_a_page_in_the_list_opens_it()
    {
        Run(
            () =>
            {
                var at = _dialog.PageList.ViewportToScreen(new Point(1, 1));
                _host.App.InjectMouse(new Mouse { ScreenPosition = at, Flags = MouseFlags.LeftButtonPressed });
                _host.App.InjectMouse(new Mouse { ScreenPosition = at, Flags = MouseFlags.LeftButtonReleased });
            },
            () => _dialog.Page == _second && _second.One.HasFocus,
            () => Press(Key.Esc));
    }

    [Fact]
    public void Tab_Shift_Tab_and_the_arrows_stay_among_the_pages_controls()
    {
        var focused = new List<View?>();
        void Record() => focused.Add(_dialog.MostFocused);
        Run([
            .. new[] { Key.Tab, Key.Tab, Key.Tab, Key.Tab.WithShift, Key.CursorDown, Key.CursorDown, Key.CursorUp, Key.CursorLeft, Key.CursorRight }
                .SelectMany(key => new Delegate[] { () => Press(key), () => Record() }),
            () => Press(Key.Esc),
        ]);

        Assert.All(focused, view => Assert.True(view == _first.One || view == _first.Two, $"{view} had focus"));
        Assert.False(_dialog.PageList.HasFocus);
    }

    [Fact]
    public void A_change_marks_its_page_and_putting_it_back_by_hand_clears_the_mark()
    {
        Run(
            () => Press(Key.Space),
            () => Names.SequenceEqual(["First •", "Second", "Third"]),
            () => Press(Key.Space),
            () => Names.SequenceEqual(["First", "Second", "Third"]),
            () => Press(Key.Esc));
    }

    [Fact]
    public void Ctrl_Enter_keeps_every_changed_page_and_clears_the_marks()
    {
        Run(
            () => Press(Key.Space, Key.PageDown.WithCtrl),
            () => _second.One.HasFocus,
            () => Press(Key.Space),
            () => Names.SequenceEqual(["First •", "Second •", "Third"]),
            () => Press(Key.Enter.WithCtrl),
            () => _host.App.TopRunnableView is AppWindow);

        Assert.True(_dialog.Confirmed);
        Assert.Equal([1, 1, 0], [_first.Keeps, _second.Keeps, _third.Keeps]);
        Assert.Equal(["First", "Second", "Third"], Names);
        Assert.Equal(0, _first.Reverts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Esc_or_the_close_box_puts_every_page_back(bool esc)
    {
        Run(
            () => Press(Key.Space, Key.PageDown.WithCtrl),
            () => _second.One.HasFocus,
            () => Press(Key.Space),
            () =>
            {
                if (esc) Press(Key.Esc);
                else _dialog.RequestStop();
            },
            () => _host.App.TopRunnableView is AppWindow);

        Assert.False(_dialog.Confirmed);
        Assert.Equal(CheckState.UnChecked, _first.One.Value);
        Assert.Equal(CheckState.UnChecked, _second.One.Value);
        Assert.Equal(["First", "Second", "Third"], Names);
        Assert.Equal([0, 0, 0], [_first.Keeps, _second.Keeps, _third.Keeps]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Clicking_Keep_keeps_and_clicking_Cancel_puts_back(bool keep)
    {
        Run(
            () => Press(Key.Space),
            () => _first.IsChanged,
            () => { (keep ? _dialog.KeepButton : _dialog.CancelButton).InvokeCommand(Command.Accept); },
            () => _host.App.TopRunnableView is AppWindow);

        Assert.Equal(keep, _dialog.Confirmed);
        Assert.Equal(keep ? 1 : 0, _first.Keeps);
        Assert.Equal(keep ? CheckState.Checked : CheckState.UnChecked, _first.One.Value);
    }

    [Fact]
    public void A_pages_own_key_works_only_on_that_page_and_F1_lists_it_there()
    {
        KeySheet? onFirst = null;
        KeySheet? onSecond = null;
        Run(
            () => Press(Key.X),
            () => _first.Done == 1,
            () => Press(Key.F1),
            () => _host.App.TopRunnableView is KeysDialog keys && (onFirst = keys.Sheet) is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SettingsDialog,
            () => Press(Key.PageDown.WithCtrl),
            () => _second.One.HasFocus,
            () => Press(Key.X),
            () => _second.Done == 1,
            () => Press(Key.F1),
            () => _host.App.TopRunnableView is KeysDialog keys && (onSecond = keys.Sheet) is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SettingsDialog,
            () => Press(Key.Esc));

        Assert.Equal([1, 1, 0], [_first.Done, _second.Done, _third.Done]);
        Assert.Equal("Here: Settings", onFirst!.Here!.Heading);
        Assert.Null(onFirst.Everywhere);
        var rows = onFirst.Here.Groups.SelectMany(group => group.Rows).Select(row => $"{row.Keys} {row.Label}").ToList();
        Assert.Contains("Ctrl+PgDn Next page", rows);
        Assert.Contains("Ctrl+PgUp Previous page", rows);
        Assert.Contains("Ctrl+Enter Keep", rows);
        Assert.Single(rows, "X Do it");
        Assert.Single(onSecond!.Here!.Groups.SelectMany(group => group.Rows), row => row.Keys == "X");
    }

    [Fact]
    public void It_fills_the_screen_with_Keep_and_Cancel_buttons_and_no_hint_row()
    {
        Run(() =>
        {
            Assert.Equal(_host.App.Screen.Width, _dialog.Frame.Width + _dialog.Margin!.Thickness.Horizontal);
            Assert.Equal(" Ctrl+Enter Keep ", _dialog.KeepButton.Text);
            Assert.Equal(" Esc Cancel ", _dialog.CancelButton.Text);
            Assert.True(_dialog.KeepButton.Frame.X < _dialog.CancelButton.Frame.X);
            Assert.Empty(Descendants(_dialog).OfType<HintRow>());
            Press(Key.Esc);
        });
    }

    [Fact]
    public void A_failed_keep_says_why_and_leaves_settings_open()
    {
        _first.Fail = true;
        Run(
            () => Press(Key.Space, Key.Enter.WithCtrl),
            () => _dialog.Alert.Visible,
            () =>
            {
                Assert.Equal("Couldn't keep your settings: disk full", _dialog.Alert.Message);
                Assert.Equal(["First •", "Second", "Third"], Names);
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is AppWindow);
    }

    private static IEnumerable<View> Descendants(View view) =>
        view.SubViews.SelectMany(sub => Descendants(sub).Prepend(sub));

    private sealed class TestPage : SettingsPage
    {
        private CheckState _saved = CheckState.UnChecked;

        public TestPage(string title) : base(title)
        {
            One = new CheckBox { Text = "One" };
            Two = new CheckBox { Text = "Two", Y = 1 };
            One.ValueChanged += (_, _) => OnChanged();
            Add(One, Two, ActionButton($"{title}.do", "Do it", Key.X, () => Done++));
        }

        public CheckBox One { get; }

        public CheckBox Two { get; }

        public int Keeps { get; private set; }

        public int Reverts { get; private set; }

        public int Done { get; private set; }

        public bool Fail { get; set; }

        public override bool IsChanged => One.Value != _saved;

        public override void Keep()
        {
            if (Fail) throw new IOException("disk full");
            _saved = One.Value;
            Keeps++;
        }

        public override void Revert()
        {
            One.Value = _saved;
            Reverts++;
        }
    }
}
