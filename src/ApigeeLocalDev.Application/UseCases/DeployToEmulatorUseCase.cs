using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

/// <summary>
/// Orquestra o deploy de proxies/shared flows no Apigee Emulator local.
///
/// O emulator usa a Management API padrão do Apigee Edge:
///   1. Import bundle  → POST /v1/organizations/emulator/apis?action=import&name={proxy}
///   2. Deploy revisão → POST /v1/organizations/emulator/environments/{env}/apis/{proxy}/revisions/{rev}/deployments
///
/// Para workspace archive completo:
///   POST /v1/organizations/emulator/environments/{env}:deployArchive
/// </summary>
public sealed class DeployToEmulatorUseCase(
    IWorkspaceRepository workspaceRepository,
    IApigeeEmulatorClient emulatorClient)
{
    /// <summary>
    /// Deploya um proxy ou shared flow específico do workspace.
    /// O ZIP gerado terá "apiproxy/" ou "sharedflowbundle/" na raiz —
    /// formato esperado pelo endpoint de import da Management API.
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
    /// Deploya o workspace inteiro via archive deploy.
    /// O ZIP terá a estrutura src/main/apigee/... esperada pelo
    /// endpoint POST /v1/organizations/emulator/environments/{env}:deployArchive.
    /// </summary>
    public async Task ExecuteFullAsync(
        ApigeeWorkspace workspace,
        string environment,
        CancellationToken ct = default)
    {
        var zipPath = await workspaceRepository.BuildWorkspaceZipAsync(workspace, ct);
        await emulatorClient.DeployArchiveAsync(environment, zipPath, ct);
    }
}
