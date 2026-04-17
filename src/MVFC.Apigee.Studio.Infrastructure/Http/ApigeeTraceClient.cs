namespace MVFC.Apigee.Studio.Infrastructure.Http;

/// <summary>
/// Client for querying the Apigee Emulator Management API (:8080) to list deployed APIs.
/// The trace itself is captured by the TraceMiddleware via reverse proxy (:8998).
///
/// Example usage:
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
    private static List<(string, string)> ParseDeployments(string raw)
    {
        var result = new List<(string, string)>();
        
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;

            // Try format { "deployments": [...] } or direct array
            var deployments = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("deployments", out var dep) ? dep : root;

            foreach (var d in deployments.EnumerateArray())
            {
                var api = d.TryGetProperty("apiProxy", out var a) ? a.GetString() : null;
                var rev = d.TryGetProperty("revision",  out var r) ? r.GetString() : null;

                if (api is not null && rev is not null)
                    result.Add((api, rev));
            }
        }
        catch (JsonException) { }

        return result;
    }
}