namespace MVFC.Apigee.Studio.Infrastructure.Repositories;

/// <summary>
/// Provides file system-based operations for managing Apigee workspaces, proxies, shared flows, and related files.
/// </summary>
public sealed class WorkspaceFileSystemRepository(IConfiguration config) : IWorkspaceRepository
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

    private readonly IConfiguration _config = config;

    /// <summary>
    /// Gets the root directory for all workspaces.
    /// </summary>
    private string WorkspacesRoot =>
        _config["WorkspacesRoot"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "apigee-workspaces");

    /// <summary>
    /// Gets the root path of the specified Apigee workspace.
    /// </summary>
    /// <param name="workspace">The workspace instance.</param>
    /// <returns>The root path as a string.</returns>
    private static string ApigeeRoot(ApigeeWorkspace workspace) =>
        workspace.RootPath;

    /// <inheritdoc/>
    public IReadOnlyList<ApigeeWorkspace> ListAll()
    {
        if (!Directory.Exists(WorkspacesRoot))
            return [];

        return [.. Directory
            .GetDirectories(WorkspacesRoot)
            .Select(d => new ApigeeWorkspace(Path.GetFileName(d), d)),];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "apiproxies");

        if (!Directory.Exists(path))
            return [];

        return [.. Directory.GetDirectories(path)
            .Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase),];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListSharedFlows(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "sharedflows");

        if (!Directory.Exists(path))
            return [];

        return [.. Directory.GetDirectories(path)
            .Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase),];
    }

    /// <inheritdoc/>
    public ApigeeWorkspace Create(string name, string? customPath, IReadOnlyList<string>? initialProxies = null)
    {
        var fullPath = !string.IsNullOrWhiteSpace(customPath)
            ? customPath.Trim()
            : Path.Combine(WorkspacesRoot, name);

        // Create the three root folders of the workspace directly in fullPath
        Directory.CreateDirectory(Path.Combine(fullPath, "apiproxies"));
        Directory.CreateDirectory(Path.Combine(fullPath, "sharedflows"));
        Directory.CreateDirectory(Path.Combine(fullPath, "environments"));

        if (initialProxies is { Count: > 0 })
        {
            foreach (var p in initialProxies.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                ScaffoldApiProxy(fullPath, p.Trim());
            }
        }

        return new ApigeeWorkspace(name, fullPath);
    }

    /// <inheritdoc/>
    public void Delete(ApigeeWorkspace workspace)
    {
        if (Directory.Exists(workspace.RootPath))
            Directory.Delete(workspace.RootPath, recursive: true);
    }

    /// <summary>
    /// Ensures that the specified environment exists in the workspace and writes a deployments.json file.
    /// </summary>
    /// <param name="workspace">The workspace to update.</param>
    /// <param name="envName">The environment name.</param>
    /// <param name="ct">A cancellation token.</param>
    public async Task EnsureEnvironmentAsync(ApigeeWorkspace workspace, string envName, CancellationToken ct = default)
    {
        var envPath = Path.Combine(ApigeeRoot(workspace), "environments", envName);
        Directory.CreateDirectory(envPath);

        var proxies     = ListApiProxies(workspace).ToList();
        var sharedFlows = ListSharedFlows(workspace).ToList();

        var json = BuildDeploymentsJson(proxies, sharedFlows);
        await File.WriteAllTextAsync(Path.Combine(envPath, "deployments.json"), json, Encoding.UTF8, ct);
    }

    /// <summary>
    /// Builds a JSON string representing the deployments for proxies and shared flows.
    /// </summary>
    /// <param name="proxies">List of proxy names.</param>
    /// <param name="sharedFlows">List of shared flow names.</param>
    /// <returns>A JSON string with deployment information.</returns>
    private static string BuildDeploymentsJson(List<string> proxies, List<string> sharedFlows)
    {
        using var ms     = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteStartArray("proxies");

        foreach (var p in proxies)
            writer.WriteStringValue(p);

        writer.WriteEndArray();

        // sharedFlows omitted when empty — the emulator does not accept an empty array
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

    /// <inheritdoc/>
    public async Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default) =>
        await Task.FromResult(BuildItem(workspace.RootPath, workspace.RootPath));

    /// <inheritdoc/>
    public async Task<string> ReadFileAsync(string absolutePath, CancellationToken ct = default) =>
        await File.ReadAllTextAsync(absolutePath, ct);

    /// <inheritdoc/>
    public async Task SaveFileAsync(string absolutePath, string content, CancellationToken ct = default) =>
        await File.WriteAllTextAsync(absolutePath, content, ct);

    /// <inheritdoc/>
    public async Task CreateFileAsync(string absolutePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        if (!File.Exists(absolutePath))
            await File.WriteAllTextAsync(absolutePath, string.Empty, ct);
    }

    /// <inheritdoc/>
    public Task CreateDirectoryAsync(string absolutePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(absolutePath);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteFileAsync(string absolutePath, CancellationToken ct = default)
    {
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteDirectoryAsync(string absolutePath, CancellationToken ct = default)
    {
        if (Directory.Exists(absolutePath))
            Directory.Delete(absolutePath, recursive: true);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
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
            proxyOrFlowName + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".zip");

        await using (var archive = await ZipFile.OpenAsync(zip, ZipArchiveMode.Create, ct))
            await AddDirectoryToZip(archive, sourceDir, string.Empty);

        return zip;
    }

    /// <inheritdoc/>
    public async Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var zip = Path.Combine(Path.GetTempPath(),
            workspace.Name + "_full_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".zip");

        var archive = await ZipFile.OpenAsync(zip, ZipArchiveMode.Create, ct);

        await using (archive.ConfigureAwait(false))
        {
            await AddDirectoryToZipIncludingEmpty(archive, workspace.RootPath, string.Empty);
        }

        return zip;
    }

    /// <summary>
    /// Adds all files from a directory to a zip archive asynchronously.
    /// </summary>
    /// <param name="archive">The zip archive.</param>
    /// <param name="sourceDir">The source directory.</param>
    /// <param name="zipRoot">The root path inside the zip archive.</param>
    private static async Task AddDirectoryToZip(ZipArchive archive, string sourceDir, string zipRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative  = Path.GetRelativePath(sourceDir, file).Replace(Path.DirectorySeparatorChar, '/');
            var entryName = string.IsNullOrEmpty(zipRoot) ? relative : zipRoot + "/" + relative;
            await archive.CreateEntryFromFileAsync(file, entryName, CompressionLevel.Optimal);
        }
    }

    /// <summary>
    /// Adds all files and empty directories from a directory to a zip archive.
    /// </summary>
    /// <param name="archive">The zip archive.</param>
    /// <param name="sourceDir">The source directory.</param>
    /// <param name="zipRoot">The root path inside the zip archive.</param>
    private static async Task AddDirectoryToZipIncludingEmpty(ZipArchive archive, string sourceDir, string zipRoot)
    {
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative  = Path.GetRelativePath(sourceDir, file).Replace(Path.DirectorySeparatorChar, '/');
            var entryName = string.IsNullOrEmpty(zipRoot) ? relative : zipRoot + "/" + relative;
            await archive.CreateEntryFromFileAsync(file, entryName, CompressionLevel.Optimal);
        }

        foreach (var entryName in from dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories)
                                  where !Directory.EnumerateFileSystemEntries(dir).Any()
                                  let relative = Path.GetRelativePath(sourceDir, dir).Replace(Path.DirectorySeparatorChar, '/') + "/"
                                  let entryName = string.IsNullOrEmpty(zipRoot) ? relative : zipRoot + "/" + relative
                                  select entryName)
        {
            archive.CreateEntry(entryName);
        }
    }

    /// <summary>
    /// Scaffolds the folder structure and default files for a new API proxy.
    /// </summary>
    /// <param name="apigeeRoot">The root directory of the workspace.</param>
    /// <param name="proxyName">The name of the proxy.</param>
    private void ScaffoldApiProxy(string apigeeRoot, string proxyName)
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
                    new XElement("URL", _config["ApigeeTemplates:DefaultTargetUrl"] ?? new UriBuilder(Uri.UriSchemeHttps, "httpbin.org", 443, "anything").ToString()))));

        SaveXml(targetEndpoint, Path.Combine(baseDir, "apiproxy", "targets", "default.xml"));
    }

    /// <summary>
    /// Saves an XML document to the specified path using the configured XML writer settings.
    /// </summary>
    /// <param name="doc">The XML document.</param>
    /// <param name="path">The file path to save to.</param>
    private static void SaveXml(XDocument doc, string path)
    {
        using var writer = XmlWriter.Create(path, XmlSettings);
        doc.Save(writer);
    }

    /// <summary>
    /// Recursively builds a <see cref="WorkspaceItem"/> tree from the file system.
    /// </summary>
    /// <param name="path">The current path.</param>
    /// <param name="rootPath">The root path of the workspace.</param>
    /// <returns>A <see cref="WorkspaceItem"/> representing the directory or file.</returns>
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
            _ => WorkspaceItemType.Directory,
        };

        var children = Directory.GetFileSystemEntries(path)
            .OrderBy(p => File.Exists(p) ? 1 : 0).ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(c => BuildItem(c, rootPath)).ToList();

        return new WorkspaceItem(name, path, rel, itemType, children);
    }
}
