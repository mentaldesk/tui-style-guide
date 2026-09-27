using MentalDesk.Tui.Commands;

namespace MentalDesk.Tui.Keys;

public sealed record KeyBinding(IReadOnlyList<Key> Chord, string CommandId, CommandScope Scope)
{
    public string Display => KeyChord.Display(Chord);
}

public enum KeyConflict
{
    ExactMatch,

    PrefixOfExisting,

    ExtensionOfExisting,
}
