namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor page component for managing Apigee environment configuration files (KVMs, caches, target servers) per workspace and environment.
/// Allows the user to select a workspace, manage environments, and edit/save related configuration JSON files.
/// </summary>
public partial class EnvironmentConfig : ComponentBase, IDisposable
{
    private bool _disposed;

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
    /// Service for managing session state across navigations.
    /// </summary>
    [Inject]
    public required SessionStateService SessionState { get; set; }

    /// <summary>
    /// Indicates if no workspace is currently selected.
    /// </summary>
    public bool WorkspaceNotSelected =>
        _workspace == null;

    /// <summary>
    /// Loads the list of workspaces on component initialization.
    /// </summary>
    protected override void OnInitialized()
    {
        _workspaces = WorkspaceRepo.ListAll();

        if (SessionState.Has("envconfig:workspaceName"))
        {
            _selectedWorkspaceName = SessionState.Get<string>("envconfig:workspaceName") ?? "";
            _selectedEnvironment = SessionState.Get<string>("envconfig:environment") ?? "";

            var envs = SessionState.Get<List<string>>("envconfig:environments");
            if (envs != null)
            {
                _environments.Clear();
                _environments.AddRange(envs);
            }

            _kvmJson = SessionState.Get<string>("envconfig:kvmJson") ?? "[]";
            _cachesJson = SessionState.Get<string>("envconfig:cachesJson") ?? "[]";
            _targetServersJson = SessionState.Get<string>("envconfig:targetServersJson") ?? "[]";

            _kvmIsDirty = SessionState.Get<bool>("envconfig:kvmIsDirty");
            _cachesIsDirty = SessionState.Get<bool>("envconfig:cachesIsDirty");
            _targetIsDirty = SessionState.Get<bool>("envconfig:targetIsDirty");

            _workspace = _workspaces.FirstOrDefault(w => string.Equals(w.Name, _selectedWorkspaceName, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var globalWorkspace = SessionState.Get<string>("global:lastWorkspace");
            if (!string.IsNullOrEmpty(globalWorkspace))
            {
                _selectedWorkspaceName = globalWorkspace;
                OnWorkspaceChanged();
            }
        }
    }

    /// <summary>
    /// Handles workspace selection changes, loads environments for the selected workspace.
    /// </summary>
    private void OnWorkspaceChanged()
    {
        _workspace = _workspaces.FirstOrDefault(w => string.Equals(w.Name, _selectedWorkspaceName, StringComparison.OrdinalIgnoreCase));
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

    private void UpdateKvm(ChangeEventArgs e)
    {
        _kvmJson = e.Value?.ToString() ?? "";
        _kvmIsDirty = true;
    }

    private void UpdateCaches(ChangeEventArgs e)
    {
        _cachesJson = e.Value?.ToString() ?? "";
        _cachesIsDirty = true;
    }

    private void UpdateTargetServers(ChangeEventArgs e)
    {
        _targetServersJson = e.Value?.ToString() ?? "";
        _targetIsDirty = true;
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
            SessionState.Set("envconfig:workspaceName", _selectedWorkspaceName);
            SessionState.Set("envconfig:environment", _selectedEnvironment);
            SessionState.Set("envconfig:environments", _environments.ToList());
            SessionState.Set("envconfig:kvmJson", _kvmJson);
            SessionState.Set("envconfig:cachesJson", _cachesJson);
            SessionState.Set("envconfig:targetServersJson", _targetServersJson);
            SessionState.Set("envconfig:kvmIsDirty", _kvmIsDirty);
            SessionState.Set("envconfig:cachesIsDirty", _cachesIsDirty);
            SessionState.Set("envconfig:targetIsDirty", _targetIsDirty);

            if (!string.IsNullOrEmpty(_selectedWorkspaceName))
            {
                SessionState.Set("global:lastWorkspace", _selectedWorkspaceName);
            }
        }

        _disposed = true;
    }
}