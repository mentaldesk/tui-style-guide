namespace MentalDesk.Tui.Chrome;

public enum MessageKind
{
    // Answers what the user just did.
    Reply,

    // Happened by itself.
    News,
}

public sealed class StatusMessage(string text, Severity severity, MessageKind kind, DateTimeOffset at)
{
    public string Text { get; } = text;

    public Severity Severity { get; } = severity;

    public MessageKind Kind { get; } = kind;

    public DateTimeOffset At { get; } = at;

    public bool CountsAsUnread => Kind == MessageKind.News && Severity == Severity.Error;

    public override string ToString() => $"{At:HH:mm}  {Text}";
}
