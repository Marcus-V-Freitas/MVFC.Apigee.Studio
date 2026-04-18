namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Reads the flows of an API proxy from XML files on disk
/// and returns the ordered list of TracePoints with the defined policies.
/// </summary>
public interface IBundleFlowReader
{
    /// <summary>
    /// Returns the steps (policies) defined in the proxy's ProxyEndpoint,
    /// in execution order: PreFlow Request, Flows Request, PostFlow Request,
    /// PostFlow Response, Flows Response, PreFlow Response.
    /// </summary>
    /// <param name="workspaceRoot">Workspace root path (e.g., C:/apigee-workspaces/my-ws)</param>
    /// <param name="proxyName">API proxy name (e.g., ola-mundo)</param>
    /// <param name="statusCode">Response status code — used to infer Executed and Error</param>
    IReadOnlyList<TracePoint> ReadFlowPoints(string workspaceRoot, string proxyName, int statusCode);
}
