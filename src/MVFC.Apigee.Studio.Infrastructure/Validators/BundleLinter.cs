namespace MVFC.Apigee.Studio.Infrastructure.Validators;

/// <summary>
/// Service responsible for performing structural linting on Apigee bundles.
/// </summary>
public sealed class BundleLinter : IBundleLinter
{
    /// <summary>
    /// Performs structural linting on the specified proxy bundle.
    /// </summary>
    /// <param name="workspaceRoot">The root path of the workspace.</param>
    /// <param name="proxyName">The name of the proxy to lint.</param>
    /// <returns>A result containing any structural issues found.</returns>
    public LintResult Lint(string workspaceRoot, string proxyName)
    {
        var issues = new List<LintIssue>();
        var root = ResolveProxyRoot(workspaceRoot, proxyName, issues);
        if (root == null)
        {
            return new LintResult(issues);
        }

        ValidateProxiesDirectory(root, issues);
        var policyFiles = GetPolicyFiles(root);
        ValidatePolicyReferences(root, policyFiles, issues);
        ValidateXmlWellFormedness(root, issues);

        return new LintResult(issues);
    }

    /// <summary>
    /// Resolves the proxy root directory by checking standard and flat structures.
    /// </summary>
    private static string? ResolveProxyRoot(string workspaceRoot, string proxyName, List<LintIssue> issues)
    {
        var root = Path.Combine(workspaceRoot, "apiproxies", proxyName, "apiproxy");
        if (!Directory.Exists(root))
        {
            root = Path.Combine(workspaceRoot, "apiproxy");
        }

        if (!Directory.Exists(root))
        {
            issues.Add(new LintIssue("warning", "", $"Diretório do proxy '{proxyName}' não encontrado na estrutura esperada. Validação estrutural pulada."));
            return null;
        }
        return root;
    }

    /// <summary>
    /// Validates the existence and content of the 'proxies/' directory.
    /// </summary>
    private static void ValidateProxiesDirectory(string root, List<LintIssue> issues)
    {
        var proxiesDir = Path.Combine(root, "proxies");
        if (!Directory.Exists(proxiesDir))
        {
            issues.Add(new LintIssue("error", "proxies/", "Diretório 'proxies/' não encontrado."));
        }
        else if (Directory.GetFiles(proxiesDir, "*.xml").Length == 0)
        {
            issues.Add(new LintIssue("error", "proxies/", "Nenhum ProxyEndpoint.xml encontrado."));
        }
    }

    /// <summary>
    /// Scans the 'policies/' directory for available policy XML files.
    /// </summary>
    private static HashSet<string> GetPolicyFiles(string root)
    {
        var policiesDir = Path.Combine(root, "policies");
        return Directory.Exists(policiesDir)
            ? Directory.GetFiles(policiesDir, "*.xml")
                       .Select(Path.GetFileNameWithoutExtension)
                       .Where(name => name != null)
                       .ToHashSet(StringComparer.OrdinalIgnoreCase)!
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that all policies referenced in endpoints exist in the 'policies/' directory.
    /// </summary>
    private static void ValidatePolicyReferences(string root, HashSet<string> policyFiles, List<LintIssue> issues)
    {
        var endpointFiles = new List<string>();
        var proxiesDir = Path.Combine(root, "proxies");
        if (Directory.Exists(proxiesDir))
        {
            endpointFiles.AddRange(Directory.GetFiles(proxiesDir, "*.xml"));
        }

        var targetsDir = Path.Combine(root, "targets");
        if (Directory.Exists(targetsDir))
        {
            endpointFiles.AddRange(Directory.GetFiles(targetsDir, "*.xml"));
        }

        foreach (var (relPath, policy) in endpointFiles.SelectMany(e => ExtractStepNames(e).Select(p => (RelPath: Path.GetRelativePath(root, e), Policy: p)))
                                                .Where(x => !policyFiles.Contains(x.Policy)))
        {
            issues.Add(new LintIssue("error", relPath, $"Policy '{policy}' referenciada no fluxo mas o arquivo não existe em policies/."));
        }
    }

    /// <summary>
    /// Validates that all policy XML files are well-formed.
    /// </summary>
    private static void ValidateXmlWellFormedness(string root, List<LintIssue> issues)
    {
        var policiesDir = Path.Combine(root, "policies");
        var policyFiles = Directory.Exists(policiesDir) ? Directory.GetFiles(policiesDir, "*.xml") : [];
        foreach (var policyFile in policyFiles)
        {
            if (!IsWellFormedXml(policyFile, out var xmlError))
            {
                issues.Add(new LintIssue("error", Path.GetRelativePath(root, policyFile), $"XML Malformado: {xmlError}"));
            }
        }
    }

    /// <summary>
    /// Extracts the names of all policies referenced in 'Step' elements.
    /// </summary>
    private static IEnumerable<string> ExtractStepNames(string endpointPath)
    {
        try
        {
            var doc = XDocument.Load(endpointPath);
            return [.. doc.Descendants("Step")
                      .SelectMany(s => s.Elements("Name"))
                      .Select(n => n.Value)
                      .Distinct(StringComparer.OrdinalIgnoreCase),];
        }
        catch
        {

            return [];

        }
    }

    /// <summary>
    /// Checks if an XML file is well-formed.
    /// </summary>
    private static bool IsWellFormedXml(string path, out string error)
    {
        try
        {
            XDocument.Load(path);
            error = string.Empty;
            return true;
        }
        catch (XmlException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
