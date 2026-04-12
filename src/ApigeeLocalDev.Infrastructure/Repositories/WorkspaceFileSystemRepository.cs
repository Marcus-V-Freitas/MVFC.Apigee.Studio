using System.IO.Compression;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ApigeeLocalDev.Infrastructure.Repositories;

public sealed class WorkspaceFileSystemRepository(IConfiguration configuration) : IWorkspaceRepository
{
    private string WorkspacesRoot =>
        configuration["WorkspacesRoot"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "apigee-workspaces");

    public IReadOnlyList<ApigeeWorkspace> ListAll()
    {
        if (!Directory.Exists(WorkspacesRoot))
            return [];

        return Directory
            .GetDirectories(WorkspacesRoot)
            .Select(d => new ApigeeWorkspace(Path.GetFileName(d), d))
            .ToList();
    }

    // Se customPath for preenchido, usa ele como raiz do workspace.
    // Caso contrário, cria dentro de WorkspacesRoot/name.
    public ApigeeWorkspace Create(string name, string? customPath)
    {
        var fullPath = !string.IsNullOrWhiteSpace(customPath)
            ? customPath.Trim()
            : Path.Combine(WorkspacesRoot, name);

        Directory.CreateDirectory(Path.Combine(fullPath, "apiproxies"));
        Directory.CreateDirectory(Path.Combine(fullPath, "sharedflows"));
        Directory.CreateDirectory(Path.Combine(fullPath, "environments"));
        return new ApigeeWorkspace(name, fullPath);
    }

    public Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var root = BuildItem(workspace.RootPath, workspace.RootPath);
        return Task.FromResult(root);
    }

    public Task<string> ReadFileAsync(string absolutePath, CancellationToken ct = default)
        => File.ReadAllTextAsync(absolutePath, ct);

    public Task SaveFileAsync(string absolutePath, string content, CancellationToken ct = default)
        => File.WriteAllTextAsync(absolutePath, content, ct);

    public async Task CreateFileAsync(string absolutePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        if (!File.Exists(absolutePath))
            await File.WriteAllTextAsync(absolutePath, string.Empty, ct);
    }

    public Task CreateDirectoryAsync(string absolutePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(absolutePath);
        return Task.CompletedTask;
    }

    // Zipa um proxy ou shared flow específico
    public Task<string> BuildBundleZipAsync(ApigeeWorkspace workspace, string proxyOrFlowName, CancellationToken ct = default)
    {
        var sourcePath = Path.Combine(workspace.RootPath, "apiproxies", proxyOrFlowName);
        if (!Directory.Exists(sourcePath))
            sourcePath = Path.Combine(workspace.RootPath, "sharedflows", proxyOrFlowName);

        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Proxy or shared flow '{proxyOrFlowName}' not found in workspace '{workspace.Name}'.");

        var zipPath = Path.Combine(Path.GetTempPath(), $"{proxyOrFlowName}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        ZipFile.CreateFromDirectory(sourcePath, zipPath);
        return Task.FromResult(zipPath);
    }

    // Zipa o workspace inteiro (todos os proxies, sharedflows e environments)
    public Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"{workspace.Name}_full_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        ZipFile.CreateFromDirectory(workspace.RootPath, zipPath);
        return Task.FromResult(zipPath);
    }

    private static WorkspaceItem BuildItem(string path, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);
        var name = Path.GetFileName(path);

        if (File.Exists(path))
            return new WorkspaceItem(name, path, relativePath, WorkspaceItemType.File, []);

        var dirName = Path.GetFileName(path).ToLowerInvariant();
        var itemType = dirName switch
        {
            "apiproxies"  => WorkspaceItemType.ApiProxy,
            "sharedflows" => WorkspaceItemType.SharedFlow,
            "environments" => WorkspaceItemType.Environment,
            _ => WorkspaceItemType.Directory
        };

        var children = Directory
            .GetFileSystemEntries(path)
            .OrderBy(p => File.Exists(p) ? 1 : 0)
            .ThenBy(p => p)
            .Select(child => BuildItem(child, rootPath))
            .ToList();

        return new WorkspaceItem(name, path, relativePath, itemType, children);
    }
}
