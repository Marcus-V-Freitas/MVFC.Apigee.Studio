namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Contract for managing Apigee workspaces and their contents.
/// </summary>
public interface IWorkspaceRepository
{
    /// <summary>
    /// Lists all available workspaces.
    /// </summary>
    IReadOnlyList<ApigeeWorkspace> ListAll();

    /// <summary>
    /// Creates a new workspace.
    /// </summary>
    /// <param name="name">Workspace name.</param>
    /// <param name="customPath">Custom path for the workspace, or null to use the default location.</param>
    /// <param name="initialProxies">Optional list of initial proxies to include.</param>
    ApigeeWorkspace Create(string name, string? customPath, IReadOnlyList<string>? initialProxies = null);

    /// <summary>
    /// Deletes the specified workspace.
    /// </summary>
    void Delete(ApigeeWorkspace workspace);

    /// <summary>
    /// Loads the workspace tree structure asynchronously.
    /// </summary>
    Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default);

    /// <summary>
    /// Reads the contents of a file asynchronously.
    /// </summary>
    Task<string> ReadFileAsync(string absolutePath, CancellationToken ct = default);

    /// <summary>
    /// Saves content to a file asynchronously.
    /// </summary>
    Task SaveFileAsync(string absolutePath, string content, CancellationToken ct = default);

    /// <summary>
    /// Creates a new file asynchronously.
    /// </summary>
    Task CreateFileAsync(string absolutePath, CancellationToken ct = default);

    /// <summary>
    /// Creates a new directory asynchronously.
    /// </summary>
    Task CreateDirectoryAsync(string absolutePath, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file asynchronously.
    /// </summary>
    Task DeleteFileAsync(string absolutePath, CancellationToken ct = default);

    /// <summary>
    /// Deletes a directory asynchronously.
    /// </summary>
    Task DeleteDirectoryAsync(string absolutePath, CancellationToken ct = default);


    /// <summary>
    /// Builds a ZIP archive of the entire workspace.
    /// </summary>
    Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default);

    /// <summary>
    /// Lists all API proxies in the workspace.
    /// </summary>
    IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace);

    /// <summary>
    /// Lists all shared flows in the workspace.
    /// </summary>
    IReadOnlyList<string> ListSharedFlows(ApigeeWorkspace workspace);

    /// <summary>
    /// Registers an existing directory as a workspace without modifying its content.
    /// Creates a directory junction in the workspaces root pointing to the existing path.
    /// </summary>
    /// <param name="existingPath">The absolute path to the existing directory.</param>
    /// <returns>The registered <see cref="ApigeeWorkspace"/>.</returns>
    ApigeeWorkspace RegisterExisting(string existingPath);

    /// <summary>
    /// Ensures that the folder src/main/apigee/environments/{envName}/ exists on disk.
    /// Creates it empty if it does not exist.
    /// </summary>
    Task EnsureEnvironmentAsync(ApigeeWorkspace workspace, string envName, CancellationToken ct = default);

    /// <summary>
    /// Loads the test resources (mock plane) for the workspace.
    /// </summary>
    Task<TestResources> GetTestResourcesAsync(ApigeeWorkspace workspace, CancellationToken ct = default);

    /// <summary>
    /// Saves the test resources (mock plane) for the workspace.
    /// </summary>
    Task SaveTestResourcesAsync(ApigeeWorkspace workspace, TestResources resources, CancellationToken ct = default);

    /// <summary>
    /// Builds a ZIP archive of the test resources bundle (products, developers, apps).
    /// </summary>
    Task<string> BuildTestBundleZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default);
}