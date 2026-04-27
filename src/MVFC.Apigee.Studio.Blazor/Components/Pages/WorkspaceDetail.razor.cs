namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

/// <summary>
/// Blazor page component for browsing and editing files in an Apigee workspace.
/// Provides a file tree, Monaco editor integration, context menu actions, and quick add features for files and folders.
/// </summary>
public partial class WorkspaceDetail : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// The HTML element ID for the Monaco editor container.
    /// </summary>
    private const string EditorId = "monaco-editor-container";

    /// <summary>
    /// The currently loaded workspace.
    /// </summary>
    private ApigeeWorkspace? _workspace;

    /// <summary>
    /// The root of the workspace file tree.
    /// </summary>
    private WorkspaceItem? _tree;

    /// <summary>
    /// The current search query for filtering the file tree.
    /// </summary>
    private string _searchQuery = string.Empty;

    // UI State
    /// <summary>
    /// Indicates if the context menu is visible.
    /// </summary>
    private bool _showContextMenu;

    /// <summary>
    /// X coordinate for the context menu position.
    /// </summary>
    private double _contextMenuX;

    /// <summary>
    /// Y coordinate for the context menu position.
    /// </summary>
    private double _contextMenuY;

    /// <summary>
    /// The workspace item associated with the context menu.
    /// </summary>
    private WorkspaceItem? _contextMenuItem;

    /// <summary>
    /// Indicates if a file is currently being saved.
    /// </summary>
    private bool _saving;

    /// <summary>
    /// Indicates if the new item dialog is visible.
    /// </summary>
    private bool _showNewItem;

    /// <summary>
    /// Indicates if the new item to be created is a directory.
    /// </summary>
    private bool _newItemIsDir;

    /// <summary>
    /// The name for the new file or directory.
    /// </summary>
    private string _newItemName = string.Empty;

    /// <summary>
    /// The error message for new item creation.
    /// </summary>
    private string _newItemError = string.Empty;

    /// <summary>
    /// The directory context for creating a new item.
    /// </summary>
    private WorkspaceItem? _targetDirContext;

    /// <summary>
    /// Reference to the input element for the new item dialog.
    /// </summary>
    private ElementReference _newItemInput;

    // Quick Add drawer state
    /// <summary>
    /// Indicates if the quick add drawer is open.
    /// </summary>
    private bool _quickAddOpen;

    /// <summary>
    /// The path of the directory selected in the new item modal dropdown.
    /// </summary>
    private string? _selectedNewItemPath;

    /// <summary>
    /// The path for the quick add operation.
    /// </summary>
    private string _quickAddPath = string.Empty;

    /// <summary>
    /// The category for the quick add operation.
    /// </summary>
    private string _quickAddCategory = string.Empty;

    /// <summary>
    /// Reference to the Monaco editor instance.
    /// </summary>
    private MonacoEditor? _editor;

    /// <summary>
    /// The name of the workspace to load (from route parameter).
    /// </summary>
    [Parameter]
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>
    /// Repository for workspace and file operations.
    /// </summary>
    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    /// <summary>
    /// Service for displaying toast notifications.
    /// </summary>
    [Inject]
    public required ToastService Toast { get; set; }

    /// <summary>
    /// JavaScript runtime for interop calls.
    /// </summary>
    [Inject]
    public required IJSRuntime JS { get; set; }

    /// <summary>
    /// Service for managing editor tab state.
    /// </summary>
    [Inject]
    public required EditorStateService EditorState { get; set; }

    /// <summary>
    /// Service for managing session state across navigations.
    /// </summary>
    [Inject]
    public required SessionStateService SessionState { get; set; }

    /// <summary>
    /// Gets the placeholder text for the new item input based on type.
    /// </summary>
    private string NewItemPlaceholder => _newItemIsDir ? "folder-name" : "file.xml";

    /// <summary>
    /// Loads the workspace and file tree on initialization and resets editor state.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        _workspace = WorkspaceRepo.ListAll().FirstOrDefault(w => string.Equals(w.Name, WorkspaceName, StringComparison.OrdinalIgnoreCase));

        if (_workspace is not null)
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);

        var wsKey = $"workspace:{WorkspaceName}";
        SessionState.Set("global:lastWorkspace", WorkspaceName);
        
        if (SessionState.Has($"{wsKey}:tabs"))
        {
            var tabs = SessionState.Get<List<EditorTab>>($"{wsKey}:tabs") ?? [];
            var activeTab = SessionState.Get<string>($"{wsKey}:activeTab");
            _searchQuery = SessionState.Get<string>($"{wsKey}:search") ?? string.Empty;
            EditorState.RestoreTabs(tabs, activeTab);
        }
        else
        {
            EditorState.Reset();
        }

        EditorState.OnTabsChanged += HandleTabsChanged;
    }

    private async Task RefreshTree()
    {
        if (_workspace is not null)
        {
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
            StateHasChanged();
        }
    }

    private void HandleTabsChanged()
    {
        _ = InvokeAsync(async () =>
        {
            if (EditorState.ActiveTab is not null && _editor is not null)
            {
                var editorValue = await _editor.GetValue();
                if (!string.Equals(editorValue, EditorState.ActiveTab.Content, StringComparison.Ordinal))
                {
                    await _editor.SetValue(EditorState.ActiveTab.Content, EditorState.ActiveTab.FullPath);
                }
            }
            StateHasChanged();
        });
    }

    /// <summary>
    /// Executes actions after the component has rendered.
    /// If the new item modal is open, automatically focuses the input field for better user experience.
    /// </summary>
    /// <param name="firstRender">Indicates whether this is the first render of the component.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_showNewItem)
        {
            await Task.Yield();
            await _newItemInput.FocusAsync();
        }
    }

    /// <summary>
    /// Detects the language for the Monaco editor based on the active tab's file extension.
    /// </summary>
    /// <returns>The language name as a string.</returns>
    private string DetectLanguage()
    {
        if (EditorState.ActiveTab is null)
            return "";

        var ext = Path.GetExtension(EditorState.ActiveTab.FullPath).TrimStart('.').ToLower(CultureInfo.InvariantCulture);
        return ext switch
        {
            "xml" => "XML",
            "json" => "JSON",
            "js" => "JavaScript",
            "yaml" or "yml" => "YAML",
            "md" => "Markdown",
            "css" => "CSS",
            "html" => "HTML",
            _ => ext.ToUpperInvariant(),
        };
    }

    /// <summary>
    /// Switches to the specified editor tab, saving the current tab's content if necessary.
    /// </summary>
    /// <param name="tab">The tab to switch to.</param>
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

    /// <summary>
    /// Closes the specified editor tab, prompting to discard unsaved changes if necessary.
    /// </summary>
    /// <param name="tab">The tab to close.</param>
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

    /// <summary>
    /// Closes all tabs except the specified one, prompting for unsaved changes if needed.
    /// </summary>
    private async Task CloseOtherTabs(EditorTab keep)
    {
        if (EditorState.ActiveTab is not null && _editor is not null)
        {
            EditorState.ActiveTab.IsDirty = await _editor.IsDirty();
            EditorState.ActiveTab.Content = await _editor.GetValue();
        }

        var toClose = EditorState.OpenTabs.Where(t => t != keep).ToList();
        var dirtyTabs = toClose.Where(t => t.IsDirty).ToList();
        if (dirtyTabs.Count != 0)
        {
            var discard = await JS.InvokeAsync<bool>("confirm", $"Existem {dirtyTabs.Count} aba(s) com alterações não salvas. Descartar?");
            if (!discard) return;
        }

        EditorState.CloseOtherTabs(keep);
        StateHasChanged();
    }

    /// <summary>
    /// Closes all tabs, prompting for unsaved changes if needed.
    /// </summary>
    private async Task CloseAllTabs()
    {
        if (EditorState.ActiveTab is not null && _editor is not null)
        {
            EditorState.ActiveTab.IsDirty = await _editor.IsDirty();
            EditorState.ActiveTab.Content = await _editor.GetValue();
        }

        var dirtyTabs = EditorState.OpenTabs.Where(t => t.IsDirty).ToList();
        if (dirtyTabs.Count != 0)
        {
            var discard = await JS.InvokeAsync<bool>("confirm", $"Existem {dirtyTabs.Count} aba(s) com alterações não salvas. Descartar?");
            if (!discard) return;
        }

        EditorState.CloseAllTabs();
        StateHasChanged();
    }

    /// <summary>
    /// Loads a file into a new editor tab, or switches to the tab if already open.
    /// </summary>
    /// <param name="path">The file path to load.</param>
    private async Task LoadFile(string path)
    {
        var existing = EditorState.OpenTabs.FirstOrDefault(t => string.Equals(t.FullPath, path, StringComparison.OrdinalIgnoreCase));
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

    /// <summary>
    /// Saves the currently active file in the editor and runs lint checks if applicable.
    /// </summary>
    private async Task SaveFile()
    {
        if (EditorState.ActiveTab is null || _saving || _editor is null)
            return;

        _saving = true;
        StateHasChanged();

        try
        {
            var content = await _editor.GetValue();
            await WorkspaceRepo.SaveFileAsync(EditorState.ActiveTab.FullPath, content);
            await _editor.ClearDirty();
            EditorState.UpdateActiveTabContent(content, isDirty: false);
            Toast.ShowSuccess("✔ Arquivo salvo com sucesso!");

            if (_workspace != null && EditorState.ActiveTab.FullPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                var lintResults = await ApigeeLintService.RunLintAsync(_workspace, EditorState.ActiveTab.FullPath);
                var activeFileLint = lintResults.FirstOrDefault();
                await _editor.SetMarkers(activeFileLint?.Messages ?? (IEnumerable<object>)[]);
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

    /// <summary>
    /// Formats the current document in the Monaco editor.
    /// </summary>
    private async Task FormatDocument()
    {
        if (_editor is not null)
            await _editor.FormatDocument();
    }

    /// <summary>
    /// Deletes the currently active file and closes its tab.
    /// </summary>
    private async Task DeleteSelectedFile()
    {
        if (EditorState.ActiveTab is null || _workspace is null)
            return;

        var fileName = EditorState.ActiveTab.FileName;
        var confirm = await JS.InvokeAsync<bool>("confirm", $"Remover arquivo '{fileName}'?");
        if (!confirm) return;

        await WorkspaceRepo.DeleteFileAsync(EditorState.ActiveTab.FullPath);
        EditorState.CloseTab(EditorState.ActiveTab);
        _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        Toast.ShowSuccess($"✔ Arquivo '{fileName}' removido.");
    }

    /// <summary>
    /// Disposes resources when the component is disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        EditorState.OnTabsChanged -= HandleTabsChanged;

        if (EditorState.ActiveTab is not null && _editor is not null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                var isDirtyTask = _editor.IsDirty();
                var contentTask = _editor.GetValue();

                if (await Task.WhenAny(Task.WhenAll(isDirtyTask, contentTask), Task.Delay(500, cts.Token)) == Task.WhenAll(isDirtyTask, contentTask))
                {
                    EditorState.ActiveTab.IsDirty = isDirtyTask.Result;
                    EditorState.ActiveTab.Content = contentTask.Result;
                }
            }
            catch { /* ignore JS disconnected errors during disposal */ }
        }

        var wsKey = $"workspace:{WorkspaceName}";
        SessionState.Set($"{wsKey}:tabs", EditorState.OpenTabs.ToList());
        SessionState.Set($"{wsKey}:activeTab", EditorState.ActiveTab?.FullPath);
        SessionState.Set($"{wsKey}:search", _searchQuery);
    }

    /// <summary>
    /// Handles the context menu event for a workspace item.
    /// </summary>
    /// <param name="args">Tuple containing mouse event args and the workspace item.</param>
    private void HandleContextMenu((MouseEventArgs e, WorkspaceItem item) args)
    {
        _contextMenuX = args.e.ClientX;
        _contextMenuY = args.e.ClientY;
        _contextMenuItem = args.item;
        _showContextMenu = true;
    }

    /// <summary>
    /// Closes the context menu.
    /// </summary>
    private void CloseContextMenu() => _showContextMenu = false;

    /// <summary>
    /// Opens the new item dialog from the context menu.
    /// </summary>
    /// <param name="isDir">Whether the new item is a directory.</param>
    private void ContextAdd(bool isDir)
    {
        _showContextMenu = false;
        OpenNewItemDialogWorspace(isDir, _contextMenuItem);
    }

    /// <summary>
    /// Deletes the selected file or directory from the context menu.
    /// </summary>
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
            var tab = EditorState.OpenTabs.FirstOrDefault(t => string.Equals(t.FullPath, _contextMenuItem.FullPath, StringComparison.OrdinalIgnoreCase));
            if (tab is not null) EditorState.CloseTab(tab);
        }

        _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        Toast.ShowSuccess($"✔ '{fileName}' removido.");
        StateHasChanged();
    }

    /// <summary>
    /// Opens the new item dialog for creating a file or directory.
    /// </summary>
    /// <param name="isDir">Whether the new item is a directory.</param>
    private void OpenNewItemDialog(bool isDir) => OpenNewItemDialogWorspace(isDir, targetDir: null);

    /// <summary>
    /// Opens the new item dialog for a specific directory context.
    /// </summary>
    /// <param name="isDir">Whether the new item is a directory.</param>
    /// <param name="targetDir">The target directory context.</param>
    private void OpenNewItemDialogWorspace(bool isDir, WorkspaceItem? targetDir = null)
    {
        _targetDirContext = targetDir;
        _newItemIsDir = isDir;
        _newItemName = _newItemError = string.Empty;
        _selectedNewItemPath = null;
        _showNewItem = true;
    }

    /// <summary>
    /// Handles keyboard events in the new item dialog.
    /// </summary>
    /// <param name="e">The keyboard event arguments.</param>
    private async Task HandleNewItemKey(KeyboardEventArgs e)
    {
        if (string.Equals(e.Key, "Enter", StringComparison.OrdinalIgnoreCase)) await ConfirmNewItem();
        if (string.Equals(e.Key, "Escape", StringComparison.OrdinalIgnoreCase)) _showNewItem = false;
    }

    /// <summary>
    /// Confirms and creates the new file or directory.
    /// </summary>
    private async Task ConfirmNewItem()
    {
        _newItemError = string.Empty;
        if (string.IsNullOrWhiteSpace(_newItemName)) { _newItemError = "Informe um nome."; return; }
        if (_workspace is null) return;

        var basePath = GetBasePathForNewItem();
        var fullPath = Path.Combine(basePath, _newItemName);

        try
        {
            if (_newItemIsDir)
            {
                if (Directory.Exists(fullPath))
                {
                    Toast.ShowError("Uma pasta com este nome já existe neste local.");
                    return;
                }
                await WorkspaceRepo.CreateDirectoryAsync(fullPath);
            }
            else
            {
                if (File.Exists(fullPath)) 
                {
                    Toast.ShowError("Um arquivo com este nome já existe neste local."); 
                    return; 
                }
                await WorkspaceRepo.CreateFileAsync(fullPath); await LoadFile(fullPath);
            }

            _showNewItem = false;
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        }
        catch (Exception ex) 
        { 
            _newItemError = ex.Message; 
        }
    }

    /// <summary>
    /// Determines the base path where a new file or directory should be created,
    /// based on the current dialog context (target directory, context menu item, or active tab).
    /// If a target directory context is set and is a file, returns the directory of that file.
    /// If it is a directory, returns the full path of the context menu item.
    /// Otherwise, uses the directory of the active tab or, if none, the workspace root path.
    /// </summary>
    /// <returns>The base path for creating the new item.</returns>
    private string GetBasePathForNewItem()
    {
        if (!string.IsNullOrEmpty(_selectedNewItemPath))
            return _selectedNewItemPath;

        if (_targetDirContext is not null)
        {
            if (_targetDirContext.Type == WorkspaceItemType.File)
                return Path.GetDirectoryName(_targetDirContext.FullPath)!;
            return _contextMenuItem!.FullPath;
        }

        if (EditorState.ActiveTab is not null)
            return Path.GetDirectoryName(EditorState.ActiveTab.FullPath)!;

        return _workspace!.RootPath;
    }

    /// <summary>
    /// Opens the quick add modal for a given path and category.
    /// </summary>
    /// <param name="args">Tuple containing the path and category.</param>
    private void OpenQuickAddModal((string Path, string Category) args)
    {
        _quickAddPath = args.Path;
        _quickAddCategory = args.Category;
        _quickAddOpen = true;
    }

    /// <summary>
    /// Closes the quick add modal.
    /// </summary>
    private void CloseQuickAdd() => _quickAddOpen = false;

    /// <summary>
    /// Handles the event when a new item is created via quick add.
    /// </summary>
    /// <param name="path">The path of the created item.</param>
    private async Task OnItemCreated(string? path)
    {
        if (_workspace is not null)
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);

        if (!string.IsNullOrEmpty(path))
            await LoadFile(path);

        StateHasChanged();
    }

    /// <summary>
    /// Recursively collects all directory paths in the workspace tree.
    /// </summary>
    /// <param name="node">The current workspace item node.</param>
    private static IEnumerable<WorkspaceItem> CollectDirectories(WorkspaceItem node)
    {
        if (node.Type is WorkspaceItemType.Directory or WorkspaceItemType.ApiProxy or WorkspaceItemType.SharedFlow or WorkspaceItemType.Environment)
        {
            yield return node;
        }

        foreach (var child in node.Children)
        {
            foreach (var childDir in CollectDirectories(child))
            {
                yield return childDir;
            }
        }
    }
}