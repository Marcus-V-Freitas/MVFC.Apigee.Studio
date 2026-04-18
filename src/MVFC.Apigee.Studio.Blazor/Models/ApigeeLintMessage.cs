namespace MVFC.Apigee.Studio.Blazor.Models;

/// <summary>
/// Represents a lint message returned by apigeelint, including its location, message, and severity.
/// </summary>
public sealed class ApigeeLintMessage
{
    /// <summary>
    /// The line number where the lint message was found.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The column number where the lint message was found.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// The lint message text.
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// The severity of the lint message: 1 = warning, 2 = error (apigeelint convention).
    /// </summary>
    public int Severity { get; set; } // 1 = warning, 2 = error (apigeelint convention)
}