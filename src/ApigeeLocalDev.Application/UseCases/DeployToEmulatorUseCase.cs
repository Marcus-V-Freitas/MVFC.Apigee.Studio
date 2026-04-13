using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

public sealed class DeployToEmulatorUseCase(
    IWorkspaceRepository workspaceRepository,
    IApigeeEmulatorClient emulatorClient)
{
    /// <summary>
    /// Deploya um proxy ou shared flow específico.
    /// O ZIP gerado tem o conteúdo começando em "apiproxy/" ou "sharedflowbundle/"
    /// direto na raiz, que é o formato esperado pelo endpoint de bundle deploy.
    /// </summary>
    public async Task ExecuteAsync(
        ApigeeWorkspace workspace,
        string proxyOrFlowName,
        string environment,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(proxyOrFlowName))
            throw new ArgumentException("Informe o nome do proxy ou shared flow.", nameof(proxyOrFlowName));

        var zipPath = await workspaceRepository.BuildBundleZipAsync(workspace, proxyOrFlowName, ct);
        await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
    }

    /// <summary>
    /// Deploya o workspace inteiro (todos os proxies + shared flows + environments)
    /// via endpoint de archive deploy no formato src/main/apigee/...
    ///
    /// NOTA: o emulator local (Cloud Code) suporta este formato via
    /// POST /v1/organizations/{org}/environments/{env}:deployArchive
    /// com o ZIP no body. Se o seu emulator usar outro endpoint,
    /// ajuste IApigeeEmulatorClient.DeployArchiveAsync.
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
