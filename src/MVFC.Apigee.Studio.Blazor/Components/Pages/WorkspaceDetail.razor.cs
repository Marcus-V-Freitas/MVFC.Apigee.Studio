using MVFC.Apigee.Studio.Domain.Enums;

namespace MVFC.Apigee.Studio.Blazor.Components.Pages;

public partial class WorkspaceDetail : ComponentBase, IAsyncDisposable
{
    private const string EditorId = "monaco-editor-container";
    private const string DeployModeProxy = "proxy";
    private const string DeployModeFull = "full";

    private class OpenFileTab
    {
        public string FullPath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FullPath);
        public string Content { get; set; } = string.Empty;
        public bool IsDirty { get; set; }
    }

    private ApigeeWorkspace? _workspace;
    private WorkspaceItem? _tree;

    private readonly List<OpenFileTab> _openTabs = [];
    private OpenFileTab? _activeTab;

    private string _searchQuery = string.Empty;

    // Context menu state
    private bool _showContextMenu;
    private double _contextMenuX;
    private double _contextMenuY;
    private WorkspaceItem? _contextMenuItem;

    // true quando o div do editor já existe no DOM e aguarda monacoInterop.create.
    private bool _editorPendingCreate = false;
    private bool _editorCreated = false;

    // dirty state / saving
    private bool _saving;

    private bool _showNewItem;
    private bool _newItemIsDir;
    private string _newItemName = string.Empty;
    private string _newItemError = string.Empty;
    private WorkspaceItem? _targetDirContext;

    private string _deployTarget = string.Empty;
    private string _deployEnv = "local";
    private string _deployMode = DeployModeFull;
    private bool _deploying;
    private string _deployMessage = string.Empty;
    private bool _deployError;

    private bool _quickAddOpen = false;
    private string _quickAddPath = string.Empty;
    private string _quickAddCategory = string.Empty;
    private IReadOnlyList<PolicyTemplate> _allTemplates = [];
    private PolicyTemplate? _quickTemplate;
    private Dictionary<string, string> _quickParams = [];
    private string _quickFileName = string.Empty;
    private string _quickMessage = string.Empty;
    private string _quickXmlEditable = string.Empty;
    private bool _quickError;
    private bool _quickGenerating;

    [Parameter]
    public string WorkspaceName { get; set; } = string.Empty;

    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    [Inject]
    public required DeployToEmulatorUseCase DeployUseCase { get; set; }

    [Inject]
    public required IPolicyTemplateRepository TemplateRepo { get; set; }

    [Inject]
    public required GeneratePolicyUseCase GeneratePolicy { get; set; }

    [Inject]
    public required ToastService Toast { get; set; }

    [Inject]
    public required ApigeeLintService LintService { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    private string NewItemPlaceholder => 
        _newItemIsDir ? "folder-name" : "file.xml";
    
    private string DeployMessageClass => 
        _deployError ? "error-text" : "success-text";
    
    private string QuickMessageClass => 
        _quickError ? "error-text" : "success-text";
    
    private string TabClass(string m) => 
        _deployMode == m ? "btn btn-primary btn-sm" : "btn btn-ghost btn-sm";

    private void SetDeployModeProxy() => 
        _deployMode = DeployModeProxy;
    
    private void SetDeployModeFull() => 
        _deployMode = DeployModeFull;

    private string DetectLanguage()
    {
        if (_activeTab is null) 
            return "";
        
        var ext = Path.GetExtension(_activeTab.FullPath).TrimStart('.').ToLower();
        
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

    private WorkspaceItem? FilterTree(WorkspaceItem? root)
    {
        if (root is null) 
            return null;
        
        if (string.IsNullOrWhiteSpace(_searchQuery)) 
            return root;

        return FilterNode(root, _searchQuery);
    }

    private static WorkspaceItem? FilterNode(WorkspaceItem node, string query)
    {
        // Se for arquivo e combinar com a busca, retorna o nó
        if (node.Type is WorkspaceItemType.File)
        {
            if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return node;
            return null;
        }

        // Se for pasta, filtra os filhos
        var filteredChildren = new List<WorkspaceItem>();
        foreach (var child in node.Children)
        {
            var filteredChild = FilterNode(child, query);
            if (filteredChild is not null)
                filteredChildren.Add(filteredChild);
        }

        // Se a pasta tiver filhos ou se o próprio nome da pasta combinar com a busca
        if (filteredChildren.Count != 0 || node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return new WorkspaceItem(
                node.Name,
                node.FullPath,
                node.RelativePath,
                node.Type,
                filteredChildren
            );
        }

        return null;
    }

    protected override async Task OnInitializedAsync()
    {
        _workspace = WorkspaceRepo.ListAll().FirstOrDefault(w => w.Name == WorkspaceName);
        _allTemplates = TemplateRepo.GetAll();

        if (_workspace is not null)
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);

        // Reseta o estado do editor a cada visita à página.
        // No Blazor Server, OnInitializedAsync re-executa quando o usuário navega
        // para outra página e volta, MAS os campos de instância sobrevivem (mesma
        // instância do componente no circuito SignalR). 
        _openTabs.Clear();
        _activeTab = null;
        _editorPendingCreate = false;
        _editorCreated = false;
        _saving = false;
        _searchQuery = string.Empty;
    }

    // OnAfterRenderAsync é chamado após CADA render.
    // _editorPendingCreate garante que o monacoInterop.create só é chamado
    // depois que o Blazor colocou o div#monaco-editor-container no DOM.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_editorPendingCreate || _activeTab is null)
        {
            return;
        }

        _editorPendingCreate = false;
        _editorCreated = true;
        await JS.InvokeVoidAsync(
            "monacoInterop.create",
            EditorId,
            _activeTab.Content,
            _activeTab.FullPath);
    }

    private async Task SwitchTab(OpenFileTab tab)
    {
        if (_activeTab == tab) 
            return;

        // Antes de trocar, salva o conteúdo do anterior em memória
        if (_activeTab is not null && _editorCreated)
        {
            _activeTab.Content = await JS.InvokeAsync<string>("monacoInterop.getValue", EditorId);
            _activeTab.IsDirty = await JS.InvokeAsync<bool>("monacoInterop.isDirty", EditorId);
        }

        _activeTab = tab;

        if (!_editorCreated)
        {
            _editorPendingCreate = true;
        }
        else
        {
            await JS.InvokeVoidAsync("monacoInterop.setValue", EditorId, tab.Content, tab.FullPath);
            if (!tab.IsDirty) await JS.InvokeVoidAsync("monacoInterop.clearDirty", EditorId);
        }
        StateHasChanged();
    }

    private async Task CloseTab(OpenFileTab tab)
    {
        var isCurrent = _activeTab == tab;
        if (tab.IsDirty)
        {
            // Precisamos confirmar se queremos fechar e perder dados
            // Como tab.IsDirty não reflete instantaneamente, usamos o do editor
            if (isCurrent) tab.IsDirty = await JS.InvokeAsync<bool>("monacoInterop.isDirty", EditorId);

            if (tab.IsDirty)
            {
                var discard = await JS.InvokeAsync<bool>("confirm", $"A aba '{tab.FileName}' tem alterações não salvas. Descartar?");
                if (!discard) 
                    return;
            }
        }

        _openTabs.Remove(tab);

        if (isCurrent)
        {
            if (_openTabs.Count != 0)
            {
                await SwitchTab(_openTabs.Last());
            }
            else
            {
                _activeTab = null;
                _editorPendingCreate = false;
                _editorCreated = false;
                try { await JS.InvokeVoidAsync("monacoInterop.dispose", EditorId); } catch { }
            }
        }

        StateHasChanged();
    }

    private async Task LoadFile(string path)
    {
        var existingTab = _openTabs.FirstOrDefault(t => t.FullPath == path);
        if (existingTab is not null)
        {
            await SwitchTab(existingTab);
            return;
        }

        var isFirstLoad = _activeTab is null;
        var content = await WorkspaceRepo.ReadFileAsync(path);

        var newTab = new OpenFileTab { FullPath = path, Content = content };
        _openTabs.Add(newTab);

        // Switch to the newly opening tab
        if (_activeTab is not null && _editorCreated)
        {
            _activeTab.Content = await JS.InvokeAsync<string>("monacoInterop.getValue", EditorId);
            _activeTab.IsDirty = await JS.InvokeAsync<bool>("monacoInterop.isDirty", EditorId);
        }

        _activeTab = newTab;

        if (isFirstLoad || !_editorCreated)
        {
            _editorPendingCreate = true;
        }
        else
        {
            await JS.InvokeVoidAsync("monacoInterop.setValue", EditorId, newTab.Content, newTab.FullPath);
        }

        StateHasChanged();
    }

    private async Task SaveFile()
    {
        if (_activeTab is null || _saving) 
            return;
        
        _saving = true;
        StateHasChanged();
        
        try
        {
            _activeTab.Content = await JS.InvokeAsync<string>("monacoInterop.getValue", EditorId);
            await WorkspaceRepo.SaveFileAsync(_activeTab.FullPath, _activeTab.Content);
            await JS.InvokeVoidAsync("monacoInterop.clearDirty", EditorId);
            _activeTab.IsDirty = false;
            Toast.ShowSuccess("✔ Arquivo salvo com sucesso!");

            // Run Apigeelint locally
            if (_workspace != null && _activeTab.FullPath.EndsWith(".xml"))
            {
                var lintResults = await LintService.RunLintAsync(_workspace);
                var activeFileLint = lintResults.FirstOrDefault(r => r.FilePath.Replace("\\", "/").EndsWith(_activeTab.FileName));
                if (activeFileLint != null && activeFileLint.Messages.Count != 0)
                {
                    await JS.InvokeVoidAsync("monacoInterop.setMarkers", EditorId, activeFileLint.Messages);
                }
                else
                {
                    await JS.InvokeVoidAsync("monacoInterop.setMarkers", EditorId, Array.Empty<object>());
                }
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
        if (_activeTab is null) 
            return;
        
        await JS.InvokeVoidAsync("monacoInterop.formatDocument", EditorId);
    }

    private async Task DeleteSelectedFile()
    {
        if (_activeTab is null || _workspace is null) 
            return;
        
        var fileName = _activeTab.FileName;
        var confirm = await JS.InvokeAsync<bool>("confirm", "Remover arquivo '" + fileName + "'?");
        
        if (!confirm) 
            return;

        await WorkspaceRepo.DeleteFileAsync(_activeTab.FullPath);

        // Remove the tab and switch to another
        _openTabs.Remove(_activeTab);

        if (_openTabs.Count != 0)
        {
            await SwitchTab(_openTabs.Last());
        }
        else
        {
            _activeTab = null;
            _editorPendingCreate = false;
            _editorCreated = false;

            try 
            { 
                await JS.InvokeVoidAsync("monacoInterop.dispose", EditorId); 
            } catch { }
        }

        _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);

        Toast.ShowSuccess("✔ Arquivo '" + fileName + "' removido.");
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        try
        {
            await JS.InvokeVoidAsync("monacoInterop.dispose", EditorId);
        }
        catch {}        
    }

    private void HandleContextMenu((MouseEventArgs e, WorkspaceItem item) args)
    {
        _contextMenuX = args.e.ClientX;
        _contextMenuY = args.e.ClientY;
        _contextMenuItem = args.item;
        _showContextMenu = true;
    }

    private void CloseContextMenu() => 
        _showContextMenu = false;

    private void ContextAdd(bool isDir)
    {
        _showContextMenu = false;
        OpenNewItemDialog(isDir, _contextMenuItem);
    }

    private async Task ContextDelete()
    {
        _showContextMenu = false;
        if (_contextMenuItem is null || _workspace is null) 
            return;

        var fileName = _contextMenuItem.Name;
        var confirm = await JS.InvokeAsync<bool>("confirm", $"Remover '{fileName}'?");
        
        if (!confirm) 
            return;

        if (_contextMenuItem.Type is WorkspaceItemType.Directory or WorkspaceItemType.Environment or WorkspaceItemType.ApiProxy or WorkspaceItemType.SharedFlow)
        {
            await WorkspaceRepo.DeleteDirectoryAsync(_contextMenuItem.FullPath);
        }
        else
        {
            await WorkspaceRepo.DeleteFileAsync(_contextMenuItem.FullPath);

            // If the deleted file is an open tab, close it
            var tabToClose = _openTabs.FirstOrDefault(t => t.FullPath == _contextMenuItem.FullPath);
            
            if (tabToClose is not null) 
                await CloseTab(tabToClose);
        }

        _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        Toast.ShowSuccess($"✔ '{fileName}' removido.");
        StateHasChanged();
    }

    private void OpenNewItemDialog(bool isDir, WorkspaceItem? targetDir = null)
    {
        _targetDirContext = targetDir;
        _newItemIsDir = isDir;
        _newItemName = _newItemError = string.Empty;
        _showNewItem = true;
    }

    private async Task HandleNewItemKey(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") 
            await ConfirmNewItem();
        
        if (e.Key == "Escape") 
            _showNewItem = false;
    }

    private async Task ConfirmNewItem()
    {
        _newItemError = string.Empty;
        
        if (string.IsNullOrWhiteSpace(_newItemName)) 
        { 
            _newItemError = "Informe um nome."; 
            return; 
        }

        if (_workspace is null) 
            return;

        var basePath = _targetDirContext is not null
            ? (_targetDirContext.Type == WorkspaceItemType.File ? Path.GetDirectoryName(_targetDirContext.FullPath)! : _targetDirContext.FullPath)
            : (_activeTab is not null ? Path.GetDirectoryName(_activeTab.FullPath)! : _workspace.RootPath);

        var fullPath = Path.Combine(basePath, _newItemName);

        try
        {
            if (_newItemIsDir) 
                await WorkspaceRepo.CreateDirectoryAsync(fullPath);
            else 
            { 
                await WorkspaceRepo.CreateFileAsync(fullPath); 
                await LoadFile(fullPath); 
            }

            _showNewItem = false;
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        }
        catch (Exception ex) 
        { 
            _newItemError = ex.Message; 
        }
    }

    private void OpenQuickAddModal((string Path, string Category) args)
    {
        _quickAddPath = args.Path;
        _quickAddCategory = args.Category;
        _quickTemplate = null;
        _quickParams = [];
        _quickFileName = string.Empty;
        _quickMessage = string.Empty;
        _quickError = false;
        _quickAddOpen = true;
    }

    private void CloseQuickAdd() => 
        _quickAddOpen = false;

    private void SelectQuickTemplate(PolicyTemplate t)
    {
        _quickTemplate = t;
        _quickParams = t.Parameters.ToDictionary(p => p, _ => string.Empty);
        
        if (_quickParams.ContainsKey("PolicyName")) 
            _quickParams["PolicyName"] = t.Name + "Policy";
        
        UpdateLivePreview();
    }

    private string GetQParam(string k) => 
        _quickParams.TryGetValue(k, out var v) ? v : string.Empty;
    
    private void SetQParam(string k, string v)
    {
        _quickParams[k] = v;
        UpdateLivePreview();
    }

    private void UpdateLivePreview()
    {
        if (_quickTemplate is not null)
            _quickXmlEditable = TemplateRepo.GeneratePolicyXml(_quickTemplate, _quickParams);
    }

    private async Task ConfirmQuickPolicy()
    {
        if (_quickTemplate is null || _workspace is null) 
            return;
        
        _quickGenerating = true;
        _quickMessage = string.Empty;
        _quickError = false;

        try
        {
            var path = await GeneratePolicy.ExecuteAtPathAsync(
                _quickAddPath,
                _quickTemplate.Name,
                _quickParams,
                _quickXmlEditable);

            _quickMessage = "✔ Criado: " + Path.GetFileName(path);
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
            Toast.ShowSuccess("Política '" + Path.GetFileName(path) + "' criada.");
            
            await LoadFile(path);
            CloseQuickAdd();
        }
        catch (Exception ex) 
        { 
            _quickMessage = "✘ " + ex.Message; 
            _quickError = true;
        }
        finally 
        { 
            _quickGenerating = false; 
        }
    }

    private async Task ConfirmQuickFile()
    {
        if (string.IsNullOrWhiteSpace(_quickFileName) || _workspace is null) 
            return;
        
        _quickMessage = string.Empty;
        _quickError = false;
        
        try
        {
            if (string.Equals(_quickAddCategory, "apiproxies", StringComparison.OrdinalIgnoreCase))
            {
                await CreateApiProxySkeletonAsync();
            }
            else
            {
                var fullPath = Path.Combine(_quickAddPath, _quickFileName);
                await WorkspaceRepo.CreateFileAsync(fullPath);
                await LoadFile(fullPath);
                _quickAddOpen = false;
                _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
            }
        }
        catch (Exception ex) 
        { 
            _quickMessage = "✘ " + ex.Message; 
            _quickError = true; 
        }
    }

    private async Task ConfirmQuickSharedFlow()
    {
        if (string.IsNullOrWhiteSpace(_quickFileName) || _workspace is null) 
            return;
        
        _quickMessage = string.Empty;
        _quickError = false;
        
        try
        {
            var name = _quickFileName.Trim();
            var sfDir = Path.Combine(_workspace.RootPath, "sharedflows", name, "sharedflowbundle");
            var polDir = Path.Combine(sfDir, "policies");
            var resDir = Path.Combine(sfDir, "sharedflows");

            await WorkspaceRepo.CreateDirectoryAsync(sfDir);
            await WorkspaceRepo.CreateDirectoryAsync(polDir);
            await WorkspaceRepo.CreateDirectoryAsync(resDir);

            var bundleXml =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                "<SharedFlowBundle name=\"" + name + "\">\n" +
                "    <Description>" + name + "</Description>\n" +
                "    <Revision>1</Revision>\n" +
                "    <SharedFlows>\n" +
                "        <SharedFlow>default</SharedFlow>\n" +
                "    </SharedFlows>\n" +
                "</SharedFlowBundle>\n";

            await WorkspaceRepo.SaveFileAsync(
                Path.Combine(sfDir, name + ".xml"), bundleXml);

            var sharedFlowXml =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
                "<SharedFlow name=\"default\">\n" +
                "    <Description>Default shared flow</Description>\n" +
                "</SharedFlow>\n";

            await WorkspaceRepo.SaveFileAsync(
                Path.Combine(resDir, "default.xml"), sharedFlowXml);

            _quickMessage = "✔ Shared Flow '" + name + "' criado.";
            _quickAddOpen = false;
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
            Toast.ShowSuccess("Shared Flow '" + name + "' criado.");
        }
        catch (Exception ex) 
        { 
            _quickMessage = "✘ " + ex.Message; 
            _quickError = true; 
        }
    }

    private async Task ConfirmQuickEnvironment()
    {
        if (string.IsNullOrWhiteSpace(_quickFileName) || _workspace is null) 
            return;
        
        _quickMessage = string.Empty;
        _quickError = false;
        
        try
        {
            var envName = _quickFileName.Trim();
            var envDir = Path.Combine(_workspace.RootPath, "environments", envName);
            await WorkspaceRepo.CreateDirectoryAsync(envDir);

            await WorkspaceRepo.SaveFileAsync(
                Path.Combine(envDir, "deployments.json"),
                "{\n  \"proxies\": [],\n  \"sharedFlows\": []\n}");

            await WorkspaceRepo.SaveFileAsync(
                Path.Combine(envDir, "flowhooks.json"),
                "{}");

            await WorkspaceRepo.SaveFileAsync(
                Path.Combine(envDir, "targetservers.json"),
                "[]\n");

            _quickMessage = "✔ Environment '" + envName + "' criado.";
            _quickAddOpen = false;
            _deployEnv = envName;
            _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
            Toast.ShowSuccess("Environment '" + envName + "' criado.");
        }
        catch (Exception ex) 
        { 
            _quickMessage = "✘ " + ex.Message; 
            _quickError = true; 
        }
    }

    private async Task CreateApiProxySkeletonAsync()
    {
        if (_workspace is null) 
            return;

        var name = _quickFileName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Informe o nome da API.");

        var apiproxyDir = Path.Combine(_quickAddPath, name, "apiproxy");
        var policiesDir = Path.Combine(apiproxyDir, "policies");
        var proxiesDir = Path.Combine(apiproxyDir, "proxies");
        var resourcesDir = Path.Combine(apiproxyDir, "resources");
        var targetsDir = Path.Combine(apiproxyDir, "targets");

        await WorkspaceRepo.CreateDirectoryAsync(apiproxyDir);
        await WorkspaceRepo.CreateDirectoryAsync(policiesDir);
        await WorkspaceRepo.CreateDirectoryAsync(proxiesDir);
        await WorkspaceRepo.CreateDirectoryAsync(resourcesDir);
        await WorkspaceRepo.CreateDirectoryAsync(targetsDir);

        var descriptorXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<APIProxy name=\"" + name + "\">\n" +
            "    <Description>" + name + "</Description>\n" +
            "    <Revision>1</Revision>\n" +
            "</APIProxy>\n";
        await WorkspaceRepo.SaveFileAsync(Path.Combine(apiproxyDir, name + ".xml"), descriptorXml);

        var proxyEndpointXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<ProxyEndpoint name=\"default\">\n" +
            "    <Description>" + name + " proxy endpoint</Description>\n" +
            "    <HTTPProxyConnection>\n" +
            "        <BasePath>/" + name + "</BasePath>\n" +
            "        <VirtualHost>default</VirtualHost>\n" +
            "    </HTTPProxyConnection>\n" +
            "    <PreFlow name=\"PreFlow\">\n" +
            "        <Request/>\n" +
            "        <Response/>\n" +
            "    </PreFlow>\n" +
            "    <PostFlow name=\"PostFlow\">\n" +
            "        <Request/>\n" +
            "        <Response/>\n" +
            "    </PostFlow>\n" +
            "    <Flows/>\n" +
            "    <RouteRule name=\"default\">\n" +
            "        <TargetEndpoint>default</TargetEndpoint>\n" +
            "    </RouteRule>\n" +
            "</ProxyEndpoint>\n";
        await WorkspaceRepo.SaveFileAsync(Path.Combine(proxiesDir, "default.xml"), proxyEndpointXml);

        var targetEndpointXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<TargetEndpoint name=\"default\">\n" +
            "    <Description>Default target endpoint</Description>\n" +
            "    <PreFlow name=\"PreFlow\">\n" +
            "        <Request/>\n" +
            "        <Response/>\n" +
            "    </PreFlow>\n" +
            "    <PostFlow name=\"PostFlow\">\n" +
            "        <Request/>\n" +
            "        <Response/>\n" +
            "    </PostFlow>\n" +
            "    <Flows/>\n" +
            "    <HTTPTargetConnection>\n" +
            "        <URL>https://httpbin.org/anything</URL>\n" +
            "    </HTTPTargetConnection>\n" +
            "</TargetEndpoint>\n";
        await WorkspaceRepo.SaveFileAsync(Path.Combine(targetsDir, "default.xml"), targetEndpointXml);

        await LoadFile(Path.Combine(proxiesDir, "default.xml"));
        _quickAddOpen = false;
        _tree = await WorkspaceRepo.LoadTreeAsync(_workspace);
        Toast.ShowSuccess("API Proxy '" + name + "' criado com default.xml em proxies e targets.");
    }

    private async Task DeployProxy()
    {
        if (_workspace is null) 
            return;

        await RunDeploy(() => DeployUseCase.ExecuteAsync(_workspace, _deployTarget, _deployEnv));
    }

    private async Task DeployFull()
    {
        if (_workspace is null) 
            return;

        await RunDeploy(() => DeployUseCase.ExecuteFullAsync(_workspace, _deployEnv));
    }

    private async Task RunDeploy(Func<Task> action)
    {
        _deploying = true; 
        _deployMessage = string.Empty;
        _deployError = false;
        
        try
        {
            await action();
            _deployMessage = "✔ Deploy realizado com sucesso!";
            Toast.ShowSuccess("Deploy concluído com sucesso!");
        }
        catch (Exception ex)
        {
            _deployMessage = "✘ Deploy falhou: " + ex.Message;
            _deployError = true;
            Toast.ShowError("Deploy falhou: " + ex.Message);
        }
        finally 
        { 
            _deploying = false; 
        }
    }
}
