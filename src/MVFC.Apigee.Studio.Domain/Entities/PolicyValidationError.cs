namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents a validation error in a policy file.
/// </summary>
/// <param name="Line">The line number (1-indexed).</param>
/// <param name="Column">The column number (1-indexed).</param>
/// <param name="Message">The error message.</param>
/// <param name="Severity">The severity level ("error" or "warning").</param>
public sealed record PolicyValidationError(
    int Line,
    int Column,
    string Message,
    string Severity
);
