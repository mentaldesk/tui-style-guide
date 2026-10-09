using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;

namespace MentalDesk.Tui.Chrome;

public sealed record MenuSpec(string Title, IReadOnlyList<MenuEntry?> Items);

public sealed record MenuEntry(string CommandId, string? Title = null, Func<bool>? IsChecked = null)
{
    public static implicit operator MenuEntry(string commandId) => new(commandId);
}

public sealed class AppMenu
{
    private readonly CommandRegistry _commands;
    private readonly Keymap _keys;
    private readonly List<(string Id, CommandMenuItem Item)> _items = [];
    private readonly List<(Func<bool> IsChecked, MenuItem Item, string Title)> _checks = [];
    private readonly List<(MenuBarItem Menu, string[] Ids)> _menus = [];

    public AppMenu(CommandRegistry commands, Keymap keys, IEnumerable<MenuSpec> layout)
    {
        _commands = commands;
        _keys = keys;
        Layout = [.. layout];
        Bar = new MenuBar { Menus = [.. Layout.Select(Menu)] };
        Bar.Disposing += (_, _) => _menus.Where(entry => entry.Menu.SuperView is null).ToList().ForEach(entry => entry.Menu.Dispose());
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
    }

    // Taken off the bar rather than hidden: Terminal.Gui keeps a hidden menu's place.
    public void ShowAvailable()
    {
        if (IsOpen) return;
        var here = _keys.FocusedScope();
        List<MenuBarItem> shown = [.. _menus.Where(entry => entry.Ids.Any(id => _commands.IsAvailable(id, here))).Select(entry => entry.Menu)];
        if (!shown.SequenceEqual(Shown))
            Bar.Menus = [.. shown];
    }

    // Terminal.Gui also binds a title's bare letter, app-wide, which would swallow the app's own keys.
    private MenuBarItem Menu(MenuSpec spec)
    {
        var menu = new MenuBarItem(spec.Title, [.. spec.Items.Select(Item)]);
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
        _menus.Add((menu, [.. spec.Items.OfType<MenuEntry>().Select(entry => entry.CommandId)]));
        return menu;
    }

    // A shut menu Terminal.Gui leaves enabled still answers its items' letters.
    private static void Shut(MenuBarItem menu)
    {
        if (menu is { PopoverMenuOpen: false, PopoverMenu: { } popover })
            popover.Enabled = false;
    }

    private View Item(MenuEntry? entry)
    {
        if (entry is not (var id, var title, var isChecked))
            return new Line();
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
}
