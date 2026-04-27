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
    /// Initiates deployment of the entire workspace.
    /// </summary>
    private async Task DeployFull()
    {
        if (Workspace is null) return;
        await RunDeploy(() => DeployUseCase.ExecuteFullAsync(Workspace, _deployEnv));
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
            _deployMessages = results.ToList();
            
            if (results.Any(m => m.Contains("[WARNING]") || m.Contains("[ERROR]")))
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
