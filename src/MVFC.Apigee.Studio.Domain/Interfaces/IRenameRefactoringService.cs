namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Service for renaming policies and updating all references within the proxy bundle.
/// </summary>
public interface IRenameRefactoringService
{
    /// <summary>
    /// Renames a policy file and updates all references to it within the proxy bundle.
    /// Returns the list of relative file paths that were modified.
    /// </summary>
    Task<IReadOnlyList<string>> RenamePolicyAsync(
        string workspaceRoot,
        string proxyName,
        string oldPolicyName,
        string newPolicyName,
        CancellationToken ct = default);
}
