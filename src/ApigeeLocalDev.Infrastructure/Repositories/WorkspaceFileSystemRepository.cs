namespace ApigeeLocalDev.Infrastructure.Repositories;

public sealed class WorkspaceFileSystemRepository(IConfiguration configuration) : IWorkspaceRepository
{
    private static readonly string[] ProxySubFolders =
        ["apiproxy", "apiproxy/policies", "apiproxy/proxies", "apiproxy/targets", "apiproxy/resources"];

    private static readonly XmlWriterSettings XmlSettings = new()
    {
        Indent             = true,
        IndentChars        = "    ",
        Encoding           = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        OmitXmlDeclaration = false,
    };

    private string WorkspacesRoot =>
        configuration["WorkspacesRoot"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "apigee-workspaces");

    // A estrutura correta do Apigee Cloud Code workspace é:
    //   <workspace-root>/
    //   ├── apiproxies/
    //   ├── sharedflows/
    //   └── environments/
    // Sem nenhum src/main/apigee intermediário.
    private static string ApigeeRoot(ApigeeWorkspace workspace) => 
        workspace.RootPath;

    public IReadOnlyList<ApigeeWorkspace> ListAll()
    {
        if (!Directory.Exists(WorkspacesRoot)) 
            return [];
        
        return [.. Directory
            .GetDirectories(WorkspacesRoot)
            .Select(d => new ApigeeWorkspace(Path.GetFileName(d), d))];
    }

    public IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "apiproxies");
        
        if (!Directory.Exists(path)) 
            return [];

        return [.. Directory.GetDirectories(path)
            .Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x)];
    }

    public IReadOnlyList<string> ListSharedFlows(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "sharedflows");

        if (!Directory.Exists(path)) 
            return [];
        
        return [.. Directory.GetDirectories(path)
            .Select(Path.GetFileName).OfType<string>()
            .OrderBy(x => x)];
    }

    public ApigeeWorkspace Create(string name, string? customPath, IReadOnlyList<string>? initialProxies = null)
    {
        var fullPath = !string.IsNullOrWhiteSpace(customPath)
            ? customPath.Trim()
            : Path.Combine(WorkspacesRoot, name);

        // Cria as três pastas raiz do workspace diretamente em fullPath
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


    public async Task EnsureEnvironmentAsync(ApigeeWorkspace workspace, string envName, CancellationToken ct = default)
    {
        var envPath = Path.Combine(ApigeeRoot(workspace), "environments", envName);
        Directory.CreateDirectory(envPath);

        var proxies     = ListApiProxies(workspace).ToList();
        var sharedFlows = ListSharedFlows(workspace).ToList();

        var json = BuildDeploymentsJson(proxies, sharedFlows);
        await File.WriteAllTextAsync(Path.Combine(envPath, "deployments.json"), json, Encoding.UTF8, ct);
    }

    private static string BuildDeploymentsJson(List<string> proxies, List<string> sharedFlows)
    {
        using var ms     = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteStartArray("proxies");
        
        foreach (var p in proxies) 
            writer.WriteStringValue(p);
        
        writer.WriteEndArray();

        // sharedFlows omitido quando vazio — o emulator não aceita array vazio
        if (sharedFlows.Count > 0)
        {
            writer.WriteStartArray("sharedFlows");
            foreach (var sf in sharedFlows) writer.WriteStringValue(sf);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public async Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default) => 
        await Task.FromResult(BuildItem(workspace.RootPath, workspace.RootPath));

    public async Task<string> ReadFileAsync(string absolutePath, CancellationToken ct = default) => 
        await File.ReadAllTextAsync(absolutePath, ct);

    public async Task SaveFileAsync(string absolutePath, string content, CancellationToken ct = default) => 
        await File.WriteAllTextAsync(absolutePath, content, ct);

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

    public Task DeleteDirectoryAsync(string absolutePath, CancellationToken ct = default)
    {
        if (Directory.Exists(absolutePath))
            Directory.Delete(absolutePath, recursive: true);

        return Task.CompletedTask;
    }

    public async Task<string> BuildBundleZipAsync(ApigeeWorkspace workspace, string proxyOrFlowName, CancellationToken ct = default)
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
            throw new DirectoryNotFoundException($"Proxy or shared flow '{proxyOrFlowName}' not found in workspace.");

        var zip = Path.Combine(Path.GetTempPath(),
            proxyOrFlowName + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");

        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            await AddDirectoryToZip(archive, sourceDir, string.Empty);

        return zip;
    }

    public Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var zip = Path.Combine(Path.GetTempPath(),
            workspace.Name + "_full_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".zip");

        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            AddDirectoryToZipIncludingEmpty(archive, workspace.RootPath, string.Empty);

        return Task.FromResult(zip);
    }

    private static async Task AddDirectoryToZip(ZipArchive archive, string sourceDir, string zipRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative  = Path.GetRelativePath(sourceDir, file).Replace(Path.DirectorySeparatorChar, '/');
            var entryName = string.IsNullOrEmpty(zipRoot) ? relative : zipRoot + "/" + relative;
            await archive.CreateEntryFromFileAsync(file, entryName, CompressionLevel.Optimal);
        }
    }

    private static void AddDirectoryToZipIncludingEmpty(ZipArchive archive, string sourceDir, string zipRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative  = Path.GetRelativePath(sourceDir, file).Replace(Path.DirectorySeparatorChar, '/');
            var entryName = string.IsNullOrEmpty(zipRoot) ? relative : zipRoot + "/" + relative;
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                var relative  = Path.GetRelativePath(sourceDir, dir).Replace(Path.DirectorySeparatorChar, '/') + "/";
                var entryName = string.IsNullOrEmpty(zipRoot) ? relative : zipRoot + "/" + relative;
                archive.CreateEntry(entryName);
            }
        }
    }

    private static void ScaffoldApiProxy(string apigeeRoot, string proxyName)
    {
        var baseDir = Path.Combine(apigeeRoot, "apiproxies", proxyName);
        foreach (var sub in ProxySubFolders)
            Directory.CreateDirectory(Path.Combine(baseDir, sub));

        var proxyEndpoint = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("ProxyEndpoint",
                new XAttribute("name", "default"),
                new XElement("Description", $"{proxyName} proxy endpoint"),
                new XElement("HTTPProxyConnection",
                    new XElement("BasePath", $"/{proxyName}"),
                    new XElement("VirtualHost", "default")),
                new XElement("RouteRule",
                    new XAttribute("name", "default"),
                    new XElement("TargetEndpoint", "default"))));

        SaveXml(proxyEndpoint, Path.Combine(baseDir, "apiproxy", "proxies", "default.xml"));

        var targetEndpoint = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("TargetEndpoint",
                new XAttribute("name", "default"),
                new XElement("Description", "Default target endpoint"),
                new XElement("HTTPTargetConnection",
                    new XElement("URL", "https://httpbin.org/anything"))));

        SaveXml(targetEndpoint, Path.Combine(baseDir, "apiproxy", "targets", "default.xml"));
    }

    private static void SaveXml(XDocument doc, string path)
    {
        using var writer = XmlWriter.Create(path, XmlSettings);
        doc.Save(writer);
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
