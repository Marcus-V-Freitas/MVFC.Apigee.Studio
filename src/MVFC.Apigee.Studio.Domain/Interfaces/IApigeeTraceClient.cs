namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// Contract for emulator management operations related to trace.
/// The trace itself is captured via TraceMiddleware — this client only
/// lists the deployed APIs to populate the selector in the UI.
/// </summary>
public interface IApigeeTraceClient
{
    /// <summary>
    /// Lists APIs deployed in an environment (Management API :8080).
    /// </summary>
    Task<IReadOnlyList<(string ApiProxy, string Revision)>> ListDeployedApisAsync(string environment, CancellationToken ct = default);
}
