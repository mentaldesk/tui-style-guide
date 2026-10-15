using System.Collections.ObjectModel;
using MentalDesk.Tui.Fields;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Settings;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;

namespace Swatch;

internal sealed class AppearancePage : SettingsPage
{
    private const int IconsX = 7;

    private static readonly IconStyle[] Styles = [IconStyle.Auto, IconStyle.NerdFont, IconStyle.Plain];

    private readonly AppShell _shell;
    private readonly SwatchSettings _settings;
    private readonly IReadOnlyList<string> _themes = Themes.Names;
    private readonly ObservableCollection<string> _rows;
    private readonly Label _iconsDefault;
    private string _savedTheme = Themes.Current;

    public AppearancePage(AppShell shell, SwatchSettings settings) : base("Appearance")
    {
        _shell = shell;
        _settings = settings;
        _rows = new ObservableCollection<string>(_themes.Select(Row));
        Theme = new ListView { Y = 1, Width = Dim.Fill(), Height = _themes.Count };
        Theme.SetSource(_rows);
        Theme.SelectedItem = Math.Max(0, IndexOfTheme(Themes.Current));
        Theme.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } index) PreviewTheme(_themes[index]);
        };

        var iconsY = _themes.Count + 2;
        Icons = new OptionSelector
        {
            X = IconsX,
            Y = iconsY,
            Orientation = Orientation.Horizontal,
            Labels = [.. Styles.Select(IconSettings.Name)],
            Value = Array.IndexOf(Styles, shell.Icons.Style),
        }.SelectOnArrows();
        Icons.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } index) shell.Icons.Preview(Styles[index]);
        };
        _iconsDefault = Dimmed(new Label { X = Pos.Right(Icons) + 2, Y = iconsY });
        shell.Icons.Changed += OnIconsChanged;

        Add(new Label { Text = "Theme" }, Theme,
            new Label { Text = "Icons", Y = iconsY }, Icons, _iconsDefault,
            Dimmed(new Label { Text = shell.Icons.AutoReason, Y = iconsY + 1 }));
        ShowIconsDefault();
    }

    public ListView Theme { get; }

    public OptionSelector Icons { get; }

    public string IconsDefault => _iconsDefault.Text;

    public IReadOnlyList<string> ThemeRows => _rows;

    public override bool IsChanged => Themes.Current != _savedTheme || _shell.Icons.Style != _shell.Icons.Saved;

    public override void Keep()
    {
        _settings.Theme = Themes.Current;
        _savedTheme = Themes.Current;
        _shell.Icons.Keep();
    }

    public override void Revert()
    {
        PreviewTheme(_savedTheme);
        _shell.Icons.Revert();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _shell.Icons.Changed -= OnIconsChanged;
        base.Dispose(disposing);
    }

    private static string Row(string theme) =>
        theme == Themes.Current && theme != Themes.Default ? $"{theme}  {DefaultNote(Themes.Default)}" : theme;

    private int IndexOfTheme(string theme) => _themes.ToList().IndexOf(theme);

    private void PreviewTheme(string theme)
    {
        if (theme != Themes.Current)
            _shell.ApplyTheme(theme);
        for (var i = 0; i < _themes.Count; i++)
        {
            if (_rows[i] != Row(_themes[i]))
                _rows[i] = Row(_themes[i]);
        }
        if (Theme.SelectedItem != IndexOfTheme(Themes.Current))
            Theme.SelectedItem = IndexOfTheme(Themes.Current);
        OnChanged();
    }

    private void OnIconsChanged(object? sender, EventArgs e)
    {
        var index = Array.IndexOf(Styles, _shell.Icons.Style);
        if (Icons.Value != index)
            Icons.Value = index;
        ShowIconsDefault();
        OnChanged();
    }

    private void ShowIconsDefault() =>
        _iconsDefault.Text = _shell.Icons.Style == IconSettings.Default ? string.Empty : DefaultNote(IconSettings.Name(IconSettings.Default));
}
