using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

/// <summary>
/// Orquestra o deploy no Apigee Emulator local.
///
/// O endpoint do emulator é:
///   POST /v1/emulator/deploy?environment={env}
///   Body: ZIP do workspace inteiro com estrutura src/main/apigee/...
///
/// O emulator valida que exista a pasta src/main/apigee/environments/{env}/
/// dentro do ZIP — sem ela retorna 400 InvalidEnvironment.
/// </summary>
public sealed class DeployToEmulatorUseCase(
    IWorkspaceRepository workspaceRepository,
    IApigeeEmulatorClient emulatorClient)
{
    /// <summary>
    /// Deploya um único proxy ou shared flow (build de bundle individual).
    /// Ainda usa o workspace ZIP completo pois o emulator exige o environment.
    /// </summary>
    public async Task ExecuteAsync(
        ApigeeWorkspace workspace,
        string proxyOrFlowName,
        string environment,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(proxyOrFlowName))
            throw new ArgumentException(
                "Informe o nome do proxy ou shared flow.", nameof(proxyOrFlowName));

        await workspaceRepository.EnsureEnvironmentAsync(workspace, environment, ct);
        var zipPath = await workspaceRepository.BuildWorkspaceZipAsync(workspace, ct);
        await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
    }

    /// <summary>
    /// Deploya o workspace completo de uma vez.
    /// O endpoint /v1/emulator/deploy recebe um único ZIP com toda a estrutura
    /// src/main/apigee/... — não proxy-a-proxy.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExecuteFullAsync(
        ApigeeWorkspace workspace,
        string environment,
        CancellationToken ct = default)
    {
        // Garante que a pasta environments/{env}/ existe no disco antes de zipar
        await workspaceRepository.EnsureEnvironmentAsync(workspace, environment, ct);

        var zipPath = await workspaceRepository.BuildWorkspaceZipAsync(workspace, ct);
        await emulatorClient.DeployBundleAsync(environment, zipPath, ct);

        // Retorna o que foi deployado
        var proxies     = workspaceRepository.ListApiProxies(workspace);
        var sharedFlows = workspaceRepository.ListSharedFlows(workspace);
        return proxies.Concat(sharedFlows).ToList();
    }
}
