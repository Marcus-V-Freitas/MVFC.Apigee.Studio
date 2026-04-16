namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class TreeNode : ComponentBase
{
    // Pastas que merecem o botão "+" contextual
    // environments  -> cria novo environment (pasta + JSONs mínimos)
    // sharedflows   -> cria novo shared flow (skeleton)
    // apiproxies    -> cria novo proxy (skeleton)
    // policies      -> abre galeria de templates
    // resources / targets / proxies -> cria arquivo livre
    private static readonly HashSet<string> QuickAddFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "policies",
            "apiproxies",
            "sharedflows",
            "environments",
            "resources",
            "targets",
            "proxies"
        };

    [Parameter] 
    public required WorkspaceItem Item { get; set; }
    
    [Parameter] 
    public string? SelectedFilePath { get; set; }
    
    [Parameter] 
    public EventCallback<string> OnFileSelected { get; set; }

    [Parameter] 
    public EventCallback<(string Path, string Category)> OnQuickAdd { get; set; }
    
    [Parameter] 
    public EventCallback<(MouseEventArgs e, WorkspaceItem item)> OnContextMenu { get; set; }

    private bool _expanded = true;

    private string? QuickAddCategory =>
        QuickAddFolders.Contains(Item.Name) ? Item.Name.ToLowerInvariant() : null;

    private void ToggleExpand() => _expanded = !_expanded;

    private Task HandleQuickAdd()
        => OnQuickAdd.InvokeAsync((Item.FullPath, Item.Name.ToLowerInvariant()));

    private Task HandleContextMenu(MouseEventArgs e, WorkspaceItem item)
        => OnContextMenu.InvokeAsync((e, item));

    private static string GetDragPayload(WorkspaceItem item)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(item.Name);
        var dirName = Path.GetFileName(Path.GetDirectoryName(item.FullPath))?.ToLowerInvariant();

        if (dirName == "policies")
        {
            return $"<Step>\n    <Name>{nameWithoutExt}</Name>\n</Step>";
        }
        if (dirName == "targets")
        {
            return $"<RouteRule name=\"{nameWithoutExt}\">\n    <TargetEndpoint>{nameWithoutExt}</TargetEndpoint>\n</RouteRule>";
        }
        if (dirName == "sharedflows")
        {
            return $"<FlowCallout name=\"FlowCallout_{nameWithoutExt}\">\n    <SharedFlowBundle>{nameWithoutExt}</SharedFlowBundle>\n</FlowCallout>";
        }

        return nameWithoutExt;
    }
}
