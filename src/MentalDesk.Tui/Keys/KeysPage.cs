using System.Collections.ObjectModel;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Filtering;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Keys;

public sealed class KeysPage : SettingsPage
{
    public const string ResetId = "keys.reset";
    public const string Everywhere = "Everywhere";
    public const string Recording = "Press keys…";
    public const string BrowsingHint = "Enter adds a key · Del removes it";
    public const string RecordingHint = "Enter takes the keys · Esc stops recording";

    public static readonly ConfirmAction ReplaceAction = new("Replace", ButtonKind.Primary, CtrlEnter.Key);
    public static readonly ConfirmAction UseHereAction = new("Use it", ButtonKind.Primary, CtrlEnter.Key);
    public static readonly ConfirmAction ResetAction = new("Reset", ButtonKind.Primary, CtrlEnter.Key);

    private const string Gap = "  ";
    private const int RecordingWidth = 20;

    private static readonly (Key Key, string Does)[] Fixed =
    [
        (Key.Esc, "cancels"),
        (Key.Enter, "takes the keys"),
        (CtrlEnter.Key, "confirms"),
        (Key.Tab, "moves focus"),
        (Key.Tab.WithShift, "moves focus"),
        (Key.F1, "opens Help"),
        (Key.F6, "moves between panes"),
        (Key.F6.WithShift, "moves between panes"),
    ];

    private readonly AppShell _shell;
    private readonly KeyOverrides _overrides;
    private readonly PlaceholderField _filter;
    private readonly Label _header;
    private readonly ListView _list;
    private readonly Label _noMatches;
    private readonly Label _hint;
    private readonly ObservableCollection<string> _shown = [];
    private readonly KeyCapture _capture;
    private List<KeyBinding> _kept;
    private List<KeyBinding> _staged;
    private List<Row> _all = [];
    private IReadOnlyList<Row> _rows = [];
    private (int Command, int Keys) _widths;
    private Row? _recording;
    private readonly List<Key> _typed = [];
    private (Row Row, string Text)? _note;
    private IRunnable? _host;

    public KeysPage(AppShell shell, KeyOverrides overrides) : base("Keys")
    {
        _shell = shell;
        _overrides = overrides;
        _kept = [.. overrides.Current];
        _staged = [.. _kept];
        _capture = new KeyCapture(Record);

        _filter = new PlaceholderField { Width = Dim.Fill(), Placeholder = "Type to filter" };
        _filter.TextChanged += (_, _) => Show(selectFirst: true);
        _filter.KeyDown += OnFilterKey;
        _header = Dimmed(new Label { Y = 1 });
        _list = new ListView { Y = 2, Width = Dim.Fill(), Height = Dim.Fill(3), CanFocus = false };
        _list.SetSource(_shown);
        _noMatches = new Label { Y = 2, Text = "(no matches)", Enabled = false, Visible = false };
        _hint = Dimmed(new Label { Y = Pos.AnchorEnd(2), Text = BrowsingHint });
        ResetButton = ActionButton(ResetId, "Reset to defaults…", Key.R.WithCtrl, ConfirmReset);
        ResetButton.X = Pos.AnchorEnd();
        ResetButton.Y = Pos.AnchorEnd(1);
        Add(_filter, _header, _list, _noMatches, _hint, ResetButton);
        Show(selectFirst: true);
    }

    public TextField Filter => _filter;

    public ListView List => _list;

    public Button ResetButton { get; }

    public string Header => _header.Text;

    public string Hint => _hint.Text;

    public bool IsRecording => _recording is not null;

    public bool NoMatches => _noMatches.Visible;

    public IReadOnlyList<string> Rows => _shown;

    public IReadOnlyList<KeyBinding> Staged => _staged;

    public override bool IsChanged => !_staged.ToHashSet(KeyOverrides.SameKey.Instance).SetEquals(_kept);

    public static ConfirmDialog ResetConfirm() =>
        new("Reset to defaults", ["Put every key back to its default?", "Esc on Settings still undoes it."], [ResetAction]);

    public static ConfirmDialog ReplaceConfirm(string chord, string other, string where, string command) =>
        new("Replace key?", [$"{chord} already runs {other} {In(where)}.", $"Give it to {command} instead?"], [ReplaceAction]);

    public static ConfirmDialog HideConfirm(string chord, string global, string local, string where, bool addingLocal) =>
        new("Hide a key?", addingLocal
            ? [$"{chord} runs {global} everywhere.", $"In {where} it would run {local} instead."]
            : [$"{chord} runs {local} in {where}.", $"Everywhere else it would run {global}."], [UseHereAction]);

    public override void Keep()
    {
        StopRecording();
        _overrides.Keep(_staged);
        _kept = [.. _staged];
        Show();
    }

    public override void Revert()
    {
        StopRecording();
        _staged = [.. _kept];
        Show();
        OnChanged();
    }

    private static string In(string where) => where == Everywhere ? "everywhere" : $"in {where}";

