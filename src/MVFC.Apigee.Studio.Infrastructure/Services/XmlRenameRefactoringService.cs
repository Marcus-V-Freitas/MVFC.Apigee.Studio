namespace MVFC.Apigee.Studio.Infrastructure.Services;

/// <summary>
/// XML-based implementation of <see cref="IRenameRefactoringService"/>.
/// </summary>
public sealed class XmlRenameRefactoringService : IRenameRefactoringService
{
    private static readonly string[] _endpointPatterns = ["ProxyEndpoint.xml", "TargetEndpoint.xml"];

    /// <summary>
    /// Renames a policy file and updates all references to it within the proxy bundle.
    /// </summary>
    /// <param name="workspaceRoot">The root path of the workspace.</param>
    /// <param name="proxyName">The name of the API proxy.</param>
    /// <param name="oldPolicyName">The current name of the policy.</param>
    /// <param name="newPolicyName">The new name for the policy.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of modified files (relative paths).</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the proxy root directory is not found.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the policy file is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the new policy name already exists.</exception>
    public async Task<IReadOnlyList<string>> RenamePolicyAsync(
        string workspaceRoot,
        string proxyName,
        string oldPolicyName,
        string newPolicyName,
        CancellationToken ct = default)
    {
        var proxyRoot = ResolveProxyRoot(workspaceRoot, proxyName);
        var policiesDir = Path.Combine(proxyRoot, "policies");
        var modified = new List<string>();

        // 1. Rename physical file
        var oldFile = Path.Combine(policiesDir, $"{oldPolicyName}.xml");
        var newFile = Path.Combine(policiesDir, $"{newPolicyName}.xml");

        if (!File.Exists(oldFile))
        {
            throw new FileNotFoundException($"Policy file not found: {oldFile}");
        }

        if (File.Exists(newFile))
        {
            throw new InvalidOperationException($"A policy named '{newPolicyName}' already exists.");
        }

        File.Move(oldFile, newFile);
        modified.Add(Path.GetRelativePath(proxyRoot, newFile));

        // 2. Update 'name' attribute in the policy file itself
        await UpdatePolicyNameAttributeAsync(newFile, oldPolicyName, newPolicyName);

        // 3. Update references in endpoints
        await UpdateEndpointReferencesAsync(proxyRoot, oldPolicyName, newPolicyName, modified);

        return modified;
    }

    private static string ResolveProxyRoot(string workspaceRoot, string proxyName)
    {
        var proxyRoot = Path.Combine(workspaceRoot, "apiproxies", proxyName, "apiproxy");

        if (!Directory.Exists(proxyRoot))
        {
            proxyRoot = Path.Combine(workspaceRoot, "apiproxy");
        }

        if (!Directory.Exists(proxyRoot))
        {
            throw new DirectoryNotFoundException($"Proxy root directory not found. Tried: {Path.Combine(workspaceRoot, "apiproxies", proxyName, "apiproxy")} and {Path.Combine(workspaceRoot, "apiproxy")}");
        }

        return proxyRoot;
    }

    private static async Task UpdateEndpointReferencesAsync(string proxyRoot, string oldName, string newName, List<string> modified)
    {
        var endpointFiles = _endpointPatterns
            .SelectMany(_ => Directory.Exists(Path.Combine(proxyRoot, "proxies"))
                ? Directory.GetFiles(Path.Combine(proxyRoot, "proxies"), "*.xml")
                : [])
            .Concat(_endpointPatterns
                .SelectMany(_ => Directory.Exists(Path.Combine(proxyRoot, "targets"))
                    ? Directory.GetFiles(Path.Combine(proxyRoot, "targets"), "*.xml")
                    : []))
            .Distinct(StringComparer.Ordinal);

        foreach (var endpoint in endpointFiles)
        {
            var changed = await ReplaceStepNameAsync(endpoint, oldName, newName);
            if (changed)
            {
                modified.Add(Path.GetRelativePath(proxyRoot, endpoint));
            }
        }
    }

    /// <summary>
    /// Updates the 'name' attribute of the root element in a policy XML file.
    /// </summary>
    private static async Task UpdatePolicyNameAttributeAsync(
        string filePath, string oldName, string newName)
    {
        var doc = XDocument.Load(filePath);
        var root = doc.Root;
        if (root != null && string.Equals(root.Attribute("name")?.Value, oldName, StringComparison.Ordinal))
        {
            root.SetAttributeValue("name", newName);
            await using var writer = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(doc.ToString());
        }
    }

    /// <summary>
    /// Replaces occurrences of a step name in an endpoint XML file.
    /// </summary>
    private static async Task<bool> ReplaceStepNameAsync(
        string filePath, string oldName, string newName)
    {
        var doc = XDocument.Load(filePath);
        var steps = doc.Descendants("Step")
                      .SelectMany(s => s.Elements("Name"))
                      .Where(n => string.Equals(n.Value, oldName, StringComparison.Ordinal))
                      .ToList();

        if (steps.Count == 0)
        {
            return false;
        }

        foreach (var nameEl in steps)
        {
            nameEl.Value = newName;
        }

        await using var writer = new StreamWriter(filePath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        doc.Save(writer);
        return true;
    }
}
