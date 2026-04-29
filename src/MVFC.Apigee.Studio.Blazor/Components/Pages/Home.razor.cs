namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor page component for managing Apigee workspaces.
/// Allows users to create new workspaces (with optional proxies and custom path), list existing workspaces,
/// and delete workspaces with confirmation.
/// </summary>
public partial class Home : ComponentBase
{
    /// <summary>
    /// Indicates if the create workspace form is visible.
    /// </summary>
    private bool _showCreate;

    /// <summary>
    /// The name for the new workspace to be created.
    /// </summary>
    private string _newName = string.Empty;

    /// <summary>
    /// The custom path for the new workspace (optional).
    /// </summary>
    private string _newPath = string.Empty;

    /// <summary>
    /// Stores the last error message from the create workspace operation.
    /// </summary>
    private string _createError = string.Empty;

    /// <summary>
    /// The workspace pending deletion (for confirmation dialog).
    /// </summary>
    private ApigeeWorkspace? _pendingDelete;

    /// <summary>
    /// List of proxy names to be created with the new workspace.
    /// </summary>
    private IList<string> _proxyEntries = [];

    /// <summary>
    /// List of all Apigee workspaces.
    /// </summary>
    private IList<ApigeeWorkspace> _workspaces = [];

    /// <summary>
    /// Repository for workspace operations (list, create, delete).
    /// </summary>
    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    /// <summary>
    /// Application configuration.
    /// </summary>
    [Inject]
    public required IConfiguration Config { get; set; }

    /// <summary>
    /// Toast notification service.
    /// </summary>
    [Inject]
    public required ToastService Toast { get; set; }

    /// <summary>
    /// The global directory path where new workspaces are created.
    /// </summary>
    private string _workspacesRoot = string.Empty;

    /// <summary>
    /// Use case for creating a new workspace with optional proxies and custom path.
    /// </summary>
    [Inject]
    public required CreateWorkspaceUseCase CreateWorkspace { get; set; }

    /// <summary>
    /// Loads the list of workspaces on component initialization.
    /// </summary>
    protected override void OnInitialized()
    {
        _workspacesRoot = Config["WorkspacesRoot"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "apigee-workspaces");
        _workspaces = [.. WorkspaceRepo.ListAll()];
    }

    /// <summary>
    /// Saves the global workspaces root path to appsettings.json.
    /// </summary>
    private async Task SaveWorkspacesRoot()
    {
        try
        {
            var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            JsonObject root;

            if (File.Exists(appSettingsPath))
            {
                var json = await File.ReadAllTextAsync(appSettingsPath);
                root = JsonNode.Parse(json) as JsonObject ?? [];
            }
            else
            {
                root = [];
            }

            root["WorkspacesRoot"] = _workspacesRoot;

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(appSettingsPath, root.ToJsonString(options));

            // Wait briefly for IConfiguration file watcher to catch the change
            await Task.Delay(500);

            Toast.ShowSuccess("Pasta raiz atualizada com sucesso!");
            _workspaces = [.. WorkspaceRepo.ListAll()];
        }
        catch (Exception ex)
        {
            Toast.ShowError("Erro ao salvar a pasta: " + ex.Message);
        }
    }

    /// <summary>
    /// Toggles the create workspace form. Closes the import form if open.
    /// </summary>
    private void OpenCreateForm()
    {
        if (_showCreate)
        {
            CloseCreateForm();
        }
        else
        {
            _showCreate = true;
            _newName = _newPath = _createError = string.Empty;
            _proxyEntries = [];
        }
    }

    /// <summary>
    /// Closes the create workspace form and clears error messages.
    /// </summary>
    private void CloseCreateForm()
    {
        _showCreate = false;
        _createError = string.Empty;
    }

    /// <summary>
    /// Adds a new empty proxy entry to the list for workspace creation.
    /// </summary>
    private void AddProxyEntry() =>
        _proxyEntries.Add(string.Empty);

    /// <summary>
    /// Removes a proxy entry at the specified index from the list.
    /// </summary>
    /// <param name="idx">The index of the proxy entry to remove.</param>
    private void RemoveProxyEntry(int idx) =>
        _proxyEntries.RemoveAt(idx);

    /// <summary>
    /// Handles the creation of a new workspace using the provided name, path, and proxies.
    /// Updates the workspace list and closes the form on success.
    /// </summary>
    private void HandleCreate()
    {
        _createError = string.Empty;

        try
        {
            var customPath = string.IsNullOrWhiteSpace(_newPath) ? null : _newPath;
            var initialProxies = _proxyEntries.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            var ws = CreateWorkspace.Execute(_newName, customPath, initialProxies);

            _workspaces.Add(ws);
            CloseCreateForm();
        }
        catch (Exception ex)
        {
            _createError = ex.Message;
        }
    }

    /// <summary>
    /// Sets the workspace to be deleted (shows confirmation dialog).
    /// </summary>
    /// <param name="ws">The workspace to delete.</param>
    private void AskDelete(ApigeeWorkspace ws) =>
        _pendingDelete = ws;

    /// <summary>
    /// Cancels the workspace deletion operation.
    /// </summary>
    private void CancelDelete() =>
        _pendingDelete = null;

    /// <summary>
    /// Confirms and deletes the selected workspace, updating the workspace list.
    /// </summary>
    private void ConfirmDelete()
    {
        if (_pendingDelete is null)
            return;

        WorkspaceRepo.Delete(_pendingDelete);
        _workspaces.Remove(_pendingDelete);
        _pendingDelete = null;
    }

}