    private static string? FixedReason(Key key) =>
        Fixed.FirstOrDefault(entry => entry.Key == key).Does is { } does ? $"{KeyChord.Display(key)} is reserved: it {does}" : null;

    private string LabelOf(string id) => _shell.Commands.Find(id)!.Label;

    private string Where(CommandScope scope) =>
        scope == CommandScope.Global ? Everywhere
            : _shell.Focus.Regions.FirstOrDefault(region => region.Scope == scope)?.Name ?? scope.Name;

    private Row? Selected => _list.SelectedItem is { } index && index < _rows.Count ? _rows[index] : null;

    private void OnFilterKey(object? sender, Key key)
    {
        if (key == Key.Enter)
            StartRecording();
        else if (key == Key.Delete && _filter.InsertionPoint >= _filter.Text.Length)
            Remove();
        else if (key == Key.CursorUp) Move(-1);
        else if (key == Key.CursorDown) Move(+1);
        else if (key == Key.PageUp) Move(-_list.Viewport.Height);
        else if (key == Key.PageDown) Move(+_list.Viewport.Height);
        else return;
        key.Handled = true;
    }

    private void Move(int by)
    {
        if (_rows.Count == 0) return;
        _note = null;
        _list.SelectedItem = Math.Clamp((_list.SelectedItem ?? 0) + by, 0, _rows.Count - 1);
        Show();
    }

    private void StartRecording()
    {
        if (Selected is not { } row) return;
        _recording = row;
        _typed.Clear();
        _note = null;
        _shell.Scopes.Push(_capture);
        _host = Ancestors().OfType<IRunnable>().FirstOrDefault();
        if (_host is not null) _host.IsRunningChanging += OnHostClosing;
        _hint.Text = RecordingHint;
        Show();
    }

    private void StopRecording()
    {
        if (_recording is null) return;
        _recording = null;
        _typed.Clear();
        _shell.Scopes.Pop(_capture);
        if (_host is not null) _host.IsRunningChanging -= OnHostClosing;
        _host = null;
        _hint.Text = BrowsingHint;
        Show();
    }

    private void OnHostClosing(object? sender, EventArgs e) => StopRecording();

    private IEnumerable<View> Ancestors()
    {
        for (var view = SuperView; view is not null; view = view.SuperView)
            yield return view;
    }

    private void Record(Key key)
    {
        if (_recording is not { } row) return;
        _note = null;
        if (key == Key.Esc)
        {
            StopRecording();
            return;
        }
        if (key == Key.Enter && _typed.Count > 0)
        {
            Take(row, [.. _typed]);
            return;
        }
        if (key.NoShift.NoCtrl.NoAlt == Key.Empty) return;
        if (FixedReason(key) is { } reason)
            _note = (row, reason);
        else
            _typed.Add(Keymap.Normalize(key));
        Show();
    }

    private void Take(Row row, IReadOnlyList<Key> chord)
    {
        var command = row.Command;
        var scope = row.Binding?.Scope ?? command.Scope;
        var adding = new KeyBinding(chord, command.Id, scope);
        var display = KeyChord.Display(chord);
        var canonical = KeyChord.Canonical(chord);
        var here = _staged.Where(binding => binding.Scope == scope).ToList();

        if (here.Any(binding => KeyOverrides.SameKey.Instance.Equals(binding, adding)))
        {
            StopRecording();
            return;
        }
        if (here.FirstOrDefault(binding => StartsWith(binding.Chord, chord) && binding.Chord.Count > chord.Count) is { } longer)
        {
            _note = (row, $"{display} starts {longer.Display}, which runs {LabelOf(longer.CommandId)}");
            Show();
            return;
        }
        if (here.FirstOrDefault(binding => StartsWith(chord, binding.Chord) && chord.Count > binding.Chord.Count) is { } shorter)
        {
            _typed.Clear();
            _note = (row, $"{display} goes on from {shorter.Display}, which runs {LabelOf(shorter.CommandId)}");
            Show();
            return;
        }

        StopRecording();
        var replaced = here.FirstOrDefault(binding => KeyChord.Canonical(Keymap.Normalize(binding.Chord)) == canonical);
        if (replaced is not null)
        {
            using var confirm = ReplaceConfirm(display, LabelOf(replaced.CommandId), Where(scope), command.Label);
            confirm.Run(_shell);
            if (confirm.Chosen is null) return;
        }
        else if (Hidden(adding) is { } hidden)
        {
            var addingLocal = scope != CommandScope.Global;
            var (global, local) = addingLocal ? (hidden, adding) : (adding, hidden);
            using var confirm = HideConfirm(display, LabelOf(global.CommandId), LabelOf(local.CommandId), Where(local.Scope), addingLocal);
            confirm.Run(_shell);
            if (confirm.Chosen is null) return;
        }

        if (replaced is not null) _staged.Remove(replaced);
        _staged.Add(adding);
        Show(select: adding);
        OnChanged();
    }

