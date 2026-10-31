using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Keys;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class LeavingUnsavedWorkTests : StaticConfigurationTest
{
    private const string Posted = "A demo: Swatch has nowhere to post it, so the comment went nowhere";

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private SampleWindow? Sample => _host.App.TopRunnableView as SampleWindow;

    private ConfirmDialog? Prompt => _host.App.TopRunnableView as ConfirmDialog;

    private CommentDialog? Comment => _host.App.TopRunnableView as CommentDialog;

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

    [Fact]
    public void With_nothing_unsaved_Ctrl_Q_leaves_the_sample_at_once()
    {
        using var window = new SwatchWindow(_host.Shell);
        var prompted = false;

        _host.Run(window,
            () => Press(Key.T.WithCtrl, Key.T),
            () => Sample is not null,
            () => Press(Key.Q.WithCtrl),
            () =>
            {
                prompted |= Prompt is not null;
                return _host.App.TopRunnableView is SwatchWindow;
            });

        Assert.False(prompted);
    }

    [Fact]
    public void Two_files_left_unsaved_are_named_in_one_prompt_and_Save_all_leaves()
    {
        using var window = new SwatchWindow(_host.Shell);
        IReadOnlyList<string>? lines = null;
        string[]? buttons = null;

        _host.Run(window,
            () => Press(Key.T.WithCtrl, Key.T),
            () => Sample is not null,
            () =>
            {
                Sample!.Shell.Commands.Execute(SampleWindow.LeaveUnsaved);
                Press(Key.Q.WithCtrl);
            },
            () => Prompt is not null,
            () =>
            {
                lines = Prompt!.Lines;
                buttons = [.. Prompt.ButtonRow.Select(button => button.Text)];
                Press(CtrlEnter.Key);
            },
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(["2 files have unsaved changes:", "notes.md, plan.md", "Save them before quitting?"], lines);
        Assert.Equal([" Ctrl+Enter Save all ", " _Discard ", " Esc Cancel "], buttons!);
    }

    [Fact]
    public void Esc_on_a_comment_with_nothing_typed_closes_it_at_once()
    {
        using var window = new SwatchWindow(_host.Shell);
        var prompted = false;

        _host.Run(window,
            () => { _host.Shell.Commands.Execute(SwatchCommands.WriteComment); },
            () => Comment?.IsInitialized == true && Comment.Body.HasFocus,
            () => Press(Key.Esc),
            () =>
            {
                prompted |= Prompt is not null;
                return _host.App.TopRunnableView is SwatchWindow;
            });

        Assert.False(prompted);
    }

    public static TheoryData<string> Answers => new() { "cancel", "discard", "post" };

    [Theory]
    [MemberData(nameof(Answers))]
    public void Esc_on_a_comment_with_text_asks_and_each_button_does_what_it_says(string answer)
    {
        using var window = new SwatchWindow(_host.Shell);
        CommentDialog? comment = null;
        IReadOnlyList<string>? lines = null;
        string[]? buttons = null;
        string? title = null;

        _host.Run(window,
            () => { _host.Shell.Commands.Execute(SwatchCommands.WriteComment); },
            () => (comment = Comment)?.IsInitialized == true && comment.Body.HasFocus,
            () => Press(Key.Tab.WithShift),
            () => comment!.TitleField.HasFocus,
            () => Type("Focus"),
            () => Press(Key.Tab),
            () => comment!.Body.HasFocus,
            () => Type("Yes"),
            () => comment!.Body.Text == "Yes",
            () => Press(Key.Esc),
            () => Prompt is not null,
            () =>
            {
                title = Prompt!.Title;
                lines = Prompt.Lines;
                buttons = [.. Prompt.ButtonRow.Select(button => button.Text)];
                Press(answer switch { "cancel" => Key.Esc, "discard" => Key.D, _ => CtrlEnter.Key });
            },
            () => answer == "cancel" ? Comment == comment && comment!.Body.HasFocus : _host.App.TopRunnableView is SwatchWindow,
            () => Press(answer == "cancel" ? [Key.Esc] : []),
            () => answer != "cancel" || Prompt is not null,
            () => Press(answer == "cancel" ? [Key.D] : []),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal("Discard your comment?", title);
        Assert.Equal(["What you've written hasn't been posted."], lines);
        Assert.Equal([" Ctrl+Enter Post ", " _Discard ", " Esc Cancel "], buttons!);
        Assert.Equal(answer == "post", _host.Shell.StatusBar.Message == Posted);
    }
}
