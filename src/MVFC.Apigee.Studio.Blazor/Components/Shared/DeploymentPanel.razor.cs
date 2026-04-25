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
    /// Message displayed to the user about deployment status.
    /// </summary>
    private string _deployMessage = string.Empty;

    /// <summary>
    /// Indicates if the last deployment resulted in an error.
    /// </summary>
    private bool _deployError;

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
    /// Gets the CSS class for the deployment message (error or success).
    /// </summary>
    private string DeployMessageClass => _deployError ? "error-text" : "success-text";



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
    private async Task RunDeploy(Func<Task> action)
    {
        _deploying = true;
        _deployMessage = "Deploying...";
        _deployError = false;
        StateHasChanged();

        try
        {
            await action();
            _deployMessage = "✔ Deploy realizado com sucesso!";
            Toast.ShowSuccess("Deploy realizado!");
        }
        catch (Exception ex)
        {
            _deployError = true;
            _deployMessage = "✘ Erro no deploy: " + ex.Message;
            Toast.ShowError("Erro no deploy.");
        }
        finally
        {
            _deploying = false;
            StateHasChanged();
        }
    }
}
