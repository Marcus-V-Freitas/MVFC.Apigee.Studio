namespace MVFC.Apigee.Studio.Infrastructure.Http;

/// <summary>
/// Cliente HTTP para o Apigee Emulator local.
///
/// Endpoints do emulator (porta 8080):
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

    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            using var r1 = await http.GetAsync("/v1/emulator/healthz", ct);

            if (r1.IsSuccessStatusCode) 
                return true;
            
            using var r2 = await http.GetAsync("/v1/emulator/version", ct);
            return r2.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Emulator health check failed");
            return false;
        }
    }

    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        var url = "/v1/emulator/deploy?environment=" + Uri.EscapeDataString(environment);
        logger.LogDeployApi(zipPath, url);

        await using var fs      = File.OpenRead(zipPath);
        using var       content = new StreamContent(fs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

        using var resp = await http.PostAsync(url, content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Deploy falhou ({(int)resp.StatusCode}): {body}");
        }
    }

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

    // ── TRACE ─────────────────────────────────────────────────────────────

    public async Task<TraceSession> StartTraceAsync(string proxyName, CancellationToken ct = default)
    {
        var url = $"/v1/emulator/trace?proxyName={Uri.EscapeDataString(proxyName)}";
        logger.LogStartTraceSession(proxyName);

        using var response = await http.PostAsync(url, null, ct);
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
        var url = $"/v1/emulator/trace/transactions?sessionid={Uri.EscapeDataString(sessionId)}";
        using var response = await http.GetAsync(url, ct);

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
        var url = $"/v1/emulator/trace?sessionid={Uri.EscapeDataString(sessionId)}";
        using var response = await http.DeleteAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            logger.LogStopTrace(response.StatusCode, sessionId);
        else
            logger.LogTraceSessionStopped(sessionId);
    }

    // ── Parsing ───────────────────────────────────────────────────────────
    //
    // O JSON do emulator tem um array "point" por mensagem. Cada entry tem um "id"
    // que pode ser: StateChange, FlowInfo, Condition, Execution, Paused, Resumed.
    // Mantemos a ORDEM original do array e filtramos apenas os tipos relevantes.
    //
    // StateChange  → separador de fase (From → To)
    // Condition    → avaliação de condição de flow/rota
    // Execution    → política executada

    private static List<TraceTransaction> ParseTransactions(JsonElement root)
    {
        var result = new List<TraceTransaction>();

        if (!root.TryGetProperty("Messages", out var messages))
            return result;

        foreach (var msg in messages.EnumerateArray())
        {
            if (!msg.TryGetProperty("point", out var pointArray))
                continue;

            var points = new List<TracePoint>();
            string verb = "", uri = "", statusCode = "";
            long totalMs;

            // timestamps para calcular duração total
            long firstTs = 0, lastTs = 0;

            foreach (var point in pointArray.EnumerateArray())
            {
                var pointId = TryGetString(point, "id") ?? string.Empty;

                if (!point.TryGetProperty("results", out var results))
                    continue;

                // ── Extrai verb/uri/statusCode de RequestMessage / ResponseMessage ──
                foreach (var res in results.EnumerateArray())
                {
                    var actionResult = TryGetString(res, "ActionResult");

                    if (actionResult == "RequestMessage" && string.IsNullOrEmpty(verb))
                    {
                        verb = TryGetString(res, "verb") ?? string.Empty;
                        uri  = TryGetString(res, "uRI")  ?? string.Empty;
                    }

                    if (actionResult == "ResponseMessage" && string.IsNullOrEmpty(statusCode))
                        statusCode = TryGetString(res, "statusCode") ?? string.Empty;

                    // captura timestamp do primeiro DebugInfo para calcular duração
                    if (actionResult == "DebugInfo")
                    {
                        var ts = TryGetString(res, "timestamp");
                        if (ts is not null && TryParseEmulatorTimestamp(ts, out var ms))
                        {
                            if (firstTs == 0) firstTs = ms;
                            lastTs = ms;
                        }
                    }
                }

                // ── Só processa tipos relevantes para a timeline ──
                if (pointId is not ("StateChange" or "Condition" or "Execution"))
                    continue;

                points.Add(ParseTracePoint(point, pointId));
            }

            totalMs = lastTs > firstTs ? lastTs - firstTs : 0;

            result.Add(new TraceTransaction
            {
                MessageId     = Guid.NewGuid().ToString("N"),
                RequestMethod = verb,
                RequestUri    = uri,
                ResponseCode  = int.TryParse(statusCode, out var sc) ? sc : 0,
                TotalTimeMs   = totalMs,
                Points        = points
            });
        }

        return result;
    }

    private static TracePoint ParseTracePoint(JsonElement point, string pointId)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (point.TryGetProperty("results", out var results))
        {
            foreach (var result in results.EnumerateArray())
            {
                var actionResult = TryGetString(result, "ActionResult");

                if (result.TryGetProperty("properties", out var props) && props.TryGetProperty("property", out var propArray))
                {
                    foreach (var prop in propArray.EnumerateArray())
                    {
                        var name  = TryGetString(prop, "name");
                        var value = TryGetString(prop, "value");
                        if (name is not null && value is not null)
                            variables.TryAdd(name, value);
                    }
                }

                if (result.TryGetProperty("headers", out var headersArray))
                {
                    var prefix = actionResult == "RequestMessage" ? "request.header." : 
                                 actionResult == "ResponseMessage" ? "response.header." : "message.header.";
                    foreach (var header in headersArray.EnumerateArray())
                    {
                        var name  = TryGetString(header, "name");
                        var value = TryGetString(header, "value");
                        if (name is not null && value is not null)
                            variables.TryAdd($"{prefix}{name}", value);
                    }
                }

                if (result.TryGetProperty("content", out var bodyEl) && bodyEl.ValueKind == JsonValueKind.String)
                {
                    var prefix = actionResult == "RequestMessage" ? "request." : 
                                 actionResult == "ResponseMessage" ? "response." : "message.";
                    variables.TryAdd($"{prefix}content", bodyEl.GetString() ?? string.Empty);
                }
            }
        }

        // ── Determina PolicyName por tipo ──────────────────────────────────
        // Execution  → stepDefinition-name (nome da política)
        // Condition  → Expression (ex: "\"default\" equals proxy.name")
        // StateChange→ "From → To" como label de fase
        string policyName;
        string phase;
        string description;

        if (pointId == "StateChange")
        {
            var from = variables.GetValueOrDefault("From", string.Empty);
            var to   = variables.GetValueOrDefault("To",   string.Empty);
            policyName  = to;                       // label principal = destino
            phase       = to;
            description = string.IsNullOrEmpty(from) ? to : $"{from} → {to}";
        }
        else if (pointId == "Condition")
        {
            policyName  = variables.GetValueOrDefault("Expression", "Condition");
            phase       = string.Empty;
            description = variables.GetValueOrDefault("ExpressionResult", string.Empty);
        }
        else // Execution
        {
            policyName  = variables.GetValueOrDefault("stepDefinition-name",
                          variables.GetValueOrDefault("policy.name", pointId));
            phase       = variables.GetValueOrDefault("enforcement",
                          variables.GetValueOrDefault("current.flow.direction", string.Empty));
            description = variables.GetValueOrDefault("type", string.Empty);
        }

        var hasError = variables.GetValueOrDefault("result") == "false"
                    || variables.GetValueOrDefault("failed")  == "true";

        return new TracePoint
        {
            PointType     = pointId,
            PolicyName    = policyName,
            Phase         = phase,
            Description   = description,
            ElapsedTimeMs = 0,
            HasError      = hasError,
            Variables     = variables
        };
    }

    /// <summary>Parse do formato "dd-MM-yy HH:mm:ss:fff" em milissegundos epoch.</summary>
    private static bool TryParseEmulatorTimestamp(string ts, out long epochMs)
    {
        epochMs = 0;
        if (DateTime.TryParseExact(ts, "dd-MM-yy HH:mm:ss:fff",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        {
            epochMs = new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeMilliseconds();
            return true;
        }
        return false;
    }

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
