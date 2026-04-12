using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

public sealed class DeployToEmulatorUseCase(
    IWorkspaceRepository workspaceRepository,
    IApigeeEmulatorClient emulatorClient)
{
    /// <summary>Deploya um proxy ou shared flow específico.</summary>
    public async Task ExecuteAsync(
        ApigeeWorkspace workspace,
        string proxyOrFlowName,
        string environment,
        CancellationToken ct = default)
    {
        var zipPath = await workspaceRepository.BuildBundleZipAsync(workspace, proxyOrFlowName, ct);
        await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
    }

    /// <summary>Deploya o workspace inteiro (todos os proxies + shared flows + environments).</summary>
    public async Task ExecuteFullAsync(
        ApigeeWorkspace workspace,
        string environment,
        CancellationToken ct = default)
    {
        var zipPath = await workspaceRepository.BuildWorkspaceZipAsync(workspace, ct);
        await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
    }
}
