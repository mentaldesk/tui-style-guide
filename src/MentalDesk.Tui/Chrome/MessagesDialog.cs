using System.Collections.ObjectModel;
using MentalDesk.Tui.Copying;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Chrome;

public sealed class MessagesDialog : AppDialog
{
    public const string CopyId = "messages.copy";

    private const int ContentRows = 14;

    private readonly ListView _list;

    public MessagesDialog(AppStatusBar bar)
        : base("Messages", width: 76, ContentRows)
    {
        Rows = [.. bar.History.Reverse()];
        bar.MarkRead();

        _list = new ListView { X = 1, Y = 0, Width = Dim.Fill(1), Height = ContentRows - 2, Visible = Rows.Count > 0 };
        _list.Source = new ListWrapper<string>(new ObservableCollection<string>(Rows.Select(row => row.ToString())));
        _list.RowRender += (_, e) => e.RowAttribute = RowAttribute(e.Row) ?? e.RowAttribute;
        if (Rows.Count > 0)
            _list.SelectedItem = 0;
        Empty = new Label { X = 1, Y = 0, Text = "No messages yet", Enabled = false, Visible = Rows.Count == 0 };
        Add(_list, Empty);

        Commands
            .Register(CancelId, "Close", Cancel)
            .Register(CopyId, "Copy", CopySelected, isEnabled: () => Selected is not null);
        Keys.Bind("Ctrl+C", CopyId);
        CopyButton = CommandButton(ButtonKind.Secondary, CopyId);
        CopyButton.Enabled = Rows.Count > 0;
        CloseButton = CommandButton(ButtonKind.Secondary, CancelId);
        AddButtonRow(ContentRows - 1, CopyButton, CloseButton);
        Initialized += (_, _) => _list.SetFocus();
    }

    // Newest first.
    public IReadOnlyList<StatusMessage> Rows { get; }

    public ListView List => _list;

    public Label Empty { get; }

    public Button CopyButton { get; }

    public Button CloseButton { get; }

    public StatusMessage? Selected => _list.SelectedItem is { } index && index < Rows.Count ? Rows[index] : null;

    // The selected row keeps the list's own highlight, so it shows which row Ctrl+C copies.
    internal Attribute? RowAttribute(int row) =>
        row < Rows.Count && Rows[row].Severity == Severity.Error && !(row == _list.SelectedItem && _list.HasFocus)
            ? Terminal.Gui.Configuration.SchemeManager.GetScheme(SchemeNames.Error).Normal
            : null;

    private void CopySelected()
    {
        if (Selected is not { } row) return;
        var terminal = App?.Clipboard as TerminalClipboard;
        var outcome = terminal?.Write(row.ToString()) ?? VerifiedClipboard.Write(App?.Clipboard, row.ToString());
        if (outcome is CopyOutcome.Failed)
        {
            ShowAlert(outcome.Message, outcome.Severity);
            return;
        }
        terminal?.Announce(outcome);
    }
}
