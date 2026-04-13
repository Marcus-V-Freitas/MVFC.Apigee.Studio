namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Contrato para comunicação com o Apigee Emulator (container Docker local).
///
/// O emulator expõe a Management API no padrão Apigee Edge na porta de controle (default 8080).
/// Endpoints documentados pela comunidade:
///   GET  /v1/emulator/version
///   POST /v1/organizations/{org}/apis?action=import&name={proxy}   — importa bundle ZIP
///   POST /v1/organizations/{org}/environments/{env}/apis/{api}/revisions/{rev}/deployments
///   POST /v1/organizations/{org}/environments/{env}:deployArchive  — workspace archive
/// Org padrão do emulator: "emulator"
/// </summary>
public interface IApigeeEmulatorClient
{
    /// <summary>
    /// Verifica se o emulator está acessível via GET /v1/emulator/version.
    /// </summary>
    Task<bool> IsAliveAsync(CancellationToken ct = default);

    /// <summary>
    /// Importa e deploya um bundle individual (proxy ou shared flow).
    /// Fluxo:
    ///   1. POST /v1/organizations/{org}/apis?action=import&name={proxy}  → extrai revision
    ///   2. POST /v1/organizations/{org}/environments/{env}/apis/{proxy}/revisions/{rev}/deployments
    /// O ZIP deve ter "apiproxy/" ou "sharedflowbundle/" na raiz.
    /// </summary>
    Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default);

    /// <summary>
    /// Deploya um workspace archive completo.
    /// POST /v1/organizations/{org}/environments/{env}:deployArchive
    /// </summary>
    Task DeployArchiveAsync(string environment, string zipPath, CancellationToken ct = default);

    /// <summary>Lista imagens Docker disponíveis do emulator.</summary>
    Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default);

    /// <summary>Inicia o container do emulator com a imagem especificada.</summary>
    Task StartContainerAsync(string image, CancellationToken ct = default);

    /// <summary>Para o container do emulator.</summary>
    Task StopContainerAsync(CancellationToken ct = default);
}
