namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Interface for running apigeelint against a workspace.
/// </summary>
public interface IApigeeLintRunner
{
    /// <summary>
    /// Runs the apigeelint CLI tool against the specified workspace.
    /// </summary>
    /// <param name="workspace">The Apigee workspace to lint.</param>
    /// <param name="filterFilePath">Optional path to filter results for a specific file.</param>
    /// <returns>A list of linting results containing issues found.</returns>
    Task<IList<ApigeeLintResult>> RunLintAsync(ApigeeWorkspace workspace, string? filterFilePath = null);
}
