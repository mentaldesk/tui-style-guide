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

    private const string NoMatchesText = "(no matches)";

    private readonly PlaceholderField _filter;
    private readonly ListView _list;
    private readonly Label _note;
    private readonly LoadStateView? _state;
    private readonly Func<T, string> _text;
    private readonly Func<T, string> _display;
    private readonly int _listY;
    private IReadOnlyList<T> _all;
    private IReadOnlyList<T> _shown;
    private string? _noteText;
    private Action? _retry;

    public PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, Func<T, string> text, string verb,
        Func<T, string>? display = null, string? header = null)
        : this(title, width, contentRows, rows, loading: null, text, verb, display, header, linesAbove: 0, ButtonKind.Primary)
    {
        AddButtonRow(contentRows - 1, PickButton, CancelButton);
    }

    // Opens before its rows are in: it says `loading` in the list until ShowRows or ShowFailed.
    public PickerDialog(
        string title, int width, int contentRows, string loading, Func<T, string> text, string verb,
        Func<T, string>? display = null, string? header = null)
        : this(title, width, contentRows, [], loading, text, verb, display, header, linesAbove: 0, ButtonKind.Primary)
    {
        AddButtonRow(contentRows - 1, PickButton, CancelButton);
    }

    // A subclass puts its own lines above the filter and adds its own button row.
    protected PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, Func<T, string> text, string verb,
        Func<T, string>? display, int linesAbove, ButtonKind pickKind)
        : this(title, width, contentRows, rows, loading: null, text, verb, display, header: null, linesAbove, pickKind)
    {
    }

    private PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, string? loading, Func<T, string> text,
        string verb, Func<T, string>? display, string? header, int linesAbove, ButtonKind pickKind)
        : base(title, width, contentRows)
    {
        _all = rows;
        _shown = rows;
        _text = text;
        _display = display ?? text;

        _listY = linesAbove + (header is null ? 2 : 3);
        _filter = new PlaceholderField { X = 1, Y = linesAbove, Width = Dim.Fill(1), Placeholder = "Type to filter" };
        _filter.TextChanged += (_, _) => Filter(select: null);
        _list = new ListView { X = 1, Y = _listY, Width = Dim.Fill(1), Height = contentRows - _listY - 2, CanFocus = false };
        _list.RowRender += (_, e) =>
        {
            if (e.Row >= _shown.Count || !IsDim(_shown[e.Row])) return;
            var dim = _list.GetAttributeForRole(VisualRole.Disabled);
            e.RowAttribute = e.Row == _list.SelectedItem
                ? new Attribute(dim.Foreground, (e.RowAttribute ?? _list.GetAttributeForRole(VisualRole.Active)).Background)
                : dim;
        };
        _note = new Label { X = 1, Y = _listY, Width = Dim.Fill(1), Text = NoMatchesText, Enabled = false, Visible = false };
        Add(_filter);
        if (header is not null)
            Add(new Label { X = 1, Y = linesAbove + 2, Width = Dim.Fill(1), Text = header, Enabled = false });
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
        Add(_note);

        Commands.Register(PickId, verb, PickSelected);
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
        PickButton = CommandButton(pickKind, PickId);
        CancelButton = CommandButton(ButtonKind.Secondary, CancelId);
        Filter(select: null);
    }

    // Cancel it to keep the picker open, then Choose the row or ShowAlert why not.
    public event EventHandler<PickingEventArgs<T>>? Picking;

    public T? Chosen { get; private set; }

    public IReadOnlyList<T> Shown => _shown;

    public TextField FilterField => _filter;

    public Button PickButton { get; }

    public Button CancelButton { get; }

    public IReadOnlyList<string> Labels => [.. _shown.Select(_display)];

    public T? Selected => _list.SelectedItem is { } index && index < _shown.Count ? _shown[index] : null;

    public bool NoMatches => _note.Visible && _noteText is null;

    public string? Note => _note.Visible ? _note.Text : null;

    public LoadState State => _state?.State ?? LoadState.Loaded;

    public int? SelectedIndex
    {
        get => _list.SelectedItem;
        set => _list.SelectedItem = value is { } index && _shown.Count > 0 ? Math.Clamp(index, 0, _shown.Count - 1) : null;
    }

    public void Replace(IReadOnlyList<T> rows, T? select = null)
    {
        _all = rows;
        Filter(select);
    }

    public void ShowLoading()
    {
        _retry = null;
        RequireState().ShowLoading();
        Filter(select: null);
    }

    // Called again as more rows arrive, it keeps the selected row selected.
    public void ShowRows(IReadOnlyList<T> rows, string empty)
    {
        var selected = Selected;
        _all = rows;
        if (rows.Count == 0) RequireState().ShowEmpty(empty);
        else RequireState().ShowContent();
        Filter(selected);
    }

    public void ShowFailed(string message, Action retry)
    {
        _retry = retry;
        var step = new Hint(RetryId, "retry").Resolve(Commands, Keys);
        RequireState().ShowFailed(message, (step?.Text ?? "Retry", retry));
        Filter(select: null);
    }

    public void Choose(T row)
    {
        Chosen = row;
        Accept();
    }

    protected void ShowNote(string? text)
    {
        _noteText = text;
        Filter(Selected);
    }

    // Matches again, as when rows arrive: the first row stays selected unless the user moved off it.
    protected void Refresh() => Filter(_list.SelectedItem is > 0 ? Selected : null);

    protected virtual IReadOnlyList<T> Match(string query, IReadOnlyList<T> rows) => ListFilter.Apply(query, rows, _text);

    protected virtual bool IsDim(T row) => false;

    protected virtual bool CanPick() => _shown.Count > 0;

    protected virtual void PickSelected()
    {
        if (Selected is { } row) Pick(row);
    }

    protected void Pick(T row)
    {
        var picking = new PickingEventArgs<T>(row);
        Picking?.Invoke(this, picking);
        if (!picking.Cancel)
            Choose(picking.Row);
    }

    private LoadStateView RequireState() =>
        _state ?? throw new InvalidOperationException("Only a picker opened with a loading message loads its rows.");

    private void Filter(T? select)
    {
        if (Alert.Lines > 0)
        {
            Alert.Clear();
            SetNeedsLayout();
        }
        var loaded = State == LoadState.Loaded;
        _shown = loaded ? Match(_filter.Text ?? string.Empty, _all) : [];
        _list.Source = new ListWrapper<string>(new ObservableCollection<string>(_shown.Select(_display)));
        var at = select is null ? -1 : _shown.ToList().IndexOf(select);
        _list.SelectedItem = _shown.Count > 0 ? Math.Max(at, 0) : null;
        _note.Text = _noteText ?? NoMatchesText;
        _note.Y = _listY + _shown.Count;
        _note.Visible = loaded && (_noteText is not null || _shown.Count == 0);
        PickButton.Enabled = CanPick();
    }

    private void Move(int by)
    {
        if (_shown.Count == 0) return;
        _list.SelectedItem = Math.Clamp((_list.SelectedItem ?? 0) + by, 0, _shown.Count - 1);
    }
}

public sealed class PickingEventArgs<T>(T row) : EventArgs
{
    public T Row { get; } = row;

    public bool Cancel { get; set; }
}
