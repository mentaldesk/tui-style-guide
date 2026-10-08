using MentalDesk.Tui.Dialogs;

namespace MentalDesk.Tui.Tests;

public class ConfirmDialogTests : StaticConfigurationTest
{
    private static readonly ConfirmAction Delete = new("Delete", ButtonKind.Danger, Key.Delete);
    private static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, Key.S);

    [Fact]
    public void Enter_is_never_bound_to_a_danger_action()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Delete", ["Sure?"], [Delete], enter: Delete));
    }

    [Fact]
    public void Enter_is_bound_only_to_one_of_the_actions()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Save", ["Sure?"], [Save], enter: Delete));
    }

    [Fact]
    public void A_key_that_is_not_a_letter_of_the_label_is_written_on_the_button()
    {
        using var dialog = new ConfirmDialog("Delete", ["Sure?"], [Delete]);

        Assert.Equal(" Delete  Del ", dialog.ButtonFor(Delete).Text);
    }

    [Fact]
    public void A_letter_key_is_the_buttons_underlined_hotkey()
    {
        using var dialog = new ConfirmDialog("Save", ["Sure?"], [Save]);

        Assert.Equal(" _Save ", dialog.ButtonFor(Save).Text);
        Assert.Equal(Key.S, dialog.ButtonFor(Save).HotKey);
    }

    [Fact]
    public void Cancel_comes_last_and_hints_end_with_Esc_cancel()
    {
        using var dialog = new ConfirmDialog("Save", ["Sure?"], [Save, Delete], enter: Save);

        Assert.Equal([dialog.ButtonFor(Save), dialog.ButtonFor(Delete), dialog.CancelButton], dialog.ButtonRow);
        Assert.Equal("Enter save  •  Del delete  •  Esc cancel", dialog.HintText);
    }
}
