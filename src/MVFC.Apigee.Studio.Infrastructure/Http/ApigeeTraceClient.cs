namespace MVFC.Apigee.Studio.Infrastructure.Http;

/// <summary>
/// <para>
/// Client for querying the Apigee Emulator Management API (:8080) to list deployed APIs.
/// The trace itself is captured by the TraceMiddleware via reverse proxy (:8998).
/// </para>
/// <para>Example usage:</para>
/// <code>
/// var client = new ApigeeTraceClient(httpClient, logger);
/// var apis = await client.ListDeployedApisAsync("local");
/// // apis: [("hello-world", "1"), ("my-proxy", "2")]
/// </code>
/// </summary>
public sealed class ApigeeTraceClient(HttpClient http, ILogger<ApigeeTraceClient> logger) : IApigeeTraceClient
{
    private const string Org = "emulator";

    private readonly HttpClient _http = http;
    private readonly ILogger<ApigeeTraceClient> _logger = logger;

    /// <summary>
    /// Lists deployed API proxies in the specified environment using the Management API.
    /// </summary>
    /// <param name="environment">The environment name (e.g., "local").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tuples containing API proxy name and revision.</returns>
    public async Task<IReadOnlyList<(string ApiProxy, string Revision)>> ListDeployedApisAsync(string environment, CancellationToken ct = default)
    {
        var url = $"/v1/organizations/{Org}/environments/{Uri.EscapeDataString(environment)}/deployments";

        _logger.LogFetchDeployments(url);

        using var resp = await _http.GetAsync(url, ct);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogListDeployedApis(resp.StatusCode);
            return [];
        }

        var raw = await resp.Content.ReadAsStringAsync(ct);
        return ParseDeployments(raw);
    }

    /// <summary>
    /// Parses the deployments JSON response and extracts API proxy names and revisions.
    /// Supports both { "deployments": [...] } and direct array formats.
    /// Example input:
    /// <code>
    /// {
    ///   "deployments": [
    ///     { "apiProxy": "hello-world", "revision": "1" },
    ///     { "apiProxy": "my-proxy", "revision": "2" }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    /// <param name="raw">The raw JSON string from the API response.</param>
    /// <returns>A list of (ApiProxy, Revision) tuples.</returns>
    private List<(string, string)> ParseDeployments(string raw)
    {
        var result = new List<(string, string)>();

        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;

            // Try format { "deployments": [...] } or direct array
            var deployments = ExtractDeploymentsArray(root);

            foreach (var d in deployments.EnumerateArray())
            {
                var api = d.TryGetProperty("apiProxy", out var a) ? a.GetString() : null;
                var rev = d.TryGetProperty("revision",  out var r) ? r.GetString() : null;

                if (api is not null && rev is not null)
                    result.Add((api, rev));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogParseDeploymentsError(ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Extracts the deployments array from the root JSON element.
    /// Handles both array root and { "deployments": [...] } object formats.
    /// </summary>
    /// <param name="root">The root <see cref="JsonElement"/> of the parsed JSON.</param>
    /// <returns>
    /// The <see cref="JsonElement"/> representing the deployments array,
    /// or the root itself if already an array.
    /// </returns>
    private static JsonElement ExtractDeploymentsArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.TryGetProperty("deployments", out var dep))
            return dep;

        return root;
    }
}