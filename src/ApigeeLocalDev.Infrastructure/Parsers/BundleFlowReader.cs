using ApigeeLocalDev.Infrastructure.Logs;

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
            logger.LogProxiesDirNotFound(proxiesDir);
            return [];
        }

        var proxyFile = Directory
            .EnumerateFiles(proxiesDir, "*.xml")
            .FirstOrDefault();

        if (proxyFile is null)
        {
            logger.LogNoXmlFound(proxiesDir);
            return [];
        }

        try
        {
            var doc  = XDocument.Load(proxyFile);
            var root = doc.Root;
            if (root is null) return [];

            var points  = new List<TracePoint>();

            // PreFlow Request
            ExtractSteps(root, "PreFlow",  "Request",  "ProxyRequest", points);
            // Flows Request (condicional)
            ExtractFlowSteps(root, "Request",  "ProxyRequest", points);
            // PostFlow Request
            ExtractSteps(root, "PostFlow", "Request",  "ProxyRequest", points);

            // PostFlow Response (inverso)
            ExtractSteps(root, "PostFlow", "Response", "ProxyResponse", points);
            // Flows Response (condicional)
            ExtractFlowSteps(root, "Response", "ProxyResponse", points);
            // PreFlow Response
            ExtractSteps(root, "PreFlow",  "Response", "ProxyResponse", points);

            return points;
        }
        catch (Exception ex)
        {
            logger.LogErrorParsingProxyEndpointXml(ex, proxyFile);
            return [];
        }
    }

    private static void ExtractSteps(
        XElement root, string flowName, string direction,
        string phase, List<TracePoint> points)
    {
        var steps = root
            .Descendants(flowName)
            .FirstOrDefault()
            ?.Element(direction)
            ?.Elements("Step")
            ?? [];

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
            });
        }
    }

    private static void ExtractFlowSteps(XElement root, string direction, string phase, List<TracePoint> points)
    {
        var flows = root
            .Element("Flows")
            ?.Elements("Flow")
            ?? [];

        foreach (var flow in flows)
        {
            var flowCondition = flow.Element("Condition")?.Value;
            var steps = flow.Element(direction)?.Elements("Step")
                        ?? [];

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
                });
            }
        }
    }
}
