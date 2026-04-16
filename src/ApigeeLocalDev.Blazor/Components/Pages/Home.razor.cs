namespace ApigeeLocalDev.Blazor.Components.Pages;

public partial class Home : ComponentBase
{
    private bool _showCreate;
    private string _newName = string.Empty;
    private string _newPath = string.Empty;
    private string _createError = string.Empty;
    private ApigeeWorkspace? _pendingDelete;

    private IList<string> _proxyEntries = [];
    private IList<ApigeeWorkspace> _workspaces = [];

    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    [Inject]
    public required CreateWorkspaceUseCase CreateWorkspace { get; set; }


    protected override void OnInitialized()
        => _workspaces = [.. WorkspaceRepo.ListAll()];

    private void OpenCreateForm()
    {
        _showCreate = true; _newName = _newPath = _createError = string.Empty;
        _proxyEntries = [];
    }

    private void CloseCreateForm() 
    { 
        _showCreate = false; 
        _createError = string.Empty; 
    }

    private void AddProxyEntry() => 
        _proxyEntries.Add(string.Empty);
    
    private void RemoveProxyEntry(int idx) => 
        _proxyEntries.RemoveAt(idx);

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

    private void AskDelete(ApigeeWorkspace ws) => 
        _pendingDelete = ws;
    
    private void CancelDelete() =>
        _pendingDelete = null;

    private void ConfirmDelete()
    {
        if (_pendingDelete is null) 
            return;
        
        WorkspaceRepo.Delete(_pendingDelete);
        _workspaces.Remove(_pendingDelete);
        _pendingDelete = null;
    }
}