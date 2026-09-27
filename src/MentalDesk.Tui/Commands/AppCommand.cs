namespace MentalDesk.Tui.Commands;

public sealed record AppCommand(string Id, string Label, CommandScope Scope);
