using ApigeeLocalDev.Domain.Entities;

namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Contrato para comunicação com o Apigee Emulator (container Docker local).
///
/// Endpoints usados:
///   GET    /v1/emulator/healthz                                   — liveness check
///   POST   /v1/emulator/deploy?environment=                       — bundle deploy
///   POST   /v1/emulator/deployArchive?environment=                — archive deploy
///   POST   /v1/emulator/trace?proxyName=                          — inicia sessão de trace
///   GET    /v1/emulator/trace/transactions?sessionid=             — polling de transações
///   DELETE /v1/emulator/trace?sessionid=                          — encerra sessão
/// </summary>
public interface IApigeeEmulatorClient
{
    /// <summary>Verifica se o emulator está acessível via GET /v1/emulator/healthz.</summary>
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
    /// Inicia uma sessão de trace para o proxy informado.
    /// POST /v1/emulator/trace?proxyName={proxyName}
    /// </summary>
    Task<TraceSession> StartTraceAsync(string proxyName, CancellationToken ct = default);

    /// <summary>
    /// Busca as transações capturadas até o momento para a sessão ativa.
    /// GET /v1/emulator/trace/transactions?sessionid={sessionId}
    /// Deve ser chamado em polling (~2 s) enquanto a sessão estiver ativa.
    /// </summary>
    Task<IReadOnlyList<TraceTransaction>> GetTraceTransactionsAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Encerra a sessão de trace ativa.
    /// DELETE /v1/emulator/trace?sessionid={sessionId}
    /// </summary>
    Task StopTraceAsync(string sessionId, CancellationToken ct = default);
}
