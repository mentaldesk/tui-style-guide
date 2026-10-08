using System.Collections.ObjectModel;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Filtering;

namespace MentalDesk.Tui.Dialogs;

public class PickerDialog<T> : AppDialog
    where T : class
{
    public const string PickId = "picker.pick";

    private readonly TextField _filter;
    private readonly ListView _list;
    private readonly Label _noMatches;
    private readonly IReadOnlyList<T> _all;
    private readonly Func<T, string> _text;
    private readonly Func<T, string> _display;
    private readonly string _verb;
    private IReadOnlyList<T> _shown;

    public PickerDialog(
        string title, int width, int contentRows, IReadOnlyList<T> rows, Func<T, string> text, string verb,
        Func<T, string>? display = null)
        : base(title, width, contentRows)
    {
        _all = rows;
        _shown = rows;
        _text = text;
        _display = display ?? text;
        _verb = verb;

        _filter = new TextField { X = 1, Y = 0, Width = Dim.Fill(1) };
        _filter.TextChanged += (_, _) => Filter();
        _list = new ListView { X = 1, Y = 2, Width = Dim.Fill(1), Height = contentRows - 2, CanFocus = false };
        _noMatches = new Label { X = 1, Y = 2, Text = "(no matches)", Enabled = false, Visible = false };
        Add(_filter, _list, _noMatches);

        Commands.Register(PickId, "Pick", Pick);
        Commands.Register("picker.up", "Previous", () => Move(-1));
        Commands.Register("picker.down", "Next", () => Move(+1));
        Commands.Register("picker.pageUp", "Page up", () => Move(-_list.Viewport.Height));
        Commands.Register("picker.pageDown", "Page down", () => Move(+_list.Viewport.Height));
        Keys.Bind("Enter", PickId)
            .Bind("CursorUp", "picker.up")
            .Bind("CursorDown", "picker.down")
            .Bind("PageUp", "picker.pageUp")
            .Bind("PageDown", "picker.pageDown");
        Filter();
    }

    public T? Chosen { get; private set; }

    public IReadOnlyList<T> Shown => _shown;

    public IReadOnlyList<string> Labels => [.. _shown.Select(_display)];

    public bool NoMatches => _noMatches.Visible;

    private void Filter()
    {
        _shown = ListFilter.Apply(_filter.Text ?? string.Empty, _all, _text);
        _list.Source = new ListWrapper<string>(new ObservableCollection<string>(_shown.Select(_display)));
        _list.SelectedItem = _shown.Count > 0 ? 0 : null;
        _noMatches.Visible = _shown.Count == 0;
        if (_shown.Count > 0)
            ShowHints(Hint.Note("Type to filter"), Hint.Note("Up/Down/PgUp/PgDn"), new Hint(PickId, _verb), new Hint(CancelId, "cancel"));
        else
            ShowHints(Hint.Note("Type to filter"), new Hint(CancelId, "cancel"));
    }

    private void Move(int by)
    {
        if (_shown.Count == 0) return;
        _list.SelectedItem = Math.Clamp((_list.SelectedItem ?? 0) + by, 0, _shown.Count - 1);
    }

    private void Pick()
    {
        if (_list.SelectedItem is not { } index || index >= _shown.Count) return;
        Chosen = _shown[index];
        Accept();
    }
}
