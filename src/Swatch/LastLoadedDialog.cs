using MentalDesk.Tui.Dialogs;

namespace Swatch;

internal sealed class LastLoadedDialog : AppDialog
{
    public LastLoadedDialog(string at)
        : base("Files", width: 40, contentRows: 3)
    {
        Text = new Label { X = 1, Y = 0, Text = $"Last loaded {at}" };
        Add(Text);
        Commands.Register(CancelId, "Close", Cancel);
        AddButtonRow(2, CommandButton(ButtonKind.Secondary, CancelId));
    }

    public new Label Text { get; }
}
