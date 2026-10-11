using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Tests;

public class ConfirmDialogTests : StaticConfigurationTest
{
    private static readonly ConfirmAction Delete = new("Delete", ButtonKind.Danger, CtrlEnter.Key);
    private static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, CtrlEnter.Key);
    private static readonly ConfirmAction DontSave = new("Don't save", ButtonKind.Danger, Key.D);

    [Theory]
    [InlineData(ButtonKind.Primary)]
    [InlineData(ButtonKind.Danger)]
    public void An_action_bound_to_Enter_is_refused(ButtonKind kind)
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Save", ["Sure?"], [Save with { Kind = kind }, DontSave with { Key = Key.Enter }]));
    }

    [Fact]
    public void Ctrl_Enter_is_bound_to_exactly_one_action()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Save", ["Sure?"], [DontSave]));
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Save", ["Sure?"], [Save, Delete]));
    }

    [Fact]
    public void Focus_never_starts_on_a_Danger_button()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Delete", ["Sure?"], [Delete], focus: Delete));
    }

    [Fact]
    public void Ctrl_Enter_is_written_before_the_label_and_no_button_is_the_default()
    {
        using var dialog = new ConfirmDialog("Delete", ["Sure?"], [Delete]);

        Assert.Equal(" Ctrl+Enter Delete ", dialog.ButtonFor(Delete).Text);
        Assert.Equal(" Esc Cancel ", dialog.CancelButton.Text);
        Assert.DoesNotContain(dialog.ButtonRow, button => button.IsDefault);
    }

    [Fact]
    public void A_letter_key_is_the_buttons_underlined_hotkey()
    {
        using var dialog = new ConfirmDialog("Save", ["Sure?"], [Save, DontSave]);

        Assert.Equal(" _Don't save ", dialog.ButtonFor(DontSave).Text);
        Assert.Equal(Key.D, dialog.ButtonFor(DontSave).HotKey);
    }

    [Fact]
    public void Cancel_comes_last_and_the_buttons_are_the_only_hints()
    {
        using var dialog = new ConfirmDialog("Save", ["Sure?"], [Save, DontSave]);

        Assert.Equal([dialog.ButtonFor(Save), dialog.ButtonFor(DontSave), dialog.CancelButton], dialog.ButtonRow);
        Assert.Empty(dialog.SubViews.OfType<HintRow>());
    }
}
