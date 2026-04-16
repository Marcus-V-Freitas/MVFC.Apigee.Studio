namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class ToastContainer : ComponentBase, IDisposable
{
    private readonly List<ToastEntry> _toasts = [];

    [Inject]
    public required ToastService Toast { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    protected override void OnInitialized()
        => Toast.OnShow += HandleToast;

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

    private async Task Dismiss(ToastEntry entry)
    {
        entry.Exiting = true;
        StateHasChanged();

        await Task.Delay(350);
        _toasts.Remove(entry);
    }

    private static string IconFor(ToastLevel l) => l switch
    {
        ToastLevel.Success => "circle-check",
        ToastLevel.Error => "circle-x",
        ToastLevel.Warning => "triangle-alert",
        _ => "info",
    };
    
    private async ValueTask Lucide() => 
        await JS.InvokeVoidAsync("initLucide");

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Toast.OnShow -= HandleToast;
    }
}