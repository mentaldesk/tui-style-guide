using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Commands;

namespace MentalDesk.Tui.Keys;

public sealed record KeyRow(string Keys, string Label);

public sealed record KeyGroup(string? Heading, IReadOnlyList<KeyRow> Rows);

public sealed record KeyColumn(string Heading, IReadOnlyList<KeyGroup> Groups);

public sealed record KeySheet(KeyColumn? Here, KeyColumn Everywhere)
{
    public const string OtherHeading = "Other";

    public static KeySheet For(
        CommandRegistry commands, Keymap keys, string? regionName, CommandScope scope, IEnumerable<MenuSpec> menu)
    {
        var bindings = keys.Bindings
            .Where(binding => commands.Find(binding.CommandId) is not null && commands.IsEnabled(binding.CommandId))
            .ToList();

        KeyColumn? here = null;
        if (scope != CommandScope.Global && Rows(bindings.Where(binding => binding.Scope == scope)) is { Count: > 0 } local)
            here = new KeyColumn($"Here: {regionName ?? scope.Name}", [new KeyGroup(null, local)]);

        var global = bindings.Where(binding => binding.Scope == CommandScope.Global).ToList();
        var groups = new List<KeyGroup>();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in menu)
        {
            var ids = spec.CommandIds.Where(listed.Add).ToList();
            var rows = Rows(ids.SelectMany(id => global.Where(binding => binding.CommandId == id)));
            if (rows.Count > 0)
                groups.Add(new KeyGroup(spec.Title.Replace("_", string.Empty, StringComparison.Ordinal), rows));
        }
        var other = Rows(global.Where(binding => !listed.Contains(binding.CommandId)));
        if (other.Count > 0)
            groups.Add(new KeyGroup(OtherHeading, other));
        return new KeySheet(here, new KeyColumn("Everywhere", groups));

        List<KeyRow> Rows(IEnumerable<KeyBinding> shown) =>
            [.. shown.Select(binding => new KeyRow(binding.Display, commands.Find(binding.CommandId)!.Label))];
    }
}