    // A region's key wins over the same key everywhere else while that region has focus.
    private KeyBinding? Hidden(KeyBinding adding)
    {
        var canonical = KeyChord.Canonical(Keymap.Normalize(adding.Chord));
        return _staged.FirstOrDefault(binding =>
            binding.CommandId != adding.CommandId
            && (adding.Scope == CommandScope.Global) != (binding.Scope == CommandScope.Global)
            && KeyChord.Canonical(Keymap.Normalize(binding.Chord)) == canonical);
    }

    private static bool StartsWith(IReadOnlyList<Key> chord, IReadOnlyList<Key> start) =>
        start.Count <= chord.Count
        && Keymap.Normalize(chord).Take(start.Count).SequenceEqual(Keymap.Normalize(start));

    private void Remove()
    {
        if (Selected is not { Binding: { } binding } row) return;
        if (binding.Chord is [var only] && FixedReason(only) is { } reason)
        {
            _note = (row, reason);
            Show();
            return;
        }
        _staged.Remove(binding);
        _note = null;
        Show();
        OnChanged();
    }

    private void ConfirmReset()
    {
        StopRecording();
        using var confirm = ResetConfirm();
        confirm.Run(_shell);
        if (confirm.Chosen is null) return;
        _staged = [.. _overrides.Defaults];
        Show(selectFirst: true);
        OnChanged();
    }

    private void Show(bool selectFirst = false, KeyBinding? select = null)
    {
        var keep = select is null ? Selected : null;
        _all = [.. Build()];
        _widths = (_all.Max(row => row.Command.Label.Length), _all.Select(row => row.Keys.Length).Append(RecordingWidth).Max());
        _header.Text = Format("Command", "Keys", "Where");
        _rows = ListFilter.Apply(_filter.Text, _all, row => $"{row.Command.Label} {row.Keys} {row.Where}");
        var index = selectFirst ? 0
            : select is not null ? Find(row => row.Binding is { } binding && KeyOverrides.SameKey.Instance.Equals(binding, select))
            : keep is null ? 0
            : Math.Max(0, Find(row => row.Command == keep.Command && (keep.Binding is null || row.Binding is null
                ? row.Binding == keep.Binding
                : KeyOverrides.SameKey.Instance.Equals(row.Binding, keep.Binding))));
        if (_recording is { } recording)
            _recording = _rows.FirstOrDefault(row => row.Command == recording.Command && row.Binding == recording.Binding) ?? recording;

        var texts = _rows.Select(RowText).ToList();
        if (!texts.SequenceEqual(_shown))
        {
            _shown.Clear();
            foreach (var text in texts)
                _shown.Add(text);
        }
        _list.SelectedItem = _rows.Count > 0 ? Math.Clamp(index < 0 ? 0 : index, 0, _rows.Count - 1) : null;
        _noMatches.Visible = _rows.Count == 0;

        int Find(Func<Row, bool> match) => _rows.ToList().FindIndex(row => match(row));
    }

    private IEnumerable<Row> Build()
    {
        foreach (var command in _shell.Commands.All.OrderBy(command => command.Label, StringComparer.CurrentCultureIgnoreCase))
        {
            var mine = _staged.Where(binding => binding.CommandId == command.Id)
                .OrderBy(binding => binding.Scope != CommandScope.Global).ThenBy(binding => binding.Chord.Count).ToList();
            var defaults = _overrides.Defaults.Where(binding => binding.CommandId == command.Id).ToList();
            var changed = !mine.ToHashSet(KeyOverrides.SameKey.Instance).SetEquals(defaults);
            var note = changed ? DefaultNote(defaults.Count == 0 ? "none" : string.Join(", ", defaults.Select(binding => binding.Display))) : null;
            if (mine.Count == 0)
            {
                yield return new Row(command, null, string.Empty, Where(command.Scope), note);
                continue;
            }
            for (var i = 0; i < mine.Count; i++)
                yield return new Row(command, mine[i], mine[i].Display, Where(mine[i].Scope), i == 0 ? note : null);
        }
    }

    private string RowText(Row row)
    {
        if (_recording is { } recording && row.Command == recording.Command && row.Binding == recording.Binding)
        {
            var typed = _typed.Count > 0 ? $"{Recording} {KeyChord.Display(_typed)}" : Recording;
            return Format(row.Command.Label, typed, row.Where) + NoteFor(row);
        }
        return Format(row.Command.Label, row.Keys, row.Where) + (row.Note is null ? string.Empty : $"{Gap}{row.Note}") + NoteFor(row);
    }

    private string NoteFor(Row row) => _note is { } note && note.Row.Command == row.Command && note.Row.Binding == row.Binding ? $"{Gap}{note.Text}" : string.Empty;

    private string Format(string command, string keys, string where) =>
        $"{command.PadRight(_widths.Command)}{Gap}{keys.PadRight(Math.Max(_widths.Keys, keys.Length))}{Gap}{where}";

    private sealed record Row(AppCommand Command, KeyBinding? Binding, string Keys, string Where, string? Note);
}
