namespace MVFC.Apigee.Studio.Infrastructure.Http;

/// <summary>
/// HTTP client for the local Apigee Emulator.
///
/// Emulator endpoints (port 8080):
///   GET    /v1/emulator/healthz
///   GET    /v1/emulator/version
///   POST   /v1/emulator/deploy?environment={env}
///   POST   /v1/emulator/trace?proxyName={proxy}
///   GET    /v1/emulator/trace/transactions?sessionid={id}
///   DELETE /v1/emulator/trace?sessionid={id}
/// </summary>
public sealed class ApigeeEmulatorClient(
    HttpClient http,
    IConfiguration config,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
    private const string DefaultContainerName = "apigee-emulator";
    private readonly HttpClient _http = http;
    private readonly IConfiguration _config = config;
    private readonly ILogger<ApigeeEmulatorClient> _logger = logger;

    /// <summary>
    /// Checks if the emulator is alive by calling health and version endpoints.
    /// </summary>
    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            using var r1 = await _http.GetAsync("/v1/emulator/healthz", ct);

            if (r1.IsSuccessStatusCode)
                return true;

            using var r2 = await _http.GetAsync("/v1/emulator/version", ct);
            return r2.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogEmulatorNotHealth(ex);
            return false;
        }
    }

    /// <summary>
    /// Deploys a bundle (proxy or shared flow) to the emulator.
    /// </summary>
    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        var url = "/v1/emulator/deploy?environment=" + Uri.EscapeDataString(environment);
        _logger.LogDeployApi(zipPath, url);

        await using var fs = File.OpenRead(zipPath);
        using var content = new StreamContent(fs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var resp = await _http.PostAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Deploy falhou ({(int)resp.StatusCode}): {body}");
        }
    }

    /// <summary>
    /// Lists available Docker images for the emulator.
    /// Merges the configured default list with locally available images via `docker images`.
    /// Falls back to the default list if Docker is unavailable.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default)
    {
        var defaultImages = _config
            .GetSection("ApigeeEmulator:DefaultImages")
            .Get<List<string>>()
            ?? [
                "gcr.io/apigee-release/hybrid/apigee-emulator:1.12.0",
                "gcr.io/apigee-release/hybrid/apigee-emulator:1.11.0",
                "gcr.io/apigee-release/hybrid/apigee-emulator:1.10.0"
            ];

        var images = new List<string>(defaultImages);

        try
        {
            var psi = new ProcessStartInfo("docker", "images --format \"{{.Repository}}:{{.Tag}}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                var output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                foreach (var img in output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(l => l.Contains("apigee", StringComparison.OrdinalIgnoreCase))
                    .Where(l => !images.Contains(l)))
                    images.Add(img);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDockerImageNotAvailable(ex);
        }

        return images;
    }

    /// <summary>
    /// Starts the emulator Docker container with the specified image.
    /// </summary>
    public async Task StartContainerAsync(string image, CancellationToken ct = default)
    {
        var port = _config["ApigeeEmulator:Port"] ?? "8080";
        await RunDockerAsync($"run -d --rm -p {port}:8080 -p 8998:8998 --name {DefaultContainerName} {image}", ct);
    }

    /// <summary>
    /// Stops the emulator Docker container.
    /// </summary>
    public async Task StopContainerAsync(CancellationToken ct = default)
        => await RunDockerAsync($"stop {DefaultContainerName}", ct);

    /// <summary>
    /// Starts a trace session for the specified proxy.
    /// </summary>
    public async Task<TraceSession> StartTraceAsync(string proxyName, CancellationToken ct = default)
    {
        var url = $"/v1/emulator/trace?proxyName={Uri.EscapeDataString(proxyName)}";
        _logger.LogStartTraceSession(proxyName);

        using var response = await _http.PostAsync(url, null, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Trace start falhou ({(int)response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var sessionId = TraceJsonParser.TryGetString(json, "name")
                       ?? TraceJsonParser.TryGetString(json, "sessionId")
                       ?? Guid.NewGuid().ToString("N");
        var application = TraceJsonParser.TryGetString(json, "application") ?? proxyName;

        return new TraceSession
        {
            SessionId = sessionId,
            ApiProxy = proxyName,
            Application = application,
            StartedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the trace transactions captured so far for the active session.
    /// Delegate all parsing to <see cref="TraceJsonParser"/>.
    /// </summary>
    public async Task<IReadOnlyList<TraceTransaction>> GetTraceTransactionsAsync(
        string sessionId, CancellationToken ct = default)
    {
        var url = $"/v1/emulator/trace/transactions?sessionid={Uri.EscapeDataString(sessionId)}";
        using var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"GetTraceTransactions falhou ({(int)response.StatusCode}): {body}");
        }

        var root = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return TraceJsonParser.ParseTransactions(root);
    }

    /// <summary>
    /// Stops the active trace session.
    /// </summary>
    public async Task StopTraceAsync(string sessionId, CancellationToken ct = default)
    {
        var url = $"/v1/emulator/trace?sessionid={Uri.EscapeDataString(sessionId)}";
        using var response = await _http.DeleteAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            _logger.LogStopTrace(response.StatusCode, sessionId);
        else
            _logger.LogTraceSessionStopped(sessionId);
    }

    /// <summary>
    /// Runs a Docker command. Throws <see cref="InvalidOperationException"/> on non-zero exit.
    /// </summary>
    private static async Task RunDockerAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("docker", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar o processo docker.");

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"docker {args} falhou: {err}");
        }
    }
}