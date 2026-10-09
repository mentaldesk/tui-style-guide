using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Chrome;

public sealed record MenuSpec(string Title, IReadOnlyList<MenuEntry?> Items)
{
    public IEnumerable<string> CommandIds =>
        Items.OfType<MenuEntry>().SelectMany(entry => entry.Opposite is { } opposite ? [entry.CommandId, opposite.CommandId] : new[] { entry.CommandId });
}

public sealed record MenuEntry(string CommandId, string? Title = null, Func<bool>? IsChecked = null)
{
    // Shares this entry's place in the menu, which shows whichever of the two can run.
    public MenuEntry? Opposite { get; init; }

    public static implicit operator MenuEntry(string commandId) => new(commandId);
}

public sealed class AppMenu
{
    private readonly CommandRegistry _commands;
    private readonly Keymap _keys;
    private readonly List<(string Id, CommandMenuItem Item)> _items = [];
    private readonly List<(Func<bool> IsChecked, MenuItem Item, string Title)> _checks = [];
    private readonly List<(MenuBarItem Menu, string[] Ids, Slot[] Slots)> _menus = [];

    public AppMenu(CommandRegistry commands, Keymap keys, IEnumerable<MenuSpec> layout)
    {
        _commands = commands;
        _keys = keys;
        Layout = [.. layout];
        Bar = new MenuBar { Menus = [.. Layout.Select(Menu)] };
        Bar.Disposing += (_, _) => _menus
            .SelectMany(entry => entry.Slots.SelectMany(slot => new View?[] { slot.View, slot.Opposite }).Append(entry.Menu))
            .Where(view => view is { SuperView: null }).ToList().ForEach(view => view!.Dispose());
    }

    public MenuBar Bar { get; }

    public IReadOnlyList<MenuSpec> Layout { get; }

    public IReadOnlyList<(string Id, MenuItem Item)> Items => [.. _items.Select(entry => (entry.Id, (MenuItem)entry.Item))];

    public IReadOnlyList<MenuBarItem> Menus => [.. _menus.Select(entry => entry.Menu)];

    public IEnumerable<MenuBarItem> Shown => Bar.SubViews.OfType<MenuBarItem>();

    public bool IsOpen => Bar.IsOpen();

    public void Open() => Bar.InvokeCommand(Command.HotKey);

    internal bool IsDimmed(string id) => _items.Single(entry => entry.Id == id).Item.Dimmed;

    public void Refresh()
    {
        var here = _keys.FocusedScope();
        foreach (var (id, item) in _items)
        {
            item.KeyView.Text = KeysFor(id);
            item.Dimmed = !_commands.IsAvailable(id, here);
        }
        foreach (var (isChecked, item, title) in _checks)
            item.Title = Checked(title, isChecked());
        Arrange(here);
    }

    // Taken off the bar rather than hidden: Terminal.Gui keeps a hidden menu's place.
    public void ShowAvailable()
    {
        if (IsOpen) return;
        var here = _keys.FocusedScope();
        List<MenuBarItem> shown = [.. _menus.Where(entry => entry.Ids.Any(id => _commands.IsAvailable(id, here))).Select(entry => entry.Menu)];
        if (!shown.SequenceEqual(Shown))
            Bar.Menus = [.. shown];
        Arrange(here);
    }

    // Taken out rather than hidden: Terminal.Gui keeps a hidden item's place.
    private void Arrange(CommandScope here)
    {
        foreach (var (menu, _, slots) in _menus)
        {
            if (menu.PopoverMenu?.Root is not { } root) continue;
            List<View> shown = [.. slots.Select(slot => slot.Showing(id => _commands.IsAvailable(id, here)))];
            if (root.SubViews.SequenceEqual(shown)) continue;
            foreach (var view in root.SubViews.ToList())
                root.Remove(view);
            root.Add([.. shown]);
        }
    }

    // Terminal.Gui also binds a title's bare letter, app-wide, which would swallow the app's own keys.
    private MenuBarItem Menu(MenuSpec spec)
    {
        Slot[] slots = [.. spec.Items.Select(SlotFor)];
        var menu = new MenuBarItem(spec.Title, [.. slots.Select(slot => slot.View)]);
        menu.HotKeyBindings.Remove(menu.HotKey);
        menu.HotKeyBindings.Remove(menu.HotKey.WithShift);
        // Esc no longer quits, and it took the menu's close key with it.
        if (menu.PopoverMenu is { } popover && !popover.KeyBindings.TryGet(Key.Esc, out _))
            popover.KeyBindings.Add(Key.Esc, Command.Quit);
        menu.PopoverMenuOpenChanged += (_, _) =>
        {
            Shut(menu);
            Refresh();
        };
        Shut(menu);
        _menus.Add((menu, [.. spec.CommandIds], slots));
        return menu;
    }

    // A shut menu Terminal.Gui leaves enabled still answers its items' letters.
    private static void Shut(MenuBarItem menu)
    {
        if (menu is { PopoverMenuOpen: false, PopoverMenu: { } popover })
            popover.Enabled = false;
    }

    private Slot SlotFor(MenuEntry? entry) =>
        entry is null ? new Slot(new Line()) : new Slot(Item(entry), entry.CommandId, entry.Opposite is { } opposite ? Item(opposite) : null, entry.Opposite?.CommandId);

    private CommandMenuItem Item(MenuEntry entry)
    {
        var (id, title, isChecked) = entry;
        title ??= Hot(_commands.Find(id)?.Label ?? id);
        var item = new CommandMenuItem
        {
            Title = isChecked is null ? title : Checked(title, isChecked()),
            // A label only: the keymap already runs this key.
            BindKeyToApplication = false,
            Action = () => _commands.Execute(id),
        };
        item.KeyView.Text = KeysFor(id);
        _items.Add((id, item));
        if (isChecked is not null)
            _checks.Add((isChecked, item, title));
        return item;
    }

    private static string Checked(string title, bool isChecked) => $"{(isChecked ? '●' : ' ')} {title}";

    private string KeysFor(string id) => _keys.For(id, _commands.ScopeOf(id)).FirstOrDefault()?.Display ?? string.Empty;

    private static string Hot(string label) => $"_{label}";

    private sealed record Slot(View View, string? Id = null, CommandMenuItem? Opposite = null, string? OppositeId = null)
    {
        public View Showing(Func<string, bool> canRun) =>
            Opposite is not null && !canRun(Id!) && canRun(OppositeId!) ? Opposite : View;
    }
}
