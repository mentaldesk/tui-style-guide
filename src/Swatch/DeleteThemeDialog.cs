using MentalDesk.Tui.Dialogs;

namespace Swatch;

internal sealed class DeleteThemeDialog : AppDialog
{
    public DeleteThemeDialog(string theme)
        : base("Delete theme", width: 52, contentRows: 3)
    {
        Message = $"Delete \"{theme}\"? This can't be undone.";
        DeleteButton = AppButton.Danger("Delete");
        CancelButton = AppButton.Secondary("Cancel");
        CancelButton.X = Pos.AnchorEnd() - 1;
        CancelButton.Y = 2;
        DeleteButton.X = Pos.Left(CancelButton) - DeleteButton.Text.Length - 3;
        DeleteButton.Y = 2;
        DeleteButton.Accepting += (_, e) => Choose(e, Accept);
        CancelButton.Accepting += (_, e) => Choose(e, Cancel);
        Add(new Label { X = 1, Y = 0, Text = Message }, DeleteButton, CancelButton);
        Initialized += (_, _) => CancelButton.SetFocus();
    }

    public string Message { get; }

    public Button DeleteButton { get; }

    public Button CancelButton { get; }

    private static void Choose(CommandEventArgs e, Action choice)
    {
        e.Handled = true;
        choice();
    }
}
