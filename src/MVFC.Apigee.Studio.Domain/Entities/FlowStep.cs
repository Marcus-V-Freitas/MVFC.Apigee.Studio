namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents a single step (policy reference) within a flow.
/// </summary>
/// <param name="Name">The name of the policy.</param>
/// <param name="Condition">Optional condition for the step.</param>
/// <param name="FaultRule">Indicates if this is a fault rule step.</param>
public sealed record FlowStep(
    string Name,
    string? Condition = null,
    bool FaultRule = false
);
