using System.IO.Compression;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ApigeeLocalDev.Infrastructure.Repositories;

public sealed class WorkspaceFileSystemRepository(IConfiguration configuration) : IWorkspaceRepository
{
    private static readonly string[] ProxySubFolders =
        ["apiproxy", "apiproxy/policies", "apiproxy/proxies", "apiproxy/targets", "apiproxy/resources"];

    // Prefixo exigido pelo Apigee Emulator dentro do ZIP e no disco
    private const string ApigeeRelPath = "src/main/apigee";

    private string WorkspacesRoot =>
        configuration["WorkspacesRoot"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "apigee-workspaces");

    // Retorna o caminho src/main/apigee dentro do workspace
    private static string ApigeeRoot(ApigeeWorkspace workspace)
        => Path.Combine(workspace.RootPath, "src", "main", "apigee");

    // ── lista ────────────────────────────────────────────────────────────────

    public IReadOnlyList<ApigeeWorkspace> ListAll()
    {
        if (!Directory.Exists(WorkspacesRoot)) return [];
        return Directory
            .GetDirectories(WorkspacesRoot)
            .Select(d => new ApigeeWorkspace(Path.GetFileName(d), d))
            .ToList();
    }

    public IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "apiproxies");
        if (!Directory.Exists(path)) return [];
        return Directory.GetDirectories(path)
            .Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x).ToList();
    }

    public IReadOnlyList<string> ListSharedFlows(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "sharedflows");
        if (!Directory.Exists(path)) return [];
        return Directory.GetDirectories(path)
            .Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x).ToList();
    }

    // ── CRUD ─────────────────────────────────────────────────────────────────

    public ApigeeWorkspace Create(string name, string? customPath, IReadOnlyList<string>? initialProxies = null)
    {
        var fullPath = !string.IsNullOrWhiteSpace(customPath)
            ? customPath.Trim()
            : Path.Combine(WorkspacesRoot, name);

        var apigeeRoot = Path.Combine(fullPath, "src", "main", "apigee");

        Directory.CreateDirectory(Path.Combine(apigeeRoot, "apiproxies"));
        Directory.CreateDirectory(Path.Combine(apigeeRoot, "sharedflows"));
        Directory.CreateDirectory(Path.Combine(apigeeRoot, "environments"));

        if (initialProxies is { Count: > 0 })
            foreach (var p in initialProxies.Where(p => !string.IsNullOrWhiteSpace(p)))
                ScaffoldApiProxy(apigeeRoot, p.Trim());

        return new ApigeeWorkspace(name, fullPath);
    }

    public void Delete(ApigeeWorkspace workspace)
    {
        if (Directory.Exists(workspace.RootPath))
            Directory.Delete(workspace.RootPath, recursive: true);
    }

    // ── árvore e arquivos ────────────────────────────────────────────────────

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

    public Task DeleteFileAsync(string absolutePath, CancellationToken ct = default)
    {
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
        return Task.CompletedTask;
    }

    // ── ZIP helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// ZIP de um proxy/shared flow individual (usado para deploys avulsos futuros).
    /// </summary>
    public Task<string> BuildBundleZipAsync(
        ApigeeWorkspace workspace, string proxyOrFlowName, CancellationToken ct = default)
    {
        var apigeeRoot = ApigeeRoot(workspace);
        var proxySrc   = Path.Combine(apigeeRoot, "apiproxies",  proxyOrFlowName);
        var sfSrc      = Path.Combine(apigeeRoot, "sharedflows", proxyOrFlowName);

        string sourceDir;
        if (Directory.Exists(proxySrc))
            sourceDir = proxySrc;
        else if (Directory.Exists(sfSrc))
            sourceDir = sfSrc;
        else
            throw new DirectoryNotFoundException(
                "Proxy or shared flow '" + proxyOrFlowName + "' not found in workspace.");

        var zip = Path.Combine(Path.GetTempPath(),
            proxyOrFlowName + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");

        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            AddDirectoryToZip(archive, sourceDir, string.Empty);

        return Task.FromResult(zip);
    }

    /// <summary>
    /// ZIP do workspace completo mantendo a estrutura src/main/apigee/...
    /// exigida pelo endpoint POST /v1/emulator/deploy?environment={env}.
    /// </summary>
    public Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var apigeeRoot = ApigeeRoot(workspace);

        var zip = Path.Combine(Path.GetTempPath(),
            workspace.Name + "_full_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");

        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            // Zipa tudo a partir de workspace.RootPath para preservar src/main/apigee/...
            AddDirectoryToZip(archive, workspace.RootPath, string.Empty);
        }

        return Task.FromResult(zip);
    }

    // ── privados ──────────────────────────────────────────────────────────────

    private static void AddDirectoryToZip(ZipArchive archive, string sourceDir, string zipRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file)
                               .Replace(Path.DirectorySeparatorChar, '/');

            var entryName = string.IsNullOrEmpty(zipRoot)
                ? relative
                : zipRoot + "/" + relative;

            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }
    }

    private static void ScaffoldApiProxy(string apigeeRoot, string proxyName)
    {
        var baseDir = Path.Combine(apigeeRoot, "apiproxies", proxyName);
        foreach (var sub in ProxySubFolders)
            Directory.CreateDirectory(Path.Combine(baseDir, sub));

        File.WriteAllText(
            Path.Combine(baseDir, "apiproxy", "proxies", "default.xml"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<ProxyEndpoint name=\"default\">\n" +
            "    <Description>" + proxyName + " proxy endpoint</Description>\n" +
            "    <HTTPProxyConnection>\n" +
            "        <BasePath>/" + proxyName + "</BasePath>\n" +
            "        <VirtualHost>default</VirtualHost>\n" +
            "    </HTTPProxyConnection>\n" +
            "    <RouteRule name=\"default\">\n" +
            "        <TargetEndpoint>default</TargetEndpoint>\n" +
            "    </RouteRule>\n" +
            "</ProxyEndpoint>\n");

        File.WriteAllText(
            Path.Combine(baseDir, "apiproxy", "targets", "default.xml"),
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<TargetEndpoint name=\"default\">\n" +
            "    <Description>Default target endpoint</Description>\n" +
            "    <HTTPTargetConnection>\n" +
            "        <URL>https://httpbin.org/anything</URL>\n" +
            "    </HTTPTargetConnection>\n" +
            "</TargetEndpoint>\n");
    }

    private static WorkspaceItem BuildItem(string path, string rootPath)
    {
        var rel  = Path.GetRelativePath(rootPath, path);
        var name = Path.GetFileName(path);

        if (File.Exists(path))
            return new WorkspaceItem(name, path, rel, WorkspaceItemType.File, []);

        var itemType = name.ToLowerInvariant() switch
        {
            "apiproxies"   => WorkspaceItemType.ApiProxy,
            "sharedflows"  => WorkspaceItemType.SharedFlow,
            "environments" => WorkspaceItemType.Environment,
            _              => WorkspaceItemType.Directory
        };

        var children = Directory.GetFileSystemEntries(path)
            .OrderBy(p => File.Exists(p) ? 1 : 0).ThenBy(p => p)
            .Select(c => BuildItem(c, rootPath)).ToList();

        return new WorkspaceItem(name, path, rel, itemType, children);
    }
}
