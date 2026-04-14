using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using ApigeeLocalDev.Infrastructure.Http.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para o Apigee Emulator local.
///
/// O emulator expõe dois grupos de endpoints:
///   1. API própria  (porta 8080): /v1/emulator/healthz, /v1/emulator/deploy, ...
///   2. Management API compatível: /v1/organizations/emulator/environments/{env}/...
///      — usada para debug sessions (trace), idêntica ao Apigee Edge/Hybrid.
///
/// IMPORTANTE — estrutura real do payload de trace:
///   O endpoint GET .../debugsessions/{id}/data retorna:
///   {
///     "DebugSession": { ... },
///     "Messages": [
///       { "completed": true, "point": [ { "id": "StateChange", "results": [...] }, ... ] }
///     ]
///   }
///   Não há endpoint por messageId — todas as transações chegam inline no mesmo response.
/// </summary>
public sealed class ApigeeEmulatorClient(
    HttpClient http,
    IConfiguration config,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
    private const string DefaultContainerName = "apigee-emulator";
    private const string Org                  = "emulator";

    // ── Health check ───────────────────────────────────────────────────────
    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            using var r1 = await http.GetAsync("/v1/emulator/healthz", ct);
            if (r1.IsSuccessStatusCode) return true;

            using var r2 = await http.GetAsync("/v1/emulator/version", ct);
            return r2.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Emulator health check failed");
            return false;
        }
    }

    // ── Deploy ─────────────────────────────────────────────────────────────
    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        var url = "/v1/emulator/deploy?environment=" + Uri.EscapeDataString(environment);
        logger.LogInformation("Deploying {Zip} -> {Url}", zipPath, url);

        await using var fs      = File.OpenRead(zipPath);
        using var       content = new StreamContent(fs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var resp = await http.PostAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Deploy falhou ({(int)resp.StatusCode}): {body}");
        }
    }

    // ── Docker ────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default)
    {
        var images = new List<string>
        {
            "gcr.io/apigee-release/hybrid/apigee-emulator:latest",
            "gcr.io/apigee-release/hybrid/apigee-emulator:1.12.0",
            "gcr.io/apigee-release/hybrid/apigee-emulator:1.11.0",
            "gcr.io/apigee-release/hybrid/apigee-emulator:1.10.0"
        };

        try
        {
            var psi = new ProcessStartInfo("docker", "images --format \"{{.Repository}}:{{.Tag}}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                foreach (var img in output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                             .Where(l => l.Contains("apigee", StringComparison.OrdinalIgnoreCase))
                             .Where(l => !images.Contains(l)))
                    images.Add(img);
            }
        }
        catch { /* docker não disponível */ }

        return Task.FromResult<IReadOnlyList<string>>(images);
    }

    public async Task StartContainerAsync(string image, CancellationToken ct = default)
    {
        var port = config["ApigeeEmulator:Port"] ?? "8080";
        await RunDockerAsync($"run -d --rm -p {port}:8080 -p 8998:8998 --name {DefaultContainerName} {image}", ct);
    }

    public async Task StopContainerAsync(CancellationToken ct = default)
        => await RunDockerAsync($"stop {DefaultContainerName}", ct);

    // ── TRACE — Management API ─────────────────────────────────────────────
    //
    // Endpoint base:
    //   /v1/organizations/{org}/environments/{env}/apis/{proxy}/revisions/{rev}/debugsessions

    private static string DebugSessionsBase(string env, string proxy, string rev)
        => $"/v1/organizations/{Org}/environments/{Uri.EscapeDataString(env)}"
         + $"/apis/{Uri.EscapeDataString(proxy)}"
         + $"/revisions/{Uri.EscapeDataString(rev)}/debugsessions";

    public async Task<TraceSession> StartTraceAsync(
        string proxyName,
        string environment = "local",
        string revision    = "0",
        CancellationToken ct = default)
    {
        var url       = DebugSessionsBase(environment, proxyName, revision);
        var sessionId = Guid.NewGuid().ToString();
        var body      = JsonContent.Create(new { name = sessionId, timeout = "600" });

        logger.LogInformation("Starting trace session '{SessionId}' for proxy '{Proxy}'", sessionId, proxyName);

        using var resp = await http.PostAsync(url, body, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"StartTrace falhou ({(int)resp.StatusCode}): {err}");
        }

        // O emulator pode retornar o sessionId diferente — usa o do response se disponível
        var json           = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var returnedName   = TryGetString(json, "name") ?? sessionId;

        return new TraceSession
        {
            SessionId   = returnedName,
            ApiProxy    = proxyName,
            Application = proxyName,
            StartedAt   = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<TraceTransaction>> GetTraceTransactionsAsync(
        string sessionId,
        string proxyName,
        string environment = "local",
        string revision    = "0",
        CancellationToken ct = default)
    {
        // O emulator retorna TODAS as transações inline neste único endpoint.
        // Não existe subpath por messageId — diferente da Management API pública.
        var url = DebugSessionsBase(environment, proxyName, revision)
                + $"/{Uri.EscapeDataString(sessionId)}/data";

        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"GetTraceTransactions falhou ({(int)resp.StatusCode}): {err}");
        }

        var root = await resp.Content.ReadFromJsonAsync<DebugSessionResponse>(cancellationToken: ct);
        return root is null ? [] : MapTransactions(root);
    }

    public async Task StopTraceAsync(
        string sessionId,
        string proxyName,
        string environment = "local",
        string revision    = "0",
        CancellationToken ct = default)
    {
        var url = DebugSessionsBase(environment, proxyName, revision)
                + $"/{Uri.EscapeDataString(sessionId)}";

        using var resp = await http.DeleteAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
            logger.LogWarning("StopTrace retornou {Status} para sessão '{SessionId}'",
                (int)resp.StatusCode, sessionId);
        else
            logger.LogInformation("Trace session '{SessionId}' encerrada", sessionId);
    }

    // ── Mapeamento DebugSessionResponse -> TraceTransaction[] ──────────────

    private static IReadOnlyList<TraceTransaction> MapTransactions(DebugSessionResponse root)
    {
        var result = new List<TraceTransaction>(root.Messages.Count);

        for (var i = 0; i < root.Messages.Count; i++)
        {
            var msg = root.Messages[i];

            // Extrai verb/URI do primeiro StateChange com ActionResult == RequestMessage
            var reqResult = msg.Points
                .Where(p => p.Id == "StateChange")
                .SelectMany(p => p.Results)
                .FirstOrDefault(r => r.ActionResult == "RequestMessage" && r.Verb is not null);

            // Extrai statusCode do último ResponseMessage encontrado
            var respResult = msg.Points
                .SelectMany(p => p.Results)
                .LastOrDefault(r => r.ActionResult == "ResponseMessage" && r.StatusCode is not null);

            // Calcula elapsed entre primeiro e último timestamp
            var timestamps = msg.Points
                .SelectMany(p => p.Results)
                .Where(r => r.Timestamp is not null)
                .Select(r => ParseTimestamp(r.Timestamp!))
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .ToList();

            var durationMs = timestamps.Count >= 2
                ? (long)(timestamps.Max() - timestamps.Min()).TotalMilliseconds
                : 0L;

            // Mapeia apenas points com id == "Execution" como políticas executadas
            var points = msg.Points
                .Where(p => p.Id == "Execution")
                .Select(p => MapExecutionPoint(p))
                .ToList();

            // Inclui StateChanges como waypoints de fase para dar contexto na timeline
            var stateChanges = msg.Points
                .Where(p => p.Id == "StateChange")
                .Select(p => MapStateChangePoint(p))
                .ToList();

            var allPoints = stateChanges
                .Concat(points)
                .OrderBy(pt => pt.Phase)
                .ToList<TracePoint>();

            _ = int.TryParse(respResult?.StatusCode, out var statusCode);

            result.Add(new TraceTransaction(
                MessageId:    $"tx-{i}",
                RequestPath:  reqResult?.Uri    ?? "/",
                Verb:         reqResult?.Verb   ?? "GET",
                StatusCode:   statusCode,
                DurationMs:   durationMs,
                RequestBody:  null,
                ResponseBody: respResult?.Content,
                Points:       allPoints));
        }

        return result;
    }

    private static TracePoint MapExecutionPoint(DebugPoint point)
    {
        var props = point.Results
            .Where(r => r.ActionResult == "DebugInfo")
            .SelectMany(r => r.Properties?.Property ?? [])
            .ToDictionary(p => p.Name, p => p.Value);

        var policyName = props.GetValueOrDefault("stepDefinition-name",
                         props.GetValueOrDefault("stepDefinition-displayName", string.Empty));
        var phase      = props.GetValueOrDefault("enforcement", string.Empty); // "request" | "response"
        var result     = props.GetValueOrDefault("result", "true");
        var action     = props.GetValueOrDefault("action", "CONTINUE");
        var hasError   = action == "FAULT" || props.GetValueOrDefault("stepDefinition-failed") == "true";
        var executed   = result == "true" && !hasError;

        // Timestamp do DebugInfo para ordenação
        var ts = point.Results
            .FirstOrDefault(r => r.ActionResult == "DebugInfo")?.Timestamp ?? string.Empty;

        return new TracePoint(
            Policy:     policyName,
            Phase:      phase,
            Executed:   executed,
            Error:      hasError,
            DurationMs: 0,
            Condition:  props.GetValueOrDefault("stepDefinition-type", string.Empty),
            Variables:  props);
    }

    private static TracePoint MapStateChangePoint(DebugPoint point)
    {
        var props = point.Results
            .Where(r => r.ActionResult == "DebugInfo")
            .SelectMany(r => r.Properties?.Property ?? [])
            .ToDictionary(p => p.Name, p => p.Value);

        var from  = props.GetValueOrDefault("From", string.Empty);
        var to    = props.GetValueOrDefault("To",   string.Empty);
        var label = string.IsNullOrEmpty(from) ? to : $"{from} → {to}";

        return new TracePoint(
            Policy:     string.Empty,
            Phase:      label,
            Executed:   true,
            Error:      false,
            DurationMs: 0,
            Condition:  null,
            Variables:  props);
    }

    private static DateTime? ParseTimestamp(string ts)
    {
        // Formato do emulator: "14-04-26 00:06:47:660" (dd-MM-yy HH:mm:ss:fff)
        if (DateTime.TryParseExact(ts, "dd-MM-yy HH:mm:ss:fff",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string? TryGetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static async Task RunDockerAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("docker", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
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
