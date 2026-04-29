namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Container for all flows within a ProxyEndpoint or TargetEndpoint.
/// </summary>
/// <param name="Name">The endpoint name.</param>
/// <param name="PreFlow">The PreFlow structure.</param>
/// <param name="Flows">The list of conditional flows.</param>
/// <param name="PostFlow">The PostFlow structure.</param>
public sealed record EndpointStructure(
    string Name,
    FlowStructure? PreFlow,
    IReadOnlyList<FlowStructure> Flows,
    FlowStructure? PostFlow
);
