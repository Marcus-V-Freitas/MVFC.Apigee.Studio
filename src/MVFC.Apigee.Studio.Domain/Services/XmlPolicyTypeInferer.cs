namespace MVFC.Apigee.Studio.Domain.Services;

/// <summary>
/// Helper service to infer the Apigee policy type from its XML content.
/// </summary>
public static class XmlPolicyTypeInferer
{
    /// <summary>
    /// Infers the policy type from the root element of the XML content.
    /// </summary>
    /// <param name="xmlContent">The raw XML content of the policy.</param>
    /// <returns>The local name of the root element, or null if malformed.</returns>
    public static string? Infer(string xmlContent)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return null;

        try
        {
            using var reader = XmlReader.Create(new StringReader(xmlContent));
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                    return reader.LocalName; // The first element is the policy type
            }
        }
        catch
        {
            // Malformed XML - return null
        }
        return null;
    }
}
