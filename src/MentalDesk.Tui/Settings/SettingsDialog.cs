using System.Collections.ObjectModel;
using MentalDesk.Tui.Commands;
using MentalDesk.Tui.Dialogs;

namespace MentalDesk.Tui.Settings;

public sealed class SettingsDialog : AppDialog
{
    public const string KeepId = "settings.keep";
    public const string NextPageId = "settings.nextPage";
    public const string PreviousPageId = "settings.previousPage";

    private const string ChangedMark = " •";

    private readonly List<SettingsPage> _pages;
    private readonly ObservableCollection<string> _names;

    public SettingsDialog(IReadOnlyList<SettingsPage> pages) : base("Settings", width: 0, contentRows: 0)
    {
        if (pages.Count == 0) throw new ArgumentException("Settings needs at least one page.", nameof(pages));
        _pages = [.. pages];
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        Alert.Y = Pos.AnchorEnd();

        var listWidth = pages.Max(page => page.Title.Length) + ChangedMark.Length + 1;
        var rows = Dim.Func(_ => Math.Max(0, Viewport.Height - 2 - Alert.Lines));
        _names = new ObservableCollection<string>(pages.Select(Name));
        PageList = new ListView { Width = listWidth, Height = rows, CanFocus = false };
        PageList.SetSource(_names);
        PageList.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } index) Open(index);
        };
        Body = new View { X = listWidth + 2, Width = Dim.Fill(1), Height = rows, CanFocus = true };
        Add(PageList, new Line { Orientation = Orientation.Vertical, X = listWidth, Height = rows }, Body);

        Commands
            .Register(KeepId, "Keep", Keep)
            .Register(NextPageId, "Next page", () => Step(+1))
            .Register(PreviousPageId, "Previous page", () => Step(-1));
        Keys.Bind("Ctrl+Enter", KeepId)
            .Bind("Ctrl+PageDown", NextPageId)
            .Bind("Ctrl+PageUp", PreviousPageId);
        // Each page has its own scope, so two pages can give their actions the same key.
        var scopes = pages.Select((_, i) => new CommandScope($"settings.page{i}")).ToList();
        Keys.FocusedScope = () => Page is null ? CommandScope.Global : scopes[_pages.IndexOf(Page)];
        foreach (var (page, scope) in pages.Zip(scopes))
        {
            page.Visible = false;
            page.Changed += (_, _) => Mark(page);
            Body.Add(page);
            foreach (var action in page.Actions)
            {
                Commands.Register(action.Id, action.Label, action.Run, scope, isEnabled: () => Page == page);
                Keys.Bind([action.Key], action.Id);
            }
        }

        KeepButton = CommandButton(ButtonKind.Primary, KeepId);
        CancelButton = CommandButton(ButtonKind.Secondary, CancelId);
        AddButtonRow(Pos.Top(Alert) - 1, KeepButton, CancelButton);
        Initialized += (_, _) => Open(0);
    }

    public IReadOnlyList<SettingsPage> Pages => _pages;

    public SettingsPage? Page { get; private set; }

    public ListView PageList { get; }

    public View Body { get; }

    public Button KeepButton { get; }

    public Button CancelButton { get; }

    public void Open(int index)
    {
        var page = Pages[index];
        if (page != Page)
        {
            Page?.Visible = false;
            Page = page;
            page.Visible = true;
            page.SubViews.FirstOrDefault(view => view.CanFocus)?.SetFocus();
        }
        if (PageList.SelectedItem != index)
            PageList.SelectedItem = index;
    }

    // Enter on a page's list would otherwise reach Dialog, which closes on it.
    protected override bool OnAccepting(CommandEventArgs args)
    {
        args.Handled = true;
        return true;
    }

    protected override void OnClosed()
    {
        if (Confirmed) return;
        foreach (var page in Pages)
            page.Revert();
    }

    private static string Name(SettingsPage page) => page.IsChanged ? page.Title + ChangedMark : page.Title;

    private void Step(int by)
    {
        var at = Page is null ? 0 : _pages.IndexOf(Page);
        Open((at + by + Pages.Count) % Pages.Count);
    }

    private void Mark(SettingsPage page)
    {
        var index = _pages.IndexOf(page);
        if (_names[index] != Name(page))
            _names[index] = Name(page);
    }

    private void Keep()
    {
        try
        {
            foreach (var page in Pages.Where(page => page.IsChanged))
                page.Keep();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowAlert($"Couldn't keep your settings: {ex.Message}", Severity.Error);
            return;
        }
        finally
        {
            foreach (var page in Pages)
                Mark(page);
        }
        Accept();
    }
}
