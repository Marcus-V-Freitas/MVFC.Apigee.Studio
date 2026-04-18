namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents an item within a workspace, such as a file, directory, API proxy, shared flow, or environment.
/// </summary>
/// <param name="Name">The display name of the item.</param>
/// <param name="FullPath">The absolute path to the item on disk.</param>
/// <param name="RelativePath">The path to the item relative to the workspace root.</param>
/// <param name="Type">The type of the workspace item (e.g., File, Directory, ApiProxy, SharedFlow, Environment).</param>
/// <param name="Children">The child items contained within this item, if any.</param>
public sealed record WorkspaceItem(
    string Name,
    string FullPath,
    string RelativePath,
    WorkspaceItemType Type,
    IReadOnlyList<WorkspaceItem> Children
);
