namespace MVFC.Apigee.Studio.Blazor.Components.Layout;

/// <summary>
/// Blazor navigation menu component that manages the sidebar state and responds to navigation and editor tab changes.
/// Integrates with JavaScript for icon initialization and ensures proper event unsubscription on disposal.
/// </summary>
public partial class NavMenu : ComponentBase, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Optional child content to be rendered inside the navigation menu.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// JavaScript runtime used for invoking JS interop methods.
    /// </summary>
    [Inject]
    public required IJSRuntime JS { get; set; }

    /// <summary>
    /// Provides navigation and URL management for the application.
    /// </summary>
    [Inject]
    public required NavigationManager Nav { get; set; }

    /// <summary>
    /// Service for managing the state of editor tabs.
    /// </summary>
    [Inject]
    public required EditorStateService EditorState { get; set; }

    private bool _sidebarOpen;

    /// <summary>
    /// Subscribes to navigation and editor tab change events.
    /// </summary>
    protected override void OnInitialized()
    {
        Nav.LocationChanged += HandleLocationChanged;
        EditorState.OnTabsChanged += HandleTabsChanged;
    }

    /// <summary>
    /// Handles navigation changes by closing the sidebar and updating the UI.
    /// </summary>
    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _sidebarOpen = false;
        StateHasChanged();
    }

    /// <summary>
    /// Handles editor tab changes by closing the sidebar if it is open and updating the UI.
    /// </summary>
    private void HandleTabsChanged()
    {
        if (_sidebarOpen)
        {
            _sidebarOpen = false;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// Toggles the sidebar open/close state.
    /// </summary>
    private void ToggleSidebar() =>
        _sidebarOpen = !_sidebarOpen;

    /// <summary>
    /// Closes the sidebar.
    /// </summary>
    private void CloseSidebar() =>
        _sidebarOpen = false;

    /// <summary>
    /// Invokes JavaScript to initialize Lucide icons after the component is rendered.
    /// </summary>
    /// <param name="firstRender">Indicates if this is the first render.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender) =>
        await JS.InvokeVoidAsync("initLucide");

    /// <summary>
    /// Unsubscribes from events and suppresses finalization when the component is disposed.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose pattern implementation.
    /// </summary>
    /// <param name="disposing">Indica se está sendo chamado explicitamente.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Nav.LocationChanged -= HandleLocationChanged;
            EditorState.OnTabsChanged -= HandleTabsChanged;
        }

        _disposed = true;
    }
}