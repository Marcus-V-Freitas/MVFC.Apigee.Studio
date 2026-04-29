namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class FlowBlock : ComponentBase
{
    [Parameter]
    public required FlowStructure Flow { get; set; }

    [Parameter]
    public EventCallback<string> OnStepClick { get; set; }
}
