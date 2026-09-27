namespace MentalDesk.Tui.Commands;

public readonly record struct CommandScope(string Name)
{
    public static CommandScope Global { get; } = new("Global");

    public override string ToString() => Name;
}
