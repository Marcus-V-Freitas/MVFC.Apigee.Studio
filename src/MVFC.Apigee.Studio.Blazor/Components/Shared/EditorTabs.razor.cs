namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class EditorTabs : ComponentBase
{
    [Parameter]
    public IEnumerable<EditorTab> Tabs { get; set; } = [];

    [Parameter]
    public EditorTab? ActiveTab { get; set; }

    [Parameter]
    public EventCallback<EditorTab> OnTabSwitch { get; set; }

    [Parameter]
    public EventCallback<EditorTab> OnTabClose { get; set; }

    [Parameter]
    public EventCallback<EditorTab> OnCloseOtherTabs { get; set; }

    [Parameter]
    public EventCallback OnCloseAllTabs { get; set; }

    private bool _showTabCtx;
    private double _ctxX;
    private double _ctxY;
    private EditorTab? _ctxTab;

    private void HandleTabContextMenu(MouseEventArgs e, EditorTab tab)
    {
        _ctxX = e.ClientX;
        _ctxY = e.ClientY;
        _ctxTab = tab;
        _showTabCtx = true;
    }

    private void CloseTabContextMenu() => _showTabCtx = false;

    private async Task CtxCloseThis()
    {
        _showTabCtx = false;
        if (_ctxTab is not null)
            await Close(_ctxTab);
    }

    private async Task CtxCloseOthers()
    {
        _showTabCtx = false;
        if (_ctxTab is not null)
            await OnCloseOtherTabs.InvokeAsync(_ctxTab);
    }

    private async Task CtxCloseAll()
    {
        _showTabCtx = false;
        await OnCloseAllTabs.InvokeAsync();
    }

    private async Task Switch(EditorTab tab) => await OnTabSwitch.InvokeAsync(tab);

    private async Task Close(EditorTab tab) => await OnTabClose.InvokeAsync(tab);
}
