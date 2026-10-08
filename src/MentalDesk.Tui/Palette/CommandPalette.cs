using System.Collections.ObjectModel;
using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Palette;

public sealed class CommandPalette : AppDialog
{
    private const int LabelWidth = 48;

    private readonly TextField _filter;
    private readonly ListView _list;
    private readonly IReadOnlyList<Row> _all;
    private IReadOnlyList<Row> _shown;

    public CommandPalette(CommandRegistry commands, Keymap keys, CommandScope scope)
        : base("Commands", width: 76, contentRows: 18)
    {
        _all = [.. commands.All
            .Where(command => (command.Scope == CommandScope.Global || command.Scope == scope) && commands.IsEnabled(command.Id))
            .Select(command => new Row(command.Id, command.Label, string.Join(", ", keys.For(command.Id, scope).Select(b => b.Display))))
            .OrderBy(row => row.Label, StringComparer.OrdinalIgnoreCase)];
        _shown = _all;

        _filter = new TextField { X = 1, Y = 0, Width = Dim.Fill(1) };
        _filter.TextChanged += (_, _) => Filter();
        _list = new ListView { X = 1, Y = 2, Width = Dim.Fill(1), Height = 16, CanFocus = false };
        Add(_filter, _list);

        Commands.Register("palette.run", "Run", Run);
        Commands.Register("palette.up", "Previous", () => Move(-1));
        Commands.Register("palette.down", "Next", () => Move(+1));
        Commands.Register("palette.pageUp", "Page up", () => Move(-_list.Viewport.Height));
        Commands.Register("palette.pageDown", "Page down", () => Move(+_list.Viewport.Height));
        Keys.Bind("Enter", "palette.run")
            .Bind("CursorUp", "palette.up")
            .Bind("CursorDown", "palette.down")
            .Bind("PageUp", "palette.pageUp")
            .Bind("PageDown", "palette.pageDown");
        ShowHints(Hint.Note("Type to filter"), Hint.Note("Up/Down/PgUp/PgDn"), new Hint("palette.run", "run"), new Hint(CancelId, "cancel"));
        Filter();
    }

    public string? Chosen { get; private set; }

    public IReadOnlyList<string> Labels => [.. _shown.Select(row => row.Label)];

    private void Filter()
    {
        var text = _filter.Text ?? string.Empty;
        _shown = text.Length == 0 ? _all : [.. _all.Where(row => row.Matches(text))];
        _list.Source = new ListWrapper<string>(new ObservableCollection<string>(_shown.Select(row => row.Format())));
        _list.SelectedItem = _shown.Count > 0 ? 0 : null;
    }

    private void Move(int by)
    {
        if (_shown.Count == 0) return;
        _list.SelectedItem = Math.Clamp((_list.SelectedItem ?? 0) + by, 0, _shown.Count - 1);
    }

    private void Run()
    {
        if (_list.SelectedItem is not { } index || index >= _shown.Count) return;
        Chosen = _shown[index].Id;
        Accept();
    }

    private sealed record Row(string Id, string Label, string Keys)
    {
        public bool Matches(string text) =>
            Label.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Keys.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Id.Contains(text, StringComparison.OrdinalIgnoreCase);

        public string Format() =>
            $"{(Label.Length <= LabelWidth ? Label : Label[..(LabelWidth - 1)] + "…"),-50}{Keys}";
    }
}
