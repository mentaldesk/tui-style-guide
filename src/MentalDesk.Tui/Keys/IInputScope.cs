namespace MentalDesk.Tui.Keys;

public enum KeyResult
{
    Pass,

    Consumed,

    ChordInProgress,
}

public interface IInputScope
{
    KeyResult Handle(Key key);

    string? PendingChord => null;
}
