namespace MVFC.Apigee.Studio.Infrastructure.Parsers;

/// <summary>
/// Parses the XML returned by the Debug API of the Apigee Emulator.
/// The schema follows the format of the Apigee Edge Management API:
///   &lt;DebugSession&gt; → &lt;Messages&gt; → &lt;Message&gt; → &lt;Point id="..."&gt; → &lt;DebugInfo&gt;
///
/// Note: This parser is maintained for future compatibility.
/// The primary trace is now captured via JSON by ApigeeEmulatorClient.
/// </summary>
public static class DebugSessionXmlParser
{
    /// <summary>
    /// Parses a debug session XML and returns a <see cref="TraceTransaction"/>.
    /// </summary>
    /// <param name="xml">The XML string to parse.</param>
    /// <param name="messageId">The message ID for the transaction.</param>
    /// <returns>A <see cref="TraceTransaction"/> representing the parsed data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the XML cannot be parsed.</exception>
    public static TraceTransaction Parse(string xml, string messageId)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse debug session XML for messageId '{messageId}'.", ex);
        }

        var allProps = doc
            .Descendants("Property")
            .Where(p => p.Attribute("name") is not null)
            .GroupBy(p => p.Attribute("name")!.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

        var verb       = allProps.GetValueOrDefault("request.verb", "GET");
        var path       = allProps.GetValueOrDefault("request.path", "/");
        var statusCode = int.TryParse(allProps.GetValueOrDefault("response.status.code"), out var s) ? s : 0;

        var points = doc
            .Descendants("Point")
            .Select(ParsePoint)
            .Where(p => p is not null)
            .Cast<TracePoint>()
            .ToList();

        return new TraceTransaction
        {
            MessageId     = messageId,
            RequestMethod = verb,
            RequestUri    = path,
            ResponseCode  = statusCode,
            TotalTimeMs   = 0,
            Points        = points
        };
    }

    /// <summary>
    /// Parses a &lt;Point&gt; element and returns a <see cref="TracePoint"/> instance.
    /// </summary>
    /// <param name="point">The XElement representing the point.</param>
    /// <returns>A <see cref="TracePoint"/> or null if the point is invalid.</returns>
    private static TracePoint? ParsePoint(XElement point)
    {
        var pointId = point.Attribute("id")?.Value;
        if (string.IsNullOrWhiteSpace(pointId)) return null;

        var debugInfo = point.Element("DebugInfo");

        var phase = debugInfo
            ?.Element("Properties")
            ?.Elements("Property")
            .FirstOrDefault(p => p.Attribute("name")?.Value
                .Equals("phase", StringComparison.OrdinalIgnoreCase) == true)
            ?.Value ?? string.Empty;

        var description = debugInfo
            ?.Element("Properties")
            ?.Elements("Property")
            .FirstOrDefault(p => p.Attribute("name")?.Value
                .Equals("type", StringComparison.OrdinalIgnoreCase) == true)
            ?.Value ?? string.Empty;

        var policyName = debugInfo
            ?.Element("Properties")
            ?.Elements("Property")
            .FirstOrDefault(p => p.Attribute("name")?.Value
                .Equals("stepDefinition-name", StringComparison.OrdinalIgnoreCase) == true)
            ?.Value ?? pointId;

        var hasError = point.Descendants("Error").Any() ||
                       point.Descendants("Fault").Any();

        var durationMs = long.TryParse(
            point.Descendants("Property")
                 .FirstOrDefault(p => p.Attribute("name")?.Value
                     .Equals("timeInMillis", StringComparison.OrdinalIgnoreCase) == true)
                 ?.Value, out var d) ? d : 0L;

        var variables = point
            .Descendants("VariableAssignment")
            .Select(v => new
            {
                Name  = v.Element("Name")?.Value,
                v.Element("Value")?.Value,
            })
            .Where(v => !string.IsNullOrWhiteSpace(v.Name))
            .GroupBy(v => v.Name!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value ?? string.Empty);

        return new TracePoint
        {
            PointType     = pointId,
            PolicyName    = policyName,
            Phase         = phase,
            Description   = description,
            ElapsedTimeMs = durationMs,
            HasError      = hasError,
            Variables     = variables
        };
    }
}