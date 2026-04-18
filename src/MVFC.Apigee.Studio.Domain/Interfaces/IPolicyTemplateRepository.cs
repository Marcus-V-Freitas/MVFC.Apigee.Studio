namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Contract for accessing and generating policy templates.
/// </summary>
public interface IPolicyTemplateRepository
{
    /// <summary>
    /// Returns all available policy templates.
    /// </summary>
    IReadOnlyList<PolicyTemplate> GetAll();

    /// <summary>
    /// Returns a policy template by its name, or null if not found.
    /// </summary>
    /// <param name="name">The name of the policy template.</param>
    PolicyTemplate? GetByName(string name);

    /// <summary>
    /// Generates the policy XML from a template and parameters.
    /// </summary>
    /// <param name="policyTemplate">The policy template to use.</param>
    /// <param name="parameters">Parameters to fill in the template.</param>
    /// <returns>The generated policy XML as a string.</returns>
    string GeneratePolicyXml(PolicyTemplate policyTemplate, IDictionary<string, string> parameters);
}
