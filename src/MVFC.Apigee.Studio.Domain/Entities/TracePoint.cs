namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// An execution point within a transaction.
/// Mapped from Messages[].point[] in the emulator payload.
///
/// Possible PointType values: StateChange | Execution | Condition | FlowInfo
/// </summary>
public sealed class TracePoint
{
    /// <summary>
    /// Friendly type for display (e.g., "Step", "Flow", "State").
    /// </summary>
    public string PointType     { get; init; } = string.Empty;

    /// <summary>
    /// The raw point ID from the emulator (e.g., "StateChange", "FlowInfo", "Execution").
    /// </summary>
    public string RawPointId    { get; init; } = string.Empty;

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
    /// Type of the step/policy (e.g., "AssignMessage", "JavaScript").
    /// </summary>
    public string StepType      { get; init; } = string.Empty;

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
    /// Error message if the point failed.
    /// </summary>
    public string? ErrorMessage  { get; init; }

    /// <summary>
    /// Error code if the point failed.
    /// </summary>
    public string? ErrorCode     { get; init; }

    /// <summary>
    /// Gets the key-value properties/metadata for this point (e.g. policy type, phase).
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the actual flow variables modified or accessed by this point (e.g. request.header.foo).
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Variables read during the execution of this point.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> VariablesRead { get; init; } = [];

    /// <summary>
    /// Variables set during the execution of this point.
    /// </summary>
    public IReadOnlyList<(string Name, string Value, bool Success)> VariablesSet { get; init; } = [];

    /// <summary>
    /// Variables removed during the execution of this point.
    /// </summary>
    public IReadOnlyList<(string Name, bool Success)> VariablesRemoved { get; init; } = [];

    /// <summary>
    /// Gets the headers associated with a Request/Response/Error message in this point.
    /// </summary>
    public IReadOnlyDictionary<string, string> MessageHeaders { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the payload content of a Request/Response/Error message in this point.
    /// </summary>
    public string? MessageContent { get; init; }

    /// <summary>
    /// Gets the HTTP Verb if this point contains a RequestMessage.
    /// </summary>
    public string? RequestVerb { get; init; }

    /// <summary>
    /// Gets the URI if this point contains a RequestMessage.
    /// </summary>
    public string? RequestUri { get; init; }
}
