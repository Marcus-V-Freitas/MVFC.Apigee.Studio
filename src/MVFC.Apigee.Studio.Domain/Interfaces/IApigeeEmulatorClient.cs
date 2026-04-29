namespace MVFC.Apigee.Studio.Domain.Interfaces;

/// <summary>
/// <para>Contract for communication with the Apigee Emulator (local Docker container).</para>
/// <para>
/// Emulator endpoints (port 8080):
///   GET    /v1/emulator/healthz
///   POST   /v1/emulator/deploy?environment={env}
///   POST   /v1/emulator/trace?proxyName={proxy}             — starts a trace session
///   GET    /v1/emulator/trace/transactions?sessionid={id}  — transaction polling
///   DELETE /v1/emulator/trace?sessionid={id}               — ends session
/// </para>
/// </summary>
public interface IApigeeEmulatorClient
{
    /// <summary>
    /// Checks if the emulator is accessible.
    /// </summary>
    Task<bool> IsAliveAsync(CancellationToken ct = default);

    /// <summary>
    /// Imports and deploys a bundle (proxy or shared flow).
    /// </summary>
    Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default);

    /// <summary>
    /// Deploys test resources (products, developers, apps) to the emulator.
    /// POST /v1/emulator/testdata
    /// </summary>
    Task DeployTestDataAsync(string zipPath, CancellationToken ct = default);

    /// <summary>
    /// Lists available Docker images for the emulator.
    /// </summary>
    Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts the emulator container with the specified image.
    /// </summary>
    Task StartContainerAsync(string image, CancellationToken ct = default);

    /// <summary>
    /// Stops the emulator container.
    /// </summary>
    Task StopContainerAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts a trace session for the given proxy.
    /// POST /v1/emulator/trace?proxyName={proxyName}
    /// </summary>
    Task<TraceSession> StartTraceAsync(string proxyName, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the transactions captured so far for the active session.
    /// GET /v1/emulator/trace/transactions?sessionid={sessionId}
    /// Should be called in polling (~2 s) while the session is active.
    /// </summary>
    Task<IReadOnlyList<TraceTransaction>> GetTraceTransactionsAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Ends the active trace session.
    /// DELETE /v1/emulator/trace?sessionid={sessionId}
    /// </summary>
    Task StopTraceAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the list of developer apps currently active in the emulator, including their generated credentials.
    /// GET /v1/emulator/test/developerapps
    /// </summary>
    Task<IReadOnlyList<DeveloperApp>> GetLiveDeveloperAppsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the name:tag of the image currently running in the emulator container.
    /// Returns null if the container is not running.
    /// </summary>
    Task<string?> GetRunningImageAsync(CancellationToken ct = default);
}