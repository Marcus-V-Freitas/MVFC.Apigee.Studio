namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class DeploymentPanel : ComponentBase
{
    private const string DeployModeProxy = "proxy";
    private const string DeployModeFull = "full";

    private string _deployTarget = string.Empty;
    private string _deployEnv = "local";
    private string _deployMode = DeployModeFull;
    private bool _deploying;
    private string _deployMessage = string.Empty;
    private bool _deployError;

    [Parameter]
    public ApigeeWorkspace? Workspace { get; set; }

    [Inject]
    public required DeployToEmulatorUseCase DeployUseCase { get; set; }

    [Inject]
    public required ToastService Toast { get; set; }

    private string DeployMessageClass => _deployError ? "error-text" : "success-text";
    private string TabClass(string m) => _deployMode == m ? "btn btn-primary btn-sm" : "btn btn-ghost btn-sm";

    private void SetDeployModeProxy() => _deployMode = DeployModeProxy;
    private void SetDeployModeFull() => _deployMode = DeployModeFull;

    private async Task DeployProxy()
    {
        if (Workspace is null) return;
        await RunDeploy(() => DeployUseCase.ExecuteAsync(Workspace, _deployTarget, _deployEnv));
    }

    private async Task DeployFull()
    {
        if (Workspace is null) return;
        await RunDeploy(() => DeployUseCase.ExecuteFullAsync(Workspace, _deployEnv));
    }

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
