namespace MVFC.Apigee.Studio.Blazor.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    public required ToastService Toast { get; set; }
}