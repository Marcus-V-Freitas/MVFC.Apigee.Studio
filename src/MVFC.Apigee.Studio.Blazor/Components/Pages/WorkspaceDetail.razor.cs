namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

public partial class WorkspaceDetail : ComponentBase, IAsyncDisposable
{
    private const string EditorId = "monaco-editor-container";

    private ApigeeWorkspace? _workspace;
    private WorkspaceItem? _tree;
    private string _searchQuery = string.Empty;

    // UI State
    private bool _showContextMenu;
    private double _contextMenuX;
    private double _contextMenuY;
    private WorkspaceItem? _contextMenuItem;
    private bool _saving;
    private bool _showNewItem;
    private bool _newItemIsDir;
    private string _newItemName = string.Empty;
    private string _newItemError = string.Empty;
    private WorkspaceItem? _targetDirContext;
    private ElementReference _newItemInput;

    // Quick Add drawer state
    private bool _quickAddOpen = false;
    private string _quickAddPath = string.Empty;
    private string _quickAddCategory = string.Empty;

    private MonacoEditor? _editor;

    [Parameter]
    public string WorkspaceName { get; set; } = string.Empty;

    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    [Inject]
    public required ToastService Toast { get; set; }

    [Inject]
    public required ApigeeLintService LintService { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    [Inject]
    public required EditorStateService EditorState { get; set; }

    private string NewItemPlaceholder => _newItemIsDir ? "folder-name" : "file.xml";
    
    protected override async Task OnInitializedAsync()
    {
        _workspace = WorkspaceRepo.ListAll().FirstOrDefault(w => w.Name == WorkspaceName);

        if (_workspace is not null)
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);

        // State is managed by EditorStateService, but we reset it on first load of this workspace
        // Usually, we might want to persist tabs, but for now we follow original logic of clearing.
        EditorState.Reset();
    }

    private string DetectLanguage()
    {
        if (EditorState.ActiveTab is null) return "";
        var ext = Path.GetExtension(EditorState.ActiveTab.FullPath).TrimStart('.').ToLower();
        return ext switch
        {
            "xml" => "XML",
            "json" => "JSON",
            "js" => "JavaScript",
            "yaml" or "yml" => "YAML",
            "md" => "Markdown",
            "css" => "CSS",
            "html" => "HTML",
            _ => ext.ToUpper()
        };
    }

    private async Task SwitchTab(EditorTab tab)
    {
        if (EditorState.ActiveTab == tab) return;

        if (EditorState.ActiveTab is not null && _editor is not null)
        {
            var content = await _editor.GetValue();
            var isDirty = await _editor.IsDirty();
            EditorState.UpdateActiveTabContent(content, isDirty);
        }

        EditorState.SwitchToTab(tab);
        StateHasChanged();
    }

    private async Task CloseTab(EditorTab tab)
    {
        var isCurrent = EditorState.ActiveTab == tab;
        if (tab.IsDirty)
        {
            if (isCurrent && _editor is not null) 
                tab.IsDirty = await _editor.IsDirty();

            if (tab.IsDirty)
            {
                var discard = await JS.InvokeAsync<bool>("confirm", $"A aba '{tab.FileName}' tem alterações não salvas. Descartar?");
                if (!discard) return;
            }
        }

        EditorState.CloseTab(tab);
        StateHasChanged();
    }

    private async Task LoadFile(string path)
    {
        var existing = EditorState.OpenTabs.FirstOrDefault(t => t.FullPath == path);
        if (existing is not null)
        {
            await SwitchTab(existing);
            return;
        }

        if (EditorState.ActiveTab is not null && _editor is not null)
        {
            var currentContent = await _editor.GetValue();
            var currentlyDirty = await _editor.IsDirty();
            EditorState.UpdateActiveTabContent(currentContent, currentlyDirty);
        }

        var content = await WorkspaceRepo.ReadFileAsync(path);
        EditorState.OpenTab(path, content);
        StateHasChanged();
    }

