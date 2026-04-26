namespace MVFC.Apigee.Studio.Domain.Services;

/// <summary>
/// Service for generating skeleton XML and JSON files for Apigee artifacts.
/// These templates include examples to serve as documentation for the user.
/// </summary>
public static class SkeletonTemplateService
{
    private static readonly XmlWriterSettings XmlSettings = new()
    {
        Indent = true,
        IndentChars = "    ",
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        OmitXmlDeclaration = false,
    };

    private static readonly JsonSerializerOptions JsonIndentedOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Helper to convert an XDocument to a string using consistent XML writer settings.
    /// </summary>
    private static string ToXmlString(XDocument doc)
    {
        using var ms = new MemoryStream();
        using var writer = XmlWriter.Create(ms, XmlSettings);
        doc.Save(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Generates the XML for a Shared Flow Bundle descriptor.
    /// </summary>
    public static string GetSharedFlowBundleXml(string name)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("SharedFlowBundle",
                new XAttribute("name", name),
                new XElement("Description", name),
                new XElement("Revision", "1"),
                new XElement("SharedFlows",
                    new XElement("SharedFlow", "default"))));

        return ToXmlString(doc);
    }

    /// <summary>
    /// Generates the XML for a default Shared Flow.
    /// </summary>
    public static string GetSharedFlowXml(string name)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("SharedFlow",
                new XAttribute("name", name),
                new XElement("Description", "Default shared flow")));

        return ToXmlString(doc);
    }

    /// <summary>
    /// Generates a default deployments.json file.
    /// </summary>
    public static string GetDeploymentsJson()
    {
        var data = new
        {
            proxies = Array.Empty<string>(),
            sharedFlows = Array.Empty<string>(),
        };
        return JsonSerializer.Serialize(data, JsonIndentedOptions);
    }

    /// <summary>
    /// Generates a default flowhooks.json file with examples.
    /// </summary>
    public static string GetFlowhooksJson()
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {           
        };
        return JsonSerializer.Serialize(data, JsonIndentedOptions);
    }

    /// <summary>
    /// Generates a default targetservers.json file with an example.
    /// </summary>
    public static string GetTargetServersJson()
    {
        var data = new[]
        {
            new
            {
                name = "my-target-server",
                host = "localhost",
                port = 8080,
                isEnabled = true
            }
        };
        return JsonSerializer.Serialize(data, JsonIndentedOptions);
    }

    /// <summary>
    /// Generates a default maps.json (KVM) file with an example.
    /// </summary>
    public static string GetKvmJson()
    {
        var data = new[]
        {
            new
            {
                name = "my-kvm",
                scope = "environment",
                encrypted = false,
                entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "key1", "example-value" }
                }
            }
        };
        return JsonSerializer.Serialize(data, JsonIndentedOptions);
    }

    /// <summary>
    /// Generates a default caches.json file with an example.
    /// </summary>
    public static string GetCachesJson()
    {
        var data = new[]
        {
            new { name = "my-cache" }
        };
        return JsonSerializer.Serialize(data, JsonIndentedOptions);
    }

    /// <summary>
    /// Generates the XML for an API Proxy descriptor.
    /// </summary>
    public static string GetApiProxyDescriptorXml(string name)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("APIProxy",
                new XAttribute("name", name),
                new XElement("Description", name),
                new XElement("Revision", "1")));

        return ToXmlString(doc);
    }

    /// <summary>
    /// Generates the XML for a default Proxy Endpoint.
    /// </summary>
    public static string GetProxyEndpointXml(string name)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("ProxyEndpoint",
                new XAttribute("name", "default"),
                new XElement("Description", $"{name} proxy endpoint"),
                new XElement("HTTPProxyConnection",
                    new XElement("BasePath", $"/{name}"),
                    new XElement("VirtualHost", "default")),
                new XElement("PreFlow", new XAttribute("name", "PreFlow"),
                    new XElement("Request"),
                    new XElement("Response")),
                new XElement("PostFlow", new XAttribute("name", "PostFlow"),
                    new XElement("Request"),
                    new XElement("Response")),
                new XElement("Flows"),
                new XElement("RouteRule", new XAttribute("name", "default"),
                    new XElement("TargetEndpoint", "default"))));

        return ToXmlString(doc);
    }

    /// <summary>
    /// Generates the XML for a default Target Endpoint.
    /// </summary>
    public static string GetTargetEndpointXml()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement("TargetEndpoint",
                new XAttribute("name", "default"),
                new XElement("Description", "Default target endpoint"),
                new XElement("PreFlow", new XAttribute("name", "PreFlow"),
                    new XElement("Request"),
                    new XElement("Response")),
                new XElement("PostFlow", new XAttribute("name", "PostFlow"),
                    new XElement("Request"),
                    new XElement("Response")),
                new XElement("Flows"),
                new XElement("HTTPTargetConnection",
                    new XElement("URL", "https://httpbin.org/anything"))));

        return ToXmlString(doc);
    }
}
