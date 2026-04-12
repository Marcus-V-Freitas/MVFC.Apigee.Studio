namespace ApigeeLocalDev.Domain.Interfaces;

public interface IApigeeEmulatorClient
{
    Task DeployBundleAsync(string environment, string zipFilePath, CancellationToken ct = default);
    Task<bool> IsAliveAsync(CancellationToken ct = default);

    /// <summary>
    /// Sobe o container do Apigee Emulator usando a imagem Docker informada.
    /// A implementação pode usar docker CLI ou qualquer outro mecanismo.
    /// </summary>
    Task StartContainerAsync(string image, CancellationToken ct = default);

    /// <summary>
    /// Derruba (stop + rm) o container padrão do Apigee Emulator, se existir.
    /// </summary>
    Task StopContainerAsync(CancellationToken ct = default);

    /// <summary>
    /// Lista imagens Docker disponíveis no host para auto-completar na UI.
    /// </summary>
    Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default);
}
