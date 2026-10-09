using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Fields;

namespace Swatch;

internal sealed class CommentDialog : AppDialog
{
    private const string PostId = "comment.post";

    public CommentDialog() : base("Writing a comment", width: 60, contentRows: 11)
    {
        Body = new MultiLineField { X = 1, Y = 3, Width = Dim.Fill(1), Height = 6, Placeholder = "Leave a comment…" };
        PostButton = AppButton.Primary("Ctrl+Enter Post");
        PostButton.IsDefault = false;
        PostButton.Accepting += (_, e) => Press(e, Accept);
        CancelButton = AppButton.Secondary("Esc Cancel");
        CancelButton.Accepting += (_, e) => Press(e, Cancel);
        CancelButton.X = Pos.AnchorEnd() - 1;
        PostButton.X = Pos.Left(CancelButton) - PostButton.Text.Length - 3;
        PostButton.Y = CancelButton.Y = 10;
        Add(new Label { Text = "Title", X = 1, Y = 0 }, TitleField, Body, PostButton, CancelButton);

        Commands.Register(PostId, "Post", Accept);
        Keys.Bind("Ctrl+Enter", PostId);
        Initialized += (_, _) => Body.SetFocus();
    }

    public TextField TitleField { get; } = new() { X = 1, Y = 1, Width = Dim.Fill(1) };

    public MultiLineField Body { get; }

    public Button PostButton { get; }

    public Button CancelButton { get; }

    private static void Press(CommandEventArgs e, Action choice)
    {
        e.Handled = true;
        choice();
    }
}
