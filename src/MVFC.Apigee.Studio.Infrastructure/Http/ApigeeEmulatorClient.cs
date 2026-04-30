namespace MVFC.Apigee.Studio.Infrastructure.Http;

/// <summary>
/// <para>HTTP client for the local Apigee Emulator.</para>
/// <para>
/// Emulator endpoints (port 8080):
///   GET    /v1/emulator/healthz
///   GET    /v1/emulator/version
///   POST   /v1/emulator/deploy?environment={env}
///   POST   /v1/emulator/trace?proxyName={proxy}
///   GET    /v1/emulator/trace/transactions?sessionid={id}
///   DELETE /v1/emulator/trace?sessionid={id}
/// </para>
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

        var bytes = await File.ReadAllBytesAsync(zipPath, ct);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var resp = await _http.PostAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Deploy falhou ({resp.StatusCode}): {body}");
        }
    }

    /// <summary>
    /// Deploys test resources (products, developers, apps) to the emulator.
    /// </summary>
    public async Task DeployTestDataAsync(string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP de dados de teste não encontrado: " + zipPath);

        const string url = "/v1/emulator/setup/tests";
        _logger.LogTestDataSending(url);

        var bytes = await File.ReadAllBytesAsync(zipPath, ct);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var resp = await _http.PostAsync(url, content, ct);
        if (resp.IsSuccessStatusCode)
        {
            _logger.LogTestDataSuccess(url);
            return;
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Erro em {url} ({resp.StatusCode}): {body}", null, resp.StatusCode);
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
                "gcr.io/apigee-release/hybrid/apigee-emulator:1.10.0",
            ];

        var images = new List<string>(defaultImages);

        try
        {
            var psi = new ProcessStartInfo("docker", "images --format \"{{.Repository}}:{{.Tag}}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                var output = await proc.StandardOutput.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);

                images.AddRange(output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Where(l => l.Contains("apigee", StringComparison.OrdinalIgnoreCase) && !images.Contains(l)));
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

        using var response = await _http.PostAsync(url, content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Trace start falhou ({response.StatusCode}): {body}");
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
            StartedAt = DateTime.UtcNow,
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
            throw new HttpRequestException($"GetTraceTransactions falhou ({response.StatusCode}): {body}");
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeveloperApp>> GetLiveDeveloperAppsAsync(CancellationToken ct = default)
    {
        const string url = "/v1/emulator/test/developerapps";
        using var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"GetLiveDeveloperApps falhou ({response.StatusCode}): {body}");
        }

        return await response.Content.ReadFromJsonAsync<List<DeveloperApp>>(cancellationToken: ct) ?? [];
    }

    /// <inheritdoc/>
    public async Task<string?> GetRunningImageAsync(CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo("docker", $"inspect {DefaultContainerName} --format \"{{{{.Config.Image}}}}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            return proc.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
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
            CreateNoWindow = true,
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

    /// <inheritdoc/>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        const string url = "/v1/emulator/reset";
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var response = await _http.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Reset falhou ({response.StatusCode}): {body}");
        }
    }

    /// <inheritdoc/>
    public async Task<System.Text.Json.Nodes.JsonNode?> GetDeploymentsAsync(CancellationToken ct = default)
    {
        const string url = "/v1/emulator/tree";
        using var response = await _http.GetAsync(url, ct);
        return await ReadJsonSafeAsync<System.Text.Json.Nodes.JsonNode>(response, ct);
    }

    /// <inheritdoc/>
    public async Task<System.Text.Json.Nodes.JsonArray?> GetProductsAsync(CancellationToken ct = default)
    {
        const string url = "/v1/emulator/test/products";
        using var response = await _http.GetAsync(url, ct);
        return await ReadJsonSafeAsync<System.Text.Json.Nodes.JsonArray>(response, ct);
    }

    /// <inheritdoc/>
    public async Task<System.Text.Json.Nodes.JsonArray?> GetDevelopersAsync(CancellationToken ct = default)
    {
        const string url = "/v1/emulator/test/developers";
        using var response = await _http.GetAsync(url, ct);
        return await ReadJsonSafeAsync<System.Text.Json.Nodes.JsonArray>(response, ct);
    }

    /// <inheritdoc/>
    public async Task<System.Text.Json.Nodes.JsonArray?> GetKeyValueMapsAsync(CancellationToken ct = default)
    {
        const string url = "/v1/emulator/test/maps";
        using var response = await _http.GetAsync(url, ct);
        return await ReadJsonSafeAsync<System.Text.Json.Nodes.JsonArray>(response, ct);
    }

    /// <inheritdoc/>
    public async Task<System.Text.Json.Nodes.JsonArray?> GetAnalyticsAsync(CancellationToken ct = default)
    {
        const string url = "/v1/emulator/analytics";
        using var response = await _http.GetAsync(url, ct);
        return await ReadJsonSafeAsync<System.Text.Json.Nodes.JsonArray>(response, ct);
    }

    private static async Task<T?> ReadJsonSafeAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : class
    {
        if (!response.IsSuccessStatusCode) return null;
        var content = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(content);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetContainerLogsAsync(CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo("docker", $"logs --tail 200 {DefaultContainerName}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return "Erro ao iniciar processo do Docker.";

            var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errorTask = proc.StandardError.ReadToEndAsync(ct);

            await proc.WaitForExitAsync(ct);

            var output = await outputTask;
            var error = await errorTask;

            return string.IsNullOrEmpty(error) ? output : $"{output}\n--- ERROS ---\n{error}";
        }
        catch (Exception ex)
        {
            return $"Erro ao buscar logs: {ex.Message}";
        }
    }
}