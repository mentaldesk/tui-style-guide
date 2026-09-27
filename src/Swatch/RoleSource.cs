using System.Collections;
using System.Collections.Specialized;
using MentalDesk.Tui.Theming;

namespace Swatch;

internal sealed class RoleSource(Scheme scheme) : IListDataSource
{
    private const string Sample = " Aa ";

    private static readonly VisualRole[] Roles = Enum.GetValues<VisualRole>();

    public event NotifyCollectionChangedEventHandler? CollectionChanged { add { } remove { } }

    public int Count => Roles.Length;

    public int MaxItemLength => 48;

    public bool SuspendCollectionChangedEvent { get; set; }

    public static string Describe(Attribute attribute)
    {
        var colours = $"{TerminalCursor.Hex(attribute.Foreground)} on {TerminalCursor.Hex(attribute.Background)}";
        return attribute.Style == TextStyle.None ? colours : $"{colours}  {attribute.Style.ToString().ToLowerInvariant()}";
    }

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        var role = Roles[item];
        var rowRole = selected ? listView.HasFocus ? VisualRole.Focus : VisualRole.Active : VisualRole.Normal;
        var text = listView.GetAttributeForRole(rowRole);
        listView.Move(col, row);
        listView.SetAttribute(text);
        listView.AddStr($" {role,-10} ");
        listView.SetAttribute(scheme.GetAttributeForRole(role));
        listView.AddStr(Sample);
        listView.SetAttribute(text);
        var rest = $" {Describe(scheme.GetAttributeForRole(role))}";
        var room = Math.Max(0, width - 12 - Sample.Length);
        listView.AddStr(rest.Length > room ? rest[..room] : rest.PadRight(room));
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value) { }

    public IList ToList() => Roles;

    public void Dispose() { }
}
