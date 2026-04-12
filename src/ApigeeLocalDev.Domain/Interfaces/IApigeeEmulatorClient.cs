namespace ApigeeLocalDev.Domain.Interfaces;

public interface IApigeeEmulatorClient
{
    Task DeployBundleAsync(string environment, string zipFilePath, CancellationToken ct = default);
    Task<bool> IsAliveAsync(CancellationToken ct = default);
}
