using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Tests;

public class InputScopesTests
{
    [Fact]
    public void Only_the_top_scope_sees_keys()
    {
        var below = new List<Key>();
        var above = new List<Key>();
        var scopes = new InputScopes();
        scopes.Push(new KeyCapture(below.Add));
        var dialog = new KeyCapture(above.Add);
        scopes.Push(dialog);

        scopes.Handle(Key.A);
        scopes.Pop(dialog);
        scopes.Handle(Key.B);

        Assert.Equal([Key.A], above);
        Assert.Equal([Key.B], below);
    }

    [Fact]
    public void Popping_out_of_order_fails()
    {
        var scopes = new InputScopes();
        var first = new KeyCapture(_ => { });
        scopes.Push(first);
        scopes.Push(new KeyCapture(_ => { }));

        Assert.Throws<InvalidOperationException>(() => scopes.Pop(first));
    }

    [Fact]
    public void Reports_the_chord_in_flight_as_it_starts_and_ends()
    {
        var commands = new CommandRegistry().Register("go", "Go", () => { });
        var scopes = new InputScopes();
        scopes.Push(new Keymap(commands).Bind("Ctrl+G T", "go"));
        var seen = new List<string?>();
        scopes.ChordChanged += (_, chord) => seen.Add(chord);

        scopes.Handle(Key.G.WithCtrl);
        scopes.Handle(Key.T);

        Assert.Equal(["Ctrl+G", null], seen);
    }
}
