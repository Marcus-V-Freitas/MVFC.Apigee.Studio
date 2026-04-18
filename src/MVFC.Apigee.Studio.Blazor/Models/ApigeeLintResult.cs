namespace MVFC.Apigee.Studio.Blazor.Models;

/// <summary>
/// Represents the result of running apigeelint on a file, including the file path and lint messages.
/// </summary>
public sealed class ApigeeLintResult
{
    /// <summary>
    /// The path of the file that was linted.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// The list of lint messages found in the file.
    /// </summary>
    public IList<ApigeeLintMessage> Messages { get; set; } = [];
}