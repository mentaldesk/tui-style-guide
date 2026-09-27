using MentalDesk.Tui.Theming;

namespace MentalDesk.Tui.Chrome;

// Not Terminal.Gui's StatusBar: that draws a border between items and paints in the Menu scheme.
public sealed class AppStatusBar : View
{
    private readonly Label _focus = new() { X = 1, Y = 0 };
    private readonly Label _state = new() { X = Pos.AnchorEnd() - 1, Y = 0 };
    private readonly HintRow _hints;
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
        _hints = new HintRow { X = Pos.Right(_focus), Width = Dim.Fill(2, _state) };
        _message = new Label { X = Pos.Right(_focus), Width = Dim.Fill(2, _state), Visible = false };
        Add(_focus, _hints, _message, _state);
    }

    public string FocusWord { get; private set; } = string.Empty;

    public string? Message => _shown?.Text;

    public string Says => _message.Visible ? _message.Text : _hints.Says;

    public string State
    {
        get => _state.Text;
        set => _state.Text = value;
    }

    public HintRow Hints => _hints;

    public void SetFocusWord(string word)
    {
        FocusWord = word;
        _focus.Text = word.Length == 0 ? string.Empty : $"{word}{HintRow.Separator}";
    }

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
        _hints.Visible = !_message.Visible;
        SetNeedsDraw();
    }
}
