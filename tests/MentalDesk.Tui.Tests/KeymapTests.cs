using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Tests;

public class KeymapTests
{
    private static readonly CommandScope Editor = new("Editor");

    private readonly CommandRegistry _commands = new();
    private readonly List<string> _ran = [];
    private readonly Keymap _keys;

    public KeymapTests()
    {
        _keys = new Keymap(_commands);
    }

    private void Register(string id, CommandScope? scope = null, Func<bool>? isEnabled = null) =>
        _commands.Register(id, id, () => _ran.Add(id), scope, isEnabled);

    [Fact]
    public void A_single_key_runs_its_command()
    {
        Register("save");
        _keys.Bind("Ctrl+S", "save");

        Assert.Equal(KeyResult.Consumed, _keys.Handle(Key.S.WithCtrl));
        Assert.Equal(["save"], _ran);
    }

    [Fact]
    public void A_chord_waits_for_its_last_key_and_says_so()
    {
        Register("go.themes");
        _keys.Bind("Ctrl+G T", "go.themes");

        Assert.Equal(KeyResult.ChordInProgress, _keys.Handle(Key.G.WithCtrl));
        Assert.Equal("Ctrl+G", _keys.PendingChord);
        Assert.Empty(_ran);

        Assert.Equal(KeyResult.Consumed, _keys.Handle(new Key('t')));
        Assert.Null(_keys.PendingChord);
        Assert.Equal(["go.themes"], _ran);
    }

    [Fact]
    public void Esc_cancels_a_chord_and_a_stray_key_abandons_it_without_passing_through()
    {
        Register("go.themes");
        _keys.Bind("Ctrl+G T", "go.themes");

        _keys.Handle(Key.G.WithCtrl);
        Assert.Equal(KeyResult.Consumed, _keys.Handle(Key.Esc));
        _keys.Handle(Key.G.WithCtrl);
        Assert.Equal(KeyResult.Consumed, _keys.Handle(new Key('x')));

        Assert.Null(_keys.PendingChord);
        Assert.Empty(_ran);
    }

    [Fact]
    public void A_chord_step_matches_either_case()
    {
        Register("go.themes");
        _keys.Bind("Ctrl+G T", "go.themes");

        _keys.Handle(Key.G.WithCtrl);
        _keys.Handle(new Key('T'));

        Assert.Equal(["go.themes"], _ran);
    }

    [Fact]
    public void The_focused_scope_is_tried_before_Global()
    {
        Register("global.enter");
        Register("editor.enter", Editor);
        _keys.Bind("Enter", "global.enter").Bind("Enter", "editor.enter");

        _keys.FocusedScope = () => Editor;
        _keys.Handle(Key.Enter);
        _keys.FocusedScope = () => CommandScope.Global;
        _keys.Handle(Key.Enter);

        Assert.Equal(["editor.enter", "global.enter"], _ran);
    }

    [Fact]
    public void A_disabled_command_lets_its_key_fall_through_to_Global()
    {
        Register("global.esc");
        Register("editor.esc", Editor, isEnabled: () => false);
        _keys.Bind("Esc", "global.esc").Bind("Esc", "editor.esc");
        _keys.FocusedScope = () => Editor;

        _keys.Handle(Key.Esc);

        Assert.Equal(["global.esc"], _ran);
    }

    [Fact]
    public void A_key_nothing_is_bound_to_passes()
    {
        Assert.Equal(KeyResult.Pass, _keys.Handle(new Key('a')));
    }

    [Theory]
    [InlineData("Ctrl+S", KeyConflict.ExactMatch)]
    [InlineData("Ctrl+G", KeyConflict.PrefixOfExisting)]
    [InlineData("Ctrl+S X", KeyConflict.ExtensionOfExisting)]
    [InlineData("Ctrl+H", null)]
    public void Conflicts_are_reported_before_binding(string chord, KeyConflict? expected)
    {
        Register("save");
        Register("go.themes");
        _keys.Bind("Ctrl+S", "save").Bind("Ctrl+G T", "go.themes");

        Assert.Equal(expected, _keys.CheckConflict(KeyChord.Parse(chord), CommandScope.Global));
    }

    [Fact]
    public void Unbinding_the_only_chord_under_a_prefix_frees_the_prefix()
    {
        Register("go.themes");
        _keys.Bind("Ctrl+G T", "go.themes");

        Assert.True(_keys.Unbind(KeyChord.Parse("Ctrl+G T"), CommandScope.Global));

        Assert.Null(_keys.CheckConflict(KeyChord.Parse("Ctrl+G"), CommandScope.Global));
        Assert.Empty(_keys.Bindings);
    }

    [Fact]
    public void A_commands_shortest_binding_comes_first()
    {
        Register("palette");
        _keys.Bind("Ctrl+K P", "palette").Bind("Ctrl+E", "palette");

        Assert.Equal(["Ctrl+E", "Ctrl+K P"], _keys.For("palette").Select(binding => binding.Display));
    }

    [Fact]
    public void A_region_s_command_is_bound_in_that_region_and_nowhere_else()
    {
        Register("editor.move", Editor);

        _keys.Bind("Ctrl+K U", "editor.move");

        Assert.Equal(Editor, _keys.For("editor.move").Single().Scope);
        Assert.Throws<ArgumentException>(() => _keys.Bind("Ctrl+K D", "editor.move", CommandScope.Global));
        Assert.Throws<ArgumentException>(() => _keys.Bind("Ctrl+K D", "editor.move", new CommandScope("Files")));
    }

    [Fact]
    public void A_key_bound_in_a_narrower_scope_runs_the_command_only_there()
    {
        Register("try");
        _keys.Bind("Ctrl+T T", "try").Bind("Enter", "try", Editor);

        Assert.Equal(KeyResult.Pass, _keys.Handle(Key.Enter));
        _keys.FocusedScope = () => Editor;
        Assert.Equal(KeyResult.Consumed, _keys.Handle(Key.Enter));
        Assert.Equal(["try"], _ran);
    }

    [Fact]
    public void A_scopes_own_bindings_come_first_and_other_scopes_are_left_out()
    {
        Register("try");
        _keys.Bind("Ctrl+T T", "try").Bind("Enter", "try", Editor);

        Assert.Equal(["Enter", "Ctrl+T T"], _keys.For("try", Editor).Select(binding => binding.Display));
        Assert.Equal(["Ctrl+T T"], _keys.For("try", CommandScope.Global).Select(binding => binding.Display));
        Assert.Equal(["Ctrl+T T", "Enter"], _keys.For("try").Select(binding => binding.Display));
    }

    [Fact]
    public void A_chord_the_focused_scope_shares_a_prefix_with_still_reaches_the_global_one()
    {
        Register("try");
        Register("use", Editor);
        _keys.Bind("Ctrl+T T", "try").Bind("Ctrl+T U", "use");
        _keys.FocusedScope = () => Editor;

        Assert.Equal(KeyResult.ChordInProgress, _keys.Handle(Key.T.WithCtrl));
        Assert.Equal(KeyResult.Consumed, _keys.Handle(Key.T));
        Assert.Equal(KeyResult.ChordInProgress, _keys.Handle(Key.T.WithCtrl));
        Assert.Equal(KeyResult.Consumed, _keys.Handle(Key.U));
        Assert.Equal(["try", "use"], _ran);
    }
}
