using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Tests;

public class ConfirmFieldTests : StaticConfigurationTest
{
    private static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, Key.Enter);

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    [Fact]
    public void The_field_starts_focused_keeps_its_arrows_and_letters_and_Enter_saves()
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
                Press(Key.Enter);
            },
            () => _host.App.TopRunnableView == window);

        Assert.Equal("Solax r", typed);
        Assert.Equal(Save, dialog.Chosen);
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

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }
}
