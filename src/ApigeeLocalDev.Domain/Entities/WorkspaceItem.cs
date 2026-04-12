namespace ApigeeLocalDev.Domain.Entities;

public enum WorkspaceItemType { ApiProxy, SharedFlow, Environment, File, Directory }

public sealed record WorkspaceItem(
    string Name,
    string FullPath,
    string RelativePath,
    WorkspaceItemType Type,
    IReadOnlyList<WorkspaceItem> Children
);
