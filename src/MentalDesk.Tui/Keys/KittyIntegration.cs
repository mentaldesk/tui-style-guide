using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Keys;

/// <summary>A kitty config file whose mappings apply only while the focused window has the app's user variable set.</summary>
public sealed class KittyIntegration : TerminalIntegration
{
    internal const string VersionMarker = "# MentalDeskKeysVersion:";

    internal KittyIntegration(MacKeys keys, TerminalEnvironment environment) : base(keys, environment)
    {
    }

    public override string Terminal => "kitty";

    // Another terminal started from a kitty shell inherits kitty's variables, but sets its own TERM_PROGRAM.
    public override bool IsHere =>
        IsMacOS && Variable("TERM_PROGRAM") is null or "" or "kitty"
        && (Variable("TERM") == "xterm-kitty" || !string.IsNullOrEmpty(Variable("KITTY_WINDOW_ID")));

    public override string Path => Home(".config", "kitty", $"{Keys.FileName}.conf");

    public override string ConfigLine => $"include {Keys.FileName}.conf";

    public override string ConfigFile => "~/.config/kitty/kitty.conf";

    internal override int Version => 1;

    // `all`, not `normal,application`: the kitty keyboard protocol is a mode of its own.
    internal override string Contents =>
        $"{VersionMarker} {Version}\n" +
        $"# Mac keys for {Keys.App}. Load it from kitty.conf with: {ConfigLine}\n" +
        string.Concat(MacKeyMap.All.Select(key =>
            $"map --when-focus-on var:{Keys.Variable}=1 {key.Kitty} send_text all {MacKeyMap.Escaped(key.Bytes)}\n"));

    internal override int? InstalledVersion(string text) => VersionAfter(VersionMarker, text);
}
