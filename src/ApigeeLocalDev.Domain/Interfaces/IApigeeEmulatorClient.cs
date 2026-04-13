namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Contrato para comunicação com o Apigee Emulator (container Docker local).
///
/// O emulator expõe a Management API padrão do Apigee Edge na porta 8080:
///   GET  /v1/emulator/version                                        — health check
///   POST /v1/organizations/{org}/apis?action=import&name={proxy}     — import bundle ZIP
///   POST /v1/organizations/{org}/environments/{env}/apis/{api}/revisions/{rev}/deployments
///
/// IMPORTANTE: o emulator local NÃO suporta deployArchive (requer GCS).
/// Para deploy completo de workspace, use DeployBundleAsync para cada proxy.
/// Org padrão: "emulator".
/// </summary>
public interface IApigeeEmulatorClient
{
    /// <summary>Verifica se o emulator está acessível via GET /v1/emulator/version.</summary>
    Task<bool> IsAliveAsync(CancellationToken ct = default);

    /// <summary>
    /// Importa e deploya um bundle individual (proxy ou shared flow).
    /// Fluxo de 2 passos:
    ///   1. POST /v1/organizations/emulator/apis?action=import&name={proxy}  → extrai revision
    ///   2. POST /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/deployments
    /// O ZIP deve ter "apiproxy/" ou "sharedflowbundle/" diretamente na raiz.
    /// </summary>
    Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default);

    /// <summary>Lista imagens Docker disponíveis do emulator.</summary>
    Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default);

    /// <summary>Inicia o container do emulator com a imagem especificada.</summary>
    Task StartContainerAsync(string image, CancellationToken ct = default);

    /// <summary>Para o container do emulator.</summary>
    Task StopContainerAsync(CancellationToken ct = default);
}
