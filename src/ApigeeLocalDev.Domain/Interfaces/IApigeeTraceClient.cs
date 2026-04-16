namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Contrato para operações de management do emulator relacionadas ao trace.
/// O trace em si é capturado via TraceMiddleware — este client apenas
/// lista as APIs deployadas para popular o seletor na UI.
/// </summary>
public interface IApigeeTraceClient
{
    /// <summary>Lista APIs deployadas em um ambiente (Management API :8080).</summary>
    Task<IReadOnlyList<(string ApiProxy, string Revision)>> ListDeployedApisAsync(string environment, CancellationToken ct = default);
}
