namespace MentalDesk.Tui.Shell;

public sealed record AppScreen(string Name, char Letter)
{
    public string CommandId => $"app.screen.{Name}";

    public string Chord => $"Ctrl+G {char.ToUpperInvariant(Letter)}";
}
