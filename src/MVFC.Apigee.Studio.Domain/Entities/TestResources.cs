namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Aggregate of all test resources (mock plane) for a workspace.
/// </summary>
public sealed record TestResources(
    IReadOnlyList<ApiProduct> Products,
    IReadOnlyList<Developer> Developers,
    IReadOnlyList<DeveloperApp> Apps);
