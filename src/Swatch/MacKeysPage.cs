using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Shell;

namespace Swatch;

// Built only from MacKeys, so another app can copy it for its own Settings.
internal sealed class MacKeysPage : SettingsPage
{
    public const string CopyId = "macKeys.copy";

    private const int ValueX = 10;

    private readonly AppShell _shell;
    private readonly MacKeys _keys;
    private readonly TerminalIntegration? _here;
    private readonly Label _note;
    private readonly Label _configLine;
    private readonly View _rows;
    private MacKeysState _state;

    public MacKeysPage(AppShell shell, MacKeys keys) : base("Mac keys")
    {
        _shell = shell;
        _keys = keys;
        _here = keys.Here;
        _state = _here?.State ?? MacKeysState.NotInstalled;

        State = new Label { X = ValueX, Y = 1 };
        TurnedOn = new CheckBox
        {
            Y = 3,
            Text = $"Cmd and Option keys in {keys.App}",
            Enabled = _here is not null,
            Value = _state == MacKeysState.NotInstalled ? CheckState.UnChecked : CheckState.Checked,
        };
        TurnedOn.ValueChanged += (_, _) => OnChanged();
        _note = new Label { Y = 5 };
        _configLine = new Label { X = 2 };
        CopyButton = ActionButton(CopyId, "Copy", Key.C, Copy);
        CopyButton.X = Pos.Right(_configLine) + 4;
        CopyButton.Y = Pos.Top(_configLine);
        var width = MacKeys.Rows.Max(row => row.Keys.Length) + 4;
        _rows = new View { X = 2, Width = Dim.Fill(), Height = MacKeys.Rows.Count };
        _rows.Add([.. MacKeys.Rows.SelectMany((row, i) => new View[]
        {
            new Label { Y = i, Text = row.Keys },
            Dimmed(new Label { X = width, Y = i, Text = row.Does }),
        })]);

        Add(new Label { Text = "Terminal" }, new Label { X = ValueX, Text = keys.Terminal },
            new Label { Y = 1, Text = "State" }, State, TurnedOn, _note, _configLine, CopyButton, _rows);
        Show();
    }

    public Label State { get; }

    public CheckBox TurnedOn { get; }

    public Button CopyButton { get; }

    public string Note => _note.Text;

    public override bool IsChanged => _here is not null && (Ticked ? _state != MacKeysState.Installed : _state != MacKeysState.NotInstalled);

    private bool Ticked => TurnedOn.Value == CheckState.Checked;

    public override void Keep()
    {
        if (_here is null) return;
        if (Ticked)
            _here.Install();
        else
            _here.Remove();
        _state = _here.State;
        Show();
    }

    public override void Revert() =>
        TurnedOn.Value = _state == MacKeysState.NotInstalled ? CheckState.UnChecked : CheckState.Checked;

    private void Show()
    {
        State.Text = _here is null ? "Can't be installed here" : _state switch
        {
            MacKeysState.Installed => "Installed",
            MacKeysState.OutOfDate => "Out of date · saving updates it",
            _ => "Not installed",
        };
        var configLine = _state != MacKeysState.NotInstalled ? _here?.ConfigLine : null;
        _note.Text = _here is null ? Unsupported()
            : configLine is not null ? $"Add this line to {_here.ConfigFile}, then reopen {_keys.App}:"
            : $"Only {_keys.App} is affected; your shell and other apps are unchanged.";
        _configLine.Text = configLine ?? string.Empty;
        _configLine.Y = Pos.Bottom(_note);
        _configLine.Visible = CopyButton.Visible = configLine is not null;
        _rows.Y = Pos.Bottom(configLine is null ? _note : _configLine) + 1;
    }

    private string Unsupported() => _keys.OnAMac
        ? $"""
          {_keys.Terminal} can't change keys for one app only. These work without it:
          Option+←/→ moves a word, fn+←/→ goes to the start and end of the line.
          iTerm2, kitty and WezTerm can do the rest.
          """
        : """
          Cmd and Option keys come from a Mac's keyboard. In iTerm2, kitty or
          WezTerm on a Mac, this page turns them on.
          """;

    private void Copy()
    {
        if (_here?.ConfigLine is not { } line || !CopyButton.Visible) return;
        var outcome = _shell.Copy(line);
        if (SuperView?.SuperView is AppDialog dialog)
            dialog.ShowAlert(outcome.Message, outcome.Severity);
    }
}
