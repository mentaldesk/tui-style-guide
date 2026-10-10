using MentalDesk.Tui.Icons;

namespace Swatch;

internal static class SwatchIcons
{
    public static readonly Icon Folder = new("\U000F024B", "▸"); // nf-md-folder
    public static readonly Icon File = new("\U000F0214", "·"); // nf-md-file
    public static readonly Icon Ok = new("\U000F05E0", "✓"); // nf-md-check_circle
    public static readonly Icon Failed = new("\U000F0159", "✗"); // nf-md-close_circle
    public static readonly Icon Warning = new("\U000F0026", "⚠"); // nf-md-alert

    public static readonly (string Name, Icon Icon)[] Sample =
        [("Folder", Folder), ("File", File), ("Ok", Ok), ("Failed", Failed), ("Warning", Warning)];
}
