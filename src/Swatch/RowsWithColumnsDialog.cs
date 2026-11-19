using System.Collections.ObjectModel;
using MentalDesk.Tui;
using MentalDesk.Tui.Columns;
using MentalDesk.Tui.Dialogs;

namespace Swatch;

// Made-up figures; narrowing the terminal shows the name shrinking, then the right-hand columns dropping.
internal sealed class RowsWithColumnsDialog : AppDialog
{
    public const int MaxWidth = 112;

    public static RowLayout<Spend> SpendColumns(Times times) => new(
        Column<Spend>.Name("What", spend => spend.What, minWidth: 14),
        Column<Spend>.Number("Runs", spend => spend.Runs.ToString()),
        Column<Spend>.Number("Minutes", spend => spend.Minutes.ToString()),
        Column<Spend>.Number("Cost", spend => $"${spend.Cost:0.00}"),
        Column<Spend>.Number("Last run", spend => times.Age(spend.LastRun)));

    public static readonly RowLayout<Folder> FolderColumns = new(
        Column<Folder>.Name("Name", folder => folder.Name, minWidth: 8),
        Column<Folder>.Path("Where", folder => folder.Where, minWidth: 16) with { Width = 31 });

    private static Spend[] Spending(DateTimeOffset now) =>
    [
        new("Pitching #42 Narrow terminals", 3, 41, 2.10m, now),
        new("Building #43 Settings screen", 11, 187, 9.85m, now.AddHours(-3)),
        new("Reviewing #44 Empty states", 2, 18, 0.92m, now.AddDays(-2)),
        new("Building #45 Rows with columns", 7, 96, 5.40m, now.AddDays(-10)),
    ];

    private static readonly Folder[] Folders =
    [
        new("tui-style-guide", "~/code/tui-style-guide/main"),
        new("a-team", "~/code/a-team/main"),
        new("TuiCode", "/Volumes/work/code/TuiCode/side-pane-resize"),
    ];

    private readonly ObservableCollection<string> _shown = [];
    private ColumnFit<Folder>? _fit;

    public RowsWithColumnsDialog(Times times)
        : base("Rows with columns", MaxWidth, contentRows: 8)
    {
        FitToScreen(MaxWidth);
        Table = new ColumnTable<Spend>(SpendColumns(times), Spending(times.Clock.GetUtcNow())) { X = 1, Y = 0, Width = Dim.Percent(50), Height = 6 };
        var line = new Line { Orientation = Orientation.Vertical, X = Pos.Right(Table) + 1, Height = 6 };
        Header = new Label { X = Pos.Right(line) + 2, Y = 0, Width = Dim.Fill(1), Enabled = false };
        List = new ListView { X = Pos.Left(Header), Y = 1, Width = Dim.Fill(1), Height = 5 };
        List.SetSource(_shown);
        List.FrameChanged += (_, _) =>
        {
            if (List.Viewport.Width != _fit?.Width) ShowFolders();
        };
        Add(Table, line, Header, List);
        Commands.Register(CancelId, "Close", Cancel);
        AddButtonRow(7, CommandButton(ButtonKind.Secondary, CancelId));
        ShowFolders();
        Initialized += (_, _) => Table.SetFocus();
    }

    public ColumnTable<Spend> Table { get; }

    public Label Header { get; }

    public ListView List { get; }

    public IReadOnlyList<string> Rows => _shown;

    private void ShowFolders()
    {
        _fit = FolderColumns.Fit(Folders, List.Viewport.Width > 0 ? List.Viewport.Width : int.MaxValue);
        Header.Text = _fit.Header ?? string.Empty;
        var selected = List.SelectedItem ?? 0;
        _shown.Clear();
        foreach (var folder in Folders)
            _shown.Add(_fit.Row(folder));
        List.SelectedItem = selected;
    }

    public sealed record Spend(string What, int Runs, int Minutes, decimal Cost, DateTimeOffset LastRun);

    public sealed record Folder(string Name, string Where);
}
