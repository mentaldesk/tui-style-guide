using System.Text;

namespace MentalDesk.Tui.Chrome;

// A Label that draws '_' as written instead of taking the first one as a hotkey marker.
public class VerbatimLabel : Label
{
    public static readonly Rune NoHotKey = (Rune)0xffff;

    public VerbatimLabel() => HotKeySpecifier = NoHotKey;
}
