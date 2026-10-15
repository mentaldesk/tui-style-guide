using MentalDesk.Tui.Keys;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class AlternateKeyCodesTests
{
    [Theory]
    [InlineData("\e[17:0:0;2~", KeyCode.F6 | KeyCode.ShiftMask)]
    [InlineData("\e[17:0:0~", KeyCode.F6)]
    [InlineData("\e[3::;5~", KeyCode.Delete | KeyCode.CtrlMask)]
    public void Reads_a_CSI_tilde_key_whose_code_carries_alternate_keys(string sequence, KeyCode expected)
    {
        Assert.Equal(expected, AlternateKeyCodes.Parse(sequence)?.KeyCode);
    }

    [Fact]
    public void Reads_the_release()
    {
        Assert.Equal(KeyEventType.Release, AlternateKeyCodes.Parse("\e[17:0:0;2:3~")?.EventType);
    }

    [Theory]
    [InlineData("\e[17;2~")]
    [InlineData("\e[99:0:0;2~")]
    [InlineData("\e[17:0:0;2u")]
    public void Leaves_other_sequences_alone(string sequence)
    {
        Assert.Null(AlternateKeyCodes.Parse(sequence));
    }
}
