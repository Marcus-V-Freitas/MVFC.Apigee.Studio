namespace MVFC.Apigee.Studio.Application.UseCases;

/// <summary>
/// Orchestrates the deployment to the Apigee Emulator.
/// Swaps the order: Proxy first, then Test Resources.
/// </summary>
public sealed class DeployToEmulatorUseCase(
    IApigeeEmulatorClient emulatorClient,
    IWorkspaceRepository workspaceRepository,
    IBundleSnapshotRepository snapshotRepository,
    IApigeeLintRunner lintRunner,
    IBundleLinter bundleLinter,
    ILogger<DeployToEmulatorUseCase> logger)
{
    private readonly ILogger<DeployToEmulatorUseCase> _logger = logger;
    private readonly IBundleLinter _bundleLinter = bundleLinter;

    /// <summary>
    /// Executes a full deployment of the workspace to the specified environment.
    /// </summary>
    /// <param name="workspace">The workspace to deploy.</param>
    /// <param name="environment">The target environment name.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of informational messages about the deployment process.</returns>
    public async Task<IReadOnlyList<string>> ExecuteFullAsync(
        ApigeeWorkspace workspace,
        string environment,
        CancellationToken ct = default)
    {
        var messages = new List<string>();

        await RunPreDeployLintAsync(workspace, messages);
        await DeployMainBundleAsync(workspace, environment, ct);

        // Give the emulator a moment to activate the proxy before deploying test resources
        await Task.Delay(1000, ct);

        await DeployTestResourcesAsync(workspace, messages, ct);

        messages.Add($"[SUCCESS] Proxy implantado com sucesso em '{environment}'.");
        return messages;
    }

    /// <summary>
    /// Performs analysis before deployment, including structural linting, deep linting, and bundle diffing.
    /// </summary>
    /// <param name="workspace">The workspace to analyze.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A tuple containing structural lint results, deep lint results, and the bundle diff.</returns>
    public async Task<(LintResult Structural, IList<ApigeeLintResult> Deep, BundleDiff Diff)> PreDeployAnalysisAsync(
        ApigeeWorkspace workspace,

        CancellationToken ct = default)
    {
        // 1. Structural Lint (fast)
        var proxyNames = workspaceRepository.ListApiProxies(workspace);
        var structuralIssues = new List<LintIssue>();
        foreach (var proxyName in proxyNames)
        {
            var result = _bundleLinter.Lint(workspace.RootPath, proxyName);
            structuralIssues.AddRange(result.Issues);
        }
        var structural = new LintResult(structuralIssues);

        // 2. Deep Lint (apigeelint)

        var deep = await lintRunner.RunLintAsync(workspace);

        // 3. Diff

        var diff = await snapshotRepository.GetDiffAsync(workspace, ct);

        return (structural, deep, diff);
    }

    /// <summary>
    /// Gets a preview of what has changed since the last deployment.
    /// </summary>
    /// <param name="workspace">The workspace to check.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The bundle diff representing changes since the last snapshot.</returns>
    public async Task<BundleDiff> GetPreviewDiffAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        return await snapshotRepository.GetDiffAsync(workspace, ct);
    }

    private async Task RunPreDeployLintAsync(ApigeeWorkspace workspace, List<string> messages)
    {
        try
        {
            var lintResults = await lintRunner.RunLintAsync(workspace);
            var errors = lintResults.SelectMany(r => r.Messages).Count(m => m.Severity == 2);
            if (errors > 0)
            {
                messages.Add($"[WARNING] O bundle possui {errors.ToString(CultureInfo.InvariantCulture)} erro(s) de linting detectados pelo apigeelint.");
            }
        }
        catch
        {
            /* Ignore linting errors during deploy */

        }
    }

    private async Task DeployMainBundleAsync(ApigeeWorkspace workspace, string environment, CancellationToken ct)
    {
        await workspaceRepository.EnsureEnvironmentAsync(workspace, environment, ct);
        var fullZipPath = await workspaceRepository.BuildWorkspaceZipAsync(workspace, ct);
        try
        {
            await emulatorClient.DeployBundleAsync(environment, fullZipPath, ct);
            await snapshotRepository.CreateSnapshotAsync(workspace, ct);
        }
        finally
        {
            if (File.Exists(fullZipPath))
                File.Delete(fullZipPath);
        }
    }

    private async Task DeployTestResourcesAsync(ApigeeWorkspace workspace, List<string> messages, CancellationToken ct)
    {
        var testZipPath = await workspaceRepository.BuildTestBundleZipAsync(workspace, ct);
        try
        {
            _logger.LogDeployingTestResources();
            await emulatorClient.DeployTestDataAsync(testZipPath, ct);
            _logger.LogTestResourcesDeployed();
        }
        catch (Exception ex)
        {
            messages.Add($"[WARNING] Falha ao enviar recursos de teste: {ex.Message}");
        }
        finally
        {
            if (File.Exists(testZipPath))
                File.Delete(testZipPath);
        }
    }
}
