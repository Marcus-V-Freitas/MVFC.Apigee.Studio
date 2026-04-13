using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para o Apigee Emulator local.
///
/// Endpoints do emulator (porta 8080):
///   GET    /v1/emulator/version
///   GET    /v1/emulator/healthz
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

    // ── Health check ───────────────────────────────────────────────────────
    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync("/v1/emulator/healthz", ct);
            if (response.IsSuccessStatusCode) return true;

            // fallback para o endpoint de versão usado originalmente
            using var v = await http.GetAsync("/v1/emulator/version", ct);
            return v.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Emulator health check failed");
            return false;
        }
    }

    // ── Deploy ─────────────────────────────────────────────────────────────
    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
        => await PostZipAsync("/v1/emulator/deploy?environment=" + Uri.EscapeDataString(environment), zipPath, ct);

    private async Task PostZipAsync(string url, string zipPath, CancellationToken ct)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        await using var fs      = File.OpenRead(zipPath);
        using var       content = new StreamContent(fs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        logger.LogInformation("Deploying {Zip} -> {Url}", zipPath, url);
        var response = await http.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Deploy falhou ({(int)response.StatusCode} {response.StatusCode}): {body}");
        }
    }

    // ── Docker images / container ─────────────────────────────────────────
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
        catch { /* docker não instalado ou acessível */ }

        return Task.FromResult<IReadOnlyList<string>>(images);
    }

    public async Task StartContainerAsync(string image, CancellationToken ct = default)
    {
        var port = config["ApigeeEmulator:Port"] ?? "8080";
        await RunDockerAsync($"run -d --rm -p {port}:8080 --name {DefaultContainerName} {image}", ct);
    }

    public async Task StopContainerAsync(CancellationToken ct = default)
        => await RunDockerAsync($"stop {DefaultContainerName}", ct);

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

    // ── TRACE ─────────────────────────────────────────────────────────────

    public async Task<TraceSession> StartTraceAsync(string proxyName, CancellationToken ct = default)
    {
        var url = $"/v1/emulator/trace?proxyName={Uri.EscapeDataString(proxyName)}";
        logger.LogInformation("Starting trace session for proxy '{Proxy}'", proxyName);

        var response = await http.PostAsync(url, null, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Trace start falhou ({(int)response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        var sessionId   = TryGetString(json, "name")        ?? TryGetString(json, "sessionId") ?? Guid.NewGuid().ToString("N");
        var application = TryGetString(json, "application") ?? proxyName;

        return new TraceSession
        {
            SessionId   = sessionId,
            ApiProxy    = proxyName,
            Application = application,
            StartedAt   = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<TraceTransaction>> GetTraceTransactionsAsync(
        string sessionId, CancellationToken ct = default)
    {
        var url      = $"/v1/emulator/trace/transactions?sessionid={Uri.EscapeDataString(sessionId)}";
        var response = await http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"GetTraceTransactions falhou ({(int)response.StatusCode}): {body}");
        }

        var root = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return ParseTransactions(root);
    }

    public async Task StopTraceAsync(string sessionId, CancellationToken ct = default)
    {
        var url      = $"/v1/emulator/trace?sessionid={Uri.EscapeDataString(sessionId)}";
        var response = await http.DeleteAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            logger.LogWarning("StopTrace retornou {Status} para sessão {SessionId}",
                (int)response.StatusCode, sessionId);
        else
            logger.LogInformation("Trace session '{SessionId}' encerrada", sessionId);
    }

    // ── Parsing ───────────────────────────────────────────────────────────

    private static IReadOnlyList<TraceTransaction> ParseTransactions(JsonElement root)
    {
        var result  = new List<TraceTransaction>();
        var session = root.TryGetProperty("DebugSession", out var ds) ? ds : root;

        if (!session.TryGetProperty("Messages", out var messages))
            return result;

        foreach (var msg in messages.EnumerateArray())
        {
            var points = new List<TracePoint>();

            if (msg.TryGetProperty("tracePoints", out var tps))
                foreach (var tp in tps.EnumerateArray())
                    points.Add(ParseTracePoint(tp));

            result.Add(new TraceTransaction
            {
                MessageId     = TryGetString(msg, "messageId")     ?? string.Empty,
                RequestMethod = TryGetString(msg, "requestMethod") ?? string.Empty,
                RequestUri    = TryGetString(msg, "requestURI")    ?? string.Empty,
                ResponseCode  = msg.TryGetProperty("responseCode", out var rc) ? rc.GetInt32() : 0,
                TotalTimeMs   = msg.TryGetProperty("totalTime",    out var tt) ? tt.GetInt64()  : 0,
                Points        = points
            });
        }

        return result;
    }

    private static TracePoint ParseTracePoint(JsonElement tp)
    {
        var variables = new Dictionary<string, string>();

        if (tp.TryGetProperty("properties", out var props))
            foreach (var p in props.EnumerateObject())
                variables[p.Name] = p.Value.ToString();

        return new TracePoint
        {
            PointType     = TryGetString(tp, "pointType")   ?? string.Empty,
            PolicyName    = TryGetString(tp, "id")           ?? TryGetString(tp, "policyName") ?? string.Empty,
            Phase         = TryGetString(tp, "properties", "TO") ?? TryGetString(tp, "phase") ?? string.Empty,
            Description   = TryGetString(tp, "description") ?? string.Empty,
            ElapsedTimeMs = tp.TryGetProperty("elapsedTime", out var el) ? el.GetInt64() : 0,
            HasError      = tp.TryGetProperty("pointType",   out var pt) && pt.GetString() == "Error",
            Variables     = variables
        };
    }

    private static string? TryGetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string? TryGetString(JsonElement el, string outer, string inner)
    {
        if (!el.TryGetProperty(outer, out var o)) return null;
        return TryGetString(o, inner);
    }
}
