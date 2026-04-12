namespace ApigeeLocalDev.Web.Domain.Models;

public sealed class WorkspaceDetail
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public IReadOnlyList<string> ApiProxies { get; init; } = [];
    public IReadOnlyList<string> SharedFlows { get; init; } = [];
    public IReadOnlyList<string> Environments { get; init; } = [];
    public IReadOnlyList<string> EditableFiles { get; init; } = [];
}
