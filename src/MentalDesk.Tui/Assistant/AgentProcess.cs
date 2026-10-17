using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipelines;

namespace MentalDesk.Tui.Assistant;

// A running agent: what it writes, what it reads, and a way to end it.
public interface IAgentProcess : IDisposable
{
    Stream Output { get; }
    Stream Input { get; }
    Task Exited { get; }
    void Kill();
}

public interface IAgentLauncher
{
    // Null when the program isn't installed.
    IAgentProcess? Launch(string program, IReadOnlyList<string> arguments, string directory);
}

public sealed class ProcessLauncher : IAgentLauncher
{
    public IAgentProcess? Launch(string program, IReadOnlyList<string> arguments, string directory)
    {
        var info = new ProcessStartInfo(program)
        {
            WorkingDirectory = directory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        var process = new Process { StartInfo = info };
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            process.Dispose();
            return null;
        }
        // Unread, a full stderr pipe would stall the agent.
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        return new RunningProcess(process);
    }

    private sealed class RunningProcess(Process process) : IAgentProcess
    {
        public Stream Output => process.StandardOutput.BaseStream;
        public Stream Input => process.StandardInput.BaseStream;
        public Task Exited { get; } = process.WaitForExitAsync();

        public void Kill()
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is InvalidOperationException or Win32Exception)
            {
            }
        }

        public void Dispose() => process.Dispose();
    }
}

// An agent in this process, joined to the client by in-memory pipes: a demo, or a fake in tests.
public sealed class InProcessAgent : IAgentProcess
{
    private readonly Pipe _toAgent = new();
    private readonly Pipe _toClient = new();
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public InProcessAgent()
    {
        Connection = new JsonRpcConnection(_toAgent.Reader.AsStream(), _toClient.Writer.AsStream());
    }

    // The agent's end.
    public JsonRpcConnection Connection { get; }

    public Stream Output => _toClient.Reader.AsStream();
    public Stream Input => _toAgent.Writer.AsStream();
    public Task Exited => _exited.Task;
    public bool Killed { get; private set; }

    // Ends the agent as if its process exited, closing its output.
    public void Exit()
    {
        _toClient.Writer.Complete();
        _exited.TrySetResult();
    }

    public void Kill()
    {
        Killed = true;
        Exit();
    }

    public void Dispose() => Connection.Dispose();
}
