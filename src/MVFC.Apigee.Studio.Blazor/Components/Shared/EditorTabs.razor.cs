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

    private async Task Switch(EditorTab tab) => await OnTabSwitch.InvokeAsync(tab);
    private async Task Close(EditorTab tab) => await OnTabClose.InvokeAsync(tab);
}
