using MentalDesk.Tui.Icons;

namespace MentalDesk.Tui.Keys;

/// <summary>A WezTerm Lua module with a key table it pushes while the app's user variable is set.</summary>
public sealed class WezTermIntegration : TerminalIntegration
{
    internal const string VersionMarker = "-- MentalDeskKeysVersion:";

    internal WezTermIntegration(MacKeys keys, TerminalEnvironment environment) : base(keys, environment)
    {
    }

    public override string Terminal => "WezTerm";

    public override bool IsHere => IsMacOS && Variable("TERM_PROGRAM") == "WezTerm";

    public override string Path => Home(".config", "wezterm", $"{Keys.FileName}.lua");

    public override string ConfigLine => $"require '{Keys.FileName}'.apply(config)";

    public override string ConfigFile => "~/.config/wezterm/wezterm.lua";

    internal override int Version => 1;

    internal override string Contents
    {
        get
        {
            var table = string.Concat(MacKeyMap.All.Select(key =>
                $"    {{ key = '{key.WezTermKey}', mods = '{key.WezTermMods}', action = wezterm.action.SendString '{MacKeyMap.Escaped(key.Bytes)}' }},\n"));
            // WezTerm answers the kitty keyboard protocol only with enable_kitty_keyboard, and Ctrl chords need it.
            return $$"""
                {{VersionMarker}} {{Version}}
                -- Mac keys for {{Keys.App}}. Load it from wezterm.lua with: {{ConfigLine}}
                local M = {}

                function M.apply(config)
                  local wezterm = require 'wezterm'
                  config.enable_kitty_keyboard = true
                  config.key_tables = config.key_tables or {}
                  config.key_tables.{{Keys.FileName}} = {
                {{table}}  }

                  wezterm.on('user-var-changed', function(window, pane, name, value)
                    if name ~= '{{Keys.Variable}}' then return end
                    if value == '1' then
                      window:perform_action(
                        wezterm.action.ActivateKeyTable { name = '{{Keys.FileName}}', one_shot = false, replace_current = true },
                        pane)
                    else
                      window:perform_action(wezterm.action.PopKeyTable, pane)
                    end
                  end)
                end

                return M

                """;
        }
    }

    internal override int? InstalledVersion(string text) => VersionAfter(VersionMarker, text);
}
