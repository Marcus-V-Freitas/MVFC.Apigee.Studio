using System.Xml.Linq;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Parsers;

/// <summary>
/// Lê o ProxyEndpoint.xml do bundle no disco e extrai os steps das políticas
/// em ordem de execução, inferindo HasError pelo status code.
///
/// Estrutura esperada no disco:
///   {workspaceRoot}/src/main/apigee/apiproxies/{proxyName}/apiproxy/proxies/default.xml
///   {workspaceRoot}/src/main/apigee/apiproxies/{proxyName}/apiproxy/policies/*.xml
/// </summary>
public sealed class BundleFlowReader(ILogger<BundleFlowReader> logger) : IBundleFlowReader
{
    public IReadOnlyList<TracePoint> ReadFlowPoints(
        string workspaceRoot, string proxyName, int statusCode)
    {
        var proxiesDir = Path.Combine(
            workspaceRoot, "src", "main", "apigee", "apiproxies",
            proxyName, "apiproxy", "proxies");

        if (!Directory.Exists(proxiesDir))
        {
            logger.LogDebug("ProxiesDir não encontrado: {Dir}", proxiesDir);
            return [];
        }

        var proxyFile = Directory
            .EnumerateFiles(proxiesDir, "*.xml")
            .FirstOrDefault();

        if (proxyFile is null)
        {
            logger.LogDebug("Nenhum XML em {Dir}", proxiesDir);
            return [];
        }

        try
        {
            var doc  = XDocument.Load(proxyFile);
            var root = doc.Root;
            if (root is null) return [];

            var isError = statusCode >= 400;
            var points  = new List<TracePoint>();

            // PreFlow Request
            ExtractSteps(root, "PreFlow",  "Request",  "ProxyRequest",  isError, points);
            // Flows Request (condicional)
            ExtractFlowSteps(root, "Request",  "ProxyRequest",  isError, points);
            // PostFlow Request
            ExtractSteps(root, "PostFlow", "Request",  "ProxyRequest",  isError, points);

            // PostFlow Response (inverso)
            ExtractSteps(root, "PostFlow", "Response", "ProxyResponse", isError, points);
            // Flows Response (condicional)
            ExtractFlowSteps(root, "Response", "ProxyResponse", isError, points);
            // PreFlow Response
            ExtractSteps(root, "PreFlow",  "Response", "ProxyResponse", isError, points);

            return points;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Erro ao parsear ProxyEndpoint XML: {File}", proxyFile);
            return [];
        }
    }

    private static void ExtractSteps(
        XElement root, string flowName, string direction,
        string phase, bool isError, List<TracePoint> points)
    {
        var steps = root
            .Descendants(flowName)
            .FirstOrDefault()
            ?.Element(direction)
            ?.Elements("Step")
            ?? Enumerable.Empty<XElement>();

        foreach (var step in steps)
        {
            var name      = step.Element("Name")?.Value ?? string.Empty;
            var condition = step.Element("Condition")?.Value;
            if (string.IsNullOrWhiteSpace(name)) continue;

            points.Add(new TracePoint
            {
                PointType     = "Execution",
                PolicyName    = name,
                Phase         = phase,
                Description   = condition ?? string.Empty,
                ElapsedTimeMs = 0,
                HasError      = false,
                Variables     = []
            });
        }
    }

    private static void ExtractFlowSteps(
        XElement root, string direction, string phase,
        bool isError, List<TracePoint> points)
    {
        var flows = root
            .Element("Flows")
            ?.Elements("Flow")
            ?? Enumerable.Empty<XElement>();

        foreach (var flow in flows)
        {
            var flowCondition = flow.Element("Condition")?.Value;
            var steps = flow.Element(direction)?.Elements("Step")
                        ?? Enumerable.Empty<XElement>();

            foreach (var step in steps)
            {
                var name      = step.Element("Name")?.Value ?? string.Empty;
                var condition = step.Element("Condition")?.Value ?? flowCondition;
                if (string.IsNullOrWhiteSpace(name)) continue;

                points.Add(new TracePoint
                {
                    PointType     = "Execution",
                    PolicyName    = name,
                    Phase         = phase,
                    Description   = condition ?? string.Empty,
                    ElapsedTimeMs = 0,
                    HasError      = false,
                    Variables     = []
                });
            }
        }
    }
}
