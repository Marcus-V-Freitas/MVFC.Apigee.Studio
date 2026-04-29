namespace MVFC.Apigee.Studio.Domain.Entities;

public sealed record LintResult(IReadOnlyList<LintIssue> Issues)
{
    public bool IsValid => Issues.All(i => !string.Equals(i.Severity, "error", StringComparison.OrdinalIgnoreCase));
}
