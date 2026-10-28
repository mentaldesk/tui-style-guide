using MentalDesk.Tui.Dialogs;
using MentalDesk.Tui.Loading;
using Swatch;

namespace MentalDesk.Tui.Tests;

public class PickerLoadingTests : StaticConfigurationTest
{
    private readonly Host _host = new();
    private IssuePicker? _picker;

    public override void Dispose()
    {
        _host.Dispose();
        base.Dispose();
    }

    private void Run(params Delegate[] steps)
    {
        using var window = new SwatchWindow(_host.Shell);
        _host.Run(window, [
            () =>
            {
                _picker = new IssuePicker(TimeSpan.FromMilliseconds(20));
                _picker.Run(_host.Shell);
            },
            .. steps,
        ]);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
            _host.App.InjectKey(key);
    }

    private string Says => _picker!.SubViews.OfType<LoadStateView>().Single().Says;

    [Fact]
    public void It_opens_loading_with_the_pick_button_dimmed()
    {
        Run(
            () => _picker?.State == LoadState.Loading,
            () =>
            {
                Assert.False(_picker!.PickButton.Enabled);
                Assert.Empty(_picker.Shown);
                Assert.False(_picker.NoMatches);
                Press(Key.Enter);
            },
            () =>
            {
                Assert.Same(_picker, _host.App.TopRunnableView);
                Press(Key.Esc);
            });

        Assert.Null(_picker!.Chosen);
    }

    [Fact]
    public void A_failed_load_says_why_and_R_retries_it()
    {
        Run(
            () => _picker?.State == LoadState.Failed,
            () =>
            {
                Assert.Equal("Couldn't load issues: failed on purpose", Says);
                Assert.False(_picker!.PickButton.Enabled);
                Press(Key.R);
            },
            () => _picker!.State == LoadState.Loaded,
            () =>
            {
                Assert.Equal(5, _picker!.Shown.Count);
                Assert.True(_picker.PickButton.Enabled);
                Assert.Equal("", _picker.FilterField.Text);
                Press(Key.Esc);
            });
    }

    [Fact]
    public void R_types_into_the_filter_once_the_rows_are_in()
    {
        Run(
            () => _picker?.State == LoadState.Failed,
            () => Press(Key.R),
            () => _picker!.State == LoadState.Loaded,
            () => Press(Key.R),
            () => _picker!.FilterField.Text == "r",
            () => Press(Key.Esc));
    }

    [Fact]
    public void The_header_sits_above_the_rows_in_the_same_columns()
    {
        Run(
            () => _picker?.State == LoadState.Failed,
            () => Press(Key.R),
            () => _picker!.State == LoadState.Loaded,
            () =>
            {
                var header = _picker!.SubViews.OfType<Label>().Single(label => label.Text == IssuePicker.Issue.Header);
                var list = _picker.SubViews.OfType<LoadStateView>().Single();
                Assert.Equal(header.Frame.Y + 1, list.Frame.Y);
                Assert.Equal(header.Frame.X, list.Frame.X);
                Assert.Equal(IssuePicker.Issue.Header.IndexOf("State"), _picker.Labels[0].IndexOf("open"));
                Press(Key.Esc);
            });
    }

    [Fact]
    public void A_pick_that_fails_says_why_and_stays_open_until_the_filter_changes()
    {
        Run(
            () => _picker?.State == LoadState.Failed,
            () => Press(Key.R),
            () => _picker!.State == LoadState.Loaded,
            () => Press(Key.CursorDown, Key.CursorDown, Key.Enter),
            () => _picker!.Alert.Message == "#24 is closed, so there's nothing to open",
            () =>
            {
                Assert.Same(_picker, _host.App.TopRunnableView);
                Assert.Null(_picker!.Chosen);
                Press(Key.S);
            },
            () => _picker!.Alert.Message == "",
            () => Press(Key.Esc));
    }

    [Fact]
    public void A_pick_that_works_closes_with_the_row()
    {
        Run(
            () => _picker?.State == LoadState.Failed,
            () => Press(Key.R),
            () => _picker!.State == LoadState.Loaded,
            () => Press(Key.Enter),
            () => _host.App.TopRunnableView is SwatchWindow);

        Assert.Equal(27, _picker!.Chosen?.Number);
        Assert.True(_picker.Confirmed);
    }

    [Fact]
    public void More_rows_keep_the_selected_row_selected()
    {
        using var picker = new PickerDialog<string>("Rows", 40, 10, "Loading rows…", row => row, "Pick");
        picker.ShowRows(["a", "b"], "No rows");
        picker.SelectedIndex = 1;

        picker.ShowRows(["a", "b", "c"], "No rows");

        Assert.Equal(1, picker.SelectedIndex);
        Assert.True(picker.PickButton.Enabled);
    }

    [Fact]
    public void No_rows_says_so_and_dims_the_pick_button()
    {
        using var picker = new PickerDialog<string>("Rows", 40, 10, "Loading rows…", row => row, "Pick");

        picker.ShowRows([], "No rows");

        Assert.Equal(LoadState.Empty, picker.State);
        Assert.False(picker.PickButton.Enabled);
        Assert.False(picker.NoMatches);
    }

    [Fact]
    public void F1_lists_Retry_while_the_load_has_failed()
    {
        var failed = Array.Empty<string>();
        var loaded = Array.Empty<string>();
        Run(
            () => _picker?.State == LoadState.Failed,
            () => { failed = Labels(); },
            () => Press(Key.R),
            () => _picker!.State == LoadState.Loaded,
            () => { loaded = Labels(); },
            () => Press(Key.Esc));

        Assert.Contains("Retry", failed);
        Assert.DoesNotContain("Retry", loaded);
        Assert.Contains("Open", loaded);

        string[] Labels() => [.. _picker!.KeysHere().Here!.Groups.SelectMany(group => group.Rows).Select(row => row.Label)];
    }

    [Fact]
    public void A_picker_with_fixed_rows_can_not_be_told_to_load()
    {
        using var picker = new PickerDialog<string>("Fixed", 40, 10, ["a"], row => row, "Pick");

        Assert.Throws<InvalidOperationException>(() => picker.ShowRows(["b"], "No rows"));
    }
}
