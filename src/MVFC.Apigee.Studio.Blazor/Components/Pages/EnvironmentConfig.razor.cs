namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

public partial class EnvironmentConfig : ComponentBase
{
    private string _selectedWorkspaceName = "";
    private string _selectedEnvironment = "";
    private string _newEnvName = "";
    private ApigeeWorkspace? _workspace;   

    private string _kvmJson = "[]";
    private string _cachesJson = "[]";
    private string _targetServersJson = "[]";

    private bool _kvmIsDirty;
    private bool _cachesIsDirty;
    private bool _targetIsDirty;

    private readonly List<string> _environments = [];
    private IReadOnlyList<ApigeeWorkspace> _workspaces = [];

    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    [Inject] 
    public required ToastService Toast { get; set; }

    public bool WorkspaceNotSelected => 
        _workspace == null;

    protected override void OnInitialized() => 
        _workspaces = WorkspaceRepo.ListAll();

    private void OnWorkspaceChanged()
    {
        _workspace = _workspaces.FirstOrDefault(w => w.Name == _selectedWorkspaceName);
        _environments.Clear();
        _selectedEnvironment = "";

        if (_workspace == null)
        {
            return;
        }

        var envPath = Path.Combine(_workspace.RootPath, "environments");

        if (!Directory.Exists(envPath))
        {
            return;
        }

        var dirs = Directory.GetDirectories(envPath);
        _environments.AddRange(dirs.Select(d => Path.GetFileName(d)));
    }

    private async Task CreateEnvironment()
    {
        if (_workspace == null || string.IsNullOrWhiteSpace(_newEnvName))
        {
            return;
        }

        await WorkspaceRepo.EnsureEnvironmentAsync(_workspace, _newEnvName);
        
        if (!_environments.Contains(_newEnvName))
        {
            _environments.Add(_newEnvName);
        }
        
        _selectedEnvironment = _newEnvName;
        _newEnvName = "";
        
        await OnEnvironmentChanged();
        Toast.ShowSuccess($"Environment '{_selectedEnvironment}' criado.");
    }

    private async Task OnEnvironmentChanged()
    {
        if (_workspace == null || string.IsNullOrEmpty(_selectedEnvironment))
        {
            return;
        }

        await WorkspaceRepo.EnsureEnvironmentAsync(_workspace, _selectedEnvironment);
        await LoadKvm();
        await LoadCaches();
        await LoadTargetServers();
    }

    private string GetEnvPath() => 
        Path.Combine(_workspace!.RootPath, "environments", _selectedEnvironment);

    private async Task LoadKvm()
    {
        var path = Path.Combine(GetEnvPath(), "maps.json");
        
        _kvmJson = File.Exists(path) ? await File.ReadAllTextAsync(path) : "[\n  {\n    \"name\": \"my-kvm\",\n    \"scope\": \"environment\",\n    \"encrypted\": false,\n    \"entries\": {\n      \"key1\": \"value1\"\n    }\n  }\n]";
        _kvmIsDirty = false;
    }

    private async Task SaveKvm()
    {
        try
        {
            Directory.CreateDirectory(GetEnvPath());
            await File.WriteAllTextAsync(Path.Combine(GetEnvPath(), "maps.json"), _kvmJson);
            
            _kvmIsDirty = false;
            Toast.ShowSuccess("KVMs salvos com sucesso (maps.json).");
        }
        catch (Exception ex) 
        { 
            Toast.ShowError("Erro: " + ex.Message); 
        }
    }

    private async Task LoadCaches()
    {
        var path = Path.Combine(GetEnvPath(), "caches.json");
        
        _cachesJson = File.Exists(path) ? await File.ReadAllTextAsync(path) : "[\n  {\n    \"name\": \"my-cache\"\n  }\n]";
        _cachesIsDirty = false;
    }

    private async Task SaveCaches()
    {
        try
        {
            Directory.CreateDirectory(GetEnvPath());
            await File.WriteAllTextAsync(Path.Combine(GetEnvPath(), "caches.json"), _cachesJson);
            
            _cachesIsDirty = false;
            Toast.ShowSuccess("Caches salvos com sucesso.");
        }
        catch (Exception ex) 
        { 
            Toast.ShowError("Erro: " + ex.Message); 
        }
    }

    private async Task LoadTargetServers()
    {
        var path = Path.Combine(GetEnvPath(), "targetservers.json");
        
        _targetServersJson = File.Exists(path) ? await File.ReadAllTextAsync(path) : "[\n  {\n    \"name\": \"my-target-server\",\n    \"host\": \"localhost\",\n    \"port\": 5037,\n    \"isEnabled\": true\n  }\n]";
        _targetIsDirty = false;
    }

    private async Task SaveTargetServers()
    {
        try
        {
            Directory.CreateDirectory(GetEnvPath());
            await File.WriteAllTextAsync(Path.Combine(GetEnvPath(), "targetservers.json"), _targetServersJson);

            _targetIsDirty = false;
            Toast.ShowSuccess("Target Servers salvos com sucesso.");
        }
        catch (Exception ex) 
        { 
            Toast.ShowError("Erro: " + ex.Message); 
        }
    }
}