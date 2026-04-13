using System.IO.Compression;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ApigeeLocalDev.Infrastructure.Repositories;

public sealed class WorkspaceFileSystemRepository(IConfiguration configuration) : IWorkspaceRepository
{
    private static readonly string[] ProxySubFolders =
        ["apiproxy", "apiproxy/policies", "apiproxy/proxies", "apiproxy/targets", "apiproxy/resources"];

    // Prefixo exigido pelo Apigee Emulator dentro do ZIP
    private const string EmulatorZipRoot = "src/main/apigee";

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
            foreach (var p in initialProxies.Where(p => !string.IsNullOrWhiteSpace(p)))
                ScaffoldApiProxy(fullPath, p.Trim());

        return new ApigeeWorkspace(name, fullPath);
    }

    public void Delete(ApigeeWorkspace workspace)
    {
        if (Directory.Exists(workspace.RootPath))
            Directory.Delete(workspace.RootPath, recursive: true);
    }

    public IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(workspace.RootPath, "apiproxies");
        if (!Directory.Exists(path)) return [];
        return Directory.GetDirectories(path)
            .Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x).ToList();
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

    public Task DeleteFileAsync(string absolutePath, CancellationToken ct = default)
    {
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Empacota um proxy ou shared flow individual no formato exigido pelo emulator.
    ///
    /// O Apigee Emulator espera que o bundle de um proxy seja um ZIP cujo conteúdo
    /// comece diretamente na pasta "apiproxy/" (sem nenhum prefixo pai).
    ///
    /// Estrutura esperada dentro do ZIP:
    ///   apiproxy/proxy.xml
    ///   apiproxy/proxies/default.xml
    ///   apiproxy/targets/default.xml
    ///   apiproxy/policies/*.xml
    ///
    /// Para shared flows o conteúdo também começa direto na pasta "sharedflowbundle/".
    /// </summary>
    public Task<string> BuildBundleZipAsync(
        ApigeeWorkspace workspace, string proxyOrFlowName, CancellationToken ct = default)
    {
        // Tenta localizar em apiproxies primeiro, depois sharedflows
        var proxySrc = Path.Combine(workspace.RootPath, "apiproxies", proxyOrFlowName);
        var sfSrc    = Path.Combine(workspace.RootPath, "sharedflows",  proxyOrFlowName);

        string sourceDir;
        string bundleRoot; // pasta raiz dentro do ZIP

        if (Directory.Exists(proxySrc))
        {
            // O diretório do proxy contém a subpasta "apiproxy/"
            // Queremos zipar a partir da pasta do proxy para que o ZIP contenha "apiproxy/..."
            sourceDir  = proxySrc;
            bundleRoot = string.Empty; // sem prefixo: a pasta "apiproxy/" fica na raiz do ZIP
        }
        else if (Directory.Exists(sfSrc))
        {
            // O diretório do shared flow contém "sharedflowbundle/"
            sourceDir  = sfSrc;
            bundleRoot = string.Empty;
        }
        else
        {
            throw new DirectoryNotFoundException(
                "Proxy or shared flow '" + proxyOrFlowName + "' not found.");
        }

        var zip = Path.Combine(Path.GetTempPath(),
            proxyOrFlowName + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");

        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            AddDirectoryToZip(archive, sourceDir, bundleRoot);

        return Task.FromResult(zip);
    }

    /// <summary>
    /// Deploy do workspace completo:
    /// como o emulator não possui endpoint para workspace archive,
    /// geramos um ZIP no formato archive (src/main/apigee/...) com todos os
    /// proxies, shared flows e environments.
    ///
    /// Estrutura esperada pelo endpoint de archive deploy:
    ///   src/main/apigee/apiproxies/{name}/apiproxy/...
    ///   src/main/apigee/sharedflows/{name}/sharedflowbundle/...
    ///   src/main/apigee/environments/{env}/deployments.json
    ///   src/main/apigee/environments/{env}/flowhooks.json
    ///   src/main/apigee/environments/{env}/targetservers.json
    /// </summary>
    public Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var zip = Path.Combine(Path.GetTempPath(),
            workspace.Name + "_full_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");

        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            // apiproxies: src/main/apigee/apiproxies/{name}/apiproxy/...
            var proxiesRoot = Path.Combine(workspace.RootPath, "apiproxies");
            if (Directory.Exists(proxiesRoot))
                foreach (var proxyDir in Directory.GetDirectories(proxiesRoot))
                {
                    var name = Path.GetFileName(proxyDir);
                    AddDirectoryToZip(archive, proxyDir,
                        EmulatorZipRoot + "/apiproxies/" + name);
                }

            // sharedflows: src/main/apigee/sharedflows/{name}/sharedflowbundle/...
            var sfRoot = Path.Combine(workspace.RootPath, "sharedflows");
            if (Directory.Exists(sfRoot))
                foreach (var sfDir in Directory.GetDirectories(sfRoot))
                {
                    var name = Path.GetFileName(sfDir);
                    AddDirectoryToZip(archive, sfDir,
                        EmulatorZipRoot + "/sharedflows/" + name);
                }

            // environments: src/main/apigee/environments/{env}/...
            var envRoot = Path.Combine(workspace.RootPath, "environments");
            if (Directory.Exists(envRoot))
                foreach (var envDir in Directory.GetDirectories(envRoot))
                {
                    var name = Path.GetFileName(envDir);
                    AddDirectoryToZip(archive, envDir,
                        EmulatorZipRoot + "/environments/" + name);
                }
        }

        return Task.FromResult(zip);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Adiciona recursivamente todos os arquivos de <paramref name="sourceDir"/> no archive.
    /// Se <paramref name="zipRoot"/> for vazio, os caminhos relativos ficam direto na raiz do ZIP.
    /// </summary>
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

    private static void ScaffoldApiProxy(string workspaceRoot, string proxyName)
    {
        var baseDir = Path.Combine(workspaceRoot, "apiproxies", proxyName);
        foreach (var sub in ProxySubFolders)
            Directory.CreateDirectory(Path.Combine(baseDir, sub));

        var proxyXml =
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
            "</ProxyEndpoint>\n";

        var targetXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<TargetEndpoint name=\"default\">\n" +
            "    <Description>Default target endpoint</Description>\n" +
            "    <HTTPTargetConnection>\n" +
            "        <URL>https://httpbin.org/anything</URL>\n" +
            "    </HTTPTargetConnection>\n" +
            "</TargetEndpoint>\n";

        File.WriteAllText(Path.Combine(baseDir, "apiproxy", "proxies", "default.xml"), proxyXml);
        File.WriteAllText(Path.Combine(baseDir, "apiproxy", "targets", "default.xml"), targetXml);
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
