#nullable enable
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StatsDirect.R.Interactive;

/// <summary>The generations of every session in this process, in one sequence.  The page orders sessions by generation alone, whichever
/// session they come from, so a session replacing another (the Rscript runner after a worker that could not start, a fresh session after
/// Restart) must announce a generation above the last one the page saw, or the page keeps the dead session and ignores the new one.</summary>
internal static class RSessionGenerations
{
    private static long last;

    internal static long Next() => Interlocked.Increment(ref last);
}

/// <summary>What the R script window's console holds: a session that raises the page's events (session, state, commandAccepted, inputEcho,
/// output, commandFinished, inputRequested, plot, notice, exited) whether R is the persistent worker or each command is a fresh Rscript run.
/// The host chooses the implementation for the installed R and treats both alike.</summary>
public interface IRSession : IDisposable
{
    event Action<JsonElement>? Event;

    /// <summary>True when objects persist between commands, as in the worker's interactive R; false for the Rscript runner.</summary>
    bool PersistentWorkspace { get; }

    /// <summary>The R this session runs, for the window to show: "R 4.6.2", or "R 4.1.3 through Rscript".</summary>
    string RDescription { get; }

    Task StartAsync();
    Task SubmitAsync(string source, string origin = "console");
    Task ReplyAsync(long requestId, string text);
    Task InterruptAsync();

    /// <summary>Starts the session afresh in a new generation, announced by a session event: the page waits for one after it asks to restart.</summary>
    Task RestartAsync();
    Task StopAsync();
    void PublishState();
    void PublishNotice(string text);
}
