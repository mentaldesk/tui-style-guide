using System.Collections.ObjectModel;
using MentalDesk.Tui;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Loading;
using MentalDesk.Tui.Shell;

namespace Swatch;

internal sealed class LoadingStatesDialog : AppDialog
{
    private const string ReloadId = "loading.reload";
    private const string EmptyId = "loading.empty";
    private const string FailingId = "loading.failing";

    private static readonly string[] SampleRows =
    [
        "#27 Typing to narrow the command palette",
        "#26 Esc, Space and each action's own key",
        "#24 Hints are the status bar",
        "#21 Show focus on the pane border",
        "#18 One affordance per action",
    ];

    private readonly ListView _list = new() { Width = Dim.Fill(), Height = Dim.Fill() };
    private object? _load;

    public LoadingStatesDialog(AppShell shell, TimeSpan? loadTime = null)
        : base("Loading states", width: 68, contentRows: 9)
    {
        LoadTime = loadTime ?? TimeSpan.FromSeconds(1.5);
        Rows = new LoadStateView(_list, "Loading sample rows…") { Height = 7 };
        Rows.ReloadFailed += (_, message) =>
            shell.ShowMessage($"{message}, so these are the rows from the last load", Severity.Error);
        Add(Rows);

        Commands
            .Register(CancelId, "Close", Cancel)
            .Register(ReloadId, "Reload", () => Load(Outcome.Rows))
            .Register(EmptyId, "Load empty", () => Load(Outcome.Empty))
            .Register(FailingId, "Load failing", () => Load(Outcome.Failure));
        Keys.Bind([Key.R], ReloadId)
            .Bind([Key.E], EmptyId)
            .Bind([Key.F], FailingId);
        AddButtonRow(8, [.. new[] { ReloadId, EmptyId, FailingId, CancelId }.Select(id => CommandButton(ButtonKind.Secondary, id))]);
        Initialized += (_, _) =>
        {
            _list.SetFocus();
            Load(Outcome.Rows);
        };
    }

    private enum Outcome
    {
        Rows,

        Empty,

        Failure,
    }

    public LoadStateView Rows { get; }

    public ListView List => _list;

    public TimeSpan LoadTime { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _load is not null) App?.RemoveTimeout(_load);
        base.Dispose(disposing);
    }

    private void Load(Outcome outcome)
    {
        if (_load is not null) App?.RemoveTimeout(_load);
        Rows.ShowLoading();
        _load = App?.AddTimeout(LoadTime, () =>
        {
            _load = null;
            Finish(outcome);
            return false;
        });
    }

    private void Finish(Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Rows:
                _list.SetSource(new ObservableCollection<string>(SampleRows));
                _list.SelectedItem = 0;
                Rows.ShowContent();
                break;
            case Outcome.Empty:
                _list.SetSource(new ObservableCollection<string>());
                Rows.ShowEmpty("No sample rows", Step(ReloadId, "load sample rows"));
                break;
            case Outcome.Failure:
                Rows.ShowFailed("Couldn't load sample rows: failed on purpose", Step(ReloadId, "retry")!.Value);
                break;
        }
    }

    private (string, Action)? Step(string id, string verb) =>
        new Hint(id, verb).Resolve(Commands, Keys) is (var text, { } run) ? (text, run) : null;
}
