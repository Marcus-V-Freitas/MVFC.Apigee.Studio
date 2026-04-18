namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Singleton service that receives transactions from the TraceMiddleware
/// and distributes them to Blazor consumers via IAsyncEnumerable.
/// </summary>
public interface IProxyTraceService
{
    /// <summary>
    /// Publishes a transaction captured by the middleware.
    /// </summary>
    void Publish(TraceTransaction transaction);

    /// <summary>
    /// Returns an asynchronous stream of transactions for consumption in the Blazor component.
    /// Each call receives an independent reader.
    /// </summary>
    IAsyncEnumerable<TraceTransaction> ReadAllAsync(CancellationToken ct);

    /// <summary>
    /// Indicates whether trace is active (accepting captures).
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Activates transaction capture.
    /// </summary>
    void Start();

    /// <summary>
    /// Deactivates transaction capture.
    /// </summary>
    void Stop();

    /// <summary>
    /// Registers the active workspace and proxy so that the middleware
    /// can resolve the bundle flows on disk.
    /// </summary>
    void SetActiveProxy(string workspaceRoot, string proxyName);

    /// <summary>
    /// Returns the registered workspace root and proxy name, or null if none is set.
    /// </summary>
    (string WorkspaceRoot, string ProxyName)? ActiveProxy { get; }
}