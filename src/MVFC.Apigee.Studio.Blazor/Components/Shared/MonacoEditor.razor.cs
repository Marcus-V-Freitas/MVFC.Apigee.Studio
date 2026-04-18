namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

/// <summary>
/// Blazor component wrapper for the Monaco code editor, providing integration with JavaScript interop for editing, formatting, and diagnostics.
/// Supports setting and retrieving content, tracking dirty state, formatting, and setting markers.
/// </summary>
public partial class MonacoEditor : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// Indicates whether the editor has been created in the DOM.
    /// </summary>
    private bool _editorCreated;

    /// <summary>
    /// The HTML element ID for the Monaco editor instance.
    /// </summary>
    [Parameter]
    public string Id { get; set; } = "monaco-editor";

    /// <summary>
    /// The CSS class applied to the editor container.
    /// </summary>
    [Parameter]
    public string CssClass { get; set; } = "monaco-container";

    /// <summary>
    /// The initial content to load into the editor.
    /// </summary>
    [Parameter]
    public string InitialContent { get; set; } = string.Empty;

    /// <summary>
    /// The file path associated with the editor content (used for language detection, etc).
    /// </summary>
    [Parameter]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Event callback triggered when the user requests to save the file (e.g., via keyboard shortcut).
    /// </summary>
    [Parameter]
    public EventCallback OnSave { get; set; }

    /// <summary>
    /// JavaScript runtime for invoking Monaco editor interop methods.
    /// </summary>
    [Inject]
    public required IJSRuntime JS { get; set; }

    /// <summary>
    /// Initializes the Monaco editor instance after the component is rendered.
    /// </summary>
    /// <param name="firstRender">Indicates if this is the first render.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || !_editorCreated)
        {
            _editorCreated = true;
            await JS.InvokeVoidAsync("monacoInterop.create", Id, InitialContent, FilePath);
        }
    }

    /// <summary>
    /// Sets the editor content and file path, and clears the dirty state.
    /// </summary>
    /// <param name="content">The content to set in the editor.</param>
    /// <param name="filePath">The file path associated with the content.</param>
    public async Task SetValue(string content, string filePath)
    {
        await JS.InvokeVoidAsync("monacoInterop.setValue", Id, content, filePath);
        await JS.InvokeVoidAsync("monacoInterop.clearDirty", Id);
    }

    /// <summary>
    /// Gets the current content from the editor.
    /// </summary>
    /// <returns>The editor content as a string.</returns>
    public async Task<string> GetValue() =>
        await JS.InvokeAsync<string>("monacoInterop.getValue", Id);

    /// <summary>
    /// Determines if the editor content has unsaved changes.
    /// </summary>
    /// <returns>True if the editor is dirty; otherwise, false.</returns>
    public async Task<bool> IsDirty() =>
        await JS.InvokeAsync<bool>("monacoInterop.isDirty", Id);

    /// <summary>
    /// Clears the dirty state of the editor (marks as not having unsaved changes).
    /// </summary>
    public async Task ClearDirty() =>
        await JS.InvokeVoidAsync("monacoInterop.clearDirty", Id);

    /// <summary>
    /// Formats the current document in the editor.
    /// </summary>
    public async Task FormatDocument() =>
        await JS.InvokeVoidAsync("monacoInterop.formatDocument", Id);

    /// <summary>
    /// Sets diagnostic markers (e.g., lint errors) in the editor.
    /// </summary>
    /// <param name="markers">A collection of marker objects to display.</param>
    public async Task SetMarkers(IEnumerable<object> markers) =>
        await JS.InvokeVoidAsync("monacoInterop.setMarkers", Id, markers);

    /// <summary>
    /// Disposes the Monaco editor instance and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        try
        {
            await JS.InvokeVoidAsync("monacoInterop.dispose", Id);
        }
        catch
        {
            // Ignore errors during disposal (e.g., if JS runtime is already gone)
        }
    }
}
