using System.Collections.ObjectModel;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Filtering;
using MentalDesk.Tui.Loading;

namespace MentalDesk.Tui.Dialogs;

public class PickerDialog<T> : AppDialog
    where T : class
{
    public const string PickId = "picker.pick";
    public const string RetryId = "picker.retry";

    private readonly PlaceholderField _filter;
    private readonly ListView _list;
    private readonly Label _noMatches;
    private readonly LoadStateView? _state;
    private readonly Func<T, string> _text;
    private readonly Func<T, string> _display;
    private IReadOnlyList<T> _all;
    private IReadOnlyList<T> _shown;
    private Action? _retry;

    public PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, Func<T, string> text, string verb,
        Func<T, string>? display = null, string? header = null)
        : this(title, width, contentRows, rows, loading: null, text, verb, display, header)
    {
    }

    // Opens before its rows are in: it says `loading` in the list until ShowRows or ShowFailed.
    public PickerDialog(
        string title, int width, int contentRows, string loading, Func<T, string> text, string verb,
        Func<T, string>? display = null, string? header = null)
        : this(title, width, contentRows, [], loading, text, verb, display, header)
    {
    }

    private PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, string? loading, Func<T, string> text,
        string verb, Func<T, string>? display, string? header)
        : base(title, width, contentRows)
    {
        _all = rows;
        _shown = rows;
        _text = text;
        _display = display ?? text;

        var listY = header is null ? 2 : 3;
        _filter = new PlaceholderField { X = 1, Y = 0, Width = Dim.Fill(1), Placeholder = "Type to filter" };
        _filter.TextChanged += (_, _) => Filter();
        _list = new ListView { X = 1, Y = listY, Width = Dim.Fill(1), Height = contentRows - listY - 2, CanFocus = false };
        _noMatches = new Label { X = 1, Y = listY, Text = "(no matches)", Enabled = false, Visible = false };
        Add(_filter);
        if (header is not null)
            Add(new Label { X = 1, Y = 2, Width = Dim.Fill(1), Text = header, Enabled = false });
        if (loading is null)
        {
            Add(_list);
        }
        else
        {
            _state = new LoadStateView(_list, loading) { X = _list.X, Y = _list.Y, Width = _list.Width, Height = _list.Height, CanFocus = false };
            _list.X = 0;
            _list.Y = 0;
            _list.Width = Dim.Fill();
            _list.Height = Dim.Fill();
            Add(_state);
        }
        Add(_noMatches);

        Commands.Register(PickId, verb, Pick);
        Commands.Register("picker.up", "Previous", () => Move(-1));
        Commands.Register("picker.down", "Next", () => Move(+1));
        Commands.Register("picker.pageUp", "Page up", () => Move(-_list.Viewport.Height));
        Commands.Register("picker.pageDown", "Page down", () => Move(+_list.Viewport.Height));
        Keys.Bind("Enter", PickId)
            .Bind("CursorUp", "picker.up")
            .Bind("CursorDown", "picker.down")
            .Bind("PageUp", "picker.pageUp")
            .Bind("PageDown", "picker.pageDown");
        if (_state is not null)
        {
            Commands.Register(RetryId, "Retry", () => _retry?.Invoke(), isEnabled: () => State == LoadState.Failed);
            Keys.Bind([Key.R], RetryId);
        }
        PickButton = CommandButton(ButtonKind.Primary, PickId);
        CancelButton = CommandButton(ButtonKind.Secondary, CancelId);
        AddButtonRow(contentRows - 1, PickButton, CancelButton);
        Filter();
    }

    // Cancel it to keep the picker open, then Choose the row or ShowAlert why not.
    public event EventHandler<PickingEventArgs<T>>? Picking;

    public T? Chosen { get; private set; }

    public IReadOnlyList<T> Shown => _shown;

    public TextField FilterField => _filter;

    public Button PickButton { get; }

    public Button CancelButton { get; }

    public IReadOnlyList<string> Labels => [.. _shown.Select(_display)];

    public bool NoMatches => _noMatches.Visible;

    public LoadState State => _state?.State ?? LoadState.Loaded;

    public int? SelectedIndex
    {
        get => _list.SelectedItem;
        set => _list.SelectedItem = value is { } index && _shown.Count > 0 ? Math.Clamp(index, 0, _shown.Count - 1) : null;
    }

    public void ShowLoading()
    {
        _retry = null;
        RequireState().ShowLoading();
        Filter();
    }

    // Called again as more rows arrive, it keeps the selected row selected.
    public void ShowRows(IReadOnlyList<T> rows, string empty)
    {
        var selected = SelectedIndex is { } index && index < _shown.Count ? _shown[index] : null;
        _all = rows;
        if (rows.Count == 0) RequireState().ShowEmpty(empty);
        else RequireState().ShowContent();
        Filter();
        if (selected is not null && _shown.ToList().IndexOf(selected) is >= 0 and var kept)
            _list.SelectedItem = kept;
    }

    public void ShowFailed(string message, Action retry)
    {
        _retry = retry;
        var step = new Hint(RetryId, "retry").Resolve(Commands, Keys);
        RequireState().ShowFailed(message, (step?.Text ?? "Retry", retry));
        Filter();
    }

    public void Choose(T row)
    {
        Chosen = row;
        Accept();
    }

    private LoadStateView RequireState() =>
        _state ?? throw new InvalidOperationException("Only a picker opened with a loading message loads its rows.");

    private void Filter()
    {
        if (Alert.Lines > 0)
        {
            Alert.Clear();
            SetNeedsLayout();
        }
        var loaded = State == LoadState.Loaded;
        _shown = loaded ? ListFilter.Apply(_filter.Text ?? string.Empty, _all, _text) : [];
        _list.Source = new ListWrapper<string>(new ObservableCollection<string>(_shown.Select(_display)));
        _list.SelectedItem = _shown.Count > 0 ? 0 : null;
        _noMatches.Visible = loaded && _shown.Count == 0;
        PickButton.Enabled = _shown.Count > 0;
    }

    private void Move(int by)
    {
        if (_shown.Count == 0) return;
        _list.SelectedItem = Math.Clamp((_list.SelectedItem ?? 0) + by, 0, _shown.Count - 1);
    }

    private void Pick()
    {
        if (_list.SelectedItem is not { } index || index >= _shown.Count) return;
        var picking = new PickingEventArgs<T>(_shown[index]);
        Picking?.Invoke(this, picking);
        if (!picking.Cancel)
            Choose(picking.Row);
    }
}

public sealed class PickingEventArgs<T>(T row) : EventArgs
{
    public T Row { get; } = row;

    public bool Cancel { get; set; }
}
