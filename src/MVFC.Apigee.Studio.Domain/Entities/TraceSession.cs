namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents a trace session started in the emulator.
/// Returned by POST /v1/emulator/trace?proxyName={proxy}
/// </summary>
public sealed class TraceSession
{
    /// <summary>
    /// Unique identifier for the trace session.
    /// </summary>
    public string SessionId    { get; init; } = string.Empty;

    /// <summary>
    /// Name of the API proxy being traced.
    /// </summary>
    public string ApiProxy     { get; init; } = string.Empty;

    /// <summary>
    /// Name of the application associated with the trace session.
    /// </summary>
    public string Application  { get; init; } = string.Empty;

    /// <summary>
    /// Organization name in which the trace session is running.
    /// </summary>
    public string Organization { get; init; } = string.Empty;

    /// <summary>
    /// Environment name in which the trace session is running.
    /// </summary>
    public string Environment  { get; init; } = string.Empty;

    /// <summary>
    /// Revision of the API proxy being traced.
    /// </summary>
    public string Revision     { get; init; } = string.Empty;

    /// <summary>
    /// UTC date and time when the trace session was started.
    /// </summary>
    public DateTime StartedAt  { get; init; } = DateTime.UtcNow;
}
