using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class CheckingTests : StaticConfigurationTest
{
    private static readonly ConfirmAction Save = new("Save", ButtonKind.Primary, CtrlEnter.Key);
    private static readonly ConfirmAction DontSave = new("Don't save", ButtonKind.Danger, Key.D);

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private CommentDialog? Comment => _host.App.TopRunnableView as CommentDialog;

    private static Label LabelOf(View dialog, string text) => dialog.SubViews.OfType<Label>().Single(label => label.Text == text);

    private static bool Marked(View dialog, string text) => LabelOf(dialog, text).SchemeName == SchemeNames.Error;

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private void Type(string text)
    {
        foreach (var c in text)
            _host.App.InjectKey(new Key(c));
    }

    private void WriteComment(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, [
            () => { _host.Shell.Commands.Execute(SwatchCommands.WriteComment); },
            () => Comment?.IsInitialized == true && Comment.Body.HasFocus,
            .. steps,
        ]);
    }

    [Fact]
    public void A_refused_post_marks_every_empty_field_focuses_the_first_and_says_its_reason_with_the_count()
    {
        bool markedBefore = true, titleMarked = false, bodyMarked = false, titleFocused = false;
        string? before = null, after = null;
        WriteComment(
            () =>
            {
                markedBefore = Marked(Comment!, "Title") || Marked(Comment!, "Comment");
                before = Comment!.Alert.Message;
                Press(CtrlEnter.Key);
            },
            () => Comment!.Alert.Lines > 0,
            () =>
            {
                titleMarked = Marked(Comment!, "Title");
                bodyMarked = Marked(Comment!, "Comment");
                titleFocused = Comment!.TitleField.HasFocus;
                after = Comment.Alert.Message;
                Press(Key.Esc);
            },
            () => Comment is null);

        Assert.False(markedBefore);
        Assert.Equal("", before);
        Assert.True(titleMarked);
        Assert.True(bodyMarked);
        Assert.True(titleFocused);
        Assert.Equal("Title is needed  •  1 more to fix", after);
        Assert.NotEqual("A demo: Swatch has nowhere to post it, so the comment went nowhere", _host.Shell.StatusBar.Message);
    }

    [Fact]
    public void The_message_follows_focus()
    {
        string? onBody = null, backOnTitle = null;
        WriteComment(
            () => Press(CtrlEnter.Key),
            () => Comment!.TitleField.HasFocus && Comment.Alert.Lines > 0,
            () => Press(Key.Tab),
            () => Comment!.Body.HasFocus,
            () =>
            {
                onBody = Comment!.Alert.Message;
                Press(Key.Tab.WithShift);
            },
            () => Comment!.TitleField.HasFocus,
            () =>
            {
                backOnTitle = Comment!.Alert.Message;
                Press(Key.Esc);
            },
            () => Comment is null);

        Assert.Equal("Comment is needed  •  1 more to fix", onBody);
        Assert.Equal("Title is needed  •  1 more to fix", backOnTitle);
    }

    [Fact]
    public void Marks_clear_as_each_field_is_fixed_and_the_next_post_posts()
    {
        string? oneLeft = null;
        bool titleMarked = true, bodyMarked = false, bodyMarkedAfter = true;
        var linesAfter = -1;
        WriteComment(
            () => Press(CtrlEnter.Key),
            () => Comment!.TitleField.HasFocus && Comment.Alert.Lines > 0,
            () => Type("Focus"),
            () => Comment!.TitleField.Text == "Focus",
            () =>
            {
                oneLeft = Comment!.Alert.Message;
                titleMarked = Marked(Comment, "Title");
                bodyMarked = Marked(Comment, "Comment");
                Press(Key.Tab);
            },
            () => Comment!.Body.HasFocus,
            () => Type("Yes"),
            () => Comment!.Body.Text == "Yes",
            () =>
            {
                bodyMarkedAfter = Marked(Comment!, "Comment");
                linesAfter = Comment!.Alert.Lines;
                Press(CtrlEnter.Key);
            },
            () => Comment is null);

        Assert.Equal("Comment is needed", oneLeft);
        Assert.False(titleMarked);
        Assert.True(bodyMarked);
        Assert.False(bodyMarkedAfter);
        Assert.Equal(0, linesAfter);
        Assert.Equal("A demo: Swatch has nowhere to post it, so the comment went nowhere", _host.Shell.StatusBar.Message);
    }

    [Fact]
    public void A_field_never_refused_is_not_checked_as_you_type()
    {
        var marked = true;
        var lines = -1;
        WriteComment(
            () => Type("a"),
            () => Comment!.Body.Text == "a",
            () => Press(Key.Backspace),
            () => Comment!.Body.Text.Length == 0,
            () =>
            {
                marked = Marked(Comment!, "Comment");
                lines = Comment!.Alert.Lines;
                Press(Key.Esc);
            },
            () => Comment is null);

        Assert.False(marked);
        Assert.Equal(0, lines);
    }

    [Theory]
    [InlineData("", "Name is needed")]
    [InlineData("  ", "Name is needed")]
    [InlineData("Taken", "Taken is taken")]
    public void A_confirm_check_stops_Ctrl_Enter_marks_the_prompt_and_keeps_the_text(string typed, string said)
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = Named(typed);
        var ticks = 0;
        bool marked = false, focused = false;
        string? message = null, text = null;

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press(Key.Tab, CtrlEnter.Key),
            () => ++ticks > 5,
            () => _host.App.TopRunnableView == dialog,
            () =>
            {
                marked = Marked(dialog, "Save as");
                focused = dialog.Field!.HasFocus;
                message = dialog.Alert.Message;
                text = dialog.Field.Text;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView == window);

        Assert.Null(dialog.Chosen);
        Assert.True(marked);
        Assert.True(focused);
        Assert.Equal(said, message);
        Assert.Equal(typed, text);
    }

    [Fact]
    public void A_confirm_check_passes_and_saves()
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = Named("");

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press(CtrlEnter.Key),
            () => dialog.Alert.Lines > 0,
            () => Type("Solar"),
            () => dialog.Alert.Lines == 0 && !Marked(dialog, "Save as"),
            () => Press(CtrlEnter.Key),
            () => _host.App.TopRunnableView == window);

        Assert.Equal(Save, dialog.Chosen);
    }

    [Fact]
    public void Only_Ctrl_Enter_is_checked()
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = new ConfirmDialog(
            "Save theme", ["Save as"], [Save, DontSave], field: new TextField(), check: _ => "Name is needed");

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press(Key.Tab, Key.Tab, Key.D),
            () => _host.App.TopRunnableView == window);

        Assert.Equal(DontSave, dialog.Chosen);
    }

    [Fact]
    public void A_refusal_that_comes_back_later_marks_and_focuses_its_field_and_keeps_the_text()
    {
        using var window = new AppWindow(_host.Shell);
        using var dialog = Named("Solar");
        bool marked = false, focused = false;
        string? message = null;

        _host.Run(window,
            () => dialog.Run(_host.Shell),
            () => dialog.Field!.HasFocus,
            () => Press(Key.Tab),
            () => dialog.ButtonFor(Save).HasFocus,
            () => dialog.Refuse(dialog.Field!, "Couldn't write themes.json"),
            () => dialog.Field!.HasFocus,
            () =>
            {
                marked = Marked(dialog, "Save as");
                focused = dialog.Field!.HasFocus;
                message = dialog.Alert.Message;
                Press(Key.End, Key.Backspace);
            },
            () => dialog.Field!.Text == "Sola",
            () => !Marked(dialog, "Save as") && dialog.Alert.Lines == 0,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView == window);

        Assert.True(marked);
        Assert.True(focused);
        Assert.Equal("Couldn't write themes.json", message);
    }

    [Fact]
    public void A_check_without_a_field_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new ConfirmDialog("Sure?", ["Sure?"], [Save], check: _ => null));
    }

    [Fact]
    public void Save_as_in_the_sample_refuses_an_empty_name_and_a_theme_that_exists()
    {
        using var window = new SwatchWindow(_host.Shell);
        ConfirmDialog? Dialog() => _host.App.TopRunnableView as ConfirmDialog;
        string? empty = null, taken = null, kept = null;

        _host.Run(window,
            () => _host.Shell.StatusBar.FocusWord == "Themes",
            () => Press(Key.T.WithCtrl, Key.T),
            () => _host.App.TopRunnableView is SampleWindow,
            () => { ((SampleWindow)_host.App.TopRunnableView!).Shell.Commands.Execute(SampleWindow.SaveTheme); },
            () => Dialog()?.Field?.HasFocus == true,
            () =>
            {
                Dialog()!.Field!.Text = "";
                Press(CtrlEnter.Key);
            },
            () => Dialog()?.Alert.Message == "Name is needed",
            () =>
            {
                empty = Dialog()!.Alert.Message;
                Type(Themes.Midnight);
                Press(CtrlEnter.Key);
            },
            () => Dialog()?.Alert.Message.StartsWith("There's", StringComparison.Ordinal) == true,
            () =>
            {
                taken = Dialog()!.Alert.Message;
                kept = Dialog()!.Field!.Text;
                Press(Key.Esc);
            },
            () => _host.App.TopRunnableView is SampleWindow,
            () => Press(Key.Esc),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("Name is needed", empty);
        Assert.Equal($"There's already a theme named {Themes.Midnight}", taken);
        Assert.Equal(Themes.Midnight, kept);
    }

    private static ConfirmDialog Named(string text) => new(
        "Save theme", ["Save as"], [Save], field: new TextField { Text = text },
        check: name => string.IsNullOrWhiteSpace(name) ? "Name is needed" : name == "Taken" ? "Taken is taken" : null);
}
