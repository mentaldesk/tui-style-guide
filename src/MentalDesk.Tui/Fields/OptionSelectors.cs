namespace MentalDesk.Tui.Fields;

public static class OptionSelectors
{
    // Terminal.Gui's arrows only move focus between options, and focus enters on the first option rather than the chosen one.
    public static T SelectOnArrows<T>(this T selector) where T : OptionSelector
    {
        foreach (var option in selector.SubViews.OfType<CheckBox>())
            Follow(option);
        selector.SubViewAdded += (_, e) =>
        {
            if (e.SubView is CheckBox option) Follow(option);
        };
        selector.HasFocusChanged += (_, focus) =>
        {
            if (focus.NewValue && selector.SubViews.OfType<CheckBox>().FirstOrDefault(o => o.Value == CheckState.Checked) is { HasFocus: false } chosen)
                chosen.SetFocus();
        };
        return selector;

        void Follow(CheckBox option) => option.HasFocusChanged += (_, focus) =>
        {
            if (focus.NewValue && focus.CurrentFocused?.SuperView == selector)
                selector.Value = selector.GetCheckBoxValue(option);
        };
    }
}
