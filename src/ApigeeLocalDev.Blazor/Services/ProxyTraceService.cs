using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Blazor.Services;

/// <summary>
/// Singleton que gerencia o ciclo de vida do trace por proxy reverso.
/// O TraceMiddleware chama Publish() a cada request interceptado;
/// o TraceViewer.razor consome via ReadAllAsync().
///
/// Usa um BroadcastChannel: cada chamada a ReadAllAsync() cria um
/// Channel próprio registrado como subscriber — todos recebem as mesmas
/// transações (útil caso múltiplas abas estejam abertas).
/// </summary>
public sealed class ProxyTraceService : IProxyTraceService
{
    private readonly Lock _lock = new();
    private readonly List<ChannelWriter<TraceTransaction>> _subscribers = [];

    public bool IsActive { get; private set; }

    public void Start()
    {
        lock (_lock)
            IsActive = true;
    }

    public void Stop()
    {
        lock (_lock)
            IsActive = false;
    }

    public void Publish(TraceTransaction transaction)
    {
        if (!IsActive) return;

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

    public async IAsyncEnumerable<TraceTransaction> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct)
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
