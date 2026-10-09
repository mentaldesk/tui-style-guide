using System.Drawing;
using MentalDesk.Tui.Chrome;
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
        Body = new View { X = 1, Y = 0, Width = Dim.Fill(1), Height = plan.ContentRows(screen) - 1, CanFocus = false };
        Body.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
        Body.SetContentSize(new Size(plan.SheetWidth, plan.SheetHeight));
        if (plan.Here is { } here)
        {
            Body.Add(here.View);
            if (Stacked)
            {
                here.View.Width = Dim.Fill();
                plan.Everywhere.View.Y = here.Height + 1;
            }
            else
            {
                Body.Add(new Line { Orientation = Orientation.Vertical, X = here.Width + 1, Height = Dim.Fill() });
                plan.Everywhere.View.X = here.Width + Gutter;
            }
        }
        plan.Everywhere.View.Width = Dim.Fill();
        Body.Add(plan.Everywhere.View);
        Add(Body);

        Commands
            .Register("keys.up", "Scroll up", () => Body.ScrollVertical(-1))
            .Register("keys.down", "Scroll down", () => Body.ScrollVertical(1))
            .Register("keys.pageUp", "Page up", () => Body.ScrollVertical(-Body.Viewport.Height))
            .Register("keys.pageDown", "Page down", () => Body.ScrollVertical(Body.Viewport.Height));
        Keys.Bind("CursorUp", "keys.up")
            .Bind("CursorDown", "keys.down")
            .Bind("PageUp", "keys.pageUp")
            .Bind("PageDown", "keys.pageDown");
        ShowHints(new Hint(CancelId, "close"));
        _scope = new ReferenceScope(Keys);
    }

    public KeySheet Sheet { get; }

    public bool Stacked { get; }

    public View Body { get; }

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

    private sealed record Plan(Column? Here, Column Everywhere, bool Stacked)
    {
        public int SheetWidth => Here is null ? Everywhere.Width
            : Stacked ? Math.Max(Here.Width, Everywhere.Width)
            : Here.Width + Gutter + Everywhere.Width;

        public int SheetHeight => Here is null ? Everywhere.Height
            : Stacked ? Here.Height + 1 + Everywhere.Height
            : Math.Max(Here.Height, Everywhere.Height);

        public int Width => Math.Max(MinWidth, SheetWidth + Chrome);

        // A blank row between the sheet and the hint.
        public int ContentRows(Size screen) => Math.Max(3, Math.Min(SheetHeight + 1, screen.Height - Chrome));

        public static Plan Of(KeySheet sheet, Size screen)
        {
            var here = sheet.Here is null ? null : Column.Of(sheet.Here);
            var everywhere = Column.Of(sheet.Everywhere);
            var sideBySide = new Plan(here, everywhere, Stacked: false);
            return here is null || sideBySide.Width <= screen.Width ? sideBySide : sideBySide with { Stacked = true };
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
