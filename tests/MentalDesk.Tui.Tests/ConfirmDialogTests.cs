using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Dialogs;

namespace MentalDesk.Tui.Tests;

public class ConfirmDialogTests : StaticConfigurationTest
{
    private static readonly ConfirmAction Delete = new("Delete", ButtonKind.Danger, Key.Delete);
    private static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, Key.S);
    private static readonly ConfirmAction SaveOnEnter = new("Save", ButtonKind.Primary, Key.Enter);

    [Fact]
    public void Enter_is_never_bound_to_a_danger_action()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Delete", ["Sure?"], [Delete with { Key = Key.Enter }]));
    }

    [Fact]
    public void Enter_is_bound_to_one_action_at_most()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Save", ["Sure?"], [SaveOnEnter, SaveOnEnter with { Label = "Keep" }]));
    }

    [Fact]
    public void A_key_that_is_not_a_letter_of_the_label_is_written_before_it_on_the_button()
    {
        using var dialog = new ConfirmDialog("Delete", ["Sure?"], [Delete, SaveOnEnter]);

        Assert.Equal(" Del Delete ", dialog.ButtonFor(Delete).Text);
        Assert.Equal(" Enter Save ", dialog.ButtonFor(SaveOnEnter).Text);
        Assert.True(dialog.ButtonFor(SaveOnEnter).IsDefault);
        Assert.Equal(" Esc Cancel ", dialog.CancelButton.Text);
    }

    [Fact]
    public void A_letter_key_is_the_buttons_underlined_hotkey()
    {
        using var dialog = new ConfirmDialog("Save", ["Sure?"], [Save]);

        Assert.Equal(" _Save ", dialog.ButtonFor(Save).Text);
        Assert.Equal(Key.S, dialog.ButtonFor(Save).HotKey);
    }

    [Fact]
    public void Cancel_comes_last_and_the_buttons_are_the_only_hints()
    {
        using var dialog = new ConfirmDialog("Save", ["Sure?"], [Save, Delete]);

        Assert.Equal([dialog.ButtonFor(Save), dialog.ButtonFor(Delete), dialog.CancelButton], dialog.ButtonRow);
        Assert.Empty(dialog.SubViews.OfType<HintRow>());
    }
}
