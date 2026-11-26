using MentalDesk.Tui.Chrome;
using MentalDesk.Tui.Diagnostics;
using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Icons;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Paths;
using MentalDesk.Tui.Theming;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

public class VerbatimLabelTests : StaticConfigurationTest
{
    private const string Named = "Renamed _bar to my_count";

    private readonly IApplication _app = Application.Create().Init(DriverRegistry.Names.ANSI);
    private readonly string _home = Path.Combine(Path.GetTempPath(), $"mentaldesk-verbatim-{Guid.NewGuid():N}");

    public override void Dispose()
    {
        _app.Dispose();
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
        base.Dispose();
    }

    private string[] Drawn(View view, int width = 100, int height = 30)
    {
        _app.Driver!.SetScreenSize(width, height);
        view.App = _app;
        if (view is Label or AppStatusBar) view.Width = width;
        if (!view.IsInitialized)
        {
            view.BeginInit();
            view.EndInit();
        }
        view.SetRelativeLayout(new System.Drawing.Size(width, height));
        view.Layout();
        var driver = _app.Driver!;
        driver.ClearContents();
        driver.Clip = new Region(driver.Screen);
        view.SetNeedsDraw();
        view.Draw();
        return [.. Enumerable.Range(0, height).Select(row =>
            string.Concat(Enumerable.Range(0, width).Select(col => driver.Contents![row, col].Grapheme)))];
    }

    private void Shows(string text, View view) =>
        Assert.Contains(Drawn(view), line => line.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void A_verbatim_label_draws_every_underscore_and_has_no_hotkey()
    {
        using var label = new VerbatimLabel { Text = Named };

        Shows(Named, label);
        Assert.Equal(Key.Empty, label.HotKey);
    }

    [Fact]
    public void A_plain_label_still_takes_its_first_underscore_as_a_hotkey()
    {
        using var label = new Label { Text = "_Find" };

        Assert.Equal(Key.F, label.HotKey);
        Shows("Find", label);
        Assert.DoesNotContain(Drawn(label), line => line.Contains('_'));
    }

    [Fact]
    public void The_status_bar_message_resting_text_and_focus_word_keep_their_underscores()
    {
        using var bar = new AppStatusBar { SchemeName = SchemeNames.StatusBar };
        bar.SetFocusWord("my_pane");
        bar.RestingText = "~/code/my_app/order_service.cs";
        Shows("my_pane", bar);
        Shows("~/code/my_app/order_service.cs", bar);

        bar.ShowMessage(Named);
        Shows(Named, bar);
    }

    [Fact]
    public void A_confirm_dialogs_lines_keep_their_underscores()
    {
        using var dialog = new ConfirmDialog("Delete", ["Delete _old_notes.md?"],
            [new ConfirmAction("Delete", ButtonKind.Danger, CtrlEnter.Key)]);

        Shows("Delete _old_notes.md?", dialog);
    }

    [Fact]
    public void A_pickers_header_keeps_its_underscores()
    {
        using var picker = new PickerDialog<string>("Pick", 60, 6, ["a"], row => row, "Open", header: "_name  last_seen");

        Shows("_name  last_seen", picker);
    }

    [Fact]
    public void The_path_pickers_folder_line_keeps_its_underscores()
    {
        var folder = Path.Combine(_home, "_code", "my_app");
        Directory.CreateDirectory(folder);
        using var picker = new PathPicker("Open", PathKind.File, folder, _home);

        Assert.Equal($"~{Path.DirectorySeparatorChar}_code{Path.DirectorySeparatorChar}my_app", picker.Where);
        Shows(picker.Where, picker);
    }

    [Fact]
    public void Diagnostics_values_keep_their_underscores()
    {
        Directory.CreateDirectory(_home);
        var icons = new IconSettings(new MentalDeskSettings(Path.Combine(_home, "settings.json")), () => new FontDetection(false, "test"));
        using var dialog = new DiagnosticsDialog(_app, new TerminalCursor(_ => { }), "_my_theme", icons, isWindows: false);

        Shows("_my_theme", dialog);
    }
}
