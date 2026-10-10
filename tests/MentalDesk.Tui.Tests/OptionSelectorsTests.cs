using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Tests;

public class OptionSelectorsTests : StaticConfigurationTest
{
    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    [Fact]
    public void Tab_lands_on_the_chosen_option_and_the_arrows_choose_as_they_move()
    {
        using var window = new AppWindow(_host.Shell);
        var name = new TextField { Width = 10 };
        var options = new OptionSelector { Y = 1, Labels = ["One", "Two", "Three"], Value = 1 }.SelectOnArrows();
        window.Add(name, options);
        var changes = new List<int?>();
        options.ValueChanged += (_, e) => changes.Add(e.NewValue);

        _host.Run(window,
            () => name.SetFocus(),
            () => Press(Key.Tab),
            () => options.HasFocus,
            () =>
            {
                Assert.Equal(1, options.Value);
                Assert.True(options.SubViews.OfType<CheckBox>().ElementAt(1).HasFocus);
                Press(Key.CursorDown);
            },
            () => options.Value == 2,
            () => Press(Key.CursorUp, Key.CursorUp),
            () => options.Value == 0);

        Assert.Equal([2, 1, 0], changes);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }
}
