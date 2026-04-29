namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class FlowNavigator : ComponentBase
{
    [Parameter]
    public EndpointStructure? Endpoint { get; set; }

    [Parameter]
    public EventCallback<string> OnStepClick { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await JS.InvokeVoidAsync("initLucide");
    }

    [Inject]
    public required IJSRuntime JS { get; set; }
}