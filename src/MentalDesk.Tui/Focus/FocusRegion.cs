using MentalDesk.Tui.Commands;

namespace MentalDesk.Tui.Focus;

public sealed record FocusRegion(string Name, CommandScope Scope);
