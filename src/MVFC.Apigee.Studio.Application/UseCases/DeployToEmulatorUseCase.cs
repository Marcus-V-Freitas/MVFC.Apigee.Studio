namespace MVFC.Apigee.Studio.Application.UseCases;

/// <summary>
/// Orchestrates deployment to the local Apigee Emulator.
/// <para>
/// The emulator endpoint is:
///   POST /v1/emulator/deploy?environment={env}
///   Body: ZIP file of the entire workspace with structure src/main/apigee/...
/// </para>
/// <para>
/// The emulator validates that the folder src/main/apigee/environments/{env}/
/// exists inside the ZIP — if not, it returns 400 InvalidEnvironment.
/// </para>
/// </summary>
public sealed class DeployToEmulatorUseCase(
    IWorkspaceRepository workspaceRepository,
    IApigeeEmulatorClient emulatorClient)
{

    /// <summary>
    /// Deploys the entire workspace at once.
    /// The endpoint /v1/emulator/deploy receives a single ZIP with the full structure
    /// src/main/apigee/... — not proxy-by-proxy.
    /// </summary>
    /// <param name="workspace">The workspace to deploy from.</param>
    /// <param name="environment">The target environment for deployment.</param>
    /// <param name="ct">Optional. Cancellation token for the operation.</param>
    /// <returns>A list of deployed proxies and shared flows.</returns>
    /// <remarks>
    /// Example:
    /// <code>
    /// var deployed = await useCase.ExecuteFullAsync(workspace, "test");
    /// </code>
    /// </remarks>
    public async Task<IReadOnlyList<string>> ExecuteFullAsync(
        ApigeeWorkspace workspace,
        string environment,
        CancellationToken ct = default)
    {
        // Ensures that the environments/{env}/ folder exists on disk before zipping
        await workspaceRepository.EnsureEnvironmentAsync(workspace, environment, ct);
        var zipPath = await workspaceRepository.BuildWorkspaceZipAsync(workspace, ct);
        try
        {
            await emulatorClient.DeployBundleAsync(environment, zipPath, ct);
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }

        // Returns what was deployed
        var proxies     = workspaceRepository.ListApiProxies(workspace);
        var sharedFlows = workspaceRepository.ListSharedFlows(workspace);

        return [.. proxies, .. sharedFlows];
    }
}
