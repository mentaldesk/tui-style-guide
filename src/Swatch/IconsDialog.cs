using MentalDesk.Tui;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Icons;

namespace Swatch;

internal sealed class IconsDialog : AppDialog
{
    private const string KeepId = "icons.keep";
    private const int NerdFontX = 3;
    private const int PlainX = 20;

    private static readonly IconStyle[] Styles = [IconStyle.Auto, IconStyle.NerdFont, IconStyle.Plain];

    private readonly IconSettings _icons;

    public IconsDialog(IconSettings icons) : base("Icons", width: 70, contentRows: 12)
    {
        _icons = icons;
        Style = new OptionSelector
        {
            X = 8,
            Y = 0,
            Orientation = Orientation.Horizontal,
            Labels = [.. Styles.Select(IconSettings.Name)],
            Value = Array.IndexOf(Styles, icons.Style),
        };
        Style.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } index) icons.Preview(Styles[index]);
        };
        Add(new Label { Text = "Style", X = 1, Y = 0 }, Style,
            Dimmed(icons.AutoReason, 1),
            Dimmed("Kept for every MentalDesk app; others use it when they restart.", 2),
            new Line { X = 0, Y = 3, Width = Dim.Fill() },
            new Label { Text = "Nerd Font", X = NerdFontX, Y = 4 },
            new Label { Text = "Plain", X = PlainX, Y = 4 });
        var y = 5;
        foreach (var (name, icon) in SwatchIcons.Sample)
        {
            Add(new Label { Text = IconField.Pad(icon.NerdFont) + name, X = NerdFontX, Y = y },
                new Label { Text = IconField.Pad(icon.Plain) + name, X = PlainX, Y = y });
            y++;
        }

        KeepButton = AppButton.Primary("Ctrl+Enter Keep");
        KeepButton.IsDefault = false;
        KeepButton.Accepting += (_, e) => Press(e, Keep);
        CancelButton = AppButton.Secondary("Esc Cancel");
        CancelButton.Accepting += (_, e) => Press(e, Cancel);
        CancelButton.X = Pos.AnchorEnd() - 1;
        KeepButton.X = Pos.Left(CancelButton) - KeepButton.Text.Length - 3;
        KeepButton.Y = CancelButton.Y = 11;
        Add(KeepButton, CancelButton);

        Commands.Register(KeepId, "Keep", Keep);
        Keys.Bind("Ctrl+Enter", KeepId);
        Initialized += (_, _) => Style.SetFocus();
    }

    public OptionSelector Style { get; }

    public Button KeepButton { get; }

    public Button CancelButton { get; }

    private static Label Dimmed(string text, int y)
    {
        var label = new Label { Text = text, X = 1, Y = y };
        label.GettingAttributeForRole += (_, e) =>
        {
            if (e.Role != VisualRole.Normal) return;
            e.Result = label.GetAttributeForRole(VisualRole.ReadOnly);
            e.Handled = true;
        };
        return label;
    }

    private static void Press(CommandEventArgs e, Action choice)
    {
        e.Handled = true;
        choice();
    }

    private void Keep()
    {
        try
        {
            _icons.Keep();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowAlert($"Couldn't keep the icon style: {ex.Message}", Severity.Error);
            return;
        }
        Accept();
    }
}
