using System.Text;
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
            """<AssignMessage name="{{PolicyName}}">\n  <AssignTo createNew=\"false\" type=\"request\"/>\n  <Set>\n    <Headers>\n      <Header name=\"X-Custom-Header\">{{HeaderValue}}</Header>\n    </Headers>\n  </Set>\n</AssignMessage>""",
            ["PolicyName", "HeaderValue"]),

        new PolicyTemplate(
            "VerifyAPIKey",
            "Validates API keys in requests.",
            "Security",
            """<VerifyAPIKey name="{{PolicyName}}">\n  <APIKey ref=\"request.queryparam.apikey\"/>\n</VerifyAPIKey>""",
            ["PolicyName"]),

        new PolicyTemplate(
            "SpikeArrest",
            "Throttles request rate to protect backend services.",
            "Traffic Management",
            """<SpikeArrest name="{{PolicyName}}">\n  <Rate>{{Rate}}</Rate>\n</SpikeArrest>""",
            ["PolicyName", "Rate"]),

        new PolicyTemplate(
            "OAuthV2-VerifyToken",
            "Validates OAuth 2.0 access tokens.",
            "Security",
            """<OAuthV2 name="{{PolicyName}}">\n  <Operation>VerifyAccessToken</Operation>\n</OAuthV2>""",
            ["PolicyName"]),

        new PolicyTemplate(
            "ResponseCache",
            "Caches backend responses to reduce latency.",
            "Mediation",
            """<ResponseCache name="{{PolicyName}}">\n  <CacheKey>\n    <KeyFragment ref=\"request.uri\" type=\"string\"/>\n  </CacheKey>\n  <ExpirySettings>\n    <TimeoutInSeconds>{{TimeoutSeconds}}</TimeoutInSeconds>\n  </ExpirySettings>\n</ResponseCache>""",
            ["PolicyName", "TimeoutSeconds"]),

        new PolicyTemplate(
            "RaiseFault",
            "Generates a custom error response.",
            "Mediation",
            """<RaiseFault name="{{PolicyName}}">\n  <FaultResponse>\n    <Set>\n      <StatusCode>{{StatusCode}}</StatusCode>\n      <ReasonPhrase>{{ReasonPhrase}}</ReasonPhrase>\n    </Set>\n  </FaultResponse>\n</RaiseFault>""",
            ["PolicyName", "StatusCode", "ReasonPhrase"]),

        new PolicyTemplate(
            "ExtractVariables",
            "Extracts content from request/response into variables.",
            "Mediation",
            """<ExtractVariables name="{{PolicyName}}">\n  <Source>request</Source>\n  <QueryParam name=\"{{QueryParam}}\">\n    <Pattern ignoreCase=\"true\">{{Pattern}}</Pattern>\n  </QueryParam>\n  <VariablePrefix>{{VariablePrefix}}</VariablePrefix>\n</ExtractVariables>""",
            ["PolicyName", "QueryParam", "Pattern", "VariablePrefix"])
    ];

    public IReadOnlyList<PolicyTemplate> GetAll() => _templates;

    public PolicyTemplate? GetByName(string name)
        => _templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string GeneratePolicyXml(PolicyTemplate template, IDictionary<string, string> parameters)
    {
        var sb = new StringBuilder(template.XmlContent);
        foreach (var (key, value) in parameters)
            sb.Replace($"{{{{{{key}}}}}}", value);
        return sb.ToString();
    }
}
