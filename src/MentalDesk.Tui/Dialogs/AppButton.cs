using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Dialogs;

public static class AppButton
{
    public static Button Primary(string text) => Filled(text, SchemeNames.ButtonPrimary, isDefault: true);

    public static Button Danger(string text) => Filled(text, SchemeNames.ButtonDanger);

    public static Button Secondary(string text) => Filled(text, SchemeNames.ButtonSecondary);

    // With NoDecorations the stock Button drops its padding, so the padding is part of the text.
    private static Button Filled(string text, string scheme, bool isDefault = false) => new()
    {
        Text = $" {text} ",
        NoDecorations = true,
        ShadowStyle = ShadowStyles.None,
        SchemeName = scheme,
        IsDefault = isDefault,
    };
}
