namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

/// <summary>
/// Panel for managing test resources like API Products, Developers, and Developer Apps.
/// Provides functionality to sync resources with the emulator and manage local test data.
/// </summary>
public partial class TestResourcesPanel
{
    /// <summary>
    /// The current active workspace.
    /// </summary>
    [Parameter] public ApigeeWorkspace? Workspace { get; set; }

    /// <summary>
    /// Event callback triggered when resources are modified and saved.
    /// </summary>
    [Parameter] public EventCallback OnResourcesChanged { get; set; }

    [Inject] private EditorStateService EditorService { get; set; } = default!;
    [Inject] private Services.ToastService ToastService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IApigeeEmulatorClient EmulatorClient { get; set; } = default!;

    private TestResources Resources { get; set; } = new([], [], []);
    private bool Loading { get; set; } = true;
    private ResourceTab ActiveTab { get; set; } = ResourceTab.Products;

    // --- Modal State ---
    private bool ShowProductModal { get; set; }
    private ApiProduct? EditingProduct { get; set; }
    private string ProductName { get; set; } = "";
    private string ProductDisplayName { get; set; } = "";

    private bool ShowDeveloperModal { get; set; }
    private Developer? EditingDeveloper { get; set; }
    private string DeveloperEmail { get; set; } = "";
    private string DeveloperFirstName { get; set; } = "";
    private string DeveloperLastName { get; set; } = "";

    private bool ShowAppModal { get; set; }
    private bool ShowDevDropdown { get; set; }
    private DeveloperApp? EditingApp { get; set; }
    private string AppName { get; set; } = "";
    private string AppDeveloperEmail { get; set; } = "";
    private List<string> SelectedProducts { get; set; } = [];
    private List<string> SelectedProxies { get; set; } = [];

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        if (Workspace != null)
        {
            await LoadResources();
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JSRuntime.InvokeVoidAsync("initLucide");
    }

    /// <summary>
    /// Loads test resources from the workspace's test directory.
    /// </summary>
    private async Task LoadResources()
    {
        if (Workspace == null)
        {
            return;
        }

        Loading = true;
        try
        {
            Resources = await WorkspaceRepository.GetTestResourcesAsync(Workspace);
        }
        finally
        {
            Loading = false;
        }
    }

