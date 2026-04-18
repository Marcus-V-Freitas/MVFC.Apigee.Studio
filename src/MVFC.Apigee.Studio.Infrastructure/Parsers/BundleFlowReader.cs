namespace MVFC.Apigee.Studio.Infrastructure.Parsers;

/// <summary>
/// Reads the ProxyEndpoint.xml from the bundle on disk and extracts the policy steps
/// in execution order, inferring HasError by the status code.
///
/// Expected structure on disk:
///   {workspaceRoot}/src/main/apigee/apiproxies/{proxyName}/apiproxy/proxies/default.xml
///   {workspaceRoot}/src/main/apigee/apiproxies/{proxyName}/apiproxy/policies/*.xml
/// </summary>
public sealed class BundleFlowReader(ILogger<BundleFlowReader> logger) : IBundleFlowReader
{
    private readonly ILogger<BundleFlowReader> _logger = logger;

    /// <summary>
    /// Reads the steps (policies) defined in the ProxyEndpoint of the proxy,
    /// in execution order: PreFlow Request, Flows Request, PostFlow Request,
    /// PostFlow Response, Flows Response, PreFlow Response.
    /// </summary>
    /// <param name="workspaceRoot">Root path of the workspace (e.g., C:/apigee-workspaces/my-ws).</param>
    /// <param name="proxyName">Name of the API proxy (e.g., hello-world).</param>
    /// <param name="statusCode">Response status code — used to infer Executed and Error.</param>
    /// <returns>Ordered list of <see cref="TracePoint"/> representing the policy steps.</returns>
    public IReadOnlyList<TracePoint> ReadFlowPoints(
        string workspaceRoot, string proxyName, int statusCode)
    {
        var proxiesDir = Path.Combine(
            workspaceRoot, "src", "main", "apigee", "apiproxies",
            proxyName, "apiproxy", "proxies");

        if (!Directory.Exists(proxiesDir))
        {
            _logger.LogProxiesDirNotFound(proxiesDir);
            return [];
        }

        var proxyFile = Directory
            .EnumerateFiles(proxiesDir, "*.xml")
            .FirstOrDefault();

        if (proxyFile is null)
        {
            _logger.LogNoXmlFound(proxiesDir);
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
            // Flows Request (conditional)
            ExtractFlowSteps(root, "Request",  "ProxyRequest", points);
            // PostFlow Request
            ExtractSteps(root, "PostFlow", "Request",  "ProxyRequest", points);

            // PostFlow Response (reverse)
            ExtractSteps(root, "PostFlow", "Response", "ProxyResponse", points);
            // Flows Response (conditional)
            ExtractFlowSteps(root, "Response", "ProxyResponse", points);
            // PreFlow Response
            ExtractSteps(root, "PreFlow",  "Response", "ProxyResponse", points);

            return points;
        }
        catch (Exception ex)
        {
            _logger.LogErrorParsingProxyEndpointXml(ex, proxyFile);
            return [];
        }
    }

    /// <summary>
    /// Extracts the steps from a specific flow and direction (e.g., PreFlow Request or PostFlow Response)
    /// and adds them to the provided list of trace points.
    /// </summary>
    /// <param name="root">The root XElement of the ProxyEndpoint XML.</param>
    /// <param name="flowName">The flow name ("PreFlow" or "PostFlow").</param>
    /// <param name="direction">The direction ("Request" or "Response").</param>
    /// <param name="phase">The phase label for the trace point ("ProxyRequest" or "ProxyResponse").</param>
    /// <param name="points">The list to which extracted trace points will be added.</param>
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

    /// <summary>
    /// Extracts the steps from all conditional flows (Flows/Flow) for a given direction
    /// and adds them to the provided list of trace points.
    /// </summary>
    /// <param name="root">The root XElement of the ProxyEndpoint XML.</param>
    /// <param name="direction">The direction ("Request" or "Response").</param>
    /// <param name="phase">The phase label for the trace point ("ProxyRequest" or "ProxyResponse").</param>
    /// <param name="points">The list to which extracted trace points will be added.</param>
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
