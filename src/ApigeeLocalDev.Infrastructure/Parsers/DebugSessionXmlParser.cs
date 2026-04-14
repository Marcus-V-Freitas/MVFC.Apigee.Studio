using System.Xml.Linq;
using ApigeeLocalDev.Domain.Entities;

namespace ApigeeLocalDev.Infrastructure.Parsers;

/// <summary>
/// Faz o parse do XML retornado pela Debug API do Apigee Emulator.
/// O schema segue o formato da Management API do Apigee Edge:
///   &lt;DebugSession&gt; → &lt;Messages&gt; → &lt;Message&gt; → &lt;point id="..."&gt; → &lt;DebugInfo&gt;
///
/// Nota: este parser é mantido por compatibilidade futura.
/// O trace primário agora é capturado via JSON pelo ApigeeEmulatorClient.
/// </summary>
public static class DebugSessionXmlParser
{
    public static TraceTransaction Parse(string xml, string messageId)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Falha ao parsear XML de debug session para messageId '{messageId}'.", ex);
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
                Value = v.Element("Value")?.Value
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
