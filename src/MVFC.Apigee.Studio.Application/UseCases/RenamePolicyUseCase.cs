using MVFC.Apigee.Studio.Domain.Interfaces;

namespace MVFC.Apigee.Studio.Application.UseCases;

/// <summary>
/// Orchestrates the renaming of a policy and its references.
/// </summary>
public sealed class RenamePolicyUseCase(IRenameRefactoringService refactoringService)
{
    private readonly IRenameRefactoringService _refactoringService = refactoringService;

    public async Task<IReadOnlyList<string>> ExecuteAsync(
        string workspaceRoot,
        string proxyName,
        string oldPolicyName,
        string newPolicyName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(oldPolicyName);
        ArgumentException.ThrowIfNullOrEmpty(newPolicyName);

        if (oldPolicyName.Equals(newPolicyName, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        return await _refactoringService.RenamePolicyAsync(
            workspaceRoot,
            proxyName,
            oldPolicyName,
            newPolicyName,
            ct);
    }
}
