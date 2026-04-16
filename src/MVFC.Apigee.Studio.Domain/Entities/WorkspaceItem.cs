namespace MVFC.Apigee.Studio.Domain.Entities;

public sealed record WorkspaceItem(
    string Name,
    string FullPath,
    string RelativePath,
    WorkspaceItemType Type,
    IReadOnlyList<WorkspaceItem> Children
);
