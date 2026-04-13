using System.Text.Json;
using System.Xml.Linq;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using ApigeeLocalDev.Infrastructure.Parsers;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

/// <summary>
/// Implementação da Debug API do Apigee Emulator.
/// Org padrão: "emulator".
/// </summary>
public sealed class ApigeeTraceClient(
    HttpClient http,
    ILogger<ApigeeTraceClient> logger) : IApigeeTraceClient
{
    private const string Org = "emulator";

    // ── Criar sessão ───────────────────────────────────────────────────────
    public async Task<TraceSession> CreateSessionAsync(
        string environment, string apiProxy, string revision,
        CancellationToken ct = default)
    {
        var sessionId = $"trace-{Guid.NewGuid():N}";
        var url = BuildSessionsUrl(environment, apiProxy, revision);

        logger.LogInformation("Creating debug session {Session} for {Proxy} rev {Rev}",
            sessionId, apiProxy, revision);

        var body = JsonContent.Create(new { name = sessionId, timeout = "600" });
        using var resp = await http.PostAsync(url, body, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Falha ao criar debug session ({(int)resp.StatusCode}): {raw}");

        // O emulator retorna JSON ou XML dependendo da versão; tenta JSON primeiro
        var resolvedId = TryExtractSessionId(raw) ?? sessionId;

        return new TraceSession(resolvedId, apiProxy, revision, environment, DateTime.UtcNow);
    }

    // ── Listar sessões ─────────────────────────────────────────────────────
    public async Task<IReadOnlyList<TraceSession>> ListSessionsAsync(
        string environment, string apiProxy, string revision,
        CancellationToken ct = default)
    {
        var url = BuildSessionsUrl(environment, apiProxy, revision);
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return [];

        var raw = await resp.Content.ReadAsStringAsync(ct);
        return ParseSessionList(raw, apiProxy, revision, environment);
    }

    // ── Listar IDs de transações ───────────────────────────────────────────
    public async Task<IReadOnlyList<string>> GetTransactionIdsAsync(
        string environment, string apiProxy, string revision, string sessionId,
        CancellationToken ct = default)
    {
        var url = $"{BuildSessionsUrl(environment, apiProxy, revision)}/{Uri.EscapeDataString(sessionId)}/data";
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return [];

        var raw = await resp.Content.ReadAsStringAsync(ct);
        return ParseMessageIds(raw);
    }

    // ── Detalhe de uma transação ───────────────────────────────────────────
    public async Task<TraceTransaction> GetTransactionDetailAsync(
        string environment, string apiProxy, string revision,
        string sessionId, string messageId,
        CancellationToken ct = default)
    {
        var url = $"{BuildSessionsUrl(environment, apiProxy, revision)}/{Uri.EscapeDataString(sessionId)}/data/{Uri.EscapeDataString(messageId)}";
        using var resp = await http.GetAsync(url, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Falha ao obter transação ({(int)resp.StatusCode}): {raw}");

        return DebugSessionXmlParser.Parse(raw, messageId);
    }

    // ── Deletar sessão ─────────────────────────────────────────────────────
    public async Task DeleteSessionAsync(
        string environment, string apiProxy, string revision, string sessionId,
        CancellationToken ct = default)
    {
        var url = $"{BuildSessionsUrl(environment, apiProxy, revision)}/{Uri.EscapeDataString(sessionId)}";
        using var resp = await http.DeleteAsync(url, ct);
        logger.LogInformation("Deleted debug session {Session} (status {Status})",
            sessionId, (int)resp.StatusCode);
    }

    // ── Listar APIs deployadas ─────────────────────────────────────────────
    public async Task<IReadOnlyList<(string ApiProxy, string Revision)>> ListDeployedApisAsync(
        string environment, CancellationToken ct = default)
    {
        var url = $"/v1/organizations/{Org}/environments/{Uri.EscapeDataString(environment)}/deployments";
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return [];

        var raw = await resp.Content.ReadAsStringAsync(ct);
        return ParseDeployments(raw);
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static string BuildSessionsUrl(string env, string api, string rev) =>
        $"/v1/organizations/{Org}/environments/{Uri.EscapeDataString(env)}"
        + $"/apis/{Uri.EscapeDataString(api)}/revisions/{Uri.EscapeDataString(rev)}/debugsessions";

    private static string? TryExtractSessionId(string raw)
    {
        try
        {
            var json = JsonDocument.Parse(raw);
            if (json.RootElement.TryGetProperty("name", out var name))
                return name.GetString();
        }
        catch { /* não é JSON — tenta XML */ }

        try
        {
            var xml = XDocument.Parse(raw);
            return xml.Root?.Element("name")?.Value
                   ?? xml.Root?.Attribute("name")?.Value;
        }
        catch { /* ignora */ }

        return null;
    }

    private static IReadOnlyList<TraceSession> ParseSessionList(
        string raw, string apiProxy, string revision, string environment)
    {
        var sessions = new List<TraceSession>();
        try
        {
            using var json = JsonDocument.Parse(raw);
            var arr = json.RootElement.ValueKind == JsonValueKind.Array
                ? json.RootElement
                : json.RootElement.GetProperty("debugSession");

            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (id is not null)
                    sessions.Add(new TraceSession(id, apiProxy, revision, environment, DateTime.UtcNow));
            }
        }
        catch { /* retorna lista vazia se não parsear */ }
        return sessions;
    }

    private static IReadOnlyList<string> ParseMessageIds(string raw)
    {
        try
        {
            using var json = JsonDocument.Parse(raw);
            var arr = json.RootElement.ValueKind == JsonValueKind.Array
                ? json.RootElement
                : json.RootElement.GetProperty("messageIds");

            return arr.EnumerateArray()
                      .Select(e => e.GetString())
                      .Where(id => id is not null)
                      .Cast<string>()
                      .ToList();
        }
        catch { return []; }
    }

    private static IReadOnlyList<(string, string)> ParseDeployments(string raw)
    {
        var result = new List<(string, string)>();
        try
        {
            using var json = JsonDocument.Parse(raw);
            var deployments = json.RootElement.TryGetProperty("deployments", out var dep)
                ? dep
                : json.RootElement;

            foreach (var d in deployments.EnumerateArray())
            {
                var api = d.TryGetProperty("apiProxy", out var a) ? a.GetString() : null;
                var rev = d.TryGetProperty("revision", out var r) ? r.GetString() : null;
                if (api is not null && rev is not null)
                    result.Add((api, rev));
            }
        }
        catch { /* retorna lista vazia */ }
        return result;
    }
}
