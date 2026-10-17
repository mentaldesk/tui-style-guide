using MentalDesk.Tui.Assistant;

namespace MentalDesk.Tui.Tests;

public sealed class AssistantSessionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"mentaldesk-{Guid.NewGuid():N}");

    private AssistantSessions Store => new(Path.Combine(_dir, ".tui", "Test.assistant.json"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Each_folder_keeps_its_own_last_session_for_its_agent()
    {
        Store.Save("/a", "npx x", "one");
        Store.Save("/b", "npx x", "two");

        Assert.Equal(("one", "two"), (Store.Load("/a", "npx x"), Store.Load("/b", "npx x")));
        Assert.Null(Store.Load("/a", "goose acp"));
    }

    [Fact]
    public void Saving_a_folder_again_replaces_its_session_and_null_forgets_it()
    {
        Store.Save("/a", "agent", "one");
        Store.Save("/a", "agent", "two");
        Assert.Equal("two", Store.Load("/a", "agent"));

        Store.Save("/a", "agent", null);
        Assert.Null(Store.Load("/a", "agent"));
    }

    [Fact]
    public void An_unreadable_file_is_no_sessions()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Store.Path)!);
        File.WriteAllText(Store.Path, "{ not json");

        Assert.Null(Store.Load("/a", "agent"));
    }
}
