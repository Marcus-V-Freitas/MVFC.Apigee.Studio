namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class MonacoEditor : ComponentBase, IAsyncDisposable
{
    private bool _editorCreated = false;

    [Parameter]
    public string Id { get; set; } = "monaco-editor";

    [Parameter]
    public string CssClass { get; set; } = "monaco-container";

    [Parameter]
    public string InitialContent { get; set; } = string.Empty;

    [Parameter]
    public string FilePath { get; set; } = string.Empty;

    [Parameter]
    public EventCallback OnSave { get; set; }

    [Inject]
    public required IJSRuntime JS { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || !_editorCreated)
        {
            _editorCreated = true;
            await JS.InvokeVoidAsync("monacoInterop.create", Id, InitialContent, FilePath);
        }
    }

    public async Task SetValue(string content, string filePath)
    {
        await JS.InvokeVoidAsync("monacoInterop.setValue", Id, content, filePath);
        await JS.InvokeVoidAsync("monacoInterop.clearDirty", Id);
    }

    public async Task<string> GetValue() =>
        await JS.InvokeAsync<string>("monacoInterop.getValue", Id);

    public async Task<bool> IsDirty() =>
        await JS.InvokeAsync<bool>("monacoInterop.isDirty", Id);

    public async Task ClearDirty() =>
        await JS.InvokeVoidAsync("monacoInterop.clearDirty", Id);

    public async Task FormatDocument() =>
        await JS.InvokeVoidAsync("monacoInterop.formatDocument", Id);

    public async Task SetMarkers(IEnumerable<object> markers) =>
        await JS.InvokeVoidAsync("monacoInterop.setMarkers", Id, markers);

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        try
        {
            await JS.InvokeVoidAsync("monacoInterop.dispose", Id);
        }
        catch { }
    }
}
