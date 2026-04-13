namespace ApigeeLocalDev.Domain.Interfaces;

/// <summary>
/// Contrato para comunicação com o Apigee Emulator (container Docker local).
/// </summary>
public interface IApigeeEmulatorClient
{
    /// <summary>
    /// Verifica se o emulator está acessível.
    /// </summary>
    Task<bool> IsAliveAsync(CancellationToken ct = default);

    /// <summary>
    /// Faz deploy de um bundle individual (proxy ou shared flow).
    /// O ZIP deve conter "apiproxy/" ou "sharedflowbundle/" na raiz.
    /// Endpoint: POST /v1/emulator/deploy?environment={env}
    /// </summary>
    Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default);

    /// <summary>
    /// Faz deploy de um archive completo do workspace (src/main/apigee/...).
    /// Endpoint: POST /v1/emulator/deployArchive?environment={env}
    /// </summary>
    Task DeployArchiveAsync(string environment, string zipPath, CancellationToken ct = default);

    /// <summary>Lista imagens Docker disponíveis do emulator.</summary>
    Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default);

    /// <summary>Inicia o container do emulator com a imagem especificada.</summary>
    Task StartContainerAsync(string image, CancellationToken ct = default);

    /// <summary>Para o container do emulator.</summary>
    Task StopContainerAsync(CancellationToken ct = default);
}
