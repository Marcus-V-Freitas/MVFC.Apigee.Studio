namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Um ponto de execução dentro de uma transação.
/// Mapeado de Messages[].point[] do payload do emulator.
///
/// PointType possíveis : StateChange | Execution | Condition
/// Phase     possíveis : "request" | "response"  (Execution → enforcement)
///                       valor de "To"            (StateChange, ex: "PROXY_REQ_FLOW")
/// </summary>
public sealed class TracePoint
{
    /// <summary>StateChange | Execution | Condition</summary>
    public string PointType     { get; init; } = string.Empty;

    /// <summary>
    /// Execution   → stepDefinition-name  (ex: "AM-InjetarHeader")
    /// Condition   → Expression           (ex: "\"default\" equals proxy.name")
    /// StateChange → To                   (ex: "PROXY_REQ_FLOW")
    /// </summary>
    public string PolicyName    { get; init; } = string.Empty;

    /// <summary>
    /// Execution   → enforcement ("request" | "response")
    /// StateChange → To          (ex: "TARGET_REQ_FLOW")
    /// </summary>
    public string Phase         { get; init; } = string.Empty;

    /// <summary>Valor de "type" nas properties (ex: "AssignMessageExecution").</summary>
    public string Description   { get; init; } = string.Empty;

    public long ElapsedTimeMs   { get; init; }
    public bool HasError        { get; init; }

    public IReadOnlyDictionary<string, string> Variables { get; init; }
        = new Dictionary<string, string>();
}
