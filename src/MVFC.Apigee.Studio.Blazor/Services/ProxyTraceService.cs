namespace MVFC.Apigee.Studio.Blazor.Services;

/// <summary>
/// Service that manages trace transactions and distributes them to Blazor consumers via IAsyncEnumerable.
/// </summary>
public sealed class ProxyTraceService : IProxyTraceService
{
    private readonly Lock _lock = new();
    private readonly List<ChannelWriter<TraceTransaction>> _subscribers = [];

    /// <summary>
    /// Gets a value indicating whether trace capture is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the currently active workspace root and proxy name, or null if none is set.
    /// </summary>
    public (string WorkspaceRoot, string ProxyName)? ActiveProxy { get; private set; }

    /// <summary>
    /// Activates trace capture. After calling this method, the service will accept and distribute trace transactions.
    /// </summary>
    public void Start()
    {
        lock (_lock)
            IsActive = true;
    }

    /// <summary>
    /// Deactivates trace capture and clears the active proxy information.
    /// After calling this method, the service will stop accepting trace transactions.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            IsActive   = false;
            ActiveProxy = null;
        }
    }

    /// <summary>
    /// Sets the current workspace root and proxy name as active.
    /// This allows the middleware to resolve bundle flows on disk.
    /// </summary>
    /// <param name="workspaceRoot">The root directory of the workspace.</param>
    /// <param name="proxyName">The name of the active proxy.</param>
    public void SetActiveProxy(string workspaceRoot, string proxyName)
    {
        lock (_lock)
            ActiveProxy = (workspaceRoot, proxyName);
    }

    /// <summary>
    /// Publishes a captured trace transaction to all active subscribers.
    /// If a subscriber is no longer available, it will be removed.
    /// </summary>
    /// <param name="transaction">The trace transaction to publish.</param>
    public void Publish(TraceTransaction transaction)
    {
        if (!IsActive) 
            return;

        List<ChannelWriter<TraceTransaction>> dead = [];

        lock (_lock)
        {
            foreach (var writer in _subscribers)
            {
                if (!writer.TryWrite(transaction))
                    dead.Add(writer);
            }

            foreach (var d in dead)
                _subscribers.Remove(d);
        }
    }

    /// <summary>
    /// Returns an asynchronous stream of trace transactions for Blazor component consumption.
    /// Each call receives an independent reader.
    /// </summary>
    /// <param name="ct">Cancellation token to cancel the enumeration.</param>
    /// <returns>An asynchronous stream of <see cref="TraceTransaction"/> objects.</returns>
    public async IAsyncEnumerable<TraceTransaction> ReadAllAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<TraceTransaction>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        lock (_lock)
            _subscribers.Add(channel.Writer);

        try
        {
            await foreach (var tx in channel.Reader.ReadAllAsync(ct))
                yield return tx;
        }
        finally
        {
            channel.Writer.TryComplete();
            lock (_lock)
                _subscribers.Remove(channel.Writer);
        }
    }
}