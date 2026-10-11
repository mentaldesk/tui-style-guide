using MentalDesk.Tui.Diagnostics;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Palette;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class IconsDialogTests : StaticConfigurationTest
{
    private readonly Host _host = new(detected: new FontDetection(false, "couldn't tell which font the terminal uses"));

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private IconSettings Icons => _host.Shell.Icons;

    private IconsDialog? Dialog => _host.App.TopRunnableView as IconsDialog;

    private IconStyle Kept => new MentalDeskSettings(_host.SettingsPath).IconStyle;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, [
            () => { _host.Shell.Commands.Execute(SwatchCommands.ShowIcons); },
            () => Dialog?.IsInitialized == true && Dialog.Style.HasFocus,
            .. steps,
        ]);
    }

    [Fact]
    public void It_opens_from_the_palette()
    {
        using var window = new SwatchWindow(_host.Shell);

        _host.Run(window,
            () => Press(Key.E.WithCtrl),
            () => _host.App.TopRunnableView is CommandPalette,
            () => Press([.. "icons".Select(c => new Key(c))]),
            () => ((CommandPalette)_host.App.TopRunnableView!).Shown.Select(row => row.Label).SequenceEqual(["Icons…"]),
            () => Press(Key.Enter),
            () => Dialog is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);
    }

    [Fact]
    public void It_shows_the_saved_style_and_why_auto_chose_what_it_did_with_buttons_and_no_hint_row()
    {
        Run(() =>
        {
            Assert.Equal(0, Dialog!.Style.Value);
            Assert.Contains(Dialog.SubViews, view => view.Text == "Auto chose Plain: couldn't tell which font the terminal uses");
            Assert.Equal(" Ctrl+Enter Keep ", Dialog.KeepButton.Text);
            Assert.Equal(" Esc Cancel ", Dialog.CancelButton.Text);
            Assert.Empty(Dialog.SubViews.OfType<MentalDesk.Tui.Chrome.HintRow>());
            Press(Key.Esc);
        });
    }

    [Fact]
    public void An_arrow_picks_the_next_style_and_previews_it_at_once()
    {
        Run(
            () => Press(Key.CursorRight),
            () => Icons.InUse == IconStyle.NerdFont && Dialog!.Style.Value == 1,
            () => Press(Key.CursorLeft),
            () => Icons.Style == IconStyle.Auto && Dialog!.Style.Value == 0,
            () => Press(Key.Esc));
    }

    [Fact]
    public void It_opens_on_the_saved_style_rather_than_the_first()
    {
        Icons.Preview(IconStyle.Plain);
        Icons.Keep();

        Run(() =>
        {
            Assert.Equal(2, Dialog!.Style.Value);
            Assert.True(Dialog.Style.SubViews.OfType<CheckBox>().Last().HasFocus);
            Assert.Equal(IconStyle.Plain, Icons.Style);
            Press(Key.Esc);
        });
    }

    [Fact]
    public void Ctrl_Enter_keeps_the_style_for_the_next_start()
    {
        Run(
            () => Press(Key.CursorRight, Key.CursorRight),
            () => Icons.Style == IconStyle.Plain,
            () => Press(Key.Enter.WithCtrl),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(IconStyle.Plain, Kept);
        Assert.Equal(IconStyle.Plain, new IconSettings(new MentalDeskSettings(_host.SettingsPath), () => new FontDetection(true, "")).InUse);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Esc_or_the_close_box_puts_the_saved_style_back(bool esc)
    {
        Run(
            () => Press(Key.CursorRight),
            () => Icons.InUse == IconStyle.NerdFont,
            () =>
            {
                if (esc) Press(Key.Esc);
                else Dialog!.RequestStop();
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(IconStyle.Auto, Icons.Style);
        Assert.Equal(IconStyle.Plain, Icons.InUse);
        Assert.False(File.Exists(_host.SettingsPath));
    }

    [Fact]
    public void Diagnostics_shows_the_style_in_use_and_autos_reason()
    {
        using var window = new SwatchWindow(_host.Shell);
        DiagnosticsDialog? diagnostics = null;

        _host.Run(window,
            () => Press(Key.F12),
            () => (diagnostics = _host.App.TopRunnableView as DiagnosticsDialog) is not null,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("Plain (Auto)  •  couldn't tell which font the terminal uses", diagnostics!.IconsLine);
    }
}
