namespace MVFC.Apigee.Studio.Domain.Interfaces;

public interface IPolicyTemplateRepository
{
    IReadOnlyList<PolicyTemplate> GetAll();

    PolicyTemplate? GetByName(string name);
    
    string GeneratePolicyXml(PolicyTemplate template, IDictionary<string, string> parameters);
}
