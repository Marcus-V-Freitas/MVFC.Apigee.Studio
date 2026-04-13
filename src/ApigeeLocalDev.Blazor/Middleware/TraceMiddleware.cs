using System.Diagnostics;
using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Blazor.Middleware;

/// <summary>
/// Middleware de proxy reverso leve que intercepta todas as requisições
/// em /emulator-runtime/**, as encaminha ao emulator runtime (:8998)
/// e publica uma TraceTransaction no IProxyTraceService.
///
/// Convenção ASP.NET Core para middlewares:
///   - Construtor: apenas singletons (RequestDelegate, IProxyTraceService)
///   - InvokeAsync: dependências que podem ser scoped/transient (IHttpClientFactory, ILogger)
/// </summary>
public sealed class TraceMiddleware(RequestDelegate next, IProxyTraceService traceService)
{
    private const string Prefix = "/emulator-runtime";
    private const int MaxBodyBytes = 64 * 1024; // 64 KB

    public async Task InvokeAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<TraceMiddleware> logger)
    {
        if (!context.Request.Path.StartsWithSegments(Prefix, out var remaining))
        {
            await next(context);
            return;
        }

        var sw        = Stopwatch.StartNew();
        var messageId = $"tx-{Guid.NewGuid():N}"[..16];
        var verb      = context.Request.Method;
        var path      = remaining.HasValue ? remaining.Value! : "/";

        // ── Captura request body ────────────────────────────────────────
        context.Request.EnableBuffering();
        string? requestBody = null;
        if (context.Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();
            requestBody = bodyText.Length > MaxBodyBytes
                ? bodyText[..MaxBodyBytes] + "...[truncated]"
                : bodyText;
            context.Request.Body.Position = 0;
        }

        // ── Encaminha ao emulator runtime ─────────────────────────────
        var client = httpClientFactory.CreateClient("EmulatorRuntime");
        using var proxyRequest = BuildProxyRequest(context, path);

        HttpResponseMessage? proxyResponse = null;
        string? responseBody = null;
        int statusCode = 502;

        try
        {
            proxyResponse = await client.SendAsync(
                proxyRequest,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            statusCode = (int)proxyResponse.StatusCode;

            foreach (var (key, values) in proxyResponse.Headers)
                context.Response.Headers.TryAdd(key, values.ToArray());
            foreach (var (key, values) in proxyResponse.Content.Headers)
                context.Response.Headers.TryAdd(key, values.ToArray());

            context.Response.StatusCode = statusCode;

            var responseBytes = await proxyResponse.Content.ReadAsByteArrayAsync(context.RequestAborted);
            if (responseBytes.Length > 0)
            {
                responseBody = responseBytes.Length > MaxBodyBytes
                    ? System.Text.Encoding.UTF8.GetString(responseBytes, 0, MaxBodyBytes) + "...[truncated]"
                    : System.Text.Encoding.UTF8.GetString(responseBytes);
                await context.Response.Body.WriteAsync(responseBytes, context.RequestAborted);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Erro ao encaminhar requisição para o emulator runtime");
            context.Response.StatusCode = 502;
            statusCode = 502;
        }
        finally
        {
            proxyResponse?.Dispose();
        }

        sw.Stop();

        // ── Publica no trace ─────────────────────────────────────────
        traceService.Publish(new TraceTransaction(
            MessageId:    messageId,
            RequestPath:  path,
            Verb:         verb,
            StatusCode:   statusCode,
            DurationMs:   sw.ElapsedMilliseconds,
            RequestBody:  requestBody,
            ResponseBody: responseBody,
            Points:       []));
    }

    private static HttpRequestMessage BuildProxyRequest(HttpContext context, string path)
    {
        var query      = context.Request.QueryString.Value ?? string.Empty;
        var targetUri  = new Uri(path + query, UriKind.Relative);

        var request = new HttpRequestMessage
        {
            Method     = new HttpMethod(context.Request.Method),
            RequestUri = targetUri
        };

        foreach (var (key, values) in context.Request.Headers)
        {
            if (string.Equals(key, "Host", StringComparison.OrdinalIgnoreCase)) continue;
            request.Headers.TryAddWithoutValidation(key, values.ToArray());
        }

        if (context.Request.ContentLength is > 0 ||
            context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType is { } ct)
                request.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
        }

        return request;
    }
}
