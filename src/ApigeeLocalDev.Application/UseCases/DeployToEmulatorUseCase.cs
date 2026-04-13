using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

/// <summary>
/// Orquestra o deploy de proxies/shared flows no Apigee Emulator local.
///
/// O emulator usa a Management API padrão do Apigee Edge:
///   1. Import  → POST /v1/organizations/emulator/apis?action=import&name={proxy}
///   2. Deploy  → POST /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/deployments
///
/// IMPORTANTE: o emulator NÃO suporta deployArchive (requer GCS).
/// Para deploy de workspace completo iteramos cada proxy individualmente.
/// </summary>
public sealed class DeployToEmulatorUseCase(
    IWorkspaceRepository workspaceRepository,
    IApigeeEmulatorClient emulatorClient)
{
    /// <summary>
    /// Deploya um único proxy ou shared flow do workspace.
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

        var zipPath = await workspaceRepository.BuildBundleZipAsync(workspace, proxyOrFlowName, ct);
        await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
    }

    /// <summary>
    /// Deploya todos os proxies e shared flows do workspace, um por vez.
    /// O emulator local não suporta deployArchive, por isso iteramos cada
    /// bundle individualmente usando o mesmo fluxo import + deploy.
    /// Retorna os nomes de todos os itens deployados com sucesso.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExecuteFullAsync(
        ApigeeWorkspace workspace,
        string environment,
        CancellationToken ct = default)
    {
        var deployed = new List<string>();
        var errors   = new List<string>();

        // Proxies
        var proxies = workspaceRepository.ListApiProxies(workspace);
        foreach (var proxy in proxies)
        {
            try
            {
                var zipPath = await workspaceRepository.BuildBundleZipAsync(workspace, proxy, ct);
                await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
                deployed.Add(proxy);
            }
            catch (Exception ex)
            {
                errors.Add(proxy + ": " + ex.Message);
            }
        }

        // Shared flows
        var sharedFlows = workspaceRepository.ListSharedFlows(workspace);
        foreach (var sf in sharedFlows)
        {
            try
            {
                var zipPath = await workspaceRepository.BuildBundleZipAsync(workspace, sf, ct);
                await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
                deployed.Add(sf);
            }
            catch (Exception ex)
            {
                errors.Add(sf + ": " + ex.Message);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException(
                "Deploy parcial. Falhas: " + string.Join("; ", errors),
                errors.Select(e => new Exception(e)));

        return deployed;
    }
}
