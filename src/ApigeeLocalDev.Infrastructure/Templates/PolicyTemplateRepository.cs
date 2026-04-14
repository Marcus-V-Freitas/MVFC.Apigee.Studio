using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Infrastructure.Templates;

public sealed class PolicyTemplateRepository : IPolicyTemplateRepository
{
    // All XML built via explicit string concatenation so there are zero
    // hidden carriage-returns or verbatim-string indentation artifacts.
    private static readonly IReadOnlyList<PolicyTemplate> _templates =
    [
        // ── Mediation ────────────────────────────────────────────────────
        new PolicyTemplate(
            "AssignMessage",
            "Sets or modifies HTTP request/response headers and body.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<AssignMessage name=\"{{PolicyName}}\">\n" +
            "    <AssignTo createNew=\"false\" type=\"request\"/>\n" +
            "    <Set>\n" +
            "        <Headers>\n" +
            "            <Header name=\"{{HeaderName}}\">{{HeaderValue}}</Header>\n" +
            "        </Headers>\n" +
            "    </Set>\n" +
            "    <IgnoreUnresolvedVariables>false</IgnoreUnresolvedVariables>\n" +
            "</AssignMessage>\n",
            ["PolicyName", "HeaderName", "HeaderValue"]),

        new PolicyTemplate(
            "ExtractVariables",
            "Extracts content from request/response into variables.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<ExtractVariables name=\"{{PolicyName}}\">\n" +
            "    <Source>{{Source}}</Source>\n" +
            "    <QueryParam name=\"{{QueryParam}}\">\n" +
            "        <Pattern ignoreCase=\"true\">{{Pattern}}</Pattern>\n" +
            "    </QueryParam>\n" +
            "    <VariablePrefix>{{VariablePrefix}}</VariablePrefix>\n" +
            "    <IgnoreUnresolvedVariables>true</IgnoreUnresolvedVariables>\n" +
            "</ExtractVariables>\n",
            ["PolicyName", "Source", "QueryParam", "Pattern", "VariablePrefix"]),

        new PolicyTemplate(
            "ResponseCache",
            "Caches backend responses to reduce latency.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<ResponseCache name=\"{{PolicyName}}\">\n" +
            "    <CacheKey>\n" +
            "        <KeyFragment ref=\"{{CacheKeyRef}}\" type=\"string\"/>\n" +
            "    </CacheKey>\n" +
            "    <ExpirySettings>\n" +
            "        <TimeoutInSeconds>{{TimeoutSeconds}}</TimeoutInSeconds>\n" +
            "    </ExpirySettings>\n" +
            "    <SkipCacheLookup>false</SkipCacheLookup>\n" +
            "    <SkipCachePopulation>false</SkipCachePopulation>\n" +
            "</ResponseCache>\n",
            ["PolicyName", "CacheKeyRef", "TimeoutSeconds"]),

        new PolicyTemplate(
            "RaiseFault",
            "Generates a custom HTTP error response.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<RaiseFault name=\"{{PolicyName}}\">\n" +
            "    <FaultResponse>\n" +
            "        <Set>\n" +
            "            <StatusCode>{{StatusCode}}</StatusCode>\n" +
            "            <ReasonPhrase>{{ReasonPhrase}}</ReasonPhrase>\n" +
            "            <Payload contentType=\"application/json\">\n" +
            "                {\"error\":\"{{ReasonPhrase}}\"}\n" +
            "            </Payload>\n" +
            "        </Set>\n" +
            "    </FaultResponse>\n" +
            "    <IgnoreUnresolvedVariables>true</IgnoreUnresolvedVariables>\n" +
            "</RaiseFault>\n",
            ["PolicyName", "StatusCode", "ReasonPhrase"]),

        new PolicyTemplate(
            "MessageLogging",
            "Logs request/response data to a Syslog or file endpoint.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<MessageLogging name=\"{{PolicyName}}\">\n" +
            "    <Syslog>\n" +
            "        <Message>{{MessageTemplate}}</Message>\n" +
            "        <Host>{{SyslogHost}}</Host>\n" +
            "        <Port>{{SyslogPort}}</Port>\n" +
            "        <Protocol>UDP</Protocol>\n" +
            "        <FormatMessage>true</FormatMessage>\n" +
            "    </Syslog>\n" +
            "    <logLevel>INFO</logLevel>\n" +
            "</MessageLogging>\n",
            ["PolicyName", "MessageTemplate", "SyslogHost", "SyslogPort"]),

        new PolicyTemplate(
            "ServiceCallout",
            "Calls an external service mid-flow and stores the response.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<ServiceCallout name=\"{{PolicyName}}\">\n" +
            "    <Request variable=\"{{RequestVar}}\"/>\n" +
            "    <Response>{{ResponseVar}}</Response>\n" +
            "    <HTTPTargetConnection>\n" +
            "        <URL>{{TargetURL}}</URL>\n" +
            "    </HTTPTargetConnection>\n" +
            "</ServiceCallout>\n",
            ["PolicyName", "RequestVar", "ResponseVar", "TargetURL"]),

        new PolicyTemplate(
            "JSONToXML",
            "Converts a JSON payload to XML.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<JSONToXML name=\"{{PolicyName}}\">\n" +
            "    <Source>{{Source}}</Source>\n" +
            "    <OutputVariable>{{OutputVariable}}</OutputVariable>\n" +
            "    <Options>\n" +
            "        <OmitXMLDeclaration>true</OmitXMLDeclaration>\n" +
            "        <DefaultNamespaceNodeName>nil</DefaultNamespaceNodeName>\n" +
            "        <NamespaceSeparator>:</NamespaceSeparator>\n" +
            "    </Options>\n" +
            "</JSONToXML>\n",
            ["PolicyName", "Source", "OutputVariable"]),

        new PolicyTemplate(
            "XMLToJSON",
            "Converts an XML payload to JSON.",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<XMLToJSON name=\"{{PolicyName}}\">\n" +
            "    <Source>{{Source}}</Source>\n" +
            "    <OutputVariable>{{OutputVariable}}</OutputVariable>\n" +
            "    <Options>\n" +
            "        <RecognizeNumber>true</RecognizeNumber>\n" +
            "        <RecognizeBoolean>true</RecognizeBoolean>\n" +
            "        <RecognizeNull>true</RecognizeNull>\n" +
            "    </Options>\n" +
            "</XMLToJSON>\n",
            ["PolicyName", "Source", "OutputVariable"]),

        new PolicyTemplate(
            "KeyValueMapOperations",
            "Reads or writes entries in a Key Value Map (KVM).",
            "Mediation",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<KeyValueMapOperations name=\"{{PolicyName}}\" mapIdentifier=\"{{MapName}}\">\n" +
            "    <Scope>environment</Scope>\n" +
            "    <Get assignTo=\"{{AssignTo}}\" index=\"1\">\n" +
            "        <Key>\n" +
            "            <Parameter>{{KeyName}}</Parameter>\n" +
            "        </Key>\n" +
            "    </Get>\n" +
            "</KeyValueMapOperations>\n",
            ["PolicyName", "MapName", "AssignTo", "KeyName"]),

        // ── Security ─────────────────────────────────────────────────────
        new PolicyTemplate(
            "VerifyAPIKey",
            "Validates API keys sent as a query parameter or header.",
            "Security",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<VerifyAPIKey name=\"{{PolicyName}}\">\n" +
            "    <APIKey ref=\"request.queryparam.apikey\"/>\n" +
            "</VerifyAPIKey>\n",
            ["PolicyName"]),

        new PolicyTemplate(
            "OAuthV2-VerifyToken",
            "Validates an OAuth 2.0 Bearer access token.",
            "Security",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<OAuthV2 name=\"{{PolicyName}}\">\n" +
            "    <Operation>VerifyAccessToken</Operation>\n" +
            "</OAuthV2>\n",
            ["PolicyName"]),

        new PolicyTemplate(
            "OAuthV2-GenerateAccessToken",
            "Generates an OAuth 2.0 access token (client credentials or password).",
            "Security",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<OAuthV2 name=\"{{PolicyName}}\">\n" +
            "    <Operation>GenerateAccessToken</Operation>\n" +
            "    <ExpiresIn>{{ExpiresInMs}}</ExpiresIn>\n" +
            "    <SupportedGrantTypes>\n" +
            "        <GrantType>client_credentials</GrantType>\n" +
            "    </SupportedGrantTypes>\n" +
            "    <GenerateResponse enabled=\"true\"/>\n" +
            "</OAuthV2>\n",
            ["PolicyName", "ExpiresInMs"]),

        new PolicyTemplate(
            "OAuthV2-RefreshToken",
            "Exchanges a refresh token for a new access token.",
            "Security",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<OAuthV2 name=\"{{PolicyName}}\">\n" +
            "    <Operation>RefreshAccessToken</Operation>\n" +
            "    <ExpiresIn>{{ExpiresInMs}}</ExpiresIn>\n" +
            "    <GenerateResponse enabled=\"true\"/>\n" +
            "</OAuthV2>\n",
            ["PolicyName", "ExpiresInMs"]),

        new PolicyTemplate(
            "BasicAuthentication-Encode",
            "Encodes username+password into a Base64 Basic Auth header.",
            "Security",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<BasicAuthentication name=\"{{PolicyName}}\">\n" +
            "    <Operation>Encode</Operation>\n" +
            "    <IgnoreUnresolvedVariables>false</IgnoreUnresolvedVariables>\n" +
            "    <User ref=\"{{UserVar}}\"/>\n" +
            "    <Password ref=\"{{PasswordVar}}\"/>\n" +
            "    <AssignTo createNew=\"false\">request.header.Authorization</AssignTo>\n" +
            "</BasicAuthentication>\n",
            ["PolicyName", "UserVar", "PasswordVar"]),

        new PolicyTemplate(
            "AccessControl",
            "Allows or denies access based on client IP address.",
            "Security",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<AccessControl name=\"{{PolicyName}}\">\n" +
            "    <IPRules noRuleMatchAction=\"ALLOW\">\n" +
            "        <MatchRule action=\"DENY\">\n" +
            "            <SourceAddress mask=\"{{CIDRMask}}\">{{IPAddress}}</SourceAddress>\n" +
            "        </MatchRule>\n" +
            "    </IPRules>\n" +
            "</AccessControl>\n",
            ["PolicyName", "IPAddress", "CIDRMask"]),

        new PolicyTemplate(
            "HMAC",
            "Generates or validates an HMAC signature on a message.",
            "Security",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<HMAC name=\"{{PolicyName}}\">\n" +
            "    <Algorithm>SHA-256</Algorithm>\n" +
            "    <SecretKey ref=\"{{SecretKeyRef}}\"/>\n" +
            "    <Message ref=\"{{MessageRef}}\"/>\n" +
            "    <Output encoding=\"base64\">{{OutputVar}}</Output>\n" +
            "</HMAC>\n",
            ["PolicyName", "SecretKeyRef", "MessageRef", "OutputVar"]),

        // ── Traffic Management ────────────────────────────────────────────
        new PolicyTemplate(
            "SpikeArrest",
            "Throttles request rate to protect backend services.",
            "Traffic Management",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<SpikeArrest name=\"{{PolicyName}}\">\n" +
            "    <Rate>{{Rate}}</Rate>\n" +
            "    <UseEffectiveCount>true</UseEffectiveCount>\n" +
            "</SpikeArrest>\n",
            ["PolicyName", "Rate"]),

        new PolicyTemplate(
            "Quota",
            "Limits the number of calls an app can make in a time period.",
            "Traffic Management",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<Quota name=\"{{PolicyName}}\">\n" +
            "    <Allow count=\"{{AllowCount}}\" countRef=\"verifyapikey.{{VerifyAPIKeyPolicy}}.apiproduct.developer.quota.limit\"/>\n" +
            "    <Interval ref=\"verifyapikey.{{VerifyAPIKeyPolicy}}.apiproduct.developer.quota.interval\">1</Interval>\n" +
            "    <TimeUnit ref=\"verifyapikey.{{VerifyAPIKeyPolicy}}.apiproduct.developer.quota.timeunit\">{{TimeUnit}}</TimeUnit>\n" +
            "    <Identifier ref=\"request.queryparam.apikey\"/>\n" +
            "    <Distributed>false</Distributed>\n" +
            "    <Synchronous>false</Synchronous>\n" +
            "</Quota>\n",
            ["PolicyName", "AllowCount", "VerifyAPIKeyPolicy", "TimeUnit"]),

        new PolicyTemplate(
            "ConcurrentRateLimit",
            "Limits concurrent connections to backend target servers.",
            "Traffic Management",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<ConcurrentRateLimit name=\"{{PolicyName}}\">\n" +
            "    <AllowConnections count=\"{{MaxConnections}}\"/>\n" +
            "    <Distributed>false</Distributed>\n" +
            "</ConcurrentRateLimit>\n",
            ["PolicyName", "MaxConnections"]),

        // ── Extension ─────────────────────────────────────────────────────
        new PolicyTemplate(
            "JavaScript",
            "Executes a JavaScript file as a custom policy step.",
            "Extension",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" +
            "<Javascript name=\"{{PolicyName}}\" timeLimit=\"200\">\n" +
            "    <ResourceURL>jsc://{{ScriptFile}}</ResourceURL>\n" +
            "</Javascript>\n",
            ["PolicyName", "ScriptFile"]),
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
