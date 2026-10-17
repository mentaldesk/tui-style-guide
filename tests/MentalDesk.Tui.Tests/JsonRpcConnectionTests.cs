using System.Text;
using System.Text.Json.Nodes;
using MentalDesk.Tui.Assistant;

namespace MentalDesk.Tui.Tests;

public class JsonRpcConnectionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Answers_reach_their_own_requests_whatever_order_they_come_in()
    {
        using var agent = new InProcessAgent();
        agent.Connection.RequestHandler = (method, parameters) =>
            method == "echo" ? parameters!["value"]!.DeepClone() : throw new JsonRpcException(-32000, "nope");
        agent.Connection.Start();
        using var client = Client(agent);

        var first = client.RequestAsync("echo", new JsonObject { ["value"] = "one" });
        var second = client.RequestAsync("echo", new JsonObject { ["value"] = 2 });
        var failing = client.RequestAsync("other");

        Assert.Equal(2, (await second.WaitAsync(Timeout, TestContext.Current.CancellationToken))!.GetValue<int>());
        Assert.Equal("one", (await first.WaitAsync(Timeout, TestContext.Current.CancellationToken))!.GetValue<string>());
        var error = await Assert.ThrowsAsync<JsonRpcException>(() => failing.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.Equal((-32000, "nope"), (error.Code, error.Message));
    }

    [Fact]
    public async Task Notifications_reach_the_other_side_with_their_parameters()
    {
        using var agent = new InProcessAgent();
        var received = new TaskCompletionSource<(string, JsonNode?)>();
        agent.Connection.NotificationReceived += (method, parameters) => received.TrySetResult((method, parameters));
        agent.Connection.Start();
        using var client = Client(agent);

        client.Notify("session/cancel", new JsonObject { ["sessionId"] = "x" });

        var (method, parameters) = await received.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(("session/cancel", "x"), (method, parameters!["sessionId"]!.GetValue<string>()));
    }

    [Fact]
    public async Task A_request_with_no_handler_is_method_not_found()
    {
        using var agent = new InProcessAgent();
        agent.Connection.Start();
        using var client = Client(agent);

        var unknown = await Assert.ThrowsAsync<JsonRpcException>(() => agent.Connection.RequestAsync("custom/thing").WaitAsync(Timeout, TestContext.Current.CancellationToken));

        Assert.Equal(JsonRpcConnection.MethodNotFound, unknown.Code);
    }

    [Fact]
    public async Task Requests_still_waiting_fail_when_the_other_side_goes_away()
    {
        using var agent = new InProcessAgent();
        agent.Connection.RequestHandler = (_, _) => { agent.Exit(); return null; };
        agent.Connection.Start();
        using var client = Client(agent);

        var pending = client.RequestAsync("session/prompt");

        await Assert.ThrowsAsync<IOException>(() => pending.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        await client.Completion.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<IOException>(() => client.RequestAsync("after").WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task One_message_per_line_however_the_reads_split_them()
    {
        var stream = new ChunkedStream(Encoding.UTF8.GetBytes("{\"a\":1}\n\n{\"b\":\"é\"}\r\n{\"c\":3}"), chunk: 3);
        var reader = new LineMessageReader(stream);

        Assert.Equal("""{"a":1}""", Encoding.UTF8.GetString((await reader.ReadAsync(TestContext.Current.CancellationToken))!));
        Assert.Equal("""{"b":"é"}""", Encoding.UTF8.GetString((await reader.ReadAsync(TestContext.Current.CancellationToken))!));
        Assert.Equal("""{"c":3}""", Encoding.UTF8.GetString((await reader.ReadAsync(TestContext.Current.CancellationToken))!));
        Assert.Null(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_long_message_joined_with_others_in_one_read_comes_out_whole()
    {
        var body = new string('x', 40_000);
        var reader = new LineMessageReader(new ChunkedStream(Encoding.UTF8.GetBytes($"1\n\"{body}\"\n3\n"), chunk: int.MaxValue));

        Assert.Equal("1", Encoding.UTF8.GetString((await reader.ReadAsync(TestContext.Current.CancellationToken))!));
        Assert.Equal($"\"{body}\"", Encoding.UTF8.GetString((await reader.ReadAsync(TestContext.Current.CancellationToken))!));
        Assert.Equal("3", Encoding.UTF8.GetString((await reader.ReadAsync(TestContext.Current.CancellationToken))!));
    }

    [Fact]
    public void A_message_is_written_as_its_body_and_a_newline() =>
        Assert.Equal("{}\n", Encoding.UTF8.GetString(JsonRpcConnection.Frame("{}"u8.ToArray())));

    [Fact]
    public async Task A_request_waiting_on_an_async_answer_holds_up_no_other_message()
    {
        using var agent = new InProcessAgent();
        var release = new TaskCompletionSource<JsonNode?>();
        agent.Connection.AsyncRequestHandler = (method, _) => method == "slow" ? release.Task : Task.FromResult<JsonNode?>("fast");
        agent.Connection.Start();
        using var client = Client(agent);

        var slow = client.RequestAsync("slow");
        var fast = await client.RequestAsync("fast").WaitAsync(Timeout, TestContext.Current.CancellationToken);
        release.SetResult("slow");

        Assert.Equal("fast", fast!.GetValue<string>());
        Assert.Equal("slow", (await slow.WaitAsync(Timeout, TestContext.Current.CancellationToken))!.GetValue<string>());
    }

    private static JsonRpcConnection Client(InProcessAgent agent)
    {
        var client = new JsonRpcConnection(agent.Output, agent.Input);
        client.Start();
        return client;
    }

    private sealed class ChunkedStream(byte[] data, int chunk) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunk)], cancellationToken);
    }
}
