namespace ApigeeLocalDev.Blazor.Components.Pages;

public partial class Emulator : ComponentBase
{
    private bool _alive;
    private bool _checking;
    private string _baseUrl = string.Empty;
    private string? _statusError;

    private string _zipPath = string.Empty;
    private string _env = "local";
    private bool _deploying;
    private string _message = string.Empty;
    private bool _isError;

    private string _image = string.Empty;
    private List<string> _images = [];
    private bool _dockerBusy;
    private string _dockerMessage = string.Empty;
    private bool _dockerError;

    private bool DeployDisabled => 
        _deploying || !_alive;

    protected override async Task OnInitializedAsync()
    {
        _baseUrl = Config["ApigeeEmulator:BaseUrl"] ?? "http://localhost:8080";
        _image = Config["ApigeeEmulator:Image"] ?? "gcr.io/apigee-release/hybrid/apigee-emulator:latest";
        await LoadImages();
        await Check();
    }

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

    private async Task Check()
    {
        _checking = true;
        _statusError = null;

        try
        {
            _alive = await EmulatorClient.IsAliveAsync();
        }
        catch (Exception ex)
        {
            _alive = false;
            _statusError = "Não foi possível conectar: " + ex.Message;
        }
        finally
        {
            _checking = false;
        }
    }

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
}