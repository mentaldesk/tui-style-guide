using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Shell;

namespace Swatch;

internal sealed class SwatchesPage : SettingsPage
{
    public const string ResetId = "swatches.reset";

    public static readonly ConfirmAction ResetAction = new("Reset", ButtonKind.Primary, CtrlEnter.Key);

    private readonly AppShell _shell;
    private readonly SwatchSettings _settings;
    private readonly Action<bool> _showHexValues;
    private readonly Label _hexDefault;
    private bool _savedShowHexValues;

    public SwatchesPage(AppShell shell, SwatchSettings settings, Action<bool> showHexValues) : base("Swatches")
    {
        _shell = shell;
        _settings = settings;
        _showHexValues = showHexValues;
        _savedShowHexValues = settings.ShowHexValues;
        ShowHexValues = new CheckBox
        {
            Text = "Show hex values",
            Value = _savedShowHexValues ? CheckState.Checked : CheckState.UnChecked,
        };
        ShowHexValues.ValueChanged += (_, _) => OnHexValuesChanged();
        _hexDefault = Dimmed(new Label { X = Pos.Right(ShowHexValues) + 2 });
        ResetButton = ActionButton(ResetId, "Reset to defaults…", Key.R, ConfirmReset);
        ResetButton.Y = 2;
        Add(ShowHexValues, _hexDefault, ResetButton);
        ShowHexDefault();
    }

    public CheckBox ShowHexValues { get; }

    public Button ResetButton { get; }

    public string HexDefault => _hexDefault.Text;

    public override bool IsChanged => Shown != _savedShowHexValues;

    private bool Shown => ShowHexValues.Value == CheckState.Checked;

    public static ConfirmDialog ResetConfirm() =>
        new("Reset to defaults", ["Put this page's settings back to their defaults?", "Esc on Settings still undoes it."], [ResetAction]);

    public override void Keep()
    {
        _settings.ShowHexValues = Shown;
        _savedShowHexValues = Shown;
    }

    public override void Revert() => Show(_savedShowHexValues);

    private void ConfirmReset()
    {
        using var confirm = ResetConfirm();
        confirm.Run(_shell);
        if (confirm.Chosen is not null)
            Show(SwatchSettings.DefaultShowHexValues);
    }

    private void Show(bool hexValues) =>
        ShowHexValues.Value = hexValues ? CheckState.Checked : CheckState.UnChecked;

    private void OnHexValuesChanged()
    {
        _showHexValues(Shown);
        ShowHexDefault();
        OnChanged();
    }

    private void ShowHexDefault() =>
        _hexDefault.Text = Shown == SwatchSettings.DefaultShowHexValues ? string.Empty : DefaultNote(SwatchSettings.DefaultShowHexValues ? "on" : "off");
}
