using ApigeeLocalDev.Domain.Entities;

namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Contrato para interação com a Debug API do Apigee Emulator.
///
/// Endpoints utilizados:
///   POST   /v1/organizations/emulator/environments/{env}/apis/{api}/revisions/{rev}/debugsessions
///   GET    /v1/organizations/emulator/environments/{env}/apis/{api}/revisions/{rev}/debugsessions
///   GET    /v1/organizations/emulator/environments/{env}/apis/{api}/revisions/{rev}/debugsessions/{session}/data
///   GET    /v1/organizations/emulator/environments/{env}/apis/{api}/revisions/{rev}/debugsessions/{session}/data/{messageId}
///   DELETE /v1/organizations/emulator/environments/{env}/apis/{api}/revisions/{rev}/debugsessions/{session}
///   GET    /v1/organizations/emulator/environments/{env}/deployments
/// </summary>
public interface IApigeeTraceClient
{
    /// <summary>Cria uma nova sessão de debug no emulator.</summary>
    Task<TraceSession> CreateSessionAsync(
        string environment, string apiProxy, string revision,
        CancellationToken ct = default);

    /// <summary>Lista sessões de debug ativas para um proxy/revision.</summary>
    Task<IReadOnlyList<TraceSession>> ListSessionsAsync(
        string environment, string apiProxy, string revision,
        CancellationToken ct = default);

    /// <summary>Retorna os IDs de transações capturadas na sessão.</summary>
    Task<IReadOnlyList<string>> GetTransactionIdsAsync(
        string environment, string apiProxy, string revision, string sessionId,
        CancellationToken ct = default);

    /// <summary>Retorna o detalhe completo de uma transação (parsed do XML de debug).</summary>
    Task<TraceTransaction> GetTransactionDetailAsync(
        string environment, string apiProxy, string revision,
        string sessionId, string messageId,
        CancellationToken ct = default);

    /// <summary>Encerra e remove a sessão de debug.</summary>
    Task DeleteSessionAsync(
        string environment, string apiProxy, string revision, string sessionId,
        CancellationToken ct = default);

    /// <summary>Lista APIs deployadas em um ambiente.</summary>
    Task<IReadOnlyList<(string ApiProxy, string Revision)>> ListDeployedApisAsync(
        string environment, CancellationToken ct = default);
}
