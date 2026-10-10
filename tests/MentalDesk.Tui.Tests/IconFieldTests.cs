using MentalDesk.Tui.Icons;
using Terminal.Gui.Drivers;
using Terminal.Gui.Text;

namespace MentalDesk.Tui.Tests;

public class IconFieldTests : StaticConfigurationTest
{
    private const string NerdFolder = "\U000F024B";
    private const string PlainFolder = "▸";
    private const int Columns = 20;

    private readonly IApplication _app = Application.Create().Init(DriverRegistry.Names.ANSI);
    private readonly TreeNode _src = new() { Text = "src", Children = [new TreeNode { Text = "notes.md" }] };

    public IconFieldTests() => _app.Driver!.SetScreenSize(Columns, 4);

    public override void Dispose()
    {
        _app.Dispose();
        base.Dispose();
    }

    [Theory]
    [InlineData(NerdFolder)]
    [InlineData(PlainFolder)]
    public void A_one_cell_glyph_takes_the_whole_field(string glyph) =>
        Assert.Equal(IconField.Width, IconField.Pad(glyph).GetColumns());

    [Fact]
    public void The_text_after_the_field_starts_in_the_same_column_in_either_style()
    {
        var tree = Tree(NerdFolder);
        Render(tree);
        var nerd = Column(0, NerdFolder);
        var nerdText = Column(0, "s");

        tree = Tree(PlainFolder);
        Render(tree);

        Assert.Equal(nerd, Column(0, PlainFolder));
        Assert.Equal(nerdText, Column(0, "s"));
        Assert.Equal(nerd + IconField.Width, nerdText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_icon_keeps_its_rows_background_in_its_own_colour(bool selected)
    {
        var tree = Tree(PlainFolder);
        tree.SelectedObject = selected ? _src : _src.Children[0];

        Render(tree);

        var at = Column(0, PlainFolder);
        var icon = Cell(0, at).Attribute!.Value;
        var name = Cell(0, at + IconField.Width).Attribute!.Value;
        Assert.Equal(name.Background, icon.Background);
        Assert.NotEqual(name.Foreground, icon.Foreground);
    }

    [Fact]
    public void The_text_the_tree_matches_has_no_glyph()
    {
        var tree = Tree(PlainFolder);

        Render(tree);

        Assert.Equal("src", tree.AspectGetter(_src));
    }

    private TreeView Tree(string glyph)
    {
        var tree = new TreeView { App = _app, Width = Columns, Height = 4 };
        tree.DrawLine += (_, e) => IconField.Prepend(e, glyph);
        tree.AddObject(_src);
        tree.Expand(_src);
        return tree;
    }

    private void Render(View view)
    {
        if (!view.IsInitialized)
        {
            view.BeginInit();
            view.EndInit();
        }
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
