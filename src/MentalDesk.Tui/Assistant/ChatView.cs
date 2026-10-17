using Terminal.Gui.Text;

namespace MentalDesk.Tui.Assistant;

// The conversation laid out to the width and scrolled, following the end while it's in view.
public sealed class ChatView : View
{
    private const double StripeTint = 0.1;

    private readonly Conversation _conversation;
    private IReadOnlyList<ChatLine> _lines = [];
    private (int Version, int Width) _laidOut = (-1, -1);

    public ChatView(Conversation conversation)
    {
        _conversation = conversation;
        CanFocus = true;
        ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        AddCommand(Command.Up, () => ScrollBy(-1));
        AddCommand(Command.Down, () => ScrollBy(1));
        AddCommand(Command.PageUp, () => ScrollBy(-Math.Max(Viewport.Height - 1, 1)));
        AddCommand(Command.PageDown, () => ScrollBy(Math.Max(Viewport.Height - 1, 1)));
        AddCommand(Command.Start, () => ScrollTo(0));
        AddCommand(Command.End, () => ScrollTo(int.MaxValue));
        KeyBindings.Add(Key.CursorUp, Command.Up);
        KeyBindings.Add(Key.CursorDown, Command.Down);
        KeyBindings.Add(Key.PageUp, Command.PageUp);
        KeyBindings.Add(Key.PageDown, Command.PageDown);
        KeyBindings.Add(Key.Home, Command.Start);
        KeyBindings.Add(Key.End, Command.End);
    }

    public IReadOnlyList<ChatLine> Lines
    {
        get
        {
            LayOut();
            return _lines;
        }
    }

    public bool AtBottom => Viewport.Y + Viewport.Height >= _lines.Count;

    public void Refresh()
    {
        var follow = AtBottom;
        LayOut();
        if (follow) ScrollTo(int.MaxValue);
        SetNeedsDraw();
    }

    private int TextWidth => Math.Max(Viewport.Width - 1, 1);

    private void LayOut()
    {
        if (_laidOut == (_conversation.Version, TextWidth)) return;
        _laidOut = (_conversation.Version, TextWidth);
        _lines = _conversation.Render(TextWidth);
        SetContentSize(new System.Drawing.Size(Math.Max(Viewport.Width, 1), Math.Max(_lines.Count, 1)));
    }

    private bool? ScrollBy(int rows) => ScrollTo(Viewport.Y + rows);

    private bool? ScrollTo(int top)
    {
        top = Math.Clamp(top, 0, Math.Max(_lines.Count - Viewport.Height, 0));
        if (top != Viewport.Y) Viewport = Viewport with { Y = top };
        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (mouse.Flags.HasFlag(MouseFlags.WheeledUp)) return ScrollBy(-3) == true;
        if (mouse.Flags.HasFlag(MouseFlags.WheeledDown)) return ScrollBy(3) == true;
        return base.OnMouseEvent(mouse);
    }

    protected override void OnViewportChanged(DrawEventArgs e)
    {
        base.OnViewportChanged(e);
        if (_laidOut.Width != TextWidth) Refresh();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        LayOut();
        var normal = GetAttributeForRole(VisualRole.Normal);
        var mine = Stripe(normal);
        for (var row = 0; row < Viewport.Height; row++)
        {
            var index = Viewport.Y + row;
            Move(0, row);
            SetAttribute(normal);
            AddStr(new string(' ', Viewport.Width));
            if (index >= _lines.Count) continue;
            var line = _lines[index];
            var attribute = normal;
            var left = 0;
            if (line.Role == ChatRole.User)
            {
                (attribute, left) = (mine, Conversation.Gutter);
                SetAttribute(mine);
                Move(left, row);
                AddStr(new string(' ', Math.Max(TextWidth - left, 0)));
            }
            SetAttribute(line.Kind switch
            {
                ChatLineKind.Heading => attribute with { Style = attribute.Style | TextStyle.Bold },
                ChatLineKind.Tool or ChatLineKind.Note => attribute with { Style = attribute.Style | TextStyle.Faint },
                _ => attribute,
            });
            Move(left, row);
            AddStr(line.Text);
        }
        return true;
    }

    // The user's turns sit on a band a tenth of the way from the background to the text.
    private static Attribute Stripe(Attribute normal) =>
        normal with { Background = new Color(Mix(normal.Background.R, normal.Foreground.R), Mix(normal.Background.G, normal.Foreground.G), Mix(normal.Background.B, normal.Foreground.B)) };

    private static int Mix(byte from, byte to) => (int)Math.Round(from + (to - from) * StripeTint);
}
