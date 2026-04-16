using System.Xml.Linq;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Infrastructure.Templates;

public sealed class PolicyTemplateRepository : IPolicyTemplateRepository
{
    private static readonly IReadOnlyDictionary<string, Func<IDictionary<string, string>, XElement>> _builders = 
        new Dictionary<string, Func<IDictionary<string, string>, XElement>>(StringComparer.OrdinalIgnoreCase)
    {
        ["AssignMessage"] = p => new XElement("AssignMessage",
            new XAttribute("name", p["PolicyName"]),
            new XElement("AssignTo", new XAttribute("createNew", "false"), new XAttribute("type", "request")),
            new XElement("Set",
                new XElement("Headers",
                    new XElement("Header", new XAttribute("name", p["HeaderName"]), p["HeaderValue"])
                )
            ),
            new XElement("IgnoreUnresolvedVariables", "false")
        ),

        ["ExtractVariables"] = p => new XElement("ExtractVariables",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Source", p["Source"]),
            new XElement("QueryParam", new XAttribute("name", p["QueryParam"]),
                new XElement("Pattern", new XAttribute("ignoreCase", "true"), p["Pattern"])
            ),
            new XElement("VariablePrefix", p["VariablePrefix"]),
            new XElement("IgnoreUnresolvedVariables", "true")
        ),

        ["ResponseCache"] = p => new XElement("ResponseCache",
            new XAttribute("name", p["PolicyName"]),
            new XElement("CacheKey",
                new XElement("KeyFragment", new XAttribute("ref", p["CacheKeyRef"]), new XAttribute("type", "string"))
            ),
            new XElement("ExpirySettings",
                new XElement("TimeoutInSeconds", p["TimeoutSeconds"])
            ),
            new XElement("SkipCacheLookup", "false"),
            new XElement("SkipCachePopulation", "false")
        ),

        ["RaiseFault"] = p => new XElement("RaiseFault",
            new XAttribute("name", p["PolicyName"]),
            new XElement("FaultResponse",
                new XElement("Set",
                    new XElement("StatusCode", p["StatusCode"]),
                    new XElement("ReasonPhrase", p["ReasonPhrase"]),
                    new XElement("Payload", new XAttribute("contentType", "application/json"),
                        $"{{\"error\":\"{p["ReasonPhrase"]}\"}}"
                    )
                )
            ),
            new XElement("IgnoreUnresolvedVariables", "true")
        ),

        ["MessageLogging"] = p => new XElement("MessageLogging",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Syslog",
                new XElement("Message", p["MessageTemplate"]),
                new XElement("Host", p["SyslogHost"]),
                new XElement("Port", p["SyslogPort"]),
                new XElement("Protocol", "UDP"),
                new XElement("FormatMessage", "true")
            ),
            new XElement("logLevel", "INFO")
        ),

        ["ServiceCallout"] = p => new XElement("ServiceCallout",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Request", new XAttribute("variable", p["RequestVar"])),
            new XElement("Response", p["ResponseVar"]),
            new XElement("HTTPTargetConnection",
                new XElement("URL", p["TargetURL"])
            )
        ),

        ["JSONToXML"] = p => new XElement("JSONToXML",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Source", p["Source"]),
            new XElement("OutputVariable", p["OutputVariable"]),
            new XElement("Options",
                new XElement("OmitXMLDeclaration", "true"),
                new XElement("DefaultNamespaceNodeName", "nil"),
                new XElement("NamespaceSeparator", ":")
            )
        ),

        ["XMLToJSON"] = p => new XElement("XMLToJSON",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Source", p["Source"]),
            new XElement("OutputVariable", p["OutputVariable"]),
            new XElement("Options",
                new XElement("RecognizeNumber", "true"),
                new XElement("RecognizeBoolean", "true"),
                new XElement("RecognizeNull", "true")
            )
        ),

        ["KeyValueMapOperations"] = p => new XElement("KeyValueMapOperations",
            new XAttribute("name", p["PolicyName"]),
            new XAttribute("mapIdentifier", p["MapName"]),
            new XElement("Scope", "environment"),
            new XElement("Get", new XAttribute("assignTo", p["AssignTo"]), new XAttribute("index", "1"),
                new XElement("Key",
                    new XElement("Parameter", p["KeyName"])
                )
            )
        ),

