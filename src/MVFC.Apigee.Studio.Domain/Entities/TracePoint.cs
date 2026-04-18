namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// An execution point within a transaction.
/// Mapped from Messages[].point[] in the emulator payload.
///
/// Possible PointType values: StateChange | Execution | Condition
/// Possible Phase values: "request" | "response"  (Execution → enforcement)
///                       value of "To"           (StateChange, e.g., "PROXY_REQ_FLOW")
/// </summary>
public sealed class TracePoint
{
    /// <summary>
    /// StateChange | Execution | Condition
    /// </summary>
    public string PointType     { get; init; } = string.Empty;

    /// <summary>
    /// Execution   → stepDefinition-name  (e.g., "AM-InjetarHeader")
    /// Condition   → Expression           (e.g., "\"default\" equals proxy.name")
    /// StateChange → To                   (e.g., "PROXY_REQ_FLOW")
    /// </summary>
    public string PolicyName    { get; init; } = string.Empty;

    /// <summary>
    /// Execution   → enforcement ("request" | "response")
    /// StateChange → To          (e.g., "TARGET_REQ_FLOW")
    /// </summary>
    public string Phase         { get; init; } = string.Empty;

    /// <summary>
    /// Value of "type" in properties (e.g., "AssignMessageExecution").
    /// </summary>
    public string Description   { get; init; } = string.Empty;

    /// <summary>
    /// Elapsed time in milliseconds for this point.
    /// </summary>
    public long ElapsedTimeMs   { get; init; }

    /// <summary>
    /// Indicates if this point has an error.
    /// </summary>
    public bool HasError        { get; init; }

    /// <summary>
    /// Variables captured at this point.
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
