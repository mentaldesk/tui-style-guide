using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Theming;

namespace Swatch;

// Custom: real widgets would take focus and hot keys from the app around it.
internal sealed class PreviewView : View
{
    private const string Caret = "▏";

    private static readonly (string Label, string Scheme)[] Buttons =
        [("Enter Save", SchemeNames.ButtonPrimary), ("Del Delete", SchemeNames.ButtonDanger), ("Esc Cancel", SchemeNames.ButtonSecondary)];

    private IReadOnlyDictionary<string, Scheme> _schemes = new Dictionary<string, Scheme>();

    public PreviewView()
    {
        CanFocus = true;
        Width = Dim.Fill();
        Height = Dim.Fill();
    }

    public void Show(string theme)
    {
        _schemes = Themes.SchemesOf(theme);
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (!_schemes.TryGetValue(SchemeNames.Base, out var @base) || Viewport.Width < 20) return true;
        var width = Viewport.Width;
        Fill(0, 0, width, Viewport.Height, @base.Normal);

        var menu = Get(SchemeNames.Menu);
        Fill(0, 0, width, 1, menu.Normal);
        var x = 1;
        foreach (var title in new[] { "File", "Edit", "Help" })
        {
            Put(x, 0, title[..1], menu.HotNormal);
            Put(x + 1, 0, title[1..], menu.Normal);
            x += title.Length + 2;
        }

        var split = Math.Min(16, width / 3);
        var sidebar = Get(SchemeNames.Sidebar);
        Fill(0, 1, split, 5, sidebar.Normal);
        Box(0, 1, split, 5, "Files", sidebar.Normal, heavy: false);
        Put(1, 2, "▾ src", sidebar.Normal);
        Put(1, 3, "  notes.md".PadRight(Math.Max(0, split - 2)), sidebar.Active);
        Put(1, 4, "  todo.md", sidebar.Disabled);

        Box(split, 1, width - split, 5, "Editor", @base.Focus, heavy: true);
        Put(split + 1, 2, "Hello, ", @base.Editable);
        Put(split + 8, 2, "world", @base.Highlight);
        Put(split + 13, 2, Caret, Get(SchemeNames.Cursor).Normal with { Background = @base.Editable.Background });
        Put(split + 1, 3, "read only", @base.ReadOnly);
        Put(split + 1, 4, "⚠ changed on disk", Get(SchemeNames.Warning).Normal);

        var status = Get(SchemeNames.StatusBar);
        Fill(0, 6, width, 1, status.Normal);
        Put(1, 6, string.Join(HintRow.Separator, "Editor", "F1 keys"), status.Normal);

        var dialog = Get(SchemeNames.Dialog);
        var left = Math.Min(2, width / 10);
        var dialogWidth = Math.Min(36, width - left * 2);
        Fill(left, 8, dialogWidth, 6, dialog.Normal);
        Box(left, 8, dialogWidth, 6, "Save theme", dialog.Normal, heavy: false);
        Put(left + 1, 9, "Name ", dialog.Normal);
        Put(left + 6, 9, "Solar".PadRight(Math.Max(0, dialogWidth - 8)), dialog.Editable);
        Put(left + 11, 9, Caret, Get(SchemeNames.Cursor).Normal with { Background = dialog.Editable.Background });
        Put(left + dialogWidth - 27, 11, " Enter Save ", Get(SchemeNames.ButtonPrimary).Normal);
        Put(left + dialogWidth - 13, 11, " Esc Cancel ", Get(SchemeNames.ButtonSecondary).Normal);
        Put(left + 1, 12, " Couldn't write themes.json".PadRight(Math.Max(0, dialogWidth - 2)), Get(SchemeNames.Error).Normal);

        Put(left, 15, " Saving…".PadRight(Math.Max(0, dialogWidth)), Get(SchemeNames.Accent).Normal);

        x = left;
        foreach (var (label, scheme) in Buttons)
        {
            Put(x, 17, $" {label} ", Get(scheme).Normal);
            x += label.Length + 5;
        }
        return true;
    }

    private Scheme Get(string name) =>
        _schemes.TryGetValue(name, out var scheme) ? scheme : _schemes[SchemeNames.Base];

    private void Put(int x, int y, string text, Attribute attribute)
    {
        if (y >= Viewport.Height || x >= Viewport.Width) return;
        SetAttribute(attribute);
        Move(x, y);
        AddStr(text.Length > Viewport.Width - x ? text[..(Viewport.Width - x)] : text);
    }

    private void Fill(int x, int y, int width, int height, Attribute attribute)
    {
        for (var row = y; row < y + height; row++)
            Put(x, row, new string(' ', Math.Max(0, width)), attribute);
    }

    private void Box(int x, int y, int width, int height, string title, Attribute attribute, bool heavy)
    {
        if (width < 4 || height < 2) return;
        var (h, v, tl, tr, bl, br) = heavy ? ('━', '┃', '┏', '┓', '┗', '┛') : ('─', '│', '┌', '┐', '└', '┘');
        var top = $"{tl}{h} {title} ";
        Put(x, y, (top.Length < width - 1 ? top + new string(h, width - 1 - top.Length) : top[..(width - 1)]) + tr, attribute);
        for (var row = y + 1; row < y + height - 1; row++)
        {
            Put(x, row, v.ToString(), attribute);
            Put(x + width - 1, row, v.ToString(), attribute);
        }
        Put(x, y + height - 1, bl + new string(h, width - 2) + br, attribute);
    }
}