        ["VerifyAPIKey"] = p => new XElement("VerifyAPIKey",
            new XAttribute("name", p["PolicyName"]),
            new XElement("APIKey", new XAttribute("ref", "request.queryparam.apikey"))
        ),

        ["OAuthV2-VerifyToken"] = p => new XElement("OAuthV2",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Operation", "VerifyAccessToken")
        ),

        ["OAuthV2-GenerateAccessToken"] = p => new XElement("OAuthV2",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Operation", "GenerateAccessToken"),
            new XElement("ExpiresIn", p["ExpiresInMs"]),
            new XElement("SupportedGrantTypes",
                new XElement("GrantType", "client_credentials")
            ),
            new XElement("GenerateResponse", new XAttribute("enabled", "true"))
        ),

        ["OAuthV2-RefreshToken"] = p => new XElement("OAuthV2",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Operation", "RefreshAccessToken"),
            new XElement("ExpiresIn", p["ExpiresInMs"]),
            new XElement("GenerateResponse", new XAttribute("enabled", "true"))
        ),

        ["BasicAuthentication-Encode"] = p => new XElement("BasicAuthentication",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Operation", "Encode"),
            new XElement("IgnoreUnresolvedVariables", "false"),
            new XElement("User", new XAttribute("ref", p["UserVar"])),
            new XElement("Password", new XAttribute("ref", p["PasswordVar"])),
            new XElement("AssignTo", new XAttribute("createNew", "false"), "request.header.Authorization")
        ),

        ["AccessControl"] = p => new XElement("AccessControl",
            new XAttribute("name", p["PolicyName"]),
            new XElement("IPRules", new XAttribute("noRuleMatchAction", "ALLOW"),
                new XElement("MatchRule", new XAttribute("action", "DENY"),
                    new XElement("SourceAddress", new XAttribute("mask", p["CIDRMask"]), p["IPAddress"])
                )
            )
        ),

        ["HMAC"] = p => new XElement("HMAC",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Algorithm", "SHA-256"),
            new XElement("SecretKey", new XAttribute("ref", p["SecretKeyRef"])),
            new XElement("Message", new XAttribute("ref", p["MessageRef"])),
            new XElement("Output", new XAttribute("encoding", "base64"), p["OutputVar"])
        ),

        ["SpikeArrest"] = p => new XElement("SpikeArrest",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Rate", p["Rate"]),
            new XElement("UseEffectiveCount", "true")
        ),

        ["Quota"] = p => new XElement("Quota",
            new XAttribute("name", p["PolicyName"]),
            new XElement("Allow", new XAttribute("count", p["AllowCount"]), new XAttribute("countRef", $"verifyapikey.{p["VerifyAPIKeyPolicy"]}.apiproduct.developer.quota.limit")),
            new XElement("Interval", new XAttribute("ref", $"verifyapikey.{p["VerifyAPIKeyPolicy"]}.apiproduct.developer.quota.interval"), "1"),
            new XElement("TimeUnit", new XAttribute("ref", $"verifyapikey.{p["VerifyAPIKeyPolicy"]}.apiproduct.developer.quota.timeunit"), p["TimeUnit"]),
            new XElement("Identifier", new XAttribute("ref", "request.queryparam.apikey")),
            new XElement("Distributed", "false"),
            new XElement("Synchronous", "false")
        ),

        ["ConcurrentRateLimit"] = p => new XElement("ConcurrentRateLimit",
            new XAttribute("name", p["PolicyName"]),
            new XElement("AllowConnections", new XAttribute("count", p["MaxConnections"])),
            new XElement("Distributed", "false")
        ),

