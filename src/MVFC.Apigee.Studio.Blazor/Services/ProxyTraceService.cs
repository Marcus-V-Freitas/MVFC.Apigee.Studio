namespace MVFC.Apigee.Studio.Blazor.Services;

public sealed class ProxyTraceService : IProxyTraceService
{
    private readonly Lock _lock = new();
    private readonly List<ChannelWriter<TraceTransaction>> _subscribers = [];

    public bool IsActive { get; private set; }

    public (string WorkspaceRoot, string ProxyName)? ActiveProxy { get; private set; }

    public void Start()
    {
        lock (_lock)
            IsActive = true;
    }

    public void Stop()
    {
        lock (_lock)
        {
            IsActive   = false;
            ActiveProxy = null;
        }
    }

    public void SetActiveProxy(string workspaceRoot, string proxyName)
    {
        lock (_lock)
            ActiveProxy = (workspaceRoot, proxyName);
    }

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