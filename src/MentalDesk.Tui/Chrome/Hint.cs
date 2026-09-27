using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Chrome;

public sealed record Hint(string? CommandId, string Verb)
{
    public static Hint Note(string text) => new(null, text);

    public (string Text, Action? Run)? Resolve(CommandRegistry commands, Keymap keys)
    {
        if (CommandId is not { } id)
            return (Verb, null);
        if (keys.For(id).FirstOrDefault() is not { } binding)
            return null;
        return ($"{binding.Display} {Verb}", () => commands.Execute(id));
    }
}