        ["JavaScript"] = p => new XElement("Javascript",
            new XAttribute("name", p["PolicyName"]),
            new XAttribute("timeLimit", "200"),
            new XElement("ResourceURL", $"jsc://{p["ScriptFile"]}")
        )
    };

    private static readonly IReadOnlyList<PolicyTemplate> _templates =
    [
        new PolicyTemplate("AssignMessage", "Sets or modifies HTTP request/response headers and body.", "Mediation", "", ["PolicyName", "HeaderName", "HeaderValue"]),
        new PolicyTemplate("ExtractVariables", "Extracts content from request/response into variables.", "Mediation", "", ["PolicyName", "Source", "QueryParam", "Pattern", "VariablePrefix"]),
        new PolicyTemplate("ResponseCache", "Caches backend responses to reduce latency.", "Mediation", "", ["PolicyName", "CacheKeyRef", "TimeoutSeconds"]),
        new PolicyTemplate("RaiseFault", "Generates a custom HTTP error response.", "Mediation", "", ["PolicyName", "StatusCode", "ReasonPhrase"]),
        new PolicyTemplate("MessageLogging", "Logs request/response data to a Syslog or file endpoint.", "Mediation", "", ["PolicyName", "MessageTemplate", "SyslogHost", "SyslogPort"]),
        new PolicyTemplate("ServiceCallout", "Calls an external service mid-flow and stores the response.", "Mediation", "", ["PolicyName", "RequestVar", "ResponseVar", "TargetURL"]),
        new PolicyTemplate("JSONToXML", "Converts a JSON payload to XML.", "Mediation", "", ["PolicyName", "Source", "OutputVariable"]),
        new PolicyTemplate("XMLToJSON", "Converts an XML payload to JSON.", "Mediation", "", ["PolicyName", "Source", "OutputVariable"]),
        new PolicyTemplate("KeyValueMapOperations", "Reads or writes entries in a Key Value Map (KVM).", "Mediation", "", ["PolicyName", "MapName", "AssignTo", "KeyName"]),
        new PolicyTemplate("VerifyAPIKey", "Validates API keys sent as a query parameter or header.", "Security", "", ["PolicyName"]),
        new PolicyTemplate("OAuthV2-VerifyToken", "Validates an OAuth 2.0 Bearer access token.", "Security", "", ["PolicyName"]),
        new PolicyTemplate("OAuthV2-GenerateAccessToken", "Generates an OAuth 2.0 access token (client credentials or password).", "Security", "", ["PolicyName", "ExpiresInMs"]),
        new PolicyTemplate("OAuthV2-RefreshToken", "Exchanges a refresh token for a new access token.", "Security", "", ["PolicyName", "ExpiresInMs"]),
        new PolicyTemplate("BasicAuthentication-Encode", "Encodes username+password into a Base64 Basic Auth header.", "Security", "", ["PolicyName", "UserVar", "PasswordVar"]),
        new PolicyTemplate("AccessControl", "Allows or denies access based on client IP address.", "Security", "", ["PolicyName", "IPAddress", "CIDRMask"]),
        new PolicyTemplate("HMAC", "Generates or validates an HMAC signature on a message.", "Security", "", ["PolicyName", "SecretKeyRef", "MessageRef", "OutputVar"]),
        new PolicyTemplate("SpikeArrest", "Throttles request rate to protect backend services.", "Traffic Management", "", ["PolicyName", "Rate"]),
        new PolicyTemplate("Quota", "Limits the number of calls an app can make in a time period.", "Traffic Management", "", ["PolicyName", "AllowCount", "VerifyAPIKeyPolicy", "TimeUnit"]),
        new PolicyTemplate("ConcurrentRateLimit", "Limits concurrent connections to backend target servers.", "Traffic Management", "", ["PolicyName", "MaxConnections"]),
        new PolicyTemplate("JavaScript", "Executes a JavaScript file as a custom policy step.", "Extension", "", ["PolicyName", "ScriptFile"]),
    ];

    public IReadOnlyList<PolicyTemplate> GetAll() => _templates;

    public PolicyTemplate? GetByName(string name)
        => _templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public string GeneratePolicyXml(PolicyTemplate template, IDictionary<string, string> parameters)
    {
        if (_builders.TryGetValue(template.Name, out var builder))
        {
            var element = builder(parameters);
            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), element);
            return doc.ToString();
        }

        throw new NotSupportedException($"Gerador não implementado para a política: {template.Name}");
    }
}
