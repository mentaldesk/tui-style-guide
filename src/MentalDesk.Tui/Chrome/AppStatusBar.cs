using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Chrome;

// Not Terminal.Gui's StatusBar: that draws a border between items and paints in the Menu scheme.
public sealed class AppStatusBar : View
{
    private readonly Label _focus = new() { X = 1, Y = 0 };
    private readonly Label _state = new() { X = 0, Y = 0 };
    private readonly HintRow _hints;
    private readonly View _right = new() { X = Pos.AnchorEnd() - 1, Y = 0, Width = Dim.Auto(), Height = 1, CanFocus = false };
    private string _stateText = string.Empty;
    private readonly Label _message;
    private string? _chord;
    private (string Text, Severity Severity)? _shown;

    public AppStatusBar()
    {
        Y = Pos.AnchorEnd(1);
        Width = Dim.Fill();
        Height = 1;
        CanFocus = false;
        SchemeName = SchemeNames.StatusBar;
        _hints = new HintRow { X = Pos.Right(_state), Width = Dim.Auto() };
        _right.Add(_state, _hints);
        _message = new Label { X = Pos.Right(_focus), Width = Dim.Fill(2, _right), Visible = false };
        Add(_focus, _message, _right);
    }

    public string FocusWord { get; private set; } = string.Empty;

    public string? Message => _shown?.Text;

    public Severity? MessageSeverity => _shown?.Severity;

    public string Says => _message.Visible ? _message.Text : _hints.Says;

    public string State
    {
        get => _stateText;
        set
        {
            _stateText = value;
            _state.Text = value.Length == 0 ? string.Empty : $"{value}{HintRow.Separator}";
        }
    }

    public HintRow Hints => _hints;

    public void SetFocusWord(string word)
    {
        FocusWord = word;
        ShowFocusWord();
    }

    private void ShowFocusWord() =>
        _focus.Text = FocusWord.Length > 0 && _message.Visible ? $"{FocusWord}{HintRow.Separator}" : FocusWord;

    public void ShowMessage(string text, Severity severity = Severity.Info)
    {
        _shown = (text, severity);
        Refresh();
    }

    public void ClearMessage()
    {
        if (_shown is null) return;
        _shown = null;
        Refresh();
    }

    public void SetChord(string? chord)
    {
        _chord = chord;
        Refresh();
    }

    private void Refresh()
    {
        if (_chord is not null)
        {
            _message.Text = $"{_chord}…";
            _message.SchemeName = null;
        }
        else if (_shown is var (text, severity))
        {
            _message.Text = text;
            _message.SchemeName = severity == Severity.Error ? SchemeNames.Error : null;
        }
        _message.Visible = _chord is not null || _shown is not null;
        ShowFocusWord();
        SetNeedsDraw();
    }
}
