using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApigeeLocalDev.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ApigeeLocalDev.Infrastructure.Http;

/// <summary>
/// Implementação do cliente para o Apigee Emulator local.
///
/// O emulator expõe a Management API no padrão Apigee Edge:
///   Control port (default 8080): Management API + /v1/emulator/* endpoints
///   Traffic port (default 8998): proxy runtime
///
/// Referências:
///   https://discuss.google.dev/t/apigee-emulator-api-documentation/85865
///   https://docs.apigee.com/api-platform/deploy/deploy-api-proxies-using-management-api
/// </summary>
public sealed class ApigeeEmulatorClient(
    HttpClient http,
    ILogger<ApigeeEmulatorClient> logger) : IApigeeEmulatorClient
{
    private const string DefaultContainerName = "apigee-emulator";

    // Org padrão do emulator local — não é configurável no container
    private const string DefaultOrg = "emulator";

    // ── Health check ──────────────────────────────────────────────
    // GET /v1/emulator/version — endpoint confirmado pela comunidade
    // https://discuss.google.dev/t/apigee-emulator-api-documentation/85865
    public async Task<bool> IsAliveAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync("/v1/emulator/version", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Deploy individual proxy/sharedflow ────────────────────────────────
    // Padrão Apigee Edge Management API:
    //   1. POST /v1/organizations/{org}/apis?action=import&name={proxy}
    //      Body: ZIP (application/octet-stream)
    //      Response JSON contém { "revision": [ "1" ] } ou similar
    //   2. POST /v1/organizations/{org}/environments/{env}/apis/{proxy}/revisions/{rev}/deployments
    public async Task DeployBundleAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        // Extrai nome do proxy do nome do ZIP (convenção: {proxyName}.zip)
        var proxyName = Path.GetFileNameWithoutExtension(zipPath);

        // ── Passo 1: importar bundle ─────────────────────────────────────────
        var importUrl = "/v1/organizations/" + DefaultOrg + "/apis?action=import&name=" + Uri.EscapeDataString(proxyName);
        logger.LogInformation("Importing bundle {Zip} as '{Proxy}' -> {Url}", zipPath, proxyName, importUrl);

        string revision;
        await using (var fs = File.OpenRead(zipPath))
        {
            using var importContent = new StreamContent(fs);
            importContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var importResp = await http.PostAsync(importUrl, importContent, ct);
            var importBody = await importResp.Content.ReadAsStringAsync(ct);

            if (!importResp.IsSuccessStatusCode)
            {
                logger.LogError("Import failed {Status}: {Body}", importResp.StatusCode, importBody);
                throw new HttpRequestException(
                    "Import falhou (" + (int)importResp.StatusCode + "): " + importBody);
            }

            revision = ExtractRevision(importBody);
            logger.LogInformation("Import ok — revision {Rev}", revision);
        }

        // ── Passo 2: deployar a revisão no environment ───────────────────────
        var deployUrl = "/v1/organizations/" + DefaultOrg
                      + "/environments/" + Uri.EscapeDataString(environment)
                      + "/apis/" + Uri.EscapeDataString(proxyName)
                      + "/revisions/" + revision
                      + "/deployments";

        logger.LogInformation("Deploying revision {Rev} to env '{Env}' -> {Url}", revision, environment, deployUrl);

        using var deployContent = new StringContent(string.Empty);
        deployContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var deployResp = await http.PostAsync(deployUrl, deployContent, ct);
        var deployBody = await deployResp.Content.ReadAsStringAsync(ct);

        if (!deployResp.IsSuccessStatusCode)
        {
            logger.LogError("Deploy failed {Status}: {Body}", deployResp.StatusCode, deployBody);
            throw new HttpRequestException(
                "Deploy falhou (" + (int)deployResp.StatusCode + "): " + deployBody);
        }

        logger.LogInformation("Deploy successful: {StatusCode}", deployResp.StatusCode);
    }

    // ── Deploy workspace archive completo ───────────────────────────────
    // POST /v1/organizations/{org}/environments/{env}:deployArchive
    public async Task DeployArchiveAsync(string environment, string zipPath, CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("ZIP não encontrado: " + zipPath);

        var url = "/v1/organizations/" + DefaultOrg
                + "/environments/" + Uri.EscapeDataString(environment)
                + ":deployArchive";

        logger.LogInformation("Deploying archive {Zip} to env '{Env}' -> {Url}", zipPath, environment, url);

        await using var fs = File.OpenRead(zipPath);
        using var content = new StreamContent(fs);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await http.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("DeployArchive failed {Status}: {Body}", response.StatusCode, body);
            throw new HttpRequestException(
                "DeployArchive falhou (" + (int)response.StatusCode + "): " + body);
        }

        logger.LogInformation("DeployArchive successful: {StatusCode}", response.StatusCode);
    }

    // ── Docker helpers ─────────────────────────────────────────────────

    public async Task StartContainerAsync(string image, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(image))
            throw new ArgumentException("Docker image must be provided.", nameof(image));

        await RunDockerAsync("rm -f " + DefaultContainerName, ct, ignoreErrors: true);
        await RunDockerAsync(
            "run -d --name " + DefaultContainerName + " -p 8080:8080 -p 8998:8998 " + image, ct);
        logger.LogInformation("Started Apigee Emulator {Name} ({Image})", DefaultContainerName, image);
    }

    public async Task StopContainerAsync(CancellationToken ct = default)
    {
        await RunDockerAsync("rm -f " + DefaultContainerName, ct, ignoreErrors: true);
        logger.LogInformation("Stopped Apigee Emulator container {Name}", DefaultContainerName);
    }

    public async Task<IReadOnlyList<string>> ListImagesAsync(CancellationToken ct = default)
    {
        var output = await RunDockerAsync(
            "images --format \"{{.Repository}}:{{.Tag}}\"", ct, ignoreErrors: true);

        return output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l)
            .ToList();
    }

    // ── Helpers privados ─────────────────────────────────────────────────

    /// <summary>
    /// Extrai o número da revisão do JSON retornado pelo endpoint de import.
    /// O emulator retorna a mesma estrutura do Apigee Edge:
    ///   { "revision": ["1"], ... }  ou  { "latestRevisionId": "1", ... }
    /// Fallback: "1" (primeira revisão após import fresco).
    /// </summary>
    private static string ExtractRevision(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Formato Apigee Edge: { "revision": ["1"] }
            if (root.TryGetProperty("revision", out var revArr)
                && revArr.ValueKind == JsonValueKind.Array)
            {
                var last = revArr.EnumerateArray().LastOrDefault();
                if (last.ValueKind != JsonValueKind.Undefined)
                    return last.GetString() ?? "1";
            }

            // Formato alternativo: { "latestRevisionId": "1" }
            if (root.TryGetProperty("latestRevisionId", out var latestRev))
                return latestRev.GetString() ?? "1";
        }
        catch
        {
            // JSON inválido ou formato desconhecido — usa revisão 1 como fallback
        }

        return "1";
    }

    private async Task<string> RunDockerAsync(
        string arguments, CancellationToken ct, bool ignoreErrors = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "docker",
            Arguments              = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = false };
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) sbOut.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) sbErr.AppendLine(e.Data); };

        if (!proc.Start())
            throw new InvalidOperationException("Failed to start docker process.");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(ct);

        var stdout = sbOut.ToString();
        var stderr = sbErr.ToString();

        if (proc.ExitCode != 0 && !ignoreErrors)
            throw new InvalidOperationException("docker " + arguments + " failed: " + stderr);

        if (!string.IsNullOrWhiteSpace(stderr))
            logger.LogDebug("docker {Args} stderr: {Err}", arguments, stderr.Trim());

        return stdout;
    }
}
