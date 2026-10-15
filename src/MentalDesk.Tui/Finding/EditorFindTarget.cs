using Terminal.Gui.Editor.Document.Search;

namespace MentalDesk.Tui.Finding;

public sealed class EditorFindTarget : IFindTarget
{
    private readonly Terminal.Gui.Editor.Editor _editor;
    private ISearchStrategy? _strategy;
    private View? _bar;
    private Pos _y;
    private Dim _height;

    public EditorFindTarget(Terminal.Gui.Editor.Editor editor)
    {
        _editor = editor;
        (_y, _height) = (editor.Y, editor.Height);
        editor.ContentChanged += (_, _) => ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public View View => _editor;

    public bool CanReplace => !_editor.ReadOnly;

    public (int Row, int Column) Origin => Location(_editor.HasSelection ? _editor.SelectionStart : _editor.CaretOffset);

    public string SelectedText => _editor.SelectedText;

    public event EventHandler? ContentChanged;

    public IReadOnlyList<TextMatch> FindAll(string query)
    {
        if (query.Length == 0 || _editor.Document is not { } document)
        {
            _strategy = null;
            return [];
        }
        _strategy = SearchStrategyFactory.Create(query, ignoreCase: true, matchWholeWords: false, SearchMode.Normal);
        return [.. _strategy.FindAll(document, 0, document.TextLength).Select(hit => Match(hit.Offset, hit.Length))];
    }

    // The editor paints every hit of its search strategy itself.
    public void SetHighlights(IReadOnlyList<TextMatch> matches) => _editor.SearchStrategy = matches.Count > 0 ? _strategy : null;

    public void Select(TextMatch match) => _editor.SelectRange(Offset(match), match.Length);

    public void ClearSelection() => _editor.ClearSelection();

    public void Replace(TextMatch match, string replacement) => _editor.Document!.Replace(Offset(match), match.Length, replacement);

    public void Dock(View? bar)
    {
        if (bar == _bar || _editor.SuperView is not { } pane) return;
        if (_bar is not null)
        {
            pane.Remove(_bar);
            _editor.Y = _y;
            _editor.Height = _height;
        }
        _bar = bar;
        if (bar is null) return;
        (_y, _height) = (_editor.Y, _editor.Height);
        bar.X = _editor.X;
        bar.Y = _editor.Y;
        bar.Width = Dim.Fill();
        pane.Add(bar);
        _editor.Y = Pos.Bottom(bar);
        _editor.Height = Dim.Fill();
    }

    private (int Row, int Column) Location(int offset)
    {
        var line = _editor.Document!.GetLineByOffset(offset);
        return (line.LineNumber - 1, offset - line.Offset);
    }

    private TextMatch Match(int offset, int length)
    {
        var (row, column) = Location(offset);
        return new TextMatch(row, column, length);
    }

    private int Offset(TextMatch match) => _editor.Document!.GetLineByNumber(match.Row + 1).Offset + match.Column;
}
