namespace MVFC.Apigee.Studio.Blazor.Models;

/// <summary>
/// Represents an HTTP header entry with a key and value.
/// </summary>
public sealed class HeaderEntry
{
    /// <summary>
    /// The header name (key).
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// The header value.
    /// </summary>
    public string Value { get; set; } = "";
}