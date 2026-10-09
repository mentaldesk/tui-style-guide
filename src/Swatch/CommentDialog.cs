using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Fields;

namespace Swatch;

internal sealed class CommentDialog : AppDialog
{
    private const string PostId = "comment.post";
    private const string UndoId = "comment.undo";

    public CommentDialog() : base("Writing a comment", width: 60, contentRows: 9)
    {
        Body = new MultiLineField { X = 1, Y = 3, Width = Dim.Fill(1), Height = 6, Placeholder = "Leave a comment…" };
        Add(new Label { Text = "Title", X = 1, Y = 0 }, TitleField, Body);

        Commands
            .Register(PostId, "Post", Accept)
            .Register(UndoId, "Undo", () => Body.InvokeCommand(Command.Undo));
        Keys.Bind("Ctrl+Enter", PostId)
            .Bind("Ctrl+Z", UndoId);
        ShowHints(new Hint(PostId, "post"), new Hint(UndoId, "undo"), new Hint(CancelId, "cancel"));
        Initialized += (_, _) => Body.SetFocus();
    }

    public TextField TitleField { get; } = new() { X = 1, Y = 1, Width = Dim.Fill(1) };

    public MultiLineField Body { get; }
}
