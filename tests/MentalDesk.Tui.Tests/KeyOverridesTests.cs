using System.Text.Json.Nodes;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using Terminal.Gui.Drivers;
using KeyBinding = MentalDesk.Tui.Keys.KeyBinding;

namespace MentalDesk.Tui.Tests;

public class KeyOverridesTests
{
    private static readonly CommandScope Pane = new("Pane");

    private readonly CommandRegistry _commands = new();
    private readonly Keymap _keys;
    private JsonObject? _file;

    public KeyOverridesTests()
    {
        _commands
            .Register("save", "Save", () => { })
            .Register("open", "Open", () => { })
            .Register("find", "Find", () => { })
            .Register("close", "Close", () => { }, Pane);
        _keys = new Keymap(_commands)
            .Bind("Ctrl+S", "save")
            .Bind("Ctrl+O", "open")
            .Bind("Ctrl+F", "find")
            .Bind("Ctrl+G C", "close");
    }

    private KeyOverrides Overrides() => new(_commands, _keys, () => _file, written => _file = written);

    private List<string> KeysOf(string id) => [.. _keys.For(id).Select(binding => $"{binding.Display} {binding.Scope}")];

    [Fact]
    public void A_teams_keys_load_as_they_are_with_an_empty_name_meaning_no_key()
    {
        _file = JsonNode.Parse("""{ "save": "Ctrl+A", "open": "" }""")!.AsObject();

        Overrides().Load();

        Assert.Equal(["Ctrl+A Global"], KeysOf("save"));
        Assert.Empty(KeysOf("open"));
        Assert.Equal(["Ctrl+F Global"], KeysOf("find"));
    }

    [Fact]
    public void A_bad_entry_is_skipped_and_the_rest_still_load()
    {
        _file = JsonNode.Parse("""
            {
              "save": "Ctrl+Nope",
              "open": ["Ctrl+P", 42],
              "find": ["Ctrl+H", "F3"],
              "close": { "key": "Ctrl+W" },
              "gone": "Ctrl+Z"
            }
            """)!.AsObject();

        Overrides().Load();

        Assert.Equal(["Ctrl+S Global"], KeysOf("save"));
        Assert.Equal(["Ctrl+O Global"], KeysOf("open"));
        Assert.Equal(["Ctrl+H Global", "F3 Global"], KeysOf("find"));
        Assert.Equal(["Ctrl+G C Pane"], KeysOf("close"));
    }

    [Fact]
    public void A_command_that_runs_in_one_region_cant_be_given_a_key_elsewhere()
    {
        _file = JsonNode.Parse("""{ "close": [{ "key": "Ctrl+W", "where": "Other" }] }""")!.AsObject();

        Overrides().Load();

        Assert.Equal(["Ctrl+G C Pane"], KeysOf("close"));
    }

    [Fact]
    public void Keep_writes_only_the_commands_that_differ_from_their_defaults_and_binds_them_at_once()
    {
        var overrides = Overrides();
        List<KeyBinding> bindings =
        [
            .. overrides.Defaults.Where(binding => binding.CommandId is not ("save" or "find")),
            new(KeyChord.Parse("Ctrl+T X"), "save", CommandScope.Global),
            new(KeyChord.Parse("Ctrl+F"), "find", CommandScope.Global),
            new(KeyChord.Parse("Enter"), "find", Pane),
        ];
        bindings.RemoveAll(binding => binding.CommandId == "open");

        overrides.Keep(bindings);

        Assert.Equal(
            """{"save":"Ctrl+T x","open":"","find":["Ctrl+F",{"key":"Enter","where":"Pane"}]}""",
            _file!.ToJsonString(Json.Readable));
        Assert.Equal(["Ctrl+T X Global"], KeysOf("save"));
        Assert.Empty(KeysOf("open"));
        Assert.Equal(["Ctrl+F Global", "Enter Pane"], KeysOf("find"));
    }

    [Fact]
    public void Keeping_the_defaults_writes_nothing_and_a_file_read_back_gives_the_same_keys()
    {
        var overrides = Overrides();
        overrides.Keep(overrides.Defaults);
        Assert.Equal("{}", _file!.ToJsonString(Json.Readable));

        List<KeyBinding> changed = [.. overrides.Defaults, new(KeyChord.Parse("Ctrl+G X"), "close", Pane)];
        overrides.Keep(changed);
        var read = KeyOverrides.Read(_file, overrides.Defaults, _commands);

        Assert.True(read.ToHashSet(KeyOverrides.SameKey.Instance).SetEquals(changed));
    }

    public static TheoryData<uint> EveryKey()
    {
        var data = new TheoryData<uint>();
        KeyCode[] modifiers = [0, KeyCode.CtrlMask, KeyCode.AltMask, KeyCode.ShiftMask, KeyCode.CtrlMask | KeyCode.AltMask, KeyCode.CtrlMask | KeyCode.ShiftMask, KeyCode.CtrlMask | KeyCode.AltMask | KeyCode.ShiftMask];
        var keys = Enum.GetValues<KeyCode>()
            .Where(code => (code & KeyCode.SpecialMask) == 0 && code != KeyCode.Null)
            .Concat(Enumerable.Range(0x21, 0x5E).Select(c => (KeyCode)c))
            .Distinct();
        foreach (var key in keys)
            foreach (var modifier in modifiers)
                data.Add((uint)(key | modifier));
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryKey))]
    public void Every_key_comes_back_from_the_file_as_itself(uint code)
    {
        var key = new Key((KeyCode)code);

        Assert.Equal([key], KeyOverrides.Parse(KeyOverrides.Name(key)));
    }

    [Fact]
    public void A_digit_is_its_name_and_a_keycode_is_marked()
    {
        Assert.Equal("1", KeyOverrides.Name(Key.D1));
        Assert.Equal([Key.D1], KeyOverrides.Parse("1"));
        Assert.Equal([new Key((KeyCode)0x1F600)], KeyOverrides.Parse("#128512"));
    }

    [Fact]
    public void Keys_are_written_by_name()
    {
        Assert.Equal("Ctrl+T x", KeyOverrides.Name([Key.T.WithCtrl, Key.X]));
        Assert.Equal("Shift+F6", KeyOverrides.Name([Key.F6.WithShift]));
        Assert.Equal("CursorUp", KeyOverrides.Name([Key.CursorUp]));
    }
}
