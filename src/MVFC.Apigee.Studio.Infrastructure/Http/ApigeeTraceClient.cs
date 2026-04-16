namespace MVFC.Apigee.Studio.Infrastructure.Http;

/// <summary>
/// Consulta a Management API do emulator (:8080) para listar APIs deployadas.
/// O trace em si é capturado pelo TraceMiddleware via proxy reverso (:8998).
/// </summary>
public sealed class ApigeeTraceClient(HttpClient http, ILogger<ApigeeTraceClient> logger) : IApigeeTraceClient
{
    private const string Org = "emulator";

    private readonly HttpClient _http = http;
    private readonly ILogger<ApigeeTraceClient> _logger = logger;

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

    private static List<(string, string)> ParseDeployments(string raw)
    {
        var result = new List<(string, string)>();
        
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;

            // Tenta formato { "deployments": [...] } ou array direto
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