namespace MVFC.Apigee.Studio.Domain.Interfaces;

using MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Validates the XML content of a policy file against the Apigee schema.
/// </summary>
public interface IPolicyValidator
{
    /// <summary>
    /// Validates the XML content of a policy file against the Apigee schema.
    /// Returns an empty list if valid.
    /// </summary>
    /// <param name="xmlContent">The raw XML content of the policy.</param>
    /// <param name="policyType">The type of the policy (e.g., "AssignMessage").</param>
    /// <returns>A list of validation errors, if any.</returns>
    IReadOnlyList<PolicyValidationError> Validate(string xmlContent, string policyType);
}