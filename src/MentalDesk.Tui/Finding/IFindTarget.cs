namespace MentalDesk.Tui.Finding;

// What a FindBar searches. Rows and columns are the target's own, as long as reading order sorts them.
public interface IFindTarget
{
    // Where focus goes back to when the bar closes.
    View View { get; }

    bool CanReplace { get; }

    (int Row, int Column) Origin { get; }

    string SelectedText { get; }

    event EventHandler? ContentChanged;

    IReadOnlyList<TextMatch> FindAll(string query);

    void SetHighlights(IReadOnlyList<TextMatch> matches);

    void Select(TextMatch match);

    void ClearSelection();

    void Replace(TextMatch match, string replacement);

    // Puts the bar above the text, or takes it away again with null.
    void Dock(View? bar);
}