    private async Task SaveFile()
    {
        if (EditorState.ActiveTab is null || _saving || _editor is null) return;
        
        _saving = true;
        StateHasChanged();
        
        try
        {
            var content = await _editor.GetValue();
            await WorkspaceRepo.SaveFileAsync(EditorState.ActiveTab.FullPath, content);
            await _editor.ClearDirty();
            EditorState.UpdateActiveTabContent(content, false);
            Toast.ShowSuccess("✔ Arquivo salvo com sucesso!");

            if (_workspace != null && EditorState.ActiveTab.FullPath.EndsWith(".xml"))
            {
                var lintResults = await LintService.RunLintAsync(_workspace);
                var activeFileLint = lintResults.FirstOrDefault(r => r.FilePath.Replace("\\", "/").EndsWith(EditorState.ActiveTab.FileName));
                await _editor.SetMarkers(activeFileLint?.Messages ?? (IEnumerable<object>)Array.Empty<object>());
            }
        }
        catch (Exception ex)
        {
            Toast.ShowError("Erro ao salvar: " + ex.Message);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task FormatDocument()
    {
        if (_editor is not null) await _editor.FormatDocument();
    }

    private async Task DeleteSelectedFile()
    {
        if (EditorState.ActiveTab is null || _workspace is null) return;
        
        var fileName = EditorState.ActiveTab.FileName;
        var confirm = await JS.InvokeAsync<bool>("confirm", $"Remover arquivo '{fileName}'?");
        if (!confirm) return;

        await WorkspaceRepo.DeleteFileAsync(EditorState.ActiveTab.FullPath);
        EditorState.CloseTab(EditorState.ActiveTab);
        _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        Toast.ShowSuccess($"✔ Arquivo '{fileName}' removido.");
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        // MonacoEditor handles its own disposal
    }

    private void HandleContextMenu((MouseEventArgs e, WorkspaceItem item) args)
    {
        _contextMenuX = args.e.ClientX;
        _contextMenuY = args.e.ClientY;
        _contextMenuItem = args.item;
        _showContextMenu = true;
    }

    private void CloseContextMenu() => _showContextMenu = false;

    private void ContextAdd(bool isDir)
    {
        _showContextMenu = false;
        OpenNewItemDialog(isDir, _contextMenuItem);
    }

    private async Task ContextDelete()
    {
        _showContextMenu = false;
        if (_contextMenuItem is null || _workspace is null) return;

        var fileName = _contextMenuItem.Name;
        var confirm = await JS.InvokeAsync<bool>("confirm", $"Remover '{fileName}'?");
        if (!confirm) return;

        if (_contextMenuItem.Type is WorkspaceItemType.Directory or WorkspaceItemType.Environment or WorkspaceItemType.ApiProxy or WorkspaceItemType.SharedFlow)
        {
            await WorkspaceRepo.DeleteDirectoryAsync(_contextMenuItem.FullPath);
        }
        else
        {
            await WorkspaceRepo.DeleteFileAsync(_contextMenuItem.FullPath);
            var tab = EditorState.OpenTabs.FirstOrDefault(t => t.FullPath == _contextMenuItem.FullPath);
            if (tab is not null) EditorState.CloseTab(tab);
        }

        _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        Toast.ShowSuccess($"✔ '{fileName}' removido.");
        StateHasChanged();
    }

    private void OpenNewItemDialog(bool isDir) => OpenNewItemDialog(isDir, null);

    private void OpenNewItemDialog(bool isDir, WorkspaceItem? targetDir = null)
    {
        _targetDirContext = targetDir;
        _newItemIsDir = isDir;
        _newItemName = _newItemError = string.Empty;
        _showNewItem = true;
    }

    private async Task HandleNewItemKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await ConfirmNewItem();
        if (e.Key == "Escape") _showNewItem = false;
    }

    private async Task ConfirmNewItem()
    {
        _newItemError = string.Empty;
        if (string.IsNullOrWhiteSpace(_newItemName)) { _newItemError = "Informe um nome."; return; }
        if (_workspace is null) return;

        var basePath = _targetDirContext is not null
            ? (_targetDirContext.Type == WorkspaceItemType.File ? Path.GetDirectoryName(_targetDirContext.FullPath)! : _contextMenuItem!.FullPath)
            : (EditorState.ActiveTab is not null ? Path.GetDirectoryName(EditorState.ActiveTab.FullPath)! : _workspace.RootPath);

        var fullPath = Path.Combine(basePath, _newItemName);

        try
        {
            if (_newItemIsDir) await WorkspaceRepo.CreateDirectoryAsync(fullPath);
            else 
            { 
                await WorkspaceRepo.CreateFileAsync(fullPath); 
                await LoadFile(fullPath); 
            }

            _showNewItem = false;
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        }
        catch (Exception ex) { _newItemError = ex.Message; }
    }

    private void OpenQuickAddModal((string Path, string Category) args)
    {
        _quickAddPath = args.Path;
        _quickAddCategory = args.Category;
        _quickAddOpen = true;
    }

    private void CloseQuickAdd() => _quickAddOpen = false;

    private async Task OnItemCreated(string? path)
    {
        if (_workspace is not null)
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        
        if (!string.IsNullOrEmpty(path))
            await LoadFile(path);
        
        StateHasChanged();
    }
}
