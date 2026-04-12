namespace ApigeeLocalDev.Web.Application.Services;

public interface IApigeeEmulatorClient
{
    Task<bool> DeployAsync(string environment, string zipPath);
}
