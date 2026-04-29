namespace MVFC.Apigee.Studio.Domain.Entities;

public sealed record LintIssue(
    string Severity,   // "error" | "warning"
    string File,       // relative path to bundle root
    string Message
);
