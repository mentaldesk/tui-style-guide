using MentalDesk.Tui.Columns;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Palette;

public sealed class CommandPalette(CommandRegistry commands, Keymap keys, CommandScope scope)
    : PickerDialog<CommandPalette.Row>("Commands", width: 76, contentRows: 18, Rows(commands, keys, scope),
        row => $"{row.Label} {row.Keys}", verb: "Run", Row.Columns, showHeader: false)
{
    private static Row[] Rows(CommandRegistry commands, Keymap keys, CommandScope scope) =>
        [.. commands.All
            .Where(command => commands.IsAvailable(command.Id, scope))
            .Select(command => new Row(command.Id, command.Label, string.Join(", ", keys.For(command.Id, scope).Select(b => b.Display))))
            .OrderBy(row => row.Label, StringComparer.OrdinalIgnoreCase)];

    public sealed record Row(string Id, string Label, string Keys)
    {
        public static readonly RowLayout<Row> Columns = new(
            Column<Row>.Name("Command", row => row.Label) with { Width = 48 },
            Column<Row>.Text("Keys", row => row.Keys));
    }
}
