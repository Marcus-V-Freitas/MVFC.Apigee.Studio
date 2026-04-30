namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// A transaction captured within a trace session.
/// Mapped from DebugSession.Messages[] returned by the emulator.
/// </summary>
public sealed class TraceTransaction
{
    /// <summary>
    /// Unique identifier for the message/transaction.
    /// </summary>
    public string MessageId     { get; init; } = string.Empty;

    /// <summary>
    /// Name of the API proxy associated with this transaction.
    /// </summary>
    public string Application  { get; init; } = string.Empty;

    /// <summary>
    /// HTTP method of the request (e.g., GET, POST).
    /// </summary>
    public string RequestMethod { get; init; } = string.Empty;

    /// <summary>
    /// URI of the request.
    /// </summary>
    public string RequestUri    { get; init; } = string.Empty;

    /// <summary>
    /// HTTP response status code.
    /// </summary>
    public int    ResponseCode  { get; init; }

    /// <summary>
    /// Total time taken for the transaction, in milliseconds.
    /// </summary>
    public long   TotalTimeMs   { get; init; }

    /// <summary>
    /// Ordered list of trace points (policies) executed in this transaction.
    /// </summary>
    public IReadOnlyList<TracePoint> Points { get; init; } = [];
}
