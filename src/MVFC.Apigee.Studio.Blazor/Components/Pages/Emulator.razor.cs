namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor page component for managing the Apigee Emulator lifecycle and deployments.
/// Allows the user to check emulator status, start/stop the Docker container, list available images,
/// and deploy bundles to a selected environment.
/// </summary>
public partial class Emulator : ComponentBase, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Indicates if the emulator is alive (reachable and running).
    /// </summary>
    private bool _alive;

    /// <summary>
    /// Indicates if the emulator status is currently being checked.
    /// </summary>
    private bool _checking;

    /// <summary>
    /// The base URL of the Apigee Emulator.
    /// </summary>
    private string _baseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 8080).ToString();

    /// <summary>
    /// Stores the last error message encountered when checking emulator status.
    /// </summary>
    private string? _statusError;

    /// <summary>
    /// Path to the ZIP bundle to deploy.
    /// </summary>
    private string _zipPath = string.Empty;

    /// <summary>
    /// Target environment for deployment (e.g., "local").
    /// </summary>
    private string _env = "local";

    /// <summary>
    /// Indicates if a deployment is currently in progress.
    /// </summary>
    private bool _deploying;

    /// <summary>
    /// Message displayed to the user about deployment status.
    /// </summary>
    private string _message = string.Empty;

    /// <summary>
    /// Indicates if the last deployment message is an error.
    /// </summary>
    private bool _isError;

    /// <summary>
    /// The Docker image currently selected for the emulator.
    /// </summary>
    private string _image = string.Empty;

    /// <summary>
    /// List of available Docker images for the emulator.
    /// </summary>
    private List<string> _images = [];

    /// <summary>
    /// Indicates if a Docker operation (start/stop) is in progress.
    /// </summary>
    private bool _dockerBusy;

    /// <summary>
    /// Message displayed to the user about Docker operations.
    /// </summary>
    private string _dockerMessage = string.Empty;

    /// <summary>
    /// Indicates if the last Docker operation resulted in an error.
    /// </summary>
    private bool _dockerError;

    /// <summary>
    /// The Docker image currently running in the container.
    /// </summary>
    private string? _runningImage;

    /// <summary>
    /// Client for communicating with the Apigee Emulator.
    /// </summary>
    [Inject]
    public required IApigeeEmulatorClient EmulatorClient { get; set; }

    /// <summary>
    /// Application configuration provider.
    /// </summary>
    [Inject]
    public required IConfiguration Config { get; set; }

    /// <summary>
    /// Service for managing session state across navigations.
    /// </summary>
    [Inject]
    public required SessionStateService SessionState { get; set; }

    /// <summary>
    /// Indicates if deployment actions should be disabled (when deploying or emulator is not alive).
    /// </summary>
    private bool DeployDisabled =>
        _deploying || !_alive;

    /// <summary>
    /// Returns the status text for the emulator to display in the UI.
    /// Shows "Verificando..." if checking, "Emulator online" if alive,
    /// or "Emulator offline" otherwise.
    /// </summary>
    /// <returns>Status text for the emulator.</returns>
    private string GetStatusText()
    {
        if (_checking)
            return "Verificando...";
        return _alive ? "Emulator online" : "Emulator offline";
    }

    /// <summary>
    /// Loads emulator configuration, available images, and checks emulator status on initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        _baseUrl = Config["ApigeeEmulator:BaseUrl"] ?? new UriBuilder(Uri.UriSchemeHttp, "localhost", 8080).ToString();
        _image = Config["ApigeeEmulator:Image"] ?? "gcr.io/apigee-release/hybrid/apigee-emulator:latest";

        if (SessionState.Has("emulator:image"))
        {
            _image = SessionState.Get<string>("emulator:image") ?? _image;
            _zipPath = SessionState.Get<string>("emulator:zipPath") ?? string.Empty;
            _env = SessionState.Get<string>("emulator:env") ?? "local";
        }

        await LoadImages();
        await Check();
    }

    /// <summary>
    /// Loads the list of available Docker images for the emulator.
    /// </summary>
    private async Task LoadImages()
    {
        try
        {
            var list = await EmulatorClient.ListImagesAsync();
            _images = [.. list];

            if (string.IsNullOrWhiteSpace(_image))
                _image = _images.FirstOrDefault(i => i.Contains("apigee", StringComparison.OrdinalIgnoreCase)) ?? "gcr.io/apigee-release/hybrid/apigee-emulator:latest";
        }
        catch (Exception ex)
        {
            _dockerMessage = "Não foi possível listar imagens: " + ex.Message;
            _dockerError = true;
        }
    }

    /// <summary>
    /// Starts the emulator Docker container with the selected image.
    /// </summary>
    private async Task StartContainer()
    {
        if (string.IsNullOrWhiteSpace(_image))
        {
            _dockerMessage = "Informe a imagem Docker.";
            _dockerError = true;
            return;
        }

        _dockerBusy = true;
        _dockerMessage = string.Empty;
        _dockerError = false;
        try
        {
            await EmulatorClient.StartContainerAsync(_image);
            _dockerMessage = "\u2714 Container iniciado.";
            await Check();
        }
        catch (Exception ex)
        {
            _dockerMessage = "\u2718 Erro ao iniciar: " + ex.Message;
            _dockerError = true;
        }
        finally
        {
            _dockerBusy = false;
        }
    }

    /// <summary>
    /// Stops the emulator Docker container.
    /// </summary>
    private async Task StopContainer()
    {
        _dockerBusy = true;
        _dockerMessage = string.Empty;
        _dockerError = false;
        try
        {
            await EmulatorClient.StopContainerAsync();
            _dockerMessage = "\u2714 Container parado.";
            await Check();
        }
        catch (Exception ex)
        {
            _dockerMessage = "\u2718 Erro ao parar: " + ex.Message;
            _dockerError = true;
        }
        finally
        {
            _dockerBusy = false;
        }
    }

    /// <summary>
    /// Restarts the emulator Docker container (Stops then Starts).
    /// </summary>
    private async Task RestartContainer()
    {
        _dockerBusy = true;
        _dockerMessage = string.Empty;
        _dockerError = false;
        try
        {
            _dockerMessage = "Reiniciando...";
            StateHasChanged();
            
            await EmulatorClient.StopContainerAsync();
            await EmulatorClient.StartContainerAsync(_image);
            
            _dockerMessage = "\u2714 Container reiniciado.";
            await Check();
        }
        catch (Exception ex)
        {
            _dockerMessage = "\u2718 Erro ao reiniciar: " + ex.Message;
            _dockerError = true;
        }
        finally
        {
            _dockerBusy = false;
        }
    }

    /// <summary>
    /// Checks if the emulator is alive and updates the status.
    /// </summary>
    private async Task Check()
    {
        _checking = true;
        _statusError = null;

        try
        {
            _alive = await EmulatorClient.IsAliveAsync();
            if (_alive)
            {
                _runningImage = await EmulatorClient.GetRunningImageAsync();
            }
            else
            {
                _runningImage = null;
            }
        }
        catch (Exception ex)
        {
            _alive = false;
            _runningImage = null;
            _statusError = "Não foi possível conectar: " + ex.Message;
        }
        finally
        {
            _checking = false;
        }
    }

    /// <summary>
    /// Deploys a bundle ZIP to the emulator in the selected environment.
    /// </summary>
    private async Task QuickDeploy()
    {
        if (string.IsNullOrWhiteSpace(_zipPath))
        {
            _message = "Informe o caminho do ZIP.";
            _isError = true;
            return;
        }

        _deploying = true;
        _message = string.Empty;
        _isError = false;

        try
        {
            await EmulatorClient.DeployBundleAsync(_env, _zipPath);
            _message = "\u2714 Deploy realizado com sucesso!";
        }
        catch (Exception ex)
        {
            _message = "\u2718 Erro: " + ex.Message;
            _isError = true;
        }
        finally
        {
            _deploying = false;
        }
    }

    /// <summary>
    /// Saves the current component state to the session state service.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose pattern implementation.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            SessionState.Set("emulator:image", _image);
            SessionState.Set("emulator:zipPath", _zipPath);
            SessionState.Set("emulator:env", _env);
        }

        _disposed = true;
    }
}