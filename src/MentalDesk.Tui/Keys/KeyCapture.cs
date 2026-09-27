namespace MentalDesk.Tui.Keys;

public sealed class KeyCapture(Action<Key> onKey) : IInputScope
{
    public KeyResult Handle(Key key)
    {
        onKey(key);
        return KeyResult.Consumed;
    }
}
