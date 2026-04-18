namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents a policy template, including its metadata and XML content.
/// </summary>
/// <param name="Name">The name of the policy template.</param>
/// <param name="Description">A description of the policy template.</param>
/// <param name="Category">The category to which the policy template belongs.</param>
/// <param name="XmlContent">The XML content of the policy template.</param>
/// <param name="Parameters">A list of parameter names required by the template.</param>
public sealed record PolicyTemplate(
    string Name,
    string Description,
    string Category,
    string XmlContent,
    IReadOnlyList<string> Parameters);
