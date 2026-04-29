namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents the visual structure of an Apigee Flow (PreFlow, PostFlow or Conditional Flow).
/// </summary>
/// <param name="Name">The name of the flow.</param>
/// <param name="Type">The flow type (e.g., "PreFlow", "PostFlow", "Flow").</param>
/// <param name="RequestSteps">The steps in the request phase.</param>
/// <param name="ResponseSteps">The steps in the response phase.</param>
/// <param name="Condition">Optional condition for the flow.</param>
public sealed record FlowStructure(
    string Name,
    string Type, // "PreFlow", "PostFlow", "Flow"
    IReadOnlyList<FlowStep> RequestSteps,
    IReadOnlyList<FlowStep> ResponseSteps,
    string? Condition = null
);
