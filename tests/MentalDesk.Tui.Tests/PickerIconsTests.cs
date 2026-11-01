using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Icons;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class PickerIconsTests : StaticConfigurationTest
{
    private const string Nerd = "";
    private const string Emoji = "📄";
    private const int Columns = 20;

    private readonly IApplication _app = Application.Create().Init(DriverRegistry.Names.ANSI);

    public PickerIconsTests() => _app.Driver!.SetScreenSize(Columns, 4);

    public override void Dispose()
    {
        _app.Dispose();
        base.Dispose();
    }

    [Fact]
    public void Names_start_in_the_same_column_with_a_one_or_two_cell_icon_or_none()
    {
        Render(List(["src", "a.md", "b.md"], [Nerd, Emoji, null]));

        Assert.Equal(Nerd, Cell(0, 0).Grapheme);
        Assert.Equal(Emoji, Cell(1, 0).Grapheme);
        Assert.Equal(IconField.Width, Column(0, "s"));
        Assert.Equal(IconField.Width, Column(1, "a"));
        Assert.Equal(IconField.Width, Column(2, "b"));
    }

    [Fact]
    public void An_icon_takes_the_rows_hot_colour_over_its_background()
    {
        Render(List(["src"], [Nerd]));

        var icon = Cell(0, 0).Attribute!.Value;
        var name = Cell(0, IconField.Width).Attribute!.Value;
        Assert.NotEqual(name.Foreground, icon.Foreground);
        Assert.Equal(name.Background, icon.Background);
    }

    [Fact]
    public void A_picker_shows_icons_once_it_is_given_them_and_filters_on_the_names_alone()
    {
        using var picker = new PickerDialog<string>("Open", 40, 10, ["src", "notes.md"], row => row, "Open");
        picker.FilterField.Text = "no";
        Assert.Equal(["notes.md"], picker.Labels);

        picker.Icon = row => row.EndsWith(".md", StringComparison.Ordinal) ? Emoji : Nerd;

        Assert.Equal(["notes.md"], picker.Labels);
        Assert.IsType<IconListSource>(picker.SubViews.OfType<ListView>().Single().Source);
    }

    private ListView List(string[] items, string?[] icons) =>
        new() { App = _app, Width = Columns, Height = 4, Source = new IconListSource(items, icons) };

    private void Render(View view)
    {
        view.BeginInit();
        view.EndInit();
        view.SetFocus();
        view.Layout();
        var driver = _app.Driver!;
        driver.ClearContents();
        driver.Clip = new Region(driver.Screen);
        view.SetNeedsDraw();
        view.Draw();
    }

    private Cell Cell(int row, int col) => _app.Driver!.Contents![row, col];

    private int Column(int row, string grapheme) =>
        Enumerable.Range(0, Columns).First(col => Cell(row, col).Grapheme == grapheme);
}
