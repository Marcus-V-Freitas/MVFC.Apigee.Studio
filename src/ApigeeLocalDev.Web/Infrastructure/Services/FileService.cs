using ApigeeLocalDev.Web.Application.Services;
using ApigeeLocalDev.Web.Domain.Models;
using Microsoft.Extensions.Options;

namespace ApigeeLocalDev.Web.Infrastructure.Services;

public sealed class FileService : IFileService
{
    private static readonly string[] AllowedExtensions = [".xml", ".json", ".yaml", ".yml"];

    private readonly string _root;

    public FileService(IOptions<WorkspaceOptions> options, IWebHostEnvironment env)
    {
        var configured = options.Value.WorkspacesRoot;
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
    }

    public async Task<TextFile?> ReadFileAsync(string workspaceName, string relativePath)
    {
        var fullPath = ResolveSafePath(workspaceName, relativePath);
        if (fullPath is null || !File.Exists(fullPath))
            return null;

        var content = await File.ReadAllTextAsync(fullPath);
        return new TextFile
        {
            RelativePath = relativePath,
            FullPath = fullPath,
            Content = content
        };
    }

    public async Task SaveFileAsync(string workspaceName, string relativePath, string content)
    {
        var fullPath = ResolveSafePath(workspaceName, relativePath);
        if (fullPath is null)
            throw new InvalidOperationException($"Invalid file path: {relativePath}");

        await File.WriteAllTextAsync(fullPath, content);
    }

    private string? ResolveSafePath(string workspaceName, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        // Workspace name must be a simple directory name (no separators)
        if (!IsSimpleName(workspaceName))
            return null;

        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return null;

        var workspacePath = Path.Combine(_root, workspaceName);
        var fullPath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));

        // Guard against path traversal: resolved path must stay inside the workspace
        var relative = Path.GetRelativePath(workspacePath, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            return null;

        return fullPath;
    }

    /// <summary>
    /// Returns true if <paramref name="name"/> is a single directory-name component
    /// (no path separators, no <c>..</c> or <c>.</c> as the whole name).
    /// </summary>
    private static bool IsSimpleName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        if (name is "." or "..")
            return false;

        return true;
    }
}
