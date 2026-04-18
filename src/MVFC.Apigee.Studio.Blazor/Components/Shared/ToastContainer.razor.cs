namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

/// <summary>
/// Blazor component that displays toast notifications on the screen.
/// Listens to the <see cref="ToastService"/> for new messages and manages their display, animation, and removal.
/// </summary>
public partial class ToastContainer : ComponentBase, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// List of currently displayed toast entries.
    /// </summary>
    private readonly List<ToastEntry> _toasts = [];

    /// <summary>
    /// Service for receiving and displaying toast notifications.
    /// </summary>
    [Inject]
    public required ToastService Toast { get; set; }

    /// <summary>
    /// JavaScript runtime for invoking JS interop (e.g., icon initialization).
    /// </summary>
    [Inject]
    public required IJSRuntime JS { get; set; }

    /// <summary>
    /// Subscribes to toast events on initialization.
    /// </summary>
    protected override void OnInitialized()
        => Toast.OnShow += HandleToast;

    /// <summary>
    /// Handles the display and lifecycle of a new toast message.
    /// Adds the toast, triggers UI updates and animations, and removes it after a delay.
    /// </summary>
    /// <param name="msg">The toast message to display.</param>
    private async void HandleToast(ToastMessage msg)
    {
        var entry = new ToastEntry(msg.Id, msg.Message, msg.Level);
        _toasts.Add(entry);
        await InvokeAsync(StateHasChanged);
        await Task.Delay(50);
        await InvokeAsync(async () =>
        {
            await Lucide();
            StateHasChanged();
        });

        await Task.Delay(3800);
        entry.Exiting = true;
        await InvokeAsync(StateHasChanged);
        await Task.Delay(350);
        _toasts.Remove(entry);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Dismisses a toast entry manually, triggering its exit animation and removal.
    /// </summary>
    /// <param name="entry">The toast entry to dismiss.</param>
    private async Task Dismiss(ToastEntry entry)
    {
        entry.Exiting = true;
        StateHasChanged();

        await Task.Delay(350);
        _toasts.Remove(entry);
    }

    /// <summary>
    /// Returns the icon name for the specified toast level.
    /// </summary>
    /// <param name="l">The toast level.</param>
    /// <returns>The icon name as a string.</returns>
    private static string IconFor(ToastLevel l) => l switch
    {
        ToastLevel.Success => "circle-check",
        ToastLevel.Error => "circle-x",
        ToastLevel.Warning => "triangle-alert",
        _ => "info",
    };

    /// <summary>
    /// Invokes JavaScript to initialize Lucide icons.
    /// </summary>
    private async ValueTask Lucide() =>
        await JS.InvokeVoidAsync("initLucide");

    /// <summary>
    /// Unsubscribes from toast events and suppresses finalization when the component is disposed.
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
            Toast.OnShow -= HandleToast;
            // Libere outros recursos gerenciados aqui, se necessário.
        }

        // Libere recursos não gerenciados aqui, se houver.

        _disposed = true;
    }
}