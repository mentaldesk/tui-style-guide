using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;

namespace MentalDesk.Tui.Finding;

// Docked above what you're reading, never a dialog. Its keys work while it has focus, and Esc from the text too.
public sealed class FindBar : View
{
    public const string Heading = "Find bar";
    public const string NextId = "find.next";
    public const string PreviousId = "find.previous";
    public const string ReplaceId = "find.replace";
    public const string ReplaceAllId = "find.replaceAll";
    public const string OtherFieldId = "find.otherField";
    public const string CloseId = "find.close";

    private const int LabelWidth = 9;
    private const int SideWidth = 14;

    private static readonly CommandScope InReplacement = new("Replacement");

    private readonly Label _queryLabel = new() { X = 1, Y = 0, Text = "Find" };
    private readonly TextField _query = new() { X = LabelWidth, Y = 0, Width = Dim.Fill(SideWidth) };
    private readonly Label _status = new() { X = Pos.AnchorEnd(SideWidth), Y = 0, Width = SideWidth - 1, TextAlignment = Alignment.End };
    private readonly Label _replaceLabel = new() { X = 1, Y = 1, Text = "Replace", Visible = false };
    private readonly TextField _replacement = new() { X = LabelWidth, Y = 1, Width = Dim.Fill(SideWidth), Visible = false };
    private readonly CommandRegistry _commands = new();
    private readonly Keymap _keys;
    private IReadOnlyList<TextMatch> _matches = [];
    private (int Row, int Column) _anchor;
    private bool _replacing;

    public FindBar()
    {
        CanFocus = true;
        Height = 2;
        BorderStyle = LineStyle.Single;
        Border!.Thickness = new Thickness(0, 0, 0, 1);
        Add(_queryLabel, _query, _status, _replaceLabel, _replacement);

        _keys = new Keymap(_commands) { FocusedScope = () => Holds(_replacement) ? InReplacement : CommandScope.Global };
        _commands
            .Register(NextId, "Next match", Next)
            .Register(PreviousId, "Previous match", Previous)
            .Register(ReplaceId, "Replace and go to next", ReplaceOne, InReplacement)
            .Register(ReplaceAllId, "Replace all", ReplaceAll, isEnabled: () => ReplaceVisible)
            .Register(OtherFieldId, "Other field", OtherField, isEnabled: () => ReplaceVisible)
            .Register(CloseId, "Close", Close)
            .Register(ShellCommands.ShowKeys, "Show keys", () => KeysRequested?.Invoke(this, KeysHere()),
                isEnabled: () => KeysRequested is not null);
        _keys.Bind("Enter", NextId)
            .Bind("Shift+Enter", PreviousId)
            .Bind("Enter", ReplaceId, InReplacement)
            .Bind([CtrlEnter.Key], ReplaceAllId)
            .Bind("Tab", OtherFieldId)
            .Bind("Shift+Tab", OtherFieldId)
            .Bind("Esc", CloseId)
            .Bind("F1", ShellCommands.ShowKeys);

        _query.TextChanged += (_, _) => Recompute(fromAnchor: true);
    }

    public event EventHandler? Closed;

    // Without a handler, F1 is left to the host.
    public event EventHandler<KeyColumn>? KeysRequested;

    public IFindTarget? Target { get; private set; }

    public bool IsOpen => Target is not null;

    public string Query
    {
        get => _query.Text;
        set => _query.Text = value;
    }

    public string Replacement
    {
        get => _replacement.Text;
        set => _replacement.Text = value;
    }

    public string Status => _status.Text;

    public IReadOnlyList<TextMatch> Matches => _matches;

    public int Current { get; private set; } = -1;

    // A growing log stops following its tail while this is true, so the found line stays put.
    public bool HoldsPlace => IsOpen && Current >= 0;

    public bool ReplaceVisible => _replacement.Visible;

    public TextField QueryField => _query;

    public TextField ReplacementField => _replacement;

    // A host with its own key stack pushes this when it opens the bar and pops it on Closed.
    public IInputScope ScopeOver(IInputScope below) => new LayeredScope(this, below);

    public void Open(IFindTarget target, bool replace = false)
    {
        if (Target != target)
        {
            Detach();
            Target = target;
            target.Dock(this);
            target.ContentChanged += OnContentChanged;
        }
        _anchor = target.Origin;
        ShowReplace(replace && target.CanReplace);
        var selected = target.SelectedText;
        if (selected.Length > 0 && !selected.Contains('\n') && !selected.Contains('\r'))
            _query.Text = selected;
        Recompute(fromAnchor: true);
        _query.SetFocus();
        _query.SelectAll();
    }