    /// <summary>
    /// Saves the current state of test resources to the workspace and notifies other components.
    /// </summary>
    private async Task SaveResources()
    {
        if (Workspace == null)
        {
            return;
        }

        await WorkspaceRepository.SaveTestResourcesAsync(Workspace, Resources);

        // Sync open editor tabs
        var options = new JsonSerializerOptions { WriteIndented = true };
        var testDir = Path.Combine(Workspace.RootPath, "test");

        EditorService.SyncTabContent(Path.Combine(testDir, "products.json"),
            JsonSerializer.Serialize(Resources.Products, options));
        EditorService.SyncTabContent(Path.Combine(testDir, "developers.json"),
            JsonSerializer.Serialize(Resources.Developers, options));
        EditorService.SyncTabContent(Path.Combine(testDir, "developerapps.json"),
            JsonSerializer.Serialize(Resources.Apps, options));

        await OnResourcesChanged.InvokeAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Internal tabs for the resource panel.
    /// </summary>
    private enum ResourceTab { Products, Developers, Apps }

    // --- Actions ---

    /// <summary>
    /// Opens the modal to add a new API Product.
    /// </summary>
    private void AddProduct()
    {
        EditingProduct = null;
        ProductName = "";
        ProductDisplayName = "";
        SelectedProxies = [.. WorkspaceRepository.ListApiProxies(Workspace!)];
        ShowProductModal = true;
    }

    /// <summary>
    /// Opens the modal to edit an existing API Product.
    /// </summary>
    /// <param name="prod">The product to edit.</param>
    private void EditProduct(ApiProduct prod)
    {
        EditingProduct = prod;
        ProductName = prod.Name;
        ProductDisplayName = prod.DisplayName;
        SelectedProxies = prod.Proxies?.ToList() ?? [];
        ShowProductModal = true;
    }

    /// <summary>
    /// Toggles the selection state of an API Proxy for the current product being edited.
    /// </summary>
    /// <param name="proxyName">The name of the proxy.</param>
    private void ToggleProxy(string proxyName)
    {
        if (!SelectedProxies.Remove(proxyName))
        {
            SelectedProxies.Add(proxyName);
        }
    }

    /// <summary>
    /// Saves the product currently being edited to the local resource state.
    /// </summary>
    private async Task SaveProduct()
    {
        if (string.IsNullOrWhiteSpace(ProductName))
        {
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(ProductDisplayName) ? ProductName : ProductDisplayName;
        var newProduct = new ApiProduct(ProductName, displayName, "Test Product", "auto", ["local"], [.. SelectedProxies]);

        if (EditingProduct != null)
        {
            Resources = Resources with
            {
                Products = [.. Resources.Products.Select(p => string.Equals(p.Name, EditingProduct.Name, StringComparison.Ordinal) ? newProduct : p)],
            };
        }
        else
        {
            Resources = Resources with { Products = [.. Resources.Products, newProduct] };
        }

        await SaveResources();
        ShowProductModal = false;
        ToastService.ShowSuccess($"Produto '{ProductName}' salvo com sucesso.");
    }

    /// <summary>
    /// Removes an API Product from the local resource state.
    /// </summary>
    /// <param name="prod">The product to delete.</param>
    private async Task DeleteProduct(ApiProduct prod)
    {
        Resources = Resources with
        {
            Products = [.. Resources.Products.Where(p => p != prod)],
        };
        await SaveResources();
        ToastService.ShowInfo($"Produto '{prod.Name}' removido.");
    }

    /// <summary>
    /// Opens the modal to add a new Developer.
    /// </summary>
    private void AddDeveloper()
    {
        EditingDeveloper = null;
        DeveloperEmail = "";
        DeveloperFirstName = "";
        DeveloperLastName = "";
        ShowDeveloperModal = true;
    }

    /// <summary>
    /// Opens the modal to edit an existing Developer.
    /// </summary>
    /// <param name="dev">The developer to edit.</param>
    private void EditDeveloper(Developer dev)
    {
        EditingDeveloper = dev;
        DeveloperEmail = dev.Email;
        DeveloperFirstName = dev.FirstName;
        DeveloperLastName = dev.LastName;
        ShowDeveloperModal = true;
    }

    /// <summary>
    /// Saves the developer currently being edited to the local resource state.
    /// </summary>
    private async Task SaveDeveloper()
    {
        if (string.IsNullOrWhiteSpace(DeveloperEmail))
        {
            return;
        }

        var newDev = new Developer(DeveloperEmail, DeveloperFirstName, DeveloperLastName, DeveloperEmail);

        if (EditingDeveloper != null)
        {
            Resources = Resources with
            {
                Developers = [.. Resources.Developers.Select(d => d == EditingDeveloper ? newDev : d)],
            };
        }
        else
        {
            Resources = Resources with { Developers = [.. Resources.Developers, newDev] };
        }

        await SaveResources();
        ShowDeveloperModal = false;
        ToastService.ShowSuccess($"Desenvolvedor '{DeveloperEmail}' salvo.");
    }

    /// <summary>
    /// Removes a Developer from the local resource state.
    /// </summary>
    /// <param name="dev">The developer to delete.</param>
    private async Task DeleteDeveloper(Developer dev)
    {
        Resources = Resources with
        {
            Developers = [.. Resources.Developers.Where(d => d != dev)],
        };
        await SaveResources();
        ToastService.ShowInfo($"Desenvolvedor '{dev.Email}' removido.");
    }

    /// <summary>
    /// Toggles the visibility of the developer selection dropdown in the App modal.
    /// </summary>
    private async Task ToggleDevDropdown()
    {
        ShowDevDropdown = !ShowDevDropdown;
        if (ShowDevDropdown)
        {
            await Task.Yield();
            await JSRuntime.InvokeVoidAsync("initLucide");
        }
    }

    /// <summary>
    /// Selects a developer from the custom dropdown.
    /// </summary>
    /// <param name="email">The email of the selected developer.</param>
    private async Task SelectDeveloper(string email)
    {
        AppDeveloperEmail = email;
        ShowDevDropdown = false;
        StateHasChanged();
        await Task.Yield();
    }

    /// <summary>
    /// Opens the modal to add a new Developer App.
    /// </summary>
    private void AddApp()
    {
        EditingApp = null;
        AppName = "";
        AppDeveloperEmail = Resources.Developers.Count > 0 ? Resources.Developers[0].Email : "";
        SelectedProducts = [];
        ShowDevDropdown = false;
        ShowAppModal = true;
    }

    /// <summary>
    /// Opens the modal to edit an existing Developer App.
    /// </summary>
    /// <param name="app">The app to edit.</param>
    private void EditApp(DeveloperApp app)
    {
        EditingApp = app;
        AppName = app.Name;
        AppDeveloperEmail = app.DeveloperId;
        SelectedProducts = [.. app.ApiProducts];
        ShowDevDropdown = false;
        ShowAppModal = true;
    }

    /// <summary>
    /// Toggles the selection state of an API Product for the current app being edited.
    /// </summary>
    /// <param name="productName">The name of the product.</param>
    private void ToggleProduct(string productName)
    {
        if (!SelectedProducts.Remove(productName))
        {
            SelectedProducts.Add(productName);
        }
    }

    /// <summary>
    /// Saves the developer app currently being edited to the local resource state.
    /// </summary>
    private async Task SaveApp()
    {
        if (string.IsNullOrWhiteSpace(AppName) ||
            string.IsNullOrWhiteSpace(AppDeveloperEmail))
        {
            return;
        }

        var newApp = new DeveloperApp(AppName, AppName, AppDeveloperEmail, AppDeveloperEmail, [.. SelectedProducts]);

        if (EditingApp != null)
        {
            Resources = Resources with
            {
                Apps = [.. Resources.Apps.Select(a => string.Equals(a.Name, EditingApp.Name, StringComparison.Ordinal) ? newApp : a)],
            };
        }
        else
        {
            Resources = Resources with { Apps = [.. Resources.Apps, newApp] };
        }

        await SaveResources();
        ShowAppModal = false;
        ToastService.ShowSuccess($"App '{AppName}' salvo.");
    }

    /// <summary>
    /// Synchronizes app credentials from the live emulator environment.
    /// Merges the most recent credentials into the local resource state.
    /// </summary>
    private async Task SyncCredentials()
    {
        Loading = true;
        try
        {
            var liveApps = await EmulatorClient.GetLiveDeveloperAppsAsync();
            if (liveApps.Count == 0)
            {
                ToastService.ShowWarning("Nenhum app encontrado no emulador. Faça o deploy primeiro.");
                return;
            }

            // Merge live credentials into local apps
            var updatedApps = Resources.Apps.Select(localApp =>
            {
                var live = liveApps.FirstOrDefault(l => string.Equals(l.Name, localApp.Name, StringComparison.Ordinal));
                // Keep only the most recent credential (the last one in the list)
                var lastCred = live?.Credentials is [.., var last] ? last : null;
                return live != null && lastCred != null
                    ? localApp with { Credentials = [lastCred], AppId = live.AppId }
                    : localApp;
            }).ToList();

            Resources = Resources with { Apps = updatedApps };
            await SaveResources();
            ToastService.ShowSuccess("Credenciais sincronizadas com o emulador!");
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Erro ao sincronizar: {ex.Message}");
        }
        finally
        {
            Loading = false;
        }
    }

    /// <summary>
    /// Removes a Developer App from the local resource state.
    /// </summary>
    /// <param name="app">The app to delete.</param>
    private async Task DeleteApp(DeveloperApp app)
    {
        Resources = Resources with
        {
            Apps = [.. Resources.Apps.Where(a => a != app)],
        };
        await SaveResources();
        ToastService.ShowInfo($"App '{app.Name}' removido.");
    }
}
