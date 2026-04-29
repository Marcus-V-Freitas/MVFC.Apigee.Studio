namespace MVFC.Apigee.Studio.Infrastructure.Repositories;

/// <summary>
/// Implementation of <see cref="IBundleSnapshotRepository"/> using a local metadata file.
/// </summary>
public sealed class BundleSnapshotRepository : IBundleSnapshotRepository
{
    private sealed record SnapshotMetadata(IReadOnlyDictionary<string, string> FileHashes);

    /// <inheritdoc/>
    public async Task CreateSnapshotAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var hashes = await CalculateHashesAsync(workspace, ct);
        var metadata = new SnapshotMetadata(hashes);

        var snapshotPath = GetSnapshotPath(workspace);
        var dir = Path.GetDirectoryName(snapshotPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(snapshotPath, json, ct);
    }

    /// <inheritdoc/>
    public async Task<BundleDiff> GetDiffAsync(ApigeeWorkspace workspace, CancellationToken ct = default)
    {
        var snapshotPath = GetSnapshotPath(workspace);
        if (!File.Exists(snapshotPath))
        {
            // First deploy: everything is new
            var allFiles = await CalculateHashesAsync(workspace, ct);
            return new BundleDiff([.. allFiles.Keys], [], []);
        }

        var json = await File.ReadAllTextAsync(snapshotPath, ct);
        var snapshot = JsonSerializer.Deserialize<SnapshotMetadata>(json);
        if (snapshot == null)
            return new BundleDiff([], [], []);

        var currentHashes = await CalculateHashesAsync(workspace, ct);

        var added = currentHashes.Keys.Except(snapshot.FileHashes.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var removed = snapshot.FileHashes.Keys.Except(currentHashes.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var modified = currentHashes.Keys
            .Intersect(snapshot.FileHashes.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(file => !string.Equals(currentHashes[file], snapshot.FileHashes[file], StringComparison.Ordinal))
            .ToList();

        return new BundleDiff(added, modified, removed);
    }

    /// <summary>
    /// Gets the path to the snapshot metadata file for a workspace.
    /// </summary>
    private static string GetSnapshotPath(ApigeeWorkspace workspace)
        => Path.Combine(workspace.RootPath, ".studio", "last_deploy_snapshot.json");

    /// <summary>
    /// Calculates SHA256 hashes for all relevant files in the workspace bundle.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> CalculateHashesAsync(ApigeeWorkspace workspace, CancellationToken ct)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const string searchPattern = "*.*";

        // 1. Check for standard structure: apiproxies/, sharedflows/, environments/
        var standardFolders = new[] { "apiproxies", "sharedflows", "environments" };
        foreach (var dirName in standardFolders)
        {
            var dirPath = Path.Combine(workspace.RootPath, dirName);
            if (!Directory.Exists(dirPath))
                continue;

            foreach (var file in Directory.EnumerateFiles(dirPath, searchPattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(workspace.RootPath, file);
                hashes[relative] = await ComputeHashAsync(file, ct);
            }
        }

        // 2. Check for flat structure: apiproxy/ (at root)
        var flatProxyDir = Path.Combine(workspace.RootPath, "apiproxy");
        if (Directory.Exists(flatProxyDir))
        {
            foreach (var file in Directory.EnumerateFiles(flatProxyDir, searchPattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(workspace.RootPath, file);
                if (!hashes.ContainsKey(relative))
                {
                    hashes[relative] = await ComputeHashAsync(file, ct);
                }
            }
        }

        return hashes;
    }

    /// <summary>
    /// Computes the SHA256 hash of a file.
    /// </summary>
    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
