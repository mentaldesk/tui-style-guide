using System.Drawing;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;

namespace MentalDesk.Tui.Keys;

public sealed class KeysDialog : AppDialog
{
    private const int Gutter = 3;
    private const int Chrome = 6;
    private const int MinWidth = 30;

    private readonly ReferenceScope _scope;

    public KeysDialog(KeySheet sheet, Size screen)
        : this(sheet, screen, Plan.Of(sheet, screen))
    {
    }

    private KeysDialog(KeySheet sheet, Size screen, Plan plan)
        : base("Keys", plan.Width, plan.ContentRows(screen))
    {
        Sheet = sheet;
        Stacked = plan.Stacked;
        var contentRows = plan.ContentRows(screen);
        Body = new View { X = 1, Y = 0, Width = Dim.Fill(1), Height = contentRows - 2, CanFocus = false };
        Body.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        Body.SetContentSize(new Size(plan.SheetWidth, plan.SheetHeight));
        if (plan.First is { } first)
        {
            Body.Add(first.View);
            if (Stacked)
            {
                first.View.Width = Dim.Fill();
                plan.Last.View.Y = first.Height + 1;
            }
            else
            {
                Body.Add(new Line { Orientation = Orientation.Vertical, X = first.Width + 1, Height = Dim.Fill() });
                plan.Last.View.X = first.Width + Gutter;
            }
        }
        plan.Last.View.Width = Dim.Fill();
        Body.Add(plan.Last.View);
        Add(Body);

        Commands
            .Register(CancelId, "Close", Cancel)
            .Register("keys.up", "Scroll up", () => Body.ScrollVertical(-1))
            .Register("keys.down", "Scroll down", () => Body.ScrollVertical(1))
            .Register("keys.pageUp", "Page up", () => Body.ScrollVertical(-Body.Viewport.Height))
            .Register("keys.pageDown", "Page down", () => Body.ScrollVertical(Body.Viewport.Height));
        Keys.Unbind([Key.F1], CommandScope.Global);
        Keys.Bind("CursorUp", "keys.up")
            .Bind("CursorDown", "keys.down")
            .Bind("PageUp", "keys.pageUp")
            .Bind("PageDown", "keys.pageDown");
        CloseButton = CommandButton(ButtonKind.Secondary, CancelId);
        AddButtonRow(contentRows - 1, CloseButton);
        _scope = new ReferenceScope(Keys);
    }

    public KeySheet Sheet { get; }

    public bool Stacked { get; }

    public View Body { get; }

    public Button CloseButton { get; }

    protected override IInputScope Scope => _scope;

    private sealed record Column(View View, int Width, int Height)
    {
        public static Column Of(KeyColumn column)
        {
            var keyWidth = column.Groups.SelectMany(group => group.Rows).Select(row => row.Keys.Length).DefaultIfEmpty(0).Max();
            var lines = new List<string>();
            foreach (var group in column.Groups)
            {
                var indent = group.Heading is null ? string.Empty : "  ";
                if (group.Heading is not null)
                    lines.Add(group.Heading);
                lines.AddRange(group.Rows.Select(row => $"{indent}{row.Keys.PadRight(keyWidth)}{new string(' ', Gutter)}{row.Label}"));
            }
            var width = lines.Append(column.Heading).Max(line => line.Length);
            var view = new View { Width = width, Height = lines.Count + 2, CanFocus = false };
            view.Add(new Label { Text = column.Heading }, new Line { Y = 1, Width = Dim.Fill() });
            for (var i = 0; i < lines.Count; i++)
                view.Add(new Label { Y = i + 2, Text = lines[i] });
            return new Column(view, width, lines.Count + 2);
        }
    }

    // Last is Everywhere, or Here alone in a dialog's sheet.
    private sealed record Plan(Column? First, Column Last, bool Stacked)
    {
        public int SheetWidth => First is null ? Last.Width
            : Stacked ? Math.Max(First.Width, Last.Width)
            : First.Width + Gutter + Last.Width;

        public int SheetHeight => First is null ? Last.Height
            : Stacked ? First.Height + 1 + Last.Height
            : Math.Max(First.Height, Last.Height);

        public int Width => Math.Max(MinWidth, SheetWidth + Chrome);

        // A blank row, then the button.
        public int ContentRows(Size screen) => Math.Max(4, Math.Min(SheetHeight + 2, screen.Height - Chrome));

        public static Plan Of(KeySheet sheet, Size screen)
        {
            Column[] columns = [.. new[] { sheet.Here, sheet.Everywhere }.OfType<KeyColumn>().Select(Column.Of)];
            if (columns.Length == 1) return new Plan(null, columns[0], Stacked: false);
            var sideBySide = new Plan(columns[0], columns[1], Stacked: false);
            return sideBySide.Width <= screen.Width ? sideBySide : sideBySide with { Stacked = true };
        }
    }

    // A reference, not a runner: no key reaches the app behind it.
    private sealed class ReferenceScope(Keymap keys) : IInputScope
    {
        public KeyResult Handle(Key key)
        {
            keys.Handle(key);
            return KeyResult.Consumed;
        }
    }
}
