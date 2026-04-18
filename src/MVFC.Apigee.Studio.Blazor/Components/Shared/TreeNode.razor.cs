namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

/// <summary>
/// <para>
/// Blazor component representing a node in the workspace file tree.
/// Supports expand/collapse, file selection, context menu, and quick add actions for specific folders.
/// </para>
/// <para>Quick add examples:</para>
/// <code>
/// // environments  -> creates a new environment (folder + minimal JSONs)
/// // sharedflows   -> creates a new shared flow (skeleton)
/// // apiproxies    -> creates a new proxy (skeleton)
/// // policies      -> opens template gallery
/// // resources / targets / proxies -> creates a free-form file
/// </code>
/// </summary>
public partial class TreeNode : ComponentBase
{
    /// <summary>
    /// Set of folder names that support the contextual "+" quick add button.
    /// </summary>
    private static readonly HashSet<string> QuickAddFolders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "policies",
            "apiproxies",
            "sharedflows",
            "environments",
            "resources",
            "targets",
            "proxies",
        };

    /// <summary>
    /// The workspace item represented by this tree node.
    /// </summary>
    [Parameter]
    public required WorkspaceItem Item { get; set; }

    /// <summary>
    /// The currently selected file path in the tree.
    /// </summary>
    [Parameter]
    public string? SelectedFilePath { get; set; }

    /// <summary>
    /// Event callback triggered when a file is selected.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnFileSelected { get; set; }

    /// <summary>
    /// Event callback triggered when the quick add button is clicked.
    /// The tuple contains the path and the quick add category.
    /// </summary>
    [Parameter]
    public EventCallback<(string Path, string Category)> OnQuickAdd { get; set; }

    /// <summary>
    /// Event callback triggered when the context menu is opened for a workspace item.
    /// </summary>
    [Parameter]
    public EventCallback<(MouseEventArgs e, WorkspaceItem item)> OnContextMenu { get; set; }

    /// <summary>
    /// Indicates whether the node is expanded in the tree.
    /// </summary>
    private bool _expanded = true;

    /// <summary>
    /// Gets the quick add category for the current item, or null if not supported.
    /// </summary>
    private string? QuickAddCategory =>
        QuickAddFolders.Contains(Item.Name) ? Item.Name.ToLowerInvariant() : null;

    /// <summary>
    /// Toggles the expanded/collapsed state of the node.
    /// </summary>
    private void ToggleExpand() => _expanded = !_expanded;

    /// <summary>
    /// Handles the quick add action for the current item.
    /// </summary>
    private Task HandleQuickAdd()
        => OnQuickAdd.InvokeAsync((Item.FullPath, Item.Name.ToLowerInvariant()));

    /// <summary>
    /// Handles the context menu event for the given workspace item.
    /// </summary>
    /// <param name="e">The mouse event arguments.</param>
    /// <param name="item">The workspace item for which the context menu is opened.</param>
    private Task HandleContextMenu(MouseEventArgs e, WorkspaceItem item)
        => OnContextMenu.InvokeAsync((e, item));

    /// <summary>
    /// Returns a drag-and-drop payload string for the given workspace item.
    /// Example payloads:
    /// <code>
    /// // For a policy file:
    /// &lt;Step&gt;
    ///     &lt;Name&gt;MyPolicy&lt;/Name&gt;
    /// &lt;/Step&gt;
    ///
    /// // For a target file:
    /// &lt;RouteRule name="MyTarget"&gt;
    ///     &lt;TargetEndpoint&gt;MyTarget&lt;/TargetEndpoint&gt;
    /// &lt;/RouteRule&gt;
    ///
    /// // For a shared flow file:
    /// &lt;FlowCallout name="FlowCallout_MyFlow"&gt;
    ///     &lt;SharedFlowBundle&gt;MyFlow&lt;/SharedFlowBundle&gt;
    /// &lt;/FlowCallout&gt;
    /// </code>
    /// </summary>
    /// <param name="item">The workspace item to generate the payload for.</param>
    /// <returns>A string representing the drag payload.</returns>
    private static string GetDragPayload(WorkspaceItem item)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(item.Name);
        var dirName = Path.GetFileName(Path.GetDirectoryName(item.FullPath))?.ToLowerInvariant();

        if (string.Equals(dirName, "policies", StringComparison.OrdinalIgnoreCase))
        {
            return $"<Step>\n    <Name>{nameWithoutExt}</Name>\n</Step>";
        }
        if (string.Equals(dirName, "targets", StringComparison.OrdinalIgnoreCase))
        {
            return $"<RouteRule name=\"{nameWithoutExt}\">\n    <TargetEndpoint>{nameWithoutExt}</TargetEndpoint>\n</RouteRule>";
        }
        if (string.Equals(dirName, "sharedflows", StringComparison.OrdinalIgnoreCase))
        {
            return $"<FlowCallout name=\"FlowCallout_{nameWithoutExt}\">\n    <SharedFlowBundle>{nameWithoutExt}</SharedFlowBundle>\n</FlowCallout>";
        }

        return nameWithoutExt;
    }
}
