using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Tests;

internal static class FakeTerminal
{
    public static readonly (string, string) Iterm2 = ("TERM_PROGRAM", "iTerm.app");
    public static readonly (string, string) Kitty = ("TERM", "xterm-kitty");
    public static readonly (string, string) WezTerm = ("TERM_PROGRAM", "WezTerm");
    public static readonly (string, string) AppleTerminal = ("TERM_PROGRAM", "Apple_Terminal");

    // A Mac when any variable is given; otherwise not, so no test reaches a real terminal's integration.
    public static TerminalEnvironment With(string home, params (string Name, string Value)[] variables) => new()
    {
        Variable = name => variables.FirstOrDefault(v => v.Name == name).Value,
        Folder = folder => folder == Environment.SpecialFolder.UserProfile ? home : throw new ArgumentException(folder.ToString()),
        ReadFile = _ => null,
        PlistToXml = _ => null,
        IsMacOS = variables.Length > 0,
    };
}
