using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class ConfirmFieldTests : StaticConfigurationTest
{
    private static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, CtrlEnter.Key);
    private static readonly ConfirmAction Delete = new("Delete", ButtonKind.Danger, CtrlEnter.Key);
    private static readonly ConfirmAction DontSave = new("Don't save", ButtonKind.Danger, Key.D);

    private Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    [Fact]
    public void The_field_starts_focused_keeps_its_arrows_and_letters_and_Ctrl_Enter_saves()
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = new ConfirmDialog("Save theme", ["Save as"], [Save], field: new TextField { Text = "Solar" });
        var typed = "";

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press(Key.End, Key.CursorLeft, Key.X, Key.Space),
            () => dialog.Field!.Text == "Solax r",
            () =>
            {
                typed = dialog.Field!.Text;
                Press(CtrlEnter.Key);
            },
            () => _host.App.TopRunnableView == window);

        Assert.Equal("Solax r", typed);
        Assert.Equal(Save, dialog.Chosen);
    }

    [Theory]
    [InlineData("Primary")]
    [InlineData("Danger")]
    public void Ctrl_Enter_runs_the_main_action(string kind)
    {
        var action = kind == "Primary" ? Save : Delete;
        using var window = new AppWindow(_host.Shell);
        using var dialog = new ConfirmDialog("Sure?", ["Sure?"], [action]);

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.CancelButton.HasFocus,
            () => Press(CtrlEnter.Key),
            () => _host.App.TopRunnableView == window);

        Assert.Equal(action, dialog.Chosen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Enter_runs_nothing_wherever_the_focus_is(int tabs)
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = new ConfirmDialog("Save theme", ["Save as"], [Save, DontSave], field: new TextField { Text = "Solar" });
        var ticks = 0;
        View? focused = null;

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press([.. Enumerable.Repeat(Key.Tab, tabs)]),
            () => (focused = dialog.MostFocused) is not null,
            () => Press(Key.Enter),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView == dialog,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView == window);

        Assert.Equal(((View[])[dialog.Field!, .. dialog.ButtonRow])[tabs], focused);
        Assert.Null(dialog.Chosen);
        Assert.Equal("Solar", dialog.Field!.Text);
    }

    [Fact]
    public void Tab_to_a_button_and_Space_presses_it()
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = new ConfirmDialog("Save theme", ["Save as"], [Save, DontSave], field: new TextField { Text = "Solar" });

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press(Key.Tab, Key.Tab),
            () => dialog.ButtonFor(DontSave).HasFocus,
            () => Press(Key.Space),
            () => _host.App.TopRunnableView == window);

        Assert.Equal(DontSave, dialog.Chosen);
    }

    [Fact]
    public void Off_the_field_the_arrows_move_between_the_buttons()
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = new ConfirmDialog("Save theme", ["Save as"], [Save], field: new TextField { Text = "Solar" });

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press(Key.Tab),
            () => dialog.ButtonFor(Save).HasFocus,
            () => Press(Key.CursorRight),
            () => dialog.CancelButton.HasFocus,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView == window);

        Assert.Null(dialog.Chosen);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ctrl_LineFeed_is_Ctrl_Enter_on_Windows_only(bool isWindows)
    {
        _host.Dispose();
        _host = new Host(isWindows: isWindows);
        using var window = new AppWindow(_host.Shell);
        using var dialog = new ConfirmDialog("Delete", ["Sure?"], [Delete]);
        var ticks = 0;

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.CancelButton.HasFocus,
            () => Press(new Key(KeyCode.CtrlMask | (KeyCode)0x0A)),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView == window == isWindows,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView == window);

        Assert.Equal(isWindows ? Delete : null, dialog.Chosen);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }
}
