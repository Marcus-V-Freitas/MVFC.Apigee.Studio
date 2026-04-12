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

        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return null;

        var workspacePath = Path.GetFullPath(Path.Combine(_root, workspaceName));
        // Guard against path traversal in workspace name
        if (!workspacePath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        var fullPath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));
        // Guard against path traversal in relative file path
        if (!fullPath.StartsWith(workspacePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        return fullPath;
    }
}
