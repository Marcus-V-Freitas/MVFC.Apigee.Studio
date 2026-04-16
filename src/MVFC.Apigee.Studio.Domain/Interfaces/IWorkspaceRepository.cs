namespace MVFC.Apigee.Studio.Domain.Interfaces;

public interface IWorkspaceRepository
{
    IReadOnlyList<ApigeeWorkspace> ListAll();
    
    ApigeeWorkspace Create(string name, string? customPath, IReadOnlyList<string>? initialProxies = null);
    
    void Delete(ApigeeWorkspace workspace);

    Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default);

    Task<string> ReadFileAsync(string absolutePath, CancellationToken ct = default);
    
    Task SaveFileAsync(string absolutePath, string content, CancellationToken ct = default);
    
    Task CreateFileAsync(string absolutePath, CancellationToken ct = default);
    
    Task CreateDirectoryAsync(string absolutePath, CancellationToken ct = default);
    
    Task DeleteFileAsync(string absolutePath, CancellationToken ct = default);
    
    Task DeleteDirectoryAsync(string absolutePath, CancellationToken ct = default);

    Task<string> BuildBundleZipAsync(ApigeeWorkspace workspace, string proxyOrFlowName, CancellationToken ct = default);
    
    Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default);

    IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace);
    
    IReadOnlyList<string> ListSharedFlows(ApigeeWorkspace workspace);

    /// <summary>
    /// Garante que a pasta src/main/apigee/environments/{envName}/ exista no disco.
    /// Criada vazia se ainda não existir.
    /// </summary>
    Task EnsureEnvironmentAsync(ApigeeWorkspace workspace, string envName, CancellationToken ct = default);
}
