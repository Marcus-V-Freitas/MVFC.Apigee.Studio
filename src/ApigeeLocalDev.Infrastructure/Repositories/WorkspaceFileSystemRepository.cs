using System.IO.Compression;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ApigeeLocalDev.Infrastructure.Repositories;

public sealed class WorkspaceFileSystemRepository(IConfiguration configuration) : IWorkspaceRepository
{
    private static readonly string[] ProxySubFolders =
        ["apiproxy", "apiproxy/policies", "apiproxy/proxies", "apiproxy/targets", "apiproxy/resources"];

    private string WorkspacesRoot =>
        configuration["WorkspacesRoot"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "apigee-workspaces");

    public IReadOnlyList<ApigeeWorkspace> ListAll()
    {
        if (!Directory.Exists(WorkspacesRoot)) return [];
        return Directory
            .GetDirectories(WorkspacesRoot)
            .Select(d => new ApigeeWorkspace(Path.GetFileName(d), d))
            .ToList();
    }

    public ApigeeWorkspace Create(string name, string? customPath, IReadOnlyList<string>? initialProxies = null)
    {
        var fullPath = !string.IsNullOrWhiteSpace(customPath)
            ? customPath.Trim()
            : Path.Combine(WorkspacesRoot, name);

        Directory.CreateDirectory(Path.Combine(fullPath, "apiproxies"));
        Directory.CreateDirectory(Path.Combine(fullPath, "sharedflows"));
        Directory.CreateDirectory(Path.Combine(fullPath, "environments"));

        if (initialProxies is { Count: > 0 })
            foreach (var proxyName in initialProxies.Where(p => !string.IsNullOrWhiteSpace(p)))
                ScaffoldApiProxy(fullPath, proxyName.Trim());

        return new ApigeeWorkspace(name, fullPath);
    }

    public IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace)
    {
        var proxiesPath = Path.Combine(workspace.RootPath, "apiproxies");
        if (!Directory.Exists(proxiesPath)) return [];
        return Directory
            .GetDirectories(proxiesPath)
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(x => x)
            .ToList();
    }

    public Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
        => Task.FromResult(BuildItem(workspace.RootPath, workspace.RootPath));

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

    public Task<string> BuildBundleZipAsync(
        ApigeeWorkspace workspace, string proxyOrFlowName, CancellationToken ct = default)
    {
        var sourcePath = Path.Combine(workspace.RootPath, "apiproxies", proxyOrFlowName);
        if (!Directory.Exists(sourcePath))
            sourcePath = Path.Combine(workspace.RootPath, "sharedflows", proxyOrFlowName);
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException(
                $"Proxy or shared flow '{proxyOrFlowName}' not found in workspace '{workspace.Name}'.");

        var zipPath = Path.Combine(
            Path.GetTempPath(), $"{proxyOrFlowName}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        ZipFile.CreateFromDirectory(sourcePath, zipPath);
        return Task.FromResult(zipPath);
    }

    public Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var zipPath = Path.Combine(
            Path.GetTempPath(), $"{workspace.Name}_full_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        ZipFile.CreateFromDirectory(workspace.RootPath, zipPath);
        return Task.FromResult(zipPath);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static void ScaffoldApiProxy(string workspaceRoot, string proxyName)
    {
        var baseDir = Path.Combine(workspaceRoot, "apiproxies", proxyName);
        foreach (var sub in ProxySubFolders)
            Directory.CreateDirectory(Path.Combine(baseDir, sub));

        // Com $$$""" cada expressao C# exige {{{expr}}} e o XML com { } literal nao conflita.
        var proxyXml = $$$"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <ProxyEndpoint name="default">
                <Description>{{{proxyName}}} proxy endpoint</Description>
                <HTTPProxyConnection>
                    <BasePath>/{{{proxyName}}}</BasePath>
                    <VirtualHost>default</VirtualHost>
                </HTTPProxyConnection>
                <RouteRule name="default">
                    <TargetEndpoint>default</TargetEndpoint>
                </RouteRule>
            </ProxyEndpoint>
            """;

        var targetXml = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <TargetEndpoint name="default">
                <Description>Default target endpoint</Description>
                <HTTPTargetConnection>
                    <URL>https://httpbin.org/anything</URL>
                </HTTPTargetConnection>
            </TargetEndpoint>
            """;

        File.WriteAllText(
            Path.Combine(baseDir, "apiproxy", "proxies", "default.xml"), proxyXml);
        File.WriteAllText(
            Path.Combine(baseDir, "apiproxy", "targets", "default.xml"), targetXml);
    }

    private static WorkspaceItem BuildItem(string path, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);
        var name         = Path.GetFileName(path);

        if (File.Exists(path))
            return new WorkspaceItem(name, path, relativePath, WorkspaceItemType.File, []);

        var itemType = Path.GetFileName(path).ToLowerInvariant() switch
        {
            "apiproxies"   => WorkspaceItemType.ApiProxy,
            "sharedflows"  => WorkspaceItemType.SharedFlow,
            "environments" => WorkspaceItemType.Environment,
            _              => WorkspaceItemType.Directory
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
