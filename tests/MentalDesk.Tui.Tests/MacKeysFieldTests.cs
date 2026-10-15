using MentalDesk.Tui.Keys;
using Swatch;
using Terminal.Gui.Drivers;

namespace MentalDesk.Tui.Tests;

// Each mapping's bytes go through the driver's own input parser, as they would from the terminal.
public class MacKeysFieldTests : StaticConfigurationTest
{
    private const string Line = "one two three";
    private const string Lines = "one two three\nfour five six";

    private readonly Host _host = new();

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private CommentDialog Dialog => (CommentDialog)_host.App.TopRunnableView!;

    private static string Bytes(string kitty) => MacKeyMap.All.Single(key => key.Kitty == kitty).Bytes;

    [Fact]
    public void Each_key_moves_selects_or_deletes_in_a_TextField()
    {
        var expected = new Dictionary<string, (string Text, int Caret, string Selected)>
        {
            ["cmd+left"] = (Line, 0, ""),
            ["cmd+right"] = (Line, 13, ""),
            ["cmd+shift+left"] = (Line, 0, "one tw"),
            ["cmd+shift+right"] = (Line, 13, "o three"),
            ["cmd+up"] = (Line, 0, ""),
            ["cmd+down"] = (Line, 13, ""),
            ["cmd+shift+up"] = (Line, 0, "one tw"),
            ["cmd+shift+down"] = (Line, 13, "o three"),
            ["opt+left"] = (Line, 4, ""),
            ["opt+right"] = (Line, 8, ""),
            ["opt+shift+left"] = (Line, 4, "tw"),
            ["opt+shift+right"] = (Line, 8, "o "),
            ["cmd+backspace"] = ("o three", 0, ""),
            ["opt+backspace"] = ("one o three", 4, ""),
            ["cmd+a"] = (Line, 13, Line),
        };

        var seen = Each(expected.Keys, () =>
        {
            var field = new TextField { Width = 20, Text = Line };
            Dialog.Add(field);
            field.SetFocus();
            field.InsertionPoint = 6;
            field.ClearAllSelection();
            return () => (field.Text, field.InsertionPoint, field.SelectedText ?? "");
        });

        Assert.Equal(expected, seen);
    }

    [Fact]
    public void Each_key_moves_selects_or_deletes_in_a_MultiLineField()
    {
        var expected = new Dictionary<string, (string Text, int Caret, string Selected)>
        {
            ["cmd+left"] = (Lines, 14, ""),
            ["cmd+right"] = (Lines, 27, ""),
            ["cmd+shift+left"] = (Lines, 14, "four f"),
            ["cmd+shift+right"] = (Lines, 27, "ive six"),
            ["cmd+up"] = (Lines, 0, ""),
            ["cmd+down"] = (Lines, 27, ""),
            ["cmd+shift+up"] = (Lines, 0, "one two three\nfour f"),
            ["cmd+shift+down"] = (Lines, 27, "ive six"),
            ["opt+left"] = (Lines, 19, ""),
            ["opt+right"] = (Lines, 24, ""),
            ["opt+shift+left"] = (Lines, 19, "f"),
            ["opt+shift+right"] = (Lines, 24, "ive "),
            ["cmd+backspace"] = ("one two three\nive six", 14, ""),
            ["opt+backspace"] = ("one two three\nfour ive six", 19, ""),
            ["cmd+a"] = (Lines, 27, Lines),
        };

        var seen = Each(expected.Keys, () =>
        {
            var field = Dialog.Body;
            field.Text = Lines;
            field.ClearSelection();
            field.CaretOffset = 20;
            return () => (field.Text.ReplaceLineEndings("\n"), field.CaretOffset, field.SelectedText.ReplaceLineEndings("\n"));
        });

        Assert.Equal(expected, seen);
    }

    [Fact]
    public void Cmd_C_X_and_Z_copy_cut_and_undo_in_both()
    {
        var copied = new List<string>();
        string? cut = null;
        string? undone = null;
        Run(
            () =>
            {
                Dialog.TitleField.Text = Line;
                Dialog.TitleField.SetFocus();
                Send("cmd+a", "cmd+c");
            },
            () => Clipboard() == Line,
            () =>
            {
                copied.Add(Clipboard());
                Dialog.Body.Text = Lines;
                Dialog.Body.SetFocus();
                Send("cmd+a", "cmd+x");
            },
            () => Dialog.Body.Text.Length == 0,
            () =>
            {
                cut = Clipboard().ReplaceLineEndings("\n");
                Send("cmd+z");
            },
            () => Dialog.Body.Text.Length > 0,
            () => { undone = Dialog.Body.Text.ReplaceLineEndings("\n"); },
            () => _host.App.InjectKey(Key.Esc));

        Assert.Equal([Line], copied);
        Assert.Equal(Lines, cut);
        Assert.Equal(Lines, undone);
    }

    private string Clipboard() => _host.App.Clipboard?.GetClipboardData() ?? "";

    private void Send(params string[] keys)
    {
        var input = (InputProcessorImpl<char>)_host.App.Driver!.GetInputProcessor();
        foreach (var c in keys.SelectMany(Bytes))
            input.InputQueue.Enqueue(c);
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, [
            () => { _host.Shell.Commands.Execute(SwatchCommands.WriteComment); },
            () => _host.App.TopRunnableView is CommentDialog { IsInitialized: true },
            .. steps,
        ]);
    }

    // Sets the field up afresh for each key, sends it, and reads the field once the key has arrived.
    private Dictionary<string, (string, int, string)> Each(IEnumerable<string> keys, Func<Func<(string, int, string)>> setUp)
    {
        var seen = new Dictionary<string, (string, int, string)>();
        var steps = new List<Delegate>();
        foreach (var kitty in keys)
        {
            Func<(string, int, string)> read = null!;
            var arrived = false;
            void Arrived(object? sender, Key key) => arrived = true;
            steps.Add(() =>
            {
                read = setUp();
                _host.App.Keyboard.KeyDown += Arrived;
                Send(kitty);
            });
            steps.Add(() => arrived);
            steps.Add(() =>
            {
                _host.App.Keyboard.KeyDown -= Arrived;
                seen[kitty] = read();
            });
        }
        steps.Add(() => _host.App.InjectKey(Key.Esc));
        Run([.. steps]);
        return seen;
    }
}
