namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class DeployPreviewDialog : ComponentBase
{
    [Parameter]
    public LintResult? StructuralLint { get; set; }

    [Parameter]
    public IList<ApigeeLintResult>? DeepLint { get; set; }

    [Parameter]
    public BundleDiff? Diff { get; set; }

    [Parameter]
    public EventCallback OnConfirm { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private bool IsDeployBlocked => StructuralLint?.IsValid == false || (DeepLint?.Any(r => r.Messages.Any(m => m.Severity == 2 && !m.Message.Contains("apigeelint", StringComparison.Ordinal))) ?? false);

    private bool IsApigeeLintMissing => DeepLint?.Any(r => r.Messages.Any(m =>
        m.Message.Contains("apigeelint", StringComparison.OrdinalIgnoreCase) &&
        (m.Message.Contains("installed", StringComparison.OrdinalIgnoreCase) || m.Message.Contains("reconhecido", StringComparison.OrdinalIgnoreCase)))) ?? false;
}
