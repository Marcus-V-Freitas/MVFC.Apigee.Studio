namespace MVFC.Apigee.Studio.Blazor.Components.Shared;

public partial class FileExplorer : ComponentBase
{
    [Parameter]
    public WorkspaceItem? Tree { get; set; }

    [Parameter]
    public string? SelectedFilePath { get; set; }

    [Parameter]
    public string SearchQuery { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> SearchQueryChanged { get; set; }

    [Parameter]
    public EventCallback<string> OnFileSelected { get; set; }

    [Parameter]
    public EventCallback<(string Path, string Category)> OnQuickAdd { get; set; }

    [Parameter]
    public EventCallback<(MouseEventArgs e, WorkspaceItem item)> OnContextMenu { get; set; }

    [Parameter]
    public EventCallback<bool> OnNewItem { get; set; }

    private WorkspaceItem? FilteredTree => Filter(Tree);

    private async Task HandleSearchInput(ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString() ?? string.Empty;
        await SearchQueryChanged.InvokeAsync(newValue);
    }

    private WorkspaceItem? Filter(WorkspaceItem? root)
    {
        if (root is null) return null;
        if (string.IsNullOrWhiteSpace(SearchQuery)) return root;

        return FilterNode(root, SearchQuery);
    }

    private static WorkspaceItem? FilterNode(WorkspaceItem node, string query)
    {
        if (node.Type is WorkspaceItemType.File)
        {
            if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return node;
            return null;
        }

        var filteredChildren = new List<WorkspaceItem>();
        foreach (var child in node.Children)
        {
            var filteredChild = FilterNode(child, query);
            if (filteredChild is not null)
                filteredChildren.Add(filteredChild);
        }

        if (filteredChildren.Count != 0 || node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return new WorkspaceItem(
                node.Name,
                node.FullPath,
                node.RelativePath,
                node.Type,
                filteredChildren
            );
        }

        return null;
    }
}