namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor page component for managing Apigee environment configuration files (KVMs, caches, target servers) per workspace and environment.
/// Allows the user to select a workspace, manage environments, and edit/save related configuration JSON files.
/// </summary>
public partial class EnvironmentConfig : ComponentBase
{
    /// <summary>
    /// The name of the currently selected workspace.
    /// </summary>
    private string _selectedWorkspaceName = "";

    /// <summary>
    /// The name of the currently selected environment.
    /// </summary>
    private string _selectedEnvironment = "";

    /// <summary>
    /// The name for a new environment to be created.
    /// </summary>
    private string _newEnvName = "";

    /// <summary>
    /// The currently selected workspace instance.
    /// </summary>
    private ApigeeWorkspace? _workspace;   

    /// <summary>
    /// JSON content for KVMs (Key Value Maps).
    /// </summary>
    private string _kvmJson = "[]";

    /// <summary>
    /// JSON content for caches.
    /// </summary>
    private string _cachesJson = "[]";

    /// <summary>
    /// JSON content for target servers.
    /// </summary>
    private string _targetServersJson = "[]";

    /// <summary>
    /// Indicates if the KVM JSON has unsaved changes.
    /// </summary>
    private bool _kvmIsDirty;

    /// <summary>
    /// Indicates if the caches JSON has unsaved changes.
    /// </summary>
    private bool _cachesIsDirty;

    /// <summary>
    /// Indicates if the target servers JSON has unsaved changes.
    /// </summary>
    private bool _targetIsDirty;

    /// <summary>
    /// List of environment names for the selected workspace.
    /// </summary>
    private readonly List<string> _environments = [];

    /// <summary>
    /// List of all available workspaces.
    /// </summary>
    private IReadOnlyList<ApigeeWorkspace> _workspaces = [];

    /// <summary>
    /// Repository for workspace and environment operations.
    /// </summary>
    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    /// <summary>
    /// Service for displaying toast notifications.
    /// </summary>
    [Inject] 
    public required ToastService Toast { get; set; }

    /// <summary>
    /// Indicates if no workspace is currently selected.
    /// </summary>
    public bool WorkspaceNotSelected => 
        _workspace == null;

    /// <summary>
    /// Loads the list of workspaces on component initialization.
    /// </summary>
    protected override void OnInitialized() => 
        _workspaces = WorkspaceRepo.ListAll();

    /// <summary>
    /// Handles workspace selection changes, loads environments for the selected workspace.
    /// </summary>
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

    /// <summary>
    /// Creates a new environment for the selected workspace and updates the environment list.
    /// </summary>
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

    /// <summary>
    /// Handles environment selection changes and loads configuration files for the selected environment.
    /// </summary>
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

    /// <summary>
    /// Gets the file system path for the selected environment.
    /// </summary>
    /// <returns>The environment path as a string.</returns>
    private string GetEnvPath() => 
        Path.Combine(_workspace!.RootPath, "environments", _selectedEnvironment);

    /// <summary>
    /// Loads the KVM (maps.json) file for the selected environment.
    /// </summary>
    private async Task LoadKvm()
    {
        var path = Path.Combine(GetEnvPath(), "maps.json");
        
        _kvmJson = File.Exists(path) ? await File.ReadAllTextAsync(path) : "[\n  {\n    \"name\": \"my-kvm\",\n    \"scope\": \"environment\",\n    \"encrypted\": false,\n    \"entries\": {\n      \"key1\": \"value1\"\n    }\n  }\n]";
        _kvmIsDirty = false;
    }

    /// <summary>
    /// Saves the KVM (maps.json) file for the selected environment.
    /// </summary>
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

    /// <summary>
    /// Loads the caches (caches.json) file for the selected environment.
    /// </summary>
    private async Task LoadCaches()
    {
        var path = Path.Combine(GetEnvPath(), "caches.json");
        
        _cachesJson = File.Exists(path) ? await File.ReadAllTextAsync(path) : "[\n  {\n    \"name\": \"my-cache\"\n  }\n]";
        _cachesIsDirty = false;
    }

    /// <summary>
    /// Saves the caches (caches.json) file for the selected environment.
    /// </summary>
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

    /// <summary>
    /// Loads the target servers (targetservers.json) file for the selected environment.
    /// </summary>
    private async Task LoadTargetServers()
    {
        var path = Path.Combine(GetEnvPath(), "targetservers.json");
        
        _targetServersJson = File.Exists(path) ? await File.ReadAllTextAsync(path) : "[\n  {\n    \"name\": \"my-target-server\",\n    \"host\": \"localhost\",\n    \"port\": 5037,\n    \"isEnabled\": true\n  }\n]";
        _targetIsDirty = false;
    }

    /// <summary>
    /// Saves the target servers (targetservers.json) file for the selected environment.
    /// </summary>
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