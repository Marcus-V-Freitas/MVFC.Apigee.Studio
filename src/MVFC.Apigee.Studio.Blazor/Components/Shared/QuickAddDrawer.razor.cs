namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

/// <summary>
/// Blazor component for quickly adding new files, API proxies, shared flows, or environments to the workspace.
/// Supports template-based policy creation, skeleton generation, and minimal JSON creation for Apigee structures.
/// </summary>
public partial class QuickAddDrawer : ComponentBase
{
    /// <summary>
    /// All available policy templates for quick creation.
    /// </summary>
    private IReadOnlyList<PolicyTemplate> _allTemplates = [];

    /// <summary>
    /// The currently selected policy template for quick creation.
    /// </summary>
    private PolicyTemplate? _quickTemplate;

    /// <summary>
    /// Parameters for the selected policy template.
    /// </summary>
    private Dictionary<string, string> _quickParams = [];

    /// <summary>
    /// The file name for quick file, proxy, shared flow, or environment creation.
    /// </summary>
    private string _quickFileName = string.Empty;

    /// <summary>
    /// Message displayed to the user about the quick add operation.
    /// </summary>
    private string _quickMessage = string.Empty;

    /// <summary>
    /// Editable XML preview for the selected policy template.
    /// </summary>
    private string _quickXmlEditable = string.Empty;

    /// <summary>
    /// Indicates if the last quick add operation resulted in an error.
    /// </summary>
    private bool _quickError;

    /// <summary>
    /// Indicates if a quick add operation is currently in progress.
    /// </summary>
    private bool _quickGenerating;

    /// <summary>
    /// Indicates if the drawer is open.
    /// </summary>
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>
    /// The quick add category (e.g., "policies", "apiproxies", "sharedflows", "environments").
    /// </summary>
    [Parameter]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The base path where the new item will be created.
    /// </summary>
    [Parameter]
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// The current workspace context.
    /// </summary>
    [Parameter]
    public ApigeeWorkspace? Workspace { get; set; }

    /// <summary>
    /// Event callback triggered when the drawer is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    /// Event callback triggered when a new item is created.
    /// The string parameter is the path of the created item, or null to just refresh the tree.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnItemCreated { get; set; }

    /// <summary>
    /// Repository for accessing policy templates.
    /// </summary>
    [Inject]
    public required IPolicyTemplateRepository TemplateRepo { get; set; }

    /// <summary>
    /// Use case for generating policy XML files from templates and saving them to the workspace.
    /// </summary>
    [Inject]
    public required GeneratePolicyUseCase GeneratePolicy { get; set; }

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
    /// Gets the CSS class for the quick message (error or success).
    /// </summary>
    private string QuickMessageClass => _quickError ? "error-text" : "success-text";

    /// <summary>
    /// Loads all policy templates on component initialization.
    /// </summary>
    protected override void OnInitialized()
    {
        _allTemplates = TemplateRepo.GetAll();
    }

    /// <summary>
    /// Resets quick add state when the drawer is opened.
    /// </summary>
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

    /// <summary>
    /// Closes the quick add drawer.
    /// </summary>
    private async Task Close() => await OnClose.InvokeAsync();

    /// <summary>
    /// Selects a policy template for quick creation and initializes its parameters.
    /// </summary>
    /// <param name="t">The selected policy template.</param>
    private void SelectQuickTemplate(PolicyTemplate t)
    {
        _quickTemplate = t;
        _quickParams = t.Parameters.ToDictionary(p => p, _ => string.Empty, StringComparer.OrdinalIgnoreCase);

        if (_quickParams.ContainsKey("PolicyName"))
            _quickParams["PolicyName"] = t.Name + "Policy";

        UpdateLivePreview();
    }

    /// <summary>
    /// Gets the value of a quick parameter by key.
    /// </summary>
    /// <param name="k">The parameter key.</param>
    /// <returns>The parameter value.</returns>
    private string GetQParam(string k) =>
        _quickParams.TryGetValue(k, out var v) ? v : string.Empty;

    /// <summary>
    /// Sets the value of a quick parameter and updates the live XML preview.
    /// </summary>
    /// <param name="k">The parameter key.</param>
    /// <param name="v">The parameter value.</param>
    private void SetQParam(string k, string v)
    {
        _quickParams[k] = v;
        UpdateLivePreview();
    }

    /// <summary>
    /// Updates the live XML preview for the selected policy template.
    /// </summary>
    private void UpdateLivePreview()
    {
        if (_quickTemplate is not null)
            _quickXmlEditable = TemplateRepo.GeneratePolicyXml(_quickTemplate, _quickParams);
    }

    /// <summary>
    /// Confirms and creates a new policy file from the selected template and parameters.
    /// </summary>
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

    /// <summary>
    /// Confirms and creates a new file or API proxy skeleton, depending on the category.
    /// </summary>
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

    /// <summary>
    /// Confirms and creates a new shared flow skeleton with minimal structure and files.
    /// </summary>
    private async Task ConfirmQuickSharedFlow()
    {
        if (string.IsNullOrWhiteSpace(_quickFileName) || Workspace is null)
            return;

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

            var bundleXml = SkeletonTemplateService.GetSharedFlowBundleXml(name);
            await WorkspaceRepo.SaveFileAsync(Path.Combine(sfDir, name + ".xml"), bundleXml);

            var sharedFlowXml = SkeletonTemplateService.GetSharedFlowXml(name);
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

    /// <summary>
    /// Confirms and creates a new environment folder with minimal JSON files.
    /// </summary>
    private async Task ConfirmQuickEnvironment()
    {
        if (string.IsNullOrWhiteSpace(_quickFileName) || Workspace is null)
            return;

        _quickMessage = string.Empty;
        _quickError = false;

        try
        {
            var envName = _quickFileName.Trim();
            var envDir = Path.Combine(Workspace.RootPath, "environments", envName);
            await WorkspaceRepo.CreateDirectoryAsync(envDir);

            await WorkspaceRepo.SaveFileAsync(Path.Combine(envDir, "deployments.json"), SkeletonTemplateService.GetDeploymentsJson());
            await WorkspaceRepo.SaveFileAsync(Path.Combine(envDir, "flowhooks.json"), SkeletonTemplateService.GetFlowhooksJson());
            await WorkspaceRepo.SaveFileAsync(Path.Combine(envDir, "targetservers.json"), SkeletonTemplateService.GetTargetServersJson());

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

    /// <summary>
    /// Creates a new API proxy skeleton with the required folder structure and default files.
    /// </summary>
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

        await WorkspaceRepo.SaveFileAsync(Path.Combine(apiproxyDir, name + ".xml"), SkeletonTemplateService.GetApiProxyDescriptorXml(name));
        await WorkspaceRepo.SaveFileAsync(Path.Combine(proxiesDir, "default.xml"), SkeletonTemplateService.GetProxyEndpointXml(name));
        await WorkspaceRepo.SaveFileAsync(Path.Combine(targetsDir, "default.xml"), SkeletonTemplateService.GetTargetEndpointXml());

        var defaultProxyFile = Path.Combine(proxiesDir, "default.xml");
        Toast.ShowSuccess("API Proxy '" + name + "' criado com default.xml em proxies e targets.");
        await OnItemCreated.InvokeAsync(defaultProxyFile);
        await Close();
    }
}
