using System.Collections.ObjectModel;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Filtering;

namespace MentalDesk.Tui.Dialogs;

public class PickerDialog<T> : AppDialog
    where T : class
{
    public const string PickId = "picker.pick";

    private const string NoMatchesText = "(no matches)";

    private readonly PlaceholderField _filter;
    private readonly ListView _list;
    private readonly Label _note;
    private readonly Func<T, string> _text;
    private readonly Func<T, string> _display;
    private IReadOnlyList<T> _all;
    private IReadOnlyList<T> _shown;
    private string? _noteText;

    public PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, Func<T, string> text, string verb,
        Func<T, string>? display = null)
        : this(title, width, contentRows, rows, text, verb, display, linesAbove: 0, ButtonKind.Primary)
    {
        AddButtonRow(contentRows - 1, PickButton, CancelButton);
    }

    // A subclass puts its own lines above the filter and adds its own button row.
    protected PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, Func<T, string> text, string verb,
        Func<T, string>? display, int linesAbove, ButtonKind pickKind)
        : base(title, width, contentRows)
    {
        _all = rows;
        _shown = rows;
        _text = text;
        _display = display ?? text;

        _filter = new PlaceholderField { X = 1, Y = linesAbove, Width = Dim.Fill(1), Placeholder = "Type to filter" };
        _filter.TextChanged += (_, _) => Filter(select: null);
        _list = new ListView { X = 1, Y = linesAbove + 2, Width = Dim.Fill(1), Height = contentRows - 4 - linesAbove, CanFocus = false };
        _list.RowRender += (_, e) =>
        {
            if (e.Row >= _shown.Count || !IsDim(_shown[e.Row])) return;
            var dim = _list.GetAttributeForRole(VisualRole.Disabled);
            e.RowAttribute = e.Row == _list.SelectedItem
                ? new Attribute(dim.Foreground, (e.RowAttribute ?? _list.GetAttributeForRole(VisualRole.Active)).Background)
                : dim;
        };
        _note = new Label { X = 1, Y = linesAbove + 2, Width = Dim.Fill(1), Text = NoMatchesText, Enabled = false, Visible = false };
        Add(_filter, _list, _note);

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
        PickButton = CommandButton(pickKind, PickId);
        CancelButton = CommandButton(ButtonKind.Secondary, CancelId);
        Filter(select: null);
    }

    public T? Chosen { get; private set; }

    public IReadOnlyList<T> Shown => _shown;

    public TextField FilterField => _filter;

    public Button PickButton { get; }

    public Button CancelButton { get; }

    public IReadOnlyList<string> Labels => [.. _shown.Select(_display)];

    public T? Selected => _list.SelectedItem is { } index && index < _shown.Count ? _shown[index] : null;

    public bool NoMatches => _note.Visible && _noteText is null;

    public string? Note => _note.Visible ? _note.Text : null;

    public void Replace(IReadOnlyList<T> rows, T? select = null)
    {
        _all = rows;
        Filter(select);
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
        Chosen = row;
        Accept();
    }

    private void Filter(T? select)
    {
        _shown = Match(_filter.Text ?? string.Empty, _all);
        _list.Source = new ListWrapper<string>(new ObservableCollection<string>(_shown.Select(_display)));
        var at = select is null ? -1 : _shown.ToList().IndexOf(select);
        _list.SelectedItem = _shown.Count > 0 ? Math.Max(at, 0) : null;
        _note.Text = _noteText ?? NoMatchesText;
        _note.Y = Pos.Top(_list) + _shown.Count;
        _note.Visible = _noteText is not null || _shown.Count == 0;
        PickButton.Enabled = CanPick();
    }

    private void Move(int by)
    {
        if (_shown.Count == 0) return;
        _list.SelectedItem = Math.Clamp((_list.SelectedItem ?? 0) + by, 0, _shown.Count - 1);
    }
}
