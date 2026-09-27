using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Tests;

public class KeyChordTests
{
    [Theory]
    [InlineData("Ctrl+G T", "Ctrl+G T")]
    [InlineData("Alt+CursorDown", "Alt+↓")]
    [InlineData("CursorUp", "↑")]
    [InlineData("F10", "F10")]
    public void Displays_the_way_a_keycap_reads(string sequence, string expected)
    {
        Assert.Equal(expected, KeyChord.Display(KeyChord.Parse(sequence)));
    }

    [Fact]
    public void Identity_is_the_raw_keycodes()
    {
        Assert.Equal($"{(uint)Key.G.WithCtrl.KeyCode} {(uint)Key.F10.KeyCode}", KeyChord.Canonical([Key.G.WithCtrl, Key.F10]));
        Assert.NotEqual(KeyChord.Canonical(KeyChord.Parse("Ctrl+G T")), KeyChord.Canonical(KeyChord.Parse("Ctrl+G R")));
    }

    [Fact]
    public void An_unreadable_key_is_refused()
    {
        Assert.Throws<ArgumentException>(() => KeyChord.Parse("Ctrl+Nope"));
    }
}