    public void Close()
    {
        if (Target is not { } target) return;
        target.ClearSelection();
        Detach();
        target.View.SetFocus();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Next()
    {
        if (Target is not { } target || _matches.Count == 0) return;
        Go(Current >= 0 ? TextSearch.IndexAfter(_matches, At(Current)) : TextSearch.IndexAtOrAfter(_matches, target.Origin));
    }

    public void Previous()
    {
        if (Target is not { } target || _matches.Count == 0) return;
        Go(TextSearch.IndexBefore(_matches, Current >= 0 ? At(Current) : target.Origin));
    }

    public void ReplaceOne()
    {
        if (Target is not { } target || !ReplaceVisible || _matches.Count == 0) return;
        if (Current < 0)
        {
            Next();
            return;
        }
        var match = _matches[Current];
        var replacement = _replacement.Text;
        Edit(() => target.Replace(match, replacement));
        Recompute(fromAnchor: false);
        Go(TextSearch.IndexAtOrAfter(_matches, (match.Row, match.Column + replacement.Length)));
    }

    public void ReplaceAll()
    {
        if (Target is not { } target || !ReplaceVisible || _matches.Count == 0) return;
        var replacement = _replacement.Text;
        var count = _matches.Count;
        Edit(() =>
        {
            for (var i = _matches.Count - 1; i >= 0; i--)
                target.Replace(_matches[i], replacement);
        });
        Recompute(fromAnchor: false);
        _status.Text = $"Replaced {count}";
    }

    public KeyColumn KeysHere()
    {
        var scope = _keys.FocusedScope();
        var bindings = _keys.Bindings
            .Where(binding => binding.CommandId != ShellCommands.ShowKeys && _commands.IsEnabled(binding.CommandId))
            .ToList();
        var local = bindings.Where(binding => binding.Scope == scope && scope != CommandScope.Global).ToList();
        var rows = local
            .Concat(bindings.Where(binding => binding.Scope == CommandScope.Global && local.All(mine => mine.Display != binding.Display)))
            .GroupBy(binding => binding.CommandId)
            .Select(group => new KeyRow(string.Join(", ", group.Select(binding => binding.Display)), _commands.Find(group.Key)!.Label))
            .OrderBy(row => row.Keys == "Esc");
        return new KeyColumn(Heading, [new KeyGroup(null, [.. rows])]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Detach();
        base.Dispose(disposing);
    }

    private void ShowReplace(bool visible)
    {
        _replaceLabel.Visible = _replacement.Visible = visible;
        Height = visible ? 3 : 2;
    }

    private void OtherField()
    {
        var field = Holds(_replacement) ? _query : _replacement;
        field.SetFocus();
        field.SelectAll();
    }

    private (int Row, int Column) At(int index) => (_matches[index].Row, _matches[index].Column);

    private void Go(int index)
    {
        if (Target is not { } target || index < 0) return;
        Current = index;
        _anchor = At(index);
        target.Select(_matches[index]);
        UpdateStatus();
    }

    private void Recompute(bool fromAnchor)
    {
        if (Target is not { } target) return;
        TextMatch? previous = Current >= 0 ? _matches[Current] : null;
        _matches = target.FindAll(_query.Text);
        target.SetHighlights(_matches);
        Current = -1;
        if (fromAnchor)
        {
            if (TextSearch.IndexAtOrAfter(_matches, _anchor) is var index and >= 0)
                Go(index);
            else
                target.ClearSelection();
        }
        else if (previous is { } match)
            Current = _matches.ToList().IndexOf(match);
        UpdateStatus();
    }

    private void UpdateStatus() =>
        _status.Text = _query.Text.Length == 0 ? string.Empty
            : _matches.Count == 0 ? "No results"
            : Current >= 0 ? $"{Current + 1} of {_matches.Count}"
            : $"{_matches.Count} {(_matches.Count == 1 ? "result" : "results")}";

    // A replacement changes the content once per match; recompute once, after them all.
    private void Edit(Action edit)
    {
        _replacing = true;
        try
        {
            edit();
        }
        finally
        {
            _replacing = false;
        }
    }

    private void OnContentChanged(object? sender, EventArgs e)
    {
        if (!_replacing)
            Recompute(fromAnchor: false);
    }

    private void Detach()
    {
        if (Target is not { } target) return;
        target.ContentChanged -= OnContentChanged;
        target.SetHighlights([]);
        target.Dock(null);
        Target = null;
        _matches = [];
        Current = -1;
    }

    // Terminal.Gui can leave HasFocus set on a view the keys have moved on from.
    private bool Holds(View view)
    {
        for (var focused = FocusedView(); focused is not null; focused = focused.SuperView)
            if (focused == view) return true;
        return false;
    }

    private View? FocusedView()
    {
        var view = App?.Navigation?.GetFocused();
        while (view?.MostFocused is { } inner && inner != view)
            view = inner;
        return view;
    }

    private sealed class LayeredScope(FindBar bar, IInputScope below) : IInputScope
    {
        public KeyResult Handle(Key key)
        {
            if (below.PendingChord is null && bar.Target is { } target
                && (bar.Holds(bar) || (key == Key.Esc && bar.Holds(target.View)))
                && bar._keys.Handle(key) is var result and not KeyResult.Pass)
                return result;
            return below.Handle(key);
        }

        public string? PendingChord => bar._keys.PendingChord ?? below.PendingChord;
    }
}
