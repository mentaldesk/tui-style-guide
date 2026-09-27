using System.Text;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Chrome;

public sealed class HintRow : View
{
    public const string Separator = "  •  ";

    public HintRow()
    {
        Height = 1;
        Width = Dim.Fill();
        CanFocus = false;
    }

    public string Says => string.Concat(SubViews.Select(view => view.Text));

    public void Show(IEnumerable<Hint> hints, CommandRegistry commands, Keymap keys) =>
        Show(hints.Select(hint => hint.Resolve(commands, keys)).OfType<(string, Action?)>());

    public void Show(IEnumerable<(string Text, Action? Run)> hints)
    {
        foreach (var view in SubViews.ToArray())
        {
            Remove(view);
            view.Dispose();
        }
        Pos x = 0;
        foreach (var (text, run) in hints)
        {
            if (SubViews.Count > 0)
            {
                var separator = new Label { Text = Separator, X = x, CanFocus = false };
                Add(separator);
                x = Pos.Right(separator);
            }
            View hint = run is null ? new Label { Text = text } : Button(text, run);
            hint.X = x;
            Add(hint);
            x = Pos.Right(hint);
        }
        SetNeedsLayout();
        SetNeedsDraw();
    }

    public static Button Button(string text, Action run)
    {
        var button = new Button
        {
            Text = text,
            NoDecorations = true,
            NoPadding = true,
            ShadowStyle = ShadowStyles.None,
            HotKeySpecifier = (Rune)0xffff,
            CanFocus = false,
        };
        button.Accepting += (_, e) =>
        {
            e.Handled = true;
            run();
        };
        return button;
    }
}
