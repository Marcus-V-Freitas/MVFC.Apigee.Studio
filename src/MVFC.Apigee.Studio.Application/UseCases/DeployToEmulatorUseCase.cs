namespace MVFC.Apigee.Studio.Application.UseCases;

/// <summary>
/// Orchestrates the deployment to the Apigee Emulator.
/// Swaps the order: Proxy first, then Test Resources.
/// </summary>
public sealed class DeployToEmulatorUseCase(
    IApigeeEmulatorClient emulatorClient,
    IWorkspaceRepository workspaceRepository,
    ILogger<DeployToEmulatorUseCase> logger)
{
    private readonly ILogger<DeployToEmulatorUseCase> _logger = logger;
    /// <summary>
    /// Executes a full deployment of the workspace to the specified environment.
    /// </summary>
    public async Task<IReadOnlyList<string>> ExecuteFullAsync(
        ApigeeWorkspace workspace,
        string environment,
        CancellationToken ct = default)
    {
        var messages = new List<string>();

        // 1. Deploy Main Bundle (Proxy/SharedFlow)
        await workspaceRepository.EnsureEnvironmentAsync(workspace, environment, ct);
        var fullZipPath = await workspaceRepository.BuildWorkspaceZipAsync(workspace, ct);
        try
        {
            await emulatorClient.DeployBundleAsync(environment, fullZipPath, ct);
        }
        finally
        {
            if (File.Exists(fullZipPath)) File.Delete(fullZipPath);
        }

        // Give the emulator a moment to activate the proxy before deploying test resources
        // that depend on it (API Products validate the proxy list).
        await Task.Delay(1000, ct);

        // 2. Deploy Test Resources (Mock Plane)
        // Note: Products must be deployed AFTER proxies because the emulator validates the proxy list.
        var testZipPath = await workspaceRepository.BuildTestBundleZipAsync(workspace, ct);
        try
        {
            _logger.LogDeployingTestResources();
            await emulatorClient.DeployTestDataAsync(testZipPath, ct);
            _logger.LogTestResourcesDeployed();
        }
        catch (Exception ex)
        {
            // If test resources fail, we still want to see the proxy deployment.
            messages.Add($"[WARNING] Falha ao enviar recursos de teste: {ex.Message}");
        }
        finally
        {
            if (File.Exists(testZipPath)) File.Delete(testZipPath);
        }

        // 3. Return active proxies (as a check)
        // Note: In real scenarios, we would poll for deployment status.
        messages.Add($"[SUCCESS] Proxy implantado com sucesso em '{environment}'.");
        return messages;
    }
}
