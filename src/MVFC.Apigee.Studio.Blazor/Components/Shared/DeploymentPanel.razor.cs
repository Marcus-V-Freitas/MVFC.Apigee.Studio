namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

/// <summary>
/// Blazor component for deploying an Apigee workspace to the local Apigee Emulator.
/// </summary>
public partial class DeploymentPanel : ComponentBase
{
    /// <summary>
    /// The target environment for deployment.
    /// </summary>
    private string _deployEnv = "local";

    /// <summary>
    /// <summary>
    /// Indicates if a deployment operation is in progress.
    /// </summary>
    private bool _deploying;

    /// <summary>
    /// Messages displayed to the user about deployment status.
    /// </summary>
    private List<string> _deployMessages = [];

    /// <summary>
    /// The workspace to deploy from.
    /// </summary>
    [Parameter]
    public ApigeeWorkspace? Workspace { get; set; }

    /// <summary>
    /// Use case for orchestrating deployment to the Apigee Emulator.
    /// </summary>
    [Inject]
    public required DeployToEmulatorUseCase DeployUseCase { get; set; }

    /// <summary>
    /// Service for displaying toast notifications.
    /// </summary>
    [Inject]
    public required ToastService Toast { get; set; }

    /// <summary>
    /// Preview of changes since last deployment.
    /// </summary>
    private BundleDiff? _diff;

    // Analysis results for preview
    private bool _showPreview;
    private LintResult? _structuralLint;
    private IList<ApigeeLintResult>? _deepLint;

    protected override async Task OnParametersSetAsync()
    {
        await RefreshDiff();
    }

    private async Task RefreshDiff()
    {
        if (Workspace is null) return;
        _diff = await DeployUseCase.GetPreviewDiffAsync(Workspace);
    }



    /// <summary>
    /// Initiates deployment of the entire workspace.
    /// </summary>
    private async Task DeployFull()
    {
        if (Workspace is null) return;

        try
        {
            _deploying = true;
            (_structuralLint, _deepLint, _diff) = await DeployUseCase.PreDeployAnalysisAsync(Workspace);
            _showPreview = true;
        }
        catch (Exception ex)
        {
            Toast.ShowError("Erro na análise pré-deploy: " + ex.Message);
        }
        finally
        {
            _deploying = false;
        }
    }

    private async Task ConfirmDeployment()
    {
        _showPreview = false;
        if (Workspace is null) return;
        await RunDeploy(() => DeployUseCase.ExecuteFullAsync(Workspace, _deployEnv));
        await RefreshDiff();
    }

    private void CancelDeployment()
    {
        _showPreview = false;
    }

    /// <summary>
    /// Runs the deployment action, updates UI state, and handles success or error feedback.
    /// </summary>
    /// <param name="action">The deployment action to execute.</param>
    private async Task RunDeploy(Func<Task<IReadOnlyList<string>>> action)
    {
        _deploying = true;
        _deployMessages = ["Deploying..."];
        StateHasChanged();

        try
        {
            var results = await action();
            _deployMessages = [.. results];

            if (results.Any(m => m.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase) || m.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase)))
                Toast.ShowWarning("Deploy concluído com avisos.");
            else
                Toast.ShowSuccess("Deploy realizado com sucesso!");
        }
        catch (Exception ex)
        {
            _deployMessages = [$"✘ Erro fatal no deploy: {ex.Message}"];
            Toast.ShowError("Erro fatal no deploy.");
        }
        finally
        {
            _deploying = false;
            StateHasChanged();
        }
    }
}
