namespace MVFC.Apigee.Studio.Infrastructure.Repositories;

/// <summary>
/// Provides file system-based operations for managing Apigee workspaces, proxies, shared flows, and related files.
/// </summary>
public sealed class WorkspaceFileSystemRepository(IConfiguration config, ILogger<WorkspaceFileSystemRepository> logger) : IWorkspaceRepository
{
    private readonly ILogger<WorkspaceFileSystemRepository> _logger = logger;
    private static readonly string[] ProxySubFolders =
        ["apiproxy", "apiproxy/policies", "apiproxy/proxies", "apiproxy/targets", "apiproxy/resources"];

    private static readonly string[] ExcludedFolders = ["bin", "obj", ".git", ".vscode", ".idea", "node_modules", ".antigravity"];

    private static readonly XmlWriterSettings XmlSettings = new()
    {
        Indent = true,
        IndentChars = "    ",
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
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
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name) &&
                           !name.StartsWith('.') &&
                           !name.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                           !name.Equals("obj", StringComparison.OrdinalIgnoreCase))
            .Select(name => new ApigeeWorkspace(name!, Path.Combine(WorkspacesRoot, name!)))
            .OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase),];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListApiProxies(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "apiproxies");
        var proxies = new List<string>();

        if (Directory.Exists(path))
        {
            proxies.AddRange(Directory.GetDirectories(path).Select(Path.GetFileName).OfType<string>());
        }

        // Check for flat structure ROOT/apiproxy
        if (Directory.Exists(Path.Combine(ApigeeRoot(workspace), "apiproxy")) && 
            !proxies.Contains(workspace.Name))
        {
            proxies.Add(workspace.Name);
        }

        return [.. proxies.Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ListSharedFlows(ApigeeWorkspace workspace)
    {
        var path = Path.Combine(ApigeeRoot(workspace), "sharedflows");
        var flows = new List<string>();

        if (Directory.Exists(path))
        {
            flows.AddRange(Directory.GetDirectories(path).Select(Path.GetFileName).OfType<string>());
        }

        // Check for flat structure ROOT/sharedflowbundle
        if (Directory.Exists(Path.Combine(ApigeeRoot(workspace), "sharedflowbundle")) && 
            !flows.Contains(workspace.Name))
        {
            flows.Add(workspace.Name);
        }

        return [.. flows.Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc/>
    public ApigeeWorkspace Create(string name, string? customPath, IReadOnlyList<string>? initialProxies = null)
    {
        var fullPath = !string.IsNullOrWhiteSpace(customPath)
            ? customPath.Trim()
            : Path.Combine(WorkspacesRoot, name);

        // Create the root folders
        Directory.CreateDirectory(Path.Combine(fullPath, "apiproxies"));
        Directory.CreateDirectory(Path.Combine(fullPath, "sharedflows"));
        Directory.CreateDirectory(Path.Combine(fullPath, "environments"));
        Directory.CreateDirectory(Path.Combine(fullPath, "test"));
        
        // Ensure 'local' environment exists on creation
        EnsureLocalEnvironmentSync(fullPath);

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

    /// <inheritdoc/>
    public ApigeeWorkspace RegisterExisting(string existingPath)
    {
        var fullPath = existingPath.Trim();

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Diretório não encontrado: {fullPath}");

        var name = Path.GetFileName(fullPath);
        var linkPath = Path.Combine(WorkspacesRoot, name);

        // If already inside WorkspacesRoot, just return it
        if (string.Equals(Path.GetDirectoryName(fullPath), WorkspacesRoot, StringComparison.OrdinalIgnoreCase))
            return new ApigeeWorkspace(name, fullPath);

        // If a junction/dir with the same name already exists, use a unique name
        if (Directory.Exists(linkPath))
        {
            var counter = 2;
            while (Directory.Exists(linkPath + "-" + counter))
                counter++;
            name += string.Create(CultureInfo.InvariantCulture, $"-{counter}");
            linkPath = Path.Combine(WorkspacesRoot, name);
        }

        Directory.CreateDirectory(WorkspacesRoot);
        Directory.CreateSymbolicLink(linkPath, fullPath);

        return new ApigeeWorkspace(name, linkPath);
    }

    /// <inheritdoc/>
    public async Task EnsureEnvironmentAsync(ApigeeWorkspace workspace, string envName, CancellationToken ct = default)
    {
        var envsRoot = Path.Combine(ApigeeRoot(workspace), "environments");
        var envPath = Path.Combine(envsRoot, envName);
        Directory.CreateDirectory(envPath);

        if (!string.Equals(envName, "local", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = Path.Combine(envsRoot, "local");
            if (!Directory.Exists(localPath))
            {
                await EnsureEnvironmentAsync(workspace, "local", ct);
            }
        }

        var proxies = ListApiProxies(workspace).ToList();
        var sharedFlows = ListSharedFlows(workspace).ToList();

        var json = BuildDeploymentsJson(proxies, sharedFlows);
        await File.WriteAllTextAsync(Path.Combine(envPath, "deployments.json"), json, Encoding.UTF8, ct);

        var mapsPath = Path.Combine(envPath, "maps.json");
        if (!File.Exists(mapsPath))
            await File.WriteAllTextAsync(mapsPath, SkeletonTemplateService.GetKvmJson(), Encoding.UTF8, ct);

        var cachesPath = Path.Combine(envPath, "caches.json");
        if (!File.Exists(cachesPath))
            await File.WriteAllTextAsync(cachesPath, SkeletonTemplateService.GetCachesJson(), Encoding.UTF8, ct);

        var targetsPath = Path.Combine(envPath, "targetservers.json");
        if (!File.Exists(targetsPath))
            await File.WriteAllTextAsync(targetsPath, SkeletonTemplateService.GetTargetServersJson(), Encoding.UTF8, ct);

        var flowhooksPath = Path.Combine(envPath, "flowhooks.json");
        if (!File.Exists(flowhooksPath))
            await File.WriteAllTextAsync(flowhooksPath, SkeletonTemplateService.GetFlowhooksJson(), Encoding.UTF8, ct);
    }

    /// <summary>
    /// Synchronously ensures that the 'local' environment exists and is populated with default configuration files.
    /// </summary>
    /// <param name="workspaceRoot">The physical path to the workspace root.</param>
    private static void EnsureLocalEnvironmentSync(string workspaceRoot)
    {
        var envsRoot = Path.Combine(workspaceRoot, "environments");
        var envPath = Path.Combine(envsRoot, "local");
        Directory.CreateDirectory(envPath);

        var deployPath = Path.Combine(envPath, "deployments.json");
        if (!File.Exists(deployPath))
            File.WriteAllText(deployPath, BuildDeploymentsJson([], []), Encoding.UTF8);

        var mapsPath = Path.Combine(envPath, "maps.json");
        if (!File.Exists(mapsPath))
            File.WriteAllText(mapsPath, SkeletonTemplateService.GetKvmJson(), Encoding.UTF8);

        File.WriteAllText(Path.Combine(envPath, "caches.json"), SkeletonTemplateService.GetCachesJson(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(envPath, "targetservers.json"), SkeletonTemplateService.GetTargetServersJson(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(envPath, "flowhooks.json"), SkeletonTemplateService.GetFlowhooksJson(), Encoding.UTF8);
    }

    /// <summary>
    /// Builds a deployments.json string from lists of proxies and shared flows.
    /// </summary>
    /// <param name="proxies">List of proxy names.</param>
    /// <param name="sharedFlows">List of shared flow names.</param>
    /// <returns>A formatted JSON string.</returns>
    private static string BuildDeploymentsJson(List<string> proxies, List<string> sharedFlows)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteStartArray("proxies");
        foreach (var p in proxies)
        {
            writer.WriteStartObject();
            writer.WriteString("name", p);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        if (sharedFlows.Count > 0)
        {
            writer.WriteStartArray("sharedflows");
            foreach (var sf in sharedFlows)
            {
                writer.WriteStartObject();
                writer.WriteString("name", sf);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <inheritdoc/>
    public async Task<WorkspaceItem> LoadTreeAsync(ApigeeWorkspace workspace, CancellationToken ct = default) =>
        await Task.Run(() => BuildItem(workspace.RootPath, workspace.RootPath), ct);

    /// <inheritdoc/>
    public async Task<string> ReadFileAsync(string absolutePath, CancellationToken ct = default) =>
        await File.ReadAllTextAsync(absolutePath, ct);

    /// <inheritdoc/>
    public async Task SaveFileAsync(string absolutePath, string content, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(absolutePath, content, ct);
    }

    /// <inheritdoc/>
    public async Task CreateFileAsync(string absolutePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        if (!File.Exists(absolutePath))
            await File.WriteAllTextAsync(absolutePath, string.Empty, ct);
    }

    /// <inheritdoc/>
    public Task CreateDirectoryAsync(string absolutePath, CancellationToken ct = default) =>
        Task.Run(() => Directory.CreateDirectory(absolutePath), ct);

    /// <inheritdoc/>
    public Task DeleteFileAsync(string absolutePath, CancellationToken ct = default) =>
        Task.Run(() => { if (File.Exists(absolutePath)) File.Delete(absolutePath); }, ct);

    /// <inheritdoc/>
    public Task DeleteDirectoryAsync(string absolutePath, CancellationToken ct = default) =>
        Task.Run(() => { if (Directory.Exists(absolutePath)) Directory.Delete(absolutePath, recursive: true); }, ct);

    /// <inheritdoc/>
    public async Task<string> BuildWorkspaceZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var tempDir = Path.Combine(Path.GetTempPath(), $"apigee_build_{workspace.Name}_{timestamp}");
        var zipPath = Path.Combine(Path.GetTempPath(), $"{workspace.Name}_full_{timestamp}.zip");

        try
        {
            var targetRoot = Path.Combine(tempDir, "src", "main", "apigee");
            Directory.CreateDirectory(targetRoot);

            // Copy essential folders
            foreach (var dir in new[] { "apiproxies", "sharedflows", "environments", "test", "apiproxy" })
            {
                var source = Path.Combine(workspace.RootPath, dir);
                if (Directory.Exists(source))
                {
                    if (string.Equals(dir, "apiproxy", StringComparison.OrdinalIgnoreCase))
                    {
                        // Map flat structure ROOT/apiproxy to src/main/apigee/apiproxies/NAME/apiproxy
                        CopyDirectory(source, Path.Combine(targetRoot, "apiproxies", workspace.Name, "apiproxy"));
                    }
                    else
                    {
                        CopyDirectory(source, Path.Combine(targetRoot, dir));
                    }
                }
            }

            // Create ZIP from the temp directory
            if (File.Exists(zipPath)) File.Delete(zipPath);
            await Task.Run(() => ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false), ct);

            return zipPath;
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// Recursively copies a directory from source to destination, excluding specified folders.
    /// </summary>
    /// <param name="sourceDir">The source directory path.</param>
    /// <param name="destinationDir">The destination directory path.</param>
    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            if (IsExcluded(file)) continue;
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            if (IsExcluded(dir)) continue;

            CopyDirectory(dir, Path.Combine(destinationDir, name));
        }
    }

    /// <summary>
    /// Checks if a path should be excluded based on the predefined excluded folders list.
    /// </summary>
    /// <param name="path">The file or directory path.</param>
    /// <returns>True if it should be excluded; otherwise, false.</returns>
    private static bool IsExcluded(string path)
    {
        var parts = path.ToLowerInvariant().Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(p => ExcludedFolders.Contains(p));
    }

    /// <inheritdoc/>
    public async Task<TestResources> GetTestResourcesAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var testDir = Path.Combine(workspace.RootPath, "test");
        if (!Directory.Exists(testDir))
            return new TestResources([], [], []);

        var productsPath = Path.Combine(testDir, "products.json");
        var developersPath = Path.Combine(testDir, "developers.json");
        var appsPath = Path.Combine(testDir, "developerapps.json");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var products = await SafeLoadAsync<List<ApiProduct>>(productsPath, options, ct);
        var developers = await SafeLoadAsync<List<Developer>>(developersPath, options, ct);
        var apps = await SafeLoadAsync<List<DeveloperApp>>(appsPath, options, ct);

        return new TestResources(products, developers, apps);
    }

    /// <summary>
    /// Safely loads and deserializes a JSON file into a specified type.
    /// Returns a new instance if the file is missing or invalid.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="path">The file path.</param>
    /// <param name="options">Serializer options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized object or a new instance.</returns>
    private async Task<T> SafeLoadAsync<T>(string path, JsonSerializerOptions options, CancellationToken ct) where T : new()
    {
        if (!File.Exists(path)) return new T();

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            if (string.IsNullOrWhiteSpace(content)) return new T();
            return JsonSerializer.Deserialize<T>(content, options) ?? new T();
        }
        catch (Exception ex)
        {
            _logger.LogLoadTestFileError(ex, path);
            return new T();
        }
    }

    /// <inheritdoc/>
    public async Task SaveTestResourcesAsync(ApigeeWorkspace workspace, TestResources resources, CancellationToken ct = default)
    {
        var testDir = Path.Combine(workspace.RootPath, "test");
        Directory.CreateDirectory(testDir);

        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        await File.WriteAllTextAsync(Path.Combine(testDir, "products.json"), JsonSerializer.Serialize(resources.Products, options), ct);
        await File.WriteAllTextAsync(Path.Combine(testDir, "developers.json"), JsonSerializer.Serialize(resources.Developers, options), ct);
        await File.WriteAllTextAsync(Path.Combine(testDir, "developerapps.json"), JsonSerializer.Serialize(resources.Apps, options), ct);
    }

    /// <inheritdoc/>
    public async Task<string> BuildTestBundleZipAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var testDir = Path.Combine(workspace.RootPath, "test");
        EnsureTestDirectoryExists(testDir);

        var resources = await GetTestResourcesAsync(workspace, ct);
        var zipPath = GetTempZipPath(workspace.Name);

        await Task.Run(() =>
        {
            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    var options = CreateJsonOptions();

                    AddJsonEntry(archive, "products.json", resources.Products, options);
                    AddJsonEntry(archive, "developers.json", resources.Developers, options);

                    var cleanedApps = resources.Apps.Select(a => new
                    {
                        a.Name, a.DisplayName, a.DeveloperEmail, a.DeveloperId,
                        a.ApiProducts, a.CallbackUrl, a.ExpiryType, a.Status
                    }).ToList();
                    AddJsonEntry(archive, "developerapps.json", cleanedApps, options);
                }
            }
        }, ct);

        return zipPath;
    }

    private static void EnsureTestDirectoryExists(string testDir)
    {
        if (Directory.Exists(testDir)) return;
        Directory.CreateDirectory(testDir);
        File.WriteAllText(Path.Combine(testDir, "products.json"), "[]");
        File.WriteAllText(Path.Combine(testDir, "developers.json"), "[]");
        File.WriteAllText(Path.Combine(testDir, "developerapps.json"), "[]");
    }

    private static string GetTempZipPath(string workspaceName)
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"{workspaceName}_testdata_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
        if (File.Exists(path)) File.Delete(path);
        return path;
    }

    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static void AddJsonEntry<T>(ZipArchive archive, string entryName, T data, JsonSerializerOptions options)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, data, options);
        stream.Flush();
    }

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
                    new XElement("URL", _config["ApigeeTemplates:DefaultTargetUrl"] ?? "https://httpbin.org/anything"))));

        SaveXml(targetEndpoint, Path.Combine(baseDir, "apiproxy", "targets", "default.xml"));
    }

    private static void SaveXml(XDocument doc, string path)
    {
        using var writer = XmlWriter.Create(path, XmlSettings);
        doc.Save(writer);
    }

    private static WorkspaceItem BuildItem(string path, string rootPath)
    {
        var rel = Path.GetRelativePath(rootPath, path);
        var name = Path.GetFileName(path);

        if (File.Exists(path))
            return new WorkspaceItem(name, path, rel, WorkspaceItemType.File, []);

        var itemType = name.ToLowerInvariant() switch
        {
            "apiproxies" => WorkspaceItemType.ApiProxy,
            "sharedflows" => WorkspaceItemType.SharedFlow,
            "environments" => WorkspaceItemType.Environment,
            "test" => WorkspaceItemType.Directory,
            _ => WorkspaceItemType.Directory,
        };

        var children = Directory.GetFileSystemEntries(path)
            .OrderBy(p => File.Exists(p) ? 1 : 0).ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(c => BuildItem(c, rootPath)).ToList();

        return new WorkspaceItem(name, path, rel, itemType, children);
    }
}