using ApigeeLocalDev.Domain.Entities;

namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Contrato para comunicação com o Apigee Emulator (container Docker local).
///
/// Endpoints do emulator:
///   GET    /v1/emulator/healthz
///   POST   /v1/emulator/deploy?environment=
///
/// Endpoints da Management API (porta 8080 — mesmos do Apigee Edge/Hybrid):
///   POST   /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions
///   GET    /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions/{sessionId}/data
///   DELETE /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions/{sessionId}
/// </summary>
public interface IApigeeEmulatorClient
{
    /// <summary>Verifica se o emulator está acessível.</summary>
    Task<bool> IsAliveAsync(CancellationToken ct = default);

    /// <summary>Importa e deploya um bundle individual (proxy ou shared flow).</summary>
    Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default);

    /// <summary>Lista imagens Docker disponíveis do emulator.</summary>
    Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default);

    /// <summary>Inicia o container do emulator com a imagem especificada.</summary>
    Task StartContainerAsync(string image, CancellationToken ct = default);

    /// <summary>Para o container do emulator.</summary>
    Task StopContainerAsync(CancellationToken ct = default);

    // ─── TRACE ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia uma debug session via Management API:
    ///   POST /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions
    /// Retorna a sessão criada com SessionId para polling posterior.
    /// </summary>
    Task<TraceSession> StartTraceAsync(
        string proxyName,
        string environment = "local",
        string revision    = "0",
        CancellationToken ct = default);

    /// <summary>
    /// Busca o payload completo da debug session (Messages[].point[]) via:
    ///   GET /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions/{sessionId}/data
    /// O emulator retorna TODAS as transações inline — não há endpoint por messageId.
    /// Deve ser chamado em polling (~2 s) enquanto a sessão estiver ativa.
    /// </summary>
    Task<IReadOnlyList<TraceTransaction>> GetTraceTransactionsAsync(
        string sessionId,
        string proxyName,
        string environment = "local",
        string revision    = "0",
        CancellationToken ct = default);

    /// <summary>
    /// Encerra a debug session:
    ///   DELETE /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions/{sessionId}
    /// </summary>
    Task StopTraceAsync(
        string sessionId,
        string proxyName,
        string environment = "local",
        string revision    = "0",
        CancellationToken ct = default);
}
