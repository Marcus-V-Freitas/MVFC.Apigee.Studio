using ApigeeLocalDev.Web.Application.Services;
using ApigeeLocalDev.Web.Domain.Models;
using Microsoft.Extensions.Options;

namespace ApigeeLocalDev.Web.Infrastructure.Services;

public sealed class WorkspaceOptions
{
    public const string SectionName = "Workspaces";
    public string WorkspacesRoot { get; set; } = string.Empty;
}

public sealed class WorkspaceService : IWorkspaceService
{
    private static readonly string[] EditableExtensions = [".xml", ".json", ".yaml", ".yml"];

    private readonly string _root;

    public WorkspaceService(IOptions<WorkspaceOptions> options, IWebHostEnvironment env)
    {
        var configured = options.Value.WorkspacesRoot;
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
    }

    public Task<IReadOnlyList<Workspace>> ListWorkspacesAsync()
    {
        if (!Directory.Exists(_root))
            return Task.FromResult<IReadOnlyList<Workspace>>([]);

        var workspaces = Directory
            .EnumerateDirectories(_root)
            .Select(dir => new Workspace
            {
                Name = Path.GetFileName(dir),
                FullPath = dir
            })
            .OrderBy(w => w.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<Workspace>>(workspaces);
    }

    public Task<WorkspaceDetail?> GetWorkspaceDetailAsync(string workspaceName)
    {
        // Validate workspace name is a simple directory name with no path separators
        if (!IsSimpleName(workspaceName))
            return Task.FromResult<WorkspaceDetail?>(null);

        var workspacePath = Path.Combine(_root, workspaceName);
        if (!Directory.Exists(workspacePath))
            return Task.FromResult<WorkspaceDetail?>(null);

        var apiProxies = ListSubfolderItems(workspacePath, "apiproxies");
        var sharedFlows = ListSubfolderItems(workspacePath, "sharedflows");
        var environments = ListSubfolderItems(workspacePath, "environments");

        var editableFiles = Directory
            .EnumerateFiles(workspacePath, "*", SearchOption.AllDirectories)
            .Where(f => EditableExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f => Path.GetRelativePath(workspacePath, f))
            .OrderBy(f => f)
            .ToList();

        var detail = new WorkspaceDetail
        {
            Name = workspaceName,
            FullPath = workspacePath,
            ApiProxies = apiProxies,
            SharedFlows = sharedFlows,
            Environments = environments,
            EditableFiles = editableFiles
        };

        return Task.FromResult<WorkspaceDetail?>(detail);
    }

    private static IReadOnlyList<string> ListSubfolderItems(string workspacePath, string subfolder)
    {
        var path = Path.Combine(workspacePath, subfolder);
        if (!Directory.Exists(path))
            return [];

        return Directory
            .EnumerateDirectories(path)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Returns true if <paramref name="name"/> is a single directory-name component
    /// (no path separators, no <c>..</c> or <c>.</c> as the whole name).
    /// </summary>
    private static bool IsSimpleName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Must not contain any path separator
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        // Reject "." and ".." traversal segments
        if (name is "." or "..")
            return false;

        return true;
    }
}
