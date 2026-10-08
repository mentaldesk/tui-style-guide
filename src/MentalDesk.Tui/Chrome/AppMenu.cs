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
    private readonly List<(string Id, MenuItem Item)> _items = [];
    private readonly List<(Func<bool> IsChecked, MenuItem Item, string Title)> _checks = [];
    private readonly List<MenuBarItem> _menus = [];

    public AppMenu(CommandRegistry commands, Keymap keys, IEnumerable<MenuSpec> layout)
    {
        _commands = commands;
        _keys = keys;
        Bar = new MenuBar { Menus = [.. layout.Select(Menu)] };
    }

    public MenuBar Bar { get; }

    public IReadOnlyList<(string Id, MenuItem Item)> Items => _items;

    public IReadOnlyList<MenuBarItem> Menus => _menus;

    public bool IsOpen => Bar.IsOpen();

    public void Open() => Bar.InvokeCommand(Command.HotKey);

    public void Refresh()
    {
        foreach (var (id, item) in _items)
        {
            item.KeyView.Text = KeysFor(id);
            item.Enabled = _commands.IsEnabled(id);
        }
        foreach (var (isChecked, item, title) in _checks)
            item.Title = Checked(title, isChecked());
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
        _menus.Add(menu);
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
        var item = new MenuItem
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

    private string KeysFor(string id) => _keys.For(id, CommandScope.Global).FirstOrDefault()?.Display ?? string.Empty;

    private static string Hot(string label) => $"_{label}";
}
