using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Infrastructure.Templates;

public sealed class PolicyTemplateRepository : IPolicyTemplateRepository
{
    private static readonly IReadOnlyList<PolicyTemplate> _templates =
    [
        new PolicyTemplate(
            "AssignMessage",
            "Sets or modifies HTTP request/response headers and body.",
            "Mediation",
            @"<AssignMessage name=""{{PolicyName}}"">
  <AssignTo createNew=""false"" type=""request""/>
  <Set>
    <Headers>
      <Header name=""X-Custom-Header"">{{HeaderValue}}</Header>
    </Headers>
  </Set>
</AssignMessage>",
            ["PolicyName", "HeaderValue"]),

        new PolicyTemplate(
            "VerifyAPIKey",
            "Validates API keys in requests.",
            "Security",
            @"<VerifyAPIKey name=""{{PolicyName}}"">
  <APIKey ref=""request.queryparam.apikey""/>
</VerifyAPIKey>",
            ["PolicyName"]),

        new PolicyTemplate(
            "SpikeArrest",
            "Throttles request rate to protect backend services.",
            "Traffic Management",
            @"<SpikeArrest name=""{{PolicyName}}"">
  <Rate>{{Rate}}</Rate>
</SpikeArrest>",
            ["PolicyName", "Rate"]),

        new PolicyTemplate(
            "OAuthV2-VerifyToken",
            "Validates OAuth 2.0 access tokens.",
            "Security",
            @"<OAuthV2 name=""{{PolicyName}}"">
  <Operation>VerifyAccessToken</Operation>
</OAuthV2>",
            ["PolicyName"]),

        new PolicyTemplate(
            "ResponseCache",
            "Caches backend responses to reduce latency.",
            "Mediation",
            @"<ResponseCache name=""{{PolicyName}}"">
  <CacheKey>
    <KeyFragment ref=""request.uri"" type=""string""/>
  </CacheKey>
  <ExpirySettings>
    <TimeoutInSeconds>{{TimeoutSeconds}}</TimeoutInSeconds>
  </ExpirySettings>
</ResponseCache>",
            ["PolicyName", "TimeoutSeconds"]),

        new PolicyTemplate(
            "RaiseFault",
            "Generates a custom error response.",
            "Mediation",
            @"<RaiseFault name=""{{PolicyName}}"">
  <FaultResponse>
    <Set>
      <StatusCode>{{StatusCode}}</StatusCode>
      <ReasonPhrase>{{ReasonPhrase}}</ReasonPhrase>
    </Set>
  </FaultResponse>
</RaiseFault>",
            ["PolicyName", "StatusCode", "ReasonPhrase"]),

        new PolicyTemplate(
            "ExtractVariables",
            "Extracts content from request/response into variables.",
            "Mediation",
            @"<ExtractVariables name=""{{PolicyName}}"">
  <Source>request</Source>
  <QueryParam name=""{{QueryParam}}"">
    <Pattern ignoreCase=""true"">{{Pattern}}</Pattern>
  </QueryParam>
  <VariablePrefix>{{VariablePrefix}}</VariablePrefix>
</ExtractVariables>",
            ["PolicyName", "QueryParam", "Pattern", "VariablePrefix"])
    ];

    public IReadOnlyList<PolicyTemplate> GetAll() => _templates;

    public PolicyTemplate? GetByName(string name)
        => _templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string GeneratePolicyXml(PolicyTemplate template, IDictionary<string, string> parameters)
    {
        var xml = template.XmlContent;
        foreach (var (key, value) in parameters)
            xml = xml.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        return xml;
    }
}
