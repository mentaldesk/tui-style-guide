using System.Text.Json.Nodes;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Tests;

public class KeysPageWithoutShellTests : StaticConfigurationTest
{
    private static readonly CommandScope Pane = new("Pane");

    private readonly CommandRegistry _commands = new();
    private readonly Keymap _keys;
    private readonly InputScopes _scopes = new();
    private readonly List<ConfirmDialog> _asked = [];
    private readonly KeysPage _page;
    private JsonObject? _file;

    public KeysPageWithoutShellTests()
    {
        _commands
            .Register("save", "Save", () => { })
            .Register("open", "Open", () => { })
            .Register("close", "Close", () => { }, Pane);
        _keys = new Keymap(_commands)
            .Bind("Ctrl+S", "save")
            .Bind("Ctrl+O", "open")
            .Bind("Ctrl+W", "close");
        _scopes.Push(_keys);
        var overrides = new KeyOverrides(_commands, _keys, () => _file, written => _file = written);
        _page = new KeysPage(_commands, _scopes, overrides, scope => scope == Pane ? "The pane" : null, _asked.Add);
    }

    private void Record(string filter, params Key[] keys)
    {
        _page.Filter.Text = filter;
        _page.Filter.NewKeyDownEvent(Key.Enter);
        foreach (var key in keys)
            _scopes.Handle(key);
    }

    [Fact]
    public void It_lists_the_commands_it_is_given_naming_a_region_as_the_app_does()
    {
        Assert.Contains(_page.Rows, row => row.Contains("Close", StringComparison.Ordinal) && row.Contains("The pane", StringComparison.Ordinal));
        Assert.Contains(_page.Rows, row => row.Contains("Save", StringComparison.Ordinal) && row.Contains(KeysPage.Everywhere, StringComparison.Ordinal));
    }

    [Fact]
    public void Recording_takes_keys_through_the_scopes_it_is_given_and_gives_them_back_after()
    {
        Record("Save", Key.G.WithCtrl, new Key('s'));

        Assert.True(_page.IsRecording);
        Assert.NotSame(_keys, _scopes.Top);

        _scopes.Handle(Key.Enter);

        Assert.False(_page.IsRecording);
        Assert.Same(_keys, _scopes.Top);
        Assert.Contains(_page.Staged, binding => binding.CommandId == "save" && binding.Display == "Ctrl+G S");
    }

    [Fact]
    public void A_key_another_command_has_is_asked_about_through_the_confirm_it_is_given()
    {
        Record("Save", Key.O.WithCtrl, Key.Enter);

        var asked = Assert.Single(_asked);
        Assert.Equal("Replace key?", asked.Title);
        Assert.DoesNotContain(_page.Staged, binding => binding.CommandId == "save" && binding.Display == "Ctrl+O");
    }
}
