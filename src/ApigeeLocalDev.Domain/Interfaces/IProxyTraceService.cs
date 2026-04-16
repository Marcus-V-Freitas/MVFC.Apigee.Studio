namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Serviço singleton que recebe transações do TraceMiddleware
/// e as distribui para os consumidores Blazor via IAsyncEnumerable.
/// </summary>
public interface IProxyTraceService
{
    /// <summary>Publica uma transação capturada pelo middleware.</summary>
    void Publish(TraceTransaction transaction);

    /// <summary>
    /// Retorna um stream assíncrono de transações para consumo no componente Blazor.
    /// Cada chamada recebe um reader independente.
    /// </summary>
    IAsyncEnumerable<TraceTransaction> ReadAllAsync(CancellationToken ct);

    /// <summary>Indica se o trace está ativo (aceitando capturas).</summary>
    bool IsActive { get; }

    /// <summary>Ativa a captura de transações.</summary>
    void Start();

    /// <summary>Desativa a captura de transações.</summary>
    void Stop();

    /// <summary>
    /// Registra o workspace e proxy ativo para que o middleware
    /// possa resolver os flows do bundle no disco.
    /// </summary>
    void SetActiveProxy(string workspaceRoot, string proxyName);

    /// <summary>Retorna o workspace root e proxy name registrados, ou null se não houver.</summary>
    (string WorkspaceRoot, string ProxyName)? ActiveProxy { get; }
}