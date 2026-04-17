namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class QuickAddDrawer : ComponentBase
{
    private IReadOnlyList<PolicyTemplate> _allTemplates = [];
    private PolicyTemplate? _quickTemplate;
    private Dictionary<string, string> _quickParams = [];
    private string _quickFileName = string.Empty;
    private string _quickMessage = string.Empty;
    private string _quickXmlEditable = string.Empty;
    private bool _quickError;
    private bool _quickGenerating;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public string Category { get; set; } = string.Empty;

    [Parameter]
    public string BasePath { get; set; } = string.Empty;

    [Parameter]
    public ApigeeWorkspace? Workspace { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public EventCallback<string> OnItemCreated { get; set; }

    [Inject]
    public required IPolicyTemplateRepository TemplateRepo { get; set; }

    [Inject]
    public required GeneratePolicyUseCase GeneratePolicy { get; set; }

    [Inject]
    public required IWorkspaceRepository WorkspaceRepo { get; set; }

    [Inject]
    public required ToastService Toast { get; set; }

    [Inject]
    public required SkeletonTemplateService Skeleton { get; set; }

    private string QuickMessageClass => _quickError ? "error-text" : "success-text";

    protected override void OnInitialized()
    {
        _allTemplates = TemplateRepo.GetAll();
    }

    protected override void OnParametersSet()
    {
        if (IsOpen)
        {
            _quickTemplate = null;
            _quickParams = [];
            _quickFileName = string.Empty;
            _quickMessage = string.Empty;
            _quickXmlEditable = string.Empty;
            _quickError = false;
        }
    }

    private async Task Close() => await OnClose.InvokeAsync();

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
        if (_quickTemplate is null || Workspace is null) return;
        
        _quickGenerating = true;
        _quickMessage = string.Empty;
        _quickError = false;

        try
        {
            var path = await GeneratePolicy.ExecuteAtPathAsync(
                BasePath,
                _quickTemplate.Name,
                _quickParams,
                _quickXmlEditable);

            _quickMessage = "✔ Criado: " + Path.GetFileName(path);
            Toast.ShowSuccess("Política '" + Path.GetFileName(path) + "' criada.");
            
            await OnItemCreated.InvokeAsync(path);
            await Close();
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
        if (string.IsNullOrWhiteSpace(_quickFileName) || Workspace is null) return;
        
        _quickMessage = string.Empty;
        _quickError = false;
        
        try
        {
            if (string.Equals(Category, "apiproxies", StringComparison.OrdinalIgnoreCase))
            {
                await CreateApiProxySkeletonAsync();
            }
            else
            {
                var fullPath = Path.Combine(BasePath, _quickFileName);
                await WorkspaceRepo.CreateFileAsync(fullPath);
                await OnItemCreated.InvokeAsync(fullPath);
                await Close();
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
        if (string.IsNullOrWhiteSpace(_quickFileName) || Workspace is null) return;
        
        _quickMessage = string.Empty;
        _quickError = false;
        
        try
        {
            var name = _quickFileName.Trim();
            var sfDir = Path.Combine(Workspace.RootPath, "sharedflows", name, "sharedflowbundle");
            var polDir = Path.Combine(sfDir, "policies");
            var resDir = Path.Combine(sfDir, "sharedflows");

            await WorkspaceRepo.CreateDirectoryAsync(sfDir);
            await WorkspaceRepo.CreateDirectoryAsync(polDir);
            await WorkspaceRepo.CreateDirectoryAsync(resDir);

            var bundleXml = Skeleton.GetSharedFlowBundleXml(name);
            await WorkspaceRepo.SaveFileAsync(Path.Combine(sfDir, name + ".xml"), bundleXml);

            var sharedFlowXml = Skeleton.GetSharedFlowXml(name);
            await WorkspaceRepo.SaveFileAsync(Path.Combine(resDir, "default.xml"), sharedFlowXml);

            _quickMessage = "✔ Shared Flow '" + name + "' criado.";
            Toast.ShowSuccess("Shared Flow '" + name + "' criado.");
            await OnItemCreated.InvokeAsync(null); // Just refresh tree
            await Close();
        }
        catch (Exception ex) 
        { 
            _quickMessage = "✘ " + ex.Message; 
            _quickError = true; 
        }
    }

    private async Task ConfirmQuickEnvironment()
    {
        if (string.IsNullOrWhiteSpace(_quickFileName) || Workspace is null) return;
        
        _quickMessage = string.Empty;
        _quickError = false;
        
        try
        {
            var envName = _quickFileName.Trim();
            var envDir = Path.Combine(Workspace.RootPath, "environments", envName);
            await WorkspaceRepo.CreateDirectoryAsync(envDir);

            await WorkspaceRepo.SaveFileAsync(Path.Combine(envDir, "deployments.json"), Skeleton.GetDeploymentsJson());
            await WorkspaceRepo.SaveFileAsync(Path.Combine(envDir, "flowhooks.json"), Skeleton.GetFlowhooksJson());
            await WorkspaceRepo.SaveFileAsync(Path.Combine(envDir, "targetservers.json"), Skeleton.GetTargetServersJson());

            _quickMessage = "✔ Environment '" + envName + "' criado.";
            Toast.ShowSuccess("Environment '" + envName + "' criado.");
            await OnItemCreated.InvokeAsync(null);
            await Close();
        }
        catch (Exception ex) 
        { 
            _quickMessage = "✘ " + ex.Message; 
            _quickError = true; 
        }
    }

    private async Task CreateApiProxySkeletonAsync()
    {
        if (Workspace is null) return;

        var name = _quickFileName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Informe o nome da API.");

        var apiproxyDir = Path.Combine(BasePath, name, "apiproxy");
        var policiesDir = Path.Combine(apiproxyDir, "policies");
        var proxiesDir = Path.Combine(apiproxyDir, "proxies");
        var resourcesDir = Path.Combine(apiproxyDir, "resources");
        var targetsDir = Path.Combine(apiproxyDir, "targets");

        await WorkspaceRepo.CreateDirectoryAsync(apiproxyDir);
        await WorkspaceRepo.CreateDirectoryAsync(policiesDir);
        await WorkspaceRepo.CreateDirectoryAsync(proxiesDir);
        await WorkspaceRepo.CreateDirectoryAsync(resourcesDir);
        await WorkspaceRepo.CreateDirectoryAsync(targetsDir);

        await WorkspaceRepo.SaveFileAsync(Path.Combine(apiproxyDir, name + ".xml"), Skeleton.GetApiProxyDescriptorXml(name));
        await WorkspaceRepo.SaveFileAsync(Path.Combine(proxiesDir, "default.xml"), Skeleton.GetProxyEndpointXml(name));
        await WorkspaceRepo.SaveFileAsync(Path.Combine(targetsDir, "default.xml"), Skeleton.GetTargetEndpointXml());

        var defaultProxyFile = Path.Combine(proxiesDir, "default.xml");
        Toast.ShowSuccess("API Proxy '" + name + "' criado com default.xml em proxies e targets.");
        await OnItemCreated.InvokeAsync(defaultProxyFile);
        await Close();
    }
}
