using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MentalDesk.Tui.Icons;

internal sealed class IconListSource(IReadOnlyList<string> items, IReadOnlyList<string?> glyphs) : IListDataSource
{
    private readonly ListWrapper<string> _text = new(new ObservableCollection<string>(items));

    public event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add => _text.CollectionChanged += value;
        remove => _text.CollectionChanged -= value;
    }

    public int Count => _text.Count;

    public int MaxItemLength => _text.MaxItemLength + IconField.Width;

    public bool SuspendCollectionChangedEvent
    {
        get => _text.SuspendCollectionChangedEvent;
        set => _text.SuspendCollectionChangedEvent = value;
    }

    public void Render(ListView listView, bool selected, int item, int col, int row, int width, int viewportX = 0)
    {
        // Scrolled sideways, the icon would sit on top of the text.
        if (viewportX > 0)
        {
            _text.Render(listView, selected, item, col, row, width, viewportX);
            return;
        }

        var rowAttribute = listView.GetCurrentAttribute();
        var scheme = listView.GetScheme();
        IconField.Draw(listView, col, row, glyphs[item] ?? string.Empty, scheme, rowAttribute);
        _text.Render(listView, selected, item, col + IconField.Width, row, width - IconField.Width);
    }

    public bool IsMarked(int item) => _text.IsMarked(item);

    public void SetMark(int item, bool value) => _text.SetMark(item, value);

    public IList ToList() => _text.ToList();

    public void Dispose() => _text.Dispose();
}
