namespace ApigeeLocalDev.Application.UseCases;

/// <summary>
/// Use case for creating a new Apigee workspace, optionally at a custom path and with initial proxies.
/// </summary>
public sealed class CreateWorkspaceUseCase(IWorkspaceRepository repository)
{
    /// <summary>
    /// Creates a new Apigee workspace.
    /// </summary>
    /// <param name="name">The name of the workspace to create.</param>
    /// <param name="customPath">
    /// Optional absolute path for the workspace. If null or empty, the path will be WorkspacesRoot/name.
    /// </param>
    /// <param name="initialProxies">
    /// Optional list of proxy names to create with full structure (apiproxy/, policies/, proxies/, targets/).
    /// </param>
    /// <returns>The created <see cref="ApigeeWorkspace"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null or whitespace.</exception>
    public ApigeeWorkspace Execute(string name, string? customPath, IReadOnlyList<string>? initialProxies = null)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(name);

        return repository.Create(name, customPath, initialProxies);
    }
}
