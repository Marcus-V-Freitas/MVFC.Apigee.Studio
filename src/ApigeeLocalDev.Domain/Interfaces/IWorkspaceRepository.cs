using ApigeeLocalDev.Domain.Entities;

namespace ApigeeLocalDev.Domain.Interfaces;

public interface IWorkspaceRepository
{
    IReadOnlyList<ApigeeWorkspace> ListAll();
    ApigeeWorkspace Create(string name, string path);
    Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default);
    Task<string> ReadFileAsync(string absolutePath, CancellationToken ct = default);
    Task SaveFileAsync(string absolutePath, string content, CancellationToken ct = default);
    Task<string> BuildBundleZipAsync(ApigeeWorkspace workspace, string proxyOrFlowName, CancellationToken ct = default);
}
