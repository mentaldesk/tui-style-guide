using MentalDesk.Tui;
using MentalDesk.Tui.Dialogs;

namespace Swatch;

// Its first load fails on purpose, so all three states can be tried, and a closed issue can't be opened.
internal sealed class IssuePicker : PickerDialog<IssuePicker.Issue>
{
    public const int DialogWidth = 70;

    private static readonly Issue[] SampleIssues =
    [
        new(27, "Typing to narrow the command palette", Open: true),
        new(26, "Esc, Space and each action's own key", Open: true),
        new(24, "Hints are the status bar", Open: false),
        new(21, "Show focus on the pane border", Open: true),
        new(18, "One affordance per action", Open: false),
    ];

    private object? _load;
    private bool _failed;

    public IssuePicker(TimeSpan? loadTime = null)
        : base("Open issue", DialogWidth, contentRows: 14, "Loading issues…", issue => $"#{issue.Number} {issue.Title}", verb: "Open",
            display: issue => issue.Format(), header: Issue.Header)
    {
        LoadTime = loadTime ?? TimeSpan.FromSeconds(1.5);
        Picking += (_, e) =>
        {
            if (e.Row.Open) return;
            e.Cancel = true;
            ShowAlert($"#{e.Row.Number} is closed, so there's nothing to open", Severity.Error);
        };
        Initialized += (_, _) => Load();
    }

    public TimeSpan LoadTime { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _load is not null) App?.RemoveTimeout(_load);
        base.Dispose(disposing);
    }

    private void Load()
    {
        ShowLoading();
        _load = App?.AddTimeout(LoadTime, () =>
        {
            _load = null;
            if (_failed) ShowRows(SampleIssues, "No issues");
            else ShowFailed("Couldn't load issues: failed on purpose", Load);
            _failed = true;
            return false;
        });
    }

    public sealed record Issue(int Number, string Title, bool Open)
    {
        private const int TitleWidth = 50;

        public static string Header => $"{"#",-5}{"Title",-TitleWidth}State";

        public string Format() => $"{$"#{Number}",-5}{Title,-TitleWidth}{(Open ? "open" : "closed")}";
    }
}
